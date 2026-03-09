using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class BrevoEventPoller : BackgroundService
{
	private readonly ILogger<BrevoEventPoller> _logger;
	private readonly IConfiguration _cfg;
	private readonly IServiceProvider _sp;

	public BrevoEventPoller(ILogger<BrevoEventPoller> logger, IConfiguration cfg, IServiceProvider sp)
	{
		_logger = logger;
		_cfg = cfg;
		_sp = sp;// 注入服務工廠，用來在 Singleton 中產生 Scoped 的 AppDbContext
    }

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var minutes = _cfg.GetValue<int?>("Brevo:PollMinutes") ?? 5;
		var lookback = _cfg.GetValue<int?>("Brevo:LookbackMinutesOnStart") ?? 30;
		var apiKey = _cfg["Brevo:ApiKey"];

		if (string.IsNullOrWhiteSpace(apiKey))
		{
			_logger.LogWarning("BrevoEventPoller disabled: Brevo:ApiKey not set.");
			return;
		}

        // 主迴圈：只要 stoppingToken 沒被觸發(網站沒關閉)，就持續運行
        while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
                // 建立手動 Scope，確保 AppDbContext 能夠在背景服務中正確使用並釋放
                using var scope = _sp.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 游標管理 (Cursor)：讀取上次同步成功的時間點，防止重複抓取或漏抓
                var cursor = await db.IntegrationCursors.AsNoTracking()
					.FirstOrDefaultAsync(x => x.CursorKey == "Brevo:Events", stoppingToken);

				var startLocal = cursor?.CursorTime ?? DateTime.Now.AddMinutes(-lookback);
				var endLocal = DateTime.Now;

				// Brevo API 要求格式日期字串
				var startDate = startLocal.ToString("yyyy-MM-dd");
				var endDate = DateTime.Now.ToString("yyyy-MM-dd");

				// 事件名稱：opened / clicks
				var eventsToFetch = new[] { "opened", "clicks" };

				using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
				http.DefaultRequestHeaders.Add("api-key", apiKey);

				var baseUrl = "https://api.brevo.com/v3/smtp/statistics/events";

				var limit = 50; // 每頁抓取筆數
                var totalProcessed = 0;
				var successThisRound = false;

                // 針對開信與點擊兩種事件分別進行分頁抓取
                foreach (var ev in eventsToFetch)
				{
					var offset = 0;
					var pageIdx = 0;

					while (!stoppingToken.IsCancellationRequested)
					{
						var url = $"{baseUrl}?event={ev}&startDate={startDate}&endDate={endDate}&limit={limit}&offset={offset}&sort=asc";

						HttpResponseMessage resp = null!;

                        // 指數退避重試 (Exponential Backoff)：處理 API 頻率限制 (429) 或伺服器錯誤 (5xx)
                        for (int attempt = 0; attempt < 4; attempt++)
						{
							try
							{
								resp = await http.GetAsync(url, stoppingToken);
								var code = (int)resp.StatusCode;

								if (code == 429 || code >= 500)
								{
									var backoffMs = (int)(Math.Pow(2, attempt) * 600) + Random.Shared.Next(0, 400);
									_logger.LogWarning("Brevo events temp failure: {Status}. retry in {Delay}ms. Url={Url}",
										resp.StatusCode, backoffMs, url);
									await Task.Delay(backoffMs, stoppingToken);
									continue;
								}
								
								if (!resp.IsSuccessStatusCode)
								{
									var body = await resp.Content.ReadAsStringAsync(stoppingToken);
									_logger.LogError("Brevo events {Event} request failed {Status}. Url={Url}. Body={Body}",
										ev, resp.StatusCode, url, body);
								}

								resp.EnsureSuccessStatusCode();
								break;
							}
							catch (TaskCanceledException) when (!stoppingToken.IsCancellationRequested)
							{
                                //處理 HttpClient 超時，給予緩衝後重試
                                var backoffMs = (int)(Math.Pow(2, attempt) * 600) + Random.Shared.Next(0, 400);
								_logger.LogWarning("Brevo events timeout. retry in {Delay}ms. Url={Url}", backoffMs, url);
								await Task.Delay(backoffMs, stoppingToken);
							}
						}

						if (resp is null || !resp.IsSuccessStatusCode)
						{
							_logger.LogWarning("Brevo events fetch failed after retries. Url={Url}", url);
							break; // 請求失敗時跳出當前事件類型，不更新游標時間
                        }

                        // 解析 JSON 事件陣列
                        var json = await resp.Content.ReadAsStringAsync(stoppingToken);//把網路回傳的二進位內容轉換成String
                        using var doc = JsonDocument.Parse(json);//將String轉換為可供查詢的記憶體物件結構
                        var root = doc.RootElement;//取得 JSON 文件的「根部」

                        // 將 JSON 陣列轉換為 .NET 可迭代的 JsonElement 陣列
                        var arr = root.TryGetProperty("events", out var je) && je.ValueKind == JsonValueKind.Array
							? je.EnumerateArray().ToArray()
							: Array.Empty<JsonElement>();
                        // 每種類型的第一頁輸出採樣數據到日誌，方便除錯確認欄位內容
                        if (pageIdx == 0)
						{
							var samples = arr.Take(3).Select(e => new
							{
								eventType = e.TryGetProperty("event", out var ee) ? ee.GetString() : null,
								date = e.TryGetProperty("date", out var d) ? d.GetString() : null,
								emailHint = e.TryGetProperty("email", out var em) ? MaskEmail(em.GetString()) : null,
								msgIdRaw = e.TryGetProperty("messageId", out var mid) ? mid.GetString()
										  : (e.TryGetProperty("message-id", out var mid2) ? mid2.GetString() : null),
								msgIdNorm = NormalizeMsgId(e.TryGetProperty("messageId", out var mid3) ? mid3.GetString()
										  : (e.TryGetProperty("message-id", out var mid4) ? mid4.GetString() : null)),
								url = e.TryGetProperty("url", out var u) ? u.GetString() : null
							}).ToArray();
							_logger.LogInformation("[DBG] Brevo sample events: {Samples}", JsonSerializer.Serialize(samples));
						}
                        int pageFetched = 0, matchedLog = 0, missLog = 0, missJr = 0, dup = 0, inserted = 0;

						if (arr.Length > 0)
						{
							//收集這 50 筆中所有的 Email 與 MessageId
							var emails = arr.Select(e => e.TryGetProperty("email", out var em) ? em.GetString() : null)
										.Where(e => !string.IsNullOrEmpty(e)).Distinct().ToList();
							var mids = arr.Select(e => NormalizeMsgId(e.TryGetProperty("messageId", out var m) ? m.GetString() : null))
										  .Where(m => !string.IsNullOrEmpty(m)).ToList();

							//一次抓出所有相關 Log（動態計算抓取範圍）
							var fetchStart = startLocal.AddDays(-3);
							var allLogs = await db.MailSendLogs.AsNoTracking()
								.Where(x => x.SentAt >= fetchStart)
								.Where(x => emails.Contains(x.Recipient) || mids.Contains(x.ProviderMsgId))
								.ToListAsync();

							//建立記憶體索引
							var logById = allLogs.Where(x => !string.IsNullOrEmpty(x.ProviderMsgId))
												 .ToDictionary(x => x.ProviderMsgId!, x => x);
							var logByEmail = allLogs.GroupBy(x => x.Recipient)
													.ToDictionary(g => g.Key!, g => g.OrderByDescending(x => x.SentAt).ToList());

							foreach (var e in arr)
							{
								pageFetched++;

								var evt = e.TryGetProperty("event", out var evJ) ? evJ.GetString() : null;
								if (evt is null) continue;

								var dateStr = e.TryGetProperty("date", out var dJ) ? dJ.GetString() : null;
								var createdLocal = ParseBrevoDate(dateStr);

								// 本地端二次過濾：Brevo API 的日期篩選顆粒度較粗 (以日為單位)，所以程式須手動過濾掉小於游標的細節時間
								if (createdLocal <= startLocal) continue;

								var email = e.TryGetProperty("email", out var emJ) ? emJ.GetString() : null;

								// 處理 Brevo 不同時期或 API 版本中 MessageId 的鍵名差異
								string? messageId = null;
								if (e.TryGetProperty("messageId", out var midJ)) messageId = midJ.GetString();
								else if (e.TryGetProperty("message-id", out var mid2J)) messageId = mid2J.GetString();
								var normMid = NormalizeMsgId(messageId);

								// 讀取其他環境資訊（點擊連結、瀏覽器 Agent、IP）
								var urlClicked = e.TryGetProperty("url", out var urlJ) ? urlJ.GetString() : null;
								var ua = e.TryGetProperty("userAgent", out var uaJ) ? uaJ.GetString() : null;
								var ip = e.TryGetProperty("ip", out var ipJ) ? ipJ.GetString() : null;

								// 強化匹配策略：將外部事件匹配回系統內部的發信紀錄
								MailSendLog? log = null;

								//// a) 先以 providermsgid（正規化）嘗試精準比對
								//if (!string.isnullorwhitespace(normmid))
								//{
								//	log = await db.mailsendlogs.asnotracking()
								//		.where(x => x.sentat >= createdlocal.adddays(-3))
								//		.firstordefaultasync(x => x.providermsgid.contains(normmid), stoppingtoken);
								//}

								//// b) 以 email + 時間窗（-72h ~ +24h），取最接近 createdlocal 的一封
								//if (log == null && !string.isnullorwhitespace(email))
								//{
								//	log = await db.mailsendlogs.asnotracking()
								//		.where(x => x.recipient == email && x.sentat >= createdlocal.addhours(-72) && x.sentat <= createdlocal.addhours(24))
								//		.orderby(x => math.abs((x.sentat - createdlocal).totalseconds))
								//		.firstordefaultasync(stoppingtoken);
								//}

								//// c) 最後保底：email 最近一封發信紀錄
								//if (log == null && !string.isnullorwhitespace(email))
								//{
								//	log = await db.mailsendlogs.asnotracking()
								//		.where(x => x.recipient == email)
								//		.orderbydescending(x => x.sentat)
								//		.firstordefaultasync(stoppingtoken);
								//}

								// a) 從 ID 地圖找 (精準)
								if (!string.IsNullOrEmpty(normMid))
								{
									logById.TryGetValue(normMid, out log);
								}

								// b) 從 Email 地圖找 (時間窗最接近)
								if (log == null && !string.IsNullOrEmpty(email) && logByEmail.TryGetValue(email, out var userLogs))
								{
									// 在記憶體清單中找時間最接近的那封
									log = userLogs.Where(x => x.SentAt >= createdLocal.AddHours(-72) && x.SentAt <= createdLocal.AddHours(24))
												  .OrderBy(x => Math.Abs((x.SentAt - createdLocal).TotalSeconds))
												  .FirstOrDefault();
								}

								// c) 保底：Email 最近一封
								if (log == null && !string.IsNullOrEmpty(email) && logByEmail.TryGetValue(email, out var userLogs2))
								{
									log = userLogs2.FirstOrDefault(); // 建立 Dictionary 時已經排過序了
								}

								// 若最終還是找不到匹配紀錄，視為無效事件並記錄警告
								if (log == null)
								{
									missLog++;
									_logger.LogWarning("[Brevo] MissLog evt={Evt} email={Email} date={Date} msgId={MsgId}",
										evt, email, createdLocal, messageId);
									continue;
								}

								matchedLog++;

								// 檢查關聯日誌中是否有關聯到具體的收件人 JobRecipientId
								if (log.JobRecipientId == null)
								{
									missJr++;
									_logger.LogWarning("[Brevo] Log found but JobRecipientId is null. logId={LogId} email={Email} sentAt={SentAt}",
										log.LogId, log.Recipient, log.SentAt);
									continue;
								}

								// 獲取具體的收件人工作狀態紀錄
								var jr = await db.MailJobRecipients
									.FirstOrDefaultAsync(x => x.MailJobRecipientId == log.JobRecipientId.Value, stoppingToken);
								if (jr == null)
								{
									missJr++;
									_logger.LogWarning("[Brevo] JR not found. jobRecipientId={JRId} logId={LogId}", log.JobRecipientId, log.LogId);
									continue;
								}

								bool isOpen = evt.Equals("opened", StringComparison.OrdinalIgnoreCase) || evt.Equals("open", StringComparison.OrdinalIgnoreCase);
								bool isClick = evt.Equals("clicks", StringComparison.OrdinalIgnoreCase) || evt.Equals("click", StringComparison.OrdinalIgnoreCase);

								var newEvent = new MailEvent
								{
									MailJobId = jr.MailJobId,
									JobRecipientId = jr.MailJobRecipientId,
									LogId = log.LogId,
									EventType = isClick ? "Click" : "Open",
									Url = isClick ? urlClicked : null,
									UserAgent = ua,
									Ip = ip,
									CreatedAt = createdLocal
								};

								// 去重：JR + Type + Url + (CreatedAt ±2s)
								var createdMin = createdLocal.AddSeconds(-2);
								var createdMax = createdLocal.AddSeconds(+2);

								var isDup = await db.MailEvents.AnyAsync(x =>
									x.JobRecipientId == newEvent.JobRecipientId &&
									x.EventType == newEvent.EventType &&
									(newEvent.Url == null || x.Url == newEvent.Url) &&
									x.CreatedAt >= createdMin && x.CreatedAt <= createdMax, stoppingToken);

								if (isDup) { dup++; continue; }

								await db.MailEvents.AddAsync(newEvent, stoppingToken);

								if (isOpen)
								{
									if (jr.OpenCount == 0) jr.OpenedAt = createdLocal;
									jr.OpenCount += 1;
								}
								if (isClick)
								{
									jr.ClickCount += 1;
									jr.LastClickAt = createdLocal;
								}

								inserted++;
								totalProcessed++;
							}

						}
                        // 每一頁處理完執行一次 SaveChanges，降低長時間鎖表的風險
                        await db.SaveChangesAsync(stoppingToken);

						_logger.LogInformation("[DBG] pageSummary ev={Event} fetched={Fetched} matchedLog={Matched} missLog={MissLog} missJR={MissJR} dup={Dup} inserted={Inserted}",
							ev, pageFetched, matchedLog, missLog, missJr, dup, inserted);

						if (inserted > 0) successThisRound = true; // 只要本頁有寫入就算成功
						if (arr.Length < limit) break; // 若獲取筆數少於 limit，代表當前時間段的資料已抓完
                        offset += limit;
						pageIdx++;
					}
				}

				// 成功才推進游標（本地時間）
				if (successThisRound)
				{
					var row = await db.IntegrationCursors.FirstOrDefaultAsync(x => x.CursorKey == "Brevo:Events", stoppingToken);
					if (row == null)
						db.IntegrationCursors.Add(new IntegrationCursor { CursorKey = "Brevo:Events", CursorTime = endLocal });
					else
						row.CursorTime = endLocal;

					await db.SaveChangesAsync(stoppingToken);
				}
				else
				{
					_logger.LogWarning("Skip cursor advance due to fetch failure or no inserts.");
				}

				_logger.LogInformation("Brevo poll OK. {Start} -> {End}, eventsWritten={N}", startLocal, endLocal, totalProcessed);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "BrevoEventPoller failed");
			}

            // 依照設定分鐘數進行等待，直到下一次輪詢
            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
		}
	}

    /// <summary>
    /// 方法用途：解析 Brevo 回傳的日期字串
    /// 支援 ISO8601 與自定義日期字串
    /// </summary>
    private static DateTime ParseBrevoDate(string? s)
	{
		if (string.IsNullOrWhiteSpace(s)) return DateTime.Now;
        // 優先處理帶有時區資訊的標準 TryParse (如 ISO8601)
        if (DateTime.TryParse(s, out var dt))
			return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        // 保底處理特定的日期時間格式
        if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", null,
			System.Globalization.DateTimeStyles.AssumeLocal, out dt))
			return dt;
		return DateTime.Now;
	}

    /// <summary>
    /// 方法用途：正規化 Message-ID，移除尖括號、不可見字元並轉為小寫
    /// 目的是解決不同郵件伺服器轉發後造成的 ID 格式不一致問題	
	/// /// </summary>
    private static string? NormalizeMsgId(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))return null;

        //定義要過濾的字元陣列
        ReadOnlySpan<char> trimChars = stackalloc char[] { '<', '>', ' ', '\t', '\r', '\n' };

        //Span進行多重字元過濾
        ReadOnlySpan<char> span = s.AsSpan().Trim(trimChars);

        //如果過濾完變成空，也視為無效 ID
        if (span.IsEmpty) return null;

        //最終轉換為小寫字串
        return span.ToString().ToLowerInvariant();
    }


    /// <summary>
    /// 為了日誌安全性，遮蔽 Email 部分內容
    /// </summary>
    private static string? MaskEmail(string? email)
	{
		if (string.IsNullOrEmpty(email)) return email;
		var at = email.IndexOf('@');
		if (at <= 1) return "***" + email;
		return email.Substring(0, 1) + "***" + email.Substring(at);
	}
}
