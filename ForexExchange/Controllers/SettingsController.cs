using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ForexExchange.Models;
using ForexExchange.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class SettingsController : Controller
    {
        private readonly ISettingsService _settingsService;
        private readonly ForexDbContext _context;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(ISettingsService settingsService, ForexDbContext context, ILogger<SettingsController> logger)
        {
            _settingsService = settingsService;
            _context = context;
            _logger = logger;
        }

        // GET: Settings
        public async Task<IActionResult> Index()
        {
            try
            {
                var settings = await _settingsService.GetSystemSettingsAsync();
                
                // Load currencies for dropdown
                ViewBag.Currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.PersianName)
                    .ToListAsync();
                
                return View(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system settings");
                TempData["ErrorMessage"] = "خطا در بارگیری تنظیمات سیستم.";
                
                // Load currencies even in error case
                try
                {
                    ViewBag.Currencies = await _context.Currencies
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.PersianName)
                        .ToListAsync();
                }
                catch { /* ignore if currencies can't be loaded */ }
                
                return View(new SystemSettingsViewModel());
            }
        }

        // POST: Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(SystemSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload currencies for dropdown in case of validation errors
                try
                {
                    ViewBag.Currencies = await _context.Currencies
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.PersianName)
                        .ToListAsync();
                }
                catch { /* ignore if currencies can't be loaded */ }
                
                return View(model);
            }

            try
            {
                var currentUser = User.Identity?.Name ?? "Admin";
                await _settingsService.UpdateSystemSettingsAsync(model, currentUser);
                
                TempData["SuccessMessage"] = "تنظیمات سیستم با موفقیت بروزرسانی شد.";
                _logger.LogInformation($"System settings updated by {currentUser}");
                
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system settings");
                TempData["ErrorMessage"] = "خطا در بروزرسانی تنظیمات سیستم.";
                
                // Reload currencies for dropdown in case of error
                try
                {
                    ViewBag.Currencies = await _context.Currencies
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.PersianName)
                        .ToListAsync();
                }
                catch { /* ignore if currencies can't be loaded */ }
                
                return View(model);
            }
        }

        // GET: Settings/Commission
        public async Task<IActionResult> Commission()
        {
            try
            {
                var commissionRate = await _settingsService.GetSettingAsync(SettingKeys.CommissionRate, 0.5m);
                var exchangeFeeRate = await _settingsService.GetSettingAsync(SettingKeys.ExchangeFeeRate, 0.2m);

                var model = new CommissionSettingsViewModel
                {
                    CommissionRate = commissionRate,
                    ExchangeFeeRate = exchangeFeeRate
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading commission settings");
                TempData["ErrorMessage"] = "خطا در بارگیری تنظیمات کمیسیون.";
                return View(new CommissionSettingsViewModel());
            }
        }

        // POST: Settings/Commission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Commission(CommissionSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var currentUser = User.Identity?.Name ?? "Admin";
                await _settingsService.SetSettingAsync(SettingKeys.CommissionRate, model.CommissionRate, "نرخ کمیسیون به درصد", currentUser);
                await _settingsService.SetSettingAsync(SettingKeys.ExchangeFeeRate, model.ExchangeFeeRate, "کارمزد تبدیل ارز به درصد", currentUser);
                
                TempData["SuccessMessage"] = "تنظیمات کمیسیون با موفقیت بروزرسانی شد.";
                _logger.LogInformation($"Commission settings updated by {currentUser}: Commission={model.CommissionRate}%, Fee={model.ExchangeFeeRate}%");
                
                return RedirectToAction(nameof(Commission));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating commission settings");
                TempData["ErrorMessage"] = "خطا در بروزرسانی تنظیمات کمیسیون.";
                return View(model);
            }
        }

        // GET: Settings/TransactionLimits
        public async Task<IActionResult> TransactionLimits()
        {
            try
            {
                var model = new TransactionLimitsViewModel
                {
                    MinTransactionAmount = await _settingsService.GetSettingAsync(SettingKeys.MinTransactionAmount, 10000m),
                    MaxTransactionAmount = await _settingsService.GetSettingAsync(SettingKeys.MaxTransactionAmount, 1000000000m),
                    DailyTransactionLimit = await _settingsService.GetSettingAsync(SettingKeys.DailyTransactionLimit, 5000000000m)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transaction limits");
                TempData["ErrorMessage"] = "خطا در بارگیری محدودیت‌های تراکنش.";
                return View(new TransactionLimitsViewModel());
            }
        }

        // POST: Settings/TransactionLimits
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransactionLimits(TransactionLimitsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var currentUser = User.Identity?.Name ?? "Admin";
                await _settingsService.SetSettingAsync(SettingKeys.MinTransactionAmount, model.MinTransactionAmount, "حداقل مبلغ تراکنش به تومان", currentUser);
                await _settingsService.SetSettingAsync(SettingKeys.MaxTransactionAmount, model.MaxTransactionAmount, "حداکثر مبلغ تراکنش به تومان", currentUser);
                await _settingsService.SetSettingAsync(SettingKeys.DailyTransactionLimit, model.DailyTransactionLimit, "محدودیت تراکنش روزانه به تومان", currentUser);
                
                TempData["SuccessMessage"] = "محدودیت‌های تراکنش با موفقیت بروزرسانی شد.";
                _logger.LogInformation($"Transaction limits updated by {currentUser}");
                
                return RedirectToAction(nameof(TransactionLimits));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction limits");
                TempData["ErrorMessage"] = "خطا در بروزرسانی محدودیت‌های تراکنش.";
                return View(model);
            }
        }

        // API: Get current commission rate
        [HttpGet]
        public async Task<IActionResult> GetCommissionRate()
        {
            try
            {
                var rate = await _settingsService.GetCommissionRateAsync();
                return Json(new { success = true, rate = rate * 100 }); // Return as percentage
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting commission rate");
                return Json(new { success = false, message = "خطا در دریافت نرخ کمیسیون" });
            }
        }

        // API: Get current exchange fee rate
        [HttpGet]
        public async Task<IActionResult> GetExchangeFeeRate()
        {
            try
            {
                var rate = await _settingsService.GetExchangeFeeRateAsync();
                return Json(new { success = true, rate = rate * 100 }); // Return as percentage
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange fee rate");
                return Json(new { success = false, message = "خطا در دریافت کارمزد تبدیل" });
            }
        }
    }

    // Additional ViewModels for specific settings pages
    public class CommissionSettingsViewModel
    {
        [Display(Name = "نرخ کمیسیون (%)")]
        [Range(0, 100, ErrorMessage = "نرخ کمیسیون باید بین 0 تا 100 درصد باشد")]
        public decimal CommissionRate { get; set; }

        [Display(Name = "کارمزد تبدیل ارز (%)")]
        [Range(0, 100, ErrorMessage = "کارمزد تبدیل باید بین 0 تا 100 درصد باشد")]
        public decimal ExchangeFeeRate { get; set; }
    }

    public class TransactionLimitsViewModel
    {
        [Display(Name = "حداقل مبلغ تراکنش (تومان)")]
        [Range(1000, 1000000000, ErrorMessage = "حداقل مبلغ باید بین 1,000 تا 1,000,000,000 تومان باشد")]
        public decimal MinTransactionAmount { get; set; }

        [Display(Name = "حداکثر مبلغ تراکنش (تومان)")]
        [Range(1000, 10000000000, ErrorMessage = "حداکثر مبلغ باید بین 1,000 تا 10,000,000,000 تومان باشد")]
        public decimal MaxTransactionAmount { get; set; }

        [Display(Name = "محدودیت تراکنش روزانه (تومان)")]
        [Range(10000, 100000000000, ErrorMessage = "محدودیت روزانه باید بین 10,000 تا 100,000,000,000 تومان باشد")]
        public decimal DailyTransactionLimit { get; set; }
    }
}
