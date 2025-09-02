using ForexExchange.Models;
using HtmlAgilityPack;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    public class WebScrapingService : IWebScrapingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebScrapingService> _logger;
        private const string BaseUrl = "https://chande.net/";

        public WebScrapingService(HttpClient httpClient, ILogger<WebScrapingService> logger, ForexDbContext context)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }




        public async Task<decimal?> GetCurrencyRateAsync(string currencyCode)
        {
            try
            {
                var url = BaseUrl;
                _logger.LogInformation("Fetching exchange rate for {Currency} from {Url}", currencyCode, url);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(content);

                // XPath selectors for each currency
                string? rateXPath = currencyCode.ToLower() switch
                {
                    "usd" => "/html/body/div[1]/div/div/div/section[1]/div/div[1]/div/div/div/div/table/tbody/tr[1]/td[3]/div/div/div/section/div/div/div/div/div/div/div/table/tbody/tr/td",
                    "eur" => "/html/body/div[1]/div/div/div/section[1]/div/div[1]/div/div/div/div/table/tbody/tr[2]/td[3]/div/div/div/section/div/div/div/div/div/div/div/table/tbody/tr/td",
                    "try" => "/html/body/div[1]/div/div/div/section[1]/div/div[1]/div/div/div/div/table/tbody/tr[5]/td[3]/div/div/div/section/div/div/div/div/div/div/div/table/tbody/tr/td",
                    "aed" => "/html/body/div[1]/div/div/div/section[1]/div/div[1]/div/div/div/div/table/tbody/tr[6]/td[3]/div/div/div/section/div/div/div/div/div/div/div/table/tbody/tr/td",
                    "omr" => "/html/body/div[1]/div/div/div/section[1]/div/div[2]/div/div/div/div/table/tbody/tr[5]/td[3]/div/div/div/section/div/div/div/div/div/div/div/table/tbody/tr/td",
                    "cny" => "/html/body/div[1]/div/div/div/section[1]/div/div[2]/div/div/div/div/table/tbody/tr[11]/td[3]/div/div/div/section/div/div/div/div/div/div/div/table/tbody/tr/td", 
                    _ => null
                };

                if (string.IsNullOrEmpty(rateXPath))
                {
                    _logger.LogWarning("No XPath defined for currency {Currency}", currencyCode);
                    return null;
                }

                var rateNode = doc.DocumentNode.SelectSingleNode(rateXPath);
                if (rateNode == null)
                {
                    _logger.LogWarning("Could not find rate node for {Currency}. XPath: {XPath}", currencyCode, rateXPath);
                    return null;
                }

                var rateText = CleanRateText(rateNode.InnerText);
                if (decimal.TryParse(rateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
                {
                    _logger.LogInformation("Successfully extracted rate for {Currency}: {Rate}", currencyCode, rate);
                    // Return same value for BuyRate and SellRate for now
                    return rate;
                }
                else
                {
                    _logger.LogWarning("Failed to parse rate for {Currency}. Text: '{RateText}'", currencyCode, rateText);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while fetching exchange rate for {Currency}", currencyCode);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while fetching exchange rate for {Currency}", currencyCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching exchange rate for {Currency}", currencyCode);
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
