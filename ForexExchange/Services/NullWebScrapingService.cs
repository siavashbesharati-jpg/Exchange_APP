using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Null implementation of IWebScrapingService that does nothing
    /// Used when web scraping is disabled
    /// </summary>
    public class NullWebScrapingService : IWebScrapingService
    {
        public Task<decimal?> GetCurrencyRateAsync(string currencyCode)
        {
            // Return null to indicate no rate was found
            return Task.FromResult<decimal?>(null);
        }
    }
}