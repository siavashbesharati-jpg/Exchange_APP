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

                 await SeedTestCustomersAsync();

                // Seed default system settings including branding
                await SeedDefaultSettingsAsync();

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
                new() { Code = "IRR", Name = "Iranian Toman", PersianName = "تومان", Symbol = "تومان", IsActive = true, DisplayOrder = 1, CreatedAt = now ,RatePriority = 8},
                new() { Code = "OMR", Name = "Omani Rial", PersianName = "ریال عمان", Symbol = "ر.ع.", IsActive = true, DisplayOrder = 2, CreatedAt = now ,RatePriority = 1},
                new() { Code = "AED", Name = "UAE Dirham", PersianName = "درهم امارات", Symbol = "د.إ", IsActive = true, DisplayOrder = 3, CreatedAt = now ,RatePriority = 4},
                new() { Code = "USD", Name = "US Dollar", PersianName = "دلار آمریکا", Symbol = "$", IsActive = true, DisplayOrder = 4, CreatedAt = now,RatePriority = 3},
                new() { Code = "EUR", Name = "Euro", PersianName = "یورو", Symbol = "€", IsActive = true, DisplayOrder = 5, CreatedAt = now ,RatePriority = 2},
                new() { Code = "TRY", Name = "Turkish Lira", PersianName = "لیر ترکیه", Symbol = "₺", IsActive = true, DisplayOrder = 6, CreatedAt = now ,RatePriority = 6},
                new() { Code = "CNY", Name = "Chinese Yuan", PersianName = "یوان چین", Symbol = "¥", IsActive = true, DisplayOrder = 7, CreatedAt = now, RatePriority =  5},
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
                        IsDefault = currency.Code == "IRR", // Make base currency account default
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
                // DISABLED: Web scraping for exchange rates
                // var rates = new Dictionary<string, decimal>();
                // var currencies = await _context.Currencies
                //     .Where(c => c.IsActive && c.Code != "IRR")
                //     .ToListAsync();

                // foreach (var currency in currencies)
                // {
                //     var rateResult = await _webScrapingService.GetCurrencyRateAsync(currency.Code);
                //     if (rateResult.HasValue)
                //     {
                //         rates[currency.Code] = rateResult.Value;
                //     }
                // }

                var rates = new Dictionary<string, decimal>(); // Empty rates dictionary

                var exchangeRates = new List<ExchangeRate>();
                var baseCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "IRR");
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
                
                // Persian male names
                var persianMaleNames = new[] { "علی", "محمد", "حسن", "حسین", "احمد", "مهدی", "رضا", "امیر", "سعید", "محسن", "حامد", "مسعود", "فرهاد", "بهروز", "کیوان", "آرمان", "پوریا", "آرین", "سینا", "دانیال" };
                
                // Persian female names
                var persianFemaleNames = new[] { "فاطمه", "زهرا", "مریم", "آیدا", "نرگس", "پریسا", "سارا", "نازنین", "مینا", "شیما", "الهام", "نیلوفر", "مهسا", "طاهره", "زینب", "نگار", "ریحانه", "سمیرا", "لیلا", "مهناز" };
                
                var persianLastNames = new[] { "احمدی", "محمدی", "حسینی", "رضایی", "موسوی", "کریمی", "حسنی", "صادقی", "مرادی", "علوی", "قاسمی", "بابایی", "نوری", "صالحی", "طاهری", "کاظمی", "جعفری", "رحیمی", "فروغی", "کامرانی" };

                var customers = new List<Customer>();
                var now = DateTime.Now;

                // Create 5-10 customers (random number)
                var customerCount = random.Next(5, 11);
                _logger.LogInformation($"Creating {customerCount} test customers with proper gender assignment");

                for (int i = 1; i <= customerCount; i++)
                {
                    // Randomly choose gender first
                    bool isMale = random.Next(0, 2) == 0;
                    
                    // Select appropriate name based on gender
                    string firstName;
                    if (isMale)
                    {
                        firstName = persianMaleNames[random.Next(persianMaleNames.Length)];
                    }
                    else
                    {
                        firstName = persianFemaleNames[random.Next(persianFemaleNames.Length)];
                    }
                    
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
                        Gender = isMale, // true for male, false for female
                        CreatedAt = now.AddDays(-random.Next(1, 365)) // Random creation date within last year
                    };

                    customers.Add(customer);
                }

                _context.Customers.AddRange(customers);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created {customerCount} test customers successfully with proper gender assignment");
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
                    .Where(c => c.IsActive && c.Code != "IRR") // Don't create pool for base currency (IRR)
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
        /// Seed default system settings including website branding
        /// </summary>
        private async Task SeedDefaultSettingsAsync()
        {
            try
            {
                var now = DateTime.Now;
                var settingsToAdd = new List<SystemSettings>();

                // Define all default settings with their keys
                var defaultSettingsData = new Dictionary<string, (string Value, string Description, string DataType)>
                {
                    // Website Branding Settings
                    { SettingKeys.WebsiteName, ("سامانه معاملات تابان", "نام وب‌سایت", "string") },
                    { SettingKeys.CompanyName, ("گروه تابان", "نام شرکت", "string") },
                    { SettingKeys.CompanyWebsite, ("https://taban-group.com", "وب‌سایت شرکت", "string") },
                    
                    // Financial Settings
                    { SettingKeys.CommissionRate, ("0.5", "نرخ کمیسیون پیش‌فرض (درصد)", "decimal") },
                    { SettingKeys.ExchangeFeeRate, ("0.2", "کارمزد تبدیل ارز (درصد)", "decimal") },
                    { SettingKeys.MinTransactionAmount, ("10000", "حداقل مبلغ تراکنش (تومان)", "decimal") },
                    { SettingKeys.MaxTransactionAmount, ("1000000000", "حداکثر مبلغ تراکنش (تومان)", "decimal") },
                    { SettingKeys.DailyTransactionLimit, ("5000000000", "محدودیت تراکنش روزانه (تومان)", "decimal") },
                    
                    // System Settings
                    { SettingKeys.SystemMaintenance, ("false", "حالت تعمیرات سیستم", "bool") },
                    { SettingKeys.DefaultCurrency, ("USD", "کد ارز پیش‌فرض سیستم", "string") },
                    { SettingKeys.RateUpdateInterval, ("60", "بازه بروزرسانی نرخ ارز (دقیقه)", "int") },
                    { SettingKeys.NotificationEnabled, ("true", "فعال‌سازی سیستم اعلان‌ها", "bool") },
                    { SettingKeys.BackupEnabled, ("true", "فعال‌سازی پشتیبان‌گیری خودکار", "bool") }
                };

                // Check each setting and add only if it doesn't exist
                foreach (var (settingKey, (value, description, dataType)) in defaultSettingsData)
                {
                    var existingSetting = await _context.SystemSettings
                        .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

                    if (existingSetting == null)
                    {
                        settingsToAdd.Add(new SystemSettings
                        {
                            SettingKey = settingKey,
                            SettingValue = value,
                            Description = description,
                            DataType = dataType,
                            IsActive = true,
                            CreatedAt = now,
                            UpdatedAt = now,
                            UpdatedBy = "DataSeedService"
                        });
                    }
                }

                if (settingsToAdd.Any())
                {
                    _context.SystemSettings.AddRange(settingsToAdd);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully seeded {settingsToAdd.Count} new default system settings");
                }
                else
                {
                    _logger.LogInformation("All default system settings already exist, skipping settings seeding");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while seeding default system settings");
                throw;
            }
        }

    }
}


