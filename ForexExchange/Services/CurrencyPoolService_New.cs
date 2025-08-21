using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    /// <summary>
    /// Currency Pool Service Implementation for Cross-Currency Trading
    /// پیاده‌سازی سرویس استخر ارزی برای تجارت متقابل ارزها
    /// </summary>
    public class CurrencyPoolService : ICurrencyPoolService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CurrencyPoolService> _logger;

        public CurrencyPoolService(ForexDbContext context, ILogger<CurrencyPoolService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Update pool balance after a transaction
        /// بروزرسانی موجودی استخر پس از تراکنش
        /// </summary>
        public async Task<CurrencyPool> UpdatePoolAsync(CurrencyType currency, decimal amount, PoolTransactionType transactionType, decimal rate)
        {
            var pool = await GetPoolAsync(currency);
            
            if (pool == null)
            {
                pool = await CreatePoolAsync(currency);
            }

            // Update balances based on transaction type
            if (transactionType == PoolTransactionType.Buy)
            {
                // Exchange buys currency - increases pool balance
                pool.Balance += amount;
                pool.TotalBought += amount;
                
                // Update weighted average buy rate
                if (pool.TotalBought > 0)
                {
                    decimal previousBuyValue = (pool.TotalBought - amount) * (pool.AverageBuyRate ?? 0);
                    decimal currentBuyValue = amount * rate;
                    pool.AverageBuyRate = (previousBuyValue + currentBuyValue) / pool.TotalBought;
                }
            }
            else
            {
                // Exchange sells currency - decreases pool balance
                pool.Balance -= amount;
                pool.TotalSold += amount;
                
                // Update weighted average sell rate
                if (pool.TotalSold > 0)
                {
                    decimal previousSellValue = (pool.TotalSold - amount) * (pool.AverageSellRate ?? 0);
                    decimal currentSellValue = amount * rate;
                    pool.AverageSellRate = (previousSellValue + currentSellValue) / pool.TotalSold;
                }
            }

            pool.LastUpdated = DateTime.Now;
            
            // Update risk level
            await UpdatePoolRiskLevel(pool);
            
            _context.CurrencyPools.Update(pool);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Pool updated for {currency}: Balance={pool.Balance}, Type={transactionType}, Amount={amount}");
            
            return pool;
        }

        /// <summary>
        /// Get current balance for a currency
        /// دریافت موجودی فعلی برای یک ارز
        /// </summary>
        public async Task<decimal> GetPoolBalanceAsync(CurrencyType currency)
        {
            var pool = await _context.CurrencyPools
                .FirstOrDefaultAsync(p => p.Currency == currency && p.IsActive);
            
            return pool?.Balance ?? 0;
        }

        /// <summary>
        /// Get pool details for a currency
        /// دریافت جزئیات استخر برای یک ارز
        /// </summary>
        public async Task<CurrencyPool?> GetPoolAsync(CurrencyType currency)
        {
            return await _context.CurrencyPools
                .FirstOrDefaultAsync(p => p.Currency == currency && p.IsActive);
        }

        /// <summary>
        /// Get all active currency pools
        /// دریافت تمام استخرهای ارزی فعال
        /// </summary>
        public async Task<List<CurrencyPool>> GetAllPoolsAsync()
        {
            return await _context.CurrencyPools
                .Where(p => p.IsActive)
                .OrderBy(p => p.Currency)
                .ToListAsync();
        }

        /// <summary>
        /// Initialize a new currency pool
        /// ایجاد استخر جدید برای یک ارز
        /// </summary>
        public async Task<CurrencyPool> CreatePoolAsync(CurrencyType currency, decimal initialBalance = 0)
        {
            var existingPool = await GetPoolAsync(currency);
            if (existingPool != null)
            {
                return existingPool;
            }

            var pool = new CurrencyPool
            {
                Currency = currency,
                Balance = initialBalance,
                TotalBought = initialBalance > 0 ? initialBalance : 0,
                TotalSold = 0,
                AverageBuyRate = null,
                AverageSellRate = null,
                LastUpdated = DateTime.Now,
                RiskLevel = PoolRiskLevel.Low,
                IsActive = true,
                Notes = $"Auto-created pool for {currency}"
            };

            _context.CurrencyPools.Add(pool);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created new pool for {currency} with initial balance {initialBalance}");
            
            return pool;
        }

        /// <summary>
        /// Calculate total portfolio value in specified currency
        /// محاسبه ارزش کل پورتفولیو در ارز مشخص شده
        /// </summary>
        public async Task<decimal> CalculatePortfolioValueAsync(CurrencyType targetCurrency, Dictionary<string, decimal> exchangeRates)
        {
            var pools = await GetAllPoolsAsync();
            decimal totalValue = 0;

            foreach (var pool in pools)
            {
                if (pool.Currency == targetCurrency)
                {
                    // Same currency - no conversion needed
                    totalValue += pool.Balance;
                }
                else if (exchangeRates.TryGetValue(pool.Currency.ToString(), out var rate))
                {
                    totalValue += pool.CalculateCurrentPositionValue(targetCurrency, rate);
                }
            }

            return totalValue;
        }

        /// <summary>
        /// Get pools with high risk levels
        /// دریافت استخرهای با سطح ریسک بالا
        /// </summary>
        public async Task<List<CurrencyPool>> GetHighRiskPoolsAsync(PoolRiskLevel riskLevel = PoolRiskLevel.High)
        {
            return await _context.CurrencyPools
                .Where(p => p.IsActive && p.RiskLevel >= riskLevel)
                .OrderByDescending(p => p.RiskLevel)
                .ThenBy(p => p.Currency)
                .ToListAsync();
        }

        /// <summary>
        /// Update risk levels for all pools
        /// بروزرسانی سطح ریسک برای تمام استخرها
        /// </summary>
        public async Task<int> UpdateRiskLevelsAsync(decimal lowThreshold = 1000, decimal highThreshold = 5000)
        {
            var pools = await GetAllPoolsAsync();
            int updatedCount = 0;

            foreach (var pool in pools)
            {
                var previousRiskLevel = pool.RiskLevel;
                await UpdatePoolRiskLevel(pool, lowThreshold, highThreshold);
                
                if (pool.RiskLevel != previousRiskLevel)
                {
                    updatedCount++;
                    _context.CurrencyPools.Update(pool);
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Updated risk levels for {updatedCount} pools");
            }

            return updatedCount;
        }

        /// <summary>
        /// Get pool performance statistics
        /// دریافت آمار عملکرد استخر
        /// </summary>
        public async Task<PoolPerformance> GetPoolPerformanceAsync(CurrencyType currency, decimal currentRate)
        {
            var pool = await GetPoolAsync(currency);
            if (pool == null)
            {
                return new PoolPerformance { Currency = currency };
            }

            var performance = new PoolPerformance
            {
                Currency = pool.Currency,
                CurrentBalance = pool.Balance,
                CurrentValue = pool.CalculateCurrentPositionValue(CurrencyType.Toman, currentRate),
                NetProfitLoss = pool.CalculateNetProfitLoss(),
                RiskLevel = pool.RiskLevel,
                LastUpdated = pool.LastUpdated
            };

            // Calculate additional metrics
            var totalValue = pool.TotalBought * (pool.AverageBuyRate ?? 0) + pool.TotalSold * (pool.AverageSellRate ?? 0);
            performance.ProfitMargin = totalValue > 0 ? (performance.NetProfitLoss / totalValue) * 100 : 0;

            return performance;
        }

        /// <summary>
        /// Process transaction and update corresponding pools
        /// پردازش تراکنش و بروزرسانی استخرهای مربوطه
        /// </summary>
        public async Task<List<CurrencyPool>> ProcessTransactionAsync(Transaction transaction)
        {
            var updatedPools = new List<CurrencyPool>();

            // For cross-currency transactions, we need to update both currency pools
            var buyOrder = await _context.Orders.FindAsync(transaction.BuyOrderId);
            var sellOrder = await _context.Orders.FindAsync(transaction.SellOrderId);

            if (buyOrder != null && sellOrder != null)
            {
                // Update pools based on the exchange perspective
                // When a customer buys USD with Toman, exchange sells USD and buys Toman
                var fromCurrencyPool = await UpdatePoolAsync(buyOrder.FromCurrency, transaction.Amount, PoolTransactionType.Sell, transaction.Rate);
                var toCurrencyPool = await UpdatePoolAsync(buyOrder.ToCurrency, transaction.TotalAmount, PoolTransactionType.Buy, 1.0m);

                updatedPools.Add(fromCurrencyPool);
                if (toCurrencyPool.Id != fromCurrencyPool.Id)
                {
                    updatedPools.Add(toCurrencyPool);
                }
            }

            return updatedPools;
        }

        /// <summary>
        /// Update risk level for a single pool
        /// بروزرسانی سطح ریسک برای یک استخر
        /// </summary>
        private async Task UpdatePoolRiskLevel(CurrencyPool pool, decimal lowThreshold = 1000, decimal highThreshold = 5000)
        {
            decimal absBalance = Math.Abs(pool.Balance);
            
            if (absBalance <= lowThreshold)
                pool.RiskLevel = PoolRiskLevel.Low;
            else if (absBalance <= highThreshold)
                pool.RiskLevel = PoolRiskLevel.Medium;
            else if (absBalance <= highThreshold * 2)
                pool.RiskLevel = PoolRiskLevel.High;
            else
                pool.RiskLevel = PoolRiskLevel.Critical;

            await Task.CompletedTask; // For async consistency
        }
    }
}
