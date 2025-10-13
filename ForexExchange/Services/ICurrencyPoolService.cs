using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Interface for Currency Pool Service
    /// رابط سرویس داشبورد  ارزی
    /// </summary>
    public interface ICurrencyPoolService
    {
        /// <summary>
        /// Update pool balance after a transaction
        /// بروزرسانی موجودی داشبورد  پس از تراکنش
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
        /// دریافت جزئیات داشبورد  برای یک ارز
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <returns>Pool details or null if not found</returns>
        Task<CurrencyPool?> GetPoolAsync(int currencyId);

        /// <summary>
        /// Get pool details by pool ID
        /// دریافت جزئیات داشبورد  بر اساس شناسه داشبورد 
        /// </summary>
        /// <param name="poolId">Pool ID</param>
        /// <returns>Pool details or null if not found</returns>
        Task<CurrencyPool?> GetPoolByIdAsync(int poolId);

        /// <summary>
        /// Get all active currency pools
        /// دریافت تمام داشبورد های ارزی فعال
        /// </summary>
        /// <returns>List of all pools</returns>
        Task<List<CurrencyPool>> GetAllPoolsAsync();

        /// <summary>
        /// Initialize a new currency pool
        /// ایجاد داشبورد  جدید برای یک ارز
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
        /// دریافت داشبورد های با سطح ریسک بالا
        /// </summary>
        /// <param name="riskLevel">Minimum risk level</param>
        /// <returns>High risk pools</returns>
        Task<List<CurrencyPool>> GetHighRiskPoolsAsync(PoolRiskLevel riskLevel = PoolRiskLevel.High);

        /// <summary>
        /// Update risk levels for all pools
        /// بروزرسانی سطح ریسک برای تمام داشبورد ها
        /// </summary>
        /// <param name="lowThreshold">Low risk threshold</param>
        /// <param name="highThreshold">High risk threshold</param>
        /// <returns>Number of pools updated</returns>
        Task<int> UpdateRiskLevelsAsync(decimal lowThreshold = 1000, decimal highThreshold = 5000);

        /// <summary>
        /// Get pool performance statistics
        /// دریافت آمار عملکرد داشبورد 
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <param name="currentRate">Current market rate</param>
        /// <returns>Pool performance data</returns>
        Task<PoolPerformance> GetPoolPerformanceAsync(int currencyId, decimal currentRate);

        // TODO: Reimplement with AccountingDocument in new architecture
        /*
        /// <summary>
        /// Process transaction and update corresponding pools
        /// پردازش تراکنش و بروزرسانی داشبورد های مربوطه
        /// </summary>
        /// <param name="transaction">Transaction to process</param>
        /// <returns>Updated pools</returns>
        Task<List<CurrencyPool>> ProcessTransactionAsync(Transaction transaction);
        */

        /// <summary>
        /// Update order counts for a currency pool
        /// بروزرسانی تعداد معاملات  برای داشبورد  ارزی
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <returns>Task</returns>
        Task UpdateOrderCountsAsync(int currencyId);

        /// <summary>
        /// Update order counts for all currency pools
        /// بروزرسانی تعداد معاملات  برای همه داشبورد های ارزی
        /// </summary>
        /// <returns>Task</returns>
        Task UpdateAllOrderCountsAsync();

        /// <summary>
        /// Direct pool update for admin operations
        /// بروزرسانی مستقیم داشبورد  برای عملیات ادمین
        /// </summary>
        /// <param name="pool">Pool to update</param>
        /// <returns>Updated pool</returns>
        Task<CurrencyPool> UpdatePoolDirectAsync(CurrencyPool pool);

        /// <summary>
        /// Process accounting document and update currency pool balances
        /// پردازش سند حسابداری و بروزرسانی موجودی داشبورد ارزی
        /// </summary>
        /// <param name="document">Accounting document</param>
        Task ProcessAccountingDocumentAsync(AccountingDocument document);

        /// <summary>
        /// Clean all pools (reset to zero)
        /// پاکسازی تمام داشبورد ها (تنظیم روی صفر)
        /// </summary>
        /// <returns>Success status</returns>
        Task<bool> CleanPoolAsync();
    }

    /// <summary>
    /// Transaction type from exchange pool perspective
    /// نوع تراکنش از منظر داشبورد  معاملات 
    /// </summary>
    public enum PoolTransactionType
    {
        /// <summary>
        /// Exchange buys currency (positive to pool)
        /// معاملات  ارز می‌خرد (مثبت برای داشبورد )
        /// </summary>
        Buy,

        /// <summary>
        /// Exchange sells currency (negative to pool)
        /// معاملات  ارز می‌فروشد (منفی برای داشبورد )
        /// </summary>
        Sell
    }

    /// <summary>
    /// Pool performance statistics
    /// آمار عملکرد داشبورد 
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
