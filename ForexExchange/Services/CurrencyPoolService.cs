using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    /// <summary>
    /// Currency Pool Service Implementation for Cross-Currency Trading
    /// پیاده‌سازی سرویس صندوق  ارزی برای تجارت متقابل ارزها
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
        /// بروزرسانی موجودی صندوق  پس از تراکنش
        /// </summary>
        public async Task<CurrencyPool> UpdatePoolAsync(int currencyId, decimal amount, PoolTransactionType transactionType, decimal rate)
        {
            var pool = await GetPoolAsync(currencyId);

            if (pool == null)
            {
                _logger.LogWarning($"Currency pool not found for currency ID {currencyId}");
                throw new InvalidOperationException($"Currency pool not found for currency ID {currencyId}");


            }

            // Update balances based on transaction type
            if (transactionType == PoolTransactionType.Buy)
            {
                // Exchange buys currency - increases pool balance
                pool.Balance += amount;
                pool.TotalBought += amount;
            }
            else
            {
                // Exchange sells currency - decreases pool balance
                pool.Balance -= amount;
                pool.TotalSold += amount;
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
        /// دریافت جزئیات صندوق  برای یک ارز
        /// </summary>
        public async Task<CurrencyPool?> GetPoolAsync(int currencyId)
        {
            return await _context.CurrencyPools
                .Include(p => p.Currency)
                .FirstOrDefaultAsync(p => p.CurrencyId == currencyId && p.IsActive);
        }

        /// <summary>
        /// Get pool details by pool ID
        /// دریافت جزئیات صندوق  بر اساس شناسه صندوق 
        /// </summary>
        public async Task<CurrencyPool?> GetPoolByIdAsync(int poolId)
        {
            return await _context.CurrencyPools
                .Include(p => p.Currency)
                .FirstOrDefaultAsync(p => p.Id == poolId && p.IsActive);
        }

        /// <summary>
        /// Get all active currency pools
        /// دریافت تمام صندوق های ارزی فعال
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
        /// ایجاد صندوق  جدید برای یک ارز
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
        /// دریافت صندوق های با سطح ریسک بالا
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
        /// بروزرسانی سطح ریسک برای تمام صندوق ها
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
        /// دریافت آمار عملکرد صندوق 
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
            // Note: Profit margin calculation is now done per currency pair in ExchangeRate
            // توجه: محاسبه حاشیه سود اکنون در ExchangeRate بر اساس جفت ارز انجام می‌شود
            var totalVolume = pool.TotalBought + pool.TotalSold;
            performance.ProfitMargin = totalVolume > 0 ? (performance.NetProfitLoss / totalVolume) * 100 : 0;

            return performance;
        }

        /// <summary>
        /// Process transaction and update corresponding pools
        /// پردازش تراکنش و بروزرسانی صندوق های مربوطه
        /// </summary>
        /*
        // TODO: Re-implement with new architecture
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
                var fromCurrencyPool = await UpdatePoolAsync(buyOrder.FromCurrencyId, transaction.Amount, PoolTransactionType.Buy, transaction.Rate ?? 0);
                var toCurrencyPool = await UpdatePoolAsync(buyOrder.ToCurrencyId, transaction.TotalAmount ?? 0, PoolTransactionType.Sell, transaction.Rate ?? 0);

                updatedPools.Add(fromCurrencyPool);
                if (toCurrencyPool.Id != fromCurrencyPool.Id)
                {
                    updatedPools.Add(toCurrencyPool);
                }
            }

            return updatedPools;
        }
        */

        /// <summary>
        /// Update order counts for a currency pool
        /// بروزرسانی تعداد معاملهات برای صندوق  ارزی
        /// </summary>
        public async Task UpdateOrderCountsAsync(int currencyId)
        {
            var pool = await GetPoolAsync(currencyId);
            if (pool == null) return;

            // Count active buy orders (orders where FromCurrencyId = currencyId, meaning the exchange to buy this currency)
            var activeBuyOrders = await _context.Orders
                .Where(o => o.FromCurrencyId == currencyId)
                .CountAsync();

            // Count active sell orders (orders where ToCurrencyId = currencyId, meaning the  exchange want to sell this currency)
            var activeSellOrders = await _context.Orders
                .Where(o => o.ToCurrencyId == currencyId)
                .CountAsync();

            pool.ActiveBuyOrderCount = activeBuyOrders;
            pool.ActiveSellOrderCount = activeSellOrders;
            pool.LastUpdated = DateTime.Now;

            _context.CurrencyPools.Update(pool);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Order counts updated for currency ID {currencyId}: Buy={activeBuyOrders}, Sell={activeSellOrders}");
        }

        /// <summary>
        /// Update order counts for all currency pools
        /// بروزرسانی تعداد معاملهات برای همه صندوق های ارزی
        /// </summary>
        public async Task UpdateAllOrderCountsAsync()
        {
            var pools = await _context.CurrencyPools.ToListAsync();

            foreach (var pool in pools)
            {
                await UpdateOrderCountsAsync(pool.CurrencyId);
            }

            _logger.LogInformation("All currency pool order counts updated");
        }

        /// <summary>
        /// Update risk level for a single pool
        /// بروزرسانی سطح ریسک برای یک صندوق 
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

        /// <summary>
        /// Direct pool update for admin operations
        /// بروزرسانی مستقیم صندوق  برای عملیات ادمین
        /// </summary>
        public async Task<CurrencyPool> UpdatePoolDirectAsync(CurrencyPool pool)
        {
            try
            {
                // Update risk level
                await UpdatePoolRiskLevel(pool);

                // Update the pool
                _context.CurrencyPools.Update(pool);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Pool directly updated: ID={pool.Id}, Balance={pool.Balance}");
                return pool;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in direct pool update for pool ID {pool.Id}");
                throw;
            }
        }

        /// <summary>
        /// Clean all pools - reset to zero for admin operations
        /// پاکسازی تمام صندوق ها - بازنشانی به صفر برای عملیات ادمین
        /// </summary>
        public async Task<bool> CleanPoolAsync()
        {
            try
            {
                // Get all pools and reset them to zero
                var pools = await _context.CurrencyPools.ToListAsync();

                _logger.LogInformation($"Starting to clean {pools.Count} currency pools");

                foreach (var pool in pools)
                {
                    _logger.LogInformation($"Cleaning pool for {pool.CurrencyCode}: Balance={pool.Balance}, TotalBought={pool.TotalBought}, TotalSold={pool.TotalSold}");
                    
                    pool.ActiveBuyOrderCount = 0;
                    pool.ActiveSellOrderCount = 0;
                    pool.TotalBought = 0;
                    pool.TotalSold = 0;
                    pool.RiskLevel = PoolRiskLevel.Low;
                    pool.Balance = 0;
                    pool.LastUpdated = DateTime.Now;
                    
                    _context.CurrencyPools.Update(pool);
                    
                    _logger.LogInformation($"Pool {pool.CurrencyCode} cleaned: All values set to 0");
                }

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully cleaned {pools.Count} pools, {result} records updated");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleaning pools");
                throw;
            }
        }

        /// <summary>
        /// Process accounting document and update currency pool balances
        /// </summary>
        public async Task ProcessAccountingDocumentAsync(AccountingDocument document)
        {
            try
            {
                // Get the currency for this document
                var currency = await _context.Currencies
                    .FirstOrDefaultAsync(c => c.Code == document.CurrencyCode);

                if (currency == null)
                {
                    _logger.LogWarning($"Currency not found for code {document.CurrencyCode}");
                    return;
                }

                // Determine transaction type from pool perspective
                PoolTransactionType transactionType;
                string reason;

                if (document.PayerType == PayerType.System)
                {
                    // System is paying - system is giving currency (like selling)
                    transactionType = PoolTransactionType.Sell;
                    reason = $"System payment - Document #{document.Id} - {document.Title}";
                }
                else // PayerType.Customer
                {
                    // Customer is paying - system is receiving currency (like buying)
                    transactionType = PoolTransactionType.Buy;
                    reason = $"Customer payment - Document #{document.Id} - {document.Title}";
                }

                // Update the currency pool
                await UpdatePoolAsync(currency.Id, document.Amount, transactionType, 1.0m);

                _logger.LogInformation("Processed accounting document {DocumentId} for currency pool {CurrencyCode}",
                    document.Id, document.CurrencyCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing accounting document {document.Id}");
                throw;
            }
        }
    }
}
