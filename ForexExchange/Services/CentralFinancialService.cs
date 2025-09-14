using ForexExchange.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
                performedBy: performedBy,
                transactionDate: order.CreatedAt // Use order creation date
            );

            // 2. Receipt transaction (ToCurrency - positive impact)
            await UpdateCustomerBalanceAsync(
                customerId: order.CustomerId,
                currencyCode: order.ToCurrency.Code,
                amount: order.ToAmount, // Positive because customer receives this amount
                transactionType: CustomerBalanceTransactionType.Order,
                relatedOrderId: order.Id,
                reason: $"Order {order.Id}: Receipt in {order.ToCurrency.Code}",
                performedBy: performedBy,
                transactionDate: order.CreatedAt // Use order creation date
            );

            // Update currency pools - PRESERVE EXACT LOGIC
            // When customer sells FromCurrency to us, our pool increases
            await IncreaseCurrencyPoolAsync(
                currencyCode: order.FromCurrency.Code,
                amount: order.FromAmount,
                transactionType: CurrencyPoolTransactionType.Order,
                reason: $"Bought from customer via Order {order.Id}",
                performedBy: performedBy,
                referenceId: order.Id,
                transactionDate: order.CreatedAt // Use order creation date
            );

            // When customer buys ToCurrency from us, our pool decreases
            await DecreaseCurrencyPoolAsync(
                currencyCode: order.ToCurrency.Code,
                amount: order.ToAmount,
                transactionType: CurrencyPoolTransactionType.Order,
                reason: $"Sold to customer via Order {order.Id}",
                performedBy: performedBy,
                referenceId: order.Id,
                transactionDate: order.CreatedAt // Use order creation date
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
                    performedBy: performedBy,
                    transactionDate: document.DocumentDate // Use document date
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
                    performedBy: performedBy,
                    transactionDate: document.DocumentDate // Use document date
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
                    performedBy: performedBy,
                    transactionDate: document.DocumentDate // Use document date
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
                    performedBy: performedBy,
                    transactionDate: document.DocumentDate // Use document date
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
            string reason, string performedBy = "System", int? referenceId = null, DateTime? transactionDate = null)
        {
            await UpdateCurrencyPoolAsync(
                currencyCode: currencyCode,
                amount: Math.Abs(amount), // Ensure positive for increase
                transactionType: transactionType,
                reason: reason,
                performedBy: performedBy,
                referenceId: referenceId,
                transactionDate: transactionDate
            );
        }

        public async Task DecreaseCurrencyPoolAsync(string currencyCode, decimal amount, CurrencyPoolTransactionType transactionType,
            string reason, string performedBy = "System", int? referenceId = null, DateTime? transactionDate = null)
        {
            await UpdateCurrencyPoolAsync(
                currencyCode: currencyCode,
                amount: -Math.Abs(amount), // Ensure negative for decrease
                transactionType: transactionType,
                reason: reason,
                performedBy: performedBy,
                referenceId: referenceId,
                transactionDate: transactionDate
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
            int? relatedDocumentId, string reason, string performedBy = "System", DateTime? transactionDate = null)
        {
            await UpdateBankAccountBalanceAsync(
                bankAccountId: bankAccountId,
                amount: amount,
                transactionType: transactionType,
                relatedDocumentId: relatedDocumentId,
                reason: reason,
                performedBy: performedBy,
                transactionDate: transactionDate
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
            string? reason = null, string performedBy = "System", DateTime? transactionDate = null)
        {
            await UpdateCustomerBalanceInternalAsync(customerId, currencyCode, amount, transactionType, 
                relatedOrderId, relatedDocumentId, reason, performedBy, useTransaction: true, transactionDate);
        }

        private async Task UpdateCustomerBalanceInternalAsync(int customerId, string currencyCode, decimal amount,
            CustomerBalanceTransactionType transactionType, int? relatedOrderId = null, int? relatedDocumentId = null,
            string? reason = null, string performedBy = "System", bool useTransaction = true, DateTime? transactionDate = null)
        {
            IDbContextTransaction? transaction = null;
            if (useTransaction)
            {
                transaction = await _context.Database.BeginTransactionAsync();
            }
            
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
                    TransactionDate = transactionDate ?? DateTime.UtcNow, // Use provided date or current time
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
                
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                _logger.LogInformation($"Customer balance updated: Customer {customerId}, Currency {currencyCode}, Amount {amount}, New Balance {newBalance}");
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.LogError(ex, $"Error updating customer balance: Customer {customerId}, Currency {currencyCode}, Amount {amount}");
                throw;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        private async Task UpdateCurrencyPoolAsync(string currencyCode, decimal amount, CurrencyPoolTransactionType transactionType,
            string reason, string performedBy = "System", int? referenceId = null, DateTime? transactionDate = null)
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
                    TransactionDate = transactionDate ?? DateTime.UtcNow, // Use provided date or current time
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
            int? relatedDocumentId, string reason, string performedBy = "System", DateTime? transactionDate = null)
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
                    TransactionDate = transactionDate ?? DateTime.UtcNow, // Use provided date or current time
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
                .Where(h => h.CustomerId == customerId && 
                           h.CurrencyCode == currencyCode && 
                           !h.IsDeleted); // EXCLUDE DELETED RECORDS

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
                .Where(h => h.CurrencyCode == currencyCode && !h.IsDeleted); // EXCLUDE DELETED RECORDS

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
                .Where(h => h.BankAccountId == bankAccountId && !h.IsDeleted); // EXCLUDE DELETED RECORDS

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
                    .Where(h => h.CustomerId == balance.CustomerId && h.CurrencyCode == balance.CurrencyCode && !h.IsDeleted)
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
                    .Where(h => h.CurrencyCode == pool.CurrencyCode && !h.IsDeleted)
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
                    .Where(h => h.BankAccountId == balance.BankAccountId && !h.IsDeleted)
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
                    .Where(h => h.CustomerId == balance.CustomerId && h.CurrencyCode == balance.CurrencyCode && !h.IsDeleted)
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
                    .Where(h => h.CurrencyCode == pool.CurrencyCode && !h.IsDeleted)
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
                    .Where(h => h.BankAccountId == balance.BankAccountId && !h.IsDeleted)
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

        #region Smart Delete Operations with History Soft Delete and Recalculation

        /// <summary>
        /// Safely delete an order by soft-deleting its history records and recalculating balances
        /// </summary>
        public async Task DeleteOrderAsync(Order order, string performedBy = "Admin")
        {
            try
            {
                _logger.LogInformation($"Starting smart order deletion: Order {order.Id} by {performedBy}");

                // 1. Soft delete customer balance history records for this order
                await SoftDeleteCustomerBalanceHistoryAsync(order.Id, null, performedBy);

                // 2. Soft delete currency pool history records for this order
                await SoftDeleteCurrencyPoolHistoryAsync(order.Id, null, performedBy);

                // 3. Soft delete the order itself
                order.IsDeleted = true;
                order.DeletedAt = DateTime.UtcNow;
                order.DeletedBy = performedBy;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Smart order deletion completed: Order {order.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in smart order deletion {order.Id}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Safely delete an accounting document by soft-deleting its history records and recalculating balances
        /// </summary>
        public async Task DeleteAccountingDocumentAsync(AccountingDocument document, string performedBy = "Admin")
        {
            try
            {
                _logger.LogInformation($"Starting smart document deletion: Document {document.Id} by {performedBy}");

                // Only process if document was verified (had financial impact)
                if (document.IsVerified)
                {
                    // 1. Soft delete customer balance history records for this document
                    await SoftDeleteCustomerBalanceHistoryAsync(null, document.Id, performedBy);

                    // 2. Soft delete bank account balance history records for this document
                    await SoftDeleteBankAccountBalanceHistoryAsync(document.Id, performedBy);
                }

                // 3. Soft delete the document itself
                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;
                document.DeletedBy = performedBy;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Smart document deletion completed: Document {document.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in smart document deletion {document.Id}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Soft delete customer balance history records and recalculate subsequent balances
        /// </summary>
        private async Task SoftDeleteCustomerBalanceHistoryAsync(int? orderId, int? documentId, string performedBy)
        {
            _logger.LogInformation($"SoftDeleteCustomerBalanceHistoryAsync - OrderId: {orderId}, DocumentId: {documentId}");
            
            // Find all history records for this order/document
            List<CustomerBalanceHistory> historyRecords = new List<CustomerBalanceHistory>();
            if (orderId.HasValue)
            {
                historyRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.ReferenceId == orderId.Value &&
                                h.TransactionType == CustomerBalanceTransactionType.Order &&
                                !h.IsDeleted)
                    .OrderBy(h => h.CreatedAt)
                    .ToListAsync();
            }
            else if (documentId.HasValue)
            {
                historyRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.ReferenceId == documentId.Value &&
                                h.TransactionType == CustomerBalanceTransactionType.AccountingDocument &&
                                !h.IsDeleted)
                    .OrderBy(h => h.CreatedAt)
                    .ToListAsync();
            }
            _logger.LogInformation($"Found {historyRecords.Count} customer balance history records to soft delete for Order {orderId} Document {documentId}");

            if (!historyRecords.Any())
            {
                _logger.LogWarning($"No customer balance history found for Order {orderId} Document {documentId}");
                return;
            }

            _logger.LogInformation($"Found {historyRecords.Count} customer balance history records to soft delete");

            // Group by customer and currency for separate recalculation
            var groups = historyRecords.GroupBy(h => new { h.CustomerId, h.CurrencyCode });

            foreach (var group in groups)
            {
                var customerId = group.Key.CustomerId;
                var currencyCode = group.Key.CurrencyCode;
                var recordsToDelete = group.OrderBy(r => r.CreatedAt).ToList();

                _logger.LogInformation($"Processing Customer {customerId} Currency {currencyCode}: {recordsToDelete.Count} records");

                // 1. Mark records as deleted
                foreach (var record in recordsToDelete)
                {
                    _logger.LogInformation($"Marking history record {record.Id} as deleted (Amount: {record.TransactionAmount})");
                    record.IsDeleted = true;
                    record.DeletedAt = DateTime.UtcNow;
                    record.DeletedBy = performedBy;
                }

                // 2. Recalculate balances for subsequent records - use the minimum ID of deleted records
                var minDeletedId = recordsToDelete.Min(r => r.Id);
                _logger.LogInformation($"Starting balance recalculation for Customer {customerId} Currency {currencyCode} from record ID {minDeletedId}");
                await RecalculateCustomerBalanceHistoryAsync(customerId, currencyCode, minDeletedId);

                // Save history changes first
                await _context.SaveChangesAsync();

                // 3. Update current balance table
                await RecalculateCurrentCustomerBalanceAsync(customerId, currencyCode);
            }

            _logger.LogInformation($"Saving final changes for customer balance history soft delete");
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete currency pool history records and recalculate subsequent balances
        /// </summary>
        private async Task SoftDeleteCurrencyPoolHistoryAsync(int? orderId, int? documentId, string performedBy)
        {
            _logger.LogInformation($"SoftDeleteCurrencyPoolHistoryAsync - OrderId: {orderId}, DocumentId: {documentId}");
            
            // Find all pool history records for this order/document
            var historyRecords = await _context.CurrencyPoolHistory
                .Where(h => h.ReferenceId == (orderId ?? documentId) && 
                           ((orderId.HasValue && h.TransactionType == CurrencyPoolTransactionType.Order) ||
                            (documentId.HasValue && h.TransactionType == CurrencyPoolTransactionType.Document)) &&
                           !h.IsDeleted)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();

            _logger.LogInformation($"Found {historyRecords.Count} currency pool history records to soft delete for Order {orderId} Document {documentId}");

            if (!historyRecords.Any())
            {
                _logger.LogWarning($"No currency pool history found for Order {orderId} Document {documentId}");
                return;
            }

            _logger.LogInformation($"Found {historyRecords.Count} currency pool history records to soft delete");

            // Group by currency for separate recalculation
            var groups = historyRecords.GroupBy(h => h.CurrencyCode);

            foreach (var group in groups)
            {
                var currencyCode = group.Key;
                var recordsToDelete = group.OrderBy(r => r.CreatedAt).ToList();

                _logger.LogInformation($"Processing Currency Pool {currencyCode}: {recordsToDelete.Count} records");

                // 1. Mark records as deleted
                foreach (var record in recordsToDelete)
                {
                    _logger.LogInformation($"Marking pool history record {record.Id} as deleted (Currency: {record.CurrencyCode}, Amount: {record.TransactionAmount})");
                    record.IsDeleted = true;
                    record.DeletedAt = DateTime.UtcNow;
                    record.DeletedBy = performedBy;
                }

                // 2. Recalculate balances for subsequent records - use the minimum ID of deleted records
                var minDeletedId = recordsToDelete.Min(r => r.Id);
                _logger.LogInformation($"Starting balance recalculation for Currency Pool {currencyCode} from record ID {minDeletedId}");
                await RecalculateCurrencyPoolHistoryAsync(currencyCode, minDeletedId);

                // Save history changes first
                await _context.SaveChangesAsync();

                // 3. Update current pool balance
                await RecalculateCurrentCurrencyPoolBalanceAsync(currencyCode);
            }

            _logger.LogInformation($"Saving final changes for currency pool history soft delete");
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete bank account balance history records and recalculate subsequent balances
        /// </summary>
        private async Task SoftDeleteBankAccountBalanceHistoryAsync(int documentId, string performedBy)
        {
            _logger.LogInformation($"SoftDeleteBankAccountBalanceHistoryAsync - DocumentId: {documentId}");
            
            // Find all bank account history records for this document
            var historyRecords = await _context.BankAccountBalanceHistory
                .Where(h => h.ReferenceId == documentId && h.TransactionType == BankAccountTransactionType.Document &&
                           !h.IsDeleted)
                .OrderBy(h => h.CreatedAt)
                .ToListAsync();

            if (!historyRecords.Any())
            {
                _logger.LogInformation($"No bank account balance history found for Document {documentId}");
                return;
            }

            _logger.LogInformation($"Found {historyRecords.Count} bank account balance history records to soft delete");

            // Group by bank account for separate recalculation
            var groups = historyRecords.GroupBy(h => h.BankAccountId);

            foreach (var group in groups)
            {
                var bankAccountId = group.Key;
                var recordsToDelete = group.OrderBy(r => r.CreatedAt).ToList();

                _logger.LogInformation($"Processing Bank Account {bankAccountId}: {recordsToDelete.Count} records");

                // 1. Mark records as deleted
                foreach (var record in recordsToDelete)
                {
                    _logger.LogInformation($"Marking bank account history record {record.Id} as deleted (Amount: {record.TransactionAmount})");
                    record.IsDeleted = true;
                    record.DeletedAt = DateTime.UtcNow;
                    record.DeletedBy = performedBy;
                }

                // 2. Recalculate balances for subsequent records - use the minimum ID of deleted records
                var minDeletedId = recordsToDelete.Min(r => r.Id);
                _logger.LogInformation($"Starting balance recalculation for Bank Account {bankAccountId} from record ID {minDeletedId}");
                await RecalculateBankAccountBalanceHistoryAsync(bankAccountId, minDeletedId);

                // Save history changes first
                await _context.SaveChangesAsync();

                // 3. Update current balance
                await RecalculateCurrentBankAccountBalanceAsync(bankAccountId);
            }

            _logger.LogInformation($"Saving final changes for bank account balance history soft delete");
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Recalculate customer balance history after soft deletion based on record ID position
        /// </summary>
        private async Task RecalculateCustomerBalanceHistoryAsync(int customerId, string currencyCode, long fromRecordId)
        {
            _logger.LogInformation($"Recalculating customer balance history for Customer {customerId} Currency {currencyCode} from record ID {fromRecordId}");
            
            // Get all non-deleted records after the deletion point, ordered by ID (which preserves exact sequence)
            var subsequentRecords = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId && 
                           h.CurrencyCode == currencyCode && 
                           h.Id > fromRecordId && 
                           !h.IsDeleted)
                .OrderBy(h => h.Id)
                .ToListAsync();

            if (!subsequentRecords.Any())
            {
                _logger.LogInformation($"No subsequent records to recalculate for Customer {customerId} Currency {currencyCode} after ID {fromRecordId}");
                return;
            }

            // Find the last non-deleted record before the deletion point
            var lastValidRecord = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId && 
                           h.CurrencyCode == currencyCode && 
                           h.Id < fromRecordId && 
                           !h.IsDeleted)
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            var runningBalance = lastValidRecord?.BalanceAfter ?? 0m;

            _logger.LogInformation($"Recalculating {subsequentRecords.Count} records from balance {runningBalance} (last valid record ID: {lastValidRecord?.Id ?? 0})");

            // Recalculate each subsequent record in exact sequential order
            foreach (var record in subsequentRecords)
            {
                var oldBalanceBefore = record.BalanceBefore;
                var oldBalanceAfter = record.BalanceAfter;
                
                record.BalanceBefore = runningBalance;
                record.BalanceAfter = runningBalance + record.TransactionAmount;
                runningBalance = record.BalanceAfter;
                
                _logger.LogInformation($"Record ID {record.Id}: BalanceBefore {oldBalanceBefore} -> {record.BalanceBefore}, BalanceAfter {oldBalanceAfter} -> {record.BalanceAfter}");
            }
        }

        /// <summary>
        /// Recalculate currency pool history after soft deletion based on record ID position
        /// </summary>
        private async Task RecalculateCurrencyPoolHistoryAsync(string currencyCode, long fromRecordId)
        {
            _logger.LogInformation($"Recalculating currency pool history for Currency {currencyCode} from record ID {fromRecordId}");
            
            // Get all non-deleted records after the deletion point, ordered by ID (which preserves exact sequence)
            var subsequentRecords = await _context.CurrencyPoolHistory
                .Where(h => h.CurrencyCode == currencyCode && 
                           h.Id > fromRecordId && 
                           !h.IsDeleted)
                .OrderBy(h => h.Id)
                .ToListAsync();

            if (!subsequentRecords.Any())
            {
                _logger.LogInformation($"No subsequent records to recalculate for Currency Pool {currencyCode} after ID {fromRecordId}");
                return;
            }

            // Find the last non-deleted record before the deletion point
            var lastValidRecord = await _context.CurrencyPoolHistory
                .Where(h => h.CurrencyCode == currencyCode && 
                           h.Id < fromRecordId && 
                           !h.IsDeleted)
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            var runningBalance = lastValidRecord?.BalanceAfter ?? 0m;

            _logger.LogInformation($"Recalculating {subsequentRecords.Count} records from balance {runningBalance} (last valid record ID: {lastValidRecord?.Id ?? 0})");

            // Recalculate each subsequent record in exact sequential order
            foreach (var record in subsequentRecords)
            {
                var oldBalanceBefore = record.BalanceBefore;
                var oldBalanceAfter = record.BalanceAfter;
                
                record.BalanceBefore = runningBalance;
                record.BalanceAfter = runningBalance + record.TransactionAmount;
                runningBalance = record.BalanceAfter;
                
                _logger.LogInformation($"Currency Pool Record ID {record.Id}: BalanceBefore {oldBalanceBefore} -> {record.BalanceBefore}, BalanceAfter {oldBalanceAfter} -> {record.BalanceAfter}");
            }
        }

        /// <summary>
        /// Recalculate bank account balance history after soft deletion based on record ID position
        /// </summary>
        private async Task RecalculateBankAccountBalanceHistoryAsync(int bankAccountId, long fromRecordId)
        {
            _logger.LogInformation($"Recalculating bank account balance history for Bank Account {bankAccountId} from record ID {fromRecordId}");
            
            // Get all non-deleted records after the deletion point, ordered by ID (which preserves exact sequence)
            var subsequentRecords = await _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId && 
                           h.Id > fromRecordId && 
                           !h.IsDeleted)
                .OrderBy(h => h.Id)
                .ToListAsync();

            if (!subsequentRecords.Any())
            {
                _logger.LogInformation($"No subsequent records to recalculate for Bank Account {bankAccountId} after ID {fromRecordId}");
                return;
            }

            // Find the last non-deleted record before the deletion point
            var lastValidRecord = await _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId && 
                           h.Id < fromRecordId && 
                           !h.IsDeleted)
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            var runningBalance = lastValidRecord?.BalanceAfter ?? 0m;

            _logger.LogInformation($"Recalculating {subsequentRecords.Count} records from balance {runningBalance} (last valid record ID: {lastValidRecord?.Id ?? 0})");

            // Recalculate each subsequent record in exact sequential order
            foreach (var record in subsequentRecords)
            {
                var oldBalanceBefore = record.BalanceBefore;
                var oldBalanceAfter = record.BalanceAfter;
                
                record.BalanceBefore = runningBalance;
                record.BalanceAfter = runningBalance + record.TransactionAmount;
                runningBalance = record.BalanceAfter;
                
                _logger.LogInformation($"Bank Account Record ID {record.Id}: BalanceBefore {oldBalanceBefore} -> {record.BalanceBefore}, BalanceAfter {oldBalanceAfter} -> {record.BalanceAfter}");
            }
        }

        /// <summary>
        /// Update current customer balance from latest non-deleted history
        /// </summary>
        private async Task RecalculateCurrentCustomerBalanceAsync(int customerId, string currencyCode)
        {
            _logger.LogInformation($"Recalculating current customer balance for Customer {customerId}, Currency {currencyCode}");
            
            var latestHistory = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId && h.CurrencyCode == currencyCode && !h.IsDeleted)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefaultAsync();

            var currentBalance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

            if (currentBalance != null)
            {
                var oldBalance = currentBalance.Balance;
                currentBalance.Balance = latestHistory?.BalanceAfter ?? 0m;
                currentBalance.LastUpdated = DateTime.UtcNow;
                currentBalance.Notes = "Recalculated after deletion";
                
                _logger.LogInformation($"Updated customer {customerId} {currencyCode} balance from {oldBalance} to {currentBalance.Balance}");
            }
            else
            {
                _logger.LogWarning($"No current balance record found for Customer {customerId}, Currency {currencyCode}");
            }
        }

        /// <summary>
        /// Update current currency pool balance from latest non-deleted history
        /// </summary>
        private async Task RecalculateCurrentCurrencyPoolBalanceAsync(string currencyCode)
        {
            _logger.LogInformation($"Recalculating current currency pool balance for Currency {currencyCode}");
            
            var latestHistory = await _context.CurrencyPoolHistory
                .Where(h => h.CurrencyCode == currencyCode && !h.IsDeleted)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefaultAsync();

            var currentPool = await _context.CurrencyPools
                .FirstOrDefaultAsync(cp => cp.CurrencyCode == currencyCode && cp.IsActive);

            if (currentPool != null)
            {
                var oldBalance = currentPool.Balance;
                currentPool.Balance = latestHistory?.BalanceAfter ?? 0m;
                currentPool.LastUpdated = DateTime.UtcNow;
                currentPool.Notes = "Recalculated after deletion";
                
                _logger.LogInformation($"Updated currency pool {currencyCode} balance from {oldBalance} to {currentPool.Balance}");
            }
            else
            {
                _logger.LogWarning($"No current pool record found for Currency {currencyCode}");
            }
        }

        /// <summary>
        /// Update current bank account balance from latest non-deleted history
        /// </summary>
        private async Task RecalculateCurrentBankAccountBalanceAsync(int bankAccountId)
        {
            _logger.LogInformation($"Recalculating current bank account balance for Bank Account {bankAccountId}");
            
            var latestHistory = await _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId && !h.IsDeleted)
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefaultAsync();

            var currentBalance = await _context.BankAccountBalances
                .FirstOrDefaultAsync(bab => bab.BankAccountId == bankAccountId);

            if (currentBalance != null)
            {
                var oldBalance = currentBalance.Balance;
                currentBalance.Balance = latestHistory?.BalanceAfter ?? 0m;
                currentBalance.LastUpdated = DateTime.UtcNow;
                
                _logger.LogInformation($"Updated bank account {bankAccountId} balance from {oldBalance} to {currentBalance.Balance}");
            }
            else
            {
                _logger.LogWarning($"No current balance record found for Bank Account {bankAccountId}");
            }
        }

        #endregion

        #region Admin Methods for Deleted Records (Bypass Global Query Filters)

        /// <summary>
        /// Get all orders including deleted ones (for admin purposes)
        /// </summary>
        public async Task<List<Order>> GetAllOrdersIncludingDeletedAsync()
        {
            return await _context.Orders.IgnoreQueryFilters().ToListAsync();
        }

        /// <summary>
        /// Get all accounting documents including deleted ones (for admin purposes)
        /// </summary>
        public async Task<List<AccountingDocument>> GetAllDocumentsIncludingDeletedAsync()
        {
            return await _context.AccountingDocuments.IgnoreQueryFilters().ToListAsync();
        }

        /// <summary>
        /// Get only deleted orders (for admin recovery purposes)
        /// </summary>
        public async Task<List<Order>> GetDeletedOrdersAsync()
        {
            return await _context.Orders.IgnoreQueryFilters().Where(o => o.IsDeleted).ToListAsync();
        }

        /// <summary>
        /// Get only deleted accounting documents (for admin recovery purposes)
        /// </summary>
        public async Task<List<AccountingDocument>> GetDeletedDocumentsAsync()
        {
            return await _context.AccountingDocuments.IgnoreQueryFilters().Where(d => d.IsDeleted).ToListAsync();
        }

        /// <summary>
        /// Restore a soft-deleted order (for admin recovery)
        /// </summary>
        public async Task RestoreOrderAsync(int orderId, string performedBy = "Admin")
        {
            var order = await _context.Orders.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == orderId && o.IsDeleted);
            if (order != null)
            {
                order.IsDeleted = false;
                order.DeletedAt = null;
                order.DeletedBy = null;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Order {orderId} restored by {performedBy}");
            }
        }

        /// <summary>
        /// Restore a soft-deleted accounting document (for admin recovery)
        /// </summary>
        public async Task RestoreDocumentAsync(int documentId, string performedBy = "Admin")
        {
            var document = await _context.AccountingDocuments.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == documentId && d.IsDeleted);
            if (document != null)
            {
                document.IsDeleted = false;
                document.DeletedAt = null;
                document.DeletedBy = null;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Document {documentId} restored by {performedBy}");
            }
        }

        #endregion

        #region Manual Balance History Creation

        /// <summary>
        /// Creates a manual customer balance history record with specified transaction date
        /// This is useful for manual adjustments, corrections, or importing historical data
        /// After creating manual records, use RecalculateAllBalancesFromTransactionDatesAsync to ensure coherence
        /// </summary>
        public async Task CreateManualCustomerBalanceHistoryAsync(
            int customerId, 
            string currencyCode, 
            decimal amount, 
            string reason, 
            DateTime transactionDate, 
            string performedBy = "Manual Entry")
        {
            _logger.LogInformation($"Creating manual customer balance history: Customer {customerId}, Currency {currencyCode}, Amount {amount}, Date {transactionDate:yyyy-MM-dd}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate customer exists
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                if (customer == null)
                {
                    throw new ArgumentException($"Customer with ID {customerId} not found");
                }

                // Validate currency exists
                var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == currencyCode);
                if (currency == null)
                {
                    throw new ArgumentException($"Currency with code {currencyCode} not found");
                }

                // Get current balance for this customer/currency to calculate before/after
                var currentBalance = await _context.CustomerBalances
                    .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

                // For manual entries, we set temporary balance fields that will be corrected during recalculation
                var tempBalanceBefore = currentBalance?.Balance ?? 0m;
                var tempBalanceAfter = tempBalanceBefore + amount;

                // Create the manual history record
                var historyRecord = new CustomerBalanceHistory
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    BalanceBefore = tempBalanceBefore, // Temporary - will be corrected during recalculation
                    TransactionAmount = amount,
                    BalanceAfter = tempBalanceAfter, // Temporary - will be corrected during recalculation
                    TransactionType = CustomerBalanceTransactionType.Manual,
                    ReferenceId = null, // Manual entries don't have reference IDs
                    Description = reason,
                    TransactionDate = transactionDate, // Use the specified date
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy
                };

                _context.CustomerBalanceHistory.Add(historyRecord);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Manual customer balance history created: ID {historyRecord.Id}, Customer {customerId}, Currency {currencyCode}, Amount {amount}");
                _logger.LogWarning("IMPORTANT: Run RecalculateAllBalancesFromTransactionDatesAsync() to ensure chronological balance coherence");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating manual customer balance history: Customer {customerId}, Currency {currencyCode}, Amount {amount}");
                throw;
            }
        }

        #endregion

        #region Balance Recalculation Based on Transaction Dates

        /// <summary>
        /// Recalculates all balances based on transaction dates in chronological order.
        /// This method should be used after fixing transaction dates to ensure balance accuracy.
        /// </summary>
        public async Task RecalculateAllBalancesFromTransactionDatesAsync(string performedBy = "System")
        {
            _logger.LogInformation("Starting complete balance recalculation based on transaction dates");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Step 1: Reset all current balances to zero
                await ResetAllBalancesToZeroAsync();

                // Step 2: Recalculate customer balances from history in chronological order
                await RecalculateCustomerBalancesFromHistoryAsync();

                // Step 3: Recalculate currency pool balances from history in chronological order
                await RecalculateCurrencyPoolBalancesFromHistoryAsync();

                // Step 4: Recalculate bank account balances from history in chronological order
                await RecalculateBankAccountBalancesFromHistoryAsync();

                await transaction.CommitAsync();
                _logger.LogInformation("Successfully completed balance recalculation based on transaction dates");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to recalculate balances from transaction dates");
                throw;
            }
        }

        private async Task ResetAllBalancesToZeroAsync()
        {
            _logger.LogInformation("Resetting all balances to zero");

            // Reset customer balances
            var customerBalances = await _context.CustomerBalances.ToListAsync();
            foreach (var balance in customerBalances)
            {
                balance.Balance = 0;
                balance.LastUpdated = DateTime.UtcNow;
            }

            // Reset currency pool balances
            var poolBalances = await _context.CurrencyPools.ToListAsync();
            foreach (var pool in poolBalances)
            {
                pool.Balance = 0;
                pool.LastUpdated = DateTime.UtcNow;
            }

            // Reset bank account balances
            var bankBalances = await _context.BankAccountBalances.ToListAsync();
            foreach (var balance in bankBalances)
            {
                balance.Balance = 0;
                balance.LastUpdated = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("All balances reset to zero");
        }

        private async Task RecalculateCustomerBalancesFromHistoryAsync()
        {
            _logger.LogInformation("Recalculating customer balances from history");

            // Get all customer history records ordered by transaction date
            var historyRecords = await _context.CustomerBalanceHistory
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id) // Secondary sort for same transaction dates
                .ToListAsync();

            _logger.LogInformation($"Processing {historyRecords.Count} customer history records");

            // Process each record in chronological order
            foreach (var history in historyRecords)
            {
                // Get or create current balance record
                var currentBalance = await _context.CustomerBalances
                    .FirstOrDefaultAsync(cb => cb.CustomerId == history.CustomerId && cb.CurrencyCode == history.CurrencyCode);

                if (currentBalance == null)
                {
                    currentBalance = new CustomerBalance
                    {
                        CustomerId = history.CustomerId,
                        CurrencyCode = history.CurrencyCode,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.CustomerBalances.Add(currentBalance);
                }

                // Apply the transaction amount to current balance
                currentBalance.Balance += history.TransactionAmount;
                currentBalance.LastUpdated = DateTime.UtcNow;

                // Update the history record's balance fields to match recalculation
                history.BalanceBefore = currentBalance.Balance - history.TransactionAmount;
                history.BalanceAfter = currentBalance.Balance;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Customer balance recalculation completed");
        }

        private async Task RecalculateCurrencyPoolBalancesFromHistoryAsync()
        {
            _logger.LogInformation("Recalculating currency pool balances from history");

            // Get all pool history records ordered by transaction date
            var historyRecords = await _context.CurrencyPoolHistory
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id) // Secondary sort for same transaction dates
                .ToListAsync();

            _logger.LogInformation($"Processing {historyRecords.Count} currency pool history records");

            // Process each record in chronological order
            foreach (var history in historyRecords)
            {
                // Get or create current pool record
                var currentPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(cp => cp.CurrencyCode == history.CurrencyCode);

                if (currentPool == null)
                {
                    currentPool = new CurrencyPool
                    {
                        CurrencyCode = history.CurrencyCode,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.CurrencyPools.Add(currentPool);
                }

                // Apply the transaction amount to current balance
                currentPool.Balance += history.TransactionAmount;
                currentPool.LastUpdated = DateTime.UtcNow;

                // Update the history record's balance fields to match recalculation
                history.BalanceBefore = currentPool.Balance - history.TransactionAmount;
                history.BalanceAfter = currentPool.Balance;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Currency pool balance recalculation completed");
        }

        private async Task RecalculateBankAccountBalancesFromHistoryAsync()
        {
            _logger.LogInformation("Recalculating bank account balances from history");

            // Get all bank account history records ordered by transaction date
            var historyRecords = await _context.BankAccountBalanceHistory
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id) // Secondary sort for same transaction dates
                .ToListAsync();

            _logger.LogInformation($"Processing {historyRecords.Count} bank account history records");

            // Process each record in chronological order
            foreach (var history in historyRecords)
            {
                // Get or create current balance record
                var currentBalance = await _context.BankAccountBalances
                    .FirstOrDefaultAsync(bb => bb.BankAccountId == history.BankAccountId);

                if (currentBalance == null)
                {
                    currentBalance = new BankAccountBalance
                    {
                        BankAccountId = history.BankAccountId,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.BankAccountBalances.Add(currentBalance);
                }

                // Apply the transaction amount to current balance
                currentBalance.Balance += history.TransactionAmount;
                currentBalance.LastUpdated = DateTime.UtcNow;

                // Update the history record's balance fields to match recalculation
                history.BalanceBefore = currentBalance.Balance - history.TransactionAmount;
                history.BalanceAfter = currentBalance.Balance;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Bank account balance recalculation completed");
        }

        #endregion

    }
}
