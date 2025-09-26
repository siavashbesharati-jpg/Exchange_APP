using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class CurrenciesController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CurrenciesController> _logger;

        public CurrenciesController(ForexDbContext context, ILogger<CurrenciesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Currencies
        public async Task<IActionResult> Index(bool? onlyActive)
        {
            var query = _context.Currencies.AsQueryable();
            if (onlyActive == true)
            {
                query = query.Where(c => c.IsActive);
            }

            var currencies = await query
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Code)
                .ToListAsync();

            ViewBag.OnlyActive = onlyActive == true;
            return View(currencies);
        }

        // GET: Currencies/Create
        public IActionResult Create()
        {
            return View(new Currency { IsActive = true });
        }

        // POST: Currencies/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Code,Name,PersianName,Symbol,IsActive,IsBaseCurrency,DisplayOrder")] Currency model)
        {
            // Normalize
            model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.PersianName = model.PersianName?.Trim() ?? string.Empty;
            model.Symbol = model.Symbol?.Trim() ?? string.Empty;

            if (await _context.Currencies.AnyAsync(c => c.Code == model.Code))
            {
                ModelState.AddModelError("Code", "کد ارز باید یکتا باشد.");
            }

            if (model.IsBaseCurrency && await _context.Currencies.AnyAsync(c => c.IsBaseCurrency))
            {
                ModelState.AddModelError("IsBaseCurrency", "فقط یک ارز پایه مجاز است.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.CreatedAt = DateTime.Now;
            _context.Currencies.Add(model);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "ارز با موفقیت ایجاد شد.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Currencies/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var currency = await _context.Currencies.FindAsync(id);
            if (currency == null) return NotFound();

            return View(currency);
        }

        // POST: Currencies/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Code,Name,PersianName,Symbol,IsActive,IsBaseCurrency,DisplayOrder,CreatedAt")] Currency model)
        {
            if (id != model.Id) return NotFound();

            // Normalize
            model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.PersianName = model.PersianName?.Trim() ?? string.Empty;
            model.Symbol = model.Symbol?.Trim() ?? string.Empty;

            if (await _context.Currencies.AnyAsync(c => c.Code == model.Code && c.Id != model.Id))
            {
                ModelState.AddModelError("Code", "کد ارز باید یکتا باشد.");
            }

            if (model.IsBaseCurrency && await _context.Currencies.AnyAsync(c => c.IsBaseCurrency && c.Id != model.Id))
            {
                ModelState.AddModelError("IsBaseCurrency", "فقط یک ارز پایه مجاز است.");
            }

            // Prevent deactivating base currency
            if (!model.IsActive && model.IsBaseCurrency)
            {
                ModelState.AddModelError("IsActive", "غیرفعال کردن ارز پایه مجاز نیست.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _context.Entry(model).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "اطلاعات ارز بروزرسانی شد.";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Currencies.AnyAsync(c => c.Id == model.Id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Currencies/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var currency = await _context.Currencies.FindAsync(id);
            if (currency == null) return NotFound();

            if (currency.IsBaseCurrency && currency.IsActive == true)
            {
                TempData["ErrorMessage"] = "غیرفعال کردن ارز پایه مجاز نیست.";
                return RedirectToAction(nameof(Index));
            }

            currency.IsActive = !currency.IsActive;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = currency.IsActive ? "ارز فعال شد." : "ارز غیرفعال شد.";
            return RedirectToAction(nameof(Index));
        }
    }
}
