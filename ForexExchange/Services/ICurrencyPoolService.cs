using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Interface for Currency Pool Service
    /// رابط سرویس صندوق  ارزی
    /// </summary>
    public interface ICurrencyPoolService
    {
        /// <summary>
        /// Update pool balance after a transaction
        /// بروزرسانی موجودی صندوق  پس از تراکنش
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <param name="amount">Transaction amount</param>
        /// <param name="transactionType">Buy or Sell from exchange perspective</param>
        /// <param name="rate">Transaction rate</param>
        /// <returns>Updated pool</returns>
        Task<CurrencyPool> UpdatePoolAsync(int currencyId, decimal amount, PoolTransactionType transactionType, decimal rate);

        /// <summary>
        /// Get current balance for a currency
        /// دریافت موجودی فعلی برای یک ارز
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <returns>Current balance</returns>
        Task<decimal> GetPoolBalanceAsync(int currencyId);

        /// <summary>
        /// Get pool details for a currency
        /// دریافت جزئیات صندوق  برای یک ارز
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <returns>Pool details or null if not found</returns>
        Task<CurrencyPool?> GetPoolAsync(int currencyId);

        /// <summary>
        /// Get pool details by pool ID
        /// دریافت جزئیات صندوق  بر اساس شناسه صندوق 
        /// </summary>
        /// <param name="poolId">Pool ID</param>
        /// <returns>Pool details or null if not found</returns>
        Task<CurrencyPool?> GetPoolByIdAsync(int poolId);

        /// <summary>
        /// Get all active currency pools
        /// دریافت تمام صندوق های ارزی فعال
        /// </summary>
        /// <returns>List of all pools</returns>
        Task<List<CurrencyPool>> GetAllPoolsAsync();

        /// <summary>
        /// Initialize a new currency pool
        /// ایجاد صندوق  جدید برای یک ارز
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <param name="initialBalance">Initial balance (optional)</param>
        /// <returns>Created pool</returns>
        Task<CurrencyPool> CreatePoolAsync(int currencyId, decimal initialBalance = 0);

        /// <summary>
        /// Calculate total portfolio value in specified currency
        /// محاسبه ارزش کل پورتفولیو در ارز مشخص شده
        /// </summary>
        /// <param name="targetCurrencyCode">Target currency code for valuation</param>
        /// <param name="exchangeRates">Current exchange rates</param>
        /// <returns>Total portfolio value</returns>
        Task<decimal> CalculatePortfolioValueAsync(string targetCurrencyCode, Dictionary<string, decimal> exchangeRates);

        /// <summary>
        /// Get pools with high risk levels
        /// دریافت صندوق های با سطح ریسک بالا
        /// </summary>
        /// <param name="riskLevel">Minimum risk level</param>
        /// <returns>High risk pools</returns>
        Task<List<CurrencyPool>> GetHighRiskPoolsAsync(PoolRiskLevel riskLevel = PoolRiskLevel.High);

        /// <summary>
        /// Update risk levels for all pools
        /// بروزرسانی سطح ریسک برای تمام صندوق ها
        /// </summary>
        /// <param name="lowThreshold">Low risk threshold</param>
        /// <param name="highThreshold">High risk threshold</param>
        /// <returns>Number of pools updated</returns>
        Task<int> UpdateRiskLevelsAsync(decimal lowThreshold = 1000, decimal highThreshold = 5000);

        /// <summary>
        /// Get pool performance statistics
        /// دریافت آمار عملکرد صندوق 
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <param name="currentRate">Current market rate</param>
        /// <returns>Pool performance data</returns>
        Task<PoolPerformance> GetPoolPerformanceAsync(int currencyId, decimal currentRate);

        // TODO: Reimplement with AccountingDocument in new architecture
        /*
        /// <summary>
        /// Process transaction and update corresponding pools
        /// پردازش تراکنش و بروزرسانی صندوق های مربوطه
        /// </summary>
        /// <param name="transaction">Transaction to process</param>
        /// <returns>Updated pools</returns>
        Task<List<CurrencyPool>> ProcessTransactionAsync(Transaction transaction);
        */

        /// <summary>
        /// Update order counts for a currency pool
        /// بروزرسانی تعداد معاملهات برای صندوق  ارزی
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <returns>Task</returns>
        Task UpdateOrderCountsAsync(int currencyId);

        /// <summary>
        /// Update order counts for all currency pools
        /// بروزرسانی تعداد معاملهات برای همه صندوق های ارزی
        /// </summary>
        /// <returns>Task</returns>
        Task UpdateAllOrderCountsAsync();

        /// <summary>
        /// Direct pool update for admin operations
        /// بروزرسانی مستقیم صندوق  برای عملیات ادمین
        /// </summary>
        /// <param name="pool">Pool to update</param>
        /// <returns>Updated pool</returns>
        Task<CurrencyPool> UpdatePoolDirectAsync(CurrencyPool pool);

        /// <summary>
        /// Process accounting document and update currency pool balances
        /// پردازش سند حسابداری و بروزرسانی موجودی صندوق ارزی
        /// </summary>
        /// <param name="document">Accounting document</param>
        Task ProcessAccountingDocumentAsync(AccountingDocument document);

        /// <summary>
        /// Clean all pools (set to zero)
        /// پاکسازی تمام صندوق ها (تنظیم روی صفر)
        /// </summary>
        /// <returns>Success status</returns>
        Task<bool> CleanPullAsync();
    }

    /// <summary>
    /// Transaction type from exchange pool perspective
    /// نوع تراکنش از منظر صندوق  معاملات 
    /// </summary>
    public enum PoolTransactionType
    {
        /// <summary>
        /// Exchange buys currency (positive to pool)
        /// معاملات  ارز می‌خرد (مثبت برای صندوق )
        /// </summary>
        Buy,

        /// <summary>
        /// Exchange sells currency (negative to pool)
        /// معاملات  ارز می‌فروشد (منفی برای صندوق )
        /// </summary>
        Sell
    }

    /// <summary>
    /// Pool performance statistics
    /// آمار عملکرد صندوق 
    /// </summary>
    public class PoolPerformance
    {
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalLoss { get; set; }
        public decimal NetProfitLoss { get; set; }
        public decimal ProfitMargin { get; set; }
        public PoolRiskLevel RiskLevel { get; set; }
        public int TotalTransactions { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
