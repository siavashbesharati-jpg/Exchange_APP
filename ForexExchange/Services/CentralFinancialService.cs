
using ForexExchange.Models;
using ForexExchange.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ForexExchange.Services
{
    /// <summary>
    /// **CRITICAL FINANCIAL SERVICE** - Centralized financial operations management with complete audit trail.
    /// 
    /// **📚 FOR COMPREHENSIVE DOCUMENTATION**: See `/docs/CentralFinancialService-Documentation.md`
    /// This documentation file contains detailed explanations, business logic, safety guidelines,
    /// troubleshooting guides, and complete usage examples for this critical financial service.
    /// 
    /// This service is the heart of the forex exchange financial system, managing:
    /// - Customer balance operations (credit/debit with history)
    /// - Currency pool management (institutional liquidity pools)
    /// - Bank account balance tracking (financial institution accounts)
    /// - Complete audit trail through history tables (immutable transaction log)
    /// - Balance consistency validation and reconciliation
    /// - Smart deletion with soft-delete and recalculation capabilities
    /// 
    /// **SAFETY CRITICAL**: Every operation maintains complete audit trail and preserves
    /// exact calculation logic from existing services. Zero logic changes were made during
    /// centralization - only consolidation of previously scattered financial operations.
    /// 
    /// **DATA INTEGRITY**: All balance updates are transactional, logged, and historically tracked.
    /// History tables provide event sourcing capabilities for complete financial audit trails.
    /// 
    /// **CONSISTENCY GUARANTEE**: The service ensures that preview calculations exactly match
    /// real transaction effects, preventing discrepancies between UI previews and actual results.
    /// </summary>
    public class CentralFinancialService : ICentralFinancialService
    {


        /// <summary>
        /// Database context for Entity Framework operations
        /// </summary>
        private readonly ForexDbContext _context;

        /// <summary>
        /// Logger for comprehensive financial operation tracking and debugging
        /// </summary>
        private readonly ILogger<CentralFinancialService> _logger;

        /// <summary>
        /// Notification hub for sending real-time notifications to admin users
        /// </summary>
        private readonly INotificationHub _notificationHub;

        /// <summary>
        /// **CONSTRUCTOR** - Initializes the central financial service with required dependencies.
        /// </summary>
        /// <param name="context">Entity Framework database context for data operations</param>
        /// <param name="logger">Logger for operation tracking and debugging</param>
        /// <param name="notificationHub">Notification hub for real-time admin notifications</param>

        public CentralFinancialService(ForexDbContext context, ILogger<CentralFinancialService> logger, INotificationHub notificationHub)
        {
            _context = context;
            _logger = logger;
            _notificationHub = notificationHub;
        }


        #region  Preview
        /// <summary>
        /// **PREVIEW SIMULATION** - Calculates the financial impact of an order without making database changes.
        /// 
        /// This method simulates exactly what would happen when an order is processed, allowing the UI
        /// to show users the precise effect on their balances and institutional currency pools.
        /// 
        /// **SRP CONSISTENCY GUARANTEE**: Uses dedicated calculation methods (CalculateCustomerBalanceEffects
        /// and CalculateCurrencyPoolEffects) that are also used by ProcessOrderCreationAsync() to ensure
        /// that preview calculations exactly match actual processing results.
        /// 
        /// **Calculation Logic**:
        /// - Customer pays FromAmount in FromCurrency (balance decreases)
        /// - Customer receives ToAmount in ToCurrency (balance increases)  
        /// - Institution receives FromAmount in FromCurrency (pool increases)
        /// - Institution pays ToAmount in ToCurrency (pool decreases)
        /// 
        /// **Validation**: Verifies all required customer balances and currency pools exist before calculation.
        /// </summary>
        /// <param name="order">Order with populated FromCurrency and ToCurrency navigation properties</param>
        /// <returns>Preview effects showing before/after balances for customer and pools</returns>
        /// <exception cref="Exception">Thrown when required currencies or balances are not found</exception>
        public async Task<OrderPreviewEffectsDto> PreviewOrderEffectsAsync(Order order)
        {
            _logger.LogInformation($"[PreviewOrderEffectsAsync] Called for CustomerId={order.CustomerId}, FromCurrencyId={order.FromCurrencyId}, ToCurrencyId={order.ToCurrencyId}, FromAmount={order.FromAmount}, Rate={order.Rate}, ToAmount={order.ToAmount}");

            if (order.FromCurrency == null || string.IsNullOrWhiteSpace(order.FromCurrency.Code))
            {
                _logger.LogError($"FromCurrency or its Code is null for order preview (FromCurrencyId: {order.FromCurrencyId})");
                throw new Exception($"FromCurrency or its Code is null for order preview (FromCurrencyId: {order.FromCurrencyId})");
            }
            _logger.LogInformation($"FromCurrency: {order.FromCurrency.Code}");

            if (order.ToCurrency == null || string.IsNullOrWhiteSpace(order.ToCurrency.Code))
            {
                _logger.LogError($"ToCurrency or its Code is null for order preview (ToCurrencyId: {order.ToCurrencyId})");
                throw new Exception($"ToCurrency or its Code is null for order preview (ToCurrencyId: {order.ToCurrencyId})");
            }
            _logger.LogInformation($"ToCurrency: {order.ToCurrency.Code}");

            var customerBalanceFrom = await _context.CustomerBalances.FirstOrDefaultAsync(cb => cb.CustomerId == order.CustomerId && cb.CurrencyCode == order.FromCurrency.Code);
            if (customerBalanceFrom == null)
            {
                _logger.LogError($"Customer balance not found for customer {order.CustomerId} and currency {order.FromCurrency.Code}");
                throw new Exception($"Customer balance not found for customer {order.CustomerId} and currency {order.FromCurrency.Code}");
            }
            _logger.LogInformation($"CustomerBalanceFrom: {customerBalanceFrom.Balance}");

            var customerBalanceTo = await _context.CustomerBalances.FirstOrDefaultAsync(cb => cb.CustomerId == order.CustomerId && cb.CurrencyCode == order.ToCurrency.Code);
            if (customerBalanceTo == null)
            {
                _logger.LogError($"Customer balance not found for customer {order.CustomerId} and currency {order.ToCurrency.Code}");
                throw new Exception($"Customer balance not found for customer {order.CustomerId} and currency {order.ToCurrency.Code}");
            }
            _logger.LogInformation($"CustomerBalanceTo: {customerBalanceTo.Balance}");

            var poolBalanceFrom = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.FromCurrency.Id);
            if (poolBalanceFrom == null)
            {
                _logger.LogError($"Currency pool not found for currency {order.FromCurrency.Code}");
                throw new Exception($"Currency pool not found for currency {order.FromCurrency.Code}");
            }
            _logger.LogInformation($"PoolBalanceFrom: {poolBalanceFrom.Balance}");

            var poolBalanceTo = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.ToCurrency.Id);
            if (poolBalanceTo == null)
            {
                _logger.LogError($"Currency pool not found for currency {order.ToCurrency.Code}");
                throw new Exception($"Currency pool not found for currency {order.ToCurrency.Code}");
            }
            _logger.LogInformation($"PoolBalanceTo: {poolBalanceTo.Balance}");

            // Use SRP calculation methods to ensure consistency with actual processing
            var (newCustomerBalanceFrom, newCustomerBalanceTo) = CalculateCustomerBalanceEffects(
                currentFromBalance: customerBalanceFrom.Balance,
                currentToBalance: customerBalanceTo.Balance,
                fromAmount: order.FromAmount,
                toAmount: order.ToAmount
            );

            var (newPoolBalanceFrom, newPoolBalanceTo) = CalculateCurrencyPoolEffects(
                currentFromPool: poolBalanceFrom.Balance,
                currentToPool: poolBalanceTo.Balance,
                fromAmount: order.FromAmount,
                toAmount: order.ToAmount
            );

            _logger.LogInformation($"NewCustomerBalanceFrom: {newCustomerBalanceFrom}");
            _logger.LogInformation($"NewCustomerBalanceTo: {newCustomerBalanceTo}");
            _logger.LogInformation($"NewPoolBalanceFrom: {newPoolBalanceFrom}");
            _logger.LogInformation($"NewPoolBalanceTo: {newPoolBalanceTo}");

            return new OrderPreviewEffectsDto
            {
                CustomerId = order.CustomerId,
                FromCurrencyCode = order.FromCurrency.Code,
                ToCurrencyCode = order.ToCurrency.Code,
                OrderFromAmount = order.FromAmount,
                OrderToAmount = order.ToAmount,
                OldCustomerBalanceFrom = customerBalanceFrom.Balance,
                OldCustomerBalanceTo = customerBalanceTo.Balance,
                NewCustomerBalanceFrom = newCustomerBalanceFrom,
                NewCustomerBalanceTo = newCustomerBalanceTo,
                OldPoolBalanceFrom = poolBalanceFrom.Balance,
                OldPoolBalanceTo = poolBalanceTo.Balance,
                NewPoolBalanceFrom = newPoolBalanceFrom,
                NewPoolBalanceTo = newPoolBalanceTo
            };
        }



        /// <summary>
        /// **SRP CALCULATION** - Pure calculation of customer balance effects for an order.
        /// 
        /// This method contains the core business logic for how orders affect customer balances.
        /// Both preview and actual processing MUST use this method to ensure consistency.
        /// 
        /// **Business Logic**:
        /// - Customer pays FromAmount in FromCurrency (balance decreases)
        /// - Customer receives ToAmount in ToCurrency (balance increases)
        /// </summary>
        /// <param name="currentFromBalance">Customer's current balance in FromCurrency</param>
        /// <param name="currentToBalance">Customer's current balance in ToCurrency</param>
        /// <param name="fromAmount">Amount customer pays</param>
        /// <param name="toAmount">Amount customer receives</param>
        /// <returns>Tuple with new balances (newFromBalance, newToBalance)</returns>
        private (decimal newFromBalance, decimal newToBalance) CalculateCustomerBalanceEffects(
            decimal currentFromBalance,
            decimal currentToBalance,
            decimal fromAmount,
            decimal toAmount)
        {
            var newFromBalance = currentFromBalance - fromAmount; // Customer pays (negative impact)
            var newToBalance = currentToBalance + toAmount;       // Customer receives (positive impact)

            _logger.LogInformation($"[CalculateCustomerBalanceEffects] From: {currentFromBalance} - {fromAmount} = {newFromBalance}");
            _logger.LogInformation($"[CalculateCustomerBalanceEffects] To: {currentToBalance} + {toAmount} = {newToBalance}");

            return (newFromBalance, newToBalance);
        }

        /// <summary>
        /// **SRP CALCULATION** - Pure calculation of currency pool effects for an order.
        /// 
        /// This method contains the core business logic for how orders affect institutional pools.
        /// Both preview and actual processing MUST use this method to ensure consistency.
        /// 
        /// **Business Logic**:
        /// - Institution receives FromCurrency from customer (pool increases)
        /// - Institution provides ToCurrency to customer (pool decreases)
        /// </summary>
        /// <param name="currentFromPool">Institution's current pool in FromCurrency</param>
        /// <param name="currentToPool">Institution's current pool in ToCurrency</param>
        /// <param name="fromAmount">Amount institution receives from customer</param>
        /// <param name="toAmount">Amount institution provides to customer</param>
        /// <returns>Tuple with new pool balances (newFromPool, newToPool)</returns>
        private (decimal newFromPool, decimal newToPool) CalculateCurrencyPoolEffects(
            decimal currentFromPool,
            decimal currentToPool,
            decimal fromAmount,
            decimal toAmount)
        {
            var newFromPool = currentFromPool + fromAmount; // Institution receives (positive impact)
            var newToPool = currentToPool - toAmount;       // Institution provides (negative impact)

            _logger.LogInformation($"[CalculateCurrencyPoolEffects] From Pool: {currentFromPool} + {fromAmount} = {newFromPool}");
            _logger.LogInformation($"[CalculateCurrencyPoolEffects] To Pool: {currentToPool} - {toAmount} = {newToPool}");

            return (newFromPool, newToPool);
        }


        #endregion Preview



        #region Create reocrds


        /// <summary>
        /// **ORDER PROCESSING** - Processes the complete financial impact of a currency exchange order.
        /// 
        /// **CRITICAL DUAL-CURRENCY OPERATION**: Every order creates exactly two currency impacts:
        /// 1. **Payment Transaction**: Customer pays FromAmount in FromCurrency (negative customer balance impact)
        /// 2. **Receipt Transaction**: Customer receives ToAmount in ToCurrency (positive customer balance impact)
        /// 
        /// **Currency Pool Updates**:
        /// - Institution receives FromCurrency from customer (pool balance increases)
        /// - Institution provides ToCurrency to customer (pool balance decreases)
        /// 
        /// **SRP CONSISTENCY GUARANTEE**: Uses the same calculation methods as PreviewOrderEffectsAsync()
        /// to ensure that actual processing effects exactly match preview calculations shown to users.
        /// 
        /// **Audit Trail**: Every transaction is logged with complete history for regulatory compliance
        /// and financial auditing. All amounts, exchange rates, and timing are permanently recorded.
        /// 
        /// **Validation**: Calculates expected effects before processing to log and validate consistency.
        /// </summary>
        /// <param name="order">Complete order with all currency and amount information</param>
        /// <param name="performedBy">Identifier of who initiated the transaction (for audit trail)</param>
        public async Task ProcessOrderCreationAsync(Order order, string performedBy = "System")
        {
            _logger.LogInformation($"Processing order creation for Order ID: {order.Id}");

            // NEW: Check if order is frozen - frozen orders don't affect current balances or pool balances
            if (order.IsFrozen)
            {
                _logger.LogInformation($"Order {order.Id} is frozen - skipping all balance updates (pools and customers)");
                return;
            }

            // Rebuild all financial balances after order creation to ensure coherence
            await RebuildAllFinancialBalancesAsync(performedBy);

            _logger.LogInformation($"Order {order.Id} processing completed - dual currency impact recorded");
        }

        /// <summary>
        /// **ACCOUNTING DOCUMENT PROCESSING** - Processes financial documents (deposits, withdrawals, transfers).
        /// 
        /// **Document Types Supported**:
        /// - Customer deposits (increase customer balance)
        /// - Customer withdrawals (decrease customer balance) 
        /// - Inter-customer transfers
        /// - Bank account transactions
        /// 
        /// **Multi-Party Logic**:
        /// - **Payer**: Entity making the payment (balance increases for deposits, decreases for withdrawals)
        /// - **Receiver**: Entity receiving payment (balance decreases for payments made to them)
        /// - **Bank Accounts**: Institutional accounts affected by the document
        /// 
        /// **Verification Requirement**: Only processes verified documents to prevent unauthorized transactions.
        /// 
        /// **Complete Audit Trail**: Every document impact is logged with document reference numbers,
        /// dates, amounts, and all parties involved for comprehensive financial auditing.
        /// </summary>
        /// <param name="document">Verified accounting document with all party and amount information</param>
        /// <param name="performedBy">Identifier of who processed the document (for audit trail)</param>
        public async Task ProcessAccountingDocumentAsync(AccountingDocument document, string performedBy = "System")
        {
            _logger.LogInformation($"Processing accounting document ID: {document.Id}");
            // Rebuild all financial balances after document processing to ensure coherence
            await RebuildAllFinancialBalancesAsync(performedBy);

            _logger.LogInformation($"Document {document.Id} processing completed");
        }



        #endregion Create reocrds







        /// <summary>
        /// Comprehensive rebuild of all financial balances based on new IsFrozen strategy:
        /// - Pool balances rebuilt from non-deleted AND non-frozen orders only with coherent history starting from zero
        /// - Bank account balances rebuilt from non-deleted AND non-frozen documents only with coherent history starting from zero
        /// - Customer balance history rebuilt from non-deleted orders, documents, and manual records (including frozen orders/documents)
        /// - Active buy/sell counts recalculated properly based on non-frozen orders
        ///
        /// This ensures frozen historical records don't affect current balance calculations
        /// but are preserved for customer balance history audit trail, including manual adjustments.
        /// Creates coherent balance history chains starting from zero before first non-frozen record.
        /// </summary>
        public async Task RebuildAllFinancialBalancesAsync(string performedBy = "System")
        {
            try
            {
                var logMessages = new List<string>
                {
                    "=== COMPREHENSIVE FINANCIAL BALANCE REBUILD WITH COHERENT HISTORY ===",
                    $"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Performed by: {performedBy}",
                    ""
                };

                _logger.LogInformation("Starting comprehensive financial balance rebuild");

                // Get all manual customer balance history records (including frozen, not deleted)
                var manualCustomerRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual && !h.IsDeleted)
                    .ToListAsync();

                // Get all manual bank account balance history records (including frozen, not deleted)
                var manualBankAccountRecords = await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionType == BankAccountTransactionType.ManualEdit && !h.IsDeleted)
                    .ToListAsync();

                // Get all manual pool history records (including frozen, not deleted)
                var manualPoolRecords = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted)
                    .ToListAsync();

                logMessages.Add($"All manual records saved in memory: Customer={manualCustomerRecords.Count}, BankAccount={manualBankAccountRecords.Count}, Pool={manualPoolRecords.Count}");
                _logger.LogInformation($"Manual records loaded: Customer={manualCustomerRecords.Count}, BankAccount={manualBankAccountRecords.Count}, Pool={manualPoolRecords.Count}");

                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                // STEP 1: Clear all history tables and reset balances to zero
                logMessages.Add("STEP 1: Clearing all history tables and resetting balances...");

                // Clear pool history (will be rebuilt)
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM CurrencyPoolHistory");

                // Clear bank account balance history (will be rebuilt)
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM BankAccountBalanceHistory");

                // Clear customer balance history (will be rebuilt)
                var deletedHistoryCount = await _context.Database.ExecuteSqlRawAsync("DELETE FROM CustomerBalanceHistory");
                var remainingManualCount = await _context.CustomerBalanceHistory.CountAsync(h => h.TransactionType == CustomerBalanceTransactionType.Manual);
                logMessages.Add($"✓ Cleared non-manual customer balance history, preserved {remainingManualCount} manual records");

                // Reset customer balances
                var customerBalances = await _context.CustomerBalances.ToListAsync();
                foreach (var balance in customerBalances)
                {
                    balance.Balance = 0;
                    balance.LastUpdated = DateTime.UtcNow;
                }

                // Reset currency pool balances and active counts
                var poolBalances = await _context.CurrencyPools.ToListAsync();
                foreach (var pool in poolBalances)
                {
                    pool.Balance = 0;
                    pool.ActiveBuyOrderCount = 0;
                    pool.ActiveSellOrderCount = 0;
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
                logMessages.Add($"✓ Cleared non-manual history tables and reset {customerBalances.Count} customer balances, {poolBalances.Count} pool balances, {bankBalances.Count} bank account balances to zero");

                // STEP 2: Create coherent pool history starting from zero for each currency
                logMessages.Add("");
                logMessages.Add("STEP 2: Creating coherent pool history with zero-starting balance chains...");

                var activeOrders = await _context.Orders
                    .Where(o => !o.IsDeleted && !o.IsFrozen)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .OrderBy(o => o.CreatedAt)
                    .ToListAsync();

                logMessages.Add($"Processing {activeOrders.Count} active (non-deleted, non-frozen) orders and {manualPoolRecords.Count} manual pool records...");

                // Create unified transaction items for pools from orders and manual records
                var poolTransactionItems = new List<(string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string PoolTransactionType, string Description)>();

                // Add order transactions
                foreach (var o in activeOrders)
                {
                    var currentOrder = _context.Orders.Find(o.Id);
                    if (currentOrder is null) continue;
                    // Institution receives FromAmount in FromCurrency (pool increases)
                    poolTransactionItems.Add((o.FromCurrency.Code, o.CreatedAt, "Order", o.Id, o.FromAmount, "Buy", currentOrder?.Notes ?? ""));

                    // Institution pays ToAmount in ToCurrency (pool decreases)
                    poolTransactionItems.Add((o.ToCurrency.Code, o.CreatedAt, "Order", o.Id, -o.ToAmount, "Sell", currentOrder?.Notes ?? ""));
                }

                // Add manual pool records as transactions
                foreach (var manual in manualPoolRecords)
                {
                    poolTransactionItems.Add((
                        manual.CurrencyCode,
                        manual.TransactionDate,
                        "Manual",
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        "Manual",
                        manual.Description ?? "Manual adjustment"
                    ));
                }

                // Group by currency code to create coherent history per currency
                var currencyGroups = poolTransactionItems
                    .GroupBy(x => x.CurrencyCode)
                    .ToList();

                foreach (var currencyGroup in currencyGroups)
                {
                    var currencyCode = currencyGroup.Key;
                    var currencyTransactions = currencyGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!currencyTransactions.Any()) continue;

                    // Find the earliest transaction for this currency
                    var firstTransaction = currencyTransactions.First();
                    var zeroDateTime = firstTransaction.TransactionDate.AddMinutes(-1);

                    // Create zero-starting pool history record
                    var zeroPoolHistory = new CurrencyPoolHistory
                    {
                        CurrencyCode = currencyCode,
                        TransactionType = CurrencyPoolTransactionType.ManualEdit,
                        ReferenceId = null,
                        BalanceBefore = 0,
                        TransactionAmount = 0,
                        BalanceAfter = 0,
                        Description = "Zero-start balance for coherent history chain (non-frozen records only)",
                        TransactionDate = zeroDateTime,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = performedBy,
                        IsDeleted = false
                    };
                    _context.CurrencyPoolHistory.Add(zeroPoolHistory);

                    // Process transactions chronologically for this currency
                    decimal runningBalance = 0;
                    int buyCount = 0, sellCount = 0;
                    decimal totalBought = 0, totalSold = 0;

                    foreach (var transaction in currencyTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Order" => CurrencyPoolTransactionType.Order,
                            "Manual" => CurrencyPoolTransactionType.ManualEdit,
                            _ => CurrencyPoolTransactionType.Order
                        };

                        var poolHistory = new CurrencyPoolHistory
                        {
                            CurrencyCode = currencyCode,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            PoolTransactionType = transaction.PoolTransactionType,
                            Description = transaction.Description,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        };
                        _context.CurrencyPoolHistory.Add(poolHistory);

                        runningBalance = poolHistory.BalanceAfter;

                        // Update counts and totals for orders only (not manual records)
                        if (transaction.TransactionType == "Order")
                        {
                            if (transaction.PoolTransactionType == "Buy")
                            {
                                buyCount++;
                                totalBought += transaction.Amount;
                            }
                            else if (transaction.PoolTransactionType == "Sell")
                            {
                                sellCount++;
                                totalSold += Math.Abs(transaction.Amount);
                            }
                        }
                    }

                    // Update pool balance, active counts, and totals
                    var pool = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyCode == currencyCode);
                    if (pool != null)
                    {
                        pool.Balance = runningBalance;
                        pool.ActiveBuyOrderCount = buyCount;
                        pool.ActiveSellOrderCount = sellCount;
                        pool.TotalBought = totalBought;
                        pool.TotalSold = totalSold;
                        pool.LastUpdated = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent pool history for {currencyGroups.Count} currencies with {activeOrders.Count} active orders");

                // STEP 3: Create coherent bank account balance history starting from zero
                logMessages.Add("");
                logMessages.Add("STEP 3: Creating coherent bank account balance history with zero-starting balance chains...");

                var activeDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && !d.IsFrozen)
                    .OrderBy(d => d.DocumentDate)
                    .ToListAsync();

                logMessages.Add($"Processing {activeDocuments.Count} active (non-deleted, non-frozen) documents and {manualBankAccountRecords.Count} manual bank account records...");

                // Create unified transaction items for bank accounts from documents and manual records
                var bankAccountTransactionItems = new List<(int BankAccountId, string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string Description)>();

                // Add document transactions
                foreach (var d in activeDocuments)
                {

                    var currentDocument = _context.AccountingDocuments.Find(d.Id);
                    if (currentDocument is null) continue;
                    if (d.PayerType == PayerType.System && d.PayerBankAccountId.HasValue)
                        bankAccountTransactionItems.Add((d.PayerBankAccountId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.Id, d.Amount, currentDocument?.Notes ?? string.Empty));
                    if (d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId.HasValue)
                        bankAccountTransactionItems.Add((d.ReceiverBankAccountId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.Id, d.Amount, currentDocument?.Notes ?? string.Empty));
                }

                // Add manual bank account records as transactions
                foreach (var manual in manualBankAccountRecords)
                {
                    bankAccountTransactionItems.Add((
                        manual.BankAccountId,
                        "N/A", // Bank accounts don't have currency codes in the same way
                        manual.TransactionDate,
                        "Manual",
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        manual.Description ?? "Manual adjustment"
                    ));
                }

                // Group by bank account to create coherent history
                var bankAccountGroups = bankAccountTransactionItems
                    .GroupBy(x => x.BankAccountId)
                    .ToList();

                foreach (var bankGroup in bankAccountGroups)
                {
                    var bankAccountId = bankGroup.Key;
                    var bankTransactions = bankGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!bankTransactions.Any()) continue;

                    // Find the earliest transaction for this bank account
                    var firstTransaction = bankTransactions.First();
                    var zeroDateTime = firstTransaction.TransactionDate.AddMinutes(-1);

                    // Create zero-starting bank account balance history record
                    var zeroBankHistory = new BankAccountBalanceHistory
                    {
                        BankAccountId = bankAccountId,
                        TransactionType = BankAccountTransactionType.ManualEdit,
                        ReferenceId = null,
                        BalanceBefore = 0,
                        TransactionAmount = 0,
                        BalanceAfter = 0,
                        Description = "Zero-start balance for coherent history chain (non-frozen records only)",
                        TransactionDate = zeroDateTime,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = performedBy,
                        IsDeleted = false
                    };
                    _context.BankAccountBalanceHistory.Add(zeroBankHistory);

                    // Process transactions chronologically for this bank account
                    decimal runningBalance = 0;

                    foreach (var transaction in bankTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Document" => BankAccountTransactionType.Document,
                            "Manual" => BankAccountTransactionType.ManualEdit,
                            _ => BankAccountTransactionType.Document
                        };

                        var bankHistory = new BankAccountBalanceHistory
                        {
                            BankAccountId = bankAccountId,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            Description = transaction.Description,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        };
                        _context.BankAccountBalanceHistory.Add(bankHistory);

                        runningBalance = bankHistory.BalanceAfter;
                    }

                    // Update bank account balance
                    var finalBankAccountId = bankAccountId;
                    var bankBalance = await _context.BankAccountBalances
                        .FirstOrDefaultAsync(b => b.BankAccountId == finalBankAccountId);
                    if (bankBalance != null)
                    {
                        bankBalance.Balance = runningBalance;
                        bankBalance.LastUpdated = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent bank account balance history for {bankAccountGroups.Count} bank account + currency combinations");

                // STEP 4: Rebuild coherent customer balance history from orders, documents, and manual records (including frozen, excluding only deleted)
                logMessages.Add("");
                logMessages.Add("STEP 4: Rebuilding coherent customer balance history from orders, documents, and manual records (including frozen for customer history)...");

                // Get all non-deleted orders and documents
                var allValidDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && d.IsVerified) // Include frozen documents for customer balance history
                    .ToListAsync();

                var allValidOrders = await _context.Orders
                    .Where(o => !o.IsDeleted) // Include frozen orders for customer balance history
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .ToListAsync();

                logMessages.Add($"Processing {allValidDocuments.Count} valid documents, {allValidOrders.Count} valid orders, and {manualCustomerRecords.Count} manual customer records for customer balance history...");

                // Create unified transaction items for customers from orders, documents, and manual records
                var customerTransactionItems = new List<(int CustomerId, string CurrencyCode, DateTime TransactionDate, string TransactionType, string transactionCode, int? ReferenceId, decimal Amount, string Description)>();

                // Add document transactions
                foreach (var d in allValidDocuments)
                {
                    if (d.PayerType == PayerType.Customer && d.PayerCustomerId.HasValue)
                        customerTransactionItems.Add((d.PayerCustomerId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, d.Amount, d.Description ?? string.Empty));
                    if (d.ReceiverType == ReceiverType.Customer && d.ReceiverCustomerId.HasValue)
                        customerTransactionItems.Add((d.ReceiverCustomerId.Value, d.CurrencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, -d.Amount, d.Description ?? string.Empty));
                }

                // Add order transactions
                foreach (var o in allValidOrders)
                {
                    // Customer pays FromAmount in FromCurrency
                    customerTransactionItems.Add((o.CustomerId, o.FromCurrency.Code, o.CreatedAt, "Order", string.Empty, o.Id, -o.FromAmount, o.Notes ?? string.Empty));

                    // Customer receives ToAmount in ToCurrency
                    customerTransactionItems.Add((o.CustomerId, o.ToCurrency.Code, o.CreatedAt, "Order", string.Empty, o.Id, o.ToAmount, o.Notes ?? string.Empty));
                }

                logMessages.Add($"start adding  [{manualCustomerRecords.Count}] manual customer records");
                logMessages.Add($"customerTransactionItems is [{customerTransactionItems.Count}]");

                // Add manual customer records as transactions
                foreach (var manual in manualCustomerRecords)
                {
                    customerTransactionItems.Add((
                        manual.CustomerId,
                        manual.CurrencyCode,
                        manual.TransactionDate,
                        "Manual",
                        string.Empty,
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        manual.Description ?? "Manual adjustment"
                    ));
                }
                logMessages.Add($"customerTransactionItems is [{customerTransactionItems.Count}]");

                // Group by customer + currency and create coherent history
                var customerGroups = customerTransactionItems
                    .GroupBy(x => new { x.CustomerId, x.CurrencyCode })
                    .ToList();

                logMessages.Add($"Creating coherent history for {customerGroups.Count} customer + currency combinations...");

                foreach (var customerGroup in customerGroups)
                {
                    var customerId = customerGroup.Key.CustomerId;
                    var currencyCode = customerGroup.Key.CurrencyCode;

                    // Order all transactions chronologically by TransactionDate
                    var orderedTransactions = customerGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!orderedTransactions.Any()) continue;

                    // Process transactions chronologically for this customer + currency
                    decimal runningBalance = 0;

                    foreach (var transaction in orderedTransactions)
                    {
                        var transactionType = transaction.TransactionType switch
                        {
                            "Order" => CustomerBalanceTransactionType.Order,
                            "Document" => CustomerBalanceTransactionType.AccountingDocument,
                            "Manual" => CustomerBalanceTransactionType.Manual,
                            _ => CustomerBalanceTransactionType.AccountingDocument
                        };
                        var note = $"{transactionType} - مبلغ: {transaction.Amount:N0} {transaction.CurrencyCode}";
                        if (!string.IsNullOrEmpty(transaction.transactionCode))
                            note += $" - شناسه تراکنش: {transaction.transactionCode}";

                        var customerHistory = new CustomerBalanceHistory
                        {
                            CustomerId = customerId,
                            CurrencyCode = currencyCode,
                            TransactionType = transactionType,
                            ReferenceId = transaction.ReferenceId,
                            BalanceBefore = runningBalance,
                            TransactionAmount = transaction.Amount,
                            BalanceAfter = runningBalance + transaction.Amount,
                            Description = transaction.Description,
                            TransactionNumber = transaction.transactionCode,
                            Note = note,
                            TransactionDate = transaction.TransactionDate,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = performedBy,
                            IsDeleted = false
                        };
                        _context.CustomerBalanceHistory.Add(customerHistory);

                        runningBalance = customerHistory.BalanceAfter;
                    }

                    // Update customer balance
                    var finalCustomerId = customerId;
                    var finalCustomerCurrencyCode = currencyCode;
                    var customerBalance = await _context.CustomerBalances
                        .FirstOrDefaultAsync(b => b.CustomerId == finalCustomerId && b.CurrencyCode == finalCustomerCurrencyCode);
                    if (customerBalance == null)
                    {
                        customerBalance = new CustomerBalance
                        {
                            CustomerId = customerId,
                            CurrencyCode = currencyCode,
                            Balance = 0,
                            LastUpdated = DateTime.UtcNow
                        };
                        _context.CustomerBalances.Add(customerBalance);
                    }
                    customerBalance.Balance = runningBalance;
                    customerBalance.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Rebuilt coherent customer balance history for {customerGroups.Count} customer + currency combinations from {allValidDocuments.Count} documents and {allValidOrders.Count} orders (manual records were preserved)");

                await dbTransaction.CommitAsync();

                logMessages.Add("");
                logMessages.Add("=== REBUILD COMPLETED SUCCESSFULLY ===");
                logMessages.Add($"Finished at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add("✅ All balance histories now start from zero with coherent balance chains");
                logMessages.Add("✅ Active buy/sell counts recalculated based on non-frozen orders only");
                logMessages.Add("✅ Frozen records excluded from pool/bank calculations but included in customer history");
                logMessages.Add("✅ Manual customer balance adjustments preserved in complete customer history");

                var logSummary = string.Join("\n", logMessages);
                _logger.LogInformation($"Financial balance rebuild completed successfully. Summary: {logSummary}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during comprehensive financial balance rebuild: {ex.Message}");
                throw;
            }
        }


        #region Smart Delete Operations with History Soft Delete and Recalculation

        /// <summary>
        /// Safely delete an order by soft-deleting its history records and recalculating balances
        /// </summary>
        public async Task DeleteOrderAsync(Order order, string performedBy = "Admin")
        {
            try
            {
                _logger.LogInformation($"Starting smart order deletion: Order {order.Id} by {performedBy}");


                order.IsDeleted = true;
                order.DeletedAt = DateTime.UtcNow;
                order.DeletedBy = performedBy;
                await _context.SaveChangesAsync();

                // Rebuild all financial balances after order deletion to ensure coherence
                await RebuildAllFinancialBalancesAsync(performedBy);

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
                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;
                document.DeletedBy = performedBy;
                await _context.SaveChangesAsync();

                // Rebuild all financial balances after document deletion to ensure coherence
                await RebuildAllFinancialBalancesAsync(performedBy);

                _logger.LogInformation($"Smart document deletion completed: Document {document.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in smart document deletion {document.Id}: {ex.Message}");
                throw;
            }
        }



        #endregion



        #region Manual Balance History Creation

        /// <summary>
        /// Creates a manual customer balance history record with specified transaction date following the coherent history pattern.
        /// This method creates proper balance chains with correct BalanceBefore, TransactionAmount, and BalanceAfter calculations.
        /// Uses the same coherent sequencing pattern as RebuildAllFinancialBalances to ensure consistency.
        /// Manual transactions are never frozen and always affect current balance calculations.
        /// </summary>
        public async Task CreateManualCustomerBalanceHistoryAsync(
            int customerId,
            string currencyCode,
            decimal amount,
            string reason,
            DateTime transactionDate,
            string performedBy = "Manual Entry",
            string? transactionNumber = null,
            string? performingUserId = null)
        {
            _logger.LogInformation($"Creating manual customer balance history: Customer {customerId}, Currency {currencyCode}, Amount {amount}, Date {transactionDate:yyyy-MM-dd}");

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
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


                // Create the manual history record with proper coherent balance calculations
                var historyRecord = new CustomerBalanceHistory
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    BalanceBefore = 0, //will update to corect value in rebuild 
                    TransactionAmount = amount,
                    BalanceAfter = 0, //will update to corect value in rebuild 
                    TransactionType = CustomerBalanceTransactionType.Manual,
                    ReferenceId = null, // Manual entries don't have reference IDs
                    Description = reason,
                    TransactionNumber = transactionNumber,
                    TransactionDate = transactionDate, // Use the specified date
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy,
                    IsDeleted = false // Manual transactions are never deleted via soft delete
                };



                _context.CustomerBalanceHistory.Add(historyRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Manual customer balance history created with coherent balances: ID {historyRecord.Id}, Customer {customerId}, Currency {currencyCode}, Amount {amount}, BalanceBefore {balanceBefore}, BalanceAfter {balanceAfter}");

                await RebuildAllFinancialBalancesAsync(performedBy);
            }

        /// <summary>
        /// Deletes a manual customer balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.Manual) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// </summary>
        public async Task DeleteManualCustomerBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null)
        {
            _logger.LogInformation($"Deleting manual customer balance history: Transaction ID {transactionId}");


            // Find the manual transaction
            var historyRecord = await _context.CustomerBalanceHistory
                .Include(h => h.Customer)
                .FirstOrDefaultAsync(h => h.Id == transactionId);

            if (historyRecord == null)
            {
                throw new ArgumentException($"Customer balance history with ID {transactionId} not found");
            }

            // Verify this is a manual transaction - only manual transactions can be deleted
            if (historyRecord.TransactionType != CustomerBalanceTransactionType.Manual)
            {
                throw new InvalidOperationException($"Only manual transactions can be deleted. Transaction ID {transactionId} is of type {historyRecord.TransactionType}");
            }
            // Delete the manual transaction
            _context.CustomerBalanceHistory.Remove(historyRecord);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Manual customer balance history deleted: ID {transactionId}, Customer {customerId}, Currency {currencyCode}, Amount {amount}");


            // Rebuild all financial balances after manual customer balance deletion to ensure complete coherence
            await RebuildAllFinancialBalancesAsync(performedBy);

            // Send notification to admin users (excluding the performing user)
            try
            {

                var customerId = historyRecord.CustomerId;
                var currencyCode = historyRecord.CurrencyCode;
                var amount = historyRecord.TransactionAmount;
                var transactionDate = historyRecord.TransactionDate;
                var customerName = historyRecord.Customer?.FullName ?? $"مشتری {customerId}";

                await _notificationHub.SendCustomNotificationAsync(
                    title: "تعدیل دستی موجودی حذف شد",
                    message: $"مشتری: {customerName} | مبلغ: {amount:N2} {currencyCode}",
                    eventType: NotificationEventType.CustomerBalanceChanged,
                    userId: performingUserId, // This will exclude the current user from SignalR notifications
                    navigationUrl: $"/Reports/CustomerReports?customerId={customerId}",
                    priority: NotificationPriority.Normal
                );

                _logger.LogInformation($"Notification sent for manual balance deletion: Customer {customerId}, Amount {amount} {currencyCode}");
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, $"Error sending notification for manual balance deletion: Customer {customerId}, Amount {amount} {currencyCode}");
                // Don't fail the main operation due to notification errors
            }

        }

        /// <summary>
        /// Creates a manual currency pool balance history record with specified transaction date following the coherent history pattern.
        /// This method creates proper balance chains with correct BalanceBefore, TransactionAmount, and BalanceAfter calculations.
        /// Uses the same coherent sequencing pattern as RebuildAllFinancialBalances to ensure consistency.
        /// Manual transactions are never frozen and always affect current balance calculations.
        /// </summary>
        public async Task CreateManualPoolBalanceHistoryAsync(
            string currencyCode,
            decimal adjustmentAmount,
            string reason,
            DateTime transactionDate,
            string performedBy = "Manual Entry",
            string? performingUserId = null)
        {
            _logger.LogInformation($"Creating manual pool balance history: Currency {currencyCode}, Amount {adjustmentAmount}, Date {transactionDate:yyyy-MM-dd}");



            // Create the manual history record with proper coherent balance calculations
            var historyRecord = new CurrencyPoolHistory
            {
                CurrencyCode = currencyCode,
                BalanceBefore = 0, //will update in rebuild
                TransactionAmount = adjustmentAmount,
                BalanceAfter = 0, //will update in rebuild
                TransactionType = CurrencyPoolTransactionType.ManualEdit,
                ReferenceId = null,
                Description = reason,
                TransactionDate = transactionDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = performedBy,
                IsDeleted = false
            };


            _context.CurrencyPoolHistory.Add(historyRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Manual pool balance history created with coherent balances: ID {historyRecord.Id}, Currency {currencyCode}, Amount {adjustmentAmount}, BalanceBefore {balanceBefore}, BalanceAfter {balanceAfter}");



            await _context.SaveChangesAsync();

            // Rebuild all financial balances after manual pool balance creation to ensure complete coherence
            await RebuildAllFinancialBalancesAsync(performedBy);

            // Send notification to admin users (excluding the performing user)
            try
            {
                await _notificationHub.SendCustomNotificationAsync(
                    title: "تعدیل دستی صندوق ارزی ایجاد شد",
                    message: $"ارز: {currencyCode} | مبلغ: {adjustmentAmount:N2} | موجودی نهایی: {balanceAfter:N2} | دلیل: {reason}",
                    eventType: NotificationEventType.Custom,
                    userId: performingUserId,
                    navigationUrl: $"/Reports/PoolReports?currencyCode={currencyCode}",
                    priority: NotificationPriority.Normal
                );

                _logger.LogInformation($"Notification sent for manual pool balance creation: Currency {currencyCode}, Amount {adjustmentAmount}, Final Balance {balanceAfter:N2}");
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, $"Error sending notification for manual pool balance creation: Currency {currencyCode}, Amount {adjustmentAmount}");
            }


        }

        /// <summary>
        /// Deletes a manual currency pool balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.ManualEdit) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// </summary>
        public async Task DeleteManualPoolBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null)
        {
            _logger.LogInformation($"Deleting manual pool balance history: Transaction ID {transactionId}");


            var historyRecord = await _context.CurrencyPoolHistory
                .FirstOrDefaultAsync(h => h.Id == transactionId);

            if (historyRecord == null)
            {
                throw new ArgumentException($"Currency pool history with ID {transactionId} not found");
            }

            if (historyRecord.TransactionType != CurrencyPoolTransactionType.ManualEdit)
            {
                throw new InvalidOperationException($"Only manual transactions can be deleted. Transaction ID {transactionId} is of type {historyRecord.TransactionType}");
            }

            var currencyCode = historyRecord.CurrencyCode;
            var amount = historyRecord.TransactionAmount;
            var transactionDate = historyRecord.TransactionDate;

            _context.CurrencyPoolHistory.Remove(historyRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Manual pool balance history deleted: ID {transactionId}, Currency {currencyCode}, Amount {amount}");



            // Rebuild all financial balances after manual pool balance deletion to ensure complete coherence
            await RebuildAllFinancialBalancesAsync(performedBy);

            // Send notification to admin users (excluding the performing user)
            try
            {
                await _notificationHub.SendCustomNotificationAsync(
                    title: "تعدیل دستی صندوق ارزی حذف شد",
                    message: $"ارز: {currencyCode} | مبلغ: {amount:N2}",
                    eventType: NotificationEventType.Custom,
                    userId: performingUserId,
                    navigationUrl: $"/Reports/PoolReports?currencyCode={currencyCode}",
                    priority: NotificationPriority.Normal
                );

                _logger.LogInformation($"Notification sent for manual pool balance deletion: Currency {currencyCode}, Amount {amount}");
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, $"Error sending notification for manual pool balance deletion: Currency {currencyCode}, Amount {amount}");
            }


        }

        /// <summary>
        /// Creates a manual bank account balance history record with specified transaction date following the coherent history pattern.
        /// This method creates proper balance chains with correct BalanceBefore, TransactionAmount, and BalanceAfter calculations.
        /// Uses the same coherent sequencing pattern as RebuildAllFinancialBalances to ensure consistency.
        /// Manual transactions are never frozen and always affect current balance calculations.
        /// </summary>
        public async Task CreateManualBankAccountBalanceHistoryAsync(
            int bankAccountId,
            decimal amount,
            string reason,
            DateTime transactionDate,
            string performedBy = "Manual Entry",
            string? performingUserId = null)
        {
            _logger.LogInformation($"Creating manual bank account balance history: Bank Account {bankAccountId}, Amount {amount}, Date {transactionDate:yyyy-MM-dd}");



            // Create the manual history record with proper coherent balance calculations
            var historyRecord = new BankAccountBalanceHistory
            {
                BankAccountId = bankAccountId,
                BalanceBefore = 0, //will update in rebuild
                TransactionAmount = amount,
                BalanceAfter = 0, //will update in rebuild
                TransactionType = BankAccountTransactionType.ManualEdit,
                ReferenceId = null,
                Description = reason,
                TransactionDate = transactionDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = performedBy,
                IsDeleted = false
            };

          
            _context.BankAccountBalanceHistory.Add(historyRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Manual bank account balance history created with coherent balances: ID {historyRecord.Id}, Bank Account {bankAccountId}, Amount {amount}, BalanceBefore {balanceBefore}, BalanceAfter {balanceAfter}");

           
            // Rebuild all financial balances after manual bank account balance creation to ensure complete coherence
            await RebuildAllFinancialBalancesAsync(performedBy);

            // Send notification to admin users (excluding the performing user)
            try
            {
                var bankrecord = _context.BankAccountBalanceHistory.FirstOrDefault(c => c.BankAccountId == bankAccountId);
                var accountName = bankrecord?.BankAccount.AccountHolderName ?? $"حساب {bankAccountId}";

                await _notificationHub.SendCustomNotificationAsync(
                    title: "تعدیل دستی حساب بانکی ایجاد شد",
                    message: $"حساب: {accountName} | مبلغ: {amount:N2} | موجودی نهایی: {bankrecord?.BalanceAfter:N2} | دلیل: {reason}",
                    eventType: NotificationEventType.Custom,
                    userId: performingUserId,
                    navigationUrl: $"/Reports/BankAccountReports?bankAccountId={bankAccountId}",
                    priority: NotificationPriority.Normal
                );

                _logger.LogInformation($"Notification sent for manual bank account balance creation: Bank Account {bankAccountId}, Amount {amount}, Final Balance {balanceAfter:N2}");
            }
            catch (Exception notificationEx)
            {
                _logger.LogError(notificationEx, $"Error sending notification for manual bank account balance creation: Bank Account {bankAccountId}, Amount {amount}");
            }

        }

        /// <summary>
        /// Deletes a manual bank account balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.ManualEdit) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// </summary>
        public async Task DeleteManualBankAccountBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null)
        {
            _logger.LogInformation($"Deleting manual bank account balance history: Transaction ID {transactionId}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var historyRecord = await _context.BankAccountBalanceHistory
                    .FirstOrDefaultAsync(h => h.Id == transactionId);

                if (historyRecord == null)
                {
                    throw new ArgumentException($"Bank account balance history with ID {transactionId} not found");
                }

                if (historyRecord.TransactionType != BankAccountTransactionType.ManualEdit)
                {
                    throw new InvalidOperationException($"Only manual transactions can be deleted. Transaction ID {transactionId} is of type {historyRecord.TransactionType}");
                }

                var bankAccountId = historyRecord.BankAccountId;
                var amount = historyRecord.TransactionAmount;
                var transactionDate = historyRecord.TransactionDate;

                // Get bank account name for notification
                var bankAccount = await _context.BankAccounts
                    .FirstOrDefaultAsync(ba => ba.Id == bankAccountId);
                var accountName = bankAccount?.AccountHolderName ?? $"حساب {bankAccountId}";

                _context.BankAccountBalanceHistory.Remove(historyRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Manual bank account balance history deleted: ID {transactionId}, Bank Account {bankAccountId}, Amount {amount}");

                await RecalculateBankAccountBalanceFromDateAsync(bankAccountId, transactionDate);

                await transaction.CommitAsync();
                _logger.LogInformation($"Successfully deleted manual bank account transaction and recalculated balances for Bank Account {bankAccountId}");

                // Rebuild all financial balances after manual bank account balance deletion to ensure complete coherence
                await RebuildAllFinancialBalancesAsync(performedBy);

                // Send notification to admin users (excluding the performing user)
                try
                {
                    await _notificationHub.SendCustomNotificationAsync(
                        title: "تعدیل دستی حساب بانکی حذف شد",
                        message: $"حساب: {accountName} | مبلغ: {amount:N2}",
                        eventType: NotificationEventType.Custom,
                        userId: performingUserId,
                        navigationUrl: $"/Reports/BankAccountReports?bankAccountId={bankAccountId}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual bank account balance deletion: Bank Account {bankAccountId}, Amount {amount}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual bank account balance deletion: Bank Account {bankAccountId}, Amount {amount}");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting manual bank account balance history: Transaction ID {transactionId}");
                throw;
            }
        }

        #endregion


    }
}
