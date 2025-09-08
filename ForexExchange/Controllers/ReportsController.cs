using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class ReportsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(ForexDbContext context, ILogger<ReportsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        // POST: Reports/GetQuickSummary
        [HttpPost]
        public async Task<IActionResult> GetQuickSummary(DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId, int? bankAccountId, string? orderStatus, string? reportType)
        {
            try
            {
                // Default to last 30 days if no dates provided
                fromDate ??= DateTime.Now.AddDays(-30).Date;
                toDate ??= DateTime.Now.Date.AddDays(1).AddTicks(-1);

                // Get total orders
                var ordersQuery = _context.Orders.AsQueryable();
                
                if (fromDate.HasValue && toDate.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
                }
                
                if (customerId.HasValue)
                {
                    ordersQuery = ordersQuery.Where(o => o.CustomerId == customerId);
                }

                var totalOrders = await ordersQuery.CountAsync();

                // Get total volume (approximation)
                var totalVolume = await ordersQuery
                    .SumAsync(o => o.Amount * o.Rate); // Fixed: Use Rate instead of ExchangeRate

                // Get active customers count
                var activeCustomers = await _context.Customers.CountAsync(c => c.IsActive);

                // Get system balance (sum of all bank accounts)
                var systemBalance = await _context.BankAccounts
                    .SumAsync(ba => ba.AccountBalance);

                return Json(new
                {
                    totalOrders,
                    totalVolume = (long)totalVolume,
                    activeCustomers,
                    systemBalance = (long)systemBalance
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quick summary");
                return Json(new { totalOrders = 0, totalVolume = 0, activeCustomers = 0, systemBalance = 0 });
            }
        }

        // POST: Reports/GetOrdersData
        [HttpPost]
        public async Task<IActionResult> GetOrdersData(DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId, int? bankAccountId, string? orderStatus, string? reportType)
        {
            try
            {
                _logger.LogInformation("GetOrdersData called with fromDate: {FromDate}, toDate: {ToDate}", fromDate, toDate);
                
                // If no dates provided, get all orders (don't restrict by date)
                if (!fromDate.HasValue && !toDate.HasValue)
                {
                    // Get all orders without date restriction
                    fromDate = DateTime.MinValue;
                    toDate = DateTime.MaxValue;
                }
                else
                {
                    // Default to last 30 days if only one date provided
                    fromDate ??= DateTime.Now.AddDays(-30).Date;
                    toDate ??= DateTime.Now.Date.AddDays(1).AddTicks(-1);
                }

                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .AsQueryable();

                _logger.LogInformation("Total orders in database: {Count}", await _context.Orders.CountAsync());

                // Apply filters
                if (fromDate.HasValue && toDate.HasValue && fromDate != DateTime.MinValue)
                {
                    query = query.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
                    _logger.LogInformation("Applied date filter: {FromDate} to {ToDate}", fromDate, toDate);
                }

                if (customerId.HasValue)
                {
                    query = query.Where(o => o.CustomerId == customerId);
                    _logger.LogInformation("Applied customer filter: {CustomerId}", customerId);
                }

                if (currencyId.HasValue)
                {
                    query = query.Where(o => o.FromCurrencyId == currencyId || o.ToCurrencyId == currencyId);
                    _logger.LogInformation("Applied currency filter: {CurrencyId}", currencyId);
                }

                var ordersCount = await query.CountAsync();
                _logger.LogInformation("Orders matching filters: {Count}", ordersCount);

                var orders = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(1000) // Limit results
                    .Select(o => new
                    {
                        id = o.Id,
                        createdAt = o.CreatedAt,
                        customerName = o.Customer != null ? o.Customer.FullName : "نامشخص",
                        orderType = "تبدیل ارز",
                        fromCurrency = o.FromCurrency != null ? o.FromCurrency.Code : "نامشخص",
                        amount = o.Amount,
                        toCurrency = o.ToCurrency != null ? o.ToCurrency.Code : "نامشخص",
                        exchangeRate = o.Rate, // Fixed: Use Rate instead of ExchangeRate
                        status = "فعال"
                    })
                    .ToListAsync();

                _logger.LogInformation("Returning {Count} orders", orders.Count);
                return Json(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders data");
                return Json(new List<object>());
            }
        }

        // POST: Reports/GetBalancesData
        [HttpPost]
        public async Task<IActionResult> GetBalancesData(DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId, int? bankAccountId, string? orderStatus, string? reportType)
        {
            try
            {
                var query = _context.CustomerBalances
                    .Include(cb => cb.Customer)
                    .AsQueryable();

                // Apply filters
                if (customerId.HasValue)
                {
                    query = query.Where(cb => cb.CustomerId == customerId);
                }

                if (!string.IsNullOrEmpty(currencyId?.ToString()))
                {
                    // Find currency code by ID first
                    var currency = await _context.Currencies.FindAsync(currencyId);
                    if (currency != null)
                    {
                        query = query.Where(cb => cb.CurrencyCode == currency.Code);
                    }
                }

                var balances = await query
                    .OrderBy(cb => cb.Customer.FullName)
                    .Select(cb => new
                    {
                        customerId = cb.CustomerId,
                        customerName = cb.Customer != null ? cb.Customer.FullName : "نامشخص",
                        currencyCode = cb.CurrencyCode,
                        balance = cb.Balance,
                        lastUpdated = cb.LastUpdated
                    })
                    .ToListAsync();

                return Json(balances);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balances data");
                return Json(new List<object>());
            }
        }

        // POST: Reports/GetBankBalancesData
        [HttpPost]
        public async Task<IActionResult> GetBankBalancesData(DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId, int? bankAccountId, string? orderStatus, string? reportType)
        {
            try
            {
                var query = _context.BankAccounts.AsQueryable();

                // Apply filters
                if (bankAccountId.HasValue)
                {
                    query = query.Where(ba => ba.Id == bankAccountId);
                }

                if (!string.IsNullOrEmpty(currencyId?.ToString()))
                {
                    // Find currency code by ID first
                    var currency = await _context.Currencies.FindAsync(currencyId);
                    if (currency != null)
                    {
                        query = query.Where(ba => ba.CurrencyCode == currency.Code);
                    }
                }

                var bankBalances = await query
                    .OrderBy(ba => ba.BankName)
                    .Select(ba => new
                    {
                        id = ba.Id,
                        bankName = ba.BankName,
                        accountNumber = ba.AccountNumber,
                        currencyCode = ba.CurrencyCode,
                        balance = ba.AccountBalance,
                        lastUpdated = ba.LastModified ?? ba.CreatedAt
                    })
                    .ToListAsync();

                return Json(bankBalances);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bank balances data");
                return Json(new List<object>());
            }
        }

        // POST: Reports/GetChartsData
        [HttpPost]
        public async Task<IActionResult> GetChartsData(DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId, int? bankAccountId, string? orderStatus, string? reportType)
        {
            try
            {
                // Default to last 30 days if no dates provided
                fromDate ??= DateTime.Now.AddDays(-30).Date;
                toDate ??= DateTime.Now.Date.AddDays(1).AddTicks(-1);

                // Currency volume data
                var currencyVolume = await _context.Orders
                    .Include(o => o.FromCurrency)
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                    .GroupBy(o => o.FromCurrency.Code)
                    .Select(g => new { currency = g.Key, volume = g.Sum(o => o.Amount) })
                    .ToListAsync();

                // Daily trend data
                var dailyTrend = await _context.Orders
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                    .GroupBy(o => o.CreatedAt.Date)
                    .Select(g => new { date = g.Key, count = g.Count() })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                // Customer balance distribution
                var customerBalanceDistribution = await _context.CustomerBalances
                    .GroupBy(cb => cb.Balance > 0 ? "بستانکار" : cb.Balance < 0 ? "بدهکار" : "صفر")
                    .Select(g => new { type = g.Key, count = g.Count() })
                    .ToListAsync();

                // Bank balance data
                var bankBalances = await _context.BankAccounts
                    .Select(ba => new { bank = ba.BankName, balance = ba.AccountBalance })
                    .ToListAsync();

                return Json(new
                {
                    currencyVolume = new
                    {
                        labels = currencyVolume.Select(cv => cv.currency).ToArray(),
                        data = currencyVolume.Select(cv => cv.volume).ToArray()
                    },
                    dailyTrend = new
                    {
                        labels = dailyTrend.Select(dt => dt.date.ToString("MM/dd")).ToArray(),
                        data = dailyTrend.Select(dt => dt.count).ToArray()
                    },
                    customerBalance = new
                    {
                        data = customerBalanceDistribution.Select(cbd => cbd.count).ToArray()
                    },
                    bankBalance = new
                    {
                        labels = bankBalances.Select(bb => bb.bank).ToArray(),
                        data = bankBalances.Select(bb => (double)bb.balance / 1000000).ToArray() // Convert to millions
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting charts data");
                return Json(new { });
            }
        }

        // GET: Reports/ExportToExcel
        public IActionResult ExportToExcel(string type, DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId, int? bankAccountId, string? orderStatus)
        {
            try
            {
                // Here you would implement Excel export functionality
                // For now, return a simple CSV response
                
                var fileName = $"{type}_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var contentType = "text/csv";
                
                string csvContent = "";
                
                switch (type)
                {
                    case "orders":
                        csvContent = "ID,Date,Customer,Type,From Currency,Amount,To Currency,Rate,Status\n";
                        break;
                    case "balances":
                        csvContent = "Customer,Currency,Balance,Status,Last Updated\n";
                        break;
                    case "bank_balances":
                        csvContent = "Bank,Account Number,Currency,Balance,Status,Last Updated\n";
                        break;
                }
                
                var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
                return File(bytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting to Excel");
                return BadRequest("خطا در تولید فایل Excel");
            }
        }
    }
}
