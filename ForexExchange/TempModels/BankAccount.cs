using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class BankAccount
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string BankName { get; set; } = null!;

    public string AccountNumber { get; set; } = null!;

    public string AccountHolderName { get; set; } = null!;

    public string? Iban { get; set; }

    public string? CardNumberLast4 { get; set; }

    public string? Branch { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public int IsActive { get; set; }

    public int IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastModified { get; set; }

    public string? Notes { get; set; }

    public int AccountBalance { get; set; }

    public virtual ICollection<AccountingDocument> AccountingDocumentPayerBankAccounts { get; set; } = new List<AccountingDocument>();

    public virtual ICollection<AccountingDocument> AccountingDocumentReceiverBankAccounts { get; set; } = new List<AccountingDocument>();

    public virtual ICollection<BankAccountBalanceHistory> BankAccountBalanceHistories { get; set; } = new List<BankAccountBalanceHistory>();

    public virtual ICollection<BankAccountBalance> BankAccountBalances { get; set; } = new List<BankAccountBalance>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
