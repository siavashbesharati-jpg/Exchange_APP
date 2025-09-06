using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ForexExchange.Services;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    /// <summary>
    /// Management Controller for Advanced System Operations
    /// کنترلر مدیریت برای عملیات پیشرفته سیستم
    /// </summary>
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class ManagementController : Controller
    {
        private readonly ICurrencyPoolService _poolService;
        private readonly ICustomerBalanceService _balanceService;
        private readonly ILogger<ManagementController> _logger;

        public ManagementController(
            ICurrencyPoolService poolService,
            ICustomerBalanceService balanceService,
            ILogger<ManagementController> logger)
        {
            _poolService = poolService;
            _balanceService = balanceService;
            _logger = logger;
        }

        /// <summary>
        /// Management Dashboard with all advanced features
        /// داشبورد مدیریت با تمام امکانات پیشرفته
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get comprehensive system data for management view
                var pools = await _poolService.GetAllPoolsAsync();
                ViewBag.CurrencyPools = pools;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading management dashboard");
                TempData["ErrorMessage"] = "خطا در بارگذاری داشبورد مدیریت";
                return RedirectToAction("Dashboard", "Home");
            }
        }
    }
}
