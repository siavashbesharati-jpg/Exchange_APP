using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface IWebScrapingService
    {
        
        Task<decimal?> GetCurrencyRateAsync(string currencyCode);
    }
}
