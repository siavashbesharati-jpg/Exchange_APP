using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ForexExchange.Services;
using ForexExchange.Models;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Services.Notifications;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class OrdersController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly ICurrencyPoolService _poolService;
        private readonly AdminActivityService _adminActivityService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICustomerBalanceService _customerBalanceService;
        private readonly INotificationHub _notificationHub;

        public OrdersController(
            ForexDbContext context,
            ILogger<OrdersController> logger,
            ICurrencyPoolService poolService,
            AdminActivityService adminActivityService,
            UserManager<ApplicationUser> userManager,
            ICustomerBalanceService customerBalanceService,
            INotificationHub notificationHub)
        {
            _context = context;
            _logger = logger;
            _poolService = poolService;
            _adminActivityService = adminActivityService;
            _userManager = userManager;
            _customerBalanceService = customerBalanceService;
            _notificationHub = notificationHub;
        }





        // GET: Orders
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString,
            string currencyFilter, string statusFilter, string customerFilter)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["IdSortParm"] = String.IsNullOrEmpty(sortOrder) ? "id_desc" : "";
            ViewData["CustomerSortParm"] = sortOrder == "Customer" ? "customer_desc" : "Customer";
            // Removed OrderType sorting
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
            // Removed OrderType filter
            ViewData["CurrencyFilter"] = currencyFilter;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["CustomerFilter"] = customerFilter;


            IQueryable<Order> ordersQuery;


            ordersQuery = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency);



            // Apply filtering only - no sorting at database level for decimal fields
            if (!String.IsNullOrEmpty(currentFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName.Contains(currentFilter));
            }

            if (!String.IsNullOrEmpty(customerFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName == customerFilter);
            }

            // Removed OrderType filtering

            if (!String.IsNullOrEmpty(currencyFilter))
            {
                // Try to parse currency filter as currency code or ID
                if (int.TryParse(currencyFilter, out var currencyId))
                {
                    ordersQuery = ordersQuery.Where(o => o.FromCurrencyId == currencyId || o.ToCurrencyId == currencyId);
                }
                else
                {
                    // Filter by currency code
                    ordersQuery = ordersQuery.Where(o => o.FromCurrency.Code == currencyFilter || o.ToCurrency.Code == currencyFilter);
                }
            }

            // Apply database-level sorting only for non-decimal fields
            // Load data first, then apply all sorting client-side
            List<Order> orders;

            // For non-decimal fields, we can sort at database level for better performance
            if (sortOrder?.Contains("Amount") == true || sortOrder?.Contains("Rate") == true)
            {
                // Load all data first for decimal sorting
                orders = await ordersQuery.ToListAsync();
            }
            else
            {
                // Apply non-decimal sorting at database level
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
                    // Removed OrderType sorting
                    case "Currency":
                        ordersQuery = ordersQuery.OrderBy(o => o.FromCurrency.Code).ThenBy(o => o.ToCurrency.Code);
                        break;
                    case "currency_desc":
                        ordersQuery = ordersQuery.OrderByDescending(o => o.FromCurrency.Code).ThenByDescending(o => o.ToCurrency.Code);
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
                orders = await ordersQuery.ToListAsync();
            }

            // Apply client-side sorting for decimal fields
            switch (sortOrder)
            {
                case "Amount":
                    orders = orders.OrderBy(o => o.FromAmount).ToList();
                    break;
                case "amount_desc":
                    orders = orders.OrderByDescending(o => o.FromAmount).ToList();
                    break;
                case "Rate":
                    orders = orders.OrderBy(o => o.Rate).ToList();
                    break;
                case "rate_desc":
                    orders = orders.OrderByDescending(o => o.Rate).ToList();
                    break;
            }

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
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                // .Include(o => o.Transactions)
                // .Include(o => o.Receipts)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Debug: Log if currencies are missing
            if (order.FromCurrency == null)
            {
                _logger.LogWarning($"Order {id} has missing FromCurrency (FromCurrencyId: {order.FromCurrencyId})");
            }
            if (order.ToCurrency == null)
            {
                _logger.LogWarning($"Order {id} has missing ToCurrency (ToCurrencyId: {order.ToCurrencyId})");
            }

            // if (order.Transactions.Any())
            // {
                // TODO: Replace with AccountingDocument-based tracking for new architecture
                /*
                // Load the related customers and orders for transactions separately
                var transactionIds = order.Transactions.Select(t => t.Id).ToList();

                await _context.Transactions
                    .Where(t => transactionIds.Contains(t.Id))
                    .Include(t => t.BuyerCustomer)
                    .Include(t => t.SellerCustomer)
                    .Include(t => t.BuyOrder)
                        .ThenInclude(bo => bo.Customer)
                    .Include(t => t.SellOrder)
                        .ThenInclude(so => so.Customer)
                    .LoadAsync();
                */
            // }

            return View(order);
        }

        // GET: Orders/Create
        public async Task<IActionResult> Create(int? customerId = null)
        {
            // Load only essential data with minimal queries
            await LoadCreateViewDataOptimized();

            // If customerId is provided, create an Order model with that customer pre-selected
            if (customerId.HasValue)
            {
                var order = new Order
                {
                    CustomerId = customerId.Value
                };
                return View(order);
            }

            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order)
        {
            // Debug: Log received order data first
            _logger.LogInformation($"Order data received - CustomerId: {order.CustomerId}, FromCurrencyId: {order.FromCurrencyId}, ToCurrencyId: {order.ToCurrencyId}, Amount: {order.FromAmount}, ManualRate: {order.Rate}");

            // Remove Customer navigation property from validation as we only need CustomerId
            ModelState.Remove("Customer");
            ModelState.Remove("Transactions");
            ModelState.Remove("Receipts");
            ModelState.Remove("FromCurrency");
            ModelState.Remove("ToCurrency");
            ModelState.Remove("TotalAmount"); // TotalAmount is calculated server-side

            // Keep Rate in ModelState for manual rate validation
            // ModelState.Remove("Rate"); // Removed - now we validate manual rates

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

                // Reload view data and return with errors
                await LoadCreateViewDataOptimized();
                return View(order);
            }

            // Admin/Staff must select a customer
            if (order.CustomerId == 0)
            {
                ModelState.AddModelError("CustomerId", "انتخاب مشتری الزامی است.");
                await LoadCreateViewDataOptimized();
                return View(order);
            }

            // Validate currency pair
            if (order.FromCurrencyId == order.ToCurrencyId)
            {
                ModelState.AddModelError("ToCurrencyId", "ارز مبدأ و مقصد نمی‌توانند یکسان باشند.");
                await LoadCreateViewDataOptimized();
                return View(order);
            }

            // Validate manual rate if provided
            if (order.Rate <= 0)
            {
                ModelState.AddModelError("Rate", "نرخ ارز باید بزرگتر از صفر باشد.");
                await LoadCreateViewDataOptimized();
                return View(order);
            }

            if (ModelState.IsValid)
            {
                // Calculate system-suggested rate for comparison and fallback
                var directRate = await _context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.FromCurrencyId == order.FromCurrencyId &&
                                             r.ToCurrencyId == order.ToCurrencyId &&
                                             r.IsActive);

                decimal systemExchangeRate = 0;
                string rateSource = "";

                if (directRate != null)
                {
                    systemExchangeRate = directRate.Rate;
                    rateSource = "Direct";
                }
                else
                {
                    // Try reverse rate
                    var reverseRate = await _context.ExchangeRates
                        .FirstOrDefaultAsync(r => r.FromCurrencyId == order.ToCurrencyId &&
                                                 r.ToCurrencyId == order.FromCurrencyId &&
                                                 r.IsActive);

                    if (reverseRate != null)
                    {
                        systemExchangeRate = (1.0m / reverseRate.Rate);
                        rateSource = "Reverse";
                    }
                    else
                    {
                        // Try cross-rate via IRR (base currency)
                        var irrCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency);

                        if (irrCurrency != null)
                        {
                            var fromRate = await _context.ExchangeRates
                                .FirstOrDefaultAsync(r => r.FromCurrencyId == irrCurrency.Id &&
                                                         r.ToCurrencyId == order.FromCurrencyId && r.IsActive);
                            var toRate = await _context.ExchangeRates
                                .FirstOrDefaultAsync(r => r.FromCurrencyId == irrCurrency.Id &&
                                                         r.ToCurrencyId == order.ToCurrencyId && r.IsActive);

                            if (fromRate != null && toRate != null)
                            {
                                var fromToIrrRate = fromRate.Rate;
                                var irrToTargetRate = toRate.Rate;
                                systemExchangeRate = irrToTargetRate / fromToIrrRate;
                                rateSource = "Cross-rate";
                            }
                        }
                    }
                }

                // Determine which rate to use
                decimal finalExchangeRate;
                string finalRateSource;

                if (systemExchangeRate > 0)
                {
                    // Check if manual rate differs significantly from system rate (more than 10%)
                    decimal rateDifference = Math.Abs(order.Rate - systemExchangeRate) / systemExchangeRate;
                    if (rateDifference > 0.1m) // 10% difference
                    {
                        _logger.LogWarning($"Manual rate {order.Rate} differs significantly from system rate {systemExchangeRate} ({rateDifference:P2}) for order creation");
                        // Still allow the manual rate but log the discrepancy
                    }

                    finalExchangeRate = order.Rate;
                    finalRateSource = $"Manual (System: {systemExchangeRate} {rateSource})";
                }
                else
                {
                    // No system rate available, use manual rate
                    finalExchangeRate = order.Rate;
                    finalRateSource = "Manual (No system rate)";
                }

                // Calculate total and set order properties
                order.Rate = finalExchangeRate;
                var totalValue = order.FromAmount * order.Rate;
                order.ToAmount = totalValue;

                _context.Add(order);
                await _context.SaveChangesAsync();

                // Load related entities for notification
                await _context.Entry(order).Reference(o => o.Customer).LoadAsync();
                await _context.Entry(order).Reference(o => o.FromCurrency).LoadAsync();
                await _context.Entry(order).Reference(o => o.ToCurrency).LoadAsync();

                // Update customer balances for the order
                _logger.LogInformation("About to call ProcessOrderCreationAsync for Order {OrderId}", order.Id);
                await _customerBalanceService.ProcessOrderCreationAsync(order);
                _logger.LogInformation("Completed ProcessOrderCreationAsync for Order {OrderId}", order.Id);

                // Log admin activity and send notifications
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await _adminActivityService.LogOrderCreatedAsync(order, currentUser.Id, currentUser.UserName ?? "Unknown");
                    
                    // Send notifications through central hub (replaces individual notification calls)
                    await _notificationHub.SendOrderNotificationAsync(order, NotificationEventType.OrderCreated, currentUser.Id);
                }

                // Update currency pools
                // Add to FromCurrency pool (reduce available currency)
                await _poolService.UpdatePoolAsync(order.FromCurrencyId, order.FromAmount, PoolTransactionType.Buy, order.Rate);
                // Add to ToCurrency pool (increase available currency)
                await _poolService.UpdatePoolAsync(order.ToCurrencyId, order.FromAmount * order.Rate, PoolTransactionType.Sell, order.Rate);

                // Update order counts for both currencies
                await _poolService.UpdateOrderCountsAsync(order.FromCurrencyId);
                await _poolService.UpdateOrderCountsAsync(order.ToCurrencyId);

                // Update average rates for this currency pair
                await UpdateAverageRatesForPairAsync(order.FromCurrencyId, order.ToCurrencyId);

                _logger.LogInformation($"Order created successfully - Id: {order.Id}, Rate: {finalExchangeRate} ({finalRateSource}), Total: {totalValue}");

                TempData["SuccessMessage"] = "معامله با موفقیت ثبت شد.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            await LoadCreateViewDataOptimized();
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

            await LoadCreateViewDataOptimized();
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

            // Remove navigation properties and computed fields from validation; we bind by Ids
            ModelState.Remove("Customer");
            ModelState.Remove("Transactions");
            ModelState.Remove("Receipts");
            ModelState.Remove("FromCurrency");
            ModelState.Remove("ToCurrency");
            ModelState.Remove("TotalAmount");

            // Basic server-side validations (parity with Create)
            if (order.CustomerId == 0)
            {
                ModelState.AddModelError("CustomerId", "انتخاب مشتری الزامی است.");
            }
            if (order.FromCurrencyId == 0)
            {
                ModelState.AddModelError("FromCurrencyId", "انتخاب ارز مبدأ الزامی است.");
            }
            if (order.ToCurrencyId == 0)
            {
                ModelState.AddModelError("ToCurrencyId", "انتخاب ارز مقصد الزامی است.");
            }
            if (order.FromCurrencyId == order.ToCurrencyId && order.FromCurrencyId != 0)
            {
                ModelState.AddModelError("ToCurrencyId", "ارز مبدأ و مقصد نمی‌توانند یکسان باشند.");
            }
            if (order.Rate <= 0)
            {
                ModelState.AddModelError("Rate", "نرخ ارز باید بزرگتر از صفر باشد.");
            }

            if (ModelState.IsValid)
                {
                    try
                    {
                        // Get the original order for balance reversal
                        var originalOrder = await _context.Orders
                            .Include(o => o.FromCurrency)
                            .Include(o => o.ToCurrency)
                            .AsNoTracking()
                            .FirstOrDefaultAsync(o => o.Id == id);

                        if (originalOrder == null)
                        {
                            return NotFound();
                        }

                        // Recompute totals on server for integrity
                        var totalValue = order.FromAmount * order.Rate;

                        order.ToAmount = totalValue;
                        order.UpdatedAt = DateTime.Now;                    _context.Update(order);
                    await _context.SaveChangesAsync();

                    // Load related entities for notification
                    await _context.Entry(order).Reference(o => o.Customer).LoadAsync();
                    await _context.Entry(order).Reference(o => o.FromCurrency).LoadAsync();
                    await _context.Entry(order).Reference(o => o.ToCurrency).LoadAsync();

                    // Update customer balances for the order edit (reverse old, apply new)
                    await _customerBalanceService.ProcessOrderEditAsync(originalOrder, order);

                    // Log admin activity
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        await _adminActivityService.LogOrderUpdatedAsync(order, currentUser.Id, currentUser.UserName ?? "Unknown");
                        await _notificationHub.SendOrderNotificationAsync(order, NotificationEventType.OrderUpdated, currentUser.Id);
                    }

                    TempData["SuccessMessage"] = "معامله با موفقیت بروزرسانی شد.";
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

            await LoadCreateViewDataOptimized();
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
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: Orders/CancelWithReverseOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelWithReverseOrder(int id, decimal reverseRate)
        {
            var originalOrder = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (originalOrder == null)
            {
                TempData["ErrorMessage"] = "معامله یافت نشد.";
                return RedirectToAction(nameof(Index));
            }

            // Validate reverse rate
            if (reverseRate <= 0)
            {
                TempData["ErrorMessage"] = "نرخ معکوس باید بزرگتر از صفر باشد.";
                return RedirectToAction(nameof(Delete), new { id });
            }


            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {

                    originalOrder.UpdatedAt = DateTime.Now;
                    _context.Update(originalOrder);

                    // Create reverse order with corrected calculations
                    var reverseOrder = new Order
                    {
                        CustomerId = originalOrder.CustomerId,
                        FromCurrencyId = originalOrder.ToCurrencyId,     // Reverse currencies: IRR
                        ToCurrencyId = originalOrder.FromCurrencyId,     // USD
                        FromAmount = originalOrder.ToAmount,              // Total from original order: 10,300,000 IRR
                        Rate = reverseRate,                              // User-input rate: 104,000
                        CreatedAt = DateTime.Now,
                        ToAmount = originalOrder.ToAmount / reverseRate   // Correct calculation: 10,300,000 / 104,000 = 99
                    };



                    _context.Add(reverseOrder);
                    await _context.SaveChangesAsync();

                    // Update currency pools for the new reverse order
                    await _poolService.UpdatePoolAsync(reverseOrder.FromCurrencyId, reverseOrder.FromAmount, PoolTransactionType.Buy, reverseOrder.Rate);
                    await _poolService.UpdatePoolAsync(reverseOrder.ToCurrencyId, reverseOrder.ToAmount, PoolTransactionType.Sell, reverseOrder.Rate);

                    // Update order counts
                    await _poolService.UpdateOrderCountsAsync(reverseOrder.FromCurrencyId);
                    await _poolService.UpdateOrderCountsAsync(reverseOrder.ToCurrencyId);


                    await transaction.CommitAsync();

                    // Load related entities for notifications
                    await _context.Entry(reverseOrder).Reference(o => o.Customer).LoadAsync();
                    await _context.Entry(reverseOrder).Reference(o => o.FromCurrency).LoadAsync();
                    await _context.Entry(reverseOrder).Reference(o => o.ToCurrency).LoadAsync();

                    // Log admin activity
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser != null)
                    {
                        await _adminActivityService.LogOrderCancelledAsync(originalOrder, currentUser.Id, currentUser.UserName ?? "Unknown");
                        await _adminActivityService.LogOrderCreatedAsync(reverseOrder, currentUser.Id, currentUser.UserName ?? "Unknown");
                        await _notificationHub.SendOrderNotificationAsync(originalOrder, NotificationEventType.OrderCancelled, currentUser.Id);
                        await _notificationHub.SendOrderNotificationAsync(reverseOrder, NotificationEventType.OrderCreated, currentUser.Id);
                    }

                    TempData["SuccessMessage"] = $"معامله لغو شد و معامله معکوس با نرخ {reverseRate:N4} ایجاد شد.";
                    return RedirectToAction(nameof(Details), new { id = reverseOrder.Id });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error cancelling order {id} and creating reverse order");
                    TempData["ErrorMessage"] = "خطا در لغو معامله و ایجاد معامله معکوس.";
                    return RedirectToAction(nameof(Delete), new { id });
                }
            }
        }

        

      
        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }

        private async Task LoadCreateViewDataOptimized()
        {
            // Load minimal currency data for dropdowns (just ID, Code, Name)
            var currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.Code, c.Name, c.DisplayOrder })
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            // Create SelectListItem for proper binding
            ViewBag.FromCurrencies = currencies.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Code} - {c.Name}"
            }).ToList();

            ViewBag.ToCurrencies = currencies.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Code} - {c.Name}"
            }).ToList();

            // Load minimal customer data for dropdown (just ID and FullName)
            var customers = _context.Customers
                .Where(c => c.IsActive && c.IsSystem == false);

            ViewBag.Customers = customers.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.FullName
            }).ToList();

            ViewBag.IsAdminOrStaff = true;

            // Simplified: load a dictionary mapping currency code to balance
            var pools = await _poolService.GetAllPoolsAsync();
            // Use p.Currency.Code if available, else p.CurrencyCode
            var poolDict = pools
                .GroupBy(p => !string.IsNullOrWhiteSpace(p.Currency?.Code) ? p.Currency.Code : p.CurrencyCode)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Balance));
            ViewBag.PoolData = poolDict;

            // Don't load exchange rates here - they will be loaded via AJAX when needed
            // This eliminates the heavy Include operations
        }

        // New AJAX endpoint to get exchange rates for specific currency pair
        [HttpGet]
        public async Task<IActionResult> GetExchangeRate(int fromCurrencyId, int toCurrencyId, string orderType)
        {
            try
            {
                // Try to find direct exchange rate
                var directRate = await _context.ExchangeRates
                    .Where(r => r.FromCurrencyId == fromCurrencyId &&
                               r.ToCurrencyId == toCurrencyId &&
                               r.IsActive)
                    .Select(r => new { r.Rate, r.AverageBuyRate, r.AverageSellRate })
                    .FirstOrDefaultAsync();

                decimal rate = 0;
                decimal? averageBuyRate = null;
                decimal? averageSellRate = null;
                string source = "";

                if (directRate != null)
                {
                    rate = directRate.Rate;
                    averageBuyRate = directRate.AverageBuyRate;
                    averageSellRate = directRate.AverageSellRate;
                    source = "Direct";
                }
                else
                {
                    // Try reverse rate
                    var reverseRate = await _context.ExchangeRates
                        .Where(r => r.FromCurrencyId == toCurrencyId &&
                                   r.ToCurrencyId == fromCurrencyId &&
                                   r.IsActive)
                        .Select(r => new { r.Rate, r.AverageBuyRate, r.AverageSellRate })
                        .FirstOrDefaultAsync();

                    if (reverseRate != null)
                    {
                        rate = (1.0m / reverseRate.Rate);
                        // For reverse rates, swap buy/sell averages
                        averageBuyRate = reverseRate.AverageSellRate.HasValue ? (1.0m / reverseRate.AverageSellRate.Value) : null;
                        averageSellRate = reverseRate.AverageBuyRate.HasValue ? (1.0m / reverseRate.AverageBuyRate.Value) : null;
                        source = "Reverse";
                    }
                    else
                    {
                        // Try cross-rate via base currency
                        var baseCurrencyId = await _context.Currencies
                            .Where(c => c.IsBaseCurrency)
                            .Select(c => c.Id)
                            .FirstOrDefaultAsync();

                        if (baseCurrencyId > 0)
                        {
                            var fromRate = await _context.ExchangeRates
                                .Where(r => r.FromCurrencyId == baseCurrencyId &&
                                           r.ToCurrencyId == fromCurrencyId && r.IsActive)
                                .Select(r => new { r.Rate, r.AverageBuyRate, r.AverageSellRate })
                                .FirstOrDefaultAsync();

                            var toRate = await _context.ExchangeRates
                                .Where(r => r.FromCurrencyId == baseCurrencyId &&
                                           r.ToCurrencyId == toCurrencyId && r.IsActive)
                                .Select(r => new { r.Rate, r.AverageBuyRate, r.AverageSellRate })
                                .FirstOrDefaultAsync();

                            if (fromRate != null && toRate != null)
                            {
                                rate = toRate.Rate / fromRate.Rate;
                                // For cross rates, calculate averages proportionally
                                if (fromRate.AverageBuyRate.HasValue && toRate.AverageBuyRate.HasValue)
                                {
                                    averageBuyRate = toRate.AverageBuyRate.Value / fromRate.AverageBuyRate.Value;
                                }
                                if (fromRate.AverageSellRate.HasValue && toRate.AverageSellRate.HasValue)
                                {
                                    averageSellRate = toRate.AverageSellRate.Value / fromRate.AverageSellRate.Value;
                                }
                                source = "Cross-rate";
                            }
                        }
                    }
                }

                return Json(new
                {
                    success = true,
                    rate = rate,
                    averageBuyRate = averageBuyRate,
                    averageSellRate = averageSellRate,
                    source = source
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange rate for {FromCurrency} to {ToCurrency}", fromCurrencyId, toCurrencyId);
                return Json(new { success = false, error = "خطا در دریافت نرخ ارز" });
            }
        }

        // AJAX endpoint to get customers list
        [HttpGet]
        public async Task<IActionResult> GetCustomers(string search = "")
        {
            try
            {
                var query = _context.Customers.Where(c => c.IsActive);

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(c => c.FullName.Contains(search));
                }

                var customers = await query
                    .Select(c => new { c.Id, c.FullName })
                    .Take(50) // Limit results to prevent large responses
                    .ToListAsync();

                return Json(new { success = true, customers = customers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers list");
                return Json(new { success = false, error = "خطا در دریافت لیست مشتریان" });
            }
        }

        // Diagnostic endpoint to check for data integrity issues
        [HttpGet]
        public async Task<IActionResult> DiagnoseDataIntegrity()
        {
            try
            {
                // Check for orders with missing currencies
                var ordersWithMissingCurrencies = await _context.Orders
                    .Where(o => !_context.Currencies.Any(c => c.Id == o.FromCurrencyId) ||
                               !_context.Currencies.Any(c => c.Id == o.ToCurrencyId))
                    .Select(o => new
                    {
                        o.Id,
                        o.FromCurrencyId,
                        o.ToCurrencyId,
                        FromCurrencyExists = _context.Currencies.Any(c => c.Id == o.FromCurrencyId),
                        ToCurrencyExists = _context.Currencies.Any(c => c.Id == o.ToCurrencyId)
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    ordersWithMissingCurrencies = ordersWithMissingCurrencies,
                    totalOrders = await _context.Orders.CountAsync(),
                    totalCurrencies = await _context.Currencies.CountAsync()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error diagnosing data integrity");
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Helper method to find the optimal matching combination
        private List<Order> FindOptimalMatchingCombination(List<Order> availableOrders, decimal targetAmount)
        {
            // Sort by size descending (largest first)
            var sortedBySize = availableOrders.OrderByDescending(o => o.FromAmount).ToList();

            // Greedy algorithm: take largest orders that fit without exceeding target
            var selectedOrders = new List<Order>();
            decimal currentSum = 0;

            foreach (var order in sortedBySize)
            {
                var availableAmount = order.FromAmount;

                // If adding this order would exceed target, skip it
                if (currentSum + availableAmount > targetAmount)
                {
                    continue; // Skip this order, try next smaller one
                }

                // Add this order if it fits
                selectedOrders.Add(order);
                currentSum += availableAmount;

                // If we've reached or exceeded target, stop
                if (currentSum >= targetAmount)
                {
                    break;
                }
            }

            return selectedOrders;
        }

        /// <summary>
        /// Update average rates for a currency pair based on active orders
        /// بروزرسانی نرخ‌های میانگین برای جفت ارز بر اساس معاملهات فعال
        /// </summary>
        private async Task UpdateAverageRatesForPairAsync(int fromCurrencyId, int toCurrencyId)
        {
            // Find the exchange rate for this pair
            var exchangeRate = await _context.ExchangeRates
                .FirstOrDefaultAsync(er => er.FromCurrencyId == fromCurrencyId &&
                                         er.ToCurrencyId == toCurrencyId &&
                                         er.IsActive);

            if (exchangeRate == null) return;

            // Calculate average buy rate (orders where ToCurrencyId matches)
            var buyOrders = await _context.Orders
                .Where(o => o.FromCurrencyId == fromCurrencyId &&
                           o.ToCurrencyId == toCurrencyId)
                .ToListAsync();

            if (buyOrders.Any())
            {
                // Weighted average buy rate
                var totalBuyVolume = buyOrders.Sum(o => o.FromAmount);
                var weightedBuyRate = buyOrders.Sum(o => o.FromAmount * o.Rate) / totalBuyVolume;
                exchangeRate.AverageBuyRate = weightedBuyRate;
                exchangeRate.TotalBuyVolume = totalBuyVolume;
            }
            else
            {
                exchangeRate.AverageBuyRate = null;
                exchangeRate.TotalBuyVolume = 0;
            }

            // Calculate average sell rate (orders where FromCurrencyId matches)
            var sellOrders = await _context.Orders
                .Where(o => o.FromCurrencyId == toCurrencyId &&
                           o.ToCurrencyId == fromCurrencyId)
                .ToListAsync();

            if (sellOrders.Any())
            {
                // Weighted average sell rate
                var totalSellVolume = sellOrders.Sum(o => o.FromAmount);
                var weightedSellRate = sellOrders.Sum(o => o.FromAmount * o.Rate) / totalSellVolume;
                exchangeRate.AverageSellRate = weightedSellRate;
                exchangeRate.TotalSellVolume = totalSellVolume;
            }
            else
            {
                exchangeRate.AverageSellRate = null;
                exchangeRate.TotalSellVolume = 0;
            }

            exchangeRate.UpdatedAt = DateTime.Now;
            _context.Update(exchangeRate);
            await _context.SaveChangesAsync();
        }
    }
}
