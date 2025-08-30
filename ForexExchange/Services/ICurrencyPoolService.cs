using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Interface for Currency Pool Service
    /// رابط سرویس استخر ارزی
    /// </summary>
    public interface ICurrencyPoolService
    {
        /// <summary>
        /// Update pool balance after a transaction
        /// بروزرسانی موجودی استخر پس از تراکنش
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
        /// دریافت جزئیات استخر برای یک ارز
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <returns>Pool details or null if not found</returns>
        Task<CurrencyPool?> GetPoolAsync(int currencyId);

        /// <summary>
        /// Get all active currency pools
        /// دریافت تمام استخرهای ارزی فعال
        /// </summary>
        /// <returns>List of all pools</returns>
        Task<List<CurrencyPool>> GetAllPoolsAsync();

        /// <summary>
        /// Initialize a new currency pool
        /// ایجاد استخر جدید برای یک ارز
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
        /// دریافت استخرهای با سطح ریسک بالا
        /// </summary>
        /// <param name="riskLevel">Minimum risk level</param>
        /// <returns>High risk pools</returns>
        Task<List<CurrencyPool>> GetHighRiskPoolsAsync(PoolRiskLevel riskLevel = PoolRiskLevel.High);

        /// <summary>
        /// Update risk levels for all pools
        /// بروزرسانی سطح ریسک برای تمام استخرها
        /// </summary>
        /// <param name="lowThreshold">Low risk threshold</param>
        /// <param name="highThreshold">High risk threshold</param>
        /// <returns>Number of pools updated</returns>
        Task<int> UpdateRiskLevelsAsync(decimal lowThreshold = 1000, decimal highThreshold = 5000);

        /// <summary>
        /// Get pool performance statistics
        /// دریافت آمار عملکرد استخر
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <param name="currentRate">Current market rate</param>
        /// <returns>Pool performance data</returns>
        Task<PoolPerformance> GetPoolPerformanceAsync(int currencyId, decimal currentRate);

        /// <summary>
        /// Process transaction and update corresponding pools
        /// پردازش تراکنش و بروزرسانی استخرهای مربوطه
        /// </summary>
        /// <param name="transaction">Transaction to process</param>
        /// <returns>Updated pools</returns>
        Task<List<CurrencyPool>> ProcessTransactionAsync(Transaction transaction);

        /// <summary>
        /// Update order counts for a currency pool
        /// بروزرسانی تعداد معاملهات برای استخر ارزی
        /// </summary>
        /// <param name="currencyId">Currency ID</param>
        /// <returns>Task</returns>
        Task UpdateOrderCountsAsync(int currencyId);

        /// <summary>
        /// Update order counts for all currency pools
        /// بروزرسانی تعداد معاملهات برای همه استخرهای ارزی
        /// </summary>
        /// <returns>Task</returns>
        Task UpdateAllOrderCountsAsync();
    }

    /// <summary>
    /// Transaction type from exchange pool perspective
    /// نوع تراکنش از منظر استخر صرافی
    /// </summary>
    public enum PoolTransactionType
    {
        /// <summary>
        /// Exchange buys currency (positive to pool)
        /// صرافی ارز می‌خرد (مثبت برای استخر)
        /// </summary>
        Buy,

        /// <summary>
        /// Exchange sells currency (negative to pool)
        /// صرافی ارز می‌فروشد (منفی برای استخر)
        /// </summary>
        Sell
    }

    /// <summary>
    /// Pool performance statistics
    /// آمار عملکرد استخر
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
