using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ForexExchange.Services
{
    public interface IBankStatementService
    {
        Task<BankStatementAnalysis> ProcessBankStatementAsync(byte[] imageData, int customerId);
        Task<List<BankStatementTransaction>> ParseBankStatementAsync(string extractedText);
    }

    public class BankStatementService : IBankStatementService
    {
        private readonly IOcrService _ocrService;
        private readonly ForexDbContext _context;
        private readonly ILogger<BankStatementService> _logger;

        public BankStatementService(
            IOcrService ocrService, 
            ForexDbContext context, 
            ILogger<BankStatementService> logger)
        {
            _ocrService = ocrService;
            _context = context;
            _logger = logger;
        }

        public async Task<BankStatementAnalysis> ProcessBankStatementAsync(byte[] imageData, int customerId)
        {
            try
            {
                // Use OCR to extract text from bank statement
                var ocrResult = await _ocrService.ProcessBankStatementAsync(imageData);
                
                if (!ocrResult.Success)
                {
                    return new BankStatementAnalysis
                    {
                        Success = false,
                        ErrorMessage = ocrResult.ErrorMessage
                    };
                }

                // Parse transactions from extracted text
                var transactions = await ParseBankStatementAsync(ocrResult.RawText ?? "");

                // Get customer's pending transactions for verification
                // TODO: Replace with AccountingDocument queries when implementing new architecture
                // var customerTransactions = new List<Models.Transaction>();
                var customerTransactions = new List<object>(); // Placeholder until new architecture
                /*
                var customerTransactions = await _context.Transactions
                    .Where(t => (t.BuyerCustomerId == customerId || t.SellerCustomerId == customerId) &&
                               (t.Status == TransactionStatus.Pending ||
                                t.Status == TransactionStatus.PaymentUploaded ||
                                t.Status == TransactionStatus.ReceiptConfirmed))
                    .ToListAsync();
                */

                // TODO: Re-implement transaction matching with new architecture
                /*
                // Try to match bank statement transactions with system transactions
                var matchedTransactions = new List<TransactionMatch>();
                foreach (var bankTx in transactions)
                {
                    var match = FindMatchingTransaction(bankTx, customerTransactions);
                    if (match != null)
                    {
                        matchedTransactions.Add(new TransactionMatch
                        {
                            BankTransaction = bankTx,
                            SystemTransaction = match,
                            MatchConfidence = CalculateMatchConfidence(bankTx, match)
                        });
                    }
                }
                */

                var matchedTransactions = new List<object>(); // Temporary placeholder

                return new BankStatementAnalysis
                {
                    Success = true,
                    CustomerId = customerId,
                    ExtractedTransactions = transactions,
                    // MatchedTransactions = matchedTransactions, // TODO: Re-implement with new architecture
                    RawText = ocrResult.RawText,
                    ProcessedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bank statement for customer {CustomerId}", customerId);
                return new BankStatementAnalysis
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public Task<List<BankStatementTransaction>> ParseBankStatementAsync(string extractedText)
        {
            var transactions = new List<BankStatementTransaction>();

            try
            {
                // Try to parse as JSON first (if OCR returned structured data)
                var jsonMatch = Regex.Match(extractedText, @"\{.*\}", RegexOptions.Singleline);
                if (jsonMatch.Success)
                {
                    using var doc = JsonDocument.Parse(jsonMatch.Value);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("transactions", out var transactionsArray))
                    {
                        foreach (var txElement in transactionsArray.EnumerateArray())
                        {
                            var transaction = new BankStatementTransaction
                            {
                                Amount = GetJsonProperty(txElement, "amount") ?? "",
                                Date = GetJsonProperty(txElement, "date") ?? "",
                                Description = GetJsonProperty(txElement, "description") ?? "",
                                Type = GetJsonProperty(txElement, "type") ?? "",
                                ReferenceId = ExtractReferenceFromDescription(GetJsonProperty(txElement, "description"))
                            };

                            if (!string.IsNullOrEmpty(transaction.Amount))
                            {
                                transactions.Add(transaction);
                            }
                        }
                    }
                }

                // Fallback: Parse using regex patterns
                if (!transactions.Any())
                {
                    transactions = ParseUsingRegexPatterns(extractedText);
                }

                return Task.FromResult(transactions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing bank statement text");
                return Task.FromResult(transactions);
            }
        }

      
        private List<BankStatementTransaction> ParseUsingRegexPatterns(string text)
        {
            var transactions = new List<BankStatementTransaction>();

            // Persian/Farsi patterns for Iranian banks
            var patterns = new[]
            {
                // Pattern 1: Date Amount Description
                @"(?<date>\d{4}/\d{1,2}/\d{1,2}|\d{1,2}/\d{1,2}/\d{4})\s+(?<amount>[\d,،]+)\s+(?<desc>[^\r\n]+)",
                
                // Pattern 2: Amount followed by date and description
                @"(?<amount>[\d,،]+)\s+(?<date>\d{4}/\d{1,2}/\d{1,2}|\d{1,2}/\d{1,2}/\d{4})\s+(?<desc>[^\r\n]+)",
                
                // Pattern 3: Look for transaction lines with common banking terms
                @"(?<desc>(?:واریز|برداشت|انتقال|پرداخت)[^\d]*?)(?<amount>[\d,،]+)[^\d]*?(?<date>\d{4}/\d{1,2}/\d{1,2}|\d{1,2}/\d{1,2}/\d{4})"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                
                foreach (Match match in matches)
                {
                    if (match.Success)
                    {
                        var transaction = new BankStatementTransaction
                        {
                            Date = match.Groups["date"].Value.Trim(),
                            Amount = match.Groups["amount"].Value.Trim(),
                            Description = match.Groups["desc"].Value.Trim(),
                            Type = DetermineTransactionType(match.Groups["desc"].Value),
                            ReferenceId = ExtractReferenceFromDescription(match.Groups["desc"].Value)
                        };

                        if (IsValidTransaction(transaction))
                        {
                            transactions.Add(transaction);
                        }
                    }
                }

                if (transactions.Any())
                    break; // Use first successful pattern
            }

            return transactions.Take(10).ToList(); // Return last 10 transactions
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

        private string DetermineTransactionType(string description)
        {
            var desc = description.ToLower();
            if (desc.Contains("واریز") || desc.Contains("deposit"))
                return "واریز";
            if (desc.Contains("برداشت") || desc.Contains("withdrawal"))
                return "برداشت";
            if (desc.Contains("انتقال") || desc.Contains("transfer"))
                return "انتقال";
            return "نامشخص";
        }

        private string? ExtractReferenceFromDescription(string? description)
        {
            if (string.IsNullOrEmpty(description))
                return null;

            // Look for reference patterns
            var patterns = new[]
            {
                @"(?:مرجع|ref|reference)[:\s]*([A-Za-z0-9]+)",
                @"(?:پیگیری|tracking)[:\s]*([A-Za-z0-9]+)",
                @"([A-Za-z0-9]{8,})" // Any alphanumeric string 8+ chars
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(description, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        private bool IsValidTransaction(BankStatementTransaction transaction)
        {
            // Validate amount
            var cleanAmount = CleanAmount(transaction.Amount);
            if (!decimal.TryParse(cleanAmount, out var amount) || amount <= 0)
                return false;

            // Validate date
            if (string.IsNullOrEmpty(transaction.Date))
                return false;

            // Validate description
            if (string.IsNullOrEmpty(transaction.Description) || transaction.Description.Length < 5)
                return false;

            return true;
        }

        private string CleanAmount(string? amount)
        {
            if (string.IsNullOrEmpty(amount))
                return "0";

            // Remove commas and Persian/Arabic numerals
            return amount
                .Replace(",", "")
                .Replace("،", "")
                .Replace(".", "")
                .Trim();
        }

        /*
        // TODO: Re-implement with new architecture
        private Transaction? FindMatchingTransaction(BankStatementTransaction bankTx, List<Transaction> systemTransactions)
        {
            var cleanAmount = CleanAmount(bankTx.Amount);
            if (!decimal.TryParse(cleanAmount, out var amount))
                return null;

            // Try to find exact amount match
            var exactMatch = systemTransactions
                .FirstOrDefault(t => Math.Abs(t.TotalInToman - amount) <= 100); // 100 Toman tolerance

            if (exactMatch != null)
                return exactMatch;

            // Try to find close amount match (within 5%)
            var closeMatch = systemTransactions
                .Where(t => Math.Abs(t.TotalInToman - amount) / t.TotalInToman <= 0.05m)
                .OrderBy(t => Math.Abs(t.TotalInToman - amount))
                .FirstOrDefault();

            return closeMatch;
        }

        private decimal CalculateMatchConfidence(BankStatementTransaction bankTx, Transaction systemTx)
        {
            var confidence = 0m;

            // Amount match confidence (40% weight)
            var cleanAmount = CleanAmount(bankTx.Amount);
            if (decimal.TryParse(cleanAmount, out var amount))
            {
                var amountDiff = Math.Abs(systemTx.TotalInToman - amount) / systemTx.TotalInToman;
                confidence += (1 - Math.Min(amountDiff, 1)) * 0.4m;
            }

            // Date proximity confidence (30% weight)
            if (DateTime.TryParse(bankTx.Date, out var bankDate))
            {
                var dateDiff = Math.Abs((systemTx.CreatedAt - bankDate).TotalDays);
                confidence += Math.Max(0, (decimal)((7 - dateDiff) / 7)) * 0.3m; // Within 7 days is good
            }

            // Reference match confidence (30% weight)
            if (!string.IsNullOrEmpty(bankTx.ReferenceId))
            {
                var systemRef = systemTx.Id.ToString();
                if (bankTx.ReferenceId.Contains(systemRef) || systemRef.Contains(bankTx.ReferenceId))
                {
                    confidence += 0.3m;
                }
            }

            return Math.Min(confidence, 1m); // Cap at 100%
        }
        */
    }

    // Supporting model classes
    public class BankStatementAnalysis
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int CustomerId { get; set; }
        public List<BankStatementTransaction> ExtractedTransactions { get; set; } = new();
        // TODO: Re-implement with new architecture
        // public List<TransactionMatch> MatchedTransactions { get; set; } = new();
        public string? RawText { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    public class BankStatementTransaction
    {
        public string Amount { get; set; } = "";
        public string Date { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string? ReferenceId { get; set; }
    }

    /*
    // TODO: Re-implement with new architecture
    public class TransactionMatch
    {
        public BankStatementTransaction BankTransaction { get; set; } = null!;
        public Transaction SystemTransaction { get; set; } = null!;
        public decimal MatchConfidence { get; set; }
    }
    */
}
