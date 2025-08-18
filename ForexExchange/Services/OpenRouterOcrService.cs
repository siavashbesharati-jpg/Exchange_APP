using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace ForexExchange.Services
{
    public class OpenRouterOcrService : IOcrService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenRouterOcrService> _logger;

        public OpenRouterOcrService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterOcrService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<OcrResult> ProcessReceiptAsync(byte[] imageData)
        {
            return await ProcessImageAsync(imageData, "receipt");
        }

        public async Task<OcrResult> ProcessBankStatementAsync(byte[] imageData)
        {
            return await ProcessImageAsync(imageData, "bank_statement");
        }

        private async Task<OcrResult> ProcessImageAsync(byte[] imageData, string documentType)
        {
            try
            {
                var apiKey = _configuration["OpenRouter:ApiKey"];
                var baseUrl = _configuration["OpenRouter:BaseUrl"];
                var model = _configuration["OpenRouter:Model"];

                if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_OPENROUTER_API_KEY")
                {
                    _logger.LogWarning("OpenRouter API key not configured, using mock OCR");
                    return CreateMockOcrResult(documentType);
                }

                // Convert image to base64
                var base64Image = Convert.ToBase64String(imageData);
                var imageFormat = GetImageFormat(imageData);

                // Create the prompt based on document type
                var prompt = documentType == "receipt" 
                    ? CreateReceiptPrompt() 
                    : CreateBankStatementPrompt();

                // Prepare the request
                var requestBody = new
                {
                    model = model,
                    messages = new object[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "text",
                                    text = prompt
                                },
                                new
                                {
                                    type = "image_url",
                                    image_url = new
                                    {
                                        url = $"data:image/{imageFormat};base64,{base64Image}"
                                    }
                                }
                            }
                        }
                    },
                    max_tokens = _configuration.GetValue<int>("OpenRouter:MaxTokens", 1000),
                    temperature = _configuration.GetValue<double>("OpenRouter:Temperature", 0.1)
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://iranexpedia.com");
                _httpClient.DefaultRequestHeaders.Add("X-Title", "IranExpedia Forex System");

                var response = await _httpClient.PostAsync($"{baseUrl}/chat/completions", httpContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return ParseOpenRouterResponse(responseContent);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"OpenRouter API error: {response.StatusCode} - {errorContent}");
                    return new OcrResult
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image with OpenRouter");
                return new OcrResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string CreateReceiptPrompt()
        {
            return @"لطفاً این تصویر رسید بانکی را تجزیه و تحلیل کنید و اطلاعات زیر را استخراج کنید:

1. مبلغ تراکنش (عدد کامل)
2. شماره مرجع یا شماره پیگیری
3. تاریخ تراکنش
4. شماره حساب مقصد یا مبدأ

لطفاً پاسخ را به صورت JSON با فرمت زیر ارائه دهید:
{
  ""amount"": ""مبلغ به عدد"",
  ""reference_id"": ""شماره مرجع"",
  ""date"": ""تاریخ"",
  ""account_number"": ""شماره حساب"",
  ""raw_text"": ""تمام متن قابل خواندن از تصویر""
}

اگر اطلاعاتی پیدا نکردید، مقدار null بگذارید.";
        }

        private string CreateBankStatementPrompt()
        {
            return @"لطفاً این تصویر گردش حساب بانکی را تجزیه و تحلیل کنید و اطلاعات زیر را استخراج کنید:

1. آخرین ۱۰ تراکنش
2. مبلغ هر تراکنش
3. تاریخ هر تراکنش
4. شرح تراکنش
5. موجودی حساب

لطفاً پاسخ را به صورت JSON با فرمت زیر ارائه دهید:
{
  ""transactions"": [
    {
      ""amount"": ""مبلغ"",
      ""date"": ""تاریخ"",
      ""description"": ""شرح"",
      ""type"": ""واریز یا برداشت""
    }
  ],
  ""balance"": ""موجودی حساب"",
  ""account_number"": ""شماره حساب"",
  ""raw_text"": ""تمام متن قابل خواندن از تصویر""
}";
        }

        private OcrResult ParseOpenRouterResponse(string responseContent)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) &&
                        message.TryGetProperty("content", out var content))
                    {
                        var textContent = content.GetString();
                        return ParseExtractedData(textContent);
                    }
                }

                return new OcrResult
                {
                    Success = false,
                    ErrorMessage = "Invalid response format from OpenRouter"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing OpenRouter response");
                return new OcrResult
                {
                    Success = false,
                    ErrorMessage = "Error parsing API response"
                };
            }
        }

        private OcrResult ParseExtractedData(string? extractedText)
        {
            if (string.IsNullOrEmpty(extractedText))
            {
                return new OcrResult { Success = false, ErrorMessage = "No text extracted" };
            }

            try
            {
                // Try to parse as JSON first
                var jsonMatch = Regex.Match(extractedText, @"\{.*\}", RegexOptions.Singleline);
                if (jsonMatch.Success)
                {
                    using var doc = JsonDocument.Parse(jsonMatch.Value);
                    var root = doc.RootElement;

                    return new OcrResult
                    {
                        Success = true,
                        RawText = extractedText,
                        Amount = GetJsonProperty(root, "amount"),
                        ReferenceId = GetJsonProperty(root, "reference_id"),
                        Date = GetJsonProperty(root, "date"),
                        AccountNumber = GetJsonProperty(root, "account_number")
                    };
                }

                // Fallback: extract using regex patterns
                return ExtractUsingRegex(extractedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing extracted data");
                return new OcrResult
                {
                    Success = true,
                    RawText = extractedText,
                    ErrorMessage = "Could not parse structured data, raw text available"
                };
            }
        }

        private string? GetJsonProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property))
            {
                var value = property.GetString();
                return string.IsNullOrEmpty(value) || value == "null" ? null : value;
            }
            return null;
        }

        private OcrResult ExtractUsingRegex(string text)
        {
            // Persian and English number patterns
            var amountPattern = @"(?:مبلغ|amount|Amount)[:\s]*([0-9,،]+(?:\.[0-9]+)?)";
            var referencePattern = @"(?:مرجع|reference|پیگیری|tracking)[:\s]*([A-Za-z0-9]+)";
            var datePattern = @"(?:تاریخ|date|Date)[:\s]*([0-9/\-۰-۹]+)";
            var accountPattern = @"(?:حساب|account)[:\s]*([0-9]+)";

            var amountMatch = Regex.Match(text, amountPattern, RegexOptions.IgnoreCase);
            var referenceMatch = Regex.Match(text, referencePattern, RegexOptions.IgnoreCase);
            var dateMatch = Regex.Match(text, datePattern, RegexOptions.IgnoreCase);
            var accountMatch = Regex.Match(text, accountPattern, RegexOptions.IgnoreCase);

            return new OcrResult
            {
                Success = true,
                RawText = text,
                Amount = amountMatch.Success ? amountMatch.Groups[1].Value.Trim() : null,
                ReferenceId = referenceMatch.Success ? referenceMatch.Groups[1].Value.Trim() : null,
                Date = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : null,
                AccountNumber = accountMatch.Success ? accountMatch.Groups[1].Value.Trim() : null
            };
        }

        private string GetImageFormat(byte[] imageData)
        {
            // Simple image format detection based on file headers
            if (imageData.Length >= 4)
            {
                if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
                    return "jpeg";
                if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
                    return "png";
                if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46)
                    return "gif";
            }
            return "jpeg"; // default
        }

        private OcrResult CreateMockOcrResult(string documentType)
        {
            // Create mock data when API key is not configured
            if (documentType == "receipt")
            {
                return new OcrResult
                {
                    Success = true,
                    RawText = "رسید تست - شماره مرجع: 123456789 - مبلغ: 1,000,000 تومان - تاریخ: 1403/05/28",
                    Amount = "1000000",
                    ReferenceId = "123456789",
                    Date = "1403/05/28",
                    AccountNumber = "6037-9918-1234-5678"
                };
            }
            else
            {
                return new OcrResult
                {
                    Success = true,
                    RawText = "گردش حساب تست - موجودی: 5,000,000 تومان - آخرین تراکنش: 2,000,000 تومان",
                    Amount = "2000000",
                    Date = "1403/05/28",
                    AccountNumber = "6037-9918-1234-5678"
                };
            }
        }
    }
}
