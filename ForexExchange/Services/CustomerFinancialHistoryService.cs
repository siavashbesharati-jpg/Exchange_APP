using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Customer Financial History Service
    /// Reconstructs complete transaction timeline from existing Orders and AccountingDocuments
    /// NO DATABASE CHANGES NEEDED - Pure computation from existing data
    /// </summary>
    public class CustomerFinancialHistoryService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CustomerFinancialHistoryService> _logger;

        public CustomerFinancialHistoryService(ForexDbContext context, ILogger<CustomerFinancialHistoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get complete financial timeline for a customer
        /// This reconstructs the entire history from Orders and AccountingDocuments
        /// </summary>
        public async Task<CustomerFinancialTimeline> GetCustomerTimelineAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                    throw new ArgumentException($"Customer with ID {customerId} not found");

                fromDate ??= DateTime.MinValue;
                toDate ??= DateTime.MaxValue;

                var timeline = new CustomerFinancialTimeline
                {
                    CustomerId = customerId,
                    CustomerName = customer.FullName,
                    FromDate = fromDate.Value,
                    ToDate = toDate.Value
                };

                // Step 1: Get all current balances as "virtual" initial balances
                var currentBalances = await _context.CustomerBalances
                    .Where(cb => cb.CustomerId == customerId)
                    .ToDictionaryAsync(cb => cb.CurrencyCode, cb => cb.Balance);

                // Step 2: Collect all transactions from Orders and Documents
                var allTransactions = new List<CustomerTransactionHistory>();

                // Add Order transactions
                var orders = await _context.Orders
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.CustomerId == customerId && 
                               o.CreatedAt >= fromDate && 
                               o.CreatedAt <= toDate)
                    .OrderBy(o => o.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation($"Found {orders.Count} orders for customer {customerId} between {fromDate} and {toDate}");

                foreach (var order in orders)
                {
                    // Every order affects both currencies:
                    // 1. Customer PAYS the source currency (Amount)
                    // 2. Customer RECEIVES the target currency (TotalAmount)
                    
                    // Payment transaction (deduct from source currency - what customer pays)
                    allTransactions.Add(new CustomerTransactionHistory
                    {
                        CustomerId = customerId,
                        TransactionDate = order.CreatedAt,
                        Type = TransactionType.OrderSell,
                        Description = $"پرداخت {order.FromAmount:N2} {order.FromCurrency.Code} - سفارش #{order.Id}",
                        CurrencyCode = order.FromCurrency.Code,
                        Amount = -order.FromAmount, // Negative because customer pays this
                        ReferenceId = order.Id,
                        FromCurrency = order.FromCurrency.Code,
                        ToCurrency = order.ToCurrency.Code,
                        ExchangeRate = order.Rate,
                        Notes = $"Order #{order.Id} - Payment"
                    });

                    // Receipt transaction (add to target currency - what customer receives)
                    allTransactions.Add(new CustomerTransactionHistory
                    {
                        CustomerId = customerId,
                        TransactionDate = order.CreatedAt,
                        Type = TransactionType.OrderBuy,
                        Description = $"دریافت {order.ToAmount:N2} {order.ToCurrency.Code} - سفارش #{order.Id}",
                        CurrencyCode = order.ToCurrency.Code,
                        Amount = order.ToAmount, // Positive because customer receives this
                        ReferenceId = order.Id,
                        FromCurrency = order.FromCurrency.Code,
                        ToCurrency = order.ToCurrency.Code,
                        ExchangeRate = order.Rate,
                        Notes = $"Order #{order.Id} - Receipt"
                    });
                }

                // Add AccountingDocument transactions
                var documents = await _context.AccountingDocuments
                    .Include(d => d.PayerCustomer)
                    .Include(d => d.ReceiverCustomer)
                    .Where(d => (d.PayerCustomerId == customerId || d.ReceiverCustomerId == customerId) &&
                               d.DocumentDate >= fromDate &&
                               d.DocumentDate <= toDate &&
                               d.IsVerified) // Only verified documents
                    .OrderBy(d => d.DocumentDate)
                    .ToListAsync();

                foreach (var doc in documents)
                {
                    var isCustomerPayer = doc.PayerCustomerId == customerId;
                    var amount = isCustomerPayer ? -doc.Amount : doc.Amount;
                    var type = isCustomerPayer ? TransactionType.DocumentDebit : TransactionType.DocumentCredit;
                    var description = isCustomerPayer ? 
                        $"پرداخت {doc.Amount:N0} {doc.CurrencyCode} - {doc.Title}" :
                        $"دریافت {doc.Amount:N0} {doc.CurrencyCode} - {doc.Title}";

                    allTransactions.Add(new CustomerTransactionHistory
                    {
                        CustomerId = customerId,
                        TransactionDate = doc.DocumentDate,
                        Type = type,
                        Description = description,
                        CurrencyCode = doc.CurrencyCode,
                        Amount = amount,
                        ReferenceId = doc.Id,
                        Notes = $"Document #{doc.Id} - {doc.Description}"
                    });
                }

                // Step 3: Sort all transactions by date and calculate running balances
                allTransactions = allTransactions.OrderBy(t => t.TransactionDate).ToList();

                // Step 4: Calculate initial balances by working backwards from current balances
                var initialBalances = await CalculateInitialBalancesAsync(customerId, allTransactions, currentBalances);
                timeline.InitialBalances = initialBalances;

                // Step 5: Calculate running balances for each transaction
                var runningBalances = new Dictionary<string, decimal>(initialBalances);

                foreach (var transaction in allTransactions)
                {
                    if (!runningBalances.ContainsKey(transaction.CurrencyCode))
                        runningBalances[transaction.CurrencyCode] = 0;

                    runningBalances[transaction.CurrencyCode] += transaction.Amount;
                    transaction.RunningBalance = runningBalances[transaction.CurrencyCode];
                }

                timeline.Transactions = allTransactions;
                timeline.FinalBalances = new Dictionary<string, decimal>(runningBalances);

                // Calculate net changes
                timeline.NetChanges = timeline.FinalBalances.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value - (timeline.InitialBalances.ContainsKey(kvp.Key) ? timeline.InitialBalances[kvp.Key] : 0)
                );

                return timeline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating customer financial timeline for customer {CustomerId}", customerId);
                throw;
            }
        }

        /// <summary>
        /// Get balance snapshot at a specific date
        /// </summary>
        public async Task<CustomerBalanceSnapshot> GetBalanceSnapshotAsync(int customerId, DateTime asOfDate)
        {
            var timeline = await GetCustomerTimelineAsync(customerId, DateTime.MinValue, asOfDate);
            
            var snapshot = new CustomerBalanceSnapshot
            {
                CustomerId = customerId,
                AsOfDate = asOfDate,
                Balances = timeline.FinalBalances,
                RecentTransactions = timeline.Transactions
                    .Where(t => t.TransactionDate >= asOfDate.AddDays(-30))
                    .OrderByDescending(t => t.TransactionDate)
                    .Take(10)
                    .ToList()
            };

            return snapshot;
        }

        /// <summary>
        /// Calculate what the initial balances must have been based on current balances and all transactions
        /// This works backwards from current state
        /// </summary>
        private async Task<Dictionary<string, decimal>> CalculateInitialBalancesAsync(
            int customerId, 
            List<CustomerTransactionHistory> transactions, 
            Dictionary<string, decimal> currentBalances)
        {
            var initialBalances = new Dictionary<string, decimal>(currentBalances);

            // Work backwards through all transactions to find initial state
            foreach (var transaction in transactions.OrderByDescending(t => t.TransactionDate))
            {
                if (!initialBalances.ContainsKey(transaction.CurrencyCode))
                    initialBalances[transaction.CurrencyCode] = 0;

                // Reverse the transaction effect
                initialBalances[transaction.CurrencyCode] -= transaction.Amount;
            }

            return initialBalances;
        }

        /// <summary>
        /// Get summary statistics for customer transactions
        /// </summary>
        public async Task<Dictionary<string, object>> GetCustomerStatsAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var timeline = await GetCustomerTimelineAsync(customerId, fromDate, toDate);

            var stats = new Dictionary<string, object>
            {
                ["TotalTransactions"] = timeline.TotalTransactions,
                ["OrderTransactions"] = timeline.OrderCount,
                ["DocumentTransactions"] = timeline.DocumentCount,
                ["CurrenciesInvolved"] = timeline.CurrenciesInvolved.Count,
                ["TotalVolume"] = timeline.Transactions
                    .Where(t => t.Amount > 0)
                    .GroupBy(t => t.CurrencyCode)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount)),
                ["NetChanges"] = timeline.NetChanges,
                ["DateRange"] = new { From = timeline.FromDate, To = timeline.ToDate }
            };

            return stats;
        }

        /// <summary>
        /// Get transaction summary grouped by currency
        /// </summary>
        public async Task<Dictionary<string, CurrencyTransactionSummary>> GetCurrencyTransactionSummaryAsync(
            int customerId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var timeline = await GetCustomerTimelineAsync(customerId, fromDate, toDate);

            var summary = timeline.Transactions
                .GroupBy(t => t.CurrencyCode)
                .ToDictionary(g => g.Key, g => new CurrencyTransactionSummary
                {
                    CurrencyCode = g.Key,
                    TotalDebits = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                    TotalCredits = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    TransactionCount = g.Count(),
                    OrderTransactions = g.Count(t => t.Type == TransactionType.OrderSell || t.Type == TransactionType.OrderBuy),
                    DocumentTransactions = g.Count(t => t.Type == TransactionType.DocumentCredit || t.Type == TransactionType.DocumentDebit),
                    NetChange = g.Sum(t => t.Amount),
                    LastTransactionDate = g.Max(t => t.TransactionDate)
                });

            return summary;
        }
    }

    /// <summary>
    /// Currency-specific transaction summary
    /// </summary>
    public class CurrencyTransactionSummary
    {
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public int TransactionCount { get; set; }
        public int OrderTransactions { get; set; }
        public int DocumentTransactions { get; set; }
        public decimal NetChange { get; set; }
        public DateTime LastTransactionDate { get; set; }
        
        public decimal TotalVolume => TotalDebits + TotalCredits;
        public string NetChangeStatus => NetChange >= 0 ? "افزایش" : "کاهش";
    }
}
