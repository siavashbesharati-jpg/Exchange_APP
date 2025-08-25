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
        private readonly IRateCalculationService _rateCalc;

        public ExchangeRatesController(ForexDbContext context, ILogger<ExchangeRatesController> logger, IWebScrapingService webScrapingService, IRateCalculationService rateCalc)
        {
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
            _rateCalc = rateCalc;
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
                .Select(r => new
                {
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

        // POST: ExchangeRates/BulkUpdatePairs
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> BulkUpdatePairs(List<ExchangeRateUpdateViewModel> pairs)
        {
            if (pairs == null || pairs.Count == 0)
            {
                TempData["ErrorMessage"] = "هیچ موردی برای بروزرسانی ارسال نشد.";
                return RedirectToAction(nameof(Manage));
            }

            var currencyIds = pairs.SelectMany(p => new[] { p.FromCurrencyId, p.ToCurrencyId }).Distinct().ToList();
            var currencies = await _context.Currencies.Where(c => currencyIds.Contains(c.Id)).ToListAsync();

            foreach (var p in pairs)
            {
                if (p.FromCurrencyId == p.ToCurrencyId) continue;
                if (p.SellRate <= 0 || p.BuyRate <= 0 || p.SellRate <= p.BuyRate) continue;

                var existing = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrencyId == p.FromCurrencyId && r.ToCurrencyId == p.ToCurrencyId && r.IsActive);
                if (existing != null)
                {
                    existing.BuyRate = _rateCalc.SafeRound(p.BuyRate, 4);
                    existing.SellRate = _rateCalc.SafeRound(p.SellRate, 4);
                    existing.UpdatedAt = DateTime.Now;
                    existing.UpdatedBy = User.Identity?.Name ?? "System";
                    _context.Update(existing);
                }
                else
                {
                    var newRate = new ExchangeRate
                    {
                        FromCurrencyId = p.FromCurrencyId,
                        ToCurrencyId = p.ToCurrencyId,
                        BuyRate = _rateCalc.SafeRound(p.BuyRate, 4),
                        SellRate = _rateCalc.SafeRound(p.SellRate, 4),
                        IsActive = true,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = User.Identity?.Name ?? "System"
                    };
                    _context.Add(newRate);
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "نرخ‌های انتخاب‌شده با موفقیت بروزرسانی شدند.";
            return RedirectToAction(nameof(Manage));
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
                var scrapedMap = new Dictionary<int, (decimal buy, decimal sell)>();
                foreach (var currency in Currecnies)
                {
                    if (currency.IsBaseCurrency) continue; // skip base
                    var rate = await _webScrapingService.GetCurrencyRateAsync(currency.Code);
                    if (rate == null || rate.HasValue == false)
                    {
                        TempData["ErrorMessage"] = "هیچ نرخی از وب دریافت نشد. لطفاً اتصال اینترنت و دسترسی به سایت را بررسی کنید.";
                        return RedirectToAction(nameof(Manage));
                    }

                    if (rate.Value.SellRate <= rate.Value.BuyRate)
                    {
                        errors.Add($"نرخ‌های دریافت شده برای {currency.PersianName} نامعتبر است (نرخ فروش باید بیشتر از نرخ خرید باشد)");
                        continue;
                    }

                    // Normalize incoming web values to 4 decimals for storage
                    var roundedBuy = _rateCalc.SafeRound(rate.Value.BuyRate, 4);
                    var roundedSell = _rateCalc.SafeRound(rate.Value.SellRate, 4);
                    scrapedMap[currency.Id] = (roundedBuy, roundedSell);

                    var existingRate = await _context.ExchangeRates
                            .FirstOrDefaultAsync(r => r.FromCurrencyId == currency.Id && r.ToCurrencyId == baseCurrency.Id && r.IsActive);

                    if (existingRate != null)
                    {
                        existingRate.BuyRate = roundedBuy;
                        existingRate.SellRate = roundedSell;
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
                            BuyRate = roundedBuy,
                            SellRate = roundedSell,
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)"
                        };
                        _context.Add(newRate);
                    }
                    updatedCount++;
                    _logger.LogInformation("Updated {Code}->IRR: Buy={BuyRate}, Sell={SellRate}",
                        currency.Code, rate.Value.BuyRate, rate.Value.SellRate);
                }

                // Also maintain base->currency reverse rates
                foreach (var kv in scrapedMap)
                {
                    var currencyId = kv.Key;
                    var (buyToBase, sellToBase) = kv.Value; // currency->base
                    var rev = _rateCalc.ComputeReverseFromBase(buyToBase, sellToBase); // base->currency
                    if (rev == null) continue;

                    var existingRev = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == baseCurrency.Id && r.ToCurrencyId == currencyId && r.IsActive);

                    if (existingRev != null)
                    {
                        existingRev.BuyRate = _rateCalc.SafeRound(rev.Value.buy, 8);
                        existingRev.SellRate = _rateCalc.SafeRound(rev.Value.sell, 8);
                        existingRev.UpdatedAt = DateTime.Now;
                        existingRev.UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)";
                        _context.Update(existingRev);
                    }
                    else
                    {
                        var newRev = new ExchangeRate
                        {
                            FromCurrencyId = baseCurrency.Id,
                            ToCurrencyId = currencyId,
                            BuyRate = _rateCalc.SafeRound(rev.Value.buy, 8),
                            SellRate = _rateCalc.SafeRound(rev.Value.sell, 8),
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = $"{User.Identity?.Name ?? "System"} (Web)"
                        };
                        _context.Add(newRev);
                    }
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
            return RedirectToAction(nameof(Manage));
        }

        // Upsert cross rates among all non-base currencies using fresh currency->base map
        private async Task UpsertCrossRatesFromBaseAsync(int baseCurrencyId, Dictionary<int, (decimal buy, decimal sell)> map)
        {
            var foreignIds = map.Keys.ToList();
            for (int i = 0; i < foreignIds.Count; i++)
            {
                for (int j = 0; j < foreignIds.Count; j++)
                {
                    if (i == j) continue;
                    var fromId = foreignIds[i];
                    var toId = foreignIds[j];
                    var cross = _rateCalc.ComputeCrossFromBase(map[fromId], map[toId]);
                    if (cross == null) continue;

                    var existing = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == fromId && r.ToCurrencyId == toId && r.IsActive);

                    if (existing != null)
                    {
                        existing.BuyRate = _rateCalc.SafeRound(cross.Value.buy, 8);
                        existing.SellRate = _rateCalc.SafeRound(cross.Value.sell, 8);
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
                            BuyRate = _rateCalc.SafeRound(cross.Value.buy, 8),
                            SellRate = _rateCalc.SafeRound(cross.Value.sell, 8),
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = User.Identity?.Name ?? "System"
                        };
                        _context.Add(newCross);
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
