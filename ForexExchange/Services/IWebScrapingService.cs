using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface IWebScrapingService
    {
        
        Task<(decimal BuyRate, decimal SellRate)?> GetCurrencyRateAsync(string currencyCode);
    }
}
