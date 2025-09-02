using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PoolManagementController : Controller
    {
        private readonly ICurrencyPoolService _poolService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AdminActivityService _adminActivityService;
        private readonly ILogger<PoolManagementController> _logger;

        public PoolManagementController(
            ICurrencyPoolService poolService,
            UserManager<ApplicationUser> userManager,
            AdminActivityService adminActivityService,
            ILogger<PoolManagementController> logger)
        {
            _poolService = poolService;
            _userManager = userManager;
            _adminActivityService = adminActivityService;
            _logger = logger;
        }

        /// <summary>
        /// Display currency pools management page
        /// صفحه مدیریت استخرهای ارزی
        /// </summary>
        public async Task<IActionResult> Index()
        {
            try
            {
                var pools = await _poolService.GetAllPoolsAsync();
                return View(pools);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading currency pools management page");
                TempData["ErrorMessage"] = "خطا در بارگذاری صفحه مدیریت استخرها";
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// Update pool balance
        /// بروزرسانی موجودی استخر
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBalance(int poolId, decimal newBalance, string reason)
        {
            try
            {
                // Get current pool data
                var pool = await _poolService.GetPoolByIdAsync(poolId);
                if (pool == null)
                {
                    return Json(new { success = false, message = "استخر ارزی یافت نشد" });
                }

                var oldBalance = pool.Balance;
                
                // Calculate the difference
                var difference = newBalance - oldBalance;
                
                // Update pool balance directly
                pool.Balance = newBalance;
                pool.LastUpdated = DateTime.Now;
                
                // Save changes
                var updatedPool = await _poolService.UpdatePoolDirectAsync(pool);

                // Log admin activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogPoolBalanceChangeAsync(
                        poolId,
                        pool.Currency?.Code ?? "Unknown",
                        oldBalance,
                        newBalance,
                        difference,
                        reason ?? "Manual adjustment by admin",
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown"
                    );
                }

                _logger.LogInformation($"Pool balance updated: {pool.Currency?.Code} from {oldBalance:N0} to {newBalance:N0} by {currentUser?.UserName}");

                return Json(new { 
                    success = true, 
                    message = $"موجودی {pool.Currency?.PersianName} با موفقیت به {newBalance:N0} بروزرسانی شد",
                    newBalance = newBalance,
                    difference = difference,
                    lastUpdated = pool.LastUpdated.ToString("yyyy/MM/dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating pool balance for pool ID {poolId}");
                return Json(new { success = false, message = "خطا در بروزرسانی موجودی استخر" });
            }
        }

        /// <summary>
        /// Reset pool statistics
        /// ریست کردن آمار استخر
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPoolStats(int poolId, string reason)
        {
            try
            {
                var pool = await _poolService.GetPoolAsync(poolId);
                if (pool == null)
                {
                    return Json(new { success = false, message = "استخر ارزی یافت نشد" });
                }

                var oldData = new
                {
                    Balance = pool.Balance,
                    TotalBought = pool.TotalBought,
                    TotalSold = pool.TotalSold
                };

                // Reset statistics
                pool.TotalBought = 0;
                pool.TotalSold = 0;
                pool.LastUpdated = DateTime.Now;
                
                var updatedPool = await _poolService.UpdatePoolDirectAsync(pool);

                // Log admin activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogPoolStatsResetAsync(
                        poolId,
                        pool.Currency?.Code ?? "Unknown",
                        oldData.TotalBought,
                        oldData.TotalSold,
                        reason ?? "Statistics reset by admin",
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown"
                    );
                }

                _logger.LogInformation($"Pool statistics reset: {pool.Currency?.Code} by {currentUser?.UserName}");

                return Json(new { 
                    success = true, 
                    message = $"آمار {pool.Currency?.PersianName} با موفقیت ریست شد",
                    lastUpdated = pool.LastUpdated.ToString("yyyy/MM/dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting pool statistics for pool ID {poolId}");
                return Json(new { success = false, message = "خطا در ریست کردن آمار استخر" });
            }
        }

        /// <summary>
        /// Get pool details for modal
        /// دریافت جزئیات استخر برای مودال
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPoolDetails(int poolId)
        {
            try
            {
                var pool = await _poolService.GetPoolAsync(poolId);
                if (pool == null)
                {
                    return Json(new { success = false, message = "استخر ارزی یافت نشد" });
                }

                return Json(new { 
                    success = true,
                    pool = new {
                        id = pool.Id,
                        currencyCode = pool.Currency?.Code,
                        currencyName = pool.Currency?.PersianName,
                        balance = pool.Balance,
                        totalBought = pool.TotalBought,
                        totalSold = pool.TotalSold,
                        riskLevel = pool.RiskLevel.ToString(),
                        lastUpdated = pool.LastUpdated.ToString("yyyy/MM/dd HH:mm:ss"),
                        activeBuyOrders = pool.ActiveBuyOrderCount,
                        activeSellOrders = pool.ActiveSellOrderCount
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting pool details for pool ID {poolId}");
                return Json(new { success = false, message = "خطا در دریافت جزئیات استخر" });
            }
        }
    }
}
