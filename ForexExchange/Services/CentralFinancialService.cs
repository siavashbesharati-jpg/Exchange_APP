using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;

namespace ForexExchange.Services
{
    /// <summary>
    /// Centralized financial service that maintains complete audit trail through history tables
    /// while preserving exact calculation logic from existing services.
    /// ZERO LOGIC CHANGES - maintains compatibility with existing financial operations.
    /// </summary>
    public class CentralFinancialService : ICentralFinancialService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<CentralFinancialService> _logger;

        public CentralFinancialService(ForexDbContext context, ILogger<CentralFinancialService> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Customer Balance Operations

        public async Task<decimal> GetCustomerBalanceAsync(int customerId, string currencyCode)
        {
            // Get from current balance table for performance
            var balance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

            return balance?.Balance ?? 0;
        }

        public async Task<List<CustomerBalance>> GetCustomerBalancesAsync(int customerId)
        {
            return await _context.CustomerBalances
                .Where(cb => cb.CustomerId == customerId)
                .ToListAsync();
        }

        public async Task ProcessOrderCreationAsync(Order order, string performedBy = "System")
        {
            _logger.LogInformation($"Processing order creation for Order ID: {order.Id}");

            // PRESERVE EXACT LOGIC: Each order creates two currency impacts
            // 1. Payment transaction (FromCurrency - negative impact)
            await UpdateCustomerBalanceAsync(
                customerId: order.CustomerId,
                currencyCode: order.FromCurrency.Code,
                amount: -order.FromAmount, // Negative because customer pays this amount
                transactionType: CustomerBalanceTransactionType.Order,
                relatedOrderId: order.Id,
                reason: $"Order {order.Id}: Payment in {order.FromCurrency.Code}",
                performedBy: performedBy
            );

            // 2. Receipt transaction (ToCurrency - positive impact)
            await UpdateCustomerBalanceAsync(
                customerId: order.CustomerId,
                currencyCode: order.ToCurrency.Code,
                amount: order.ToAmount, // Positive because customer receives this amount
                transactionType: CustomerBalanceTransactionType.Order,
                relatedOrderId: order.Id,
                reason: $"Order {order.Id}: Receipt in {order.ToCurrency.Code}",
                performedBy: performedBy
            );

            // Update currency pools - PRESERVE EXACT LOGIC
            // When customer sells FromCurrency to us, our pool increases
            await IncreaseCurrencyPoolAsync(
                currencyCode: order.FromCurrency.Code,
                amount: order.FromAmount,
                transactionType: CurrencyPoolTransactionType.Order,
                reason: $"Bought from customer via Order {order.Id}",
                performedBy: performedBy,
                referenceId: order.Id
            );

            // When customer buys ToCurrency from us, our pool decreases
            await DecreaseCurrencyPoolAsync(
                currencyCode: order.ToCurrency.Code,
                amount: order.ToAmount,
                transactionType: CurrencyPoolTransactionType.Order,
                reason: $"Sold to customer via Order {order.Id}",
                performedBy: performedBy,
                referenceId: order.Id
            );

            _logger.LogInformation($"Order {order.Id} processing completed - dual currency impact recorded");
        }

        public async Task ProcessAccountingDocumentAsync(AccountingDocument document, string performedBy = "System")
        {
            _logger.LogInformation($"Processing accounting document ID: {document.Id}");

            // CORRECTED LOGIC: Process customer impact
            if (document.PayerCustomerId.HasValue)
            {
                await UpdateCustomerBalanceAsync(
                    customerId: document.PayerCustomerId.Value,
                    currencyCode: document.CurrencyCode,
                    amount: document.Amount, // Positive for payer (they paid/deposited)
                    transactionType: CustomerBalanceTransactionType.AccountingDocument,
                    relatedDocumentId: document.Id,
                    reason: $"Document {document.Id}: {document.Title}",
                    performedBy: performedBy
                );
            }

            if (document.ReceiverCustomerId.HasValue)
            {
                await UpdateCustomerBalanceAsync(
                    customerId: document.ReceiverCustomerId.Value,
                    currencyCode: document.CurrencyCode,
                    amount: -document.Amount, // Negative for receiver (they received/withdrew)
                    transactionType: CustomerBalanceTransactionType.AccountingDocument,
                    relatedDocumentId: document.Id,
                    reason: $"Document {document.Id}: {document.Title}",
                    performedBy: performedBy
                );
            }

            // Process bank account impact
            if (document.PayerBankAccountId.HasValue)
            {
                await ProcessBankAccountTransactionAsync(
                    bankAccountId: document.PayerBankAccountId.Value,
                    amount: -document.Amount, // Negative for payer account
                    transactionType: BankAccountTransactionType.Document,
                    relatedDocumentId: document.Id,
                    reason: $"Document {document.Id}: {document.Title}",
                    performedBy: performedBy
                );
            }

            if (document.ReceiverBankAccountId.HasValue)
            {
                await ProcessBankAccountTransactionAsync(
                    bankAccountId: document.ReceiverBankAccountId.Value,
                    amount: document.Amount, // Positive for receiver account
                    transactionType: BankAccountTransactionType.Document,
                    relatedDocumentId: document.Id,
                    reason: $"Document {document.Id}: {document.Title}",
                    performedBy: performedBy
                );
            }

            _logger.LogInformation($"Document {document.Id} processing completed");
        }

        public async Task AdjustCustomerBalanceAsync(int customerId, string currencyCode, decimal adjustmentAmount,
            string reason, string performedBy)
        {
            await UpdateCustomerBalanceAsync(
                customerId: customerId,
                currencyCode: currencyCode,
                amount: adjustmentAmount,
                transactionType: CustomerBalanceTransactionType.Manual,
                relatedOrderId: null,
                relatedDocumentId: null,
                reason: reason,
                performedBy: performedBy
            );
        }

        #endregion

        #region Currency Pool Operations

        public async Task<decimal> GetCurrencyPoolBalanceAsync(string currencyCode)
        {
            var pool = await _context.CurrencyPools
                .FirstOrDefaultAsync(cp => cp.CurrencyCode == currencyCode && cp.IsActive);

            return pool?.Balance ?? 0;
        }

        public async Task<List<CurrencyPool>> GetAllCurrencyPoolsAsync()
        {
            return await _context.CurrencyPools
                .Include(cp => cp.Currency)
                .Where(cp => cp.IsActive)
                .OrderBy(cp => cp.Currency.DisplayOrder)
                .ToListAsync();
        }

        public async Task IncreaseCurrencyPoolAsync(string currencyCode, decimal amount, CurrencyPoolTransactionType transactionType,
            string reason, string performedBy = "System", int? referenceId = null)
        {
            await UpdateCurrencyPoolAsync(
                currencyCode: currencyCode,
                amount: Math.Abs(amount), // Ensure positive for increase
                transactionType: transactionType,
                reason: reason,
                performedBy: performedBy,
                referenceId: referenceId
            );
        }

        public async Task DecreaseCurrencyPoolAsync(string currencyCode, decimal amount, CurrencyPoolTransactionType transactionType,
            string reason, string performedBy = "System", int? referenceId = null)
        {
            await UpdateCurrencyPoolAsync(
                currencyCode: currencyCode,
                amount: -Math.Abs(amount), // Ensure negative for decrease
                transactionType: transactionType,
                reason: reason,
                performedBy: performedBy,
                referenceId: referenceId
            );
        }

        public async Task AdjustCurrencyPoolAsync(string currencyCode, decimal adjustmentAmount,
            string reason, string performedBy)
        {
            await UpdateCurrencyPoolAsync(
                currencyCode: currencyCode,
                amount: adjustmentAmount,
                transactionType: CurrencyPoolTransactionType.ManualEdit,
                reason: reason,
                performedBy: performedBy,
                referenceId: null // Manual adjustments don't have reference IDs
            );
        }

        #endregion

        #region Bank Account Balance Operations

        public async Task<decimal> GetBankAccountBalanceAsync(int bankAccountId)
        {
            var balance = await _context.BankAccountBalances
                .FirstOrDefaultAsync(bab => bab.BankAccountId == bankAccountId);

            return balance?.Balance ?? 0;
        }

        public async Task<List<BankAccountBalance>> GetAllBankAccountBalancesAsync()
        {
            return await _context.BankAccountBalances
                .Include(bab => bab.BankAccount)
                .ToListAsync();
        }

        public async Task ProcessBankAccountTransactionAsync(int bankAccountId, decimal amount, BankAccountTransactionType transactionType,
            int? relatedDocumentId, string reason, string performedBy = "System")
        {
            await UpdateBankAccountBalanceAsync(
                bankAccountId: bankAccountId,
                amount: amount,
                transactionType: transactionType,
                relatedDocumentId: relatedDocumentId,
                reason: reason,
                performedBy: performedBy
            );
        }

        public async Task AdjustBankAccountBalanceAsync(int bankAccountId, decimal adjustmentAmount,
            string reason, string performedBy)
        {
            await UpdateBankAccountBalanceAsync(
                bankAccountId: bankAccountId,
                amount: adjustmentAmount,
                transactionType: BankAccountTransactionType.ManualEdit,
                relatedDocumentId: null,
                reason: reason,
                performedBy: performedBy
            );
        }

        #endregion

        #region Core Update Methods - PRESERVE EXACT CALCULATION LOGIC

        private async Task UpdateCustomerBalanceAsync(int customerId, string currencyCode, decimal amount,
            CustomerBalanceTransactionType transactionType, int? relatedOrderId = null, int? relatedDocumentId = null,
            string? reason = null, string performedBy = "System")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get or create current balance
                var currentBalance = await _context.CustomerBalances
                    .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

                if (currentBalance == null)
                {
                    currentBalance = new CustomerBalance
                    {
                        CustomerId = customerId,
                        CurrencyCode = currencyCode,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow,
                        Notes = "Auto-created by CentralFinancialService"
                    };
                    _context.CustomerBalances.Add(currentBalance);
                }

                var previousBalance = currentBalance.Balance;
                var newBalance = previousBalance + amount;

                // Create history record FIRST for audit trail
                var historyRecord = new CustomerBalanceHistory
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    BalanceBefore = previousBalance,
                    TransactionAmount = amount,
                    BalanceAfter = newBalance,
                    TransactionType = transactionType,
                    ReferenceId = relatedOrderId ?? relatedDocumentId,
                    Description = reason ?? GetTransactionTypeDescription(transactionType),
                    TransactionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy
                };

                // Validate calculation
                if (!historyRecord.IsCalculationValid())
                {
                    throw new InvalidOperationException($"Balance calculation validation failed for Customer {customerId}, Currency {currencyCode}");
                }

                _context.CustomerBalanceHistory.Add(historyRecord);

                // Update current balance
                currentBalance.Balance = newBalance;
                currentBalance.LastUpdated = DateTime.UtcNow;
                currentBalance.Notes = $"Updated by {performedBy} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Customer balance updated: Customer {customerId}, Currency {currencyCode}, Amount {amount}, New Balance {newBalance}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating customer balance: Customer {customerId}, Currency {currencyCode}, Amount {amount}");
                throw;
            }
        }

        private async Task UpdateCurrencyPoolAsync(string currencyCode, decimal amount, CurrencyPoolTransactionType transactionType,
            string reason, string performedBy = "System", int? referenceId = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // First, try to get active pool
                var pool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(cp => cp.CurrencyCode == currencyCode && cp.IsActive);

                if (pool == null)
                {
                    // Get currency information first
                    var currency = await _context.Currencies
                        .FirstOrDefaultAsync(c => c.Code == currencyCode && c.IsActive);

                    if (currency == null)
                    {
                        throw new InvalidOperationException($"Currency not found: {currencyCode}");
                    }



                    // Check if ANY pool exists for this currency (active or inactive)
                    var existingPool = await _context.CurrencyPools
                        .FirstOrDefaultAsync(cp => cp.CurrencyId == currency.Id);

                    if (existingPool != null)
                    {
                        // Pool exists - fix it and activate regardless of current state
                        _logger.LogWarning($"Found existing pool for Currency ID {currency.Id} (Code: {existingPool.CurrencyCode}, Active: {existingPool.IsActive}), updating and activating");
                        existingPool.CurrencyCode = currencyCode;
                        existingPool.IsActive = true;
                        existingPool.LastUpdated = DateTime.UtcNow;
                        existingPool.Notes = $"Updated and activated by CentralFinancialService - {performedBy}";
                        pool = existingPool;
                    }
                    else
                    {
                        // Auto-create missing currency pool
                        _logger.LogWarning($"Currency pool not found for {currencyCode}, creating automatically");
                        pool = new CurrencyPool
                        {
                            CurrencyId = currency.Id,
                            CurrencyCode = currencyCode,
                            Balance = 100000, // Default starting balance
                            TotalBought = 0,
                            TotalSold = 0,
                            ActiveBuyOrderCount = 0,
                            ActiveSellOrderCount = 0,
                            RiskLevel = PoolRiskLevel.Low,
                            IsActive = true,
                            LastUpdated = DateTime.UtcNow,
                            Notes = $"Auto-created by CentralFinancialService - {performedBy}"
                        };
                        _context.CurrencyPools.Add(pool);
                        await _context.SaveChangesAsync(); // Save the new pool first
                    }
                }

                var previousBalance = pool.Balance;
                var newBalance = previousBalance + amount;

                // Create history record
                var historyRecord = new CurrencyPoolHistory
                {
                    CurrencyCode = currencyCode,
                    BalanceBefore = previousBalance,
                    TransactionAmount = amount,
                    BalanceAfter = newBalance,
                    TransactionType = transactionType,
                    ReferenceId = referenceId, // Now properly set for orders and documents
                    Description = reason,
                    TransactionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy
                };

                // Validate calculation
                if (!historyRecord.IsCalculationValid())
                {
                    throw new InvalidOperationException($"Pool balance calculation validation failed for Currency {currencyCode}");
                }

                _context.CurrencyPoolHistory.Add(historyRecord);

                // Update current pool balance
                pool.Balance = newBalance;
                pool.LastUpdated = DateTime.UtcNow;
                pool.Notes = $"Updated by {performedBy} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

                // Update totals for statistical tracking
                if (amount > 0)
                {
                    pool.TotalBought += amount;
                }
                else
                {
                    pool.TotalSold += Math.Abs(amount);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Currency pool updated: Currency {currencyCode}, Amount {amount}, New Balance {newBalance}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating currency pool: Currency {currencyCode}, Amount {amount}");
                throw;
            }
        }

        private async Task UpdateBankAccountBalanceAsync(int bankAccountId, decimal amount, BankAccountTransactionType transactionType,
            int? relatedDocumentId, string reason, string performedBy = "System")
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get or create bank account balance
                var balance = await _context.BankAccountBalances
                    .FirstOrDefaultAsync(bab => bab.BankAccountId == bankAccountId);

                if (balance == null)
                {
                    balance = new BankAccountBalance
                    {
                        BankAccountId = bankAccountId,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.BankAccountBalances.Add(balance);
                }

                var previousBalance = balance.Balance;
                var newBalance = previousBalance + amount;

                // Create history record
                var historyRecord = new BankAccountBalanceHistory
                {
                    BankAccountId = bankAccountId,
                    BalanceBefore = previousBalance,
                    TransactionAmount = amount,
                    BalanceAfter = newBalance,
                    TransactionType = transactionType,
                    ReferenceId = relatedDocumentId,
                    Description = reason,
                    TransactionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy
                };

                // Validate calculation
                if (!historyRecord.IsCalculationValid())
                {
                    throw new InvalidOperationException($"Bank account balance calculation validation failed for Account {bankAccountId}");
                }

                _context.BankAccountBalanceHistory.Add(historyRecord);

                // Update current balance
                balance.Balance = newBalance;
                balance.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Bank account balance updated: Account {bankAccountId}, Amount {amount}, New Balance {newBalance}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error updating bank account balance: Account {bankAccountId}, Amount {amount}");
                throw;
            }
        }

        #endregion

        #region Balance History and Audit

        public async Task<CustomerFinancialHistoryDto> GetCustomerFinancialHistoryAsync(int customerId,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            // PRESERVE EXACT LOGIC from CustomerFinancialHistoryService
            var actualFromDate = fromDate ?? DateTime.MinValue;
            var actualToDate = toDate ?? DateTime.MaxValue;

            // Get customer data
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == customerId);

            if (customer == null)
            {
                throw new ArgumentException($"Customer with ID {customerId} not found");
            }

            // Get current balances
            var balances = await GetCustomerBalancesAsync(customerId);

            // Get all orders for the period - all orders are complete
            var orders = await _context.Orders
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => o.CustomerId == customerId &&
                           o.CreatedAt >= actualFromDate && o.CreatedAt <= actualToDate)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            // Get all verified documents for the period
            var documents = await _context.AccountingDocuments
                .Include(d => d.PayerCustomer)
                .Include(d => d.ReceiverCustomer)
                .Where(d => (d.PayerCustomerId == customerId || d.ReceiverCustomerId == customerId) &&
                           d.DocumentDate >= actualFromDate && d.DocumentDate <= actualToDate &&
                           d.IsVerified)
                .OrderBy(d => d.DocumentDate)
                .ToListAsync();

            // Create transaction list with preserved logic
            var transactions = new List<FinancialTransactionDto>();

            // Add order transactions - DUAL CURRENCY IMPACT
            foreach (var order in orders)
            {
                // Payment transaction (FromCurrency)
                transactions.Add(new FinancialTransactionDto
                {
                    Date = order.CreatedAt,
                    Type = "Order",
                    Description = $"سفارش {order.Id}: پرداخت {order.FromAmount:N0} {order.FromCurrency.PersianName}",
                    CurrencyCode = order.FromCurrency.Code,
                    Amount = -order.FromAmount, // Negative for payment
                    OrderId = order.Id,
                    Notes = order.Notes
                });

                // Receipt transaction (ToCurrency)
                transactions.Add(new FinancialTransactionDto
                {
                    Date = order.CreatedAt,
                    Type = "Order",
                    Description = $"سفارش {order.Id}: دریافت {order.ToAmount:N0} {order.ToCurrency.PersianName}",
                    CurrencyCode = order.ToCurrency.Code,
                    Amount = order.ToAmount, // Positive for receipt
                    OrderId = order.Id,
                    Notes = order.Notes
                });
            }

            // Add document transactions
            foreach (var document in documents)
            {
                if (document.PayerCustomerId == customerId)
                {
                    transactions.Add(new FinancialTransactionDto
                    {
                        Date = document.DocumentDate,
                        Type = "Document",
                        Description = $"سند {document.Id}: {document.Title} (پرداخت)",
                        CurrencyCode = document.CurrencyCode,
                        Amount = document.Amount, // Positive for payer (they paid/deposited)
                        DocumentId = document.Id,
                        Notes = document.Notes
                    });
                }

                if (document.ReceiverCustomerId == customerId)
                {
                    transactions.Add(new FinancialTransactionDto
                    {
                        Date = document.DocumentDate,
                        Type = "Document",
                        Description = $"سند {document.Id}: {document.Title} (دریافت)",
                        CurrencyCode = document.CurrencyCode,
                        Amount = -document.Amount, // Negative for receiver (they received/withdrew)
                        DocumentId = document.Id,
                        Notes = document.Notes
                    });
                }
            }

            // Sort by date and calculate running balances
            transactions = transactions.OrderBy(t => t.Date).ToList();

            // Calculate initial balances for each currency
            var initialBalances = new Dictionary<string, decimal>();
            var currentBalances = new Dictionary<string, decimal>();

            foreach (var balance in balances)
            {
                var transactionsForCurrency = transactions.Where(t => t.CurrencyCode == balance.CurrencyCode).ToList();
                var totalTransactionAmount = transactionsForCurrency.Sum(t => t.Amount);
                var initialBalance = balance.Balance - totalTransactionAmount;

                initialBalances[balance.CurrencyCode] = initialBalance;
                currentBalances[balance.CurrencyCode] = initialBalance;
            }

            // Calculate running balances
            foreach (var transaction in transactions)
            {
                if (!currentBalances.ContainsKey(transaction.CurrencyCode))
                {
                    currentBalances[transaction.CurrencyCode] = 0;
                }

                currentBalances[transaction.CurrencyCode] += transaction.Amount;
                transaction.RunningBalance = currentBalances[transaction.CurrencyCode];
            }

            return new CustomerFinancialHistoryDto
            {
                Customer = customer,
                Balances = balances,
                Transactions = transactions,
                InitialBalances = initialBalances
            };
        }

        public async Task<List<CustomerBalanceHistory>> GetCustomerBalanceHistoryAsync(int customerId,
            string currencyCode, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId && h.CurrencyCode == currencyCode);

            if (fromDate.HasValue)
                query = query.Where(h => h.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(h => h.CreatedAt <= toDate.Value);

            return await query.OrderBy(h => h.CreatedAt).ToListAsync();
        }

        public async Task<List<CurrencyPoolHistory>> GetCurrencyPoolHistoryAsync(string currencyCode,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.CurrencyPoolHistory
                .Where(h => h.CurrencyCode == currencyCode);

            if (fromDate.HasValue)
                query = query.Where(h => h.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(h => h.CreatedAt <= toDate.Value);

            return await query.OrderBy(h => h.CreatedAt).ToListAsync();
        }

        public async Task<List<BankAccountBalanceHistory>> GetBankAccountHistoryAsync(int bankAccountId,
            DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId);

            if (fromDate.HasValue)
                query = query.Where(h => h.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(h => h.CreatedAt <= toDate.Value);

            return await query.OrderBy(h => h.CreatedAt).ToListAsync();
        }

        public async Task<bool> ValidateBalanceConsistencyAsync()
        {
            var inconsistencies = new List<string>();

            // Validate customer balances
            var customerBalances = await _context.CustomerBalances.ToListAsync();
            foreach (var balance in customerBalances)
            {
                var latestHistory = await _context.CustomerBalanceHistory
                    .Where(h => h.CustomerId == balance.CustomerId && h.CurrencyCode == balance.CurrencyCode)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestHistory != null && Math.Abs(balance.Balance - latestHistory.BalanceAfter) > 0.01m)
                {
                    inconsistencies.Add($"Customer {balance.CustomerId} {balance.CurrencyCode}: Current={balance.Balance}, History={latestHistory.BalanceAfter}");
                }
            }

            // Validate currency pools
            var currencyPools = await _context.CurrencyPools.Where(p => p.IsActive).ToListAsync();
            foreach (var pool in currencyPools)
            {
                var latestHistory = await _context.CurrencyPoolHistory
                    .Where(h => h.CurrencyCode == pool.CurrencyCode)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestHistory != null && Math.Abs(pool.Balance - latestHistory.BalanceAfter) > 0.01m)
                {
                    inconsistencies.Add($"Currency Pool {pool.CurrencyCode}: Current={pool.Balance}, History={latestHistory.BalanceAfter}");
                }
            }

            // Validate bank account balances
            var bankBalances = await _context.BankAccountBalances.ToListAsync();
            foreach (var balance in bankBalances)
            {
                var latestHistory = await _context.BankAccountBalanceHistory
                    .Where(h => h.BankAccountId == balance.BankAccountId)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestHistory != null && Math.Abs(balance.Balance - latestHistory.BalanceAfter) > 0.01m)
                {
                    inconsistencies.Add($"Bank Account {balance.BankAccountId}: Current={balance.Balance}, History={latestHistory.BalanceAfter}");
                }
            }

            if (inconsistencies.Any())
            {
                _logger.LogWarning($"Balance inconsistencies found: {string.Join("; ", inconsistencies)}");
                return false;
            }

            _logger.LogInformation("All balance consistencies validated successfully");
            return true;
        }

        public async Task RecalculateAllBalancesFromHistoryAsync()
        {
            _logger.LogInformation("Starting balance recalculation from history tables");

            // Recalculate customer balances
            var customerBalances = await _context.CustomerBalances.ToListAsync();
            foreach (var balance in customerBalances)
            {
                var latestHistory = await _context.CustomerBalanceHistory
                    .Where(h => h.CustomerId == balance.CustomerId && h.CurrencyCode == balance.CurrencyCode)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestHistory != null)
                {
                    balance.Balance = latestHistory.BalanceAfter;
                    balance.LastUpdated = DateTime.UtcNow;
                    balance.Notes = "Recalculated from history";
                }
            }

            // Recalculate currency pools
            var currencyPools = await _context.CurrencyPools.Where(p => p.IsActive).ToListAsync();
            foreach (var pool in currencyPools)
            {
                var latestHistory = await _context.CurrencyPoolHistory
                    .Where(h => h.CurrencyCode == pool.CurrencyCode)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestHistory != null)
                {
                    pool.Balance = latestHistory.BalanceAfter;
                    pool.LastUpdated = DateTime.UtcNow;
                    pool.Notes = "Recalculated from history";
                }
            }

            // Recalculate bank account balances
            var bankBalances = await _context.BankAccountBalances.ToListAsync();
            foreach (var balance in bankBalances)
            {
                var latestHistory = await _context.BankAccountBalanceHistory
                    .Where(h => h.BankAccountId == balance.BankAccountId)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefaultAsync();

                if (latestHistory != null)
                {
                    balance.Balance = latestHistory.BalanceAfter;
                    balance.LastUpdated = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Balance recalculation completed");
        }

        /// <summary>
        /// Helper method to get description for transaction type
        /// </summary>
        private string GetTransactionTypeDescription(CustomerBalanceTransactionType transactionType)
        {
            return transactionType switch
            {
                CustomerBalanceTransactionType.Order => "Order",
                CustomerBalanceTransactionType.AccountingDocument => "Document",
                CustomerBalanceTransactionType.Manual => "Manual",
                _ => transactionType.ToString()
            };
        }

        /// <summary>
        /// TEMPORARY METHOD: Recalculate IRR pool balance based on existing orders
        /// This method should be called once to fix missing IRR pool updates, then removed
        /// </summary>
        public async Task RecalculateIRRPoolFromOrdersAsync()
        {
            _logger.LogInformation("Starting IRR pool recalculation from existing orders");

            try
            {
                var IRRCurrency = _context.Currencies.First(c => c.Code == "IRR");

                // Find existing IRR pool ONLY
                var irrPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(cp => cp.CurrencyId == IRRCurrency.Id );


                if (irrPool == null)
                {
                    _logger.LogWarning("IRR currency pool not found - cannot recalculate without existing pool");
                    return;
                }

                _logger.LogInformation($"Found existing IRR pool: ID={irrPool.Id}, Current Balance={irrPool.Balance}, Active={irrPool.IsActive}");

                // Get all orders involving IRR
                var irrOrders = await _context.Orders
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Where(o => o.FromCurrencyId == IRRCurrency.Id || o.ToCurrencyId == IRRCurrency.Id)
                    .OrderBy(o => o.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation($"Found {irrOrders.Count} orders involving IRR");

                // Calculate what the IRR pool adjustment should be
                decimal totalBought = 0; // IRR we bought from customers (pool increases)
                decimal totalSold = 0;   // IRR we sold to customers (pool decreases)

                foreach (var order in irrOrders)
                {
                    if (order.FromCurrency.Code == "IRR")
                    {
                        // Customer sold IRR to us, our IRR pool should increase
                        totalBought += order.FromAmount;
                        _logger.LogInformation($"Order {order.Id}: We bought {order.FromAmount} IRR from customer");
                    }

                    if (order.ToCurrency.Code == "IRR")
                    {
                        // Customer bought IRR from us, our IRR pool should decrease
                        totalSold += order.ToAmount;
                        _logger.LogInformation($"Order {order.Id}: We sold {order.ToAmount} IRR to customer");
                    }
                }

                decimal netAdjustment = totalBought - totalSold;
                _logger.LogInformation($"IRR Summary: Bought={totalBought}, Sold={totalSold}, Net Adjustment={netAdjustment}");

                // Update the existing pool balance and create history for each order
                if (netAdjustment != 0)
                {
                    decimal runningBalance = irrPool.Balance;
                    
                    // Create individual history records for each IRR order
                    foreach (var order in irrOrders)
                    {
                        decimal orderAdjustment = 0;
                        string orderDescription = "";

                        if (order.FromCurrency.Code == "IRR")
                        {
                            // Customer sold IRR to us, our IRR pool increases
                            orderAdjustment = order.FromAmount;
                            orderDescription = $"Order {order.Id}: Bought {order.FromAmount:N0} IRR from customer (to {order.ToCurrency.Code})";
                        }
                        else if (order.ToCurrency.Code == "IRR")
                        {
                            // Customer bought IRR from us, our IRR pool decreases
                            orderAdjustment = -order.ToAmount;
                            orderDescription = $"Order {order.Id}: Sold {order.ToAmount:N0} IRR to customer (from {order.FromCurrency.Code})";
                        }

                        if (orderAdjustment != 0)
                        {
                            var orderHistoryRecord = new CurrencyPoolHistory
                            {
                                CurrencyCode = "IRR",
                                BalanceBefore = runningBalance,
                                TransactionAmount = orderAdjustment,
                                BalanceAfter = runningBalance + orderAdjustment,
                                TransactionType = CurrencyPoolTransactionType.Order,
                                ReferenceId = order.Id,
                                Description = orderDescription,
                                TransactionDate = order.CreatedAt, // Use original order date
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = "IRR Pool Recalculation System"
                            };

                            _context.CurrencyPoolHistory.Add(orderHistoryRecord);
                            runningBalance += orderAdjustment;
                            
                            _logger.LogInformation($"Created history: {orderDescription}, Balance: {orderHistoryRecord.BalanceBefore} -> {orderHistoryRecord.BalanceAfter}");
                        }
                    }

                    // Update the pool with final values
                    irrPool.Balance = runningBalance;
                    irrPool.TotalBought += totalBought;
                    irrPool.TotalSold += totalSold;
                    irrPool.LastUpdated = DateTime.UtcNow;
                    irrPool.Notes = $"Recalculated from {irrOrders.Count} historical orders - adjusted by {netAdjustment}";

                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"IRR pool updated: Final Balance={irrPool.Balance}, Total Bought={totalBought}, Total Sold={totalSold}");
                    _logger.LogInformation($"Created {irrOrders.Count} individual history records for IRR orders");
                }
                else
                {
                    _logger.LogInformation("No IRR pool adjustment needed - net adjustment is zero");
                }

                _logger.LogInformation("IRR pool recalculation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during IRR pool recalculation");
                throw;
            }
        }

        #endregion
    }
}
