using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public int FromCurrencyId { get; set; }

    public int ToCurrencyId { get; set; }

    public long FromAmount { get; set; }

    public int Rate { get; set; }

    public long ToAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? UpdatedAt { get; set; }

    public string? Notes { get; set; }

    public int? BankAccountId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public int IsDeleted { get; set; }

    public int IsFrozen { get; set; }

    public virtual BankAccount? BankAccount { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Currency FromCurrency { get; set; } = null!;

    public virtual Currency ToCurrency { get; set; } = null!;
}
