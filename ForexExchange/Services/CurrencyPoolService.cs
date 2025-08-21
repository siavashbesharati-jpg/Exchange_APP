using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    /// <summary>
    /// Currency Pool Service Implementation
    /// پیاده‌سازی سرویس استخر ارزی
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
        public async Task<CurrencyPool> UpdatePoolAsync(string currency, decimal amount, PoolTransactionType transactionType, decimal rate)
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
        public async Task<decimal> GetPoolBalanceAsync(string currency)
        {
            var pool = await _context.CurrencyPools
                .FirstOrDefaultAsync(p => p.Currency == currency && p.IsActive);
            
            return pool?.Balance ?? 0;
        }

        /// <summary>
        /// Get pool details for a currency
        /// دریافت جزئیات استخر برای یک ارز
        /// </summary>
        public async Task<CurrencyPool?> GetPoolAsync(string currency)
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
        public async Task<CurrencyPool> CreatePoolAsync(string currency, decimal initialBalance = 0)
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
        /// Calculate total portfolio value in Toman
        /// محاسبه ارزش کل پورتفولیو به تومان
        /// </summary>
        public async Task<decimal> CalculatePortfolioValueAsync(Dictionary<string, decimal> exchangeRates)
        {
            var pools = await GetAllPoolsAsync();
            decimal totalValue = 0;

            foreach (var pool in pools)
            {
                if (exchangeRates.TryGetValue(pool.Currency, out var rate))
                {
                    totalValue += pool.Balance * rate;
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
                .ThenByDescending(p => Math.Abs(p.Balance))
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
                var oldRiskLevel = pool.RiskLevel;
                await UpdatePoolRiskLevel(pool, lowThreshold, highThreshold);
                
                if (pool.RiskLevel != oldRiskLevel)
                {
                    updatedCount++;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated risk levels for {updatedCount} pools");
            
            return updatedCount;
        }

        /// <summary>
        /// Get pool performance statistics
        /// دریافت آمار عملکرد استخر
        /// </summary>
        public async Task<PoolPerformance> GetPoolPerformanceAsync(string currency, decimal currentRate)
        {
            var pool = await GetPoolAsync(currency);
            
            if (pool == null)
            {
                return new PoolPerformance 
                { 
                    Currency = currency,
                    RiskLevel = PoolRiskLevel.Low 
                };
            }

            // Calculate total transactions for this currency
            var transactionCount = await _context.Transactions
                .Where(t => t.Currency.ToString() == currency)
                .CountAsync();

            var performance = new PoolPerformance
            {
                Currency = currency,
                CurrentBalance = pool.Balance,
                CurrentValue = pool.CalculateCurrentPositionValue(currentRate),
                NetProfitLoss = pool.CalculateNetProfitLoss(),
                RiskLevel = pool.RiskLevel,
                TotalTransactions = transactionCount,
                LastUpdated = pool.LastUpdated
            };

            // Calculate profit and loss separately
            if (performance.NetProfitLoss >= 0)
            {
                performance.TotalProfit = performance.NetProfitLoss;
                performance.TotalLoss = 0;
            }
            else
            {
                performance.TotalProfit = 0;
                performance.TotalLoss = Math.Abs(performance.NetProfitLoss);
            }

            // Calculate profit margin
            if (pool.TotalBought > 0 && pool.AverageBuyRate.HasValue)
            {
                decimal totalCost = pool.TotalBought * pool.AverageBuyRate.Value;
                performance.ProfitMargin = totalCost > 0 ? (performance.NetProfitLoss / totalCost) * 100 : 0;
            }

            return performance;
        }

        /// <summary>
        /// Process transaction and update corresponding pools
        /// پردازش تراکنش و بروزرسانی استخرهای مربوطه
        /// </summary>
        public async Task<List<CurrencyPool>> ProcessTransactionAsync(Transaction transaction)
        {
            var updatedPools = new List<CurrencyPool>();
            
            try
            {
                // In a market maker model, the exchange acts as counterparty
                // When customer buys, exchange sells (negative to pool)
                // When customer sells, exchange buys (positive to pool)
                
                // Get the customer order type from transaction
                var buyOrder = await _context.Orders.FindAsync(transaction.BuyOrderId);
                var sellOrder = await _context.Orders.FindAsync(transaction.SellOrderId);
                
                if (buyOrder == null || sellOrder == null)
                {
                    _logger.LogError($"Could not find orders for transaction {transaction.Id}");
                    return updatedPools;
                }

                // Update pool based on exchange perspective
                // Since exchange acts as market maker, it takes opposite position
                var currency = transaction.Currency.ToString();
                
                // Exchange sells currency to buyer (decreases pool)
                var poolAfterSell = await UpdatePoolAsync(
                    currency, 
                    transaction.Amount, 
                    PoolTransactionType.Sell, 
                    transaction.Rate
                );
                
                updatedPools.Add(poolAfterSell);

                _logger.LogInformation($"Processed transaction {transaction.Id}: Updated {currency} pool");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing transaction {transaction.Id}");
                throw;
            }

            return updatedPools;
        }

        /// <summary>
        /// Update risk level for a specific pool
        /// بروزرسانی سطح ریسک برای یک استخر خاص
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

            await Task.CompletedTask; // For consistency with async pattern
        }
    }
}
