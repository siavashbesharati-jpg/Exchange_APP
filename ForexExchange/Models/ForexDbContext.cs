using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace ForexExchange.Models
{
    public class ForexDbContext : IdentityDbContext<ApplicationUser>
    {
        public ForexDbContext(DbContextOptions<ForexDbContext> options) : base(options)
        {
        }

        public ForexDbContext()
        {
            
        }
        
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<SystemSettings> SystemSettings { get; set; }
        public DbSet<CurrencyPool> CurrencyPools { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<AdminActivity> AdminActivities { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<CustomerInitialBalance> CustomerInitialBalances { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Customer configurations
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => e.PhoneNumber).IsUnique();
            });
            
            // Order configurations
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Customer)
                      .WithMany(e => e.Orders)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            
            // Transaction configurations
            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.BuyOrder)
                      .WithMany(e => e.Transactions)
                      .HasForeignKey(e => e.BuyOrderId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(e => e.SellOrder)
                      .WithMany()
                      .HasForeignKey(e => e.SellOrderId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(e => e.BuyerCustomer)
                      .WithMany(e => e.BuyTransactions)
                      .HasForeignKey(e => e.BuyerCustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(e => e.SellerCustomer)
                      .WithMany(e => e.SellTransactions)
                      .HasForeignKey(e => e.SellerCustomerId)
                      .OnDelete(DeleteBehavior.Restrict);

            // Map currency relationships
            entity.HasOne(e => e.FromCurrency)
                .WithMany(c => c.FromCurrencyTransactions)
                .HasForeignKey(e => e.FromCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ToCurrency)
                .WithMany(c => c.ToCurrencyTransactions)
                .HasForeignKey(e => e.ToCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Useful indexes
            entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            });
            
            // Receipt configurations
            modelBuilder.Entity<Receipt>(entity =>
            {
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Customer)
                      .WithMany(e => e.Receipts)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(e => e.Order)
                      .WithMany(e => e.Receipts)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.SetNull);
                      
                entity.HasOne(e => e.Transaction)
                      .WithMany(e => e.Receipts)
                      .HasForeignKey(e => e.TransactionId)
                      .OnDelete(DeleteBehavior.SetNull);

            // Optional system bank account link
            entity.HasOne(e => e.SystemBankAccount)
                .WithMany()
                .HasForeignKey(e => e.SystemBankAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            });
            
            // ExchangeRate configurations
            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId, e.IsActive });
            });
            
            // Notification configurations
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Customer)
                      .WithMany(e => e.Notifications)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.CustomerId, e.IsRead });
                entity.HasIndex(e => e.CreatedAt);
            });

            // SystemSettings configurations
            modelBuilder.Entity<SystemSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SettingKey).IsUnique();
                entity.Property(e => e.SettingKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.SettingValue).IsRequired().HasMaxLength(500);
                entity.Property(e => e.DataType).HasMaxLength(50);
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            });

            // CurrencyPool configurations
            modelBuilder.Entity<CurrencyPool>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.CurrencyId).IsUnique();
                entity.Property(e => e.Balance).HasColumnType("decimal(18,8)");
                entity.Property(e => e.TotalBought).HasColumnType("decimal(18,8)");
                entity.Property(e => e.TotalSold).HasColumnType("decimal(18,8)");
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.HasIndex(e => new { e.CurrencyId, e.IsActive });
                entity.HasIndex(e => e.LastUpdated);
                entity.HasIndex(e => e.RiskLevel);

            entity.HasOne(e => e.Currency)
                .WithMany(c => c.CurrencyPools)
                .HasForeignKey(e => e.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);
            });
            
            // Currency configurations
            modelBuilder.Entity<Currency>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).IsRequired().HasMaxLength(3);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PersianName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Symbol).HasMaxLength(5);
                entity.HasIndex(e => new { e.IsActive, e.DisplayOrder });
                // Ignore legacy navigation not mapped on ExchangeRate
                entity.Ignore(e => e.LegacyRates);
            });

            // CustomerInitialBalance configurations
            modelBuilder.Entity<CustomerInitialBalance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.InitialBalances)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.CustomerId, e.CurrencyCode }).IsUnique();
            });
            
            // ExchangeRate configurations - Updated for cross-currency support
            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId, e.IsActive }).IsUnique();
                entity.Property(e => e.Rate).HasColumnType("decimal(18,8)");
                entity.Property(e => e.AverageBuyRate).HasColumnType("decimal(18,8)");
                entity.Property(e => e.AverageSellRate).HasColumnType("decimal(18,8)");
                entity.Property(e => e.TotalBuyVolume).HasColumnType("decimal(18,8)");
                entity.Property(e => e.TotalSellVolume).HasColumnType("decimal(18,8)");
                entity.Property(e => e.UpdatedBy).HasMaxLength(50);
                
                // Configure foreign key relationships
                entity.HasOne(e => e.FromCurrency)
                      .WithMany(c => c.FromCurrencyRates)
                      .HasForeignKey(e => e.FromCurrencyId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasOne(e => e.ToCurrency)
                      .WithMany(c => c.ToCurrencyRates)
                      .HasForeignKey(e => e.ToCurrencyId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // AdminActivity configurations
            modelBuilder.Entity<AdminActivity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AdminUserId).HasMaxLength(450); // ASP.NET Identity User ID length
                entity.Property(e => e.AdminUsername).HasMaxLength(256);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Details).HasColumnType("TEXT");
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.EntityType).HasMaxLength(100);
                entity.Property(e => e.OldValue).HasColumnType("TEXT");
                entity.Property(e => e.NewValue).HasColumnType("TEXT");
                entity.HasIndex(e => e.AdminUserId);
                entity.HasIndex(e => e.ActivityType);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => new { e.AdminUserId, e.Timestamp });
            });
            
        // Order configurations - Updated for cross-currency support
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Customer)
                      .WithMany(e => e.Orders)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Rate).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.FilledAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(500);
            // Map currency relationships
            entity.HasOne(e => e.FromCurrency)
                .WithMany(c => c.FromCurrencyOrders)
                .HasForeignKey(e => e.FromCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ToCurrency)
                .WithMany(c => c.ToCurrencyOrders)
                .HasForeignKey(e => e.ToCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Useful indexes
            entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId });
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
            });
            
            // BankAccount configurations
            modelBuilder.Entity<BankAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Customer)
                      .WithMany(e => e.BankAccounts)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.Property(e => e.BankName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.AccountHolderName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.IBAN).HasMaxLength(34);
                entity.Property(e => e.CardNumberLast4).HasMaxLength(4);
                entity.Property(e => e.Branch).HasMaxLength(100);
                entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.HasIndex(e => new { e.CustomerId, e.IsActive });
                entity.HasIndex(e => e.AccountNumber);
                entity.HasIndex(e => e.IsDefault).HasFilter("[IsDefault] = 1");
            });
            
          
            
            // Configure BankAccount-Transaction relationships
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.BuyerBankAccountNavigation)
                .WithMany()
                .HasForeignKey(t => t.BuyerBankAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.SellerBankAccountNavigation)
                .WithMany()
                .HasForeignKey(t => t.SellerBankAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // ApplicationUser configurations
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasIndex(e => e.PhoneNumber).IsUnique();
                entity.HasOne(e => e.Customer)
                      .WithOne()
                      .HasForeignKey<ApplicationUser>(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Seed initial exchange rates
            var seedDate = new DateTime(2025, 8, 18, 12, 0, 0, DateTimeKind.Utc);
            
            // Seed initial currency pools - now using Currency IDs instead of CurrencyType enum
            modelBuilder.Entity<CurrencyPool>().HasData(
                new CurrencyPool { Id = 1, CurrencyId = 1, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Iranian Rial pool - initial setup" },
                new CurrencyPool { Id = 2, CurrencyId = 2, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "US Dollar pool - initial setup" },
                new CurrencyPool { Id = 3, CurrencyId = 3, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Euro pool - initial setup" },
                new CurrencyPool { Id = 4, CurrencyId = 4, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "UAE Dirham pool - initial setup" },
                new CurrencyPool { Id = 5, CurrencyId = 5, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Omani Rial pool - initial setup" },
                new CurrencyPool { Id = 6, CurrencyId = 6, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Turkish Lira pool - initial setup" },
                new CurrencyPool { Id = 7, CurrencyId = 7, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Chinese Yuan pool - initial setup" }
            );

            // Seed initial exchange rates - now with Currency IDs and cross-currency pairs
            modelBuilder.Entity<ExchangeRate>().HasData(
                // IRR to other currencies (base currency rates)
                new ExchangeRate { Id = 1, FromCurrencyId = 1, ToCurrencyId = 2, Rate = 68500, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 2, FromCurrencyId = 1, ToCurrencyId = 3, Rate = 72500, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 3, FromCurrencyId = 1, ToCurrencyId = 4, Rate = 18750, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 4, FromCurrencyId = 1, ToCurrencyId = 5, Rate = 178000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 5, FromCurrencyId = 1, ToCurrencyId = 6, Rate = 2000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 15, FromCurrencyId = 1, ToCurrencyId = 7, Rate = 9600, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                // Reverse rates (other currencies to IRR)
                new ExchangeRate { Id = 6, FromCurrencyId = 2, ToCurrencyId = 1, Rate = 1.0m/68500, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 7, FromCurrencyId = 3, ToCurrencyId = 1, Rate = 1.0m/72500, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 8, FromCurrencyId = 4, ToCurrencyId = 1, Rate = 1.0m/18750, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 9, FromCurrencyId = 5, ToCurrencyId = 1, Rate = 1.0m/178000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 10, FromCurrencyId = 6, ToCurrencyId = 1, Rate = 1.0m/2000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 16, FromCurrencyId = 7, ToCurrencyId = 1, Rate = 1.0m/9600, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                // Sample cross-currency rates (USD to other currencies)
                new ExchangeRate { Id = 11, FromCurrencyId = 2, ToCurrencyId = 3, Rate = 0.93m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 12, FromCurrencyId = 2, ToCurrencyId = 4, Rate = 3.68m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 13, FromCurrencyId = 2, ToCurrencyId = 5, Rate = 0.385m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 14, FromCurrencyId = 2, ToCurrencyId = 6, Rate = 34.85m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 17, FromCurrencyId = 2, ToCurrencyId = 7, Rate = 7.14m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" }
            );

            // Seed initial system settings
            modelBuilder.Entity<SystemSettings>().HasData(
                new SystemSettings { Id = 1, SettingKey = SettingKeys.CommissionRate, SettingValue = "0.5", Description = "نرخ کمیسیون به درصد", DataType = "decimal", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 2, SettingKey = SettingKeys.ExchangeFeeRate, SettingValue = "0.2", Description = "کارمزد تبدیل ارز به درصد", DataType = "decimal", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 3, SettingKey = SettingKeys.MinTransactionAmount, SettingValue = "10000", Description = "حداقل مبلغ تراکنش به تومان", DataType = "decimal", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 4, SettingKey = SettingKeys.MaxTransactionAmount, SettingValue = "1000000000", Description = "حداکثر مبلغ تراکنش به تومان", DataType = "decimal", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 5, SettingKey = SettingKeys.DailyTransactionLimit, SettingValue = "5000000000", Description = "محدودیت تراکنش روزانه به تومان", DataType = "decimal", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 6, SettingKey = SettingKeys.SystemMaintenance, SettingValue = "false", Description = "حالت تعمیرات سیستم", DataType = "bool", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 7, SettingKey = SettingKeys.DefaultCurrency, SettingValue = "USD", Description = "ارز پیش‌فرض سیستم", DataType = "string", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 8, SettingKey = SettingKeys.RateUpdateInterval, SettingValue = "60", Description = "بازه بروزرسانی نرخ ارز به دقیقه", DataType = "int", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 9, SettingKey = SettingKeys.NotificationEnabled, SettingValue = "true", Description = "فعال‌سازی سیستم اعلان‌ها", DataType = "bool", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" },
                new SystemSettings { Id = 10, SettingKey = SettingKeys.BackupEnabled, SettingValue = "true", Description = "فعال‌سازی پشتیبان‌گیری خودکار", DataType = "bool", CreatedAt = seedDate, UpdatedAt = seedDate, UpdatedBy = "System" }
            );

            // Seed currencies
            modelBuilder.Entity<Currency>().HasData(
                new Currency { Id = 1, Code = "IRR", Name = "Iranian Rial", PersianName = "تومان", Symbol = "﷼", IsActive = true, IsBaseCurrency = true, DisplayOrder = 1, CreatedAt = seedDate },
                new Currency { Id = 2, Code = "USD", Name = "US Dollar", PersianName = "دلار آمریکا", Symbol = "$", IsActive = true, IsBaseCurrency = false, DisplayOrder = 4, CreatedAt = seedDate },
                new Currency { Id = 3, Code = "EUR", Name = "Euro", PersianName = "یورو", Symbol = "€", IsActive = true, IsBaseCurrency = false, DisplayOrder = 5, CreatedAt = seedDate },
                new Currency { Id = 4, Code = "AED", Name = "UAE Dirham", PersianName = "درهم امارات", Symbol = "د.إ", IsActive = true, IsBaseCurrency = false, DisplayOrder = 3, CreatedAt = seedDate },
                new Currency { Id = 5, Code = "OMR", Name = "Omani Rial", PersianName = "ریال عمان", Symbol = "ر.ع.", IsActive = true, IsBaseCurrency = false, DisplayOrder = 2, CreatedAt = seedDate },
                new Currency { Id = 6, Code = "TRY", Name = "Turkish Lira", PersianName = "لیر ترکیه", Symbol = "₺", IsActive = true, IsBaseCurrency = false, DisplayOrder = 6, CreatedAt = seedDate },
                new Currency { Id = 7, Code = "CNY", Name = "Chinese Yuan", PersianName = "یوان چین", Symbol = "¥", IsActive = true, IsBaseCurrency = false, DisplayOrder = 7, CreatedAt = seedDate }
            );
        }
    }
}
