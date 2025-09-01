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

                // Seed sample data
                //await SeedSampleDataAsync();

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
                new { Phone = "09120674032", Email = "admin1@iranexpedia.ir", FullName = "سیاوش", Password = "09120674032" },
                new { Phone = "09391374624", Email = "admin2@iranexpedia.ir", FullName = "الهه", Password = "09391374624" },
                new { Phone = "09194810612", Email = "admin3@iranexpedia.ir", FullName = "بهنام", Password = "09194810612" }
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
                    FullName = "سیستم ",
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



        private async Task SeedSampleDataAsync()
        {
            // Check if sample data already exists
            if (await _context.Customers.AnyAsync())
            {
                _logger.LogInformation("Sample data already exists, skipping seeding");
                return;
            }

            _logger.LogInformation("Starting to seed sample data...");

            // Seed Staff Users and Customers
            await SeedStaffUsersAsync();
            await SeedCustomersAndUsersAsync();
            await _context.SaveChangesAsync(); // Save customers first

            // Seed Orders
            // await SeedOrdersAsync();
            // await _context.SaveChangesAsync(); // Save orders before transactions


            // // Seed Transactions
            // await SeedTransactionsAsync();
            // await _context.SaveChangesAsync(); // Save transactions before receipts

            // // Seed Receipts
            // await SeedReceiptsAsync();
            // await _context.SaveChangesAsync(); // Save receipts before notifications

            // // Seed Notifications
            // await SeedNotificationsAsync();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Sample data seeding completed");
        }

        private async Task
        SeedStaffUsersAsync()
        {
            var staffUsers = new[]
            {
                new { Email = "manager@iranexpedia.com", FullName = "مدیر عامل", Role = UserRole.Manager, Phone = "09121234567" },
                new { Email = "operator1@iranexpedia.com", FullName = "اپراتور اول", Role = UserRole.Operator, Phone = "09121234568" },
                new { Email = "operator2@iranexpedia.com", FullName = "اپراتور دوم", Role = UserRole.Operator, Phone = "09121234569" }
            };

            foreach (var userData in staffUsers)
            {
                var existingUser = await _userManager.FindByEmailAsync(userData.Email);
                if (existingUser == null)
                {
                    // Also check by phone number (username)
                    existingUser = await _userManager.FindByNameAsync(userData.Phone);
                }

                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = userData.Phone, // Use phone as username for consistency
                        Email = userData.Email,
                        PhoneNumber = userData.Phone,
                        FullName = userData.FullName,
                        Role = userData.Role,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user, "Staff123!");
                    if (result.Succeeded)
                    {
                        var roleName = userData.Role == UserRole.Manager ? "Admin" : "Staff";
                        await _userManager.AddToRoleAsync(user, roleName);
                        _logger.LogInformation($"Staff user created: {userData.Phone} ({userData.FullName})");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError($"Failed to create staff user {userData.Phone}: {errors}");
                    }
                }
            }
        }

        private async Task SeedCustomersAndUsersAsync()
        {
            var customerData = new[]
            {
                new { FullName = "علی احمدی", Email = "ali.ahmadi@email.com", Phone = "09123456789", NationalId = "1234567890", Address = "تهران، خیابان ولیعصر، پلاک ۱۲۳" },
                new { FullName = "فاطمه محمدی", Email = "fateme.mohammadi@email.com", Phone = "09123456790", NationalId = "1234567891", Address = "اصفهان، خیابان چهارباغ، پلاک ۴۵" },
                new { FullName = "حسن کریمی", Email = "hasan.karimi@email.com", Phone = "09123456791", NationalId = "1234567892", Address = "شیراز، خیابان زند، پلاک ۶۷" },
                new { FullName = "زهرا رضایی", Email = "zahra.rezaei@email.com", Phone = "09123456792", NationalId = "1234567893", Address = "مشهد، خیابان امام رضا، پلاک ۸۹" },
                new { FullName = "محمد حسینی", Email = "mohammad.hosseini@email.com", Phone = "09123456793", NationalId = "1234567894", Address = "تبریز، خیابان ۲۹ بهمن، پلاک ۱۰۱" },
                new { FullName = "مریم علوی", Email = "maryam.alavi@email.com", Phone = "09123456794", NationalId = "1234567895", Address = "کرج، خیابان مطهری، پلاک ۱۱۲" },
                new { FullName = "امیر تقوی", Email = "amir.taghavi@email.com", Phone = "09123456795", NationalId = "1234567896", Address = "قم، خیابان انقلاب، پلاک ۱۳۴" },
                new { FullName = "نرگس باقری", Email = "narges.bagheri@email.com", Phone = "09123456796", NationalId = "1234567897", Address = "کرمان، خیابان شهید بهشتی، پلاک ۱۵۶" },
                new { FullName = "رضا موسوی", Email = "reza.mousavi@email.com", Phone = "09123456797", NationalId = "1234567898", Address = "اهواز، خیابان کیانپارس، پلاک ۱۷۸" },
                new { FullName = "سمیرا اکبری", Email = "samira.akbari@email.com", Phone = "09123456798", NationalId = "1234567899", Address = "رشت، خیابان لاکان، پلاک ۱۹۰" }
            };

            foreach (var data in customerData)
            {
                // Create Customer
                var customer = new Customer
                {
                    FullName = data.FullName,
                    Email = data.Email,
                    PhoneNumber = data.Phone,
                    NationalId = data.NationalId,
                    Address = data.Address,
                    CreatedAt = DateTime.Now.AddDays(-new Random().Next(1, 365)),
                    IsActive = true
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync(); // Save to get ID

                // Create corresponding user
                var existingUser = await _userManager.FindByEmailAsync(data.Email);
                if (existingUser == null)
                {
                    // Also check by phone number (username)
                    existingUser = await _userManager.FindByNameAsync(data.Phone);
                }

                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = data.Phone, // Use phone as username for login
                        Email = data.Email,
                        PhoneNumber = data.Phone,
                        FullName = data.FullName,
                        NationalId = data.NationalId,
                        Address = data.Address,
                        Role = UserRole.Customer,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        EmailConfirmed = true,
                        CustomerId = customer.Id
                    };

                    var result = await _userManager.CreateAsync(user, "123456"); // Changed to 6 characters
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Customer");
                        _logger.LogInformation($"Customer user created: {data.Phone} ({data.FullName})");
                    }
                    else
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        _logger.LogError($"Failed to create customer user {data.Phone}: {errors}");
                    }
                }
            }
        }

        private async Task SeedOrdersAsync()
        {
            var customers = await _context.Customers.ToListAsync();
            var exchangeRates = await _context.ExchangeRates
                .Include(r => r.FromCurrency)
                .Include(r => r.ToCurrency)
                .Where(r => r.IsActive).ToListAsync();
            var currencies = await _context.Currencies.Where(c => c.IsActive).ToListAsync();
            var baseCurrency = currencies.FirstOrDefault(c => c.IsBaseCurrency);
            var foreignCurrencies = currencies.Where(c => !c.IsBaseCurrency).ToList();
            var random = new Random();

            // Removed OrderType array

            for (int i = 0; i < 50; i++) // Create 50 sample orders
            {
                var customer = customers[random.Next(customers.Count)];
                // Removed orderType assignment
                var status = OrderStatus.Open;

                Currency fromCurrency, toCurrency;

                // Create diverse cross-currency scenarios with null checking
                if (i < 20) // 40% Base currency-based trades (traditional)
                {
                    if (baseCurrency == null)
                    {
                        // Fallback if no base currency found
                        fromCurrency = currencies.First();
                        toCurrency = foreignCurrencies.First();
                    }
                    else
                    {
                        var foreignCurrency = foreignCurrencies[random.Next(foreignCurrencies.Count)];
                        // Randomly decide direction
                        if (random.Next(2) == 0)
                        {
                            fromCurrency = baseCurrency;
                            toCurrency = foreignCurrency;
                        }
                        else
                        {
                            fromCurrency = foreignCurrency;
                            toCurrency = baseCurrency;
                        }
                    }
                }
                else if (i < 35) // 30% Cross-currency trades (USD-EUR, EUR-AED, etc.)
                {
                    var currency1 = foreignCurrencies[random.Next(foreignCurrencies.Count)];
                    var currency2 = foreignCurrencies[random.Next(foreignCurrencies.Count)];
                    while (currency2.Id == currency1.Id)
                    {
                        currency2 = foreignCurrencies[random.Next(foreignCurrencies.Count)];
                    }
                    fromCurrency = currency1;
                    toCurrency = currency2;
                }
                else // 30% Mixed scenarios including base currency as target
                {
                    fromCurrency = foreignCurrencies[random.Next(foreignCurrencies.Count)];
                    toCurrency = currencies[random.Next(currencies.Count)];
                    while (toCurrency.Id == fromCurrency.Id)
                    {
                        toCurrency = currencies[random.Next(currencies.Count)];
                    }
                }

                // Find appropriate exchange rate - prioritize direct rates, fallback to base currency conversion
                var directRate = exchangeRates.FirstOrDefault(r => r.FromCurrencyId == fromCurrency.Id && r.ToCurrencyId == toCurrency.Id);
                var reverseRate = exchangeRates.FirstOrDefault(r => r.FromCurrencyId == toCurrency.Id && r.ToCurrencyId == fromCurrency.Id);

                // For base currency conversion routes
                var fromBaseCurrencyRate = baseCurrency != null ?
                    exchangeRates.FirstOrDefault(r => r.FromCurrencyId == baseCurrency.Id && r.ToCurrencyId == fromCurrency.Id) : null;
                var toBaseCurrencyRate = baseCurrency != null ?
                    exchangeRates.FirstOrDefault(r => r.FromCurrencyId == baseCurrency.Id && r.ToCurrencyId == toCurrency.Id) : null;

                decimal rate = 1;
                if (directRate != null)
                {
                    rate = directRate.Rate;
                }
                else if (reverseRate != null)
                {
                    rate = reverseRate.Rate > 0 ? 1.0m / reverseRate.Rate : 1;
                }
                else if (fromBaseCurrencyRate != null && toBaseCurrencyRate != null && baseCurrency != null)
                {
                    rate = toBaseCurrencyRate.Rate / fromBaseCurrencyRate.Rate;
                }
                else
                {
                    // Fallback to simple conversion rates using currency codes
                    var rateMapping = new Dictionary<string, decimal>
                    {
                        { "IRR", 1 },
                        { "USD", 65000 },
                        { "EUR", 70000 },
                        { "AED", 17500 },
                        { "OMR", 168000 },
                        { "TRY", 1900 }
                    };

                    var fromRate = rateMapping.ContainsKey(fromCurrency.Code ?? "IRR") ? rateMapping[fromCurrency.Code ?? "IRR"] : 1;
                    var toRate = rateMapping.ContainsKey(toCurrency.Code ?? "IRR") ? rateMapping[toCurrency.Code ?? "IRR"] : 1;
                    rate = toRate / fromRate;
                }

                var amount = random.Next(100, 10000);
                var totalValue = amount * rate;

                var order = new Order
                {
                    CustomerId = customer.Id,
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                    Amount = amount,
                    Rate = rate,
                    TotalInToman = baseCurrency != null && fromCurrency.Id == baseCurrency.Id ? amount :
                                  (baseCurrency != null && toCurrency.Id == baseCurrency.Id ? totalValue :
                                   totalValue * 65000), // Approximate base currency value for reporting
                                                        // Removed OrderType assignment
                    Status = status,
                    CreatedAt = DateTime.Now.AddDays(-random.Next(1, 30)),
                    UpdatedAt = DateTime.Now.AddDays(-random.Next(0, 5)),
                    Notes = i % 3 == 0 ? $"معامله شماره {i + 1} - {fromCurrency.Code ?? "N/A"} به {toCurrency.Code ?? "N/A"}" : null,
                    FilledAmount = 0
                };

                _context.Orders.Add(order);
            }
        }

        private async Task SeedTransactionsAsync()
        {
            // Get open buy and sell orders that can be matched
            var buyOrders = await _context.Orders.Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.Completed).ToListAsync();
            var sellOrders = await _context.Orders.Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.Completed).ToListAsync();

            // Check if we have enough orders to create transactions
            if (!buyOrders.Any() || !sellOrders.Any())
            {
                _logger.LogWarning("Not enough orders to create transactions");
                return;
            }

            var random = new Random();
            var statuses = new[] { TransactionStatus.Pending, TransactionStatus.PaymentUploaded, TransactionStatus.ReceiptConfirmed, TransactionStatus.Completed };
            int transactionsCreated = 0;

            // Try to create up to 25 transactions by matching orders
            for (int i = 0; i < Math.Min(buyOrders.Count, 25); i++)
            {
                var buyOrder = buyOrders[i];

                // Find a matching sell order - either exact match or complementary cross-currency
                var matchingSellOrders = sellOrders.Where(s =>
                    s.Id != buyOrder.Id &&
                    (
                        // Exact currency pair match
                        (s.FromCurrency == buyOrder.ToCurrency && s.ToCurrency == buyOrder.FromCurrency) ||
                        // Same currency pair
                        (s.FromCurrency == buyOrder.FromCurrency && s.ToCurrency == buyOrder.ToCurrency)
                    )
                ).ToList();

                if (!matchingSellOrders.Any()) continue;

                var sellOrder = matchingSellOrders[random.Next(matchingSellOrders.Count)];

                // Determine transaction currencies
                var fromCurrency = buyOrder.FromCurrency;
                var toCurrency = buyOrder.ToCurrency;
                var transactionAmount = Math.Min(buyOrder.Amount, sellOrder.Amount);
                var transactionRate = (buyOrder.Rate + sellOrder.Rate) / 2;
                var totalAmount = transactionAmount * transactionRate;

                var transaction = new Transaction
                {
                    BuyOrderId = buyOrder.Id,
                    SellOrderId = sellOrder.Id,
                    BuyerCustomerId = buyOrder.CustomerId,
                    SellerCustomerId = sellOrder.CustomerId,
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                    Amount = transactionAmount,
                    Rate = transactionRate,
                    TotalAmount = totalAmount,
                    TotalInToman = fromCurrency.IsBaseCurrency ? transactionAmount :
                                  (toCurrency.IsBaseCurrency ? totalAmount :
                                   totalAmount * 65000), // Approximate base currency value
                    Status = statuses[random.Next(statuses.Length)],
                    CreatedAt = DateTime.Now.AddDays(-random.Next(1, 15)),
                    BuyerBankAccount = $"بانک ملی - {random.Next(100000, 999999)}",
                    SellerBankAccount = $"بانک صادرات - {random.Next(100000, 999999)}",
                    Notes = $"تراکنش {transactionsCreated + 1} - تبدیل {fromCurrency.Code ?? "N/A"} به {toCurrency.Code ?? "N/A"}"
                };

                if (transaction.Status == TransactionStatus.Completed)
                {
                    transaction.CompletedAt = transaction.CreatedAt.AddHours(random.Next(1, 48));

                    // Update the orders to completed status when transaction is completed
                    buyOrder.Status = OrderStatus.Completed;
                    sellOrder.Status = OrderStatus.Completed;
                }

                _context.Transactions.Add(transaction);
                transactionsCreated++;

                // Remove used sell order from the list to avoid reusing it
                sellOrders.Remove(sellOrder);

                if (transactionsCreated >= 25) break;
            }

            _logger.LogInformation($"Created {transactionsCreated} transactions");
        }

        private async Task SeedReceiptsAsync()
        {
            var customers = await _context.Customers.Take(5).ToListAsync();
            var orders = await _context.Orders.Take(10).ToListAsync();

            // Check if we have enough data to create receipts
            if (!customers.Any() || !orders.Any())
            {
                _logger.LogWarning("Not enough customers or orders to create receipts");
                return;
            }

            var random = new Random();
            var receiptTypes = new[] { ReceiptType.PaymentReceipt, ReceiptType.BankStatement };

            for (int i = 0; i < 15; i++)
            {
                var customer = customers[random.Next(customers.Count)];
                var order = orders[random.Next(orders.Count)];

                var receipt = new Receipt
                {
                    CustomerId = customer.Id,
                    OrderId = order.Id,
                    Type = receiptTypes[random.Next(receiptTypes.Length)],
                    FileName = $"receipt_{i + 1}.jpg",
                    ContentType = "image/jpeg",
                    ImageData = GenerateSampleImageData(),
                    UploadedAt = DateTime.Now.AddDays(-random.Next(1, 30)),
                    IsVerified = random.Next(100) > 30, // 70% verified
                    VerifiedAt = random.Next(100) > 30 ? DateTime.Now.AddDays(-random.Next(0, 5)) : null,
                    ExtractedText = $"مبلغ: {random.Next(1000000, 50000000):N0} تومان\nشماره حساب: {random.Next(100000, 999999)}\nتاریخ: {DateTime.Now.AddDays(-random.Next(1, 10)):yyyy/MM/dd}",
                    ParsedAmount = random.Next(1000000, 50000000).ToString(),
                    ParsedDate = DateTime.Now.AddDays(-random.Next(1, 10)).ToString("yyyy/MM/dd"),
                    ParsedAccountNumber = random.Next(100000, 999999).ToString(),
                    ParsedReferenceId = random.Next(1000000, 9999999).ToString(),
                    Notes = i % 4 == 0 ? $"رسید شماره {i + 1} - تأیید شده" : null
                };

                _context.Receipts.Add(receipt);
            }
        }

        private async Task SeedNotificationsAsync()
        {
            var customers = await _context.Customers.ToListAsync();

            // Check if we have customers to create notifications for
            if (!customers.Any())
            {
                _logger.LogWarning("No customers found to create notifications");
                return;
            }

            var random = new Random();

            var notificationTypes = new[]
            {
                "معامله شما با موفقیت ثبت شد",
                "تراکنش شما در حال پردازش است",
                "رسید شما تأیید شد",
                "نرخ ارز مورد نظر شما تغییر کرد",
                "معامله شما کامل شد",
                "لطفاً رسید تراکنش را ارسال کنید",
                "حساب کاربری شما فعال شد"
            };

            foreach (var customer in customers)
            {
                var notificationCount = random.Next(1, 6); // 1 to 5 notifications per customer

                for (int i = 0; i < notificationCount; i++)
                {
                    var notification = new Notification
                    {
                        CustomerId = customer.Id,
                        Title = "اطلاعیه سیستم",
                        Message = notificationTypes[random.Next(notificationTypes.Length)],
                        Type = NotificationType.SystemAlert,
                        IsRead = random.Next(100) > 40, // 60% read
                        CreatedAt = DateTime.Now.AddDays(-random.Next(1, 30))
                    };

                    _context.Notifications.Add(notification);
                }
            }
        }

        private byte[] GenerateSampleImageData()
        {
            // Generate a simple placeholder image data (1x1 pixel)
            return new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9 };
        }
    }
}
