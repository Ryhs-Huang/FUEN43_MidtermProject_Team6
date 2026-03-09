using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Report.Areas.Report.Controllers
{
    [Area("Report")]
    [Authorize(Policy = "Report.Access")] // 統一門票
    public abstract class ReportAreaController : Controller { }
}
