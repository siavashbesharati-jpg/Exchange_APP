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
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
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
            
            // Seed initial currency pools
            modelBuilder.Entity<CurrencyPool>().HasData(
                new CurrencyPool { Id = 1, Currency = "USD", Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "US Dollar pool - initial setup" },
                new CurrencyPool { Id = 2, Currency = "EUR", Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Euro pool - initial setup" },
                new CurrencyPool { Id = 3, Currency = "AED", Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "UAE Dirham pool - initial setup" },
                new CurrencyPool { Id = 4, Currency = "OMR", Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Omani Rial pool - initial setup" },
                new CurrencyPool { Id = 5, Currency = "TRY", Balance = 0, TotalBought = 0, TotalSold = 0, LastUpdated = seedDate, RiskLevel = PoolRiskLevel.Low, IsActive = true, Notes = "Turkish Lira pool - initial setup" }
            );

            modelBuilder.Entity<ExchangeRate>().HasData(
                new ExchangeRate { Id = 1, Currency = CurrencyType.USD, BuyRate = 68000, SellRate = 69000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 2, Currency = CurrencyType.EUR, BuyRate = 72000, SellRate = 73000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 3, Currency = CurrencyType.AED, BuyRate = 18500, SellRate = 19000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 4, Currency = CurrencyType.OMR, BuyRate = 177000, SellRate = 179000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 5, Currency = CurrencyType.TRY, BuyRate = 1950, SellRate = 2050, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" }
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
