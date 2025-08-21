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
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .OrderBy(r => r.FromCurrency.Code)
                .ThenBy(r => r.ToCurrency.Code)
                .ToListAsync();

            return View(exchangeRates);
        }

        // GET: ExchangeRates/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.FromCurrencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
                
            ViewBag.ToCurrencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
                
            return View();
        }

        // POST: ExchangeRates/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExchangeRate exchangeRate)
        {
            if (ModelState.IsValid)
            {
                // Validate that FromCurrency and ToCurrency are different
                if (exchangeRate.FromCurrencyId == exchangeRate.ToCurrencyId)
                {
                    ModelState.AddModelError("", "ارز مبدأ و مقصد نمی‌توانند یکسان باشند.");
                    await PopulateCurrencyDropdowns();
                    return View(exchangeRate);
                }

                // Check if rate already exists for this currency pair
                var existingRate = await _context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.FromCurrencyId == exchangeRate.FromCurrencyId && 
                                            r.ToCurrencyId == exchangeRate.ToCurrencyId && 
                                            r.IsActive);

                if (existingRate != null)
                {
                    // Deactivate old rate
                    existingRate.IsActive = false;
                    _context.Update(existingRate);
                }

                exchangeRate.UpdatedAt = DateTime.Now;
                exchangeRate.UpdatedBy = "Admin"; // In a real app, this would be the current user
                exchangeRate.IsActive = true;

                _context.Add(exchangeRate);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "نرخ ارز با موفقیت ثبت شد.";
                return RedirectToAction(nameof(Index));
            }
            
            await PopulateCurrencyDropdowns();
            return View(exchangeRate);
        }

        // GET: ExchangeRates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var exchangeRate = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .FirstOrDefaultAsync(r => r.Id == id);
                
            if (exchangeRate == null)
            {
                return NotFound();
            }
            
            await PopulateCurrencyDropdowns();
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
                    exchangeRate.UpdatedAt = DateTime.Now;
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
                // Update rates for base currency to foreign currencies
                var baseCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency);
                var foreignCurrencies = await _context.Currencies.Where(c => c.IsActive && !c.IsBaseCurrency).ToListAsync();
                
                if (baseCurrency == null)
                {
                    TempData["ErrorMessage"] = "ارز پایه پیدا نشد.";
                    return RedirectToAction(nameof(Index));
                }
                
                foreach (var currency in foreignCurrencies)
                {
                    var currencyKey = currency.Id.ToString();
                    
                    if (buyRates.ContainsKey(currencyKey) && sellRates.ContainsKey(currencyKey))
                    {
                        var buyRate = buyRates[currencyKey];
                        var sellRate = sellRates[currencyKey];

                        if (buyRate > 0 && sellRate > 0)
                        {
                            // Deactivate old rate for this currency pair
                            var existingRate = await _context.ExchangeRates
                                .FirstOrDefaultAsync(r => r.FromCurrencyId == baseCurrency.Id && 
                                                        r.ToCurrencyId == currency.Id && 
                                                        r.IsActive);

                            if (existingRate != null)
                            {
                                existingRate.IsActive = false;
                                _context.Update(existingRate);
                            }

                            // Create new rate from base currency to foreign currency
                            var newRate = new ExchangeRate
                            {
                                FromCurrencyId = baseCurrency.Id,
                                ToCurrencyId = currency.Id,
                                BuyRate = buyRate,
                                SellRate = sellRate,
                                UpdatedAt = DateTime.Now,
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
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .Select(r => new {
                    fromCurrency = r.FromCurrency.Code,
                    toCurrency = r.ToCurrency.Code,
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
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .OrderBy(r => r.FromCurrency.Code)
                .ThenBy(r => r.ToCurrency.Code)
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .Where(c => c.IsActive && !c.IsBaseCurrency)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Id, c.Code, c.PersianName })
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

            // Get base currency (IRR)
            var baseCurrency = await _context.Currencies
                .FirstOrDefaultAsync(c => c.IsBaseCurrency);
            
            if (baseCurrency == null)
            {
                TempData["ErrorMessage"] = "ارز پایه (ریال) در پایگاه داده یافت نشد";
                return RedirectToAction(nameof(Manage));
            }

            var currencies = await _context.Currencies.Where(c => c.IsActive).ToListAsync();

            foreach (var currency in currencies)
            {
                var currencyKey = currency.Id;
                
                if (buyRates.ContainsKey(currencyKey) && sellRates.ContainsKey(currencyKey))
                {
                    var buyRate = buyRates[currencyKey];
                    var sellRate = sellRates[currencyKey];

                    if (sellRate <= buyRate)
                    {
                        TempData["ErrorMessage"] = $"نرخ فروش {currency.Name} باید بیشتر از نرخ خرید باشد.";
                        return RedirectToAction(nameof(Manage));
                    }

                    var existingRate = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == currency.Id && r.IsActive);

                    if (existingRate != null)
                    {
                        existingRate.BuyRate = buyRate;
                        existingRate.SellRate = sellRate;
                        existingRate.UpdatedAt = DateTime.Now;
                        existingRate.UpdatedBy = User.Identity?.Name ?? "System";
                        _context.Update(existingRate);
                    }
                    else
                    {
                        var newRate = new ExchangeRate
                        {
                            FromCurrencyId = currency.Id,
                            ToCurrencyId = baseCurrency.Id,
                            BuyRate = buyRate,
                            SellRate = sellRate,
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
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
                
                // Get base currency (IRR)
                var baseCurrency = await _context.Currencies
                    .FirstOrDefaultAsync(c => c.IsBaseCurrency);
                
                if (baseCurrency == null)
                {
                    TempData["ErrorMessage"] = "ارز پایه (ریال) در پایگاه داده یافت نشد";
                    return RedirectToAction(nameof(Manage));
                }

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

                        var currencyEntity = await _context.Currencies
                            .FirstOrDefaultAsync(c => c.Code == currency);
                        
                        if (currencyEntity == null)
                        {
                            errors.Add($"ارز با کد {currency} در پایگاه داده یافت نشد");
                            continue;
                        }

                        var existingRate = await _context.ExchangeRates
                            .FirstOrDefaultAsync(r => r.FromCurrencyId == currencyEntity.Id && r.IsActive);

                        if (existingRate != null)
                        {
                            existingRate.BuyRate = rates.BuyRate;
                            existingRate.SellRate = rates.SellRate;
                            existingRate.UpdatedAt = DateTime.Now;
                            existingRate.UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)";
                            _context.Update(existingRate);
                        }
                        else
                        {
                            var newRate = new ExchangeRate
                            {
                                FromCurrencyId = currencyEntity.Id,
                                ToCurrencyId = baseCurrency.Id,
                                BuyRate = rates.BuyRate,
                                SellRate = rates.SellRate,
                                IsActive = true,
                                UpdatedAt = DateTime.Now,
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

        private async Task PopulateCurrencyDropdowns()
        {
            ViewBag.FromCurrencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
                
            ViewBag.ToCurrencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        private string GetCurrencyDisplayName(string currencyCode)
        {
            return currencyCode switch
            {
                "USD" => "دلار آمریکا",
                "EUR" => "یورو",
                "AED" => "درهم امارات",
                "OMR" => "ریال عمان",
                "TRY" => "لیر ترکیه",
                "IRR" => "ریال ایران",
                _ => currencyCode
            };
        }

        private bool ExchangeRateExists(int id)
        {
            return _context.ExchangeRates.Any(e => e.Id == id);
        }
    }
}
