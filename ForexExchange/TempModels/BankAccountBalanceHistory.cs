using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class BankAccountBalanceHistory
{
    public int Id { get; set; }

    public int BalanceAfter { get; set; }

    public int BalanceBefore { get; set; }

    public int BankAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public string? Description { get; set; }

    public int IsDeleted { get; set; }

    public int? ReferenceId { get; set; }

    public int TransactionAmount { get; set; }

    public DateTime TransactionDate { get; set; }

    public string? TransactionNumber { get; set; }

    public int TransactionType { get; set; }

    public virtual BankAccount BankAccount { get; set; } = null!;
}
