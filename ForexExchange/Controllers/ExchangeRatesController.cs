using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    public class ExchangeRatesController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ExchangeRatesController> _logger;

        public ExchangeRatesController(ForexDbContext context, ILogger<ExchangeRatesController> logger)
        {
            _context = context;
            _logger = logger;
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

                exchangeRate.UpdatedAt = DateTime.Now;
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

        private bool ExchangeRateExists(int id)
        {
            return _context.ExchangeRates.Any(e => e.Id == id);
        }
    }
}
