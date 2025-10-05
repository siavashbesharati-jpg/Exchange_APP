using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class CurrencyPoolHistory
{
    public int Id { get; set; }

    public long BalanceAfter { get; set; }

    public long BalanceBefore { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public string? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public string? Description { get; set; }

    public int IsDeleted { get; set; }

    public string? PoolTransactionType { get; set; }

    public int? ReferenceId { get; set; }

    public long TransactionAmount { get; set; }

    public DateTime TransactionDate { get; set; }

    public string? TransactionNumber { get; set; }

    public int TransactionType { get; set; }
}
