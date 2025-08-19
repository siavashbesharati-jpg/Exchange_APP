using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface IWebScrapingService
    {
        Task<Dictionary<CurrencyType, (decimal BuyRate, decimal SellRate)>> GetExchangeRatesFromWebAsync();
        Task<(decimal BuyRate, decimal SellRate)?> GetCurrencyRateAsync(CurrencyType currency);
    }
}
