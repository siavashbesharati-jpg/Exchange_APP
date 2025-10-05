using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class ExchangeRate
{
    public int Id { get; set; }

    public int FromCurrencyId { get; set; }

    public int ToCurrencyId { get; set; }

    public int Rate { get; set; }

    public decimal? AverageBuyRate { get; set; }

    public decimal? AverageSellRate { get; set; }

    public long TotalBuyVolume { get; set; }

    public long TotalSellVolume { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public int IsActive { get; set; }

    public virtual Currency FromCurrency { get; set; } = null!;

    public virtual Currency ToCurrency { get; set; } = null!;
}
