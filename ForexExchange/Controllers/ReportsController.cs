using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Services;
using Microsoft.AspNetCore.Identity;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class ReportsController : Controller
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<ReportsController> _logger;
        private readonly CustomerFinancialHistoryService _customerHistoryService;
        private readonly PoolFinancialHistoryService _poolHistoryService;
        private readonly BankAccountFinancialHistoryService _bankAccountHistoryService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICentralFinancialService _centralFinancialService;



        public ReportsController(
            ForexDbContext context,
            ILogger<ReportsController> logger,
            CustomerFinancialHistoryService customerHistoryService,
            PoolFinancialHistoryService poolHistoryService,
            BankAccountFinancialHistoryService bankAccountHistoryService,
            UserManager<ApplicationUser> userManager,
             ICentralFinancialService centralFinancialService)
        {
            _context = context;
            _logger = logger;
            _customerHistoryService = customerHistoryService;
            _poolHistoryService = poolHistoryService;
            _bankAccountHistoryService = bankAccountHistoryService;
            _userManager = userManager;
            _centralFinancialService = centralFinancialService;
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
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                var query = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate);

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
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                var query = _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Where(ad => ad.DocumentDate >= fromDate && ad.DocumentDate <= toDate);

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
                        description = $"ایجاد معامله {o.FromCurrency.Code} به {o.ToCurrency.Code}",
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
                fromDate ??= DateTime.Today.AddDays(-30);
                toDate ??= DateTime.Today.AddDays(1);

                var query = _context.AccountingDocuments
                    .Include(ad => ad.PayerCustomer)
                    .Include(ad => ad.ReceiverCustomer)
                    .Where(ad => ad.DocumentDate >= fromDate && ad.DocumentDate <= toDate);

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

                var timeline = await _bankAccountHistoryService.GetBankAccountTimelineAsync(bankAccountId, fromDate, toDate);
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

                return View("~/Views/PrintViews/CustomerPrintReport.cshtml", reportModel);
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

                return View("~/Views/PrintViews/CustomerPrintReport.cshtml", reportModel);
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
                    performedBy: currentUser?.FullName?? "نامشخص",
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

    }
}

