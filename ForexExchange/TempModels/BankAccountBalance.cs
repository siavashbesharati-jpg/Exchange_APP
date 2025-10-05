using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class BankAccountBalance
{
    public int Id { get; set; }

    public int BankAccountId { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public int Balance { get; set; }

    public DateTime LastUpdated { get; set; }

    public string? Notes { get; set; }

    public virtual BankAccount BankAccount { get; set; } = null!;
}
