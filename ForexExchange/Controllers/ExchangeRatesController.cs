using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;

namespace ForexExchange.Controllers
{
    public class ExchangeRatesController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ExchangeRatesController> _logger;
        private readonly IWebScrapingService _webScrapingService;

        public ExchangeRatesController(ForexDbContext context, ILogger<ExchangeRatesController> logger, IWebScrapingService webScrapingService)
        {
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
        }

        // GET: ExchangeRates
        public async Task<IActionResult> Index()
        {
            var exchangeRates = await _context.ExchangeRates
                .Where(r => r.IsActive)
                .OrderBy(r => r.Currency)
                .ToListAsync();

            return View(exchangeRates);
        }

        // GET: ExchangeRates/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ExchangeRates/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExchangeRate exchangeRate)
        {
            if (ModelState.IsValid)
            {
                // Check if rate already exists for this currency
                var existingRate = await _context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.Currency == exchangeRate.Currency && r.IsActive);

                if (existingRate != null)
                {
                    // Deactivate old rate
                    existingRate.IsActive = false;
                    _context.Update(existingRate);
                }

                exchangeRate.UpdatedAt = DateTime.UtcNow;
                exchangeRate.UpdatedBy = "Admin"; // In a real app, this would be the current user
                exchangeRate.IsActive = true;

                _context.Add(exchangeRate);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "نرخ ارز با موفقیت ثبت شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(exchangeRate);
        }

        // GET: ExchangeRates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var exchangeRate = await _context.ExchangeRates.FindAsync(id);
            if (exchangeRate == null)
            {
                return NotFound();
            }
            return View(exchangeRate);
        }

        // POST: ExchangeRates/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ExchangeRate exchangeRate)
        {
            if (id != exchangeRate.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    exchangeRate.UpdatedAt = DateTime.UtcNow;
                    exchangeRate.UpdatedBy = "Admin"; // In a real app, this would be the current user
                    
                    _context.Update(exchangeRate);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "نرخ ارز با موفقیت بروزرسانی شد.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExchangeRateExists(exchangeRate.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(exchangeRate);
        }

        // POST: ExchangeRates/UpdateAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAll(Dictionary<string, decimal> buyRates, Dictionary<string, decimal> sellRates)
        {
            try
            {
                foreach (var currency in Enum.GetValues<CurrencyType>())
                {
                    if (currency == CurrencyType.Toman) continue; // Skip Toman as it's the base currency

                    var currencyKey = ((int)currency).ToString();
                    
                    if (buyRates.ContainsKey(currencyKey) && sellRates.ContainsKey(currencyKey))
                    {
                        var buyRate = buyRates[currencyKey];
                        var sellRate = sellRates[currencyKey];

                        if (buyRate > 0 && sellRate > 0)
                        {
                            // Deactivate old rate
                            var existingRate = await _context.ExchangeRates
                                .FirstOrDefaultAsync(r => r.Currency == currency && r.IsActive);

                            if (existingRate != null)
                            {
                                existingRate.IsActive = false;
                                _context.Update(existingRate);
                            }

                            // Create new rate
                            var newRate = new ExchangeRate
                            {
                                Currency = currency,
                                BuyRate = buyRate,
                                SellRate = sellRate,
                                UpdatedAt = DateTime.UtcNow,
                                UpdatedBy = "Admin",
                                IsActive = true
                            };

                            _context.Add(newRate);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تمام نرخ‌های ارز با موفقیت بروزرسانی شد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating exchange rates");
                TempData["ErrorMessage"] = "خطا در بروزرسانی نرخ‌های ارز.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: API endpoint for current rates
        [HttpGet]
        public async Task<IActionResult> GetCurrentRates()
        {
            var rates = await _context.ExchangeRates
                .Where(r => r.IsActive)
                .Select(r => new {
                    currency = r.Currency,
                    buyRate = r.BuyRate,
                    sellRate = r.SellRate,
                    updatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Json(rates);
        }

        // GET: ExchangeRates/Manage
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> Manage()
        {
            var exchangeRates = await _context.ExchangeRates
                .Where(r => r.IsActive)
                .OrderBy(r => r.Currency)
                .ToListAsync();

            return View(exchangeRates);
        }

        // POST: ExchangeRates/UpdateAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> UpdateAll(Dictionary<int, decimal> buyRates, Dictionary<int, decimal> sellRates)
        {
            if (buyRates == null || sellRates == null)
            {
                TempData["ErrorMessage"] = "داده‌های ورودی نامعتبر است.";
                return RedirectToAction(nameof(Manage));
            }

            foreach (var currencyType in Enum.GetValues(typeof(CurrencyType)).Cast<CurrencyType>())
            {
                var currencyKey = (int)currencyType;
                
                if (buyRates.ContainsKey(currencyKey) && sellRates.ContainsKey(currencyKey))
                {
                    var buyRate = buyRates[currencyKey];
                    var sellRate = sellRates[currencyKey];

                    if (sellRate <= buyRate)
                    {
                        TempData["ErrorMessage"] = $"نرخ فروش {currencyType} باید بیشتر از نرخ خرید باشد.";
                        return RedirectToAction(nameof(Manage));
                    }

                    var existingRate = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.Currency == currencyType && r.IsActive);

                    if (existingRate != null)
                    {
                        existingRate.BuyRate = buyRate;
                        existingRate.SellRate = sellRate;
                        existingRate.UpdatedAt = DateTime.UtcNow;
                        existingRate.UpdatedBy = User.Identity?.Name ?? "System";
                        _context.Update(existingRate);
                    }
                    else
                    {
                        var newRate = new ExchangeRate
                        {
                            Currency = currencyType,
                            BuyRate = buyRate,
                            SellRate = sellRate,
                            IsActive = true,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = User.Identity?.Name ?? "System"
                        };
                        _context.Add(newRate);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "نرخ‌های ارز با موفقیت بروزرسانی شدند.";
            return RedirectToAction(nameof(Index));
        }

        // POST: ExchangeRates/UpdateFromWeb
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> UpdateFromWeb()
        {
            try
            {
                _logger.LogInformation("Starting web scraping update for exchange rates");
                var webRates = await _webScrapingService.GetExchangeRatesFromWebAsync();
                
                if (!webRates.Any())
                {
                    TempData["ErrorMessage"] = "هیچ نرخی از وب دریافت نشد. لطفاً اتصال اینترنت و دسترسی به سایت را بررسی کنید.";
                    return RedirectToAction(nameof(Manage));
                }

                int updatedCount = 0;
                var errors = new List<string>();

                foreach (var (currency, rates) in webRates)
                {
                    try
                    {
                        if (rates.SellRate <= rates.BuyRate)
                        {
                            errors.Add($"نرخ‌های دریافت شده برای {GetCurrencyDisplayName(currency)} نامعتبر است (نرخ فروش باید بیشتر از نرخ خرید باشد)");
                            continue;
                        }

                        var existingRate = await _context.ExchangeRates
                            .FirstOrDefaultAsync(r => r.Currency == currency && r.IsActive);

                        if (existingRate != null)
                        {
                            existingRate.BuyRate = rates.BuyRate;
                            existingRate.SellRate = rates.SellRate;
                            existingRate.UpdatedAt = DateTime.UtcNow;
                            existingRate.UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)";
                            _context.Update(existingRate);
                        }
                        else
                        {
                            var newRate = new ExchangeRate
                            {
                                Currency = currency,
                                BuyRate = rates.BuyRate,
                                SellRate = rates.SellRate,
                                IsActive = true,
                                UpdatedAt = DateTime.UtcNow,
                                UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)"
                            };
                            _context.Add(newRate);
                        }

                        updatedCount++;
                        _logger.LogInformation("Updated {Currency}: Buy={BuyRate}, Sell={SellRate}", 
                            currency, rates.BuyRate, rates.SellRate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating currency {Currency}", currency);
                        errors.Add($"خطا در بروزرسانی {GetCurrencyDisplayName(currency)}");
                    }
                }

                if (updatedCount > 0)
                {
                    await _context.SaveChangesAsync();
                    
                    var successMessage = $"{updatedCount} نرخ ارز از وب بروزرسانی شد.";
                    if (errors.Any())
                    {
                        successMessage += $" خطاها: {string.Join(", ", errors)}";
                    }
                    TempData["SuccessMessage"] = successMessage;
                }
                else
                {
                    TempData["ErrorMessage"] = errors.Any() ? 
                        $"هیچ نرخی بروزرسانی نشد. خطاها: {string.Join(", ", errors)}" :
                        "هیچ نرخی از وب دریافت نشد.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during web scraping update");
                TempData["ErrorMessage"] = "خطا در بروزرسانی نرخ‌ها از وب. لطفاً دوباره تلاش کنید.";
            }

            return RedirectToAction(nameof(Manage));
        }

        private string GetCurrencyDisplayName(CurrencyType currency)
        {
            return currency switch
            {
                CurrencyType.USD => "دلار آمریکا",
                CurrencyType.EUR => "یورو",
                CurrencyType.AED => "درهم امارات",
                CurrencyType.OMR => "ریال عمان",
                CurrencyType.TRY => "لیر ترکیه",
                _ => currency.ToString()
            };
        }

        private bool ExchangeRateExists(int id)
        {
            return _context.ExchangeRates.Any(e => e.Id == id);
        }
    }
}
