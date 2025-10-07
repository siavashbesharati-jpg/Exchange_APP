using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Extensions;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Operator,Programmer")]
    public class ReportsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ReportsController> _logger;
        private readonly CustomerFinancialHistoryService _customerHistoryService;
        private readonly PoolFinancialHistoryService _poolHistoryService;
        private readonly BankAccountFinancialHistoryService _bankAccountHistoryService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICentralFinancialService _centralFinancialService;
        private readonly ExcelExportService _excelExportService;



        public ReportsController(
            ForexDbContext context,
            ILogger<ReportsController> logger,
            CustomerFinancialHistoryService customerHistoryService,
            PoolFinancialHistoryService poolHistoryService,
            BankAccountFinancialHistoryService bankAccountHistoryService,
            UserManager<ApplicationUser> userManager,
             ICentralFinancialService centralFinancialService,
             ExcelExportService excelExportService)
        {
            _context = context;
            _logger = logger;
            _customerHistoryService = customerHistoryService;
            _poolHistoryService = poolHistoryService;
            _bankAccountHistoryService = bankAccountHistoryService;
            _userManager = userManager;
            _centralFinancialService = centralFinancialService;
            _excelExportService = excelExportService;
        }

        /// <summary>
        /// Helper method to properly format date range for reports.
        /// From date starts at 00:00:01, To date ends at 23:59:00
        /// </summary>
        private (DateTime fromDateTime, DateTime toDateTime) FormatDateRange(DateTime? fromDate, DateTime? toDate)
        {
            // Set default values if dates are null
            var defaultFromDate = DateTime.Today.AddMonths(-12);
            var defaultToDate = DateTime.Today;

            var from = fromDate ?? defaultFromDate;
            var to = toDate ?? defaultToDate;

            // Ensure fromDate starts at 00:00:01
            var fromDateTime = new DateTime(from.Year, from.Month, from.Day, 0, 0, 1);

            // Ensure toDate ends at 23:59:00
            var toDateTime = new DateTime(to.Year, to.Month, to.Day, 23, 59, 0);

            return (fromDateTime, toDateTime);
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



        // GET: Reports/BankAccountReports
        public IActionResult BankAccountReports()
        {
            return View();
        }

        // GET: Reports/AdminReports
        public IActionResult AdminReports()
        {
            return View();
        }

        // GET: Reports/AllCustomersBalances
        public IActionResult AllCustomersBalances()
        {
            return View();
        }

        // GET: Reports/PoolSummaryReport
        public IActionResult PoolSummaryReport()
        {
            return View();
        }

        // GET: Reports/BankAccountSummaryReport
        public IActionResult BankAccountSummaryReport()
        {
            return View();
        }

        // GET: Reports/CustomerSummaryReport
        public IActionResult CustomerSummaryReport()
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
                    .Where(c => c.IsActive && !c.IsSystem)
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
                    .OrderBy(c => c.fullName)
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

        // GET: Reports/GetAllCustomersBalances
        [HttpGet]
        public async Task<IActionResult> GetAllCustomersBalances(string? currencyFilter = null, string? customerFilter = null)
        {
            try
            {
                _logger.LogInformation("Starting GetAllCustomersBalances with currency filter: {CurrencyFilter}, customer filter: {CustomerFilter}", currencyFilter, customerFilter);

                // First, let's test if basic customers query works
                var customersCount = await _context.Customers
                    .Where(c => c.IsActive && !c.IsSystem)
                    .CountAsync();

                _logger.LogInformation("Found {Count} active customers", customersCount);

                // Test if CustomerBalances table exists and has data
                var balancesCount = await _context.CustomerBalances.CountAsync();
                _logger.LogInformation("Found {Count} customer balances", balancesCount);

                // Now try the full query with better error handling
                var query = _context.Customers
                    .Include(c => c.Balances)
                    .Where(c => c.IsActive && !c.IsSystem);

                // Apply customer filter if provided
                if (!string.IsNullOrEmpty(customerFilter) && int.TryParse(customerFilter, out int customerId))
                {
                    query = query.Where(c => c.Id == customerId);
                }

                var customers = await query
                    .Select(c => new
                    {
                        id = c.Id,
                        fullName = c.FullName,
                        phoneNumber = c.PhoneNumber,
                        email = c.Email,
                        createdAt = c.CreatedAt,
                        isActive = c.IsActive,
                        balances = c.Balances
                            .Where(b => string.IsNullOrEmpty(currencyFilter) || b.CurrencyCode == currencyFilter)
                            .Where(b => b.Balance != 0) // Only show non-zero balances
                            .Select(b => new
                            {
                                currencyCode = b.CurrencyCode,
                                balance = b.Balance,
                                lastUpdated = b.LastUpdated,
                                balanceStatus = b.Balance > 0 ? "اعتبار" : (b.Balance < 0 ? "بدهی" : "تسویه"),
                                absoluteBalance = b.Balance < 0 ? -b.Balance : b.Balance // Use conditional instead of Math.Abs
                            }).ToList(),
                        hasBalances = c.Balances.Any(b => b.Balance != 0),
                        totalDebt = c.Balances.Where(b => b.Balance < 0).Sum(b => -b.Balance), // Use negation instead of Math.Abs
                        totalCredit = c.Balances.Where(b => b.Balance > 0).Sum(b => b.Balance)
                    })
                    .OrderBy(c => c.fullName)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} customers", customers.Count);

                // Apply currency filter and only include customers with balances
                if (!string.IsNullOrEmpty(currencyFilter))
                {
                    customers = customers.Where(c => c.balances.Any()).ToList();
                }
                else
                {
                    customers = customers.Where(c => c.hasBalances).ToList();
                }

                _logger.LogInformation("After filtering, {Count} customers have balances", customers.Count);

                // Get summary statistics
                var totalCustomersWithBalances = customers.Count;
                var totalCustomersWithDebt = customers.Count(c => c.totalDebt > 0);
                var totalCustomersWithCredit = customers.Count(c => c.totalCredit > 0);

                // Currency-specific totals
                var currencyTotals = new Dictionary<string, object>();

                try
                {
                    if (string.IsNullOrEmpty(currencyFilter))
                    {
                        // Get totals for all currencies
                        var allCurrencies = await _context.CustomerBalances
                            .Where(cb => cb.Balance != 0)
                            .GroupBy(cb => cb.CurrencyCode)
                            .Select(g => new
                            {
                                currencyCode = g.Key,
                                totalCredit = g.Where(cb => cb.Balance > 0).Sum(cb => cb.Balance),
                                totalDebt = g.Where(cb => cb.Balance < 0).Sum(cb => -cb.Balance), // Use negation instead of Math.Abs
                                customerCount = g.Select(cb => cb.CustomerId).Distinct().Count()
                            })
                            .ToListAsync();

                        foreach (var currency in allCurrencies)
                        {
                            currencyTotals[currency.currencyCode] = new
                            {
                                totalCredit = currency.totalCredit,
                                totalDebt = currency.totalDebt,
                                netBalance = currency.totalCredit - currency.totalDebt,
                                customerCount = currency.customerCount
                            };
                        }
                    }
                    else
                    {
                        // Get totals for filtered currency
                        var currencyTotal = await _context.CustomerBalances
                            .Where(cb => cb.CurrencyCode == currencyFilter && cb.Balance != 0)
                            .GroupBy(cb => cb.CurrencyCode)
                            .Select(g => new
                            {
                                totalCredit = g.Where(cb => cb.Balance > 0).Sum(cb => cb.Balance),
                                totalDebt = g.Where(cb => cb.Balance < 0).Sum(cb => -cb.Balance), // Use negation instead of Math.Abs
                                customerCount = g.Select(cb => cb.CustomerId).Distinct().Count()
                            })
                            .FirstOrDefaultAsync();

                        if (currencyTotal != null)
                        {
                            currencyTotals[currencyFilter] = new
                            {
                                totalCredit = currencyTotal.totalCredit,
                                totalDebt = currencyTotal.totalDebt,
                                netBalance = currencyTotal.totalCredit - currencyTotal.totalDebt,
                                customerCount = currencyTotal.customerCount
                            };
                        }
                    }
                }
                catch (Exception currencyEx)
                {
                    _logger.LogWarning(currencyEx, "Error calculating currency totals, continuing without them");
                    // Continue without currency totals if there's an error
                }

                var result = new
                {
                    customers,
                    stats = new
                    {
                        totalCustomersWithBalances,
                        totalCustomersWithDebt,
                        totalCustomersWithCredit,
                        currencyFilter,
                        currencyTotals
                    }
                };

                _logger.LogInformation("Successfully completed GetAllCustomersBalances");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all customers balances with currency filter: {CurrencyFilter}, customer filter: {CustomerFilter}", currencyFilter, customerFilter);
                return Json(new { error = $"خطا در دریافت موجودی مشتریان: {ex.Message}" });
            }
        }

        // GET: Reports/GetOrdersData
        [HttpGet]
        public async Task<IActionResult> GetOrdersData(DateTime? fromDate, DateTime? toDate, string? fromCurrency, string? toCurrency, string? orderStatus)
        {
            try
            {
                var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);

                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.CreatedAt >= fromDateTime && o.CreatedAt <= toDateTime);

                // Apply currency filters
                if (!string.IsNullOrEmpty(fromCurrency))
                {
                    query = query.Where(o => o.FromCurrency.Code == fromCurrency);
                }

                if (!string.IsNullOrEmpty(toCurrency))
                {
                    query = query.Where(o => o.ToCurrency.Code == toCurrency);
                }

                // Note: Status filter not implemented since all orders are completed
                // You can add status logic here if needed

                var orders = await query
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
                return Json(new { error = "خطا در دریافت اطلاعات معاملات " });
            }
        }

        // GET: Reports/GetDocumentsData
        [HttpGet]
        public async Task<IActionResult> GetDocumentsData(DateTime? fromDate, DateTime? toDate, string? currency, string? customer, string? referenceId, decimal? fromAmount, decimal? toAmount, string? bankAccount, int page = 1, int pageSize = 10)
        {
            try
            {
                var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);

                var query = _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Where(ad => ad.DocumentDate >= fromDateTime && ad.DocumentDate <= toDateTime);

                // Apply additional filters
                if (!string.IsNullOrEmpty(currency))
                {
                    query = query.Where(ad => ad.CurrencyCode == currency);
                }

                if (!string.IsNullOrEmpty(customer) && int.TryParse(customer, out int customerId))
                {
                    query = query.Where(ad => ad.PayerCustomerId == customerId || ad.ReceiverCustomerId == customerId);
                }

                // Add reference ID filter
                if (!string.IsNullOrEmpty(referenceId))
                {
                    query = query.Where(ad => ad.ReferenceNumber != null && ad.ReferenceNumber.Contains(referenceId));
                }

                // Add amount range filter
                if (fromAmount.HasValue)
                {
                    query = query.Where(ad => ad.Amount >= fromAmount.Value);
                }

                if (toAmount.HasValue)
                {
                    query = query.Where(ad => ad.Amount <= toAmount.Value);
                }

                // Add bank account filter
                if (!string.IsNullOrEmpty(bankAccount) && int.TryParse(bankAccount, out int bankAccountId))
                {
                    query = query.Where(ad => ad.PayerBankAccountId == bankAccountId || ad.ReceiverBankAccountId == bankAccountId);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var accountingDocs = await query
                    .OrderByDescending(ad => ad.DocumentDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(ad => new
                    {
                        id = ad.Id,
                        date = ad.DocumentDate,
                        type = "سند حسابداری",
                        customerName = ad.PayerCustomer != null ? ad.PayerCustomer.FullName : (ad.ReceiverCustomer != null ? ad.ReceiverCustomer.FullName : "نامشخص"),
                        amount = ad.Amount,
                        currencyCode = ad.CurrencyCode,
                        referenceNumber = ad.ReferenceNumber,
                        description = ad.Description,
                        status = "تایید شده"
                    })
                    .ToListAsync();

                // Skip receipts since Receipt model is empty
                var allDocuments = accountingDocs.ToList();

                var totalDocuments = totalCount;
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
                    },
                    pagination = new
                    {
                        currentPage = page,
                        totalPages = totalPages,
                        totalRecords = totalCount,
                        pageSize = pageSize
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents data");
                return Json(new { error = "خطا در دریافت اطلاعات اسناد" });
            }
        }

        // GET: Reports/GetDocumentDetails/{id}
        [HttpGet]
        public async Task<IActionResult> GetDocumentDetails(int id)
        {
            try
            {
                var document = await _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Include(ad => ad.PayerBankAccount)
                    .Include(ad => ad.ReceiverBankAccount)
                    .Where(ad => ad.Id == id)
                    .FirstOrDefaultAsync();

                if (document == null)
                {
                    return Json(new { error = "سند یافت نشد" });
                }

                var result = new
                {
                    id = document.Id,
                    documentType = document.Type.ToString(),
                    documentDate = document.DocumentDate,
                    amount = document.Amount,
                    currencyCode = document.CurrencyCode,
                    description = document.Description,
                    notes = document.Notes,
                    referenceNumber = document.ReferenceNumber,

                    // Payer information
                    payerType = document.PayerType.ToString(),
                    payerCustomer = document.PayerCustomer != null ? new
                    {
                        id = document.PayerCustomer.Id,
                        fullName = document.PayerCustomer.FullName,
                        phoneNumber = document.PayerCustomer.PhoneNumber,
                        email = document.PayerCustomer.Email
                    } : null,
                    payerBankAccount = document.PayerBankAccount != null ? new
                    {
                        id = document.PayerBankAccount.Id,
                        accountNumber = document.PayerBankAccount.AccountNumber,
                        bankName = document.PayerBankAccount.BankName,
                        accountHolderName = document.PayerBankAccount.AccountHolderName
                    } : null,

                    // Receiver information
                    receiverType = document.ReceiverType.ToString(),
                    receiverCustomer = document.ReceiverCustomer != null ? new
                    {
                        id = document.ReceiverCustomer.Id,
                        fullName = document.ReceiverCustomer.FullName,
                        phoneNumber = document.ReceiverCustomer.PhoneNumber,
                        email = document.ReceiverCustomer.Email
                    } : null,
                    receiverBankAccount = document.ReceiverBankAccount != null ? new
                    {
                        id = document.ReceiverBankAccount.Id,
                        accountNumber = document.ReceiverBankAccount.AccountNumber,
                        bankName = document.ReceiverBankAccount.BankName,
                        accountHolderName = document.ReceiverBankAccount.AccountHolderName
                    } : null,

                    // Metadata
                    createdAt = document.CreatedAt,
                    isVerified = document.IsVerified,
                    verifiedAt = document.VerifiedAt,
                    verifiedBy = document.VerifiedBy ?? "نامشخص",
                    isDeleted = document.IsDeleted,
                    deletedAt = document.DeletedAt,
                    deletedBy = document.DeletedBy,
                    isFrozen = document.IsFrozen
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document details for ID: {DocumentId}", id);
                return Json(new { error = "خطا در دریافت جزئیات سند" });
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
                    .OrderBy(c => c.DisplayOrder)
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

        // GET: Reports/GetPoolDailyReport
        [HttpGet]
        public async Task<IActionResult> GetPoolDailyReport(DateTime date)
        {
            try
            {
                // Validate date - ensure it's not in the future
                if (date > DateTime.Today)
                {
                    return Json(new { error = "تاریخ انتخاب شده نمی‌تواند در آینده باشد" });
                }

                var startOfDay = date.Date;
                var endOfDay = startOfDay.AddDays(1).AddSeconds(-1);

                // Get all active currencies with their RatePriority
                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                var result = new List<object>();

                foreach (var currency in currencies)
                {
                    // Get latest balance at end of day
                    var latestHistory = await _context.CurrencyPoolHistory
                        .Where(h => h.CurrencyCode == currency.Code && h.TransactionDate <= endOfDay)
                        .OrderByDescending(h => h.TransactionDate)
                        .ThenByDescending(h => h.Id)
                        .FirstOrDefaultAsync();

                    decimal latestBalance = latestHistory?.BalanceAfter ?? 0;

                    // Get all transactions for the day
                    var transactionsRaw = await _context.CurrencyPoolHistory
                        .Where(h => h.CurrencyCode == currency.Code &&
                                   h.TransactionDate >= startOfDay &&
                                   h.TransactionDate <= endOfDay)
                        .OrderBy(h => h.TransactionDate)
                        .ThenBy(h => h.Id)
                        .ToListAsync();

                    // Extract rates and calculate weighted average
                    var ratesWithAmounts = new List<(decimal rate, decimal amount)>();
                    var transactionsWithRates = new List<(CurrencyPoolHistory historyRecord, decimal? rate, Currency? fromCurrency, Currency? toCurrency)>();

                    foreach (var h in transactionsRaw)
                    {
                        decimal? transactionRate = null;
                        Currency? fromCurrency = null;
                        Currency? toCurrency = null;

                        // Get rate and currency info from Order transactions only
                        if (h.TransactionType == CurrencyPoolTransactionType.Order && h.ReferenceId.HasValue)
                        {
                            var order = await _context.Orders
                                .Include(o => o.FromCurrency)
                                .Include(o => o.ToCurrency)
                                .FirstOrDefaultAsync(o => o.Id == h.ReferenceId.Value);

                            if (order != null && order.Rate > 0)
                            {
                                transactionRate = order.Rate;
                                fromCurrency = order.FromCurrency;
                                toCurrency = order.ToCurrency;

                                // Add to weighted average calculation with transaction amount as weight
                                decimal transactionAmount = Math.Abs(h.TransactionAmount);
                                if (transactionAmount > 0)
                                {
                                    ratesWithAmounts.Add((order.Rate, transactionAmount));
                                }
                            }
                        }

                        transactionsWithRates.Add((h, transactionRate, fromCurrency, toCurrency));
                    }

                    // Calculate weighted average rate for the day
                    decimal weightedAverageRate = 0;
                    if (ratesWithAmounts.Count > 0)
                    {
                        decimal totalWeightedRates = ratesWithAmounts.Sum(x => x.rate * x.amount);
                        decimal totalWeights = ratesWithAmounts.Sum(x => x.amount);

                        if (totalWeights > 0)
                        {
                            weightedAverageRate = totalWeightedRates / totalWeights;
                        }
                    }

                    // Calculate profit for each transaction
                    var transactions = new List<object>();
                    decimal totalDailyProfit = 0;

                    foreach (var item in transactionsWithRates)
                    {
                        var h = item.historyRecord;
                        var rate = item.rate;
                        var fromCurrency = item.fromCurrency;
                        var toCurrency = item.toCurrency;

                        decimal profit = 0;

                        // Only calculate profit for Order transactions with valid rates
                        if (rate.HasValue && rate.Value > 0 && weightedAverageRate > 0 &&
                            fromCurrency != null && toCurrency != null)
                        {
                            decimal transactionAmount = Math.Abs(h.TransactionAmount); // Use absolute value for calculation
                            decimal convertedAmount;
                            decimal reversedAmount;

                            // Determine direction based on which currency pool we're looking at
                            // If the pool currency matches FromCurrency, we're selling from pool (divide)
                            // If the pool currency matches ToCurrency, we're buying to pool (multiply)
                            bool isSellingFromPool = currency.Code == fromCurrency!.Code;

                            if (isSellingFromPool)
                            {
                                // Selling from pool: convert pool currency to target currency (divide by rate)
                                convertedAmount = transactionAmount / rate.Value;
                                reversedAmount = convertedAmount * weightedAverageRate;
                            }
                            else
                            {
                                // Buying to pool: convert source currency to pool currency (multiply by rate)
                                convertedAmount = transactionAmount * rate.Value;
                                reversedAmount = convertedAmount / weightedAverageRate;
                            }

                            // Profit = Original amount - Amount if converted at weighted average rate
                            profit = transactionAmount - reversedAmount;

                            // Debug logging to see what's happening
                            _logger.LogInformation("Profit calc (Weighted Avg): Pool={Pool}, From={From}, To={To}, Selling={Selling}, Amount={Amount}, Rate={Rate}, WeightedAvgRate={WeightedAvgRate}, Converted={Converted}, Reversed={Reversed}, Profit={Profit}",
                                currency.Code, fromCurrency.Code, toCurrency.Code, isSellingFromPool, transactionAmount, rate.Value, weightedAverageRate, convertedAmount, reversedAmount, profit);

                        }

                        totalDailyProfit += profit;

                        transactions.Add(new
                        {
                            time = h.TransactionDate.ToString("HH:mm:ss"),
                            type = h.TransactionType.ToString(),
                            description = h.Description ?? "",
                            amount = h.TransactionAmount,
                            balanceAfter = h.BalanceAfter,
                            referenceId = h.ReferenceId,
                            rate = rate,
                            weightedAverageRate = weightedAverageRate,
                            profit = profit
                        });
                    }

                    decimal dailyTransactionSum = transactionsRaw.Sum(t => t.TransactionAmount);

                    // Only include currencies with transactions or non-zero balance
                    if (transactionsRaw.Any() || latestBalance != 0)
                    {
                        result.Add(new
                        {
                            currencyCode = currency.Code,
                            currencyName = currency.Name,
                            latestBalance,
                            dailyTransactionSum,
                            transactionCount = transactionsRaw.Count,
                            totalDailyProfit,
                            weightedAverageRate, // Include weighted average in response
                            transactions
                        });
                    }
                }

                return Json(new { success = true, date = date.ToString("yyyy-MM-dd"), currencies = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pool daily report for date: {Date}", date);
                return Json(new { success = false, error = "خطا در دریافت گزارش روزانه صندوق" });
            }
        }

        // GET: Reports/GetPoolSummaryReport
        [HttpGet]
        public async Task<IActionResult> GetPoolSummaryReport(DateTime date)
        {
            try
            {
                _logger.LogInformation("GetPoolSummaryReport called with date: {Date}", date);

                // Validate date
                if (date > DateTime.Today)
                {
                    return Json(new { error = "تاریخ انتخاب شده نمی‌تواند در آینده باشد" });
                }

                var startOfDay = date.Date;
                var endOfDay = startOfDay.AddDays(1).AddSeconds(-1);

                // Get all active currencies
                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                decimal totalIRR = 0m;
                decimal totalOMR = 0m;

                // IRR transactions and details
                var irrTransactions = new List<object>();
                decimal irrTransactionCount = 0;
                decimal irrDailyProfit = 0;
                decimal irrWeightedAverageRate = 0;

                // Non-IRR transactions and details (combined for OMR display)
                var omrTransactions = new List<object>();
                decimal omrTransactionCount = 0;
                decimal omrDailyProfit = 0;
                decimal omrWeightedAverageRate = 0;

                foreach (var currency in currencies)
                {
                    // Get latest balance at end of day for this currency
                    var latestHistory = await _context.CurrencyPoolHistory
                        .Where(h => h.CurrencyCode == currency.Code && h.TransactionDate <= endOfDay)
                        .OrderByDescending(h => h.TransactionDate)
                        .ThenByDescending(h => h.Id)
                        .FirstOrDefaultAsync();

                    decimal latestBalance = latestHistory?.BalanceAfter ?? 0;

                    // Get all transactions for the day
                    var transactionsRaw = await _context.CurrencyPoolHistory
                        .Where(h => h.CurrencyCode == currency.Code &&
                                   h.TransactionDate >= startOfDay &&
                                   h.TransactionDate <= endOfDay)
                        .OrderBy(h => h.TransactionDate)
                        .ThenBy(h => h.Id)
                        .ToListAsync();

                    // Extract rates and calculate weighted average
                    var ratesWithAmounts = new List<(decimal rate, decimal amount)>();
                    var transactionsWithRates = new List<(CurrencyPoolHistory historyRecord, decimal? rate, Currency? fromCurrency, Currency? toCurrency)>();

                    foreach (var h in transactionsRaw)
                    {
                        decimal? transactionRate = null;
                        Currency? fromCurrency = null;
                        Currency? toCurrency = null;

                        // Get rate and currency info from Order transactions only
                        if (h.TransactionType == CurrencyPoolTransactionType.Order && h.ReferenceId.HasValue)
                        {
                            var order = await _context.Orders
                                .Include(o => o.FromCurrency)
                                .Include(o => o.ToCurrency)
                                .FirstOrDefaultAsync(o => o.Id == h.ReferenceId.Value);

                            if (order != null && order.Rate > 0)
                            {
                                transactionRate = order.Rate;
                                fromCurrency = order.FromCurrency;
                                toCurrency = order.ToCurrency;

                                // Add to weighted average calculation with transaction amount as weight
                                decimal transactionAmount = Math.Abs(h.TransactionAmount);
                                if (transactionAmount > 0)
                                {
                                    ratesWithAmounts.Add((order.Rate, transactionAmount));
                                }
                            }
                        }

                        transactionsWithRates.Add((h, transactionRate, fromCurrency, toCurrency));
                    }

                    // Calculate weighted average rate for the day
                    decimal weightedAverageRate = 0;
                    if (ratesWithAmounts.Count > 0)
                    {
                        decimal totalWeightedRates = ratesWithAmounts.Sum(x => x.rate * x.amount);
                        decimal totalWeights = ratesWithAmounts.Sum(x => x.amount);

                        if (totalWeights > 0)
                        {
                            weightedAverageRate = totalWeightedRates / totalWeights;
                        }
                    }

                    // Calculate profit for each transaction
                    var transactions = new List<object>();
                    decimal totalDailyProfit = 0;

                    foreach (var item in transactionsWithRates)
                    {
                        var h = item.historyRecord;
                        var rate = item.rate;
                        var fromCurrency = item.fromCurrency;
                        var toCurrency = item.toCurrency;

                        decimal profit = 0;

                        // Only calculate profit for Order transactions with valid rates
                        if (rate.HasValue && rate.Value > 0 && weightedAverageRate > 0 &&
                            fromCurrency != null && toCurrency != null)
                        {
                            decimal transactionAmount = Math.Abs(h.TransactionAmount);
                            decimal convertedAmount;
                            decimal reversedAmount;

                            // Determine direction based on which currency pool we're looking at
                            bool isSellingFromPool = currency.Code == fromCurrency!.Code;

                            if (isSellingFromPool)
                            {
                                // Selling from pool: convert pool currency to target currency (divide by rate)
                                convertedAmount = transactionAmount / rate.Value;
                                reversedAmount = convertedAmount * weightedAverageRate;
                            }
                            else
                            {
                                // Buying to pool: convert source currency to pool currency (multiply by rate)
                                convertedAmount = transactionAmount * rate.Value;
                                reversedAmount = convertedAmount / weightedAverageRate;
                            }

                            // Profit = Original amount - Amount if converted at weighted average rate
                            profit = transactionAmount - reversedAmount;
                        }

                        totalDailyProfit += profit;

                        var transactionObj = new
                        {
                            time = h.TransactionDate.ToString("HH:mm:ss"),
                            type = h.TransactionType.ToString(),
                            description = h.Description ?? "",
                            amount = h.TransactionAmount,
                            balanceAfter = h.BalanceAfter,
                            referenceId = h.ReferenceId,
                            rate = rate,
                            weightedAverageRate = weightedAverageRate,
                            profit = profit,
                            currencyCode = currency.Code,
                            currencyName = currency.Name,
                            fromCurrencyCode = fromCurrency?.Code,
                            fromCurrencyName = fromCurrency?.Name,
                            toCurrencyCode = toCurrency?.Code,
                            toCurrencyName = toCurrency?.Name,
                            // Determine the paired currency (the other currency in the exchange)
                            pairedCurrencyCode = currency.Code == fromCurrency?.Code ? toCurrency?.Code : fromCurrency?.Code,
                            pairedCurrencyName = currency.Code == fromCurrency?.Code ? toCurrency?.Name : fromCurrency?.Name
                        };

                        // Debug logging for transaction amounts
                        _logger.LogInformation("Transaction Debug - Currency: {Currency}, OriginalAmount: {OriginalAmount}, TransactionAmount: {TransactionAmount}, Description: {Description}, Time: {Time}",
                            currency.Code, h.TransactionAmount, transactionObj.amount, h.Description, h.TransactionDate);

                        transactions.Add(transactionObj);
                    }

                    // Add to totals for summary
                    if (currency.Code == "IRR")
                    {
                        totalIRR += latestBalance;
                        // Add IRR transactions to IRR group
                        irrTransactions.AddRange(transactions);
                        irrTransactionCount += transactionsRaw.Count;
                        irrDailyProfit += totalDailyProfit;
                        if (weightedAverageRate > 0)
                        {
                            irrWeightedAverageRate = weightedAverageRate; // IRR should have its own rate
                        }
                    }
                    else
                    {
                        // Add to OMR totals and transactions
                        if (latestBalance != 0)
                        {
                            var convertedToOMR = await ConvertCurrencyToOMR(latestBalance, currency.Code, date);
                            totalOMR += convertedToOMR;
                        }

                        // Add non-IRR transactions to OMR group
                        omrTransactions.AddRange(transactions);
                        omrTransactionCount += transactionsRaw.Count;
                        omrDailyProfit += totalDailyProfit;

                        // For OMR weighted average, we'll calculate it based on all non-IRR transactions
                        if (weightedAverageRate > 0 && omrTransactionCount > 0)
                        {
                            omrWeightedAverageRate = (omrWeightedAverageRate * (omrTransactionCount - transactionsRaw.Count) +
                                                     weightedAverageRate * transactionsRaw.Count) / omrTransactionCount;
                        }
                    }
                }

                // Prepare currency details for only IRR and OMR
                var currencyDetails = new List<object>();

                // Add IRR details if there are transactions or balance
                if (irrTransactions.Any() || totalIRR != 0)
                {
                    decimal irrDailyTransactionSum = 0;
                    foreach (var t in irrTransactions)
                    {
                        var amountProp = t.GetType().GetProperty("amount");
                        if (amountProp?.GetValue(t) is decimal amount)
                        {
                            irrDailyTransactionSum += amount;
                        }
                    }

                    currencyDetails.Add(new
                    {
                        currencyCode = "IRR",
                        currencyName = "تومان",
                        latestBalance = totalIRR,
                        dailyTransactionSum = irrDailyTransactionSum,
                        transactionCount = (int)irrTransactionCount,
                        totalDailyProfit = irrDailyProfit,
                        weightedAverageRate = irrWeightedAverageRate,
                        transactions = irrTransactions.OrderBy(t =>
                        {
                            var timeProp = t.GetType().GetProperty("time");
                            return timeProp?.GetValue(t)?.ToString() ?? "";
                        }).ToList()
                    });
                }

                // Add OMR details if there are transactions or balance
                if (omrTransactions.Any() || totalOMR != 0)
                {
                    decimal omrDailyTransactionSum = 0;
                    foreach (var t in omrTransactions)
                    {
                        var amountProp = t.GetType().GetProperty("amount");
                        if (amountProp?.GetValue(t) is decimal amount)
                        {
                            omrDailyTransactionSum += amount;
                        }
                    }

                    currencyDetails.Add(new
                    {
                        currencyCode = "OMR",
                        currencyName = "ریال عمان (سایر ارزها)",
                        latestBalance = totalOMR,
                        dailyTransactionSum = omrDailyTransactionSum,
                        transactionCount = (int)omrTransactionCount,
                        totalDailyProfit = omrDailyProfit,
                        weightedAverageRate = omrWeightedAverageRate,
                        transactions = omrTransactions.OrderBy(t =>
                        {
                            var timeProp = t.GetType().GetProperty("time");
                            return timeProp?.GetValue(t)?.ToString() ?? "";
                        }).ToList()
                    });
                }

                // Debug logging for final result
                _logger.LogInformation("Final Result Debug - TotalIRR: {TotalIRR}, TotalOMR: {TotalOMR}, CurrencyDetailsCount: {Count}",
                    totalIRR, totalOMR, currencyDetails.Count);

                foreach (var currencyDetail in currencyDetails)
                {
                    var currencyCode = currencyDetail.GetType().GetProperty("currencyCode")?.GetValue(currencyDetail);
                    var transactionsProp = currencyDetail.GetType().GetProperty("transactions");
                    if (transactionsProp?.GetValue(currencyDetail) is IEnumerable<object> transactions)
                    {
                        foreach (var transaction in transactions.Take(3)) // Log first 3 transactions
                        {
                            var amountProp = transaction.GetType().GetProperty("amount");
                            var currencyCodeProp = transaction.GetType().GetProperty("currencyCode");
                            var amount = amountProp?.GetValue(transaction);
                            var transCurrency = currencyCodeProp?.GetValue(transaction);
                            _logger.LogInformation("Final Transaction Debug - GroupCurrency: {GroupCurrency}, TransactionCurrency: {TransactionCurrency}, Amount: {Amount}",
                                currencyCode, transCurrency, amount);
                        }
                    }
                }

                return Json(new
                {
                    success = true,
                    date = date.ToString("yyyy-MM-dd"),
                    data = new
                    {
                        irrBalance = totalIRR,
                        omrBalance = totalOMR
                    },
                    currencies = currencyDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pool summary report for date: {Date}", date);
                return Json(new { success = false, error = "خطا در دریافت گزارش خلاصه صندوق" });
            }
        }

        // GET: Reports/GetBankAccountSummaryReport
        [HttpGet]
        public async Task<IActionResult> GetBankAccountSummaryReport(DateTime date)
        {
            try
            {
                _logger.LogInformation("Getting bank account summary report for date: {Date}", date);

                // Don't allow future dates
                if (date > DateTime.Today)
                {
                    return Json(new { success = false, error = "نمی‌توان گزارش آینده ایجاد کرد" });
                }

                // Get start and end of day
                var startOfDay = date.Date;
                var endOfDay = date.Date.AddDays(1).AddTicks(-1);

                // Get all bank account transactions for the day
                var bankAccountTransactions = await _context.BankAccountBalanceHistory
                    .Include(h => h.BankAccount)
                    .Where(h => h.TransactionDate >= startOfDay && h.TransactionDate <= endOfDay && !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ToListAsync();

                // Get all bank accounts with their current balances
                var bankAccounts = await _context.BankAccountBalances
                    .Include(b => b.BankAccount)
                    .ToListAsync();

                var bankAccountDetails = new List<object>();

                foreach (var bankAccount in bankAccounts)
                {
                    var accountTransactions = bankAccountTransactions
                        .Where(t => t.BankAccountId == bankAccount.BankAccountId)
                        .ToList();

                    var transactionDetails = accountTransactions.Select(t => new
                    {
                        amount = t.TransactionAmount,
                        description = t.Description ?? "تراکنش بانکی",
                        time = t.TransactionDate.ToString("HH:mm"),
                        transactionType = t.TransactionType.ToString(),
                        balanceBefore = t.BalanceBefore,
                        balanceAfter = t.BalanceAfter
                    }).ToList();

                    bankAccountDetails.Add(new
                    {
                        bankAccountId = bankAccount.BankAccountId,
                        bankAccountName = bankAccount.BankAccount?.BankName ?? "حساب نامشخص",
                        bankAccountNumber = bankAccount.BankAccount?.AccountNumber ?? "نامشخص",
                        latestBalance = bankAccount.Balance,
                        transactionCount = accountTransactions.Count,
                        transactions = transactionDetails
                    });
                }

                return Json(new
                {
                    success = true,
                    date = date.ToString("yyyy-MM-dd"),
                    data = new { },
                    bankAccounts = bankAccountDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bank account summary report for date: {Date}", date);
                return Json(new { success = false, error = "خطا در دریافت گزارش خلاصه حساب‌های بانکی" });
            }
        }

        // GET: Reports/GetCustomerSummaryReport
        [HttpGet]
        public async Task<IActionResult> GetCustomerSummaryReport(DateTime date)
        {
            try
            {
                _logger.LogInformation("Getting customer summary report for date: {Date}", date);

                // Don't allow future dates
                if (date > DateTime.Today)
                {
                    return Json(new { success = false, error = "نمی‌توان گزارش آینده ایجاد کرد" });
                }

                // Get start and end of day
                var startOfDay = date.Date;
                var endOfDay = date.Date.AddDays(1).AddTicks(-1);

                // Get all customer transactions for the day
                var customerTransactions = await _context.CustomerBalanceHistory
                    .Include(h => h.Customer)
                    .Where(h => h.TransactionDate >= startOfDay && h.TransactionDate <= endOfDay && !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ToListAsync();

                // Group by currency
                var currencyGroups = customerTransactions
                    .GroupBy(t => t.CurrencyCode)
                    .ToList();

                var currencyDetails = new List<object>();
                decimal totalIRR = 0;
                decimal totalOMR = 0;

                // Process IRR transactions
                var irrTransactions = customerTransactions
                    .Where(t => t.CurrencyCode == "IRR")
                    .ToList();

                if (irrTransactions.Any())
                {
                    // Get current total balance for IRR
                    var irrBalance = await _context.CustomerBalances
                        .Where(cb => cb.CurrencyCode == "IRR")
                        .SumAsync(cb => cb.Balance);

                    totalIRR = irrBalance;

                    var transactionDetails = irrTransactions.Select(t => new
                    {
                        amount = t.TransactionAmount,
                        currencyCode = t.CurrencyCode,
                        description = t.Description ?? "تراکنش مشتری",
                        time = t.TransactionDate.ToString("HH:mm"),
                        customerName = t.Customer?.FullName ?? "مشتری نامشخص",
                        transactionType = t.TransactionType.ToString(),
                        balanceBefore = t.BalanceBefore,
                        balanceAfter = t.BalanceAfter
                    }).ToList();

                    currencyDetails.Add(new
                    {
                        currencyCode = "IRR",
                        currencyName = "ریال ایران",
                        latestBalance = irrBalance,
                        transactionCount = irrTransactions.Count,
                        transactions = transactionDetails
                    });
                }

                // Process OMR transactions
                var omrTransactions = customerTransactions
                    .Where(t => t.CurrencyCode == "OMR")
                    .ToList();

                if (omrTransactions.Any())
                {
                    // Get current total balance for OMR
                    var omrBalance = await _context.CustomerBalances
                        .Where(cb => cb.CurrencyCode == "OMR")
                        .SumAsync(cb => cb.Balance);

                    totalOMR = omrBalance;

                    var transactionDetails = omrTransactions.Select(t => new
                    {
                        amount = t.TransactionAmount,
                        currencyCode = t.CurrencyCode,
                        description = t.Description ?? "تراکنش مشتری",
                        time = t.TransactionDate.ToString("HH:mm"),
                        customerName = t.Customer?.FullName ?? "مشتری نامشخص",
                        transactionType = t.TransactionType.ToString(),
                        balanceBefore = t.BalanceBefore,
                        balanceAfter = t.BalanceAfter
                    }).ToList();

                    currencyDetails.Add(new
                    {
                        currencyCode = "OMR",
                        currencyName = "ریال عمان",
                        latestBalance = omrBalance,
                        transactionCount = omrTransactions.Count,
                        transactions = transactionDetails
                    });
                }

                // Process other currencies and convert to OMR
                var otherCurrencies = currencyGroups
                    .Where(g => g.Key != "IRR" && g.Key != "OMR")
                    .ToList();

                foreach (var currencyGroup in otherCurrencies)
                {
                    var currencyCode = currencyGroup.Key;
                    var currencyTransactions = currencyGroup.ToList();

                    // Get current total balance for this currency
                    var currentBalance = await _context.CustomerBalances
                        .Where(cb => cb.CurrencyCode == currencyCode)
                        .SumAsync(cb => cb.Balance);

                    // Convert balance to OMR
                    var balanceInOMR = await ConvertCurrencyToOMR(currentBalance, currencyCode, date);
                    totalOMR += balanceInOMR;

                    var transactionDetails = currencyTransactions.Select(t => new
                    {
                        amount = t.TransactionAmount,
                        currencyCode = t.CurrencyCode,
                        description = t.Description ?? "تراکنش مشتری",
                        time = t.TransactionDate.ToString("HH:mm"),
                        customerName = t.Customer?.FullName ?? "مشتری نامشخص",
                        transactionType = t.TransactionType.ToString(),
                        balanceBefore = t.BalanceBefore,
                        balanceAfter = t.BalanceAfter,
                        // Add conversion info for non-IRR/OMR currencies
                        fromCurrencyCode = currencyCode,
                        toCurrencyCode = "OMR"
                    }).ToList();

                    // Get currency name
                    var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == currencyCode);

                    currencyDetails.Add(new
                    {
                        currencyCode = currencyCode,
                        currencyName = currency?.Name ?? currencyCode,
                        latestBalance = currentBalance,
                        transactionCount = currencyTransactions.Count,
                        transactions = transactionDetails
                    });
                }

                return Json(new
                {
                    success = true,
                    date = date.ToString("yyyy-MM-dd"),
                    data = new
                    {
                        irrBalance = totalIRR,
                        omrBalance = totalOMR
                    },
                    currencies = currencyDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer summary report for date: {Date}", date);
                return Json(new { success = false, error = "خطا در دریافت گزارش خلاصه مشتریان" });
            }
        }

        private async Task<decimal> ConvertCurrencyToOMR(decimal amount, string fromCurrencyCode, DateTime date)
        {
            try
            {
                if (fromCurrencyCode == "OMR") return amount; // Already OMR

                // Get exchange rate from database
                var fromCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == fromCurrencyCode);
                var omrCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "OMR");

                if (fromCurrency == null || omrCurrency == null)
                {
                    _logger.LogWarning("Could not find currency for conversion: {FromCurrency} to OMR", fromCurrencyCode);
                    return 0;
                }

                // Get the most recent exchange rate on or before the specified date
                var exchangeRate = await _context.ExchangeRates
                    .Where(er => ((er.FromCurrencyId == fromCurrency.Id && er.ToCurrencyId == omrCurrency.Id)))
                    .OrderByDescending(er => er.UpdatedAt)
                    .FirstOrDefaultAsync();

                if (exchangeRate == null)
                {
                    _logger.LogWarning("No exchange rate found for {FromCurrency} to OMR on or before {Date}", fromCurrencyCode, date);
                    return 0;
                }

                _logger.LogWarning(" *****   {amount} / {exchangeRate.Rate}", amount, exchangeRate.Rate);

                // Direct conversion: fromCurrency -> OMR as omr is biiger alwoays others should devide to omr rate
                return amount / (exchangeRate.Rate);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting {Amount} {FromCurrency} to OMR for date {Date}", amount, fromCurrencyCode, date);
                return 0;
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
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    query = query.Where(cb => cb.Customer.CreatedAt >= fromDateTime && cb.Customer.CreatedAt <= toDateTime);
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
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    query = query.Where(o => o.CreatedAt >= fromDateTime && o.CreatedAt <= toDateTime);
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
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    query = query.Where(ad => ad.CreatedAt >= fromDateTime && ad.CreatedAt <= toDateTime);
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
                var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);

                // For now, just redirect to a placeholder or return a message
                // You can implement actual Excel export using EPPlus or similar library
                // When implemented, use fromDateTime and toDateTime for filtering

                return Json(new
                {
                    success = true,
                    message = "قابلیت صدور فایل اکسل  در حال توسعه است",
                    downloadUrl = "#"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting customer reports");
                return Json(new { success = false, error = "خطا در صدور گزارش" });
            }
        }

        // Pool Reports API Methods

        // GET: Reports/GetPoolTimeline
        [HttpGet]
        public async Task<IActionResult> GetPoolTimeline(string? currencyCode = null, string? fromDate = null, string? toDate = null)
        {
            try
            {
                DateTime? fromDateTime = null;
                DateTime? toDateTime = null;

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var parsedFromDate))
                {
                    fromDateTime = parsedFromDate;
                }

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var parsedToDate))
                {
                    toDateTime = parsedToDate;
                }

                // Format date range if any dates are provided
                if (fromDateTime.HasValue || toDateTime.HasValue)
                {
                    var (formattedFromDateTime, formattedToDateTime) = FormatDateRange(fromDateTime, toDateTime);
                    fromDateTime = formattedFromDateTime;
                    toDateTime = formattedToDateTime;
                }

                var timeline = await _poolHistoryService.GetPoolTimelineAsync(currencyCode, fromDateTime, toDateTime);
                var summary = await _poolHistoryService.GetPoolSummaryAsync(currencyCode);

                // Return timeline as-is (oldest first)
                return Json(new { success = true, timeline, summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pool timeline for currency: {CurrencyCode}", currencyCode);
                return Json(new { success = false, error = "خطا در بارگذاری تاریخچه تراز" });
            }
        }

        // GET: Reports/GetPoolCurrencies
        [HttpGet]
        public async Task<IActionResult> GetPoolCurrencies()
        {
            try
            {
                var currencies = await _context.Currencies
                    .OrderBy(c => c.Code)
                    .Select(c => new { code = c.Code, name = c.Name })
                    .ToListAsync();

                return Json(new { success = true, currencies });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pool currencies");
                return Json(new { success = false, error = "خطا در بارگذاری ارزها" });
            }
        }

        // GET: Reports/GetBankAccountTimeline
        [HttpGet]
        public async Task<IActionResult> GetBankAccountTimeline(int? bankAccountId = null, string? fromDate = null, string? toDate = null)
        {
            try
            {
                DateTime? fromDateTime = null;
                DateTime? toDateTime = null;

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var parsedFromDate))
                {
                    fromDateTime = parsedFromDate;
                }

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var parsedToDate))
                {
                    toDateTime = parsedToDate;
                }

                // Format date range if any dates are provided
                if (fromDateTime.HasValue || toDateTime.HasValue)
                {
                    var (formattedFromDateTime, formattedToDateTime) = FormatDateRange(fromDateTime, toDateTime);
                    fromDateTime = formattedFromDateTime;
                    toDateTime = formattedToDateTime;
                }

                var timeline = await _bankAccountHistoryService.GetBankAccountTimelineAsync(bankAccountId, fromDateTime, toDateTime);
                var summary = await _bankAccountHistoryService.GetBankAccountSummaryAsync(bankAccountId);

                return Json(new { success = true, timeline, summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bank account timeline for account: {BankAccountId}", bankAccountId);
                return Json(new { success = false, error = "خطا در بارگذاری تاریخچه حساب بانکی" });
            }
        }

        // GET: Reports/GetBankAccounts
        [HttpGet]
        public async Task<IActionResult> GetBankAccounts()
        {
            try
            {
                var bankAccounts = await _bankAccountHistoryService.GetBankAccountOptionsAsync();
                return Json(new { success = true, bankAccounts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bank accounts");
                return Json(new { success = false, error = "خطا در بارگذاری حساب‌های بانکی" });
            }
        }

        // GET: Reports/GetCustomers
        [HttpGet]
        public async Task<IActionResult> GetCustomers()
        {
            try
            {
                var customers = await _context.Customers
                    .Where(c => c.IsActive && !c.IsSystem)
                    .Select(c => new { id = c.Id, fullName = c.FullName })
                    .OrderBy(c => c.fullName)
                    .ToListAsync();

                return Json(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customers");
                return Json(new { error = "خطا در بارگذاری مشتریان" });
            }
        }

        // POST: Reports/GetDocumentsDataWithFile
        [HttpPost]
        public async Task<IActionResult> GetDocumentsDataWithFile(DateTime? fromDate, DateTime? toDate, string? currency, string? customer, string? referenceId, decimal? fromAmount, decimal? toAmount, string? bankAccount, IFormFile? fileSearch, int page = 1, int pageSize = 10)
        {
            try
            {
                var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);

                var query = _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Where(ad => ad.DocumentDate >= fromDateTime && ad.DocumentDate <= toDateTime);

                // Apply additional filters
                if (!string.IsNullOrEmpty(currency))
                {
                    query = query.Where(ad => ad.CurrencyCode == currency);
                }

                if (!string.IsNullOrEmpty(customer) && int.TryParse(customer, out int customerId))
                {
                    query = query.Where(ad => ad.PayerCustomerId == customerId || ad.ReceiverCustomerId == customerId);
                }

                // Add reference ID filter
                if (!string.IsNullOrEmpty(referenceId))
                {
                    query = query.Where(ad => ad.ReferenceNumber != null && ad.ReferenceNumber.Contains(referenceId));
                }

                // Add amount range filter
                if (fromAmount.HasValue)
                {
                    query = query.Where(ad => ad.Amount >= fromAmount.Value);
                }

                if (toAmount.HasValue)
                {
                    query = query.Where(ad => ad.Amount <= toAmount.Value);
                }

                // Add bank account filter
                if (!string.IsNullOrEmpty(bankAccount) && int.TryParse(bankAccount, out int bankAccountId))
                {
                    query = query.Where(ad => ad.PayerBankAccountId == bankAccountId || ad.ReceiverBankAccountId == bankAccountId);
                }

                // Handle file search
                byte[]? searchFileData = null;
                if (fileSearch != null && fileSearch.Length > 0)
                {
                    using var memoryStream = new MemoryStream();
                    await fileSearch.CopyToAsync(memoryStream);
                    searchFileData = memoryStream.ToArray();
                }

                // Get initial results 
                var accountingDocs = await query
                    .Select(ad => new
                    {
                        id = ad.Id,
                        date = ad.DocumentDate,
                        customerName = ad.PayerCustomer != null ? ad.PayerCustomer.FullName : (ad.ReceiverCustomer != null ? ad.ReceiverCustomer.FullName : "نامشخص"),
                        amount = ad.Amount,
                        currencyCode = ad.CurrencyCode,
                        referenceNumber = ad.ReferenceNumber,
                        description = ad.Description,
                        status = "تایید شده",
                        fileData = searchFileData != null ? ad.FileData : null // Only load FileData when searching by file
                    })
                    .ToListAsync();

                // Apply file data comparison if file search is requested
                if (searchFileData != null)
                {
                    accountingDocs = accountingDocs.Where(doc => doc.fileData != null && doc.fileData.Length > 0 &&
                        CompareFileData(searchFileData, doc.fileData)).ToList();
                }

                var allDocuments = accountingDocs.Select(ad => new
                {
                    id = ad.id,
                    date = ad.date,
                    type = "سند حسابداری",
                    customerName = ad.customerName,
                    amount = ad.amount,
                    currencyCode = ad.currencyCode,
                    referenceNumber = ad.referenceNumber,
                    description = ad.description,
                    status = ad.status,
                    hasFile = ad.fileData != null && ad.fileData.Length > 0
                }).OrderByDescending(d => d.date).ToList();

                // Apply pagination after file filtering
                var totalCount = allDocuments.Count;
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                var pagedDocuments = allDocuments
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var totalDocuments = totalCount;
                var totalAmount = allDocuments.Sum(d => d.amount);
                var todayDocuments = allDocuments.Count(d => d.date.Date == DateTime.Today);

                return Json(new
                {
                    documents = pagedDocuments,
                    stats = new
                    {
                        totalDocuments,
                        totalReceipts = 0,
                        totalAmount,
                        todayDocuments
                    },
                    pagination = new
                    {
                        currentPage = page,
                        totalPages = totalPages,
                        totalRecords = totalCount,
                        pageSize = pageSize
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents data with file search");
                return Json(new { error = "خطا در دریافت اطلاعات اسناد" });
            }
        }

        /// <summary>
        /// Compare two file data arrays to determine if they are identical
        /// </summary>
        /// <param name="fileData1">First file data</param>
        /// <param name="fileData2">Second file data</param>
        /// <returns>True if files are identical, false otherwise</returns>
        private bool CompareFileData(byte[] fileData1, byte[] fileData2)
        {
            if (fileData1 == null || fileData2 == null)
                return false;

            if (fileData1.Length != fileData2.Length)
                return false;

            // Compare byte by byte
            for (int i = 0; i < fileData1.Length; i++)
            {
                if (fileData1[i] != fileData2[i])
                    return false;
            }

            return true;
        }

        // GET: Reports/PrintBankAccountReport
        [HttpGet]
        public async Task<IActionResult> PrintBankAccountReport(int bankAccountId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                if (bankAccountId <= 0)
                    return BadRequest("Invalid bank account ID");

                // Format date range if dates are provided
                DateTime? formattedFromDate = null;
                DateTime? formattedToDate = null;

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    formattedFromDate = fromDateTime;
                    formattedToDate = toDateTime;
                }

                var timeline = await _bankAccountHistoryService.GetBankAccountTimelineAsync(bankAccountId, formattedFromDate, formattedToDate);
                var summary = await _bankAccountHistoryService.GetBankAccountSummaryAsync(bankAccountId);

                // Get bank account name
                var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId);
                if (bankAccount == null)
                    return NotFound("Bank account not found");

                var bankAccountName = bankAccount.BankName + " - " + bankAccount.CurrencyCode;

                // Convert timeline to generic format with null checks
                var transactions = new List<FinancialTransactionItem>();
                if (timeline != null)
                {
                    foreach (var t in timeline)
                    {
                        try
                        {
                            // Safer date parsing
                            DateTime transactionDate;
                            if (!string.IsNullOrEmpty(t.Date) && !string.IsNullOrEmpty(t.Time))
                            {
                                string dateTimeString = $"{t.Date} {t.Time}";
                                if (DateTime.TryParse(dateTimeString, out transactionDate))
                                {
                                    transactions.Add(new FinancialTransactionItem
                                    {
                                        TransactionDate = transactionDate,
                                        TransactionType = t.TransactionType ?? "نامشخص",
                                        Description = t.Description ?? "",
                                        CurrencyCode = "IRR", // Bank accounts are typically in IRR
                                        Amount = t.Amount,
                                        RunningBalance = t.Balance,
                                        ReferenceId = t.ReferenceId,
                                        CanNavigate = t.CanNavigate
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing transaction date for bank account {BankAccountId}", bankAccountId);
                            // Skip this transaction and continue
                        }
                    }
                }

                // Get final balances from summary with null checks
                var finalBalances = new Dictionary<string, decimal>();
                if (summary != null && summary.AccountBalances != null && summary.AccountBalances.ContainsKey(bankAccountId))
                {
                    try
                    {
                        finalBalances["IRR"] = Convert.ToDecimal(summary.AccountBalances[bankAccountId]);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error converting balance for bank account {BankAccountId}", bankAccountId);
                    }
                }

                var reportModel = new FinancialReportViewModel
                {
                    ReportType = "BankAccount",
                    EntityName = bankAccountName,
                    EntityId = bankAccountId,
                    FromDate = fromDate ?? DateTime.MinValue,
                    ToDate = toDate ?? DateTime.MaxValue,
                    Transactions = transactions,
                    FinalBalances = finalBalances,
                    ReportTitle = $"گزارش حساب بانکی - {bankAccountName}",
                    ReportSubtitle = $"از {fromDate?.ToString("yyyy/MM/dd") ?? "ابتدا"} تا {toDate?.ToString("yyyy/MM/dd") ?? "انتها"}"
                };

                return View("~/Views/PrintViews/BankAccountPrintReport.cshtml", reportModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bank account report for account {BankAccountId}", bankAccountId);
                // Return a proper error response instead of View("Error")
                return StatusCode(500, "خطا در تولید گزارش حساب بانکی");
            }
        }

        // GET: Reports/PrintPoolReport
        [HttpGet]
        public async Task<IActionResult> PrintPoolReport(string currencyCode, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                if (string.IsNullOrEmpty(currencyCode))
                    return BadRequest("Invalid currency code");

                var timeline = await _poolHistoryService.GetPoolTimelineAsync(currencyCode, fromDate, toDate);
                var summary = await _poolHistoryService.GetPoolSummaryAsync(currencyCode);

                if (timeline == null || summary == null)
                    return StatusCode(500, "خطا در دریافت داده‌های گزارش صندوق");

                // Convert timeline to generic format with safe parsing
                var transactions = new List<FinancialTransactionItem>();
                foreach (var t in timeline)
                {
                    try
                    {
                        DateTime transactionDate;
                        if (!DateTime.TryParse($"{t.Date} {t.Time}", out transactionDate))
                        {
                            _logger.LogWarning("Invalid date/time format for pool transaction: Date={Date}, Time={Time}", t.Date, t.Time);
                            continue; // Skip invalid transactions
                        }

                        transactions.Add(new FinancialTransactionItem
                        {
                            TransactionDate = transactionDate,
                            TransactionType = t.TransactionType,
                            Description = t.Description,
                            CurrencyCode = t.CurrencyCode,
                            Amount = t.Amount,
                            RunningBalance = t.Balance,
                            ReferenceId = t.ReferenceId,
                            CanNavigate = t.CanNavigate
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing pool transaction item");
                        continue; // Skip problematic transactions
                    }
                }

                // Get final balances from summary with safe conversion
                var finalBalances = new Dictionary<string, decimal>();
                if (summary.CurrencyBalances != null && summary.CurrencyBalances.ContainsKey(currencyCode))
                {
                    try
                    {
                        finalBalances[currencyCode] = Convert.ToDecimal(summary.CurrencyBalances[currencyCode]);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error converting balance for pool currency {CurrencyCode}", currencyCode);
                    }
                }

                var reportModel = new FinancialReportViewModel
                {
                    ReportType = "Pool",
                    EntityName = currencyCode,
                    EntityId = null,
                    FromDate = fromDate ?? DateTime.MinValue,
                    ToDate = toDate ?? DateTime.MaxValue,
                    Transactions = transactions,
                    FinalBalances = finalBalances,
                    ReportTitle = $"گزارش صندوق - {currencyCode}",
                    ReportSubtitle = $"از {fromDate?.ToString("yyyy/MM/dd") ?? "ابتدا"} تا {toDate?.ToString("yyyy/MM/dd") ?? "انتها"}"
                };

                return View("~/Views/PrintViews/PoolPrintReport.cshtml", reportModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating pool report for currency {CurrencyCode}", currencyCode);
                // Return a proper error response instead of View("Error")
                return StatusCode(500, "خطا در تولید گزارش صندوق");
            }
        }



        #region  ManualAdjustment


        [HttpPost]
        public async Task<IActionResult> CreateManualCustomerBalanceHistory(
            int customerId,
            string currencyCode,
            decimal amount,
            string reason,
            DateTime transactionDate)
        {
            try
            {
                // Validate inputs
                if (customerId <= 0)
                {
                    TempData["Error"] = "لطفاً مشتری معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(currencyCode))
                {
                    TempData["Error"] = "لطفاً ارز معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "لطفاً دلیل تراکنش را وارد کنید";
                    return RedirectToAction("Index");
                }

                // Get customer name for display
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                var customerName = customer?.FullName ?? $"مشتری {customerId}";

                // Get current user for notification exclusion
                var currentUser = await _userManager.GetUserAsync(User);

                // Create the manual history record with notification handling in service layer
                await _centralFinancialService.CreateManualCustomerBalanceHistoryAsync(
                    customerId: customerId,
                    currencyCode: currencyCode,
                    amount: amount,
                    reason: reason,
                    transactionDate: transactionDate,
                    performedBy: "Database Admin",
                    performingUserId: currentUser?.Id
                );

                var summary = new[]
                {
                    "✅ رکورد دستی تاریخچه موجودی ایجاد شد",
                    $"👤 مشتری: {customerName}",
                    $"💰 مبلغ: {amount:N2} {currencyCode}",
                    $"📅 تاریخ تراکنش: {transactionDate:yyyy-MM-dd}",
                    $"📝 دلیل: {reason}",
                    "",
                    "⚠️ مهم: برای اطمینان از انسجام موجودی‌ها، حتماً دکمه 'بازمحاسبه بر اساس تاریخ تراکنش' را اجرا کنید"
                };

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تراکنش دستی با موفقیت ثبت شد" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در ایجاد رکورد دستی: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در ایجاد رکورد دستی: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteManualCustomerBalanceHistory(long transactionId)
        {
            try
            {
                // Find the manual transaction record
                var transaction = await _context.CustomerBalanceHistory
                    .Include(h => h.Customer)
                    .FirstOrDefaultAsync(h => h.Id == transactionId &&
                                           h.TransactionType == CustomerBalanceTransactionType.Manual);

                if (transaction == null)
                {
                    // Check if this is an AJAX request
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        return Json(new { success = false, error = "تراکنش دستی یافت نشد یا این تراکنش قابل حذف نیست" });
                    }

                    TempData["Error"] = "تراکنش دستی یافت نشد یا این تراکنش قابل حذف نیست";
                    return RedirectToAction("Index");
                }

                var customerName = transaction.Customer?.FullName ?? $"مشتری {transaction.CustomerId}";
                var amount = transaction.TransactionAmount;
                var currencyCode = transaction.CurrencyCode;

                // Get current user for notification exclusion
                var currentUser = await _userManager.GetUserAsync(User);

                // Delete the transaction and recalculate balances with notification handling in service layer
                await _centralFinancialService.DeleteManualCustomerBalanceHistoryAsync(transactionId, "Database Admin", currentUser?.Id);

                var summary = new[]
                {
                    "✅ تعدیل دستی با موفقیت حذف شد",
                    $"👤 مشتری: {customerName}",
                    $"💰 مبلغ حذف شده: {amount:N2} {currencyCode}",
                    "",
                    "🔄 موجودی‌ها بازمحاسبه شدند"
                };

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تعدیل دستی با موفقیت حذف شد و موجودی‌ها بازمحاسبه شدند" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در حذف تعدیل دستی: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در حذف تعدیل دستی: {ex.Message}";
            }

            return RedirectToAction("Index");
        }




        // ===== Manual Pool (CurrencyPoolHistory) Adjustment =====
        [HttpPost]
        public async Task<IActionResult> CreateManualPoolBalanceHistory(
            string currencyCode,
            decimal amount,
            string reason,
            DateTime transactionDate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(currencyCode))
                {
                    TempData["Error"] = "لطفاً ارز معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "لطفاً دلیل تراکنش را وارد کنید";
                    return RedirectToAction("Index");
                }
                var currentUser = await _userManager.GetUserAsync(User);
                await _centralFinancialService.CreateManualPoolBalanceHistoryAsync(
                    currencyCode: currencyCode,
                    adjustmentAmount: amount,
                    reason: reason,
                    transactionDate: transactionDate,
                    performedBy: "Database Admin",
                    performingUserId: currentUser?.Id
                );
                var summary = new[]
                {
                    "✅ رکورد دستی صندوق ارزی ایجاد شد",
                    $"💰 مبلغ: {amount:N2} {currencyCode}",
                    $"📅 تاریخ تراکنش: {transactionDate:yyyy-MM-dd}",
                    $"📝 دلیل: {reason}",
                    "",
                    "⚠️ مهم: برای اطمینان از انسجام صندوق، دکمه 'بازمحاسبه' را اجرا کنید"
                };
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تراکنش دستی صندوق با موفقیت ثبت شد" });
                }
                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در ایجاد رکورد دستی صندوق: {ex.Message}" });
                }
                TempData["Error"] = $"خطا در ایجاد رکورد دستی صندوق: {ex.Message}";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteManualPoolBalanceHistory(long transactionId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Use central service for proper deletion and balance recalculation
                await _centralFinancialService.DeleteManualPoolBalanceHistoryAsync(transactionId, "Database Admin", currentUser?.Id);

                var summary = new[]
                {
                    "✅ تعدیل دستی صندوق با موفقیت حذف شد",
                    "",
                    "🔄 صندوق بازمحاسبه شد"
                };

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تعدیل دستی صندوق با موفقیت حذف شد و صندوق بازمحاسبه شد" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در حذف تعدیل دستی صندوق: {ex.Message}" });
                }

                TempData["Error"] = $"خطا در حذف تعدیل دستی صندوق: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // ===== Manual Bank Account (BankAccountBalanceHistory) Adjustment =====
        [HttpPost]
        public async Task<IActionResult> CreateManualBankAccountBalanceHistory(
            int bankAccountId,
            decimal amount,
            string reason,
            DateTime transactionDate)
        {
            try
            {
                if (bankAccountId <= 0)
                {
                    TempData["Error"] = "لطفاً حساب بانکی معتبری انتخاب کنید";
                    return RedirectToAction("Index");
                }
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "لطفاً دلیل تراکنش را وارد کنید";
                    return RedirectToAction("Index");
                }
                var currentUser = await _userManager.GetUserAsync(User);
                await _centralFinancialService.CreateManualBankAccountBalanceHistoryAsync(
                    bankAccountId: bankAccountId,
                    amount: amount,
                    reason: reason,
                    transactionDate: transactionDate,
                    performedBy: currentUser?.FullName ?? "نامشخص",
                    performingUserId: currentUser?.Id
                );
                var summary = new[]
                {
                    "✅ رکورد دستی تاریخچه حساب بانکی ایجاد شد",
                    $"🏦 حساب بانکی: {bankAccountId}",
                    $"💰 مبلغ: {amount:N2}",
                    $"📅 تاریخ تراکنش: {transactionDate:yyyy-MM-dd}",
                    $"📝 دلیل: {reason}",
                    "",
                    "⚠️ مهم: برای اطمینان از انسجام حساب، دکمه 'بازمحاسبه' را اجرا کنید"
                };
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تراکنش دستی حساب بانکی با موفقیت ثبت شد" });
                }
                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در ایجاد رکورد دستی حساب بانکی: {ex.Message}" });
                }
                TempData["Error"] = $"خطا در ایجاد رکورد دستی حساب بانکی: {ex.Message}";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteManualBankAccountBalanceHistory(long transactionId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);

                // Use central service for proper deletion and balance recalculation
                await _centralFinancialService.DeleteManualBankAccountBalanceHistoryAsync(transactionId, "Database Admin", currentUser?.Id);

                var summary = new[]
                {
                    "✅ تعدیل دستی حساب بانکی با موفقیت حذف شد",
                    "",
                    "🔄 حساب بانکی بازمحاسبه شد"
                };

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "تعدیل دستی حساب بانکی با موفقیت حذف شد و حساب بازمحاسبه شد" });
                }

                TempData["Success"] = string.Join("<br/>", summary);
            }
            catch (Exception ex)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, error = $"خطا در حذف تعدیل دستی حساب بانکی: {ex.Message}" });
                }
                TempData["Error"] = $"خطا در حذف تعدیل دستی حساب بانکی: {ex.Message}";
            }
            return RedirectToAction("Index");
        }




        #endregion

        // GET: Reports/ComprehensiveDailyReport
        public IActionResult ComprehensiveDailyReport()
        {
            return View();
        }

        // GET: Reports/GetComprehensiveDailyReport
        [HttpGet]
        public async Task<IActionResult> GetComprehensiveDailyReport(DateTime date)
        {
            try
            {
                _logger.LogInformation("Starting comprehensive daily report for date: {Date}", date);

                // Calculate Customer Balances with details
                var customerBalances = await CalculateCustomerBalancesForDate(date);

                // Calculate Bank Account Balances with details
                var bankAccountBalances = await CalculateBankAccountBalancesForDate(date);

                // Calculate Pool Balances with details
                var poolBalances = await CalculatePoolBalancesForDate(date);

                // Calculate Grand Totals
                var grandTotalIRR = (decimal)customerBalances.irrTotal + (decimal)bankAccountBalances.irrTotal + (decimal)poolBalances.irrTotal;
                var grandTotalOMR = (decimal)customerBalances.omrTotal + (decimal)bankAccountBalances.omrTotal + (decimal)poolBalances.omrTotal;

                var result = new
                {
                    reportDate = date.ToString("yyyy-MM-dd"),
                    customers = new
                    {
                        irrTotal = customerBalances.irrTotal,
                        omrTotal = customerBalances.omrTotal,
                        details = customerBalances.details
                    },
                    bankAccounts = new
                    {
                        irrTotal = bankAccountBalances.irrTotal,
                        omrTotal = bankAccountBalances.omrTotal,
                        details = bankAccountBalances.details
                    },
                    pools = new
                    {
                        irrTotal = poolBalances.irrTotal,
                        omrTotal = poolBalances.omrTotal,
                        details = poolBalances.details
                    },
                    grandTotals = new { irrTotal = grandTotalIRR, omrTotal = grandTotalOMR }
                };

                _logger.LogInformation("Comprehensive daily report completed for date: {Date}", date);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating comprehensive daily report for date: {Date}", date);
                return Json(new { success = false, message = "خطا در تولید گزارش جامع روزانه" });
            }
        }

        private async Task<dynamic> CalculateCustomerBalancesForDate(DateTime date)
        {
            decimal irrTotal = 0;
            decimal omrTotal = 0;
            var details = new List<object>();

            try
            {
                _logger.LogInformation("Calculating customer balances for date: {Date} using CustomerBalanceHistory", date);

                var customers = await _context.Customers
                    .Where(c => c.IsActive)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} active customers", customers.Count);

                foreach (var customer in customers)
                {
                    // Get customer balance snapshot for the specified date (uses CustomerBalanceHistory internally)
                    var snapshot = await _customerHistoryService.GetBalanceSnapshotAsync(customer.Id, date);

                    decimal customerIrrTotal = 0;
                    decimal customerOmrTotal = 0;
                    var currencyBalances = new List<object>();

                    foreach (var balance in snapshot.Balances)
                    {
                        if (balance.Value != 0) // Only include non-zero balances
                        {
                            if (balance.Key == "IRR")
                            {
                                irrTotal += balance.Value;
                                customerIrrTotal += balance.Value;
                            }
                            else
                            {
                                // Convert to OMR
                                var omrEquivalent = await ConvertCurrencyToOMR(balance.Value, balance.Key, date);
                                omrTotal += omrEquivalent;
                                customerOmrTotal += omrEquivalent;
                            }

                            currencyBalances.Add(new
                            {
                                currency = balance.Key,
                                balance = balance.Value,
                                omrEquivalent = balance.Key == "IRR" ? 0 : await ConvertCurrencyToOMR(balance.Value, balance.Key, date)
                            });
                        }
                    }

                    if (currencyBalances.Any()) // Only add customers with non-zero balances
                    {
                        details.Add(new
                        {
                            customerId = customer.Id,
                            customerName = (customer.Gender ? "Mr. " : "Ms. ") + customer.FullName,
                            phoneNumber = customer.PhoneNumber,
                            irrTotal = customerIrrTotal,
                            omrTotal = customerOmrTotal,
                            currencyBalances,
                            source = "CustomerBalanceHistory"
                        });
                    }
                }

                _logger.LogInformation("Customer totals from history - IRR: {IRR}, OMR: {OMR}, Details count: {Count}", irrTotal, omrTotal, details.Count);
                return new { irrTotal, omrTotal, details };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating customer balances for date: {Date}", date);
                return new { irrTotal = 0m, omrTotal = 0m, details = new List<object>() };
            }
        }

        private async Task<dynamic> CalculateBankAccountBalancesForDate(DateTime date)
        {
            decimal irrTotal = 0;
            decimal omrTotal = 0;
            var details = new List<object>();

            try
            {
                _logger.LogInformation("Calculating bank account balances for date: {Date} using BankAccountBalanceHistory", date);

                // First, let's check if we have any bank account balance history records at all
                var totalRecords = await _context.BankAccountBalanceHistory
                    .Where(h => !h.IsDeleted)
                    .CountAsync();

                _logger.LogInformation("Total non-deleted BankAccountBalanceHistory records: {Total}", totalRecords);

                if (totalRecords == 0)
                {
                    _logger.LogWarning("No BankAccountBalanceHistory records found! This means no bank account transactions have been processed through the history system.");

                    // Let's also check if we have bank accounts at all
                    var bankAccountsCount = await _context.BankAccounts.CountAsync();
                    _logger.LogInformation("Total BankAccounts in system: {Count}", bankAccountsCount);

                    return new { irrTotal = 0m, omrTotal = 0m, details = new List<object>() };
                }

                // Check records up to the specified date
                var recordsUpToDate = await _context.BankAccountBalanceHistory
                    .Where(h => !h.IsDeleted && h.TransactionDate <= date)
                    .CountAsync();

                _logger.LogInformation("BankAccountBalanceHistory records up to date {Date}: {Count}", date, recordsUpToDate);

                // Get the latest balance history for each bank account on or before the specified date
                var latestBalances = await _context.BankAccountBalanceHistory
                    .Where(h => !h.IsDeleted && h.TransactionDate <= date)
                    .Include(h => h.BankAccount)
                        .ThenInclude(ba => ba.Customer)
                    .GroupBy(h => h.BankAccountId)
                    .Select(g => g.OrderByDescending(h => h.TransactionDate)
                                  .ThenByDescending(h => h.Id)
                                  .First())
                    .ToListAsync();

                _logger.LogInformation("Found {Count} latest bank account balance records for date {Date}", latestBalances.Count, date);

                foreach (var balanceHistory in latestBalances)
                {
                    _logger.LogInformation("Bank Account {BankAccountId} ({AccountNumber}) - Balance: {Balance} on {Date}",
                        balanceHistory.BankAccountId, balanceHistory.BankAccount?.AccountNumber, balanceHistory.BalanceAfter, balanceHistory.TransactionDate);

                    // Ensure currency code is not null or empty
                    var currencyCode = !string.IsNullOrEmpty(balanceHistory.BankAccount?.CurrencyCode)
                        ? balanceHistory.BankAccount.CurrencyCode
                        : "IRR"; // Default to IRR if not specified

                    // Get currency info for Persian name
                    var currencyInfo = await _context.Currencies
                        .Where(c => c.Code == currencyCode)
                        .FirstOrDefaultAsync();

                    if (currencyCode == "IRR")
                    {
                        irrTotal += balanceHistory.BalanceAfter;
                    }
                    else
                    {
                        // Convert to OMR
                        var omrEquivalent = await ConvertCurrencyToOMR(balanceHistory.BalanceAfter, currencyCode, date);
                        omrTotal += omrEquivalent;
                    }

                    details.Add(new
                    {
                        bankAccountId = balanceHistory.BankAccountId,
                        accountNumber = balanceHistory.BankAccount?.AccountNumber ?? "N/A",
                        bankName = balanceHistory.BankAccount?.BankName ?? "N/A",
                        customerName = balanceHistory.BankAccount?.Customer?.FullName ?? "N/A", // Fixed field name
                        currency = currencyCode, // Fixed field name
                        currencyName = currencyInfo?.PersianName ?? currencyCode,
                        balance = balanceHistory.BalanceAfter,
                        omrEquivalent = currencyCode == "IRR" ? 0 : await ConvertCurrencyToOMR(balanceHistory.BalanceAfter, currencyCode, date),
                        transactionDate = balanceHistory.TransactionDate,
                        source = "BankAccountBalanceHistory"
                    });
                }

                _logger.LogInformation("Bank account totals from history - IRR: {IRR}, OMR: {OMR}, Details count: {Count}", irrTotal, omrTotal, details.Count);
                return new { irrTotal, omrTotal, details };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating bank account balances for date: {Date}", date);
                return new { irrTotal = 0m, omrTotal = 0m, details = new List<object>() };
            }
        }

        private async Task<dynamic> CalculatePoolBalancesForDate(DateTime date)
        {
            decimal irrTotal = 0;
            decimal omrTotal = 0;
            var details = new List<object>();

            try
            {
                _logger.LogInformation("Calculating pool balances for date: {Date} using CurrencyPoolHistory", date);

                // Get the latest balance history for each currency pool on or before the specified date
                var latestBalances = await _context.CurrencyPoolHistory
                    .Where(h => !h.IsDeleted && h.TransactionDate <= date)
                    .GroupBy(h => h.CurrencyCode)
                    .Select(g => g.OrderByDescending(h => h.TransactionDate)
                                  .ThenByDescending(h => h.Id)
                                  .First())
                    .ToListAsync();

                _logger.LogInformation("Found {Count} latest pool balance records for date {Date}", latestBalances.Count, date);

                foreach (var balanceHistory in latestBalances)
                {
                    _logger.LogInformation("Currency Pool {Currency} - Balance: {Balance} on {Date}",
                        balanceHistory.CurrencyCode, balanceHistory.BalanceAfter, balanceHistory.TransactionDate);

                    if (balanceHistory.BalanceAfter != 0)
                    {
                        var currencyInfo = await _context.Currencies
                            .Where(c => c.Code == balanceHistory.CurrencyCode)
                            .FirstOrDefaultAsync();

                        if (balanceHistory.CurrencyCode == "IRR")
                        {
                            irrTotal += balanceHistory.BalanceAfter;
                        }
                        else
                        {
                            // Convert to OMR
                            var omrEquivalent = await ConvertCurrencyToOMR(balanceHistory.BalanceAfter, balanceHistory.CurrencyCode, date);
                            omrTotal += omrEquivalent;
                        }

                        details.Add(new
                        {
                            currencyCode = balanceHistory.CurrencyCode,
                            currencyName = currencyInfo?.PersianName ?? balanceHistory.CurrencyCode,
                            balance = balanceHistory.BalanceAfter,
                            omrEquivalent = balanceHistory.CurrencyCode == "IRR" ? 0 : await ConvertCurrencyToOMR(balanceHistory.BalanceAfter, balanceHistory.CurrencyCode, date),
                            transactionDate = balanceHistory.TransactionDate,
                            source = "CurrencyPoolHistory"
                        });
                    }
                }

                _logger.LogInformation("Pool totals from history - IRR: {IRR}, OMR: {OMR}, Details count: {Count}", irrTotal, omrTotal, details.Count);
                return new { irrTotal, omrTotal, details };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating pool balances for date: {Date}", date);
                return new { irrTotal = 0m, omrTotal = 0m, details = new List<object>() };
            }
        }

        #region Excel Export Methods

        // GET: Reports/ExportToExcel - Main export routing method
        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string type, int? customerId = null, int? bankAccountId = null,
            string? currencyCode = null, DateTime? fromDate = null, DateTime? toDate = null,
            string? customer = null, string? referenceId = null, decimal? fromAmount = null, decimal? toAmount = null,
            string? bankAccount = null, string? fromCurrency = null, string? toCurrency = null, string? orderStatus = null)
        {
            try
            {
                return type.ToLower() switch
                {
                    "customer" => await ExportCustomerTimeline(customerId, fromDate, toDate, currencyCode),
                    "documents" => await ExportDocuments(fromDate, toDate, currencyCode, customer, referenceId, fromAmount, toAmount, bankAccount),
                    "orders" => await ExportOrdersData(fromDate, toDate, fromCurrency, toCurrency),
                    "pool" => await ExportPoolTimeline(currencyCode, fromDate, toDate),
                    "bankaccount" => await ExportBankAccountTimeline(bankAccountId, fromDate, toDate),
                    _ => BadRequest("نوع گزارش نامعتبر است")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting {Type} report to Excel", type);
                return StatusCode(500, "خطا در تولید فایل اکسل");
            }
        }

        // Customer Timeline Excel Export
        private async Task<IActionResult> ExportCustomerTimeline(int? customerId, DateTime? fromDate, DateTime? toDate, string? currencyCode)
        {
            if (!customerId.HasValue)
            {
                return BadRequest("شناسه مشتری الزامی است");
            }

            try
            {
                // Get customer name
                var customer = await _context.Customers.FindAsync(customerId.Value);
                if (customer == null)
                {
                    return NotFound("مشتری یافت نشد");
                }

                // Format date range
                DateTime? formattedFromDate = null;
                DateTime? formattedToDate = null;

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    formattedFromDate = fromDateTime;
                    formattedToDate = toDateTime;
                }

                // Get timeline data
                var timeline = await _customerHistoryService.GetCustomerTimelineAsync(customerId.Value, formattedFromDate, formattedToDate, currencyCode);

                // Generate Excel file
                var excelData = _excelExportService.GenerateCustomerTimelineExcel(
                    customer.FullName,
                    timeline.Transactions.Cast<object>().ToList(),
                    timeline.FinalBalances,
                    formattedFromDate,
                    formattedToDate);

                var fileName = $"گزارش_مالی_مشتری_{customer.FullName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting customer timeline for customer {CustomerId}", customerId);
                return StatusCode(500, "خطا در تولید گزارش مالی مشتری");
            }
        }

        // Documents Excel Export
        private async Task<IActionResult> ExportDocuments(DateTime? fromDate, DateTime? toDate, string? currency, string? customer,
            string? referenceId, decimal? fromAmount, decimal? toAmount, string? bankAccount)
        {
            try
            {
                // Format date range
                var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);

                var query = _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Include(ad => ad.PayerBankAccount)
                    .Include(ad => ad.ReceiverBankAccount)
                    .Where(ad => ad.DocumentDate >= fromDateTime && ad.DocumentDate <= toDateTime);

                // Apply filters
                if (!string.IsNullOrEmpty(currency))
                {
                    query = query.Where(ad => ad.CurrencyCode == currency);
                }

                if (!string.IsNullOrEmpty(customer) && int.TryParse(customer, out int customerId))
                {
                    query = query.Where(ad => ad.PayerCustomerId == customerId || ad.ReceiverCustomerId == customerId);
                }

                if (!string.IsNullOrEmpty(referenceId))
                {
                    query = query.Where(ad => ad.ReferenceNumber != null && ad.ReferenceNumber.Contains(referenceId));
                }

                if (fromAmount.HasValue)
                {
                    query = query.Where(ad => ad.Amount >= fromAmount.Value);
                }

                if (toAmount.HasValue)
                {
                    query = query.Where(ad => ad.Amount <= toAmount.Value);
                }

                if (!string.IsNullOrEmpty(bankAccount) && int.TryParse(bankAccount, out int bankAccountId))
                {
                    query = query.Where(ad => ad.PayerBankAccountId == bankAccountId || ad.ReceiverBankAccountId == bankAccountId);
                }

                var documents = await query
                    .Select(ad => new
                    {
                        date = ad.DocumentDate,
                        documentType = ad.Type.ToString(),
                        amount = ad.Amount,
                        referenceNumber = ad.ReferenceNumber,
                        currencyCode = ad.CurrencyCode,
                        description = ad.Description,
                        payerName = ad.PayerCustomer != null ? ad.PayerCustomer.FullName : (ad.PayerBankAccount != null ? ad.PayerBankAccount.BankName + " - " + ad.PayerBankAccount.AccountNumber : "نامشخص"),
                        receiverName = ad.ReceiverCustomer != null ? ad.ReceiverCustomer.FullName : (ad.ReceiverBankAccount != null ? ad.ReceiverBankAccount.BankName + " - " + ad.ReceiverBankAccount.AccountNumber : "نامشخص"),
                        status = "تایید شده",
                        createdAt = ad.CreatedAt
                    })
                    .ToListAsync();

                // Generate Excel file
                var excelData = _excelExportService.GenerateDocumentsExcel(
                    documents.Cast<object>().ToList(),
                    fromDateTime,
                    toDateTime,
                    currency,
                    customer);

                var fileName = $"گزارش_اسناد_حسابداری_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting documents report");
                return StatusCode(500, "خطا در تولید گزارش اسناد حسابداری");
            }
        }

        // Pool Timeline Excel Export
        private async Task<IActionResult> ExportPoolTimeline(string? currencyCode, DateTime? fromDate, DateTime? toDate)
        {
            if (string.IsNullOrEmpty(currencyCode))
            {
                return BadRequest("کد ارز الزامی است");
            }

            try
            {
                // Format date range
                DateTime? formattedFromDate = null;
                DateTime? formattedToDate = null;

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    formattedFromDate = fromDateTime;
                    formattedToDate = toDateTime;
                }

                // Get timeline data
                var timeline = await _poolHistoryService.GetPoolTimelineAsync(currencyCode, formattedFromDate, formattedToDate);

                // Generate Excel file
                var excelData = _excelExportService.GeneratePoolTimelineExcel(
                    currencyCode,
                    timeline.Cast<object>().ToList(),
                    formattedFromDate,
                    formattedToDate);

                var fileName = $"گزارش_صندوق_{currencyCode}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting pool timeline for currency {CurrencyCode}", currencyCode);
                return StatusCode(500, "خطا در تولید گزارش صندوق");
            }
        }

        // Orders Excel Export
        private async Task<IActionResult> ExportOrdersData(DateTime? fromDate, DateTime? toDate, string? fromCurrency, string? toCurrency)
        {
            try
            {
                // Format date range
                DateTime? formattedFromDate = null;
                DateTime? formattedToDate = null;

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    formattedFromDate = fromDateTime;
                    formattedToDate = toDateTime;
                }

                // Build query for orders
                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .AsQueryable();

                // Apply date range filter
                if (formattedFromDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt >= formattedFromDate.Value);
                }
                if (formattedToDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt <= formattedToDate.Value);
                }

                // Apply currency filters
                if (!string.IsNullOrEmpty(fromCurrency))
                {
                    query = query.Where(o => o.FromCurrency.Code == fromCurrency);
                }
                if (!string.IsNullOrEmpty(toCurrency))
                {
                    query = query.Where(o => o.ToCurrency.Code == toCurrency);
                }

                // Get the orders
                var orders = await query
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new
                    {
                        id = o.Id,
                        createdAt = o.CreatedAt,
                        customerName = o.Customer.FullName,
                        fromCurrency = o.FromCurrency.Code,
                        amount = o.FromAmount,
                        toCurrency = o.ToCurrency.Code,
                        rate = o.Rate,
                        totalValue = o.ToAmount,
                        status = "تکمیل شده" // All orders are complete
                    })
                    .ToListAsync();

                // Generate Excel file
                var excelData = _excelExportService.GenerateOrdersExcel(
                    orders.Cast<object>().ToList(),
                    formattedFromDate,
                    formattedToDate,
                    fromCurrency,
                    toCurrency);

                var fileName = $"گزارش_معاملات_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting orders data");
                return StatusCode(500, "خطا در تولید گزارش معاملات");
            }
        }

        // Bank Account Timeline Excel Export
        private async Task<IActionResult> ExportBankAccountTimeline(int? bankAccountId, DateTime? fromDate, DateTime? toDate)
        {
            if (!bankAccountId.HasValue)
            {
                return BadRequest("شناسه حساب بانکی الزامی است");
            }

            try
            {
                // Get bank account name
                var bankAccount = await _context.BankAccounts.FindAsync(bankAccountId.Value);
                if (bankAccount == null)
                {
                    return NotFound("حساب بانکی یافت نشد");
                }

                // Format date range
                DateTime? formattedFromDate = null;
                DateTime? formattedToDate = null;

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var (fromDateTime, toDateTime) = FormatDateRange(fromDate, toDate);
                    formattedFromDate = fromDateTime;
                    formattedToDate = toDateTime;
                }

                // Get timeline data
                var timeline = await _bankAccountHistoryService.GetBankAccountTimelineAsync(bankAccountId.Value, formattedFromDate, formattedToDate);

                // Generate Excel file
                var excelData = _excelExportService.GenerateBankAccountTimelineExcel(
                    $"{bankAccount.BankName} - {bankAccount.AccountNumber}",
                    timeline.Cast<object>().ToList(),
                    formattedFromDate,
                    formattedToDate);

                var fileName = $"گزارش_حساب_بانکی_{bankAccount.BankName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting bank account timeline for account {BankAccountId}", bankAccountId);
                return StatusCode(500, "خطا در تولید گزارش حساب بانکی");
            }
        }

        #endregion

    }
}

