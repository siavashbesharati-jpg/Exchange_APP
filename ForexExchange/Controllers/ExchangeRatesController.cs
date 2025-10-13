using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace ForexExchange.Controllers
{
    public class ExchangeRatesController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ExchangeRatesController> _logger;
        private readonly IWebScrapingService _webScrapingService;
        private readonly AdminActivityService _adminActivityService;
        private readonly AdminNotificationService _adminNotificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExchangeRatesController(
            ForexDbContext context,
            ILogger<ExchangeRatesController> logger,
            IWebScrapingService webScrapingService,
            AdminActivityService adminActivityService,
            AdminNotificationService adminNotificationService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
            _adminActivityService = adminActivityService;
            _adminNotificationService = adminNotificationService;
            _userManager = userManager;
        }

        // GET: ExchangeRates
        public async Task<IActionResult> Index()
        {
            var exchangeRates = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .OrderBy(r => r.FromCurrency.Code)
                .ThenBy(r => r.ToCurrency.Code)
                .ToListAsync();

            return View(exchangeRates);
        }





        // GET: API endpoint for current rates
        [HttpGet]
        public async Task<IActionResult> GetCurrentRates()
        {
            var rates = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .Select(r => new
                {
                    fromCurrency = r.FromCurrency.Code,
                    toCurrency = r.ToCurrency.Code,
                    rate = r.Rate,
                    updatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Json(rates);
        }

        // GET: ExchangeRates/Manage
        [Authorize(Roles = "Admin,Operator,Programmer")]
        public async Task<IActionResult> Manage(long? refresh)
        {
            // Force fresh query to avoid EF tracking cache issues
            var exchangeRates = await _context.ExchangeRates
                .AsNoTracking()
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .OrderBy(r => r.FromCurrency.Code)
                .ThenBy(r => r.ToCurrency.Code)
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .AsNoTracking()
                .Where(c => c.IsActive && c.Code != "OMR")
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Id, c.Code, c.PersianName })
                .ToListAsync();

            return View(exchangeRates);
        }

        // POST: ExchangeRates/UpdateAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Operator,Programmer")]
        public async Task<IActionResult> UpdateAll(Dictionary<int, decimal> rates)
        {
            if (rates == null)
            {
                TempData["ErrorMessage"] = "داده‌های ورودی نامعتبر است.";
                return RedirectToAction(nameof(Manage));
            }

            // Get base currency (OMR)
            var baseCurrency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.Code == "OMR");

            if (baseCurrency == null)
            {
                TempData["ErrorMessage"] = "ارز پایه (ریال عمان) در پایگاه داده یافت نشد";
                return RedirectToAction(nameof(Manage));
            }

            var currencies = await _context.Currencies.Where(c => c.IsActive && c.Code != "OMR")
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            foreach (var currency in currencies)
            {
                var currencyKey = currency.Id;

                if (rates.ContainsKey(currencyKey))
                {
                    var newRate = rates[currencyKey];

                    // Look for existing rate with FROM=currency, TO=baseCurrency (X → OMR)
                    var existingRate = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == currency.Id && r.ToCurrencyId == baseCurrency.Id && r.IsActive);
                    if (existingRate != null)
                    {
                        existingRate.Rate = newRate;
                        existingRate.UpdatedAt = DateTime.Now;
                        existingRate.UpdatedBy = User.Identity?.Name ?? "System";
                        _context.Update(existingRate);
                    }
                    else
                    {
                        var newExchangeRate = new ExchangeRate
                        {
                            Rate = newRate,
                            FromCurrencyId = currency.Id,
                            ToCurrencyId = baseCurrency.Id,
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = User.Identity?.Name ?? "System"
                        };
                        _context.Add(newExchangeRate);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "نرخ‌های ارز با موفقیت بروزرسانی شدند.";
            return RedirectToAction(nameof(Manage));
        }

        // POST: ExchangeRates/UpdateFromWeb
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Operator,Programmer")]
        public IActionResult UpdateFromWeb()
        {
            // DISABLED: Web scraping functionality
            TempData["ErrorMessage"] = "بروزرسانی از وب غیرفعال شده است.";
            return RedirectToAction(nameof(Manage), new { refresh = DateTime.Now.Ticks });
        }

        private bool ExchangeRateExists(int id)
        {
            return _context.ExchangeRates.Any(e => e.Id == id);
        }
    }
}
