using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Models;
using System.Diagnostics;
using PRN222_EWMS_Web.Services.Admin;

namespace PRN222_EWMS_Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IAdminDashboardService _adminDashboard;

        public HomeController(ILogger<HomeController> logger, IAdminDashboardService adminDashboard)
        {
            _logger = logger;
            _adminDashboard = adminDashboard;
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("MANAGER"))
                return RedirectToAction("Dashboard", "ManagerDashboard");

            if (User.IsInRole("CEO")) return View("CeoDashboard");
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
