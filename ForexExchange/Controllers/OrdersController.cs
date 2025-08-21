using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ForexExchange.Services;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class OrdersController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly ICurrencyPoolService _poolService;

        public OrdersController(ForexDbContext context, ILogger<OrdersController> logger, ICurrencyPoolService poolService)
        {
            _context = context;
            _logger = logger;
            _poolService = poolService;
        }





        // GET: Orders
        public async Task<IActionResult> Index(string sortOrder, string currentFilter, string searchString,
            string orderTypeFilter, string currencyFilter, string statusFilter, string customerFilter)
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
            ViewData["CustomerFilter"] = customerFilter;


            IQueryable<Order> ordersQuery;


            ordersQuery = _context.Orders.Include(o => o.Customer);



            // Apply filtering only - no sorting at database level for decimal fields
            if (!String.IsNullOrEmpty(currentFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName.Contains(currentFilter));
            }

            if (!String.IsNullOrEmpty(customerFilter))
            {
                ordersQuery = ordersQuery.Where(o => o.Customer.FullName == customerFilter);
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

            if (!String.IsNullOrEmpty(statusFilter))
            {
                if (Enum.TryParse<OrderStatus>(statusFilter, out var status))
                {
                    ordersQuery = ordersQuery.Where(o => o.Status == status);
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
                    case "OrderType":
                        ordersQuery = ordersQuery.OrderBy(o => o.OrderType);
                        break;
                    case "ordertype_desc":
                        ordersQuery = ordersQuery.OrderByDescending(o => o.OrderType);
                        break;
                    case "Currency":
                        ordersQuery = ordersQuery.OrderBy(o => o.FromCurrency.Code).ThenBy(o => o.ToCurrency.Code);
                        break;
                    case "currency_desc":
                        ordersQuery = ordersQuery.OrderByDescending(o => o.FromCurrency.Code).ThenByDescending(o => o.ToCurrency.Code);
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
                orders = await ordersQuery.ToListAsync();
            }

            // Apply client-side sorting for decimal fields
            switch (sortOrder)
            {
                case "Amount":
                    orders = orders.OrderBy(o => o.Amount).ToList();
                    break;
                case "amount_desc":
                    orders = orders.OrderByDescending(o => o.Amount).ToList();
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
                .Include(o => o.Transactions)
                .Include(o => o.Receipts)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order != null && order.Transactions.Any())
            {
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
            }

            if (order == null)
            {
                return NotFound();
            }

            // Load available orders for matching if this order is still open or partially filled
            if (order.Status == OrderStatus.Open || order.Status == OrderStatus.PartiallyFilled)
            {
                var availableOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => (o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled) &&
                               // Find complementary currency pairs
                               ((o.FromCurrencyId == order.ToCurrencyId && o.ToCurrencyId == order.FromCurrencyId) ||
                                (o.FromCurrencyId == order.FromCurrencyId && o.ToCurrencyId == order.ToCurrencyId)) &&
                               o.OrderType != order.OrderType &&
                               o.Id != order.Id &&
                               o.Amount > o.FilledAmount) // Only orders with remaining amount
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
            // Get active exchange rates with currency navigation properties
            var exchangeRates = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .ToListAsync();

            ViewBag.ExchangeRates = exchangeRates;

            // Get active currencies for dropdowns
            var currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            ViewBag.FromCurrencies = currencies;
            ViewBag.ToCurrencies = currencies;

            // Admin/Staff can see all customers
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();

            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order)
        {

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



            // Admin/Staff must select a customer
            if (order.CustomerId == 0)
            {
                ModelState.AddModelError("CustomerId", "انتخاب مشتری الزامی است.");
                await LoadCreateViewData();
                return View(order);
            }


            // Debug: Log received order data
            _logger.LogInformation($"Order data received - CustomerId: {order.CustomerId}, OrderType: {order.OrderType}, FromCurrencyId: {order.FromCurrencyId}, ToCurrencyId: {order.ToCurrencyId}, Amount: {order.Amount}");

            // Validate currency pair
            if (order.FromCurrencyId == order.ToCurrencyId)
            {
                ModelState.AddModelError("ToCurrencyId", "ارز مبدأ و مقصد نمی‌توانند یکسان باشند.");
            }

            if (ModelState.IsValid)
            {
                // Try to find direct exchange rate
                var directRate = await _context.ExchangeRates
                    .FirstOrDefaultAsync(r => r.FromCurrencyId == order.FromCurrencyId &&
                                             r.ToCurrencyId == order.ToCurrencyId &&
                                             r.IsActive);

                decimal exchangeRate = 0;
                string rateSource = "";

                if (directRate != null)
                {
                    exchangeRate = order.OrderType == OrderType.Buy ? directRate.SellRate : directRate.BuyRate;
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
                        exchangeRate = order.OrderType == OrderType.Buy ?
                            (1.0m / reverseRate.BuyRate) : (1.0m / reverseRate.SellRate);
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
                                var fromToIrrRate = order.OrderType == OrderType.Buy ? fromRate.SellRate : fromRate.BuyRate;
                                var irrToTargetRate = order.OrderType == OrderType.Buy ? toRate.BuyRate : toRate.SellRate;
                                exchangeRate = irrToTargetRate / fromToIrrRate;
                                rateSource = "Cross-rate";
                            }
                        }
                    }
                }

                if (exchangeRate <= 0)
                {
                    ModelState.AddModelError("", "نرخ ارز برای این جفت ارز موجود نیست.");
                    await LoadCreateViewData();
                    return View(order);
                }

                // Calculate total and set order properties
                order.Rate = exchangeRate;
                var totalValue = order.Amount * order.Rate;

                // Calculate TotalInToman for reporting (approximate if not IRR-based)
                var baseCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency);
                if (baseCurrency != null)
                {
                    if (order.FromCurrencyId == baseCurrency.Id)
                    {
                        order.TotalInToman = order.Amount;
                    }
                    else if (order.ToCurrencyId == baseCurrency.Id)
                    {
                        order.TotalInToman = totalValue;
                    }
                    else
                    {
                        // Approximate IRR value using USD rate as base
                        var usdCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "USD" && c.IsActive);
                        if (usdCurrency != null)
                        {
                            var usdRate = await _context.ExchangeRates
                                .FirstOrDefaultAsync(r => r.FromCurrencyId == baseCurrency.Id &&
                                                         r.ToCurrencyId == usdCurrency.Id && r.IsActive);
                            order.TotalInToman = totalValue * (usdRate?.BuyRate ?? 65000);
                        }
                        else
                        {
                            order.TotalInToman = totalValue * 65000; // Default fallback
                        }
                    }
                }
                else
                {
                    order.TotalInToman = totalValue * 65000; // Default fallback
                }

                order.CreatedAt = DateTime.Now;
                order.Status = OrderStatus.Open;
                order.FilledAmount = 0;

                _context.Add(order);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Order created successfully - Id: {order.Id}, Rate: {exchangeRate} ({rateSource}), Total: {totalValue}");

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

            if (order == null || (order.Status != OrderStatus.Open && order.Status != OrderStatus.PartiallyFilled) || order.Amount <= order.FilledAmount)
            {
                TempData["ErrorMessage"] = "سفارش موجود نیست یا قابل مچ کردن نمی‌باشد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Find matching orders - only those with remaining amount to fill
            var matchingOrders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => (o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled) &&
                           // Find complementary currency pairs
                           ((o.FromCurrencyId == order.ToCurrencyId && o.ToCurrencyId == order.FromCurrencyId) ||
                            (o.FromCurrencyId == order.FromCurrencyId && o.ToCurrencyId == order.ToCurrencyId)) &&
                           o.OrderType != order.OrderType &&
                           o.Id != order.Id &&
                           o.Amount > o.FilledAmount) // Ensure order has remaining amount
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

            // Enhanced matching logic - match with best available orders
            decimal remainingAmount = order.Amount - order.FilledAmount;
            int matchCount = 0;
            var createdTransactions = new List<int>();

            foreach (var matchingOrder in matchingOrders)
            {
                if (remainingAmount <= 0) break; // Order fully filled

                var availableAmount = matchingOrder.Amount - matchingOrder.FilledAmount;
                if (availableAmount <= 0) continue; // Skip fully filled orders

                var matchAmount = Math.Min(remainingAmount, availableAmount);

                // Determine transaction rate - use the better rate for both parties
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
                    FromCurrency = order.FromCurrency,
                    ToCurrency = order.ToCurrency,
                    Amount = matchAmount,
                    Rate = transactionRate,
                    TotalAmount = matchAmount * transactionRate,
                    TotalInToman = order.ToCurrency.IsBaseCurrency ? (matchAmount * transactionRate) :
                                 (order.FromCurrency.IsBaseCurrency ? matchAmount : (matchAmount * transactionRate * 65000)), // Approximate conversion
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

                remainingAmount -= matchAmount;
                matchCount++;

                await _context.SaveChangesAsync();

                // Update currency pool after transaction creation
                try
                {
                    await _poolService.ProcessTransactionAsync(transaction);
                    _logger.LogInformation($"Currency pool updated for transaction {transaction.Id}");
                }
                catch (Exception poolEx)
                {
                    _logger.LogError(poolEx, $"Failed to update currency pool for transaction {transaction.Id}");
                    // Don't fail the matching process for pool update errors
                }

                createdTransactions.Add(transaction.Id);
            }

            if (matchCount == 0)
            {
                string message = order.OrderType == OrderType.Buy
                    ? $"هیچ سفارش فروش با نرخ {order.Rate:N0} تومان یا کمتر یافت نشد."
                    : $"هیچ سفارش خرید با نرخ {order.Rate:N0} تومان یا بیشتر یافت نشد.";
                TempData["InfoMessage"] = message;
            }
            else if (matchCount == 1)
            {
                TempData["SuccessMessage"] = $"سفارش با موفقیت مچ شد. تراکنش شماره {createdTransactions[0]} ایجاد شد.";
            }
            else
            {
                TempData["SuccessMessage"] = $"سفارش با {matchCount} سفارش مچ شد. تراکنش‌های شماره {string.Join(", ", createdTransactions)} ایجاد شدند.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }

        private async Task LoadCreateViewData()
        {
            var exchangeRates = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive)
                .ToListAsync();

            ViewBag.ExchangeRates = exchangeRates;

            // Load active currencies for dropdown selections
            var currencies = await _context.Currencies.Where(c => c.IsActive).ToListAsync();
            ViewBag.Currencies = currencies;
            ViewBag.FromCurrencies = currencies;
            ViewBag.ToCurrencies = currencies;



            // Admin/Staff can see all customers
            ViewBag.Customers = await _context.Customers.Where(c => c.IsActive).ToListAsync();
            ViewBag.IsAdminOrStaff = true;


        }
    }
}
