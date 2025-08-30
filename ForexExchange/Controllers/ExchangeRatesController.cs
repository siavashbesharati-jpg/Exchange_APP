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
        private readonly IRateCalculationService _rateCalc;
        private readonly AdminActivityService _adminActivityService;
        private readonly AdminNotificationService _adminNotificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExchangeRatesController(
            ForexDbContext context,
            ILogger<ExchangeRatesController> logger,
            IWebScrapingService webScrapingService,
            IRateCalculationService rateCalc,
            AdminActivityService adminActivityService,
            AdminNotificationService adminNotificationService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
            _rateCalc = rateCalc;
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

                // Log admin activity
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogExchangeRateUpdatedAsync(exchangeRate, currentUser.Id, currentUser.UserName ?? "Unknown");
                    await _adminNotificationService.SendExchangeRateNotificationAsync(exchangeRate, "created");
                }

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

                    // Log admin activity
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        await _adminActivityService.LogExchangeRateUpdatedAsync(exchangeRate, currentUser.Id, currentUser.UserName ?? "Unknown");
                        await _adminNotificationService.SendExchangeRateNotificationAsync(exchangeRate, "updated");
                    }

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
    public async Task<IActionResult> UpdateAll(Dictionary<string, decimal> rates)
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

                    if (rates.ContainsKey(currencyKey))
                    {
                        var rate = rates[currencyKey];

                        if (rate > 0)
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
                                Rate = rate,
                                UpdatedAt = DateTime.Now,
                                UpdatedBy = "Admin",
                                IsActive = true
                            };

                            _context.Add(newRate);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Log admin activity for bulk rate update
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogActivityAsync(
                        currentUser.Id,
                        currentUser.UserName ?? "Unknown",
                        AdminActivityType.ExchangeRateUpdated,
                        $"بروزرسانی انبوه نرخ ارز: {rates.Count} نرخ بروزرسانی شد",
                        JsonSerializer.Serialize(new { UpdatedRates = rates.Count, Rates = rates }),
                        "ExchangeRate",
                        null
                    );
                    await _adminNotificationService.SendBulkOperationNotificationAsync("بروزرسانی نرخ ارز", rates.Count, $"نرخ‌های ارز بروزرسانی شدند");
                }

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
        [Authorize(Roles = "Admin,Manager,Staff")]
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
    public async Task<IActionResult> UpdateAll(Dictionary<int, decimal> rates)
        {
            if (rates == null)
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

            var currencies = await _context.Currencies.Where(c => c.IsActive && !c.IsBaseCurrency).ToListAsync();

            foreach (var currency in currencies)
            {
                var currencyKey = currency.Id;

                if (rates.ContainsKey(currencyKey))
                {
                    var rate = rates[currencyKey];

                    // Look for existing rate with FROM=currency, TO=baseCurrency (X → IRR)
                    var existingRate = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == currency.Id && r.ToCurrencyId == baseCurrency.Id && r.IsActive); 
                    if (existingRate != null)
                    {
                        existingRate.Rate = _rateCalc.SafeRound(rate, 4);
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
                            Rate = _rateCalc.SafeRound(rate, 4),
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
            return RedirectToAction(nameof(Manage));
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
                var Currecnies = _context.Currencies.AsNoTracking().ToList();
                if (Currecnies.Any() == false)
                {
                    TempData["ErrorMessage"] = " هیچ ارزی در پایگاه داده یافت نشد";
                    return RedirectToAction(nameof(Manage));
                }

                var baseCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency);
                if (baseCurrency == null)
                {
                    TempData["ErrorMessage"] = " هیچ ارزی در پایگاه داده یافت نشد";
                    return RedirectToAction(nameof(Manage));
                }

                int updatedCount = 0;
                var errors = new List<string>();
                // Collect fresh currency->base rates from web
                var scrapedMap = new Dictionary<int, decimal>();
                foreach (var currency in Currecnies)
                {
                    if (currency.IsBaseCurrency) continue; // skip base
                    var rate = await _webScrapingService.GetCurrencyRateAsync(currency.Code);
                    if (rate == null || rate.HasValue == false)
                    {
                        TempData["ErrorMessage"] = "هیچ نرخی از وب دریافت نشد. لطفاً اتصال اینترنت و دسترسی به سایت را بررسی کنید.";
                        return RedirectToAction(nameof(Manage));
                    }

                    // Normalize incoming web values to 4 decimals for storage
                    var roundedRate = _rateCalc.SafeRound(rate.Value, 4);
                    scrapedMap[currency.Id] = roundedRate;

                    var existingRate = await _context.ExchangeRates
                            .FirstOrDefaultAsync(r => r.FromCurrencyId == currency.Id && r.ToCurrencyId == baseCurrency.Id && r.IsActive);

                    if (existingRate != null)
                    {
                        existingRate.Rate = roundedRate;
                        existingRate.UpdatedAt = DateTime.Now;
                        existingRate.UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)";
                        _context.Update(existingRate);
                    }
                    else
                    {
                        var newRate = new ExchangeRate
                        {
                            FromCurrencyId = currency.Id,
                            ToCurrencyId = baseCurrency.Id,
                            Rate = roundedRate,
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)"
                        };
                        _context.Add(newRate);
                    }
                    // Add/update reverse rate: baseCurrency -> currency (e.g., IRR -> USD)
                    var reverseRate = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == baseCurrency.Id && r.ToCurrencyId == currency.Id && r.IsActive);
                    var reverseValue = roundedRate > 0 ? _rateCalc.SafeRound(1 / roundedRate, 8) : 0;
                    if (reverseRate != null)
                    {
                        reverseRate.Rate = reverseValue;
                        reverseRate.UpdatedAt = DateTime.Now;
                        reverseRate.UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)";
                        _context.Update(reverseRate);
                    }
                    else if (reverseValue > 0)
                    {
                        var newReverse = new ExchangeRate
                        {
                            FromCurrencyId = baseCurrency.Id,
                            ToCurrencyId = currency.Id,
                            Rate = reverseValue,
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)"
                        };
                        _context.Add(newReverse);
                    }
                    updatedCount++;
                    _logger.LogInformation("Updated {Code}->IRR: Rate={Rate}",
                        currency.Code, rate.Value);
                }

                // Compute cross rates among non-base currencies using currency->base rates
                await UpsertCrossRatesFromBaseAsync(baseCurrency.Id, scrapedMap);

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
            return RedirectToAction(nameof(Manage), new { refresh = DateTime.Now.Ticks });
        }

        // Upsert cross rates among all non-base currencies using fresh currency->base map
    private async Task UpsertCrossRatesFromBaseAsync(int baseCurrencyId, Dictionary<int, decimal> map)
        {
            var foreignIds = map.Keys.ToList();
            for (int i = 0; i < foreignIds.Count; i++)
            {
                for (int j = 0; j < foreignIds.Count; j++)
                {
                    if (i == j) continue;
                    var fromId = foreignIds[i];
                    var toId = foreignIds[j];
                    var crossRate = map[fromId] / map[toId];
                    var existing = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == fromId && r.ToCurrencyId == toId && r.IsActive);

                    if (existing != null)
                    {
                        existing.Rate = crossRate;
                        existing.UpdatedAt = DateTime.Now;
                        existing.UpdatedBy = User.Identity?.Name ?? "System";
                        _context.Update(existing);
                    }
                    else
                    {
                        var newCross = new ExchangeRate
                        {
                            FromCurrencyId = fromId,
                            ToCurrencyId = toId,
                            Rate = crossRate,
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = User.Identity?.Name ?? "System"
                        };
                        _context.Add(newCross);
                    }
                        // Add/update reverse cross-rate (toId -> fromId)
                        var reverseCrossRate = crossRate > 0 ? _rateCalc.SafeRound(1 / crossRate, 8) : 0;
                        var existingReverse = await _context.ExchangeRates
                            .FirstOrDefaultAsync(r => r.FromCurrencyId == toId && r.ToCurrencyId == fromId && r.IsActive);
                        if (existingReverse != null)
                        {
                            existingReverse.Rate = reverseCrossRate;
                            existingReverse.UpdatedAt = DateTime.Now;
                            existingReverse.UpdatedBy = User.Identity?.Name ?? "System";
                            _context.Update(existingReverse);
                        }
                        else if (reverseCrossRate > 0)
                        {
                            var newReverseCross = new ExchangeRate
                            {
                                FromCurrencyId = toId,
                                ToCurrencyId = fromId,
                                Rate = reverseCrossRate,
                                IsActive = true,
                                UpdatedAt = DateTime.Now,
                                UpdatedBy = User.Identity?.Name ?? "System"
                            };
                            _context.Add(newReverseCross);
                        }
                }
            }
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



        private bool ExchangeRateExists(int id)
        {
            return _context.ExchangeRates.Any(e => e.Id == id);
        }
    }
}
