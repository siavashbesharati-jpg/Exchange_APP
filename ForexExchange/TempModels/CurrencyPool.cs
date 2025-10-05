using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class CurrencyPool
{
    public int Id { get; set; }

    public int CurrencyId { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public decimal Balance { get; set; }

    public long TotalBought { get; set; }

    public long TotalSold { get; set; }

    public int ActiveBuyOrderCount { get; set; }

    public int ActiveSellOrderCount { get; set; }

    public DateTime LastUpdated { get; set; }

    public int RiskLevel { get; set; }

    public string? Notes { get; set; }

    public int IsActive { get; set; }

    public virtual Currency Currency { get; set; } = null!;
}
