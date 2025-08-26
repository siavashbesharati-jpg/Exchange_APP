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
                    // Removed OrderType sorting
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
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Include(o => o.Transactions)
                .Include(o => o.Receipts)
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

            if (order.Transactions.Any())
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

            // Load available orders for matching if this order is still open or partially filled
            if (order.Status == OrderStatus.Open || order.Status == OrderStatus.PartiallyFilled)
            {
                var availableOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => (o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled) &&
                               // Find complementary currency pairs
                               ((o.FromCurrencyId == order.ToCurrencyId && o.ToCurrencyId == order.FromCurrencyId)) &&
                               o.Id != order.Id &&
                               o.Amount > o.FilledAmount)
                    .ToListAsync();

                ViewBag.AvailableOrders = availableOrders;
            }

            return View(order);
        }

        // GET: Orders/Create
        public async Task<IActionResult> Create()
        {
            // Load only essential data with minimal queries
            await LoadCreateViewDataOptimized();
            return View();
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order)
        {
            // Debug: Log received order data first
            _logger.LogInformation($"Order data received - CustomerId: {order.CustomerId}, FromCurrencyId: {order.FromCurrencyId}, ToCurrencyId: {order.ToCurrencyId}, Amount: {order.Amount}");

            // Remove Customer navigation property from validation as we only need CustomerId
            ModelState.Remove("Customer");
            ModelState.Remove("Transactions");
            ModelState.Remove("Receipts");
            ModelState.Remove("FromCurrency");
            ModelState.Remove("ToCurrency");
            ModelState.Remove("Rate"); // Rate is calculated server-side
            ModelState.Remove("TotalAmount"); // TotalAmount is calculated server-side
            ModelState.Remove("TotalInToman"); // TotalInToman is calculated server-side

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
                    exchangeRate = directRate.SellRate; // Always use SellRate for simplicity
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
                        exchangeRate = (1.0m / reverseRate.BuyRate); // Always use BuyRate for simplicity
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
                                var fromToIrrRate = fromRate.SellRate;
                                var irrToTargetRate = toRate.BuyRate;
                                exchangeRate = irrToTargetRate / fromToIrrRate;
                                rateSource = "Cross-rate";
                            }
                        }
                    }
                }

                if (exchangeRate <= 0)
                {
                    ModelState.AddModelError("", "نرخ ارز برای این جفت ارز موجود نیست.");
                    await LoadCreateViewDataOptimized();
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

                // Update currency pools if order is open/pending
                if (order.Status == OrderStatus.Open)
                {
                    // Add to FromCurrency pool (reduce available currency)
                    await _poolService.UpdatePoolAsync(order.FromCurrencyId, order.Amount, PoolTransactionType.Buy, order.Rate);
                    // Add to ToCurrency pool (increase available currency)
                    await _poolService.UpdatePoolAsync(order.ToCurrencyId, order.Amount * order.Rate, PoolTransactionType.Sell, order.Rate);
                }

                _logger.LogInformation($"Order created successfully - Id: {order.Id}, Rate: {exchangeRate} ({rateSource}), Total: {totalValue}");

                TempData["SuccessMessage"] = "سفارش با موفقیت ثبت شد.";
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
                // Revert currency pool changes if order was open/pending
                if (order.Status == OrderStatus.Open)
                {
                    // Revert FromCurrency pool (increase available currency)
                    await _poolService.UpdatePoolAsync(order.FromCurrencyId, order.Amount, PoolTransactionType.Sell, order.Rate);
                    // Revert ToCurrency pool (decrease available currency)
                    await _poolService.UpdatePoolAsync(order.ToCurrencyId, order.Amount * order.Rate, PoolTransactionType.Buy, order.Rate);
                }
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
                           ((o.FromCurrencyId == order.ToCurrencyId && o.ToCurrencyId == order.FromCurrencyId)) &&
                           o.Id != order.Id &&
                           o.Amount > o.FilledAmount) // Ensure order has remaining amount
                .ToListAsync();

            // Filter by rate compatibility
            // No OrderType: just sort by best rate (lowest for buyer, highest for seller)
            matchingOrders = matchingOrders.OrderBy(o => o.Rate).ToList();

            if (!matchingOrders.Any())
            {
                TempData["InfoMessage"] = $"هیچ سفارش مچ با نرخ مناسب یافت نشد.";
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

                // Determine direction: who is buyer/seller for this currency pair
                int buyOrderId, sellOrderId, buyerCustomerId, sellerCustomerId;
                ForexExchange.Models.Order buyOrder, sellOrder;
                if (order.FromCurrencyId == matchingOrder.ToCurrencyId && order.ToCurrencyId == matchingOrder.FromCurrencyId)
                {
                    // order is selling FromCurrency, matchingOrder is buying it
                    buyOrderId = matchingOrder.Id;
                    sellOrderId = order.Id;
                    buyerCustomerId = matchingOrder.CustomerId;
                    sellerCustomerId = order.CustomerId;
                    buyOrder = matchingOrder;
                    sellOrder = order;
                }
                else if (order.ToCurrencyId == matchingOrder.FromCurrencyId && order.FromCurrencyId == matchingOrder.ToCurrencyId)
                {
                    // order is buying ToCurrency, matchingOrder is selling it
                    buyOrderId = order.Id;
                    sellOrderId = matchingOrder.Id;
                    buyerCustomerId = order.CustomerId;
                    sellerCustomerId = matchingOrder.CustomerId;
                    buyOrder = order;
                    sellOrder = matchingOrder;
                }
                else
                {
                    // Not a valid match, skip
                    continue;
                }

                // Use the best rate (lowest for buyer, highest for seller)
                decimal transactionRate = Math.Min(order.Rate, matchingOrder.Rate);

                // Create transaction
                var transaction = new Transaction
                {
                    BuyOrderId = buyOrderId,
                    SellOrderId = sellOrderId,
                    BuyerCustomerId = buyerCustomerId,
                    SellerCustomerId = sellerCustomerId,
                    FromCurrency = sellOrder.FromCurrency,
                    ToCurrency = sellOrder.ToCurrency,
                    Amount = matchAmount,
                    Rate = transactionRate,
                    TotalAmount = matchAmount * transactionRate,
                    TotalInToman = sellOrder.ToCurrency.IsBaseCurrency ? (matchAmount * transactionRate) :
                                 (sellOrder.FromCurrency.IsBaseCurrency ? matchAmount : (matchAmount * transactionRate * 65000)), // Approximate conversion
                    Status = TransactionStatus.Pending,
                    CreatedAt = DateTime.Now
                };

                _context.Transactions.Add(transaction);

                // Update order statuses and filled amounts
                order.FilledAmount += matchAmount;
                matchingOrder.FilledAmount += matchAmount;

                // Correct status assignment for both orders
                if (order.FilledAmount == order.Amount)
                    order.Status = OrderStatus.Completed;
                else if (order.FilledAmount > 0)
                    order.Status = OrderStatus.PartiallyFilled;

                if (matchingOrder.FilledAmount == matchingOrder.Amount)
                    matchingOrder.Status = OrderStatus.Completed;
                else if (matchingOrder.FilledAmount > 0)
                    matchingOrder.Status = OrderStatus.PartiallyFilled;

                order.UpdatedAt = DateTime.Now;
                matchingOrder.UpdatedAt = DateTime.Now;

                remainingAmount -= matchAmount;
                matchCount++;

                await _context.SaveChangesAsync();

                // No pool update here

                createdTransactions.Add(transaction.Id);
            }

            if (matchCount == 0)
            {
                TempData["InfoMessage"] = $"هیچ سفارش مچ با نرخ مناسب یافت نشد.";
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
            var customers = await _context.Customers
                .Where(c => c.IsActive)
                .Select(c => new { c.Id, c.FullName })
                .ToListAsync();

            ViewBag.Customers = customers.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.FullName
            }).ToList();
            
            ViewBag.IsAdminOrStaff = true;

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
                    .Select(r => new { r.BuyRate, r.SellRate })
                    .FirstOrDefaultAsync();

                decimal rate = 0;
                string source = "";

                if (directRate != null)
                {
                    rate = orderType == "Buy" ? directRate.SellRate : directRate.BuyRate;
                    source = "Direct";
                }
                else
                {
                    // Try reverse rate
                    var reverseRate = await _context.ExchangeRates
                        .Where(r => r.FromCurrencyId == toCurrencyId && 
                                   r.ToCurrencyId == fromCurrencyId && 
                                   r.IsActive)
                        .Select(r => new { r.BuyRate, r.SellRate })
                        .FirstOrDefaultAsync();

                    if (reverseRate != null)
                    {
                        rate = orderType == "Buy" ? 
                            (1.0m / reverseRate.BuyRate) : (1.0m / reverseRate.SellRate);
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
                                .Select(r => new { r.BuyRate, r.SellRate })
                                .FirstOrDefaultAsync();

                            var toRate = await _context.ExchangeRates
                                .Where(r => r.FromCurrencyId == baseCurrencyId && 
                                           r.ToCurrencyId == toCurrencyId && r.IsActive)
                                .Select(r => new { r.BuyRate, r.SellRate })
                                .FirstOrDefaultAsync();

                            if (fromRate != null && toRate != null)
                            {
                                var fromToBaseRate = orderType == "Buy" ? fromRate.SellRate : fromRate.BuyRate;
                                var baseToTargetRate = orderType == "Buy" ? toRate.BuyRate : toRate.SellRate;
                                rate = baseToTargetRate / fromToBaseRate;
                                source = "Cross-rate";
                            }
                        }
                    }
                }

                return Json(new { success = true, rate = rate, source = source });
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
                    .Select(o => new { 
                        o.Id, 
                        o.FromCurrencyId, 
                        o.ToCurrencyId,
                        FromCurrencyExists = _context.Currencies.Any(c => c.Id == o.FromCurrencyId),
                        ToCurrencyExists = _context.Currencies.Any(c => c.Id == o.ToCurrencyId)
                    })
                    .ToListAsync();

                return Json(new { 
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
    }
}
