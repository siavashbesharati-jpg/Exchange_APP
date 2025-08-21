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
            });
            
            // ExchangeRate configurations
            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Currency, e.IsActive });
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
                entity.HasIndex(e => e.Currency).IsUnique();
                entity.Property(e => e.Balance).HasColumnType("decimal(18,8)");
                entity.Property(e => e.TotalBought).HasColumnType("decimal(18,8)");
                entity.Property(e => e.TotalSold).HasColumnType("decimal(18,8)");
                entity.Property(e => e.AverageBuyRate).HasColumnType("decimal(18,4)");
                entity.Property(e => e.AverageSellRate).HasColumnType("decimal(18,4)");
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.HasIndex(e => new { e.Currency, e.IsActive });
                entity.HasIndex(e => e.LastUpdated);
                entity.HasIndex(e => e.RiskLevel);
            });
            
            // ExchangeRate configurations - Updated for cross-currency support
            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.FromCurrency, e.ToCurrency, e.IsActive }).IsUnique();
                entity.Property(e => e.BuyRate).HasColumnType("decimal(18,4)");
                entity.Property(e => e.SellRate).HasColumnType("decimal(18,4)");
                entity.Property(e => e.UpdatedBy).HasMaxLength(50);
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
                entity.HasIndex(e => new { e.FromCurrency, e.ToCurrency });
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
            });
            
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
            
            // Seed initial currency pools - now including all currencies including Toman
            modelBuilder.Entity<CurrencyPool>().HasData(
                new CurrencyPool { Id = 1, Currency = CurrencyType.Toman, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Iranian Toman pool - initial setup" },
                new CurrencyPool { Id = 2, Currency = CurrencyType.USD, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "US Dollar pool - initial setup" },
                new CurrencyPool { Id = 3, Currency = CurrencyType.EUR, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Euro pool - initial setup" },
                new CurrencyPool { Id = 4, Currency = CurrencyType.AED, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "UAE Dirham pool - initial setup" },
                new CurrencyPool { Id = 5, Currency = CurrencyType.OMR, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Omani Rial pool - initial setup" },
                new CurrencyPool { Id = 6, Currency = CurrencyType.TRY, Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Turkish Lira pool - initial setup" }
            );

            // Seed initial exchange rates - now with cross-currency pairs
            modelBuilder.Entity<ExchangeRate>().HasData(
                // Toman to other currencies (legacy rates)
                new ExchangeRate { Id = 1, FromCurrency = CurrencyType.Toman, ToCurrency = CurrencyType.USD, BuyRate = 68000, SellRate = 69000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 2, FromCurrency = CurrencyType.Toman, ToCurrency = CurrencyType.EUR, BuyRate = 72000, SellRate = 73000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 3, FromCurrency = CurrencyType.Toman, ToCurrency = CurrencyType.AED, BuyRate = 18500, SellRate = 19000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 4, FromCurrency = CurrencyType.Toman, ToCurrency = CurrencyType.OMR, BuyRate = 177000, SellRate = 179000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 5, FromCurrency = CurrencyType.Toman, ToCurrency = CurrencyType.TRY, BuyRate = 1950, SellRate = 2050, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                
                // Reverse rates (other currencies to Toman)
                new ExchangeRate { Id = 6, FromCurrency = CurrencyType.USD, ToCurrency = CurrencyType.Toman, BuyRate = 1.0m/69000, SellRate = 1.0m/68000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 7, FromCurrency = CurrencyType.EUR, ToCurrency = CurrencyType.Toman, BuyRate = 1.0m/73000, SellRate = 1.0m/72000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 8, FromCurrency = CurrencyType.AED, ToCurrency = CurrencyType.Toman, BuyRate = 1.0m/19000, SellRate = 1.0m/18500, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 9, FromCurrency = CurrencyType.OMR, ToCurrency = CurrencyType.Toman, BuyRate = 1.0m/179000, SellRate = 1.0m/177000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 10, FromCurrency = CurrencyType.TRY, ToCurrency = CurrencyType.Toman, BuyRate = 1.0m/2050, SellRate = 1.0m/1950, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                
                // Sample cross-currency rates (USD to other currencies)
                new ExchangeRate { Id = 11, FromCurrency = CurrencyType.USD, ToCurrency = CurrencyType.EUR, BuyRate = 0.92m, SellRate = 0.94m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 12, FromCurrency = CurrencyType.USD, ToCurrency = CurrencyType.AED, BuyRate = 3.67m, SellRate = 3.69m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 13, FromCurrency = CurrencyType.USD, ToCurrency = CurrencyType.OMR, BuyRate = 0.384m, SellRate = 0.386m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 14, FromCurrency = CurrencyType.USD, ToCurrency = CurrencyType.TRY, BuyRate = 34.5m, SellRate = 35.2m, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" }
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
        }
    }
}
