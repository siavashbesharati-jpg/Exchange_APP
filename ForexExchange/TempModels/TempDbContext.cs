using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.TempModels;

public partial class TempDbContext : DbContext
{
    public TempDbContext(DbContextOptions<TempDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccountingDocument> AccountingDocuments { get; set; }

    public virtual DbSet<AdminActivity> AdminActivities { get; set; }

    public virtual DbSet<AspNetRole> AspNetRoles { get; set; }

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; }

    public virtual DbSet<AspNetUser> AspNetUsers { get; set; }

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; }

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; }

    public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; }

    public virtual DbSet<BankAccount> BankAccounts { get; set; }

    public virtual DbSet<BankAccountBalance> BankAccountBalances { get; set; }

    public virtual DbSet<BankAccountBalanceHistory> BankAccountBalanceHistories { get; set; }

    public virtual DbSet<Currency> Currencies { get; set; }

    public virtual DbSet<CurrencyPool> CurrencyPools { get; set; }

    public virtual DbSet<CurrencyPoolHistory> CurrencyPoolHistories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerBalance> CustomerBalances { get; set; }

    public virtual DbSet<CustomerBalanceHistory> CustomerBalanceHistories { get; set; }

    public virtual DbSet<EfmigrationsLock> EfmigrationsLocks { get; set; }

    public virtual DbSet<ExchangeRate> ExchangeRates { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<PushNotificationLog> PushNotificationLogs { get; set; }

    public virtual DbSet<PushSubscription> PushSubscriptions { get; set; }

    public virtual DbSet<ShareableLink> ShareableLinks { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<VapidConfiguration> VapidConfigurations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountingDocument>(entity =>
        {
            entity.HasIndex(e => e.DocumentDate, "IX_AccountingDocuments_DocumentDate");

            entity.HasIndex(e => e.IsVerified, "IX_AccountingDocuments_IsVerified");

            entity.HasIndex(e => e.PayerBankAccountId, "IX_AccountingDocuments_PayerBankAccountId");

            entity.HasIndex(e => e.PayerCustomerId, "IX_AccountingDocuments_PayerCustomerId");

            entity.HasIndex(e => e.ReceiverBankAccountId, "IX_AccountingDocuments_ReceiverBankAccountId");

            entity.HasIndex(e => e.ReceiverCustomerId, "IX_AccountingDocuments_ReceiverCustomerId");

            entity.HasIndex(e => e.ReferenceNumber, "IX_AccountingDocuments_ReferenceNumber");

            entity.HasIndex(e => e.Type, "IX_AccountingDocuments_Type");

            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.PayerBankAccount).WithMany(p => p.AccountingDocumentPayerBankAccounts)
                .HasForeignKey(d => d.PayerBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.PayerCustomer).WithMany(p => p.AccountingDocumentPayerCustomers)
                .HasForeignKey(d => d.PayerCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReceiverBankAccount).WithMany(p => p.AccountingDocumentReceiverBankAccounts)
                .HasForeignKey(d => d.ReceiverBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReceiverCustomer).WithMany(p => p.AccountingDocumentReceiverCustomers)
                .HasForeignKey(d => d.ReceiverCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminActivity>(entity =>
        {
            entity.HasIndex(e => e.ActivityType, "IX_AdminActivities_ActivityType");

            entity.HasIndex(e => e.AdminUserId, "IX_AdminActivities_AdminUserId");

            entity.HasIndex(e => new { e.AdminUserId, e.Timestamp }, "IX_AdminActivities_AdminUserId_Timestamp");

            entity.HasIndex(e => e.Timestamp, "IX_AdminActivities_Timestamp");
        });

        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex").IsUnique();
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.CustomerId, "IX_AspNetUsers_CustomerId").IsUnique();

            entity.HasIndex(e => e.PhoneNumber, "IX_AspNetUsers_PhoneNumber").IsUnique();

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex").IsUnique();

            entity.HasOne(d => d.Customer).WithOne(p => p.AspNetUser)
                .HasForeignKey<AspNetUser>(d => d.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasIndex(e => e.AccountNumber, "IX_BankAccounts_AccountNumber");

            entity.HasIndex(e => new { e.CustomerId, e.IsActive }, "IX_BankAccounts_CustomerId_IsActive");

            entity.HasIndex(e => e.IsDefault, "IX_BankAccounts_IsDefault");

            entity.Property(e => e.AccountBalance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Iban).HasColumnName("IBAN");

            entity.HasOne(d => d.Customer).WithMany(p => p.BankAccounts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BankAccountBalance>(entity =>
        {
            entity.HasIndex(e => new { e.BankAccountId, e.CurrencyCode }, "IX_BankAccountBalances_BankAccountId_CurrencyCode").IsUnique();

            entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.BankAccount).WithMany(p => p.BankAccountBalances).HasForeignKey(d => d.BankAccountId);
        });

        modelBuilder.Entity<BankAccountBalanceHistory>(entity =>
        {
            entity.ToTable("BankAccountBalanceHistory");

            entity.HasIndex(e => new { e.BankAccountId, e.TransactionDate, e.Id }, "IX_BankAccountBalanceHistory_Account_Latest");

            entity.HasIndex(e => e.TransactionDate, "IX_BankAccountBalanceHistory_Date");

            entity.HasIndex(e => new { e.TransactionType, e.ReferenceId }, "IX_BankAccountBalanceHistory_Reference");

            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18,4)");

            entity.HasOne(d => d.BankAccount).WithMany(p => p.BankAccountBalanceHistories)
                .HasForeignKey(d => d.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasIndex(e => e.Code, "IX_Currencies_Code").IsUnique();

            entity.HasIndex(e => new { e.IsActive, e.DisplayOrder }, "IX_Currencies_IsActive_DisplayOrder");
        });

        modelBuilder.Entity<CurrencyPool>(entity =>
        {
            entity.HasIndex(e => e.CurrencyId, "IX_CurrencyPools_CurrencyId").IsUnique();

            entity.HasIndex(e => new { e.CurrencyId, e.IsActive }, "IX_CurrencyPools_CurrencyId_IsActive");

            entity.HasIndex(e => e.LastUpdated, "IX_CurrencyPools_LastUpdated");

            entity.HasIndex(e => e.RiskLevel, "IX_CurrencyPools_RiskLevel");

            entity.Property(e => e.Balance).HasColumnType("decimal(18,8)");
            entity.Property(e => e.TotalBought).HasColumnType("decimal(18,8)");
            entity.Property(e => e.TotalSold).HasColumnType("decimal(18,8)");

            entity.HasOne(d => d.Currency).WithOne(p => p.CurrencyPool)
                .HasForeignKey<CurrencyPool>(d => d.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CurrencyPoolHistory>(entity =>
        {
            entity.ToTable("CurrencyPoolHistory");

            entity.HasIndex(e => new { e.CurrencyCode, e.TransactionDate, e.Id }, "IX_CurrencyPoolHistory_Currency_Latest");

            entity.HasIndex(e => e.TransactionDate, "IX_CurrencyPoolHistory_Date");

            entity.HasIndex(e => new { e.TransactionType, e.ReferenceId }, "IX_CurrencyPoolHistory_Reference");

            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasIndex(e => e.PhoneNumber, "IX_Customers_PhoneNumber").IsUnique();
        });

        modelBuilder.Entity<CustomerBalance>(entity =>
        {
            entity.HasIndex(e => new { e.CustomerId, e.CurrencyCode }, "IX_CustomerBalances_CustomerId_CurrencyCode").IsUnique();

            entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerBalances).HasForeignKey(d => d.CustomerId);
        });

        modelBuilder.Entity<CustomerBalanceHistory>(entity =>
        {
            entity.ToTable("CustomerBalanceHistory");

            entity.HasIndex(e => new { e.CustomerId, e.CurrencyCode, e.TransactionDate, e.Id }, "IX_CustomerBalanceHistory_Customer_Currency_Latest");

            entity.HasIndex(e => e.TransactionDate, "IX_CustomerBalanceHistory_Date");

            entity.HasIndex(e => new { e.TransactionType, e.ReferenceId }, "IX_CustomerBalanceHistory_Reference");

            entity.Property(e => e.BalanceAfter).HasColumnType("decimal(18,4)");
            entity.Property(e => e.BalanceBefore).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TransactionAmount).HasColumnType("decimal(18,4)");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerBalanceHistories)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EfmigrationsLock>(entity =>
        {
            entity.ToTable("__EFMigrationsLock");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId, e.IsActive }, "IX_ExchangeRates_FromCurrencyId_ToCurrencyId_IsActive").IsUnique();

            entity.HasIndex(e => e.ToCurrencyId, "IX_ExchangeRates_ToCurrencyId");

            entity.Property(e => e.AverageBuyRate).HasColumnType("decimal(18,8)");
            entity.Property(e => e.AverageSellRate).HasColumnType("decimal(18,8)");
            entity.Property(e => e.Rate).HasColumnType("decimal(18,8)");
            entity.Property(e => e.TotalBuyVolume).HasColumnType("decimal(18,8)");
            entity.Property(e => e.TotalSellVolume).HasColumnType("decimal(18,8)");

            entity.HasOne(d => d.FromCurrency).WithMany(p => p.ExchangeRateFromCurrencies)
                .HasForeignKey(d => d.FromCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ToCurrency).WithMany(p => p.ExchangeRateToCurrencies)
                .HasForeignKey(d => d.ToCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt, "IX_Notifications_CreatedAt");

            entity.HasIndex(e => new { e.CustomerId, e.IsRead }, "IX_Notifications_CustomerId_IsRead");

            entity.HasOne(d => d.Customer).WithMany(p => p.Notifications).HasForeignKey(d => d.CustomerId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.BankAccountId, "IX_Orders_BankAccountId");

            entity.HasIndex(e => e.CreatedAt, "IX_Orders_CreatedAt");

            entity.HasIndex(e => e.CustomerId, "IX_Orders_CustomerId");

            entity.HasIndex(e => new { e.FromCurrencyId, e.ToCurrencyId }, "IX_Orders_FromCurrencyId_ToCurrencyId");

            entity.HasIndex(e => e.ToCurrencyId, "IX_Orders_ToCurrencyId");

            entity.Property(e => e.FromAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Rate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ToAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.BankAccount).WithMany(p => p.Orders).HasForeignKey(d => d.BankAccountId);

            entity.HasOne(d => d.Customer).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.FromCurrency).WithMany(p => p.OrderFromCurrencies)
                .HasForeignKey(d => d.FromCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ToCurrency).WithMany(p => p.OrderToCurrencies)
                .HasForeignKey(d => d.ToCurrencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PushNotificationLog>(entity =>
        {
            entity.HasIndex(e => e.PushSubscriptionId, "IX_PushNotificationLogs_PushSubscriptionId");

            entity.HasIndex(e => e.SentAt, "IX_PushNotificationLogs_SentAt");

            entity.HasIndex(e => e.WasSuccessful, "IX_PushNotificationLogs_WasSuccessful");

            entity.HasOne(d => d.PushSubscription).WithMany(p => p.PushNotificationLogs).HasForeignKey(d => d.PushSubscriptionId);
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.HasIndex(e => e.Endpoint, "IX_PushSubscriptions_Endpoint");

            entity.HasIndex(e => new { e.IsActive, e.UserId }, "IX_PushSubscriptions_IsActive_UserId");

            entity.HasIndex(e => e.UserId, "IX_PushSubscriptions_UserId");

            entity.HasIndex(e => e.UserId1, "IX_PushSubscriptions_UserId1");

            entity.HasOne(d => d.User).WithMany(p => p.PushSubscriptionUsers).HasForeignKey(d => d.UserId);

            entity.HasOne(d => d.UserId1Navigation).WithMany(p => p.PushSubscriptionUserId1Navigations).HasForeignKey(d => d.UserId1);
        });

        modelBuilder.Entity<ShareableLink>(entity =>
        {
            entity.HasIndex(e => e.CustomerId, "IX_ShareableLinks_CustomerId");

            entity.HasIndex(e => new { e.IsActive, e.ExpiresAt }, "IX_ShareableLinks_IsActive_ExpiresAt");

            entity.HasIndex(e => e.Token, "IX_ShareableLinks_Token").IsUnique();

            entity.HasOne(d => d.Customer).WithMany(p => p.ShareableLinks).HasForeignKey(d => d.CustomerId);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasIndex(e => e.SettingKey, "IX_SystemSettings_SettingKey").IsUnique();
        });

        modelBuilder.Entity<VapidConfiguration>(entity =>
        {
            entity.HasIndex(e => e.ApplicationId, "IX_VapidConfigurations_ApplicationId").IsUnique();

            entity.HasIndex(e => e.CreatedAt, "IX_VapidConfigurations_CreatedAt");

            entity.HasIndex(e => e.IsActive, "IX_VapidConfigurations_IsActive");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
