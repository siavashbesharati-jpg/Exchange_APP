using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface IDataSeedService
    {
        Task SeedDataAsync();
    }

    public class DataSeedService : IDataSeedService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ForexDbContext _context;
        private readonly ILogger<DataSeedService> _logger;
        private readonly IWebScrapingService _webScrapingService;
        private readonly IRateCalculationService _rateCalc;
        private readonly ICentralFinancialService _centralFinancialService;

        public DataSeedService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ForexDbContext context,
            ILogger<DataSeedService> logger,
            IWebScrapingService webScrapingService,
            IRateCalculationService rateCalculationService,
            ICentralFinancialService centralFinancialService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
            _rateCalc = rateCalculationService;
            _centralFinancialService = centralFinancialService;
        }

        public async Task SeedDataAsync()
        {
            try
            {
                // Create roles
                await CreateRolesAsync();

                // Create admin user
                await CreateAdminUserAsync();

                await CreatCurencies(); // usd ,toman , AED,OMR,EURO,LiRA
                
                 // Initialize currency pools first (required for financial operations)
                await SeedCurrencyPoolsAsync();

                // Seed exchange rates first
                await SeedExchangeRatesAsync();

                await CreateSystemCustomerAsync();

                // Create bank accounts for system customer (one per currency)
                await SeedSystemBankAccountsAsync();

                // // Seed 5-10 test customers (as requested by user)
                // await SeedTestCustomersAsync();


                // // Initialize customer balances using CentralFinancialService (with complete audit trail)
                // await SeedCustomerBalancesAsync();

                // // Seed 20-30 orders per customer using CentralFinancialService (dual-currency impact + history)
                // await SeedCustomerOrdersAsync();

                // // Seed 20-30 accounting documents per customer using CentralFinancialService (proper balance updates + history)
                // await SeedCustomerAccountingDocumentsAsync();

                _logger.LogInformation("Data seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding data");
                throw;
            }
        }

        /// <summary>
        /// Seed default currencies if missing and enforce a single base currency (IRR)
        /// </summary>
        private async Task CreatCurencies()
        {


            if (await _context.Currencies.AnyAsync())
            {
                _logger.LogInformation("Currecnies already exist, skipping seeding");
                return;
            }

            // Desired defaults
            var now = DateTime.Now;
            var defaults = new List<Currency>
            {
                new() { Code = "IRR", Name = "Iranian Toman", PersianName = "تومان", Symbol = "﷼", IsActive = true, IsBaseCurrency = true, DisplayOrder = 1, CreatedAt = now },
                new() { Code = "OMR", Name = "Omani Rial", PersianName = "ریال عمان", Symbol = "ر.ع.", IsActive = true, IsBaseCurrency = false, DisplayOrder = 2, CreatedAt = now },
                new() { Code = "AED", Name = "UAE Dirham", PersianName = "درهم امارات", Symbol = "د.إ", IsActive = true, IsBaseCurrency = false, DisplayOrder = 3, CreatedAt = now },
                new() { Code = "USD", Name = "US Dollar", PersianName = "دلار آمریکا", Symbol = "$", IsActive = true, IsBaseCurrency = false, DisplayOrder = 4, CreatedAt = now },
                new() { Code = "EUR", Name = "Euro", PersianName = "یورو", Symbol = "€", IsActive = true, IsBaseCurrency = false, DisplayOrder = 5, CreatedAt = now },
                new() { Code = "TRY", Name = "Turkish Lira", PersianName = "لیر ترکیه", Symbol = "₺", IsActive = true, IsBaseCurrency = false, DisplayOrder = 6, CreatedAt = now },
                new() { Code = "CNY", Name = "Chinese Yuan", PersianName = "یوان چین", Symbol = "¥", IsActive = true, IsBaseCurrency = false, DisplayOrder = 7, CreatedAt = now },
            };

            _context.Currencies.AddRange(defaults);
            await _context.SaveChangesAsync();






        }

        private async Task CreateRolesAsync()
        {
            var roles = new[] { "Admin", "Customer", "Staff" };

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                    _logger.LogInformation($"Role '{role}' created successfully");
                }
            }
        }

        private async Task CreateAdminUserAsync()
        {
            // Define multiple admin users with their phone numbers as passwords
            var adminUsers = new[]
            {
                new { Phone = "00989120674032", Email = "siavash@taban-gorpup.com", FullName = "سیاوش", Password = "09120674032" },
                new { Phone = "00989391377624", Email = "elahe@taban-gorpup.com", FullName = "الهه", Password = "09391377624" },
                new { Phone = "00989194810612", Email = "behnam@taban-gorpup.com", FullName = "بهنام", Password = "09194810612" }
            };

            foreach (var adminData in adminUsers)
            {
                // Check if admin exists by email first (for existing installations)
                var adminUser = await _userManager.FindByEmailAsync(adminData.Email);

                // If found with email username, update it to use phone number
                if (adminUser != null && adminUser.UserName == adminData.Email)
                {
                    adminUser.UserName = adminData.Phone;
                    var updateResult = await _userManager.UpdateAsync(adminUser);
                    if (updateResult.Succeeded)
                    {
                        _logger.LogInformation($"Updated existing admin user to use phone number as username: {adminData.Phone}");
                    }
                    continue;
                }

                // Check if admin exists by phone number
                if (adminUser == null)
                {
                    adminUser = await _userManager.FindByNameAsync(adminData.Phone);
                }

                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = adminData.Phone, // Use phone as username for login
                        Email = adminData.Email,
                        FullName = adminData.FullName,
                        Role = UserRole.Admin,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true,
                        PhoneNumber = adminData.Phone
                    };

                    var result = await _userManager.CreateAsync(adminUser, adminData.Password);

                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(adminUser, "Admin");
                        _logger.LogInformation($"Admin user created successfully with username: {adminData.Phone} and email: {adminData.Email}");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError($"Failed to create admin user {adminData.Phone}: {errors}");
                    }
                }
                else
                {
                    _logger.LogInformation($"Admin user {adminData.Phone} already exists");
                }
            }
        }

        /// <summary>
        /// Create system customer for exchange operations
        /// ایجاد مشتری سیستم برای عملیات صرافی
        /// </summary>
        private async Task CreateSystemCustomerAsync()
        {
            var systemCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.IsSystem);

            if (systemCustomer == null)
            {
                systemCustomer = new Customer
                {
                    FullName = "ISSystem",
                    PhoneNumber = "0000000000",
                    Email = "system@exchange.local",
                    NationalId = "0000000000",
                    Address = "سیستم داخلی",
                    IsActive = true,
                    IsSystem = true,
                    CreatedAt = DateTime.Now
                };

                _context.Customers.Add(systemCustomer);
                await _context.SaveChangesAsync();


                _logger.LogInformation("System customer created successfully");
            }
            else
            {
                _logger.LogInformation("System customer already exists");
            }
        }

        /// <summary>
        /// Create bank accounts for system customer - one per active currency
        /// ایجاد حساب‌های بانکی برای مشتری سیستم - یکی برای هر ارز فعال
        /// </summary>
        private async Task SeedSystemBankAccountsAsync()
        {
            try
            {
                var systemCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.IsSystem);
                if (systemCustomer == null)
                {
                    _logger.LogError("System customer not found for bank account seeding");
                    return;
                }

                // Check if system bank accounts already exist
                var existingSystemBankAccounts = await _context.BankAccounts
                    .Where(ba => ba.CustomerId == systemCustomer.Id)
                    .CountAsync();

                if (existingSystemBankAccounts > 0)
                {
                    _logger.LogInformation($"{existingSystemBankAccounts} system bank accounts already exist, skipping bank account seeding");
                    return;
                }

                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .ToListAsync();

                var random = new Random();
                var bankNames = new[] { "بانک ملی", "بانک صادرات", "بانک تجارت", "بانک کشاورزی", "بانک پارسیان", "بانک پاسارگاد" };
                var totalAccountsCreated = 0;

                _logger.LogInformation($"Creating system bank accounts for {currencies.Count} active currencies");

                foreach (var currency in currencies)
                {
                    var bankName = bankNames[random.Next(bankNames.Length)];
                    var accountNumber = $"SYS{currency.Code}{random.Next(100000, 999999)}";
                    var iban = $"IR{random.Next(10, 99)}{random.Next(1000, 9999)}{random.Next(100000000, 999999999)}";
                    var initialBalance = (decimal)(random.NextDouble() * 500000 + 100000); // Random balance 100K-600K

                    var bankAccount = new BankAccount
                    {
                        CustomerId = systemCustomer.Id,
                        BankName = bankName,
                        AccountNumber = accountNumber,
                        AccountHolderName = $"سیستم صرافی - {currency.Name}",
                        IBAN = iban,
                        Branch = "شعبه مرکزی",
                        CurrencyCode = currency.Code,
                        IsActive = true,
                        IsDefault = currency.IsBaseCurrency, // Make base currency account default
                        AccountBalance = Math.Round(initialBalance, 2),
                        CreatedAt = DateTime.Now,
                        Notes = $"حساب سیستم برای ارز {currency.Name} - ایجاد شده توسط DataSeedService"
                    };

                    _context.BankAccounts.Add(bankAccount);
                    totalAccountsCreated++;

                    _logger.LogInformation($"Created system bank account for {currency.Code}: {accountNumber} with balance {initialBalance:N2}");
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully created {totalAccountsCreated} system bank accounts");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed system bank accounts");
                throw;
            }
        }

        private async Task SeedExchangeRatesAsync()
        {
            try
            {
                var rates = new Dictionary<string, decimal>();
                var currencies = await _context.Currencies
                    .Where(c => c.IsActive && !c.IsBaseCurrency)
                    .ToListAsync();

                foreach (var currency in currencies)
                {
                    var rateResult = await _webScrapingService.GetCurrencyRateAsync(currency.Code);
                    if (rateResult.HasValue)
                    {
                        rates[currency.Code] = rateResult.Value;
                    }
                }

                var exchangeRates = new List<ExchangeRate>();
                var baseCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.IsBaseCurrency);
                if (baseCurrency == null)
                {
                    _logger.LogError("Base currency not found in database");
                    return;
                }

                // Map: currencyId -> rate for currency->base
                var scrapedMap = new Dictionary<int, decimal>();
                foreach (var rate in rates)
                {
                    var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == rate.Key);
                    if (currency == null) { _logger.LogWarning($"Currency {rate.Key} not found in database, skipping"); continue; }

                    // currency -> base (X->IRR)
                    var existingRate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrencyId == currency.Id && r.ToCurrencyId == baseCurrency.Id && r.IsActive);
                    if (existingRate == null)
                    {
                        exchangeRates.Add(new ExchangeRate
                        {
                            FromCurrencyId = currency.Id,
                            ToCurrencyId = baseCurrency.Id,
                            Rate = _rateCalc.SafeRound(rate.Value, 4),
                            IsActive = true,
                            UpdatedAt = DateTime.Now,
                            UpdatedBy = "WebScraping-System"
                        });
                    }
                    scrapedMap[currency.Id] = rate.Value;

                    // base -> currency (IRR->X) reverse
                    var rev = scrapedMap[currency.Id] > 0 ? 1.0m / scrapedMap[currency.Id] : 0;
                    if (rev > 0)
                    {
                        var existingRev = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrencyId == baseCurrency.Id && r.ToCurrencyId == currency.Id && r.IsActive);
                        if (existingRev == null)
                        {
                            exchangeRates.Add(new ExchangeRate
                            {
                                FromCurrencyId = baseCurrency.Id,
                                ToCurrencyId = currency.Id,
                                Rate = _rateCalc.SafeRound(rev, 8),
                                IsActive = true,
                                UpdatedAt = DateTime.Now,
                                UpdatedBy = "WebScraping-System"
                            });
                        }
                    }
                }

                // Cross pairs among all non-base currencies using IRR as pivot
                var foreignIds = scrapedMap.Keys.ToList();
                for (int i = 0; i < foreignIds.Count; i++)
                {
                    for (int j = 0; j < foreignIds.Count; j++)
                    {
                        if (i == j) continue;
                        var fromId = foreignIds[i];
                        var toId = foreignIds[j];
                        var fromRate = scrapedMap[fromId];
                        var toRate = scrapedMap[toId];
                        if (fromRate > 0 && toRate > 0)
                        {
                            var cross = toRate / fromRate;
                            var existingCross = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrencyId == fromId && r.ToCurrencyId == toId && r.IsActive);
                            if (existingCross == null)
                            {
                                exchangeRates.Add(new ExchangeRate
                                {
                                    FromCurrencyId = fromId,
                                    ToCurrencyId = toId,
                                    Rate = _rateCalc.SafeRound(cross, 8),
                                    IsActive = true,
                                    UpdatedAt = DateTime.Now,
                                    UpdatedBy = "WebScraping-System"
                                });
                            }
                        }
                    }
                }

                if (exchangeRates.Any())
                {
                    _context.ExchangeRates.AddRange(exchangeRates);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully seeded {exchangeRates.Count} exchange rates from web scraping");
                }
                else
                {
                    _logger.LogWarning("No rates received from web scraping, but ForexDbContext will handle rate seeding");
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get rates from web scraping, but ForexDbContext will handle rate seeding");
            }
        }

        /// <summary>
        /// Seed 5-10 test customers (as requested by user)
        /// </summary>
        private async Task SeedTestCustomersAsync()
        {
            try
            {
                // Check if customers already exist (excluding system customer)
                var existingCustomerCount = await _context.Customers
                    .Where(c => !c.IsSystem)
                    .CountAsync();

                if (existingCustomerCount >= 5)
                {
                    _logger.LogInformation($"{existingCustomerCount} customers already exist, skipping customer seeding");
                    return;
                }

                var random = new Random();
                var persianFirstNames = new[] { "علی", "محمد", "حسن", "حسین", "احمد", "مهدی", "رضا", "امیر", "سعید", "محسن", "فاطمه", "زهرا", "مریم", "آیدا", "نرگس", "پریسا", "سارا", "نازنین", "مینا", "شیما" };
                var persianLastNames = new[] { "احمدی", "محمدی", "حسینی", "رضایی", "موسوی", "کریمی", "حسنی", "صادقی", "مرادی", "علوی", "قاسمی", "بابایی", "نوری", "صالحی", "طاهری", "کاظمی", "جعفری", "رحیمی", "فروغی", "کامرانی" };

                var customers = new List<Customer>();
                var now = DateTime.Now;

                // Create 5-10 customers (random number)
                var customerCount = random.Next(5, 11);
                _logger.LogInformation($"Creating {customerCount} test customers using CentralFinancialService");

                for (int i = 1; i <= customerCount; i++)
                {
                    var firstName = persianFirstNames[random.Next(persianFirstNames.Length)];
                    var lastName = persianLastNames[random.Next(persianLastNames.Length)];

                    var customer = new Customer
                    {
                        FullName = $"{firstName} {lastName}",
                        PhoneNumber = $"0912{random.Next(1000000, 9999999)}",
                        Email = $"customer{i}@test.com",
                        NationalId = $"{random.Next(100, 999)}{random.Next(100000, 999999)}{random.Next(100, 999)}",
                        Address = $"تهران، خیابان {random.Next(1, 50)}، پلاک {random.Next(1, 200)}",
                        IsActive = true,
                        IsSystem = false,
                        CreatedAt = now.AddDays(-random.Next(1, 365)) // Random creation date within last year
                    };

                    customers.Add(customer);
                }

                _context.Customers.AddRange(customers);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created {customerCount} test customers successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding test customers");
                throw;
            }
        }

        /// <summary>
        /// Initialize currency pools with starting balances
        /// </summary>
        private async Task SeedCurrencyPoolsAsync()
        {
            try
            {
                var existingPools = await _context.CurrencyPools.CountAsync();
                if (existingPools > 0)
                {
                    _logger.LogInformation($"{existingPools} currency pools already exist, skipping pool seeding");
                    return;
                }

                var currencies = await _context.Currencies
                    .Where(c => c.IsActive && !c.IsBaseCurrency) // Don't create pool for base currency (IRR)
                    .ToListAsync();

                var random = new Random();
                var pools = new List<CurrencyPool>();

                foreach (var currency in currencies)
                {
                    var pool = new CurrencyPool
                    {
                        CurrencyId = currency.Id,
                        CurrencyCode = currency.Code,
                        Balance = random.Next(50000, 200000), // Starting balance 50K-200K
                        TotalBought = 0,
                        TotalSold = 0,
                        ActiveBuyOrderCount = 0,
                        ActiveSellOrderCount = 0,
                        RiskLevel = PoolRiskLevel.Low,
                        IsActive = true,
                        LastUpdated = DateTime.UtcNow,
                        Notes = $"Initial pool created by DataSeedService for {currency.Name}"
                    };
                    pools.Add(pool);
                }

                _context.CurrencyPools.AddRange(pools);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created {pools.Count} currency pools successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding currency pools");
                throw;
            }
        }

        /// <summary>
        /// Seed 20-30 orders per customer using CentralFinancialService
        /// </summary>
        private async Task SeedCustomerOrdersAsync()
        {
            try
            {
                // Check if orders already exist
                var existingOrderCount = await _context.Orders.CountAsync();
                if (existingOrderCount > 50)
                {
                    _logger.LogInformation($"{existingOrderCount} orders already exist, skipping order seeding");
                    return;
                }

                var customers = await _context.Customers
                    .Where(c => !c.IsSystem)
                    .Include(c => c.Orders) // Include existing orders
                    .ToListAsync();

                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .ToListAsync();

                var exchangeRates = await _context.ExchangeRates
                    .Where(er => er.IsActive)
                    .ToListAsync();

                var random = new Random();
                var now = DateTime.Now;
                var totalOrdersCreated = 0;

                _logger.LogInformation($"Creating 20-30 orders per customer using CentralFinancialService");

                foreach (var customer in customers)
                {
                    var orderCount = random.Next(20, 31); // 20-30 orders per customer

                    for (int i = 0; i < orderCount; i++)
                    {
                        var fromCurrency = currencies[random.Next(currencies.Count)];
                        var toCurrency = currencies[random.Next(currencies.Count)];

                        // Make sure from and to currencies are different
                        while (toCurrency.Id == fromCurrency.Id)
                        {
                            toCurrency = currencies[random.Next(currencies.Count)];
                        }

                        var amount = (decimal)(random.NextDouble() * 5000 + 100); // Random amount between 100-5100

                        // Find exchange rate or calculate reasonable rate
                        var rate = exchangeRates.FirstOrDefault(er =>
                            er.FromCurrencyId == fromCurrency.Id && er.ToCurrencyId == toCurrency.Id);

                        var exchangeRate = rate?.Rate ?? (decimal)(random.NextDouble() * 2 + 0.5); // Random rate if not found
                        var totalAmount = amount * exchangeRate;

                        // Create order entity
                        var order = new Order
                        {
                            CustomerId = customer.Id,
                            FromCurrencyId = fromCurrency.Id,
                            ToCurrencyId = toCurrency.Id,
                            FromCurrency = fromCurrency,
                            ToCurrency = toCurrency,
                            FromAmount = Math.Round(amount, 2),
                            Rate = exchangeRate,
                            ToAmount = Math.Round(totalAmount, 2),
                            CreatedAt = now.AddDays(-random.Next(1, 365)), // Random date within last year
                            Notes = $"Test order created by DataSeedService"
                        };

                        // Save order first
                        _context.Orders.Add(order);
                        await _context.SaveChangesAsync();

                        // Process order creation using CentralFinancialService for dual-currency impact
                        await _centralFinancialService.ProcessOrderCreationAsync(order, "DataSeedService");

                        totalOrdersCreated++;


                    }

                    _logger.LogInformation($"Created {orderCount} orders for customer {customer.FullName}");
                }

                _logger.LogInformation($"Successfully created {totalOrdersCreated} total orders using CentralFinancialService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed customer orders");
                throw;
            }
        }

        /// <summary>
        /// Seed 20-30 accounting documents per customer using CentralFinancialService
        /// </summary>
        private async Task SeedCustomerAccountingDocumentsAsync()
        {
            try
            {
                // Check if documents already exist
                var existingDocCount = await _context.AccountingDocuments.CountAsync();
                if (existingDocCount > 50)
                {
                    _logger.LogInformation($"{existingDocCount} accounting documents already exist, skipping document seeding");
                    return;
                }

                var customers = await _context.Customers.Where(c => !c.IsSystem).ToListAsync();
                var systemCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.IsSystem);
                var currencies = await _context.Currencies.Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

                if (systemCustomer == null)
                {
                    _logger.LogError("System customer not found");
                    return;
                }

                // Load system bank accounts (one per currency)
                var systemBankAccounts = await _context.BankAccounts
                    .Where(ba => ba.CustomerId == systemCustomer.Id && ba.IsActive)
                    .ToListAsync();

                var random = new Random();
                var now = DateTime.Now;
                var totalDocumentsCreated = 0;

                var descriptions = new[] {
                    "واریز نقدی", "برداشت نقدی", "تبدیل ارز", "کارمزد معامله",
                    "واریز بانکی", "برداشت بانکی", "تسویه حساب", "پرداخت کمیسیون",
                    "انتقال وجه", "دریافت حواله", "پرداخت حواله", "سود سپرده"
                };

                _logger.LogInformation($"Creating 20-30 accounting documents per customer using CentralFinancialService");

                foreach (var customer in customers)
                {
                    var docCount = random.Next(20, 31); // 20-30 documents per customer

                    for (int i = 0; i < docCount; i++)
                    {
                        var currency = currencies[random.Next(currencies.Count)];
                        var amount = (decimal)(random.NextDouble() * 3000 + 50); // Random amount between 50-3050
                        var isPayment = random.NextDouble() > 0.5; // 50% payments, 50% receipts
                        var description = descriptions[random.Next(descriptions.Length)];

                        // Create accounting document entity
                        var document = new AccountingDocument
                        {
                            Type = random.NextDouble() > 0.5 ? DocumentType.Cash : DocumentType.Havala,
                            Title = description,
                            Description = $"Test document created by DataSeedService - {description}",
                            Amount = Math.Round(amount, 2),
                            CurrencyCode = currency.Code,
                            DocumentDate = now.AddDays(-random.Next(1, 365)), // Random date within last year
                            CreatedAt = DateTime.UtcNow,
                            IsVerified = true, // Auto-verify for testing
                            VerifiedAt = DateTime.UtcNow,
                            VerifiedBy = "DataSeedService",
                            Notes = "Generated by DataSeedService for testing",
                            ReferenceNumber = $"REF{DateTime.UtcNow.Ticks}{random.Next(1000, 9999)}"
                        };

                        // Set payer and receiver based on transaction type
                        if (isPayment)
                        {
                            // Customer pays to system
                            document.PayerCustomerId = customer.Id;
                            document.PayerType = PayerType.Customer;
                            document.ReceiverCustomerId = systemCustomer.Id;
                            document.ReceiverType = ReceiverType.Customer; // System customer is still a Customer type
                        }
                        else
                        {
                            // System pays to customer  
                            document.PayerCustomerId = systemCustomer.Id;
                            document.PayerType = PayerType.Customer; // System customer is still a Customer type
                            document.ReceiverCustomerId = customer.Id;
                            document.ReceiverType = ReceiverType.Customer;
                        }

                        // Assign system bank account based on transaction currency and direction
                        var systemBankAccount = systemBankAccounts.FirstOrDefault(ba => ba.CurrencyCode == currency.Code);
                        if (systemBankAccount != null)
                        {
                            if (isPayment)
                            {
                                // Customer pays to system - system receives to bank account
                                document.ReceiverBankAccountId = systemBankAccount.Id;
                                _logger.LogDebug($"Assigned system bank account {systemBankAccount.AccountNumber} ({currency.Code}) as receiver for document {document.ReferenceNumber}");
                            }
                            else
                            {
                                // System pays to customer - system pays from bank account
                                document.PayerBankAccountId = systemBankAccount.Id;
                                _logger.LogDebug($"Assigned system bank account {systemBankAccount.AccountNumber} ({currency.Code}) as payer for document {document.ReferenceNumber}");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"No system bank account found for currency {currency.Code} - document {document.ReferenceNumber} will not have bank account assignment");
                        }

                        // Save document first
                        _context.AccountingDocuments.Add(document);
                        await _context.SaveChangesAsync();

                        // Process document using CentralFinancialService for proper balance updates
                        await _centralFinancialService.ProcessAccountingDocumentAsync(document, "DataSeedService");

                        totalDocumentsCreated++;
                    }

                    _logger.LogInformation($"Created {docCount} accounting documents for customer {customer.FullName}");
                }

                _logger.LogInformation($"Successfully created {totalDocumentsCreated} total accounting documents using CentralFinancialService");
                _logger.LogInformation($"System bank accounts used: {systemBankAccounts.Count} accounts covering currencies: {string.Join(", ", systemBankAccounts.Select(ba => ba.CurrencyCode))}");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed customer accounting documents");
                throw;
            }
        }

        /// <summary>
        /// Initialize customer balances using CentralFinancialService for proper audit trail
        /// </summary>
        private async Task SeedCustomerBalancesAsync()
        {
            try
            {
                // Check if balances already exist
                var existingBalanceCount = await _context.CustomerBalances.CountAsync();
                if (existingBalanceCount > 0)
                {
                    _logger.LogInformation($"{existingBalanceCount} customer balances already exist, skipping balance seeding");
                    return;
                }

                var customers = await _context.Customers
                    .Where(c => !c.IsSystem)
                    .ToListAsync();

                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .ToListAsync();

                var random = new Random();
                var totalBalancesCreated = 0;

                _logger.LogInformation($"Creating initial customer balances using CentralFinancialService");

                foreach (var customer in customers)
                {
                    // Give each customer random balances in 2-4 currencies
                    var currencyCount = random.Next(2, 5);
                    var selectedCurrencies = currencies.OrderBy(x => random.Next()).Take(currencyCount);

                    foreach (var currency in selectedCurrencies)
                    {
                        var amount = (decimal)(random.NextDouble() * 30000 + 1000); // Random amount between 1000-31000

                        // Use CentralFinancialService to create initial balance with audit trail
                        await _centralFinancialService.AdjustCustomerBalanceAsync(
                            customerId: customer.Id,
                            currencyCode: currency.Code,
                            adjustmentAmount: Math.Round(amount, 2),
                            reason: $"Initial balance created by DataSeedService for {currency.Name}",
                            performedBy: "DataSeedService"
                        );

                        totalBalancesCreated++;
                    }

                    _logger.LogInformation($"Created initial balances for customer {customer.FullName}");
                }

                _logger.LogInformation($"Successfully created {totalBalancesCreated} initial customer balances using CentralFinancialService");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed customer balances");
                throw;
            }
        }
    }
}


