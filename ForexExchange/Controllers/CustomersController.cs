using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using ForexExchange.Models;
using ForexExchange.Services;
using System.Globalization;

namespace ForexExchange.Controllers
{
    [Authorize(Roles = "Admin,Manager,Staff")]
    public class CustomersController : Controller
    {
    private readonly ForexDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomersController> _logger;
    private readonly CustomerDebtCreditService _debtCreditService;
    private readonly IShareableLinkService _shareableLinkService;

    public CustomersController(
        ForexDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CustomersController> logger,
        CustomerDebtCreditService debtCreditService,
        IShareableLinkService shareableLinkService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _debtCreditService = debtCreditService;
        _shareableLinkService = shareableLinkService;
    }        // GET: Customers
        public async Task<IActionResult> Index()
        {
            var customers = await _context.Customers
                .Where(c => c.IsActive && c.IsSystem == false)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                // TODO: Include customer balances when implementing new architecture
                // .Include(c => c.Balances)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.FromCurrency)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.ToCurrency)
                // TODO: Re-implement with new architecture
                // .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                //     .ThenInclude(t => t.SellerCustomer)
                // .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                //     .ThenInclude(t => t.FromCurrency)
                // .Include(c => c.BuyTransactions.OrderByDescending(t => t.CreatedAt))
                //     .ThenInclude(t => t.ToCurrency)
                // TODO: Re-implement with new architecture
                // .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                //     .ThenInclude(t => t.BuyerCustomer)
                // .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                //     .ThenInclude(t => t.FromCurrency)
                // .Include(c => c.SellTransactions.OrderByDescending(t => t.CreatedAt))
                //     .ThenInclude(t => t.ToCurrency)
                // .Include(c => c.Receipts.OrderByDescending(r => r.UploadedAt))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Calculate customer statistics
            var stats = new CustomerProfileStats
            {
                TotalOrders = customer.Orders.Count,
                CompletedOrders = 0, // Removed OrderStatus from Order model
                PendingOrders = 0, // Removed OrderStatus from Order model
                // TODO: Re-implement with new architecture
                TotalTransactions = 0, // customer.BuyTransactions.Count + customer.SellTransactions.Count,
                CompletedTransactions = 0, // customer.BuyTransactions.Count(t => t.Status == TransactionStatus.Completed) + customer.SellTransactions.Count(t => t.Status == TransactionStatus.Completed),
                TotalAccountingDocuments = 0, // customer.Receipts.Count,
                VerifiedAccountingDocuments = 0, // customer.Receipts.Count(r => r.IsVerified),
                TotalVolumeInToman = 0, // Removed TotalInToman from Order model
                RegistrationDays = (DateTime.Now - customer.CreatedAt).Days
            };

            ViewBag.CustomerStats = stats;
            return View(customer);
        }

        // GET: Customers/Profile/5 - Comprehensive customer profile
        public async Task<IActionResult> Profile(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Balances) // Using new CustomerBalance relationship
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.FromCurrency)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.ToCurrency)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Load all accounting documents for this customer
            var allAccountingDocuments = await _context.AccountingDocuments
                .Where(d => d.CustomerId == id)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            // Calculate customer statistics
            var stats = new CustomerProfileStats
            {
                TotalOrders = customer.Orders.Count,
                CompletedOrders = 0, // Removed OrderStatus from Order model
                PendingOrders = 0, // Removed OrderStatus from Order model
                // TODO: Re-implement with new architecture
                TotalTransactions = 0, // customer.BuyTransactions.Count + customer.SellTransactions.Count,
                CompletedTransactions = 0, // customer.BuyTransactions.Count(t => t.Status == TransactionStatus.Completed) + customer.SellTransactions.Count(t => t.Status == TransactionStatus.Completed),
                TotalAccountingDocuments = allAccountingDocuments.Count,
                VerifiedAccountingDocuments = allAccountingDocuments.Count(d => d.IsVerified),
                TotalVolumeInToman = 0, // Removed TotalInToman from Order model
                RegistrationDays = (DateTime.Now - customer.CreatedAt).Days
            };

            // Calculate debt/credit information for this customer
            var customerDebtCredit = await _debtCreditService.GetCustomerDebtCreditAsync(customer.Id);

            ViewBag.CustomerStats = stats;
            ViewBag.CustomerDebtCredit = customerDebtCredit;
            ViewBag.AllAccountingDocuments = allAccountingDocuments;
            return View(customer);
        }

        // GET: Customers/ComprehensiveStatement/5 - Complete customer statement
        public async Task<IActionResult> ComprehensiveStatement(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Balances)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.FromCurrency)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.ToCurrency)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Get all accounting documents for this customer
            var documents = await _context.AccountingDocuments
                .Include(a => a.BankAccount)
                .Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.DocumentDate)
                .ToListAsync();

            // Get customer balance using the service
            var balances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == id)
                .ToListAsync();

            // Get customer debt/credit information
            var customerDebtCredit = await _debtCreditService.GetCustomerDebtCreditAsync(customer.Id);

            // Calculate customer statistics
            var stats = new CustomerProfileStats
            {
                TotalOrders = customer.Orders.Count,
                TotalAccountingDocuments = documents.Count,
                VerifiedAccountingDocuments = documents.Count(d => d.IsVerified),
                RegistrationDays = (DateTime.Now - customer.CreatedAt).Days
            };

            var viewModel = new CustomerComprehensiveStatementViewModel
            {
                Customer = customer,
                Documents = documents,
                Balances = balances,
                Orders = customer.Orders.ToList(),
                CustomerDebtCredit = customerDebtCredit,
                Stats = stats,
                StatementDate = DateTime.Now
            };

            return View(viewModel);
        }

        // GET: Customers/TransactionsStatement/5 - Customer transactions statement
        public async Task<IActionResult> TransactionsStatement(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.FromCurrency)
                .Include(c => c.Orders.OrderByDescending(o => o.CreatedAt))
                    .ThenInclude(o => o.ToCurrency)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (customer == null)
            {
                return NotFound();
            }

            // Group orders by currency pairs for analysis
            var currencyPairStats = customer.Orders
                .GroupBy(o => new { FromCurrency = o.FromCurrency?.Code, ToCurrency = o.ToCurrency?.Code })
                .Select(g => new
                {
                    FromCurrency = g.Key.FromCurrency,
                    ToCurrency = g.Key.ToCurrency,
                    TotalTransactions = g.Count(),
                    TotalAmount = g.Sum(o => o.Amount),
                    AverageRate = g.Average(o => o.Rate),
                    MinRate = g.Min(o => o.Rate),
                    MaxRate = g.Max(o => o.Rate),
                    TotalValueInTargetCurrency = g.Sum(o => o.Amount * o.Rate)
                })
                .ToList();

            // Calculate monthly statistics
            var monthlyStats = customer.Orders
                .GroupBy(o => new { Year = o.CreatedAt.Year, Month = o.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TransactionCount = g.Count(),
                    TotalVolume = g.Sum(o => o.Amount * o.Rate) // Assuming this is total value
                })
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => s.Month)
                .Take(12)
                .ToList();

            var viewModel = new CustomerTransactionsStatementViewModel
            {
                Customer = customer,
                Orders = customer.Orders.ToList(),
                CurrencyPairStats = currencyPairStats.Select(s => new CurrencyPairStatistic
                {
                    FromCurrency = s.FromCurrency ?? "",
                    ToCurrency = s.ToCurrency ?? "",
                    TotalTransactions = s.TotalTransactions,
                    TotalAmount = s.TotalAmount,
                    AverageRate = s.AverageRate,
                    MinRate = s.MinRate,
                    MaxRate = s.MaxRate,
                    TotalValueInTargetCurrency = s.TotalValueInTargetCurrency
                }).ToList(),
                MonthlyStats = monthlyStats.Select(s => new MonthlyStatistic
                {
                    Year = s.Year,
                    Month = s.Month,
                    TransactionCount = s.TransactionCount,
                    TotalVolume = s.TotalVolume
                }).ToList(),
                StatementDate = DateTime.Now
            };

            return View(viewModel);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            // supply currency dropdown
            var currencyOptions = _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Code, c.PersianName })
                .ToList();
                
            _logger.LogInformation($"GET Create: Loading {currencyOptions.Count} currency options");
            foreach (var currency in currencyOptions)
            {
                _logger.LogInformation($"Currency option: {currency.Code} - {currency.PersianName}");
            }
                
            ViewBag.CurrencyOptions = currencyOptions;
            return View(new CustomerCreateViewModel());
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerCreateViewModel model)
        {
            Console.WriteLine("\n=== CREATE CUSTOMER - SERVER SIDE ===");
            Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            
            // Log client debug info if available
            var clientDebugInfo = Request.Form["ClientDebugInfo"].FirstOrDefault();
            if (!string.IsNullOrEmpty(clientDebugInfo))
            {
                Console.WriteLine("🔍 CLIENT DEBUG INFO RECEIVED:");
                Console.WriteLine(clientDebugInfo);
                Console.WriteLine("--- END CLIENT DEBUG ---\n");
            }
            else
            {
                Console.WriteLine("⚠️ No ClientDebugInfo received from client");
            }

            // Remove any email validation errors from ModelState first
            ModelState.Remove("Email");
            
            // Custom email validation - validate format only if email is provided
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var emailAttribute = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
                if (!emailAttribute.IsValid(model.Email))
                {
                    ModelState.AddModelError("Email", "فرمت ایمیل صحیح نیست");
                }
            }

            if (ModelState.IsValid)
            {
                // Check if email exists only if email is provided
                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    var existingUser = await _userManager.FindByEmailAsync(model.Email!);
                    if (existingUser != null)
                    {
                        ModelState.AddModelError("Email", "کاربری با این ایمیل قبلاً ثبت شده است.");
                        return View(model);
                    }
                }

                // Check if phone number already exists (normalize first)
                string normalizedPhoneNumber = PhoneNumberService.NormalizePhoneNumber(model.PhoneNumber);
                
                // Validate normalized phone number
                if (!PhoneNumberService.IsValidNormalizedPhoneNumber(normalizedPhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "فرمت شماره تلفن صحیح نیست. لطفاً شماره تلفن معتبر وارد کنید.");
                    return View(model);
                }

                var existingCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhoneNumber && c.IsActive);

                if (existingCustomer != null)
                {
                    ModelState.AddModelError("PhoneNumber", "مشتری با این شماره تلفن قبلاً ثبت شده است.");
                    return View(model);
                }

                // Create Customer entity
                var customer = new Customer
                {
                    FullName = model.FullName,
                    Email = model.Email ?? string.Empty,
                    PhoneNumber = normalizedPhoneNumber, // Use normalized phone number
                    NationalId = model.NationalId ?? string.Empty,
                    Address = model.Address ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    IsActive = model.IsActive
                };

                _context.Add(customer);
                await _context.SaveChangesAsync();

                // Create corresponding ApplicationUser
                var user = new ApplicationUser
                {
                    UserName = normalizedPhoneNumber, // Use normalized phone number as username
                    Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email,
                    PhoneNumber = normalizedPhoneNumber, // Use normalized phone number
                    FullName = model.FullName,
                    NationalId = model.NationalId ?? string.Empty,
                    Address = model.Address ?? string.Empty,
                    Role = UserRole.Customer,
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email),
                    CustomerId = customer.Id
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    // Extract and save initial balances with robust parsing
                    var initialBalances = ExtractInitialBalancesFromForm();
                    _logger.LogInformation($"CREATE: Processing {initialBalances.Count} initial balances");
                    
                    foreach (var balance in initialBalances)
                    {
                        var code = balance.Key?.Trim().ToUpperInvariant();
                        var amount = balance.Value; // Preserve original value (including negative)
                        
                        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
                        {
                            _logger.LogWarning($"CREATE: Skipping invalid currency code: '{code}'");
                            continue;
                        }
                        
                        _logger.LogInformation($"CREATE: Saving {code} = {amount}");
                        _context.CustomerBalances.Add(new CustomerBalance
                        {
                            CustomerId = customer.Id,
                            CurrencyCode = code,
                            Balance = amount,
                            LastUpdated = DateTime.Now,
                            Notes = "Initial balance set during customer creation"
                        });
                    }
                    
                    if (initialBalances.Count > 0)
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"CREATE: Successfully saved {initialBalances.Count} initial balances");
                    }
                    TempData["SuccessMessage"] = "مشتری و حساب کاربری با موفقیت ایجاد شد.";
                    return RedirectToAction(nameof(Profile), new { id = customer.Id });
                }
                else
                {
                    // If user creation failed, remove the customer
                    _context.Remove(customer);
                    await _context.SaveChangesAsync();

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            // repopulate currency dropdown on error
            ViewBag.CurrencyOptions = _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Code, c.PersianName })
                .ToList();
            return View(model);
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Balances) // CustomerBalance uses CurrencyCode directly
                .FirstOrDefaultAsync(c => c.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            var model = new CustomerEditViewModel
            {
                Id = customer.Id,
                FullName = customer.FullName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                NationalId = customer.NationalId,
                Address = customer.Address,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt,
                InitialBalances = customer.Balances?.ToDictionary(b => b.CurrencyCode, b => b.Balance) ?? new Dictionary<string, decimal>()
            };

            // supply currency dropdown
            ViewBag.CurrencyOptions = await _context.Currencies
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Code, c.PersianName })
                .ToListAsync();

            return View(model);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerEditViewModel model)
        {
           
            if (id != model.Id)
            {
                return NotFound();
            }

            // Remove any email validation errors from ModelState first
            ModelState.Remove("Email");
            // Make NationalId optional: if empty, remove validation so it won't block updates
            if (string.IsNullOrWhiteSpace(model.NationalId))
            {
                model.NationalId = null;
                ModelState.Remove("NationalId");
            }
            
            // Custom email validation - validate format only if email is provided
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var emailAttribute = new System.ComponentModel.DataAnnotations.EmailAddressAttribute();
                if (!emailAttribute.IsValid(model.Email))
                {
                    ModelState.AddModelError("Email", "فرمت ایمیل صحیح نیست");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // TODO: Implement balance management with CustomerBalance in new architecture
                    /*
                    var customer = await _context.Customers.Include(c => c.InitialBalances).FirstOrDefaultAsync(c => c.Id == id);
                    */
                    var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id);
                    if (customer == null)
                    {
                        return NotFound();
                    }

                    // Normalize phone number for validation and storage
                    string normalizedPhoneNumber = PhoneNumberService.NormalizePhoneNumber(model.PhoneNumber);
                    
                    // Validate normalized phone number
                    if (!PhoneNumberService.IsValidNormalizedPhoneNumber(normalizedPhoneNumber))
                    {
                        ModelState.AddModelError("PhoneNumber", "فرمت شماره تلفن صحیح نیست. لطفاً شماره تلفن معتبر وارد کنید.");
                        return View(model);
                    }

                    // Check if email changed and if new email exists (only if email is provided)
                    if (customer.Email != model.Email && !string.IsNullOrWhiteSpace(model.Email))
                    {
                        var existingUser = await _userManager.FindByEmailAsync(model.Email!);
                        if (existingUser != null && existingUser.CustomerId != customer.Id)
                        {
                            ModelState.AddModelError("Email", "کاربری با این ایمیل قبلاً ثبت شده است.");
                            return View(model);
                        }
                    }

                    // Check if phone number changed and if new normalized phone exists
                    if (customer.PhoneNumber != normalizedPhoneNumber)
                    {
                        var existingCustomer = await _context.Customers
                            .FirstOrDefaultAsync(c => c.PhoneNumber == normalizedPhoneNumber && c.IsActive && c.Id != id);

                        if (existingCustomer != null)
                        {
                            ModelState.AddModelError("PhoneNumber", "مشتری با این شماره تلفن قبلاً ثبت شده است.");
                            return View(model);
                        }
                    }

                    // Update customer entity
                    customer.FullName = model.FullName;
                    customer.Email = model.Email ?? string.Empty;
                    customer.PhoneNumber = normalizedPhoneNumber; // Use normalized phone number
                    customer.NationalId = model.NationalId ?? string.Empty;
                    customer.Address = model.Address ?? string.Empty;
                    customer.IsActive = model.IsActive;

                    _context.Update(customer);

                    // Update corresponding ApplicationUser
                    var user = await _userManager.Users.FirstOrDefaultAsync(u => u.CustomerId == customer.Id);
                    if (user != null)
                    {
                        user.UserName = normalizedPhoneNumber; // Use normalized phone number as username
                        user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email;
                        user.PhoneNumber = normalizedPhoneNumber; // Use normalized phone number
                        user.FullName = model.FullName;
                        user.NationalId = model.NationalId ?? string.Empty;
                        user.Address = model.Address ?? string.Empty;
                        user.IsActive = model.IsActive;
                        user.EmailConfirmed = !string.IsNullOrWhiteSpace(model.Email);

                        await _userManager.UpdateAsync(user);

                        // Update password if provided
                        if (!string.IsNullOrEmpty(model.NewPassword))
                        {
                            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                            var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                            if (!passwordResult.Succeeded)
                            {
                                foreach (var error in passwordResult.Errors)
                                {
                                    ModelState.AddModelError(string.Empty, $"خطا در تغییر رمز عبور: {error.Description}");
                                }
                                return View(model);
                            }
                        }
                    }

                    // Extract and update CustomerBalances
                    var providedBalances = ExtractInitialBalancesFromForm();
                    _logger.LogInformation($"EDIT: Processing {providedBalances.Count} initial balances");
                    
                    var existingBalances = await _context.CustomerBalances
                        .Where(b => b.CustomerId == customer.Id)
                        .ToListAsync();

                    // Delete balances not in the provided list
                    foreach (var existingBalance in existingBalances)
                    {
                        if (!providedBalances.ContainsKey(existingBalance.CurrencyCode))
                        {
                            _logger.LogInformation($"EDIT: Removing {existingBalance.CurrencyCode} balance");
                            _context.CustomerBalances.Remove(existingBalance);
                        }
                    }

                    // Add or update provided balances
                    foreach (var balance in providedBalances)
                    {
                        var code = balance.Key?.Trim().ToUpperInvariant();
                        var amount = balance.Value; // Preserve original value (including negative)
                        
                        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
                        {
                            _logger.LogWarning($"EDIT: Skipping invalid currency code: '{code}'");
                            continue;
                        }

                        var existingBalance = existingBalances.FirstOrDefault(b => b.CurrencyCode == code);
                        if (existingBalance == null)
                        {
                            _logger.LogInformation($"EDIT: Adding new {code} = {amount}");
                            _context.CustomerBalances.Add(new CustomerBalance
                            {
                                CustomerId = customer.Id,
                                CurrencyCode = code,
                                Balance = amount,
                                LastUpdated = DateTime.Now,
                                Notes = "Initial balance updated during customer edit"
                            });
                        }
                        else
                        {
                            _logger.LogInformation($"EDIT: Updating {code} from {existingBalance.Balance} to {amount}");
                            existingBalance.Balance = amount;
                            existingBalance.LastUpdated = DateTime.Now;
                            existingBalance.Notes = "Balance updated during customer edit";
                            _context.CustomerBalances.Update(existingBalance);
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "اطلاعات مشتری با موفقیت به‌روزرسانی شد.";
                    return RedirectToAction(nameof(Profile), new { id = customer.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            // Before return on validation errors, repopulate currency options
            if (!ModelState.IsValid)
            {
                ViewBag.CurrencyOptions = await _context.Currencies
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new { c.Code, c.PersianName })
                    .ToListAsync();
                return View(model);
            }
            return View(model);
        }

        private Dictionary<string, decimal> ExtractInitialBalancesFromForm()
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var form = Request?.Form ?? new Microsoft.AspNetCore.Http.FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
                _logger.LogInformation($"=== EXTRACTING INITIAL BALANCES FROM FORM ===");
                _logger.LogInformation($"Form has {form.Count} total keys: {string.Join(", ", form.Keys)}");
                
                // Method 1: Try dictionary-style inputs (InitialBalances[CODE])
                _logger.LogInformation("Method 1: Looking for InitialBalances[CODE] inputs...");
                var dictionaryInputs = form.Keys.Where(k => k.StartsWith("InitialBalances[")).ToList();
                _logger.LogInformation($"Found {dictionaryInputs.Count} dictionary-style inputs: {string.Join(", ", dictionaryInputs)}");
                
                foreach (var name in dictionaryInputs)
                {
                    if (name.StartsWith("InitialBalances[", StringComparison.Ordinal) && name.EndsWith("]", StringComparison.Ordinal))
                    {
                        var inner = name.Substring("InitialBalances[".Length);
                        var code = inner.Substring(0, inner.Length - 1).Trim().ToUpperInvariant();
                        var raw = form[name].ToString().Trim();
                        _logger.LogInformation($"Processing dictionary input: {name} = '{raw}'");
                        
                        if (string.IsNullOrWhiteSpace(code)) 
                        {
                            _logger.LogWarning($"Skipping empty currency code from {name}");
                            continue;
                        }

                        // Normalize Persian/Arabic digits and separators
                        raw = (raw ?? "").Replace("\u066C", "").Replace("\u066B", "."); // Arabic thousands/decimal
                        // If string has comma but no dot, treat comma as decimal separator; otherwise remove commas (thousands)
                        if (raw.Contains(',') && !raw.Contains('.'))
                            raw = raw.Replace(',', '.');
                        else
                            raw = raw.Replace(",", "");
                        raw = raw.Replace(" ", "");

                        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                        {
                            // Preserve original value (including negative)
                           
                            result[code] = amount;
                            _logger.LogInformation($"SUCCESS: Added {code} = {amount} from dictionary method");
                        }
                        else
                        {
                            _logger.LogWarning($"FAILED: Could not parse amount '{raw}' for currency {code}");
                        }
                    }
                }

                // Method 2: Try paired arrays (ib_code[], ib_amount[])
                _logger.LogInformation("Method 2: Looking for ib_code/ib_amount arrays...");
                var codes = form["ib_code"]; // multiple
                var amounts = form["ib_amount"]; // multiple
                
                _logger.LogInformation($"Found {codes.Count} codes and {amounts.Count} amounts");
                for (int i = 0; i < codes.Count && i < amounts.Count; i++)
                {
                    _logger.LogInformation($"Array item {i}: code='{codes[i]}', amount='{amounts[i]}'");
                }

                if (codes.Count > 0 && amounts.Count > 0)
                {
                    for (int i = 0; i < Math.Min(codes.Count, amounts.Count); i++)
                    {
                        var code = codes[i]?.Trim().ToUpperInvariant();
                        var raw = amounts[i]?.Trim();
                        
                        _logger.LogInformation($"Processing array item {i}: code='{code}', amount='{raw}'");
                        
                        if (string.IsNullOrWhiteSpace(code)) 
                        {
                            _logger.LogWarning($"Skipping empty currency code at index {i}");
                            continue;
                        }
                        
                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            _logger.LogWarning($"Skipping empty amount for currency {code} at index {i}");
                            continue;
                        }
                        
                        raw = (raw ?? "").Replace("\u066C", "").Replace("\u066B", ".");
                        if (raw.Contains(',') && !raw.Contains('.')) raw = raw.Replace(',', '.');
                        else raw = raw.Replace(",", "");
                        raw = raw.Replace(" ", "");
                        
                        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                        {
                            // Preserve original value (including negative)
                            if (!result.ContainsKey(code))
                            {
                                result[code] = amount;
                                _logger.LogInformation($"SUCCESS: Added from arrays {code} = {amount}");
                            }
                            else
                            {
                                _logger.LogInformation($"SKIPPED: {code} already exists from dictionary method");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"FAILED: Could not parse amount '{raw}' for currency {code}");
                        }
                    }
                }

                _logger.LogInformation($"=== FINAL RESULT: {result.Count} currencies ===");
                foreach (var kvp in result)
                {
                    _logger.LogInformation($"Final: {kvp.Key} = {kvp.Value}");
                }
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Error in ExtractInitialBalancesFromForm");
            }
            return result;
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(m => m.Id == id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer != null)
            {
                // Soft delete - just mark as inactive
                customer.IsActive = false;
                _context.Update(customer);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "مشتری غیرفعال شد.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: API endpoint for customer search
        [HttpGet]
        public async Task<IActionResult> Search(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new List<object>());
            }

            var customers = await _context.Customers
                .Where(c => c.IsActive &&
                           (c.FullName.Contains(term) || c.PhoneNumber.Contains(term)))
                .Select(c => new
                {
                    id = c.Id,
                    text = $"{c.FullName} - {c.PhoneNumber}"
                })
                .Take(10)
                .ToListAsync();

            return Json(customers);
        }

        // POST: Customers/GenerateShareableLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateShareableLink(int customerId, ShareableLinkType linkType, int expirationDays = 7)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    TempData["ErrorMessage"] = "مشتری یافت نشد.";
                    return RedirectToAction("Details", new { id = customerId });
                }

                var currentUser = User.Identity?.Name ?? "Admin";
                var description = linkType switch
                {
                    ShareableLinkType.ComprehensiveStatement => "لینک اشتراک صورت حساب جامع",
                    ShareableLinkType.TransactionsStatement => "لینک اشتراک صورت حساب معاملات",
                    _ => "لینک اشتراک"
                };

                var shareableLink = await _shareableLinkService.GenerateLinkAsync(
                    customerId, 
                    linkType, 
                    expirationDays, 
                    description, 
                    currentUser);

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var fullUrl = shareableLink.GetShareableUrl(baseUrl);

                TempData["SuccessMessage"] = $"لینک اشتراک با موفقیت ایجاد شد. لینک تا {expirationDays} روز آینده معتبر است.";
                TempData["ShareableUrl"] = fullUrl;
                
                return RedirectToAction("Details", new { id = customerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating shareable link for customer {CustomerId}", customerId);
                TempData["ErrorMessage"] = "خطا در ایجاد لینک اشتراک.";
                return RedirectToAction("Details", new { id = customerId });
            }
        }

        // GET: Customers/ShareableLinks/5
        public async Task<IActionResult> ShareableLinks(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            var links = await _shareableLinkService.GetCustomerLinksAsync(id, activeOnly: false);
            
            ViewBag.Customer = customer;
            return View(links);
        }

        // POST: Customers/DeactivateShareableLink
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateShareableLink(int linkId, int customerId)
        {
            try
            {
                var success = await _shareableLinkService.DeactivateLinkAsync(linkId, User.Identity?.Name);
                if (success)
                {
                    TempData["SuccessMessage"] = "لینک اشتراک با موفقیت غیرفعال شد.";
                }
                else
                {
                    TempData["ErrorMessage"] = "لینک یافت نشد.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating shareable link {LinkId}", linkId);
                TempData["ErrorMessage"] = "خطا در غیرفعال کردن لینک.";
            }

            return RedirectToAction("ShareableLinks", new { id = customerId });
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}
