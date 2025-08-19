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

        public DataSeedService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ForexDbContext context,
            ILogger<DataSeedService> logger,
            IWebScrapingService webScrapingService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _webScrapingService = webScrapingService;
        }

        public async Task SeedDataAsync()
        {
            try
            {
                // Create roles
                await CreateRolesAsync();

                // Create admin user
                await CreateAdminUserAsync();

                // Seed exchange rates first
                await SeedExchangeRatesAsync();

                // Seed sample data
                await SeedSampleDataAsync();

                _logger.LogInformation("Data seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding data");
                throw;
            }
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
            const string adminEmail = "admin@iranexpedia.ir";
            const string adminPhone = "09120674032";
            const string adminPassword = "123456"; // Changed to 6 characters

            // Check if admin exists by email first (for existing installations)
            var adminUser = await _userManager.FindByEmailAsync(adminEmail);
            
            // If found with email username, update it to use phone number
            if (adminUser != null && adminUser.UserName == adminEmail)
            {
                adminUser.UserName = adminPhone;
                var updateResult = await _userManager.UpdateAsync(adminUser);
                if (updateResult.Succeeded)
                {
                    _logger.LogInformation($"Updated existing admin user to use phone number as username: {adminPhone}");
                }
                return;
            }

            // Check if admin exists by phone number
            if (adminUser == null)
            {
                adminUser = await _userManager.FindByNameAsync(adminPhone);
            }

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminPhone, // Use phone as username for login
                    Email = adminEmail,
                    FullName = "مدیر سیستم",
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    EmailConfirmed = true,
                    PhoneNumber = adminPhone
                };

                var result = await _userManager.CreateAsync(adminUser, adminPassword);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                    _logger.LogInformation($"Admin user created successfully with username: {adminPhone} and email: {adminEmail}");
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogError($"Failed to create admin user: {errors}");
                }
            }
            else
            {
                _logger.LogInformation("Admin user already exists");
            }
        }

        private async Task SeedExchangeRatesAsync()
        {
            // Check if exchange rates already exist
            if (await _context.ExchangeRates.AnyAsync())
            {
                _logger.LogInformation("Exchange rates already exist, skipping seeding");
                return;
            }

            _logger.LogInformation("Seeding exchange rates using web scraping...");

            try
            {
                // Get real-time rates from web scraping service
                var webRates = await _webScrapingService.GetExchangeRatesFromWebAsync();
                
                var exchangeRates = new List<ExchangeRate>();

                foreach (var rate in webRates)
                {
                    exchangeRates.Add(new ExchangeRate
                    {
                        Currency = rate.Key,
                        BuyRate = rate.Value.BuyRate,
                        SellRate = rate.Value.SellRate,
                        IsActive = true,
                        UpdatedAt = DateTime.Now,
                        UpdatedBy = "WebScraping-System"
                    });
                }

                if (exchangeRates.Any())
                {
                    _context.ExchangeRates.AddRange(exchangeRates);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Successfully seeded {exchangeRates.Count} exchange rates from web scraping");
                }
                else
                {
                    _logger.LogWarning("No rates received from web scraping, using fallback rates");
                    await SeedFallbackExchangeRatesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get rates from web scraping, using fallback rates");
                await SeedFallbackExchangeRatesAsync();
            }
        }

        private async Task SeedFallbackExchangeRatesAsync()
        {
            _logger.LogInformation("Seeding fallback exchange rates...");

            var exchangeRates = new[]
            {
                new ExchangeRate
                {
                    Currency = CurrencyType.USD,
                    BuyRate = 65000,
                    SellRate = 64000,
                    IsActive = true,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = "System-Fallback"
                },
                new ExchangeRate
                {
                    Currency = CurrencyType.EUR,
                    BuyRate = 70000,
                    SellRate = 69000,
                    IsActive = true,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = "System-Fallback"
                },
                new ExchangeRate
                {
                    Currency = CurrencyType.AED,
                    BuyRate = 17500,
                    SellRate = 17000,
                    IsActive = true,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = "System-Fallback"
                },
                new ExchangeRate
                {
                    Currency = CurrencyType.OMR,
                    BuyRate = 168000,
                    SellRate = 166000,
                    IsActive = true,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = "System-Fallback"
                },
                new ExchangeRate
                {
                    Currency = CurrencyType.TRY,
                    BuyRate = 1900,
                    SellRate = 1800,
                    IsActive = true,
                    UpdatedAt = DateTime.Now,
                    UpdatedBy = "System-Fallback"
                }
            };

            _context.ExchangeRates.AddRange(exchangeRates);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully seeded {exchangeRates.Length} fallback exchange rates");
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
            await SeedOrdersAsync();
            await _context.SaveChangesAsync(); // Save orders before transactions


            // Seed Transactions
            await SeedTransactionsAsync();
            await _context.SaveChangesAsync(); // Save transactions before receipts
            
            // Seed Receipts
            await SeedReceiptsAsync();
            await _context.SaveChangesAsync(); // Save receipts before notifications
            
            // Seed Notifications
            await SeedNotificationsAsync();

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
            var exchangeRates = await _context.ExchangeRates.Where(r => r.IsActive).ToListAsync();
            var random = new Random();

            var currencies = new[] { CurrencyType.USD, CurrencyType.EUR, CurrencyType.AED, CurrencyType.OMR, CurrencyType.TRY };
            var orderTypes = new[] { OrderType.Buy, OrderType.Sell };
            // Create more Open orders to allow for better transaction matching
            // var statuses = new[] { OrderStatus.Open, OrderStatus.Open, OrderStatus.Open, OrderStatus.Completed, OrderStatus.Cancelled, OrderStatus.PartiallyFilled };

            for (int i = 0; i < 50; i++) // Create 50 sample orders
            {
                var customer = customers[random.Next(customers.Count)];
                var currency = currencies[random.Next(currencies.Length)];
                var orderType = orderTypes[random.Next(orderTypes.Length)];
                var status = OrderStatus.Open;

                var exchangeRate = exchangeRates.FirstOrDefault(r => r.Currency == currency);
                if (exchangeRate == null) continue;

                var amount = random.Next(100, 10000);
                var rate = orderType == OrderType.Buy ? exchangeRate.SellRate : exchangeRate.BuyRate;

                var order = new Order
                {
                    CustomerId = customer.Id,
                    Currency = currency,
                    Amount = amount,
                    Rate = rate,
                    TotalInToman = amount * rate,
                    OrderType = orderType,
                    Status = status,
                    CreatedAt = DateTime.Now.AddDays(-random.Next(1, 30)),
                    UpdatedAt = DateTime.Now.AddDays(-random.Next(0, 5)),
                    Notes = i % 3 == 0 ? $"سفارش شماره {i + 1} - {(orderType == OrderType.Buy ? "خرید" : "فروش")} {currency}" : null,
                    FilledAmount = 0
                };

                _context.Orders.Add(order);
            }
        }

        private async Task SeedTransactionsAsync()
        {
            // Get open buy and sell orders that can be matched
            var buyOrders = await _context.Orders.Where(o => o.OrderType == OrderType.Buy && (o.Status == OrderStatus.Open || o.Status == OrderStatus.Completed)).ToListAsync();
            var sellOrders = await _context.Orders.Where(o => o.OrderType == OrderType.Sell && (o.Status == OrderStatus.Open || o.Status == OrderStatus.Completed)).ToListAsync();

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

                // Find a matching sell order for the same currency
                var matchingSellOrders = sellOrders.Where(s => s.Currency == buyOrder.Currency && s.Id != buyOrder.Id).ToList();
                if (!matchingSellOrders.Any()) continue;

                var sellOrder = matchingSellOrders[random.Next(matchingSellOrders.Count)];

                var transaction = new Transaction
                {
                    BuyOrderId = buyOrder.Id,
                    SellOrderId = sellOrder.Id,
                    BuyerCustomerId = buyOrder.CustomerId,
                    SellerCustomerId = sellOrder.CustomerId,
                    Currency = buyOrder.Currency,
                    Amount = Math.Min(buyOrder.Amount, sellOrder.Amount),
                    Rate = (buyOrder.Rate + sellOrder.Rate) / 2,
                    TotalInToman = Math.Min(buyOrder.TotalInToman, sellOrder.TotalInToman),
                    Status = statuses[random.Next(statuses.Length)],
                    CreatedAt = DateTime.Now.AddDays(-random.Next(1, 15)),
                    BuyerBankAccount = $"بانک ملی - {random.Next(100000, 999999)}",
                    SellerBankAccount = $"بانک صادرات - {random.Next(100000, 999999)}",
                    Notes = $"تراکنش {transactionsCreated + 1} - تبدیل {buyOrder.Currency}"
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
                "سفارش شما با موفقیت ثبت شد",
                "تراکنش شما در حال پردازش است",
                "رسید شما تأیید شد",
                "نرخ ارز مورد نظر شما تغییر کرد",
                "سفارش شما کامل شد",
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
