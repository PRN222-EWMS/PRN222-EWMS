using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Admin;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
public class AuditLogController(IAuditLogService auditLogs) : Controller
{
    public async Task<IActionResult> Index([FromQuery] AuditLogFilter filter)
    {
        try { return View(await auditLogs.GetPageAsync(filter)); }
        catch (ArgumentException) { return BadRequest(); }
    }

    public async Task<IActionResult> Details(long id)
    {
        var log = await auditLogs.GetDetailsAsync(id);
        return log is null ? NotFound() : View(log);
    }
}
