using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Models;
using System.Diagnostics;

namespace PRN222_EWMS_Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (User.IsInRole("MANAGER"))
                return RedirectToAction("Dashboard", "ManagerDashboard");

            if (User.IsInRole("SALES_STAFF")) return RedirectToAction("Index", "SalesDashboard");
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
