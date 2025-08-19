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
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString, 
            string orderTypeFilter, string currencyFilter, string statusFilter)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = String.IsNullOrEmpty(sortOrder) ? "id_desc" : "";
            ViewData["CustomerSortParm"] = sortOrder == "Customer" ? "customer_desc" : "Customer";
            ViewData["OrderTypeSortParm"] = sortOrder == "OrderType" ? "ordertype_desc" : "OrderType";
            ViewData["CurrencySortParm"] = sortOrder == "Currency" ? "currency_desc" : "Currency";
            ViewData["AmountSortParm"] = sortOrder == "Amount" ? "amount_desc" : "Amount";
            ViewData["RateSortParm"] = sortOrder == "Rate" ? "rate_desc" : "Rate";
            ViewData["StatusSortParm"] = sortOrder == "Status" ? "status_desc" : "Status";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            if (searchString != null)
            {
                currentFilter = searchString;
            }

            ViewData["CurrentFilter"] = currentFilter;
            ViewData["OrderTypeFilter"] = orderTypeFilter;
            ViewData["CurrencyFilter"] = currencyFilter;
            ViewData["StatusFilter"] = statusFilter;

            var isAdminOrStaff = await IsAdminOrStaffAsync();
            IQueryable<Order> ordersQuery;

            if (isAdminOrStaff)
            {
                // Admin/Staff can see all orders
                ordersQuery = _context.Orders.Include(o => o.Customer);
            }
            else
            {
                // Customer can only see their own orders
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.CustomerId == null)
                {
                    // If customer doesn't have associated customer record, show empty list
                    return View(new List<Order>());
                }

                ordersQuery = _context.Orders
                    .Include(o => o.Customer)
                    .Where(o => o.CustomerId == currentUser.CustomerId);
            }

            // Apply filters
            if (!String.IsNullOrEmpty(currentFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName.Contains(currentFilter));
            }

            if (!String.IsNullOrEmpty(orderTypeFilter))
            {
                if (Enum.TryParse<OrderType>(orderTypeFilter, out var orderType))
                {
                    ordersQuery = ordersQuery.Where(o => o.OrderType == orderType);
                }
            }

            if (!String.IsNullOrEmpty(currencyFilter))
            {
                if (Enum.TryParse<CurrencyType>(currencyFilter, out var currency))
                {
                    ordersQuery = ordersQuery.Where(o => o.Currency == currency);
                }
            }

            if (!String.IsNullOrEmpty(statusFilter))
            {
                if (Enum.TryParse<OrderStatus>(statusFilter, out var status))
                {
                    ordersQuery = ordersQuery.Where(o => o.Status == status);
                }
            }

            // Apply sorting
            switch (sortOrder)
            {
                case "id_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.Id);
                    break;
                case "Customer":
                    ordersQuery = ordersQuery.OrderBy(o => o.Customer.FullName);
                    break;
                case "customer_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.Customer.FullName);
                    break;
                case "OrderType":
                    ordersQuery = ordersQuery.OrderBy(o => o.OrderType);
                    break;
                case "ordertype_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.OrderType);
                    break;
                case "Currency":
                    ordersQuery = ordersQuery.OrderBy(o => o.Currency);
                    break;
                case "currency_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.Currency);
                    break;
                case "Amount":
                    ordersQuery = ordersQuery.OrderBy(o => o.Amount);
                    break;
                case "amount_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.Amount);
                    break;
                case "Rate":
                    ordersQuery = ordersQuery.OrderBy(o => o.Rate);
                    break;
                case "rate_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.Rate);
                    break;
                case "Status":
                    ordersQuery = ordersQuery.OrderBy(o => o.Status);
                    break;
                case "status_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.Status);
                    break;
                case "Date":
                    ordersQuery = ordersQuery.OrderBy(o => o.CreatedAt);
                    break;
                case "date_desc":
                    ordersQuery = ordersQuery.OrderByDescending(o => o.CreatedAt);
                    break;
                default:
                    ordersQuery = ordersQuery.OrderByDescending(o => o.CreatedAt);
                    break;
            }

            var orders = await ordersQuery.ToListAsync();
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

            // Load available orders for matching if this order is still open
            if (order.Status == OrderStatus.Open)
            {
                var availableOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .Where(o => o.Status == OrderStatus.Open &&
                               o.Currency == order.Currency &&
                               o.OrderType != order.OrderType &&
                               o.Id != order.Id)
                    .ToListAsync();

                // Filter by rate compatibility and sort appropriately
                if (order.OrderType == OrderType.Buy)
                {
                    // For buy orders, show sell orders with rate <= buy rate
                    availableOrders = availableOrders.Where(o => o.Rate <= order.Rate).OrderBy(o => o.Rate).ToList();
                }
                else
                {
                    // For sell orders, show buy orders with rate >= sell rate  
                    availableOrders = availableOrders.Where(o => o.Rate >= order.Rate).OrderByDescending(o => o.Rate).ToList();
                }

                ViewBag.AvailableOrders = availableOrders;
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

            var isAdminOrStaff = await IsAdminOrStaffAsync();

            if (isAdminOrStaff)
            {
                // Admin/Staff can see all customers
                ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
                ViewBag.IsAdminOrStaff = true;
            }
            else
            {
                // Customer can only create orders for themselves
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.CustomerId != null)
                {
                    var customer = await _context.Customers.FindAsync(currentUser.CustomerId);
                    ViewBag.CurrentCustomer = customer;
                }
                ViewBag.IsAdminOrStaff = false;
            }

            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order)
        {
            var isAdminOrStaff = await IsAdminOrStaffAsync();

            // Remove Customer navigation property from validation as we only need CustomerId
            ModelState.Remove("Customer");
            ModelState.Remove("Transactions");
            ModelState.Remove("Receipts");

            // Debug: Log all ModelState errors
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid for order creation:");
                foreach (var modelError in ModelState)
                {
                    foreach (var error in modelError.Value.Errors)
                    {
                        _logger.LogWarning($"Field: {modelError.Key}, Error: {error.ErrorMessage}");
                    }
                }
            }

            // Handle customer assignment based on user role
            if (!isAdminOrStaff)
            {
                // For customers, force the order to be for themselves
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.CustomerId == null)
                {
                    ModelState.AddModelError("", "خطا در شناسایی مشتری. لطفاً مجدداً وارد شوید.");
                    await LoadCreateViewData();
                    return View(order);
                }
                order.CustomerId = currentUser.CustomerId.Value;
            }
            else
            {
                // Admin/Staff must select a customer
                if (order.CustomerId == 0)
                {
                    ModelState.AddModelError("CustomerId", "انتخاب مشتری الزامی است.");
                    await LoadCreateViewData();
                    return View(order);
                }
            }

            // Debug: Log received order data
            _logger.LogInformation($"Order data received - CustomerId: {order.CustomerId}, OrderType: {order.OrderType}, Currency: {order.Currency}, Amount: {order.Amount}");

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
                order.Rate = order.OrderType == OrderType.Buy ? exchangeRate.SellRate : exchangeRate.BuyRate;
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
                .Where(o => (o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled) &&
                           o.Currency == order.Currency &&
                           o.OrderType != order.OrderType &&
                           o.Id != order.Id)
                .ToListAsync();

            // Filter by rate compatibility
            if (order.OrderType == OrderType.Buy)
            {
                // For buy orders, find sell orders with rate <= buy rate
                matchingOrders = matchingOrders.Where(o => o.Rate <= order.Rate).OrderBy(o => o.Rate).ToList();
            }
            else
            {
                // For sell orders, find buy orders with rate >= sell rate  
                matchingOrders = matchingOrders.Where(o => o.Rate >= order.Rate).OrderByDescending(o => o.Rate).ToList();
            }

            if (!matchingOrders.Any())
            {
                string message = order.OrderType == OrderType.Buy
                    ? $"هیچ سفارش فروش با نرخ {order.Rate:N0} تومان یا کمتر یافت نشد."
                    : $"هیچ سفارش خرید با نرخ {order.Rate:N0} تومان یا بیشتر یافت نشد.";
                TempData["InfoMessage"] = message;
                return RedirectToAction(nameof(Details), new { id });
            }

            // Simple matching logic - match with first available order
            var matchingOrder = matchingOrders.First();
            var matchAmount = Math.Min(order.Amount - order.FilledAmount,
                                     matchingOrder.Amount - matchingOrder.FilledAmount);

            // Determine transaction rate - use the better rate for both parties
            // For buy/sell matching, use the price that satisfies both orders
            decimal transactionRate;
            if (order.OrderType == OrderType.Buy)
            {
                // Buyer willing to pay up to order.Rate, seller asking matchingOrder.Rate
                // Use the seller's rate (lower or equal to buyer's rate)
                transactionRate = matchingOrder.Rate;
            }
            else
            {
                // Seller willing to accept order.Rate, buyer offering matchingOrder.Rate  
                // Use the buyer's rate (higher or equal to seller's rate)
                transactionRate = matchingOrder.Rate;
            }

            // Create transaction
            var transaction = new Transaction
            {
                BuyOrderId = order.OrderType == OrderType.Buy ? order.Id : matchingOrder.Id,
                SellOrderId = order.OrderType == OrderType.Sell ? order.Id : matchingOrder.Id,
                BuyerCustomerId = order.OrderType == OrderType.Buy ? order.CustomerId : matchingOrder.CustomerId,
                SellerCustomerId = order.OrderType == OrderType.Sell ? order.CustomerId : matchingOrder.CustomerId,
                Currency = order.Currency,
                Amount = matchAmount,
                Rate = transactionRate,
                TotalInToman = matchAmount * transactionRate,
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

            var isAdminOrStaff = await IsAdminOrStaffAsync();

            if (isAdminOrStaff)
            {
                // Admin/Staff can see all customers
                ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
                ViewBag.IsAdminOrStaff = true;
            }
            else
            {
                // Customer can only create orders for themselves
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.CustomerId != null)
                {
                    var customer = await _context.Customers.FindAsync(currentUser.CustomerId);
                    ViewBag.CurrentCustomer = customer;
                }
                ViewBag.IsAdminOrStaff = false;
            }
        }
    }
}
