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
        public async Task<CurrencyPool> UpdatePoolAsync(int currencyId, decimal amount, PoolTransactionType transactionType, decimal rate)
        {
            var pool = await GetPoolAsync(currencyId);
            
            if (pool == null)
            {
                pool = await CreatePoolAsync(currencyId);
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

            _logger.LogInformation($"Pool updated for currency ID {currencyId}: Balance={pool.Balance}, Type={transactionType}, Amount={amount}");
            
            return pool;
        }

        /// <summary>
        /// Get current balance for a currency
        /// دریافت موجودی فعلی برای یک ارز
        /// </summary>
        public async Task<decimal> GetPoolBalanceAsync(int currencyId)
        {
            var pool = await _context.CurrencyPools
                .FirstOrDefaultAsync(p => p.CurrencyId == currencyId && p.IsActive);
            
            return pool?.Balance ?? 0;
        }

        /// <summary>
        /// Get pool details for a currency
        /// دریافت جزئیات استخر برای یک ارز
        /// </summary>
        public async Task<CurrencyPool?> GetPoolAsync(int currencyId)
        {
            return await _context.CurrencyPools
                .Include(p => p.Currency)
                .FirstOrDefaultAsync(p => p.CurrencyId == currencyId && p.IsActive);
        }

        /// <summary>
        /// Get all active currency pools
        /// دریافت تمام استخرهای ارزی فعال
        /// </summary>
        public async Task<List<CurrencyPool>> GetAllPoolsAsync()
        {
            return await _context.CurrencyPools
                .Include(p => p.Currency)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Currency.DisplayOrder)
                .ToListAsync();
        }

        /// <summary>
        /// Initialize a new currency pool
        /// ایجاد استخر جدید برای یک ارز
        /// </summary>
        public async Task<CurrencyPool> CreatePoolAsync(int currencyId, decimal initialBalance = 0)
        {
            var existingPool = await GetPoolAsync(currencyId);
            if (existingPool != null)
            {
                return existingPool;
            }

            var currency = await _context.Currencies.FindAsync(currencyId);
            if (currency == null)
            {
                throw new ArgumentException($"Currency with ID {currencyId} not found");
            }

            var pool = new CurrencyPool
            {
                CurrencyId = currencyId,
                CurrencyCode = currency.Code,
                Balance = initialBalance,
                TotalBought = initialBalance > 0 ? initialBalance : 0,
                TotalSold = 0,
                AverageBuyRate = null,
                AverageSellRate = null,
                LastUpdated = DateTime.Now,
                RiskLevel = PoolRiskLevel.Low,
                IsActive = true,
                Notes = $"Auto-created pool for {currency.Name}"
            };

            _context.CurrencyPools.Add(pool);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created new pool for {currency.Code} with initial balance {initialBalance}");
            
            return pool;
        }

        /// <summary>
        /// Calculate total portfolio value in specified currency
        /// محاسبه ارزش کل پورتفولیو در ارز مشخص شده
        /// </summary>
        public async Task<decimal> CalculatePortfolioValueAsync(string targetCurrencyCode, Dictionary<string, decimal> exchangeRates)
        {
            var pools = await GetAllPoolsAsync();
            decimal totalValue = 0;

            foreach (var pool in pools)
            {
                if (pool.Currency?.Code == targetCurrencyCode)
                {
                    // Same currency - no conversion needed
                    totalValue += pool.Balance;
                }
                else if (!string.IsNullOrEmpty(pool.Currency?.Code) && 
                         exchangeRates.TryGetValue(pool.Currency.Code, out var rate))
                {
                    totalValue += pool.CalculateCurrentPositionValue(targetCurrencyCode, rate);
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
                .Include(p => p.Currency)
                .Where(p => p.IsActive && p.RiskLevel >= riskLevel)
                .OrderByDescending(p => p.RiskLevel)
                .ThenBy(p => p.Currency.DisplayOrder)
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
        public async Task<PoolPerformance> GetPoolPerformanceAsync(int currencyId, decimal currentRate)
        {
            var pool = await GetPoolAsync(currencyId);
            if (pool == null)
            {
                return new PoolPerformance { CurrencyCode = "Unknown" };
            }

            var performance = new PoolPerformance
            {
                CurrencyCode = pool.Currency?.Code ?? "Unknown",
                CurrentBalance = pool.Balance,
                CurrentValue = pool.CalculateCurrentPositionValue("IRR", currentRate),
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
            var buyOrder = await _context.Orders
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .FirstOrDefaultAsync(o => o.Id == transaction.BuyOrderId);
            var sellOrder = await _context.Orders.FindAsync(transaction.SellOrderId);

            if (buyOrder != null && sellOrder != null)
            {
                // Update pools based on the exchange perspective
                // When a customer buys USD with Toman, exchange sells USD and buys Toman
                var fromCurrencyPool = await UpdatePoolAsync(buyOrder.FromCurrencyId, transaction.Amount, PoolTransactionType.Buy, transaction.Rate);
                var toCurrencyPool = await UpdatePoolAsync(buyOrder.ToCurrencyId, transaction.TotalAmount, PoolTransactionType.Sell, transaction.Rate);

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
