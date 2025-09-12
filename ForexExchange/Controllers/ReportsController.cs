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

        // GET: Reports/Comprehensive
        public IActionResult Comprehensive()
        {
            return View();
        }

        // GET: Reports/CustomerReports
        public IActionResult CustomerReports()
        {
            return View();
        }

        // GET: Reports/OrderReports
        public IActionResult OrderReports()
        {
            return View();
        }

        // GET: Reports/DocumentReports
        public IActionResult DocumentReports()
        {
            return View();
        }

        // GET: Reports/PoolReports
        public IActionResult PoolReports()
        {
            return View();
        }

        // GET: Reports/AdminReports
        public IActionResult AdminReports()
        {
            return View();
        }

        // API Methods for Real Data

        // GET: Reports/GetCustomersData
        [HttpGet]
        public async Task<IActionResult> GetCustomersData()
        {
            try
            {
                var customers = await _context.Customers
                    .Include(c => c.Balances)
                    .Where(c => c.IsActive && !c.IsSystem )
                    .Select(c => new
                    {
                        id = c.Id,
                        fullName = c.FullName,
                        phoneNumber = c.PhoneNumber,
                        email = c.Email,
                        createdAt = c.CreatedAt,
                        isActive = c.IsActive,
                        balances = c.Balances.Select(b => new
                        {
                            currencyCode = b.CurrencyCode,
                            currencyName = b.CurrencyCode,
                            amount = b.Balance
                        }).ToList(),
                        totalBalanceIRR = c.Balances.Where(b => b.CurrencyCode == "IRR").Sum(b => b.Balance)
                    })
                    .OrderByDescending(c => c.createdAt)
                    .ToListAsync();

                var totalCustomers = customers.Count;
                var activeToday = customers.Count(c => c.createdAt.Date == DateTime.Today);
                var totalBalance = customers.Sum(c => c.totalBalanceIRR);

                return Json(new
                {
                    customers,
                    stats = new
                    {
                        totalCustomers,
                        activeToday,
                        totalBalance
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers data");
                return Json(new { error = "خطا در دریافت اطلاعات مشتریان" });
            }
        }

        // GET: Reports/GetOrdersData
        [HttpGet]
        public async Task<IActionResult> GetOrdersData(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                var orders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                    .Select(o => new
                    {
                        id = o.Id,
                        createdAt = o.CreatedAt,
                        customerName = o.Customer.FullName,
                        fromCurrency = o.FromCurrency.Code,
                        toCurrency = o.ToCurrency.Code,
                        amount = o.FromAmount,
                        rate = o.Rate,
                        totalValue = o.ToAmount,
                        status = "تکمیل شده" // All orders are complete since FilledAmount is removed
                    })
                    .OrderByDescending(o => o.createdAt)
                    .ToListAsync();

                var totalOrders = orders.Count;
                var totalVolume = orders.Sum(o => o.totalValue);
                var completedOrders = orders.Count; // All orders are completed since FilledAmount is removed
                var pendingOrders = 0; // No pending orders since FilledAmount is removed

                return Json(new
                {
                    orders,
                    stats = new
                    {
                        totalOrders,
                        totalVolume,
                        completedOrders,
                        pendingOrders,
                        averageOrderValue = totalOrders > 0 ? totalVolume / totalOrders : 0
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders data");
                return Json(new { error = "خطا در دریافت اطلاعات سفارشات" });
            }
        }

        // GET: Reports/GetDocumentsData
        [HttpGet]
        public async Task<IActionResult> GetDocumentsData(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                var accountingDocs = await _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Where(ad => ad.DocumentDate >= fromDate && ad.DocumentDate <= toDate)
                    .Select(ad => new
                    {
                        id = ad.Id,
                        date = ad.DocumentDate,
                        type = "سند حسابداری",
                        customerName = ad.PayerCustomer != null ? ad.PayerCustomer.FullName : (ad.ReceiverCustomer != null ? ad.ReceiverCustomer.FullName : "نامشخص"),
                        amount = ad.Amount,
                        description = ad.Description,
                        status = "تایید شده"
                    })
                    .ToListAsync();

                // Skip receipts since Receipt model is empty
                var allDocuments = accountingDocs.OrderByDescending(d => d.date).ToList();

                var totalDocuments = allDocuments.Count;
                var totalReceipts = 0; // No receipts available
                var totalAmount = allDocuments.Sum(d => d.amount);
                var todayDocuments = allDocuments.Count(d => d.date.Date == DateTime.Today);

                return Json(new
                {
                    documents = allDocuments,
                    stats = new
                    {
                        totalDocuments,
                        totalReceipts,
                        totalAmount,
                        todayDocuments
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents data");
                return Json(new { error = "خطا در دریافت اطلاعات اسناد" });
            }
        }

        // GET: Reports/GetPoolData
        [HttpGet]
        public async Task<IActionResult> GetPoolData()
        {
            try
            {
                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .Select(c => new
                    {
                        currency = c.Code,
                        name = c.Name,
                        balance = _context.CustomerBalances
                            .Where(cb => cb.CurrencyCode == c.Code)
                            .Sum(cb => cb.Balance),
                        buyRate = _context.ExchangeRates
                            .Where(er => er.FromCurrencyId == c.Id || er.ToCurrencyId == c.Id)
                            .OrderByDescending(er => er.UpdatedAt)
                            .Select(er => er.AverageBuyRate)
                            .FirstOrDefault(),
                        sellRate = _context.ExchangeRates
                            .Where(er => er.FromCurrencyId == c.Id || er.ToCurrencyId == c.Id)
                            .OrderByDescending(er => er.UpdatedAt)
                            .Select(er => er.AverageSellRate)
                            .FirstOrDefault(),
                        lastUpdate = _context.ExchangeRates
                            .Where(er => er.FromCurrencyId == c.Id || er.ToCurrencyId == c.Id)
                            .OrderByDescending(er => er.UpdatedAt)
                            .Select(er => er.UpdatedAt)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                var totalCurrencies = currencies.Count;
                var totalValue = currencies.Sum(c => c.balance * (c.sellRate ?? 0));
                var dailyTransactions = await _context.Orders
                    .Where(o => o.CreatedAt.Date == DateTime.Today)
                    .CountAsync();

                return Json(new
                {
                    currencies,
                    stats = new
                    {
                        totalCurrencies,
                        totalValue,
                        dailyTransactions,
                        lastUpdate = DateTime.Now
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pool data");
                return Json(new { error = "خطا در دریافت اطلاعات پول" });
            }
        }

        // GET: Reports/GetAdminData
        [HttpGet]
        public async Task<IActionResult> GetAdminData(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                fromDate ??= DateTime.Today.AddDays(-7);
                toDate ??= DateTime.Today.AddDays(1);

                var activities = new List<object>();

                // Get recent orders as admin activities
                var recentOrders = await _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                    .Take(50)
                    .Select(o => new
                    {
                        time = o.CreatedAt,
                        user = o.Customer.FullName,
                        type = "معامله",
                        description = $"ایجاد سفارش {o.FromCurrency.Code} به {o.ToCurrency.Code}",
                        ip = "192.168.1.100", // You might want to store this in your model
                        status = "موفق" // All orders are successful since FilledAmount is removed
                    })
                    .ToListAsync();

                activities.AddRange(recentOrders);

                var totalUsers = await _context.Customers.CountAsync(c => c.IsActive);
                var todayLogins = await _context.Orders
                    .Where(o => o.CreatedAt.Date == DateTime.Today)
                    .Select(o => o.CustomerId)
                    .Distinct()
                    .CountAsync();
                var adminActions = recentOrders.Count;
                var securityEvents = 0; // You might want to add a security log table

                return Json(new
                {
                    activities,
                    stats = new
                    {
                        totalUsers,
                        todayLogins,
                        adminActions,
                        securityEvents
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin data");
                return Json(new { error = "خطا در دریافت اطلاعات مدیریت" });
            }
        }

        // New API Methods for Customer Reports Page

        // GET: Reports/GetCustomerBalances
        [HttpGet]
        public async Task<IActionResult> GetCustomerBalances(DateTime? fromDate, DateTime? toDate, string currency, string customer, int page = 1, int pageSize = 10)
        {
            try
            {
                // Require customer selection - no data without customer filter
                if (string.IsNullOrEmpty(customer) || !int.TryParse(customer, out int customerId))
                {
                    return Json(new
                    {
                        success = true,
                        data = new object[0],
                        totalPages = 0,
                        currentPage = page,
                        totalCount = 0
                    });
                }

                var query = _context.CustomerBalances
                    .Include(cb => cb.Customer)
                    .Where(cb => cb.CustomerId == customerId);

                // Apply date filter if provided (filter by customer creation date)
                if (fromDate.HasValue && toDate.HasValue)
                {
                    query = query.Where(cb => cb.Customer.CreatedAt >= fromDate && cb.Customer.CreatedAt <= toDate);
                }

                if (!string.IsNullOrEmpty(currency))
                {
                    query = query.Where(cb => cb.CurrencyCode == currency);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var data = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(cb => new
                    {
                        customerId = cb.CustomerId,
                        customerName = cb.Customer.FullName,
                        currencyCode = cb.CurrencyCode,
                        amount = cb.Balance,
                        lastUpdated = cb.LastUpdated
                    })
                    .OrderByDescending(x => x.lastUpdated)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = data,
                    totalPages = totalPages,
                    currentPage = page,
                    totalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer balances");
                return Json(new { success = false, error = "خطا در دریافت موجودی مشتریان" });
            }
        }

        // GET: Reports/GetCustomerOrders
        [HttpGet]
        public async Task<IActionResult> GetCustomerOrders(DateTime? fromDate, DateTime? toDate, string currency, string customer, int page = 1, int pageSize = 10)
        {
            try
            {
                // Require customer selection - no data without customer filter
                if (string.IsNullOrEmpty(customer) || !int.TryParse(customer, out int customerId))
                {
                    return Json(new
                    {
                        success = true,
                        data = new object[0],
                        totalPages = 0,
                        currentPage = page,
                        totalCount = 0
                    });
                }

                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.CustomerId == customerId);

                // Apply date filter if provided (filter by order creation date)
                if (fromDate.HasValue && toDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);
                }

                if (!string.IsNullOrEmpty(currency))
                {
                    query = query.Where(o => o.FromCurrency.Code == currency || o.ToCurrency.Code == currency);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var data = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(o => new
                    {
                        id = o.Id,
                        customerId = o.CustomerId,
                        customerName = o.Customer.FullName,
                        fromCurrency = o.FromCurrency.Code,
                        fromAmount = o.FromAmount,
                        toCurrency = o.ToCurrency.Code,
                        toAmount = o.ToAmount,
                        createdAt = o.CreatedAt,
                        status = "Completed" // All orders are completed since FilledAmount is removed
                    })
                    .OrderByDescending(x => x.createdAt)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = data,
                    totalPages = totalPages,
                    currentPage = page,
                    totalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer orders");
                return Json(new { success = false, error = "خطا در دریافت معاملات مشتریان" });
            }
        }

        // GET: Reports/GetCustomerDocuments
        [HttpGet]
        public async Task<IActionResult> GetCustomerDocuments(DateTime? fromDate, DateTime? toDate, string currency, string customer, int page = 1, int pageSize = 10)
        {
            try
            {
                // Require customer selection - no data without customer filter
                if (string.IsNullOrEmpty(customer) || !int.TryParse(customer, out int customerId))
                {
                    return Json(new
                    {
                        success = true,
                        data = new object[0],
                        totalPages = 0,
                        currentPage = page,
                        totalCount = 0
                    });
                }

                var query = _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Where(ad => ad.PayerCustomerId == customerId || ad.ReceiverCustomerId == customerId);

                // Apply date filter if provided (filter by document creation date)
                if (fromDate.HasValue && toDate.HasValue)
                {
                    query = query.Where(ad => ad.CreatedAt >= fromDate && ad.CreatedAt <= toDate);
                }

                if (!string.IsNullOrEmpty(currency))
                {
                    query = query.Where(ad => ad.CurrencyCode == currency);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var data = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(ad => new
                    {
                        id = ad.Id,
                        documentNumber = ad.Id.ToString(),
                        customerId = ad.PayerCustomerId ?? ad.ReceiverCustomerId,
                        customerName = ad.PayerCustomer != null ? ad.PayerCustomer.FullName : 
                                     (ad.ReceiverCustomer != null ? ad.ReceiverCustomer.FullName : "نامشخص"),
                        type = ad.PayerCustomerId != null ? "Payment" : "Receipt",
                        amount = ad.Amount,
                        currencyCode = ad.CurrencyCode ?? "IRR",
                        createdAt = ad.DocumentDate,
                        description = ad.Description
                    })
                    .OrderByDescending(x => x.createdAt)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = data,
                    totalPages = totalPages,
                    currentPage = page,
                    totalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer documents");
                return Json(new { success = false, error = "خطا در دریافت اسناد حسابداری" });
            }
        }

        // GET: Reports/ExportCustomerReports
        [HttpGet]
        public IActionResult ExportCustomerReports(string type, DateTime? fromDate, DateTime? toDate, string currency)
        {
            try
            {
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                // For now, just redirect to a placeholder or return a message
                // You can implement actual Excel export using EPPlus or similar library
                
                return Json(new { 
                    success = true, 
                    message = "قابلیت صدور فایل Excel در حال توسعه است",
                    downloadUrl = "#" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting customer reports");
                return Json(new { success = false, error = "خطا در صدور گزارش" });
            }
        }
    }
}
