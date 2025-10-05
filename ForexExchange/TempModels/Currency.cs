using System;
using System.Collections.Generic;

namespace ForexExchange.TempModels;

public partial class Currency
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public int DisplayOrder { get; set; }

    public int IsActive { get; set; }

    public string Name { get; set; } = null!;

    public string PersianName { get; set; } = null!;

    public int RatePriority { get; set; }

    public string Symbol { get; set; } = null!;

    public virtual CurrencyPool? CurrencyPool { get; set; }

    public virtual ICollection<ExchangeRate> ExchangeRateFromCurrencies { get; set; } = new List<ExchangeRate>();

    public virtual ICollection<ExchangeRate> ExchangeRateToCurrencies { get; set; } = new List<ExchangeRate>();

    public virtual ICollection<Order> OrderFromCurrencies { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderToCurrencies { get; set; } = new List<Order>();
}
