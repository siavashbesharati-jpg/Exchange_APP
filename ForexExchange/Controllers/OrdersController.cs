using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Authorize] // All actions require authentication
    public class OrdersController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrdersController(ForexDbContext context, ILogger<OrdersController> logger, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // Helper method to check if user is admin or staff
        private async Task<bool> IsAdminOrStaffAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user != null && (user.Role == UserRole.Admin || user.Role == UserRole.Operator || user.Role == UserRole.Manager);
        }

        // Helper method to get current user's customer ID
        private async Task<int?> GetCurrentCustomerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.CustomerId;
        }

        // GET: Orders
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Transactions)
                .Include(o => o.Receipts)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // GET: Orders/Create
        public async Task<IActionResult> Create()
        {
            // Get active exchange rates
            var exchangeRates = await _context.ExchangeRates
                .Where(r => r.IsActive)
                .ToListAsync();

            ViewBag.ExchangeRates = exchangeRates;
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();

            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order)
        {
            if (ModelState.IsValid)
            {
                // Get the current exchange rate
                var exchangeRate = await _context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.Currency == order.Currency && r.IsActive);

                if (exchangeRate == null)
                {
                    ModelState.AddModelError("Currency", "نرخ ارز مورد نظر موجود نیست.");
                    await LoadCreateViewData();
                    return View(order);
                }

                // Calculate rate and total based on order type
                order.Rate = order.OrderType == OrderType.Buy ? exchangeRate.BuyRate : exchangeRate.SellRate;
                order.TotalInToman = order.Amount * order.Rate;
                order.CreatedAt = DateTime.Now;
                order.Status = OrderStatus.Open;

                _context.Add(order);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "سفارش با موفقیت ثبت شد.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            await LoadCreateViewData();
            return View(order);
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            await LoadCreateViewData();
            return View(order);
        }

        // POST: Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Order order)
        {
            if (id != order.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    order.UpdatedAt = DateTime.Now;
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "سفارش با موفقیت بروزرسانی شد.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.Id))
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

            await LoadCreateViewData();
            return View(order);
        }

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order != null)
            {
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.Now;
                _context.Update(order);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "سفارش لغو شد.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Orders/Match/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Match(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null || order.Status != OrderStatus.Open)
            {
                TempData["ErrorMessage"] = "سفارش موجود نیست یا قابل مچ کردن نمی‌باشد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Find matching orders
            var matchingOrders = await _context.Orders
                .Include(o => o.Customer)
                .Where(o => o.Status == OrderStatus.Open &&
                           o.Currency == order.Currency &&
                           o.OrderType != order.OrderType &&
                           o.Id != order.Id)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            if (!matchingOrders.Any())
            {
                TempData["InfoMessage"] = "هیچ سفارش مطابقی یافت نشد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Simple matching logic - match with first available order
            var matchingOrder = matchingOrders.First();
            var matchAmount = Math.Min(order.Amount - order.FilledAmount, 
                                     matchingOrder.Amount - matchingOrder.FilledAmount);

            // Create transaction
            var transaction = new Transaction
            {
                BuyOrderId = order.OrderType == OrderType.Buy ? order.Id : matchingOrder.Id,
                SellOrderId = order.OrderType == OrderType.Sell ? order.Id : matchingOrder.Id,
                BuyerCustomerId = order.OrderType == OrderType.Buy ? order.CustomerId : matchingOrder.CustomerId,
                SellerCustomerId = order.OrderType == OrderType.Sell ? order.CustomerId : matchingOrder.CustomerId,
                Currency = order.Currency,
                Amount = matchAmount,
                Rate = order.Rate,
                TotalInToman = matchAmount * order.Rate,
                Status = TransactionStatus.Pending,
                CreatedAt = DateTime.Now
            };

            _context.Transactions.Add(transaction);

            // Update order statuses
            order.FilledAmount += matchAmount;
            matchingOrder.FilledAmount += matchAmount;

            if (order.FilledAmount >= order.Amount)
                order.Status = OrderStatus.Completed;
            else
                order.Status = OrderStatus.PartiallyFilled;

            if (matchingOrder.FilledAmount >= matchingOrder.Amount)
                matchingOrder.Status = OrderStatus.Completed;
            else
                matchingOrder.Status = OrderStatus.PartiallyFilled;

            order.UpdatedAt = DateTime.Now;
            matchingOrder.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"سفارش با موفقیت مچ شد. تراکنش شماره {transaction.Id} ایجاد شد.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }

        private async Task LoadCreateViewData()
        {
            var exchangeRates = await _context.ExchangeRates
                .Where(r => r.IsActive)
                .ToListAsync();

            ViewBag.ExchangeRates = exchangeRates;
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
        }
    }
}
