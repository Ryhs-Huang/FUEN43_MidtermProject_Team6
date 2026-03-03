using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookLoop.Services.Mail
{
    /// <summary>
    /// 執行一次 MailJob（群發或排程，取決於建立 Job 時的 SendAt）。
    /// 流程：讀取模板版本 → 解析名單 → 逐筆渲染 → 寄送 → 更新狀態。
    /// </summary>
    public class MailJobRunner : IMailJobRunner
    {
        private readonly AppDbContext _db;
        private readonly IMailService _mail;
        private readonly ITemplateRenderer _renderer;
        private readonly ILogger<MailJobRunner> _logger;

        public MailJobRunner(
            AppDbContext db,
            IMailService mail,
            ITemplateRenderer renderer,
            ILogger<MailJobRunner> logger)
        {
            _db = db;
            _mail = mail;
            _renderer = renderer;
            _logger = logger;
        }

        /// <summary>
        /// 被排程系統（例如 Hangfire）呼叫進來。
        /// </summary>
        public async Task RunAsync(long jobId, CancellationToken ct = default)
        {
            var job = await _db.MailJobs.FirstOrDefaultAsync(x => x.JobId == jobId, ct);
            if (job == null) { _logger.LogWarning("MailJob {JobId} not found.", jobId); return; }

            // 已完成/取消就跳過
            if (job.Status is "Completed" or "Canceled")
            {
                _logger.LogInformation("MailJob {JobId} status is {Status}, skip.", jobId, job.Status);
                return;
            }

            // 起始
            job.Status = "Sending"; 
            job.StartedAt = DateTime.Now;
            await _db.SaveChangesAsync(ct);

            int failCount = 0;

            try
            {
                // 讀模板版本：僅讀取一次，提升大筆發送時的效率
                var version = await _db.TemplateVersions
                    .Include(v => v.Template)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v =>
                        v.TemplateId == job.TemplateId &&
                        v.TemplateVersionId == job.TemplateVersionId, ct);
                if (version == null)
                    throw new InvalidOperationException($"找不到 TemplateVersion (TemplateId={job.TemplateId}, TemplateVersionId={job.TemplateVersionId})");

                bool hasMore = true;
                while (hasMore)
                {
                    // 中途檢查是否被取消：讓管理員能即時停止正在執行的 Job
                    var currentStatus = await _db.MailJobs
                        .Where(j => j.JobId == jobId)
                        .Select(j => j.Status)
                        .FirstOrDefaultAsync(CancellationToken.None); // 這裡不傳 ct 避免查不到

                    if (currentStatus == "Canceled") throw new OperationCanceledException();

                    // 分批抓取名單 (Batching)，避免記憶體溢出
                    var recipients = await _db.MailJobRecipients
                        .Where(r => r.MailJobId == job.JobId && r.Status == "Pending")
                        .OrderBy(r => r.MailJobRecipientId)
                        .Take(100) 
                        .ToListAsync(ct);

                    if (!recipients.Any()) { hasMore = false; continue; }

                    // 狀態鎖定：先改為 Sending，確保斷點續傳的冪等性，避免重複發送
                    foreach (var r in recipients) r.Status = "Sending";
                    await _db.SaveChangesAsync(ct);

                    foreach (var r in recipients)
                    {
                        // 偵測 Hangfire 傳進來的取消訊號
                        ct.ThrowIfCancellationRequested();
                        // 姓名推導：RecipientName → Members.Nickname/Username → email 前綴 

                        // 1) 先用名單上的 RecipientName（Create 頁匯入的 username 會被存到這裡）
                        string name = r.RecipientName;

                        // 2) 沒有的話，用 Email 去 Members 查 Username
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            var mem = await _db.Members
                                .AsNoTracking()
                                .Where(m => m.Email == r.RecipientEmail)
                                .Select(m => new { m.Username })
                                .FirstOrDefaultAsync(ct); // ← 如果你的方法內有 CancellationToken 變數就用它；沒有就拿掉 (ct)

                            name = mem?.Username;
                        }

                        // 3) 還是沒有，就用 email 的 @ 前字串
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            var at = r.RecipientEmail?.IndexOf('@') ?? -1;
                            name = at > 0 ? r.RecipientEmail.Substring(0, at) : (r.RecipientEmail ?? "");
                        }
                        // 置換 tokens（用 SimpleTemplateRenderer）
                        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Recipient"] = r.RecipientEmail,
                            ["Name"] = name,
                            ["Campaign"] = job.CampaignName ?? "",
                            ["TemplateKey"] = job.TemplateKey ?? ""
                        };
                        var subject = _renderer.Render(version.Subject ?? "", tokens);
                        var body = _renderer.Render(version.BodyHtml ?? "", tokens);

                        try
                        {
                            // 寄送：把 JobRecipientId 帶進去，日誌就能 1:1 對到這位名單
                            await _mail.SendAsync(
                                to: r.RecipientEmail,
                                subject: subject,
                                body: body,
                                attachmentName: null,
                                attachmentBytes: null,
                                contentType: "text/html",
                                templateId: version.TemplateId,
                                templateKey: version.Template?.TemplateKey,
                                templateVersionId: version.TemplateVersionId,
                                mailJobId: job.JobId,
                                jobRecipientId: r.MailJobRecipientId,      // ← 關鍵
                                category: "Bulk",
                                cancellationToken: ct);

                            r.Status = "Sent";
                            r.SentAt = DateTime.Now;
                            job.SentCount++;
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            r.Status = "Failed";
                            r.Error = ex.Message;
                            _logger.LogError(ex, "Job {JobId} 收件者 {Email} 寄送失敗", job.JobId, r.RecipientEmail);
                        }

                        // 節流控制 (Throttling)：避免過快請求被封鎖
                        await Task.Delay(150, ct);
                    }
                    // 批次存檔：每 100 封存一次，平衡效能與資料安全
                    await _db.SaveChangesAsync(ct);
                }

                job.FinishedAt = DateTime.Now;
                job.Status =  "Completed";
                await _db.SaveChangesAsync(CancellationToken.None);

                _logger.LogInformation("MailJob {JobId} Completed. Sent {Ok}/{Total}, Failed={Fail}.",
                    job.JobId, job.SentCount, job.TotalRecipients, failCount);
            }
            // 註解原因:避免和Controller的寫入產生Race Condition（競態條件），也讓Hangfire自動偵測到異常，自動標記為取消
            //catch (OperationCanceledException)
            //{
            //    job.Status = "Canceled";
            //    await _db.SaveChangesAsync(CancellationToken.None);
            //    _logger.LogWarning("MailJob {JobId} cancelled.", job.JobId);
            //    throw;
            //}

            // 捕捉 OperationCanceledException，讓管理員在 Dashboard 手動取消 Job 時能即時反映在資料庫狀態，並且記錄日誌
            catch (OperationCanceledException)
            {
                // 當 Controller 呼叫 BackgroundJob.Delete 時會跳到這裡
                var canceledJob = await _db.MailJobs.FindAsync(jobId);
                if (canceledJob != null)
                {
                    canceledJob.Status = "Canceled";
                    canceledJob.FinishedAt = DateTime.Now;
                    // 用 CancellationToken.None 強行寫入資料庫
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
                _logger.LogWarning("MailJob {JobId} 已被手動取消並中斷執行。", jobId);
                throw; // 拋回給 Hangfire
            }
            catch (Exception ex)
            {
                job.Status = "Failed";
                job.Description = (job.Description ?? string.Empty) + $" | Error: {ex.Message}";
                await _db.SaveChangesAsync(CancellationToken.None);//確保失敗狀態能寫入
                _logger.LogError(ex, "MailJob {JobId} failed.", job.JobId);
                throw;
            }
        }
        /// <summary>
        /// 解析 CSV 名單（每行：email[,name]），忽略空白行。
        /// </summary>
        private static List<(string email, string? name)> ParseCsvRecipients(string? csv)
        {
            var result = new List<(string, string?)>();
            if (string.IsNullOrWhiteSpace(csv)) return result;

            using var sr = new StringReader(csv);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0) continue;

                var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
                var email = parts[0];
                var name = parts.Length > 1 ? parts[1] : null;

                // 這裡不做嚴格 email 驗證，留給後續擴充
                result.Add((email, name));
            }
            return result;
        }
    }
}
