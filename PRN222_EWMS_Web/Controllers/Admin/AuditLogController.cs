using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Admin;

namespace PRN222_EWMS_Web.Controllers.Admin;

[Authorize(Roles = "ADMIN")]
public class AuditLogController(IAuditLogService auditLogs) : Controller
{
    public async Task<IActionResult> Index(string? tableName, string? actionType, int page = 1)
    {
        try { return View(await auditLogs.GetPageAsync(tableName, actionType, page)); }
        catch (ArgumentException) { return BadRequest(); }
    }

    public async Task<IActionResult> Details(long id)
    {
        var log = await auditLogs.GetDetailsAsync(id);
        return log is null ? NotFound() : View(log);
    }
}
