using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Globalization;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class ReportsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ReportsController> _logger;
        private readonly ISettingsService _settingsService;

        public ReportsController(ForexDbContext context, ILogger<ReportsController> logger, ISettingsService settingsService)
        {
            _context = context;
            _logger = logger;
            _settingsService = settingsService;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reports/Financial
        public async Task<IActionResult> Financial(DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId)
        {
            // Default to last 30 days if no dates provided
            fromDate ??= DateTime.Now.AddDays(-30).Date;
            toDate ??= DateTime.Now.Date;

            // Ensure fromDate starts at beginning of day and toDate includes the entire day
            var fromDateTime = fromDate.Value.Date;
            var toDateTime = toDate.Value.Date.AddDays(1).AddTicks(-1); // End of the day

            var query = _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.BuyOrder)
                .Include(t => t.SellOrder)
                .Include(t => t.FromCurrency)
                .Include(t => t.ToCurrency)
                .Where(t => t.CreatedAt >= fromDateTime && t.CreatedAt <= toDateTime);

            if (customerId.HasValue)
            {
                query = query.Where(t => t.BuyerCustomerId == customerId || t.SellerCustomerId == customerId);
            }

            if (currencyId.HasValue)
            {
                query = query.Where(t => t.FromCurrencyId == currencyId || t.ToCurrencyId == currencyId);
            }

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            // Calculate financial metrics
            var report = new FinancialReport
            {
                FromDate = fromDate.Value,
                ToDate = toDate.Value,
                TotalTransactions = transactions.Count,
                CompletedTransactions = transactions.Count(t => t.Status == TransactionStatus.Completed),
                PendingTransactions = transactions.Count(t => t.Status == TransactionStatus.Pending || 
                                                           t.Status == TransactionStatus.PaymentUploaded || 
                                                           t.Status == TransactionStatus.ReceiptConfirmed),
                FailedTransactions = transactions.Count(t => t.Status == TransactionStatus.Failed),
                TotalVolumeInToman = transactions.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.TotalInToman),
                TotalCommissionEarned = transactions.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.TotalInToman * 0.005m), // 0.5% commission
                Transactions = transactions
            };

            // Currency breakdown
            report.CurrencyBreakdown = transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .GroupBy(t => t.FromCurrencyId)
                .Select(g => new CurrencyVolumeReport
                {
                    CurrencyId = g.Key,
                    CurrencyCode = g.First().FromCurrency?.Code ?? "",
                    CurrencyName = g.First().FromCurrency?.Name ?? "",
                    TotalVolume = g.Sum(t => t.Amount),
                    TransactionCount = g.Count(),
                    TotalValueInToman = g.Sum(t => t.TotalInToman),
                    AverageRate = g.Average(t => t.Rate)
                })
                .ToList();

            // Daily breakdown for chart
            report.DailyBreakdown = transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new DailyVolumeReport
                {
                    Date = g.Key,
                    TransactionCount = g.Count(),
                    TotalVolumeInToman = g.Sum(t => t.TotalInToman)
                })
                .OrderBy(d => d.Date)
                .ToList();

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive)
                .Select(c => new { Id = c.Id, Name = c.FullName })
                .ToListAsync();

            // Load currencies for filter dropdown
            ViewBag.Currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.PersianName)
                .Select(c => new { c.Id, c.Code, c.PersianName })
                .ToListAsync();

            // Preserve filter values in ViewBag
            ViewBag.SelectedCustomerId = customerId;
            ViewBag.SelectedCurrencyId = currencyId;

            return View(report);
        }

        // GET: Reports/CustomerActivity
        public async Task<IActionResult> CustomerActivity(DateTime? fromDate, DateTime? toDate)
        {
            fromDate ??= DateTime.Now.AddDays(-30).Date;
            toDate ??= DateTime.Now.Date;

            // Ensure fromDate starts at beginning of day and toDate includes the entire day
            var fromDateTime = fromDate.Value.Date;
            var toDateTime = toDate.Value.Date.AddDays(1).AddTicks(-1); // End of the day

            var customerActivity = await _context.Customers
                .Include(c => c.Orders.Where(o => o.CreatedAt >= fromDateTime && o.CreatedAt <= toDateTime))
                .Include(c => c.BuyTransactions.Where(t => t.CreatedAt >= fromDateTime && t.CreatedAt <= toDateTime))
                .Include(c => c.SellTransactions.Where(t => t.CreatedAt >= fromDateTime && t.CreatedAt <= toDateTime))
                .Where(c => c.IsActive)
                .Select(c => new CustomerActivityReport
                {
                    Customer = c,
                    TotalOrders = c.Orders.Count,
                    CompletedOrders = c.Orders.Count(o => o.Status == OrderStatus.Completed),
                    TotalTransactions = c.BuyTransactions.Count + c.SellTransactions.Count,
                    CompletedTransactions = c.BuyTransactions.Count(t => t.Status == TransactionStatus.Completed) + 
                                          c.SellTransactions.Count(t => t.Status == TransactionStatus.Completed),
                    TotalVolumeInToman = c.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalInToman),
                    LastActivityDate = c.Orders.Any() ? c.Orders.Max(o => o.CreatedAt) : c.CreatedAt
                })
                .ToListAsync();

            // Apply client-side sorting for decimal field (TotalVolumeInToman)
            customerActivity = customerActivity.OrderByDescending(c => c.TotalVolumeInToman).ToList();

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            return View(customerActivity);
        }

        // GET: Reports/OrderBook
        public async Task<IActionResult> OrderBook(int? currencyId, string? currency)
        {
            var query = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.PartiallyFilled);

            if (currencyId.HasValue)
            {
                query = query.Where(o => o.FromCurrencyId == currencyId || o.ToCurrencyId == currencyId);
            }
            else if (!string.IsNullOrEmpty(currency))
            {
                query = query.Where(o => o.FromCurrency.Symbol == currency || o.ToCurrency.Symbol == currency);
            }

            // Load currencies for filter dropdown
            ViewBag.Currencies = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.PersianName)
                .ToListAsync();
            
            ViewBag.SelectedCurrency = currency;

            // Load data first, then sort client-side for decimal fields
            var orders = await query
                .OrderBy(o => o.OrderType)
                .ToListAsync();

            // Apply client-side sorting for decimal field (Rate)
            orders = orders
                .OrderBy(o => o.OrderType)
                .ThenByDescending(o => o.Rate)
                .ToList();

            var orderBook = orders
                .GroupBy(o => new { o.FromCurrencyId, o.ToCurrencyId, o.OrderType })
                .Select(g => new OrderBookReport
                {
                    FromCurrencyId = g.Key.FromCurrencyId,
                    ToCurrencyId = g.Key.ToCurrencyId,
                    FromCurrencyCode = g.First().FromCurrency?.Code ?? "",
                    ToCurrencyCode = g.First().ToCurrency?.Code ?? "",
                    OrderType = g.Key.OrderType,
                    // Client-side sorting for decimal Rate field
                    Orders = g.OrderBy(o => o.OrderType == OrderType.Buy ? -o.Rate : o.Rate).ToList(),
                    TotalVolume = g.Sum(o => o.Amount - o.FilledAmount),
                    AverageRate = g.Average(o => o.Rate),
                    OrderCount = g.Count()
                })
                .ToList();

            return View(orderBook);
        }

        // GET: Reports/Commission
        public async Task<IActionResult> Commission(DateTime? fromDate, DateTime? toDate)
        {
            fromDate ??= DateTime.Now.AddDays(-30).Date;
            toDate ??= DateTime.Now.Date;

            // Ensure fromDate starts at beginning of day and toDate includes the entire day
            var fromDateTime = fromDate.Value.Date;
            var toDateTime = toDate.Value.Date.AddDays(1).AddTicks(-1); // End of the day

            var completedTransactions = await _context.Transactions
                .Where(t => t.Status == TransactionStatus.Completed && 
                           t.CompletedAt >= fromDateTime && t.CompletedAt <= toDateTime)
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .ToListAsync();

            // Get dynamic rates from settings
            var commissionRate = await _settingsService.GetCommissionRateAsync();
            var exchangeFeeRate = await _settingsService.GetExchangeFeeRateAsync();

            var commissionReport = new CommissionReport
            {
                FromDate = fromDate.Value,
                ToDate = toDate.Value,
                TotalTransactions = completedTransactions.Count,
                TotalVolumeInToman = completedTransactions.Sum(t => t.TotalInToman),
                TotalCommissionEarned = completedTransactions.Sum(t => t.TotalInToman * commissionRate),
                TotalExchangeFeesEarned = completedTransactions.Sum(t => t.TotalInToman * exchangeFeeRate),
                AverageTransactionValue = completedTransactions.Any() ? completedTransactions.Average(t => t.TotalInToman) : 0,
                DailyCommissions = completedTransactions
                    .Where(t => t.CompletedAt.HasValue)
                    .GroupBy(t => t.CompletedAt!.Value.Date)
                    .Select(g => new DailyCommissionReport
                    {
                        Date = g.Key,
                        TransactionCount = g.Count(),
                        TotalVolume = g.Sum(t => t.TotalInToman),
                        CommissionEarned = g.Sum(t => t.TotalInToman * commissionRate),
                        ExchangeFeesEarned = g.Sum(t => t.TotalInToman * exchangeFeeRate)
                    })
                    .OrderBy(d => d.Date)
                    .ToList()
            };

            return View(commissionReport);
        }

        // API: Export financial report to CSV
        [HttpGet]
        public async Task<IActionResult> ExportFinancial(DateTime? fromDate, DateTime? toDate, int? customerId, int? currencyId)
        {
            fromDate ??= DateTime.Now.AddDays(-30);
            toDate ??= DateTime.Now;

            var query = _context.Transactions
                .Include(t => t.BuyerCustomer)
                .Include(t => t.SellerCustomer)
                .Include(t => t.FromCurrency)
                .Include(t => t.ToCurrency)
                .Where(t => t.CreatedAt >= fromDate && t.CreatedAt <= toDate);

            if (customerId.HasValue)
            {
                query = query.Where(t => t.BuyerCustomerId == customerId || t.SellerCustomerId == customerId);
            }

            if (currencyId.HasValue)
            {
                query = query.Where(t => t.FromCurrencyId == currencyId || t.ToCurrencyId == currencyId);
            }

            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var csv = GenerateTransactionsCsv(transactions);
            var fileName = $"financial_report_{fromDate?.ToString("yyyyMMdd") ?? "unknown"}_{toDate?.ToString("yyyyMMdd") ?? "unknown"}.csv";

            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }

        private string GenerateTransactionsCsv(List<Transaction> transactions)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,تاریخ ایجاد,خریدار,فروشنده,ارز,مقدار,نرخ,کل به تومان,وضعیت,تاریخ تکمیل");

            foreach (var transaction in transactions)
            {
                csv.AppendLine($"{transaction.Id}," +
                              $"{transaction.CreatedAt:yyyy/MM/dd HH:mm}," +
                              $"{transaction.BuyerCustomer.FullName}," +
                              $"{transaction.SellerCustomer.FullName}," +
                              $"{transaction.FromCurrency?.Code}-{transaction.ToCurrency?.Code}," +
                              $"{transaction.Amount}," +
                              $"{transaction.Rate}," +
                              $"{transaction.TotalInToman}," +
                              $"{GetStatusText(transaction.Status)}," +
                              $"{transaction.CompletedAt?.ToString("yyyy/MM/dd HH:mm") ?? "-"}");
            }

            return csv.ToString();
        }

        private string GetStatusText(TransactionStatus status)
        {
            return status switch
            {
                TransactionStatus.Pending => "در انتظار",
                TransactionStatus.PaymentUploaded => "رسید آپلود شده",
                TransactionStatus.ReceiptConfirmed => "رسید تأیید شده",
                TransactionStatus.Completed => "تکمیل شده",
                TransactionStatus.Failed => "ناموفق",
                _ => status.ToString()
            };
        }
    }

    // Report model classes
    public class FinancialReport
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public int PendingTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public decimal TotalVolumeInToman { get; set; }
        public decimal TotalCommissionEarned { get; set; }
        public List<Transaction> Transactions { get; set; } = new();
        public List<CurrencyVolumeReport> CurrencyBreakdown { get; set; } = new();
        public List<DailyVolumeReport> DailyBreakdown { get; set; } = new();
    }

    public class CurrencyVolumeReport
    {
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = "";
        public string CurrencyName { get; set; } = "";
        public decimal TotalVolume { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalValueInToman { get; set; }
        public decimal AverageRate { get; set; }
    }

    public class DailyVolumeReport
    {
        public DateTime Date { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalVolumeInToman { get; set; }
    }

    public class CustomerActivityReport
    {
        public Customer Customer { get; set; } = null!;
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int TotalTransactions { get; set; }
        public int CompletedTransactions { get; set; }
        public decimal TotalVolumeInToman { get; set; }
        public DateTime LastActivityDate { get; set; }
    }

    public class OrderBookReport
    {
        public int FromCurrencyId { get; set; }
        public int ToCurrencyId { get; set; }
        public string FromCurrencyCode { get; set; } = "";
        public string ToCurrencyCode { get; set; } = "";
        public OrderType OrderType { get; set; }
        public List<Order> Orders { get; set; } = new();
        public decimal TotalVolume { get; set; }
        public decimal AverageRate { get; set; }
        public int OrderCount { get; set; }
    }

    public class CommissionReport
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalVolumeInToman { get; set; }
        public decimal TotalCommissionEarned { get; set; }
        public decimal TotalExchangeFeesEarned { get; set; }
        public decimal AverageTransactionValue { get; set; }
        public List<DailyCommissionReport> DailyCommissions { get; set; } = new();
    }

    public class DailyCommissionReport
    {
        public DateTime Date { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalVolume { get; set; }
        public decimal CommissionEarned { get; set; }
        public decimal ExchangeFeesEarned { get; set; }
    }
}
