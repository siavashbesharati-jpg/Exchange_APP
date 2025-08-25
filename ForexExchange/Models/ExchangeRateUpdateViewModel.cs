namespace ForexExchange.Models
{
    public class ExchangeRateUpdateViewModel
    {
        public int? Id { get; set; }
        public int FromCurrencyId { get; set; }
        public int ToCurrencyId { get; set; }
        public decimal BuyRate { get; set; }
        public decimal SellRate { get; set; }
    }
}
