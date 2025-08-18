using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Models
{
    public class ForexDbContext : DbContext
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
            
            // Seed initial exchange rates
            var seedDate = new DateTime(2025, 8, 18, 12, 0, 0, DateTimeKind.Utc);
            modelBuilder.Entity<ExchangeRate>().HasData(
                new ExchangeRate { Id = 1, Currency = CurrencyType.USD, BuyRate = 68000, SellRate = 69000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 2, Currency = CurrencyType.EUR, BuyRate = 72000, SellRate = 73000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 3, Currency = CurrencyType.AED, BuyRate = 18500, SellRate = 19000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 4, Currency = CurrencyType.OMR, BuyRate = 177000, SellRate = 179000, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" },
                new ExchangeRate { Id = 5, Currency = CurrencyType.TRY, BuyRate = 1950, SellRate = 2050, IsActive = true, UpdatedAt = seedDate, UpdatedBy = "System" }
            );
        }
    }
}
