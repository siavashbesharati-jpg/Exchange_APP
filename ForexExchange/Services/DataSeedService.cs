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

    }
}


