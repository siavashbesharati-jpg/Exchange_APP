using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class Customer
{
    public int Id { get; set; }

    public string FullName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string NationalId { get; set; } = null!;

    public string Address { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public int IsActive { get; set; }

    public int IsSystem { get; set; }

    public int Gender { get; set; }

    public virtual ICollection<AccountingDocument> AccountingDocumentPayerCustomers { get; set; } = new List<AccountingDocument>();

    public virtual ICollection<AccountingDocument> AccountingDocumentReceiverCustomers { get; set; } = new List<AccountingDocument>();

    public virtual AspNetUser? AspNetUser { get; set; }

    public virtual ICollection<BankAccount> BankAccounts { get; set; } = new List<BankAccount>();

    public virtual ICollection<CustomerBalanceHistory> CustomerBalanceHistories { get; set; } = new List<CustomerBalanceHistory>();

    public virtual ICollection<CustomerBalance> CustomerBalances { get; set; } = new List<CustomerBalance>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<ShareableLink> ShareableLinks { get; set; } = new List<ShareableLink>();
}
