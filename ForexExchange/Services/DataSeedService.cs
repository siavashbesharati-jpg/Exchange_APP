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

        public DataSeedService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ForexDbContext context,
            ILogger<DataSeedService> logger,
            IWebScrapingService webScrapingService,
            IRateCalculationService rateCalculationService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
            _rateCalc = rateCalculationService;
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

                // Seed exchange rates first
                await SeedExchangeRatesAsync();

                await CreateSystemCustomerAsync();

                // Seed 50 test customers
                await SeedTestCustomersAsync();

                // Initialize customer balances
                await SeedCustomerBalancesAsync();

                // Seed orders for customers (and update balances)
                await SeedCustomerOrdersAsync();

                // Seed accounting documents for customers (and update balances)
                await SeedCustomerAccountingDocumentsAsync();

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
                new() { Code = "IRR", Name = "Iranian Rial", PersianName = "تومان", Symbol = "﷼", IsActive = true, IsBaseCurrency = true, DisplayOrder = 1, CreatedAt = now },
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
        /// Seed 50 test customers
        /// </summary>
        private async Task SeedTestCustomersAsync()
        {
            try
            {
                // Check if customers already exist (excluding system customer)
                var existingCustomerCount = await _context.Customers
                    .Where(c => !c.IsSystem)
                    .CountAsync();

                if (existingCustomerCount >= 50)
                {
                    _logger.LogInformation($"{existingCustomerCount} customers already exist, skipping customer seeding");
                    return;
                }

                var random = new Random();
                var persianFirstNames = new[] { "علی", "محمد", "حسن", "حسین", "احمد", "مهدی", "رضا", "امیر", "سعید", "محسن", "فاطمه", "زهرا", "مریم", "آیدا", "نرگس", "پریسا", "سارا", "نازنین", "مینا", "شیما" };
                var persianLastNames = new[] { "احمدی", "محمدی", "حسینی", "رضایی", "موسوی", "کریمی", "حسنی", "صادقی", "مرادی", "علوی", "قاسمی", "بابایی", "نوری", "صالحی", "طاهری", "کاظمی", "جعفری", "رحیمی", "فروغی", "کامرانی" };
                
                var customers = new List<Customer>();
                var now = DateTime.Now;

                for (int i = 1; i <= 50; i++)
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

                _logger.LogInformation($"Successfully created {customers.Count} test customers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed test customers");
                throw;
            }
        }

        /// <summary>
        /// Seed 50-60 orders for each customer
        /// </summary>
        private async Task SeedCustomerOrdersAsync()
        {
            try
            {
                // Check if orders already exist
                var existingOrderCount = await _context.Orders.CountAsync();
                if (existingOrderCount > 100)
                {
                    _logger.LogInformation($"{existingOrderCount} orders already exist, skipping order seeding");
                    return;
                }

                var customers = await _context.Customers
                    .Where(c => !c.IsSystem)
                    .ToListAsync();

                var currencies = await _context.Currencies
                    .Where(c => c.IsActive)
                    .ToListAsync();

                var exchangeRates = await _context.ExchangeRates
                    .Where(er => er.IsActive)
                    .ToListAsync();

                var random = new Random();
                var orders = new List<Order>();
                var now = DateTime.Now;

                foreach (var customer in customers)
                {
                    var orderCount = random.Next(50, 61); // 50-60 orders per customer

                    for (int i = 0; i < orderCount; i++)
                    {
                        var fromCurrency = currencies[random.Next(currencies.Count)];
                        var toCurrency = currencies[random.Next(currencies.Count)];
                        
                        // Make sure from and to currencies are different
                        while (toCurrency.Id == fromCurrency.Id)
                        {
                            toCurrency = currencies[random.Next(currencies.Count)];
                        }

                        var amount = (decimal)(random.NextDouble() * 10000 + 100); // Random amount between 100-10100

                        // Find exchange rate
                        var rate = exchangeRates.FirstOrDefault(er => 
                            er.FromCurrencyId == fromCurrency.Id && er.ToCurrencyId == toCurrency.Id);
                        
                        var totalAmount = rate != null ? amount * rate.Rate : amount * 1.1m;

                        var isCompleted = random.NextDouble() > 0.3; // 70% completion rate
                        var filledAmount = isCompleted ? Math.Round(amount, 2) : Math.Round(amount * (decimal)random.NextDouble(), 2);

                        var order = new Order
                        {
                            CustomerId = customer.Id,
                            FromCurrencyId = fromCurrency.Id,
                            ToCurrencyId = toCurrency.Id,
                            Amount = Math.Round(amount, 2),
                            Rate = rate?.Rate ?? 1.1m,
                            TotalAmount = Math.Round(totalAmount, 2),
                            FilledAmount = filledAmount,
                            CreatedAt = now.AddDays(-random.Next(1, 365)) // Random date within last year
                        };

                        orders.Add(order);

                        // Update balances for completed orders
                        if (isCompleted && filledAmount > 0)
                        {
                            // Deduct from source currency
                            await UpdateCustomerBalanceAsync(customer.Id, fromCurrency.Code, -filledAmount, order.CreatedAt);
                            
                            // Add to target currency
                            var receivedAmount = Math.Round(filledAmount * order.Rate, 2);
                            await UpdateCustomerBalanceAsync(customer.Id, toCurrency.Code, receivedAmount, order.CreatedAt);
                        }
                    }
                }

                // Add all orders to context first
                _context.Orders.AddRange(orders);
                
                // Save all changes (orders and balance updates) together
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully created {orders.Count} orders for customers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed customer orders");
                throw;
            }
        }

        /// <summary>
        /// Seed 50-60 accounting documents for each customer
        /// </summary>
        private async Task SeedCustomerAccountingDocumentsAsync()
        {
            try
            {
                // Check if documents already exist
                var existingDocCount = await _context.AccountingDocuments.CountAsync();
                if (existingDocCount > 100)
                {
                    _logger.LogInformation($"{existingDocCount} accounting documents already exist, skipping document seeding");
                    return;
                }

                var customers = await _context.Customers.Where(c => !c.IsSystem).ToListAsync();
                var systemCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.IsSystem);
                var currencies = await _context.Currencies.Where(c => c.IsActive).ToListAsync();

                if (systemCustomer == null)
                {
                    _logger.LogError("System customer not found");
                    return;
                }

                var random = new Random();
                var documents = new List<AccountingDocument>();
                var now = DateTime.Now;

                var descriptions = new[] { 
                    "واریز نقدی", "برداشت نقدی", "تبدیل ارز", "کارمزد معامله", 
                    "واریز بانکی", "برداشت بانکی", "تسویه حساب", "پرداخت کمیسیون",
                    "انتقال وجه", "دریافت حواله", "پرداخت حواله", "سود سپرده"
                };

                foreach (var customer in customers)
                {
                    var docCount = random.Next(50, 61); // 50-60 documents per customer

                    for (int i = 0; i < docCount; i++)
                    {
                        var currency = currencies[random.Next(currencies.Count)];
                        var amount = (decimal)(random.NextDouble() * 5000 + 50); // Random amount between 50-5050
                        var isPayment = random.NextDouble() > 0.5; // 50% payments, 50% receipts

                        var isVerified = random.NextDouble() > 0.1; // 90% verified

                        var document = new AccountingDocument
                        {
                            Type = (DocumentType)random.Next(0, 3), // Cash, BankStatement, or Havala
                            PayerType = isPayment ? PayerType.Customer : PayerType.System,
                            PayerCustomerId = isPayment ? customer.Id : systemCustomer.Id,
                            ReceiverType = isPayment ? ReceiverType.System : ReceiverType.Customer,
                            ReceiverCustomerId = isPayment ? systemCustomer.Id : customer.Id,
                            Amount = Math.Round(amount, 2),
                            CurrencyCode = currency.Code,
                            Description = descriptions[random.Next(descriptions.Length)],
                            DocumentDate = now.AddDays(-random.Next(1, 365)), // Random date within last year
                            CreatedAt = now.AddDays(-random.Next(1, 365)),
                            IsVerified = isVerified,
                            VerifiedAt = isVerified ? now.AddDays(-random.Next(1, 300)) : null,
                            VerifiedBy = isVerified ? "System" : null
                        };

                        documents.Add(document);

                        // Update customer balance for verified documents
                        if (isVerified)
                        {
                            if (isPayment)
                            {
                                // Customer is paying - deduct from balance
                                await UpdateCustomerBalanceAsync(customer.Id, currency.Code, -document.Amount, document.DocumentDate);
                            }
                            else
                            {
                                // Customer is receiving - add to balance
                                await UpdateCustomerBalanceAsync(customer.Id, currency.Code, document.Amount, document.DocumentDate);
                            }
                        }
                    }
                }

                // Add all documents to context first
                _context.AccountingDocuments.AddRange(documents);
                
                // Save all changes (documents and balance updates) together
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully created {documents.Count} accounting documents for customers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed customer accounting documents");
                throw;
            }
        }

        /// <summary>
        /// Initialize customer balances with random amounts
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
                var balances = new List<CustomerBalance>();
                var now = DateTime.Now;

                foreach (var customer in customers)
                {
                    // Give each customer random balances in 2-4 currencies
                    var currencyCount = random.Next(2, 5);
                    var selectedCurrencies = currencies.OrderBy(x => random.Next()).Take(currencyCount);

                    foreach (var currency in selectedCurrencies)
                    {
                        var amount = (decimal)(random.NextDouble() * 50000 + 1000); // Random amount between 1000-51000

                        var balance = new CustomerBalance
                        {
                            CustomerId = customer.Id,
                            CurrencyCode = currency.Code,
                            Balance = Math.Round(amount, 2),
                            LastUpdated = now.AddDays(-random.Next(1, 30)) // Updated within last month
                        };

                        balances.Add(balance);
                    }
                }

                _context.CustomerBalances.AddRange(balances);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully created {balances.Count} customer balances");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed customer balances");
                throw;
            }
        }

        /// <summary>
        /// Helper method to update customer balance
        /// </summary>
        private async Task UpdateCustomerBalanceAsync(int customerId, string currencyCode, decimal amount, DateTime transactionDate)
        {
            var balance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

            if (balance == null)
            {
                // Create new balance if doesn't exist
                balance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    Balance = amount,
                    LastUpdated = transactionDate
                };
                _context.CustomerBalances.Add(balance);
            }
            else
            {
                // Update existing balance
                balance.Balance += amount;
                balance.LastUpdated = transactionDate;
            }
        }

    }
}


