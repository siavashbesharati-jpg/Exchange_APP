using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface IWebScrapingService
    {
        Task<Dictionary<string, (decimal BuyRate, decimal SellRate)>> GetExchangeRatesFromWebAsync();
        Task<(decimal BuyRate, decimal SellRate)?> GetCurrencyRateAsync(Currency currency);
        Task<(decimal BuyRate, decimal SellRate)?> GetCurrencyRateAsync(string currencyCode);
    }
}
