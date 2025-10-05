using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class CustomerBalance
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public int Balance { get; set; }

    public DateTime LastUpdated { get; set; }

    public string? Notes { get; set; }

    public virtual Customer Customer { get; set; } = null!;
}
