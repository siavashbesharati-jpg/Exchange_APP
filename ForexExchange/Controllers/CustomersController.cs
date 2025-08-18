using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class CustomersController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ForexDbContext context, ILogger<CustomersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Customers
        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.SellerCustomer)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.BuyerCustomer)
                .Include(c => c.Receipts.OrderByDescending(r => r.UploadedAt))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customers/Profile/5 - Comprehensive customer profile
        public async Task<IActionResult> Profile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.SellerCustomer)
                .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                    .ThenInclude(t => t.BuyerCustomer)
                .Include(c => c.Receipts.OrderByDescending(r => r.UploadedAt))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Calculate customer statistics
            var stats = new CustomerProfileStats
            {
                TotalOrders = customer.Orders.Count,
                CompletedOrders = customer.Orders.Count(o => o.Status == OrderStatus.Completed),
                PendingOrders = customer.Orders.Count(o => o.Status == OrderStatus.Open),
                TotalTransactions = customer.BuyTransactions.Count + customer.SellTransactions.Count,
                CompletedTransactions = customer.BuyTransactions.Count(t => t.Status == TransactionStatus.Completed) + 
                                     customer.SellTransactions.Count(t => t.Status == TransactionStatus.Completed),
                TotalReceipts = customer.Receipts.Count,
                VerifiedReceipts = customer.Receipts.Count(r => r.IsVerified),
                TotalVolumeInToman = customer.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalInToman),
                RegistrationDays = (DateTime.Now - customer.CreatedAt).Days
            };

            ViewBag.CustomerStats = stats;
            return View(customer);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                // Check if phone number already exists
                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == customer.PhoneNumber && c.IsActive);

                if (existingCustomer != null)
                {
                    ModelState.AddModelError("PhoneNumber", "مشتری با این شماره تلفن قبلاً ثبت شده است.");
                    return View(customer);
                }

                customer.CreatedAt = DateTime.Now;
                customer.IsActive = true;

                _context.Add(customer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "مشتری با موفقیت ثبت شد.";
                return RedirectToAction(nameof(Details), new { id = customer.Id });
            }
            return View(customer);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if phone number already exists for another customer
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.PhoneNumber == customer.PhoneNumber && 
                                           c.Id != customer.Id && c.IsActive);

                    if (existingCustomer != null)
                    {
                        ModelState.AddModelError("PhoneNumber", "مشتری دیگری با این شماره تلفن موجود است.");
                        return View(customer);
                    }

                    _context.Update(customer);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "اطلاعات مشتری با موفقیت بروزرسانی شد.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.Id))
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
            return View(customer);
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                // Soft delete - just mark as inactive
                customer.IsActive = false;
                _context.Update(customer);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "مشتری غیرفعال شد.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: API endpoint for customer search
        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            var customers = await _context.Customers
                .Where(c => c.IsActive && 
                           (c.FullName.Contains(term) || c.PhoneNumber.Contains(term)))
                .Select(c => new { 
                    id = c.Id, 
                    text = $"{c.FullName} - {c.PhoneNumber}"
                })
                .Take(10)
                .ToListAsync();

            return Json(customers);
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
