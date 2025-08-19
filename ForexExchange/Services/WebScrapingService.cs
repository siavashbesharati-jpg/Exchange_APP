using ForexExchange.Models;
using HtmlAgilityPack;
using System.Globalization;

namespace ForexExchange.Services
{
    public class WebScrapingService : IWebScrapingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebScrapingService> _logger;

        private readonly Dictionary<CurrencyType, string> _currencyUrls = new()
        {
            { CurrencyType.USD, "https://alanchand.com/currencies-price/usd" },
            { CurrencyType.EUR, "https://alanchand.com/currencies-price/eur" },
            { CurrencyType.AED, "https://alanchand.com/currencies-price/aed" },
            { CurrencyType.OMR, "https://alanchand.com/currencies-price/omr" },
            { CurrencyType.TRY, "https://alanchand.com/currencies-price/try" }
        };

        public WebScrapingService(HttpClient httpClient, ILogger<WebScrapingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<Dictionary<CurrencyType, (decimal BuyRate, decimal SellRate)>> GetExchangeRatesFromWebAsync()
        {
            var results = new Dictionary<CurrencyType, (decimal BuyRate, decimal SellRate)>();

            foreach (var currency in _currencyUrls.Keys)
            {
                try
                {
                    var rates = await GetCurrencyRateAsync(currency);
                    if (rates.HasValue)
                    {
                        results[currency] = rates.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get exchange rate for {Currency}", currency);
                }
            }

            return results;
        }

        public async Task<(decimal BuyRate, decimal SellRate)?> GetCurrencyRateAsync(CurrencyType currency)
        {
            if (!_currencyUrls.ContainsKey(currency))
            {
                _logger.LogWarning("No URL configured for currency {Currency}", currency);
                return null;
            }

            try
            {
                var url = _currencyUrls[currency];
                _logger.LogInformation("Fetching exchange rate for {Currency} from {Url}", currency, url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                // XPath selectors for buy and sell rates
                var buyRateXPath = "/html/body/main/section[2]/div/div/div[2]/div/div/div/div/span";
                var sellRateXPath = "/html/body/main/section[2]/div/div/div[1]/div/div/div/div/span[1]";

                var buyRateNode = doc.DocumentNode.SelectSingleNode(buyRateXPath);
                var sellRateNode = doc.DocumentNode.SelectSingleNode(sellRateXPath);

                if (buyRateNode == null || sellRateNode == null)
                {
                    _logger.LogWarning("Could not find rate nodes for {Currency}. Buy node: {BuyNode}, Sell node: {SellNode}", 
                        currency, buyRateNode != null, sellRateNode != null);
                    return null;
                }

                var buyRateText = CleanRateText(buyRateNode.InnerText);
                var sellRateText = CleanRateText(sellRateNode.InnerText);

                if (decimal.TryParse(buyRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var buyRate) &&
                    decimal.TryParse(sellRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var sellRate))
                {
                    _logger.LogInformation("Successfully extracted rates for {Currency}: Buy={BuyRate}, Sell={SellRate}", 
                        currency, buyRate, sellRate);
                    return (buyRate, sellRate);
                }
                else
                {
                    _logger.LogWarning("Failed to parse rates for {Currency}. Buy text: '{BuyText}', Sell text: '{SellText}'", 
                        currency, buyRateText, sellRateText);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while fetching exchange rate for {Currency}", currency);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while fetching exchange rate for {Currency}", currency);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching exchange rate for {Currency}", currency);
                return null;
            }
        }

        private string CleanRateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove common separators and non-numeric characters except decimal point
            text = text.Trim()
                      .Replace(",", "")  // Remove thousand separators
                      .Replace("٬", "")  // Remove Persian thousand separators
                      .Replace("۱", "1")  // Replace Persian digits
                      .Replace("۲", "2")
                      .Replace("۳", "3")
                      .Replace("۴", "4")
                      .Replace("۵", "5")
                      .Replace("۶", "6")
                      .Replace("۷", "7")
                      .Replace("۸", "8")
                      .Replace("۹", "9")
                      .Replace("۰", "0");

            // Extract only numbers and decimal point
            var cleanText = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());

            return cleanText;
        }
    }
}
