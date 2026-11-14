using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Models;
using System.Diagnostics;
using PRN222_EWMS_Web.Services.Admin;
using PRN222_EWMS_Web.Services.Ceo;
using PRN222_EWMS_Web.ViewModels.Ceo;
using System.Data.Common;

namespace PRN222_EWMS_Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAdminDashboardService _adminDashboard;
        private readonly ICeoDashboardService _ceoDashboard;

        public HomeController(ILogger<HomeController> logger, IAdminDashboardService adminDashboard,
            ICeoDashboardService ceoDashboard)
        {
            _logger = logger;
            _adminDashboard = adminDashboard;
            _ceoDashboard = ceoDashboard;
        }

        public async Task<IActionResult> Index([FromQuery] CeoDashboardFilter filter)
        {
            if (User.IsInRole("MANAGER"))
                return RedirectToAction("Dashboard", "ManagerDashboard");

            if (User.IsInRole("CEO"))
            {
                if (!ModelState.IsValid)
                {
                    var message = ModelState.Values.SelectMany(value => value.Errors)
                        .Select(error => error.ErrorMessage).FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));
                    return View("CeoDashboard", new CeoDashboardViewModel
                    {
                        Filter = filter,
                        Error = message ?? "Bộ lọc không hợp lệ. Vui lòng chọn lại kho và khoảng theo dõi."
                    });
                }
                try { return View("CeoDashboard", await _ceoDashboard.GetDashboardAsync(filter)); }
                catch (ArgumentException ex)
                {
                    return View("CeoDashboard", new CeoDashboardViewModel { Filter = filter, Error = ex.Message });
                }
                catch (Exception ex) when (ex is DbException or TimeoutException)
                {
                    _logger.LogError(ex, "Unable to load CEO warehouse dashboard.");
                    return View("CeoDashboard", new CeoDashboardViewModel
                    {
                        Filter = filter,
                        Error = "Chưa tải được số liệu tổng quan. Vui lòng tải lại trang."
                    });
                }
            }
            if (User.IsInRole("SALES_STAFF")) return RedirectToAction("Index", "SalesDashboard");
            if (User.IsInRole("WAREHOUSE_STAFF")) return RedirectToAction("Index", "WarehouseDashboard");
            if (User.IsInRole("PURCHASING_STAFF")) return RedirectToAction("Index", "PurchasingDashboard");
            if (User.IsInRole("ADMIN"))
            {
                return View("AdminDashboard", await _adminDashboard.GetDashboardAsync());
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
