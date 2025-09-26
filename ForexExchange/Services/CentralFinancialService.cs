
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

        #region Customer Balance Operations

        /// <summary>
        /// **BALANCE RETRIEVAL** - Gets current balance for a specific customer and currency.
        /// 
        /// Retrieves balance from the current balance table for optimal performance.
        /// Returns zero if no balance record exists for the customer-currency combination.
        /// </summary>
        /// <param name="customerId">Unique identifier of the customer</param>
        /// <param name="currencyCode">Currency code (e.g., "USD", "EUR", "IRR")</param>
        /// <returns>Current balance amount, or 0 if no balance record exists</returns>
        public async Task<decimal> GetCustomerBalanceAsync(int customerId, string currencyCode)
        {
            // Get from current balance table for performance
            var balance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

            return balance?.Balance ?? 0;
        }

        /// <summary>
        /// **MULTI-CURRENCY BALANCE RETRIEVAL** - Gets all currency balances for a specific customer.
        /// 
        /// Returns all active currency balances associated with the customer, enabling
        /// comprehensive balance display and financial analysis.
        /// </summary>
        /// <param name="customerId">Unique identifier of the customer</param>
        /// <returns>List of all currency balances for the customer</returns>
        public async Task<List<CustomerBalance>> GetCustomerBalancesAsync(int customerId)
        {
            return await _context.CustomerBalances
                .Where(cb => cb.CustomerId == customerId)
                .ToListAsync();
        }

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

            // Get current balances to validate calculations match preview
            var customerBalanceFrom = await _context.CustomerBalances.FirstOrDefaultAsync(cb => cb.CustomerId == order.CustomerId && cb.CurrencyCode == order.FromCurrency.Code);
            var customerBalanceTo = await _context.CustomerBalances.FirstOrDefaultAsync(cb => cb.CustomerId == order.CustomerId && cb.CurrencyCode == order.ToCurrency.Code);
            var poolBalanceFrom = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.FromCurrency.Id);
            var poolBalanceTo = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.ToCurrency.Id);

            // Use SRP calculation methods to validate expected effects (same as preview)
            var (expectedNewCustomerFrom, expectedNewCustomerTo) = CalculateCustomerBalanceEffects(
                currentFromBalance: customerBalanceFrom?.Balance ?? 0,
                currentToBalance: customerBalanceTo?.Balance ?? 0,
                fromAmount: order.FromAmount,
                toAmount: order.ToAmount
            );

            var (expectedNewPoolFrom, expectedNewPoolTo) = CalculateCurrencyPoolEffects(
                currentFromPool: poolBalanceFrom?.Balance ?? 0,
                currentToPool: poolBalanceTo?.Balance ?? 0,
                fromAmount: order.FromAmount,
                toAmount: order.ToAmount
            );

            _logger.LogInformation($"[ProcessOrderCreationAsync] Expected customer effects - From: {expectedNewCustomerFrom}, To: {expectedNewCustomerTo}");
            _logger.LogInformation($"[ProcessOrderCreationAsync] Expected pool effects - From: {expectedNewPoolFrom}, To: {expectedNewPoolTo}");

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

            // NEW: Check if document is frozen - frozen documents don't affect current balances or bank account balances
            if (document.IsFrozen)
            {
                _logger.LogInformation($"Document {document.Id} is frozen - skipping all balance updates (customers and bank accounts)");
                return;
            }

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
                    transactionDate: document.DocumentDate, // Use document date
                    transactionNumber: document.ReferenceNumber
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
                    transactionDate: document.DocumentDate, // Use document date
                    transactionNumber: document.ReferenceNumber
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
                    transactionDate: document.DocumentDate, // Use document date
                    transactionNumber: document.ReferenceNumber
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
                    transactionDate: document.DocumentDate, // Use document date
                    transactionNumber: document.ReferenceNumber
                );
            }

            _logger.LogInformation($"Document {document.Id} processing completed");
        }

        /// <summary>
        /// **MANUAL BALANCE ADJUSTMENT** - Allows manual correction of customer balances with full audit trail.
        /// 
        /// Used for administrative corrections, error fixes, or special adjustments that require
        /// manual intervention. All adjustments are logged with detailed reasoning for audit purposes.
        /// 
        /// **Use Cases**:
        /// - Correcting data entry errors
        /// - Compensating customers for system issues
        /// - Administrative adjustments per management decisions
        /// - Balance corrections after system maintenance
        /// 
        /// **Audit Requirement**: Must provide clear reason for adjustment for regulatory compliance.
        /// </summary>
        /// <param name="customerId">Customer whose balance is being adjusted</param>
        /// <param name="currencyCode">Currency being adjusted</param>
        /// <param name="adjustmentAmount">Amount to adjust (positive for increase, negative for decrease)</param>
        /// <param name="reason">Detailed reason for the adjustment (required for audit trail)</param>
        /// <param name="performedBy">Administrator or system performing the adjustment</param>
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
            int? relatedDocumentId, string reason, string performedBy = "System", DateTime? transactionDate = null, string? transactionNumber = null)
        {
            await UpdateBankAccountBalanceAsync(
                bankAccountId: bankAccountId,
                amount: amount,
                transactionType: transactionType,
                relatedDocumentId: relatedDocumentId,
                reason: reason,
                performedBy: performedBy,
                transactionDate: transactionDate,
                transactionNumber: transactionNumber
            );
        }

      

        #endregion

        #region SRP Calculation Methods - Single Responsibility for Order Effects

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

        #endregion

        #region Core Update Methods - PRESERVE EXACT CALCULATION LOGIC

        private async Task UpdateCustomerBalanceAsync(int customerId, string currencyCode, decimal amount,
            CustomerBalanceTransactionType transactionType, int? relatedOrderId = null, int? relatedDocumentId = null,
            string? reason = null, string performedBy = "System", DateTime? transactionDate = null, string? transactionNumber = null)
        {
            await UpdateCustomerBalanceInternalAsync(customerId, currencyCode, amount, transactionType,
                relatedOrderId, relatedDocumentId, reason, performedBy, useTransaction: true, transactionDate, transactionNumber);
        }

        private async Task UpdateCustomerBalanceInternalAsync(int customerId, string currencyCode, decimal amount,
            CustomerBalanceTransactionType transactionType, int? relatedOrderId = null, int? relatedDocumentId = null,
            string? reason = null, string performedBy = "System", bool useTransaction = true, DateTime? transactionDate = null, string? transactionNumber = null)
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
                    TransactionNumber = transactionNumber,
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
            int? relatedDocumentId, string reason, string performedBy = "System", DateTime? transactionDate = null, string? transactionNumber = null)
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
                    TransactionNumber = transactionNumber,
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
                    Description = $"معامله {order.Id}: پرداخت {order.FromAmount:N0} {order.FromCurrency.PersianName}",
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
                    Description = $"معامله {order.Id}: دریافت {order.ToAmount:N0} {order.ToCurrency.PersianName}",
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
                           !h.IsDeleted); // EXCLUDE ONLY DELETED RECORDS FOR REPORTING

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
                .Where(h => h.CurrencyCode == currencyCode && !h.IsDeleted); // EXCLUDE ONLY DELETED RECORDS FOR REPORTING

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
                .Where(h => h.BankAccountId == bankAccountId && !h.IsDeleted); // EXCLUDE ONLY DELETED RECORDS FOR REPORTING

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
                    .FirstOrDefaultAsync(cp => cp.CurrencyId == IRRCurrency.Id);


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

                // Get active orders involving this currency
                var activeOrders = await _context.Orders
                    .Where(o => !o.IsDeleted && !o.IsFrozen)
                    .Where(o => o.FromCurrency.Code == currencyCode || o.ToCurrency.Code == currencyCode)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .ToListAsync();

                decimal totalBought = activeOrders.Where(o => o.FromCurrency.Code == currencyCode).Sum(o => o.FromAmount); // Amount we bought from customers (pool increases)
                decimal totalSold = activeOrders.Where(o => o.ToCurrency.Code == currencyCode).Sum(o => o.ToAmount);   // Amount we sold to customers (pool decreases)
                int activeBuyOrderCount = activeOrders.Count(o => o.ToCurrency.Code == currencyCode);  // Orders where customers buy from us
                int activeSellOrderCount = activeOrders.Count(o => o.FromCurrency.Code == currencyCode); // Orders where customers sell to us

                currentPool.TotalBought = totalBought;
                currentPool.TotalSold = totalSold;
                currentPool.ActiveBuyOrderCount = activeBuyOrderCount;
                currentPool.ActiveSellOrderCount = activeSellOrderCount;
                currentPool.LastUpdated = DateTime.UtcNow;
                currentPool.Notes = "Recalculated after deletion";

                _logger.LogInformation($"Updated currency pool {currencyCode} balance from {oldBalance} to {currentPool.Balance}, Bought: {totalBought}, Sold: {totalSold}, Buy Orders: {activeBuyOrderCount}, Sell Orders: {activeSellOrderCount}");
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

                // COHERENT HISTORY PATTERN: Find proper BalanceBefore by looking at chronologically prior transactions
                var priorTransactions = await _context.CustomerBalanceHistory
                    .Where(h => h.CustomerId == customerId &&
                               h.CurrencyCode == currencyCode &&
                               h.TransactionDate <= transactionDate &&
                               !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                // Calculate the proper BalanceBefore for this manual transaction
                decimal balanceBefore = 0m;
                if (priorTransactions.Any())
                {
                    // If inserting between existing transactions, we need to recalculate the chain
                    var transactionsBeforeThisDate = priorTransactions
                        .Where(h => h.TransactionDate < transactionDate || 
                               (h.TransactionDate == transactionDate && h.Id < long.MaxValue)) // Handle same-date transactions
                        .ToList();

                    if (transactionsBeforeThisDate.Any())
                    {
                        // Calculate balance up to the insertion point
                        decimal runningBalance = 0m;
                        foreach (var priorTransaction in transactionsBeforeThisDate)
                        {
                            runningBalance += priorTransaction.TransactionAmount;
                        }
                        balanceBefore = runningBalance;
                    }
                }

                var balanceAfter = balanceBefore + amount;

                // Create the manual history record with proper coherent balance calculations
                var historyRecord = new CustomerBalanceHistory
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    BalanceBefore = balanceBefore, // Calculated from chronological prior transactions
                    TransactionAmount = amount,
                    BalanceAfter = balanceAfter, // Coherent calculation: BalanceBefore + TransactionAmount
                    TransactionType = CustomerBalanceTransactionType.Manual,
                    ReferenceId = null, // Manual entries don't have reference IDs
                    Description = reason,
                    TransactionNumber = transactionNumber,
                    TransactionDate = transactionDate, // Use the specified date
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy,
                    IsDeleted = false // Manual transactions are never deleted via soft delete
                };

                // Validate the balance calculation
                if (!historyRecord.IsCalculationValid())
                {
                    throw new InvalidOperationException($"Balance calculation validation failed: BalanceBefore({balanceBefore}) + TransactionAmount({amount}) != BalanceAfter({balanceAfter})");
                }

                _context.CustomerBalanceHistory.Add(historyRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Manual customer balance history created with coherent balances: ID {historyRecord.Id}, Customer {customerId}, Currency {currencyCode}, Amount {amount}, BalanceBefore {balanceBefore}, BalanceAfter {balanceAfter}");

                // COHERENT HISTORY PATTERN: Recalculate all subsequent transactions to maintain coherence
                // Find all transactions after this insertion point that need recalculation
                var subsequentTransactions = await _context.CustomerBalanceHistory
                    .Where(h => h.CustomerId == customerId &&
                               h.CurrencyCode == currencyCode &&
                               (h.TransactionDate > transactionDate || 
                                (h.TransactionDate == transactionDate && h.Id > historyRecord.Id)) &&
                               !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                if (subsequentTransactions.Any())
                {
                    _logger.LogInformation($"Recalculating {subsequentTransactions.Count} subsequent transactions to maintain balance coherence");
                    
                    decimal runningBalance = balanceAfter; // Start from our new transaction's balance
                    
                    foreach (var transaction in subsequentTransactions)
                    {
                        var oldBalanceBefore = transaction.BalanceBefore;
                        var oldBalanceAfter = transaction.BalanceAfter;
                        
                        transaction.BalanceBefore = runningBalance;
                        transaction.BalanceAfter = runningBalance + transaction.TransactionAmount;
                        runningBalance = transaction.BalanceAfter;
                        
                        _logger.LogDebug($"Recalculated Transaction ID {transaction.Id}: BalanceBefore {oldBalanceBefore} → {transaction.BalanceBefore}, BalanceAfter {oldBalanceAfter} → {transaction.BalanceAfter}");
                        
                        // Validate each recalculation
                        if (!transaction.IsCalculationValid())
                        {
                            throw new InvalidOperationException($"Recalculation validation failed for Transaction ID {transaction.Id}");
                        }
                    }
                    
                    balanceAfter = runningBalance; // Final balance after all recalculations
                }

                // Update the current customer balance to reflect the final coherent balance
                var currentBalance = await _context.CustomerBalances
                    .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

                if (currentBalance == null)
                {
                    // Create new balance record if none exists
                    currentBalance = new CustomerBalance
                    {
                        CustomerId = customerId,
                        CurrencyCode = currencyCode,
                        Balance = balanceAfter,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.CustomerBalances.Add(currentBalance);
                }
                else
                {
                    // Update existing balance with final coherent amount
                    currentBalance.Balance = balanceAfter;
                    currentBalance.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation($"Successfully created manual transaction with coherent balance chain - Customer {customerId}, Currency {currencyCode}, Final Balance: {balanceAfter:N2}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    var customerName = customer.FullName ?? $"مشتری {customerId}";

                    await _notificationHub.SendCustomNotificationAsync(
                        title: "تعدیل دستی موجودی ایجاد شد",
                        message: $"مشتری: {customerName} | مبلغ: {amount:N2} {currencyCode} | موجودی نهایی: {balanceAfter:N2} | دلیل: {reason}",
                        eventType: NotificationEventType.CustomerBalanceChanged,
                        userId: performingUserId, // This will exclude the current user from SignalR notifications
                        navigationUrl: $"/Reports/CustomerReports?customerId={customerId}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual balance creation: Customer {customerId}, Amount {amount} {currencyCode}, Final Balance {balanceAfter:N2}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual balance creation: Customer {customerId}, Amount {amount} {currencyCode}");
                    // Don't fail the main operation due to notification errors
                }
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating manual customer balance history: Customer {customerId}, Currency {currencyCode}, Amount {amount}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a manual customer balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.Manual) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// </summary>
        public async Task DeleteManualCustomerBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null)
        {
            _logger.LogInformation($"Deleting manual customer balance history: Transaction ID {transactionId}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
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

                var customerId = historyRecord.CustomerId;
                var currencyCode = historyRecord.CurrencyCode;
                var amount = historyRecord.TransactionAmount;
                var transactionDate = historyRecord.TransactionDate;
                var customerName = historyRecord.Customer?.FullName ?? $"مشتری {customerId}";

                // Delete the manual transaction
                _context.CustomerBalanceHistory.Remove(historyRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Manual customer balance history deleted: ID {transactionId}, Customer {customerId}, Currency {currencyCode}, Amount {amount}");

                // Recalculate balances for this customer/currency from the transaction date onwards
                await RecalculateCustomerCurrencyBalanceFromDateAsync(customerId, currencyCode, transactionDate);

                await transaction.CommitAsync();
                _logger.LogInformation($"Successfully deleted manual transaction and recalculated balances for Customer {customerId}, Currency {currencyCode}");

                // Send notification to admin users (excluding the performing user)
                try
                {
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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting manual customer balance history: Transaction ID {transactionId}");
                throw;
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

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate currency exists
                var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == currencyCode);
                if (currency == null)
                {
                    throw new ArgumentException($"Currency with code {currencyCode} not found");
                }

                // COHERENT HISTORY PATTERN: Find proper BalanceBefore by looking at chronologically prior transactions
                var priorTransactions = await _context.CurrencyPoolHistory
                    .Where(h => h.CurrencyCode == currencyCode &&
                               h.TransactionDate <= transactionDate &&
                               !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                // Calculate the proper BalanceBefore for this manual transaction
                decimal balanceBefore = 0m;
                if (priorTransactions.Any())
                {
                    var transactionsBeforeThisDate = priorTransactions
                        .Where(h => h.TransactionDate < transactionDate || 
                               (h.TransactionDate == transactionDate && h.Id < long.MaxValue))
                        .ToList();

                    if (transactionsBeforeThisDate.Any())
                    {
                        decimal runningBalance = 0m;
                        foreach (var priorTransaction in transactionsBeforeThisDate)
                        {
                            runningBalance += priorTransaction.TransactionAmount;
                        }
                        balanceBefore = runningBalance;
                    }
                }

                var balanceAfter = balanceBefore + adjustmentAmount;

                // Create the manual history record with proper coherent balance calculations
                var historyRecord = new CurrencyPoolHistory
                {
                    CurrencyCode = currencyCode,
                    BalanceBefore = balanceBefore,
                    TransactionAmount = adjustmentAmount,
                    BalanceAfter = balanceAfter,
                    TransactionType = CurrencyPoolTransactionType.ManualEdit,
                    ReferenceId = null,
                    Description = reason,
                    TransactionDate = transactionDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy,
                    IsDeleted = false
                };

                // Validate the balance calculation
                if (!historyRecord.IsCalculationValid())
                {
                    throw new InvalidOperationException($"Balance calculation validation failed: BalanceBefore({balanceBefore}) + TransactionAmount({adjustmentAmount}) != BalanceAfter({balanceAfter})");
                }

                _context.CurrencyPoolHistory.Add(historyRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Manual pool balance history created with coherent balances: ID {historyRecord.Id}, Currency {currencyCode}, Amount {adjustmentAmount}, BalanceBefore {balanceBefore}, BalanceAfter {balanceAfter}");

                // COHERENT HISTORY PATTERN: Recalculate all subsequent transactions
                var subsequentTransactions = await _context.CurrencyPoolHistory
                    .Where(h => h.CurrencyCode == currencyCode &&
                               (h.TransactionDate > transactionDate || 
                                (h.TransactionDate == transactionDate && h.Id > historyRecord.Id)) &&
                               !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                if (subsequentTransactions.Any())
                {
                    _logger.LogInformation($"Recalculating {subsequentTransactions.Count} subsequent pool transactions to maintain balance coherence");
                    
                    decimal runningBalance = balanceAfter;
                    
                    foreach (var transaction in subsequentTransactions)
                    {
                        var oldBalanceBefore = transaction.BalanceBefore;
                        var oldBalanceAfter = transaction.BalanceAfter;
                        
                        transaction.BalanceBefore = runningBalance;
                        transaction.BalanceAfter = runningBalance + transaction.TransactionAmount;
                        runningBalance = transaction.BalanceAfter;
                        
                        _logger.LogDebug($"Recalculated Pool Transaction ID {transaction.Id}: BalanceBefore {oldBalanceBefore} → {transaction.BalanceBefore}, BalanceAfter {oldBalanceAfter} → {transaction.BalanceAfter}");
                        
                        if (!transaction.IsCalculationValid())
                        {
                            throw new InvalidOperationException($"Recalculation validation failed for Pool Transaction ID {transaction.Id}");
                        }
                    }
                    
                    balanceAfter = runningBalance;
                }

                // Update the current currency pool balance
                var currentPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(cp => cp.CurrencyCode == currencyCode);

                if (currentPool == null)
                {
                    currentPool = new CurrencyPool
                    {
                        CurrencyCode = currencyCode,
                        Balance = balanceAfter,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.CurrencyPools.Add(currentPool);
                }
                else
                {
                    currentPool.Balance = balanceAfter;
                    currentPool.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation($"Successfully created manual pool transaction with coherent balance chain - Currency {currencyCode}, Final Balance: {balanceAfter:N2}");

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
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating manual pool balance history: Currency {currencyCode}, Amount {adjustmentAmount}");
                throw;
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
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

                await RecalculateCurrencyPoolBalanceFromDateAsync(currencyCode, transactionDate);

                await transaction.CommitAsync();
                _logger.LogInformation($"Successfully deleted manual pool transaction and recalculated balances for Currency {currencyCode}");

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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error deleting manual pool balance history: Transaction ID {transactionId}");
                throw;
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

            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate bank account exists
                var bankAccount = await _context.BankAccounts.FirstOrDefaultAsync(ba => ba.Id == bankAccountId);
                if (bankAccount == null)
                {
                    throw new ArgumentException($"Bank account with ID {bankAccountId} not found");
                }

                // COHERENT HISTORY PATTERN: Find proper BalanceBefore by looking at chronologically prior transactions
                var priorTransactions = await _context.BankAccountBalanceHistory
                    .Where(h => h.BankAccountId == bankAccountId &&
                               h.TransactionDate <= transactionDate &&
                               !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                // Calculate the proper BalanceBefore for this manual transaction
                decimal balanceBefore = 0m;
                if (priorTransactions.Any())
                {
                    var transactionsBeforeThisDate = priorTransactions
                        .Where(h => h.TransactionDate < transactionDate || 
                               (h.TransactionDate == transactionDate && h.Id < long.MaxValue))
                        .ToList();

                    if (transactionsBeforeThisDate.Any())
                    {
                        decimal runningBalance = 0m;
                        foreach (var priorTransaction in transactionsBeforeThisDate)
                        {
                            runningBalance += priorTransaction.TransactionAmount;
                        }
                        balanceBefore = runningBalance;
                    }
                }

                var balanceAfter = balanceBefore + amount;

                // Create the manual history record with proper coherent balance calculations
                var historyRecord = new BankAccountBalanceHistory
                {
                    BankAccountId = bankAccountId,
                    BalanceBefore = balanceBefore,
                    TransactionAmount = amount,
                    BalanceAfter = balanceAfter,
                    TransactionType = BankAccountTransactionType.ManualEdit,
                    ReferenceId = null,
                    Description = reason,
                    TransactionDate = transactionDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy,
                    IsDeleted = false
                };

                // Validate the balance calculation
                if (!historyRecord.IsCalculationValid())
                {
                    throw new InvalidOperationException($"Balance calculation validation failed: BalanceBefore({balanceBefore}) + TransactionAmount({amount}) != BalanceAfter({balanceAfter})");
                }

                _context.BankAccountBalanceHistory.Add(historyRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Manual bank account balance history created with coherent balances: ID {historyRecord.Id}, Bank Account {bankAccountId}, Amount {amount}, BalanceBefore {balanceBefore}, BalanceAfter {balanceAfter}");

                // COHERENT HISTORY PATTERN: Recalculate all subsequent transactions
                var subsequentTransactions = await _context.BankAccountBalanceHistory
                    .Where(h => h.BankAccountId == bankAccountId &&
                               (h.TransactionDate > transactionDate || 
                                (h.TransactionDate == transactionDate && h.Id > historyRecord.Id)) &&
                               !h.IsDeleted)
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id)
                    .ToListAsync();

                if (subsequentTransactions.Any())
                {
                    _logger.LogInformation($"Recalculating {subsequentTransactions.Count} subsequent bank account transactions to maintain balance coherence");
                    
                    decimal runningBalance = balanceAfter;
                    
                    foreach (var transaction in subsequentTransactions)
                    {
                        var oldBalanceBefore = transaction.BalanceBefore;
                        var oldBalanceAfter = transaction.BalanceAfter;
                        
                        transaction.BalanceBefore = runningBalance;
                        transaction.BalanceAfter = runningBalance + transaction.TransactionAmount;
                        runningBalance = transaction.BalanceAfter;
                        
                        _logger.LogDebug($"Recalculated Bank Account Transaction ID {transaction.Id}: BalanceBefore {oldBalanceBefore} → {transaction.BalanceBefore}, BalanceAfter {oldBalanceAfter} → {transaction.BalanceAfter}");
                        
                        if (!transaction.IsCalculationValid())
                        {
                            throw new InvalidOperationException($"Recalculation validation failed for Bank Account Transaction ID {transaction.Id}");
                        }
                    }
                    
                    balanceAfter = runningBalance;
                }

                // Update the current bank account balance
                var currentBalance = await _context.BankAccountBalances
                    .FirstOrDefaultAsync(bab => bab.BankAccountId == bankAccountId);

                if (currentBalance == null)
                {
                    currentBalance = new BankAccountBalance
                    {
                        BankAccountId = bankAccountId,
                        Balance = balanceAfter,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.BankAccountBalances.Add(currentBalance);
                }
                else
                {
                    currentBalance.Balance = balanceAfter;
                    currentBalance.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation($"Successfully created manual bank account transaction with coherent balance chain - Bank Account {bankAccountId}, Final Balance: {balanceAfter:N2}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    var accountName = bankAccount.AccountHolderName ?? $"حساب {bankAccountId}";

                    await _notificationHub.SendCustomNotificationAsync(
                        title: "تعدیل دستی حساب بانکی ایجاد شد",
                        message: $"حساب: {accountName} | مبلغ: {amount:N2} | موجودی نهایی: {balanceAfter:N2} | دلیل: {reason}",
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
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating manual bank account balance history: Bank Account {bankAccountId}, Amount {amount}");
                throw;
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

        #region Helper Methods for Targeted Balance Recalculation

        /// <summary>
        /// Recalculates balance for a specific customer and currency from a given date onwards.
        /// This is more efficient than recalculating all balances when only one customer/currency is affected.
        /// </summary>
        private async Task RecalculateCustomerCurrencyBalanceFromDateAsync(int customerId, string currencyCode, DateTime fromDate)
        {
            _logger.LogInformation($"Recalculating balance for Customer {customerId}, Currency {currencyCode} from date {fromDate:yyyy-MM-dd}");

            // Get all transactions for this customer/currency from the specified date onwards, ordered by date
            var transactions = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId &&
                           h.CurrencyCode == currencyCode &&
                           h.TransactionDate >= fromDate)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id) // Secondary sort by ID for same-date transactions
                .ToListAsync();

            if (!transactions.Any())
            {
                _logger.LogInformation($"No transactions found for Customer {customerId}, Currency {currencyCode} from {fromDate:yyyy-MM-dd}");
                return;
            }

            // Get the balance before the first transaction in our date range
            var firstTransaction = transactions.First();
            var balanceBeforeFirstTransaction = 0m;

            // Find the last transaction before our date range to get the starting balance
            var lastTransactionBeforeDate = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId &&
                           h.CurrencyCode == currencyCode &&
                           h.TransactionDate < fromDate)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            if (lastTransactionBeforeDate != null)
            {
                balanceBeforeFirstTransaction = lastTransactionBeforeDate.BalanceAfter;
            }

            // Recalculate balances for all transactions from the date onwards
            var runningBalance = balanceBeforeFirstTransaction;
            foreach (var transaction in transactions)
            {
                transaction.BalanceBefore = runningBalance;
                runningBalance += transaction.TransactionAmount;
                transaction.BalanceAfter = runningBalance;
            }

            // Update the current balance record
            var currentBalance = await _context.CustomerBalances
                .FirstOrDefaultAsync(cb => cb.CustomerId == customerId && cb.CurrencyCode == currencyCode);

            if (currentBalance != null)
            {
                currentBalance.Balance = runningBalance;
                currentBalance.LastUpdated = DateTime.UtcNow;
            }
            else if (runningBalance != 0) // Only create balance record if there's a non-zero balance
            {
                currentBalance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyCode = currencyCode,
                    Balance = runningBalance,
                    LastUpdated = DateTime.UtcNow
                };
                _context.CustomerBalances.Add(currentBalance);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Recalculated {transactions.Count} transactions for Customer {customerId}, Currency {currencyCode}. Final balance: {runningBalance:N2}");
        }

        /// <summary>
        /// Recalculates balance for a specific currency pool from a given date onwards.
        /// This is more efficient than recalculating all balances when only one currency pool is affected.
        /// </summary>
        private async Task RecalculateCurrencyPoolBalanceFromDateAsync(string currencyCode, DateTime fromDate)
        {
            _logger.LogInformation($"Recalculating balance for Currency Pool {currencyCode} from date {fromDate:yyyy-MM-dd}");

            var transactions = await _context.CurrencyPoolHistory
                .Where(h => h.CurrencyCode == currencyCode &&
                           h.TransactionDate >= fromDate)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            if (!transactions.Any())
            {
                _logger.LogInformation($"No transactions found for Currency Pool {currencyCode} from {fromDate:yyyy-MM-dd}");
                return;
            }

            var firstTransaction = transactions.First();
            var balanceBeforeFirstTransaction = 0m;

            var lastTransactionBeforeDate = await _context.CurrencyPoolHistory
                .Where(h => h.CurrencyCode == currencyCode &&
                           h.TransactionDate < fromDate)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            if (lastTransactionBeforeDate != null)
            {
                balanceBeforeFirstTransaction = lastTransactionBeforeDate.BalanceAfter;
            }

            var runningBalance = balanceBeforeFirstTransaction;
            foreach (var transaction in transactions)
            {
                transaction.BalanceBefore = runningBalance;
                runningBalance += transaction.TransactionAmount;
                transaction.BalanceAfter = runningBalance;
            }

            var currentPool = await _context.CurrencyPools
                .FirstOrDefaultAsync(cp => cp.CurrencyCode == currencyCode);

            if (currentPool != null)
            {
                currentPool.Balance = runningBalance;
                currentPool.LastUpdated = DateTime.UtcNow;
            }
            else if (runningBalance != 0)
            {
                currentPool = new CurrencyPool
                {
                    CurrencyCode = currencyCode,
                    Balance = runningBalance,
                    LastUpdated = DateTime.UtcNow
                };
                _context.CurrencyPools.Add(currentPool);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Recalculated {transactions.Count} transactions for Currency Pool {currencyCode}. Final balance: {runningBalance:N2}");
        }

        /// <summary>
        /// Recalculates balance for a specific bank account from a given date onwards.
        /// This is more efficient than recalculating all balances when only one bank account is affected.
        /// </summary>
        private async Task RecalculateBankAccountBalanceFromDateAsync(int bankAccountId, DateTime fromDate)
        {
            _logger.LogInformation($"Recalculating balance for Bank Account {bankAccountId} from date {fromDate:yyyy-MM-dd}");

            var transactions = await _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId &&
                           h.TransactionDate >= fromDate)
                .OrderBy(h => h.TransactionDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            if (!transactions.Any())
            {
                _logger.LogInformation($"No transactions found for Bank Account {bankAccountId} from {fromDate:yyyy-MM-dd}");
                return;
            }

            var firstTransaction = transactions.First();
            var balanceBeforeFirstTransaction = 0m;

            var lastTransactionBeforeDate = await _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId &&
                           h.TransactionDate < fromDate)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            if (lastTransactionBeforeDate != null)
            {
                balanceBeforeFirstTransaction = lastTransactionBeforeDate.BalanceAfter;
            }

            var runningBalance = balanceBeforeFirstTransaction;
            foreach (var transaction in transactions)
            {
                transaction.BalanceBefore = runningBalance;
                runningBalance += transaction.TransactionAmount;
                transaction.BalanceAfter = runningBalance;
            }

            var currentBalance = await _context.BankAccountBalances
                .FirstOrDefaultAsync(bab => bab.BankAccountId == bankAccountId);

            if (currentBalance != null)
            {
                currentBalance.Balance = runningBalance;
                currentBalance.LastUpdated = DateTime.UtcNow;
            }
            else if (runningBalance != 0)
            {
                currentBalance = new BankAccountBalance
                {
                    BankAccountId = bankAccountId,
                    Balance = runningBalance,
                    LastUpdated = DateTime.UtcNow
                };
                _context.BankAccountBalances.Add(currentBalance);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Recalculated {transactions.Count} transactions for Bank Account {bankAccountId}. Final balance: {runningBalance:N2}");
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

            // Get all unique customer+currency combinations (excluding deleted and frozen records)
            var customerCurrencyCombinations = await _context.CustomerBalanceHistory
                .Where(h => !h.IsDeleted ) // Only consider non-deleted 
                .Select(h => new { h.CustomerId, h.CurrencyCode })
                .Distinct()
                .ToListAsync();

            _logger.LogInformation($"Processing {customerCurrencyCombinations.Count} customer+currency combinations");

            // Pre-load all existing customer balances into a dictionary for efficient lookup
            var existingBalances = await _context.CustomerBalances.ToListAsync();
            var balanceLookup = existingBalances.ToDictionary(
                cb => $"{cb.CustomerId}_{cb.CurrencyCode}",
                cb => cb
            );

            // Process each customer+currency combination separately
            foreach (var combination in customerCurrencyCombinations)
            {
                _logger.LogInformation($"Processing Customer {combination.CustomerId} - Currency {combination.CurrencyCode}");

                // Get all history records for this specific customer+currency, ordered by transaction date
                // IMPORTANT: Exclude deleted and frozen records from the sequence
                var historyRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.CustomerId == combination.CustomerId &&
                               h.CurrencyCode == combination.CurrencyCode &&
                               !h.IsDeleted) // EXCLUDE DELETED 
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id) // Secondary sort for same transaction dates
                    .ToListAsync();

                // Special logging for customer+currency that contains ReferenceId = 114
                bool hasReferenceId114 = historyRecords.Any(h => h.ReferenceId == 114);
                if (hasReferenceId114)
                {
                    _logger.LogWarning($"[REF-114-SEQUENCE] Found ReferenceId 114 in Customer {combination.CustomerId} - Currency {combination.CurrencyCode}");
                    _logger.LogWarning($"[REF-114-SEQUENCE] Total transactions in this sequence: {historyRecords.Count}");
                    _logger.LogWarning($"[REF-114-SEQUENCE] FULL SEQUENCE BEFORE PROCESSING:");

                    for (int i = 0; i < historyRecords.Count; i++)
                    {
                        var h = historyRecords[i];
                        _logger.LogWarning($"[REF-114-SEQUENCE] #{i + 1}: ID={h.Id}, RefId={h.ReferenceId}, Date={h.TransactionDate:yyyy-MM-dd HH:mm:ss}, Type={h.TransactionType}, Amount={h.TransactionAmount}, OLD_Before={h.BalanceBefore}, OLD_After={h.BalanceAfter}");
                    }
                }

                // Get or create current balance record for this customer+currency
                var balanceKey = $"{combination.CustomerId}_{combination.CurrencyCode}";
                if (!balanceLookup.TryGetValue(balanceKey, out var currentBalance))
                {
                    currentBalance = new CustomerBalance
                    {
                        CustomerId = combination.CustomerId,
                        CurrencyCode = combination.CurrencyCode,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.CustomerBalances.Add(currentBalance);
                    balanceLookup[balanceKey] = currentBalance;
                }

                // Reset balance to zero for this customer+currency before recalculation
                currentBalance.Balance = 0;

                // Process each transaction chronologically for this customer+currency
                for (int i = 0; i < historyRecords.Count; i++)
                {
                    var history = historyRecords[i];

                    // Special detailed logging for ReferenceId = 114
                    bool isReferenceId114 = history.ReferenceId == 114;

                    if (isReferenceId114)
                    {
                        _logger.LogWarning($"[REF-114] ==================== PROCESSING REF-114 ====================");
                        _logger.LogWarning($"[REF-114] Customer: {history.CustomerId}, Currency: {history.CurrencyCode}");
                        _logger.LogWarning($"[REF-114] Transaction ID: {history.Id}, Date: {history.TransactionDate:yyyy-MM-dd HH:mm:ss}");
                        _logger.LogWarning($"[REF-114] Transaction Type: {history.TransactionType}, Amount: {history.TransactionAmount}");
                        _logger.LogWarning($"[REF-114] Record position in sequence: {i + 1} of {historyRecords.Count}");
                        _logger.LogWarning($"[REF-114] OLD BalanceBefore: {history.BalanceBefore}, OLD BalanceAfter: {history.BalanceAfter}");

                        // Show previous record details if exists
                        if (i > 0)
                        {
                            var prevRecord = historyRecords[i - 1];
                            _logger.LogWarning($"[REF-114] PREVIOUS RECORD IN SEQUENCE:");
                            _logger.LogWarning($"[REF-114]   - Previous ID: {prevRecord.Id}, RefId: {prevRecord.ReferenceId}");
                            _logger.LogWarning($"[REF-114]   - Previous Date: {prevRecord.TransactionDate:yyyy-MM-dd HH:mm:ss}");
                            _logger.LogWarning($"[REF-114]   - Previous Type: {prevRecord.TransactionType}, Amount: {prevRecord.TransactionAmount}");
                            _logger.LogWarning($"[REF-114]   - Previous BalanceBefore: {prevRecord.BalanceBefore}");
                            _logger.LogWarning($"[REF-114]   - Previous BalanceAfter: {prevRecord.BalanceAfter}");
                            _logger.LogWarning($"[REF-114]   - Will use Previous.BalanceAfter ({prevRecord.BalanceAfter}) as my BalanceBefore");
                        }
                        else
                        {
                            _logger.LogWarning($"[REF-114] This is the FIRST record in sequence, BalanceBefore will be 0");
                        }
                    }

                    // Set balance before this transaction:
                    // - For first record: use 0 (starting balance)
                    // - For subsequent records: use exactly the BalanceAfter of previous record
                    if (i == 0)
                    {
                        history.BalanceBefore = 0; // First transaction starts from zero
                    }
                    else
                    {
                        history.BalanceBefore = historyRecords[i - 1].BalanceAfter; // Chain consistency!
                    }

                    // Apply the transaction amount to get BalanceAfter
                    history.BalanceAfter = history.BalanceBefore + history.TransactionAmount;

                    if (isReferenceId114)
                    {
                        _logger.LogWarning($"[REF-114] CALCULATION RESULT:");
                        _logger.LogWarning($"[REF-114] NEW BalanceBefore: {history.BalanceBefore}");
                        _logger.LogWarning($"[REF-114] Transaction Amount: {history.TransactionAmount}");
                        _logger.LogWarning($"[REF-114] NEW BalanceAfter: {history.BalanceAfter} = {history.BalanceBefore} + {history.TransactionAmount}");

                        if (i > 0)
                        {
                            var prevRecord = historyRecords[i - 1];
                            _logger.LogWarning($"[REF-114] Chain Check: Previous BalanceAfter ({prevRecord.BalanceAfter}) == My BalanceBefore ({history.BalanceBefore}): {prevRecord.BalanceAfter == history.BalanceBefore}");
                        }

                        _logger.LogWarning($"[REF-114] ==================== END REF-114 PROCESSING ====================");
                    }
                }

                // Update the current balance to the final balance (last record's BalanceAfter)
                currentBalance.Balance = historyRecords.Count > 0 ? historyRecords.Last().BalanceAfter : 0;
                currentBalance.LastUpdated = DateTime.UtcNow;

                if (hasReferenceId114)
                {
                    _logger.LogWarning($"[REF-114-SEQUENCE] FINAL SEQUENCE AFTER PROCESSING:");
                    for (int i = 0; i < historyRecords.Count; i++)
                    {
                        var h = historyRecords[i];
                        var prefix = h.ReferenceId == 114 ? ">>> REF-114 >>>" : "              ";
                        _logger.LogWarning($"[REF-114-SEQUENCE] {prefix} #{i + 1}: ID={h.Id}, RefId={h.ReferenceId}, Date={h.TransactionDate:yyyy-MM-dd HH:mm:ss}, Amount={h.TransactionAmount}, Before={h.BalanceBefore}, After={h.BalanceAfter}");

                        // Check sequence consistency
                        if (i > 0)
                        {
                            var prevTransaction = historyRecords[i - 1];
                            if (h.BalanceBefore != prevTransaction.BalanceAfter)
                            {
                                _logger.LogError($"[REF-114-ERROR] SEQUENCE BREAK at transaction #{i + 1}! Previous BalanceAfter={prevTransaction.BalanceAfter} != Current BalanceBefore={h.BalanceBefore}");
                            }
                            else
                            {
                                _logger.LogInformation($"[REF-114-SUCCESS] Chain consistent at #{i + 1}: Previous BalanceAfter={prevTransaction.BalanceAfter} == Current BalanceBefore={h.BalanceBefore}");
                            }
                        }

                        // Show next record details if this is REF-114
                        if (h.ReferenceId == 114 && i < historyRecords.Count - 1)
                        {
                            var nextRecord = historyRecords[i + 1];
                            _logger.LogWarning($"[REF-114-CHAIN] NEXT RECORD AFTER REF-114:");
                            _logger.LogWarning($"[REF-114-CHAIN]   - Next ID: {nextRecord.Id}, RefId: {nextRecord.ReferenceId}");
                            _logger.LogWarning($"[REF-114-CHAIN]   - Next Date: {nextRecord.TransactionDate:yyyy-MM-dd HH:mm:ss}");
                            _logger.LogWarning($"[REF-114-CHAIN]   - Next should get BalanceBefore = {h.BalanceAfter} (my BalanceAfter)");
                            _logger.LogWarning($"[REF-114-CHAIN]   - Next actual BalanceBefore = {nextRecord.BalanceBefore}");
                            _logger.LogWarning($"[REF-114-CHAIN]   - Chain continues correctly: {nextRecord.BalanceBefore == h.BalanceAfter}");
                        }
                    }
                    var finalBalance = historyRecords.Count > 0 ? historyRecords.Last().BalanceAfter : 0;
                    _logger.LogWarning($"[REF-114-SEQUENCE] Final Balance for Customer {combination.CustomerId} - Currency {combination.CurrencyCode}: {finalBalance}");
                }

                var finalBalance2 = historyRecords.Count > 0 ? historyRecords.Last().BalanceAfter : 0;
                _logger.LogInformation($"Customer {combination.CustomerId} - Currency {combination.CurrencyCode}: {historyRecords.Count} transactions processed, final balance: {finalBalance2}");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Customer balance recalculation completed");
        }

        private async Task RecalculateCurrencyPoolBalancesFromHistoryAsync()
        {
            _logger.LogInformation("Recalculating currency pool balances from history");

            // Get all unique currencies that have pool history (excluding deleted and frozen records)
            var currencies = await _context.CurrencyPoolHistory
                .Where(h => !h.IsDeleted) // Only consider non-deleted records
                .Select(h => h.CurrencyCode)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation($"Processing {currencies.Count} currencies for pool balances");

            // Process each currency separately
            foreach (var currencyCode in currencies)
            {
                _logger.LogInformation($"Processing Currency Pool: {currencyCode}");

                // Get all pool history records for this currency, ordered by transaction date
                // IMPORTANT: Exclude deleted and frozen records from the sequence
                var historyRecords = await _context.CurrencyPoolHistory
                    .Where(h => h.CurrencyCode == currencyCode && !h.IsDeleted) // EXCLUDE DELETED RECORDS!
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id) // Secondary sort for same transaction dates
                    .ToListAsync();

                // Get or create current pool record for this currency
                var currentPool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(cp => cp.CurrencyCode == currencyCode);

                if (currentPool == null)
                {
                    currentPool = new CurrencyPool
                    {
                        CurrencyCode = currencyCode,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.CurrencyPools.Add(currentPool);
                }

                // Reset balance to zero for this currency before recalculation
                currentPool.Balance = 0;

                // Process each transaction chronologically for this currency
                for (int i = 0; i < historyRecords.Count; i++)
                {
                    var history = historyRecords[i];

                    // Set balance before this transaction:
                    // - For first record: use 0 (starting balance)
                    // - For subsequent records: use exactly the BalanceAfter of previous record
                    if (i == 0)
                    {
                        history.BalanceBefore = 0; // First transaction starts from zero
                    }
                    else
                    {
                        history.BalanceBefore = historyRecords[i - 1].BalanceAfter; // Chain consistency!
                    }

                    // Apply the transaction amount to get BalanceAfter
                    history.BalanceAfter = history.BalanceBefore + history.TransactionAmount;
                }

                // Update the current pool balance to the final balance (last record's BalanceAfter)
                currentPool.Balance = currentPool.TotalBought - currentPool.TotalSold;
                currentPool.LastUpdated = DateTime.UtcNow;
                _logger.LogInformation($"Currency Pool {currencyCode}: {historyRecords.Count} transactions processed, final balance: {currentPool.Balance}");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Currency pool balance recalculation completed");
        }

        private async Task RecalculateBankAccountBalancesFromHistoryAsync()
        {
            _logger.LogInformation("Recalculating bank account balances from history");

            // Get all unique bank accounts that have balance history (excluding deleted and frozen records)
            var bankAccountIds = await _context.BankAccountBalanceHistory
                .Where(h => !h.IsDeleted) // Only consider non-deleted records
                .Select(h => h.BankAccountId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation($"Processing {bankAccountIds.Count} bank accounts for balance recalculation");

            // Process each bank account separately
            foreach (var bankAccountId in bankAccountIds)
            {
                _logger.LogInformation($"Processing Bank Account ID: {bankAccountId}");

                // Get all bank account history records for this account, ordered by transaction date
                // IMPORTANT: Exclude deleted and frozen records from the sequence
                var historyRecords = await _context.BankAccountBalanceHistory
                    .Where(h => h.BankAccountId == bankAccountId && !h.IsDeleted) // EXCLUDE DELETED RECORDS!
                    .OrderBy(h => h.TransactionDate)
                    .ThenBy(h => h.Id) // Secondary sort for same transaction dates
                    .ToListAsync();

                // Get or create current balance record for this bank account
                var currentBalance = await _context.BankAccountBalances
                    .FirstOrDefaultAsync(bb => bb.BankAccountId == bankAccountId);

                if (currentBalance == null)
                {
                    currentBalance = new BankAccountBalance
                    {
                        BankAccountId = bankAccountId,
                        Balance = 0,
                        LastUpdated = DateTime.UtcNow
                    };
                    _context.BankAccountBalances.Add(currentBalance);
                }

                // Reset balance to zero for this bank account before recalculation
                currentBalance.Balance = 0;

                // Process each transaction chronologically for this bank account
                for (int i = 0; i < historyRecords.Count; i++)
                {
                    var history = historyRecords[i];

                    // Set balance before this transaction:
                    // - For first record: use 0 (starting balance)
                    // - For subsequent records: use exactly the BalanceAfter of previous record
                    if (i == 0)
                    {
                        history.BalanceBefore = 0; // First transaction starts from zero
                    }
                    else
                    {
                        history.BalanceBefore = historyRecords[i - 1].BalanceAfter; // Chain consistency!
                    }

                    // Apply the transaction amount to get BalanceAfter
                    history.BalanceAfter = history.BalanceBefore + history.TransactionAmount;
                }

                // Update the current balance to the final balance (last record's BalanceAfter)
                currentBalance.Balance = historyRecords.Count > 0 ? historyRecords.Last().BalanceAfter : 0;
                currentBalance.LastUpdated = DateTime.UtcNow;

                var finalBankBalance = historyRecords.Count > 0 ? historyRecords.Last().BalanceAfter : 0;
                _logger.LogInformation($"Bank Account {bankAccountId}: {historyRecords.Count} transactions processed, final balance: {finalBankBalance}");
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Bank account balance recalculation completed");
        }

        #endregion

    }
}
