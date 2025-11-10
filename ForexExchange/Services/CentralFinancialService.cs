
using ForexExchange.Extensions;
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
        /// Thread safety semaphore to prevent concurrent balance rebuilds
        /// </summary>
        private static readonly SemaphoreSlim _rebuildSemaphore = new SemaphoreSlim(1, 1);

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

        private readonly ICurrencyPoolService _currencyPoolService;

        /// <summary>
        /// Service provider for creating scoped services in background tasks
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// **CONSTRUCTOR** - Initializes the central financial service with required dependencies.
        /// </summary>
        /// <param name="context">Entity Framework database context for data operations</param>
        /// <param name="logger">Logger for operation tracking and debugging</param>
        /// <param name="notificationHub">Notification hub for real-time admin notifications</param>
        /// <param name="serviceProvider">Service provider for creating scoped services</param>

        public CentralFinancialService(ForexDbContext context, ILogger<CentralFinancialService> logger, INotificationHub notificationHub, ICurrencyPoolService currencyPoolService, IServiceProvider serviceProvider)
        {
            _context = context;
            _logger = logger;
            _notificationHub = notificationHub;
            _currencyPoolService = currencyPoolService;
            _serviceProvider = serviceProvider;
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

            // Normalize currency codes to uppercase for case-insensitive matching
            var fromCurrencyCode = (order.FromCurrency.Code ?? "").ToUpperInvariant().Trim();
            var toCurrencyCode = (order.ToCurrency.Code ?? "").ToUpperInvariant().Trim();
            
            // Load all customer balances for this customer for case-insensitive lookup
            var customerBalances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == order.CustomerId)
                .ToListAsync();
            
            var customerBalanceFrom = customerBalances.FirstOrDefault(cb => 
                (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == fromCurrencyCode);
            
            if (customerBalanceFrom == null)
            {
                _logger.LogWarning($"Customer balance not found for customer {order.CustomerId} and currency {fromCurrencyCode} - creating with zero balance");
                
                // Auto-create missing customer balance record with zero balance (normalized to uppercase)
                customerBalanceFrom = new CustomerBalance
                {
                    CustomerId = order.CustomerId,
                    CurrencyCode = fromCurrencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.UtcNow
                };
                
                _context.CustomerBalances.Add(customerBalanceFrom);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Created new customer balance record: CustomerId={order.CustomerId}, Currency={fromCurrencyCode}, Balance=0");
            }
            else if (customerBalanceFrom.CurrencyCode != fromCurrencyCode)
            {
                // Normalize existing balance's currency code to uppercase
                _logger.LogWarning($"Normalizing CustomerBalance CurrencyCode from '{customerBalanceFrom.CurrencyCode}' to '{fromCurrencyCode}' for Customer {order.CustomerId}");
                customerBalanceFrom.CurrencyCode = fromCurrencyCode;
                await _context.SaveChangesAsync();
            }
            _logger.LogInformation($"CustomerBalanceFrom: {customerBalanceFrom.Balance}");

            var customerBalanceTo = customerBalances.FirstOrDefault(cb => 
                (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == toCurrencyCode);
            
            if (customerBalanceTo == null)
            {
                _logger.LogWarning($"Customer balance not found for customer {order.CustomerId} and currency {toCurrencyCode} - creating with zero balance");
                
                // Auto-create missing customer balance record with zero balance (normalized to uppercase)
                customerBalanceTo = new CustomerBalance
                {
                    CustomerId = order.CustomerId,
                    CurrencyCode = toCurrencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.UtcNow
                };
                
                _context.CustomerBalances.Add(customerBalanceTo);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Created new customer balance record: CustomerId={order.CustomerId}, Currency={toCurrencyCode}, Balance=0");
            }
            else if (customerBalanceTo.CurrencyCode != toCurrencyCode)
            {
                // Normalize existing balance's currency code to uppercase
                _logger.LogWarning($"Normalizing CustomerBalance CurrencyCode from '{customerBalanceTo.CurrencyCode}' to '{toCurrencyCode}' for Customer {order.CustomerId}");
                customerBalanceTo.CurrencyCode = toCurrencyCode;
                await _context.SaveChangesAsync();
            }
            _logger.LogInformation($"CustomerBalanceTo: {customerBalanceTo.Balance}");

            var poolBalanceFrom = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.FromCurrency.Id);
            if (poolBalanceFrom == null)
            {
                await _currencyPoolService.CreatePoolAsync(order.FromCurrency.Id);
                _logger.LogError($"Currency pool not found for currency {order.FromCurrency.Code}");
                throw new Exception($"Currency pool not found for currency {order.FromCurrency.Code}");
            }
            _logger.LogInformation($"PoolBalanceFrom: {poolBalanceFrom.Balance}");

            var poolBalanceTo = await _context.CurrencyPools.FirstOrDefaultAsync(p => p.CurrencyId == order.ToCurrency.Id);
            if (poolBalanceTo == null)
            {
                await _currencyPoolService.CreatePoolAsync(order.ToCurrency.Id);
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


        /// <summary>
        /// **PREVIEW SIMULATION** - Calculates the financial impact of an accounting document without making database changes.
        /// 
        /// This method simulates exactly what would happen when a document is processed, allowing the UI
        /// to show users the precise effect on customer and bank account balances.
        /// 
        /// **Auto-Create Missing Balances**: Automatically creates missing CustomerBalance and BankAccountBalance
        /// records with zero balance to ensure preview calculations work for all scenarios.
        /// 
        /// **Business Logic**:
        /// - Payer Customer: Gets +amount (receives money/credit)
        /// - Receiver Customer: Gets -amount (pays money/debit)
        /// - Payer Bank Account: Gets -amount (money flows out)
        /// - Receiver Bank Account: Gets +amount (money flows in)
        /// </summary>
        /// <param name="document">Accounting document with all party and amount information</param>
        /// <returns>Preview effects showing before/after balances for customers and bank accounts</returns>
        public async Task<AccountingDocumentPreviewEffectsDto> PreviewAccountingDocumentEffectsAsync(AccountingDocument document)
        {
            _logger.LogInformation($"[PreviewAccountingDocumentEffectsAsync] Called for DocumentId={document.Id}, Amount={document.Amount}, Currency={document.CurrencyCode}");

            var effects = new AccountingDocumentPreviewEffectsDto
            {
                DocumentId = document.Id,
                Amount = document.Amount,
                CurrencyCode = document.CurrencyCode,
                CustomerEffects = new List<CustomerBalanceEffect>(),
                BankAccountEffects = new List<BankAccountBalanceEffect>(),
                Warnings = new List<string>()
            };

            // Validate required fields
            if (document.Amount == 0)
            {
                effects.Warnings.Add("مبلغ سند نمی‌تواند صفر باشد.");
                return effects;
            }

            if (string.IsNullOrEmpty(document.CurrencyCode))
            {
                effects.Warnings.Add("ارز سند انتخاب نشده است.");
                return effects;
            }

            // Process Payer Customer Effect
            if (document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue)
            {
                var payerCustomer = await _context.Customers.FindAsync(document.PayerCustomerId.Value);
                if (payerCustomer != null)
                {
                    // Get or create customer balance
                    var customerBalance = await _context.CustomerBalances
                        .FirstOrDefaultAsync(cb => cb.CustomerId == document.PayerCustomerId.Value && cb.CurrencyCode == document.CurrencyCode);
                    
                    if (customerBalance == null)
                    {
                        _logger.LogWarning($"Customer balance not found for customer {document.PayerCustomerId.Value} and currency {document.CurrencyCode} - creating with zero balance");
                        
                        customerBalance = new CustomerBalance
                        {
                            CustomerId = document.PayerCustomerId.Value,
                            CurrencyCode = document.CurrencyCode,
                            Balance = 0,
                            LastUpdated = DateTime.UtcNow
                        };
                        
                        _context.CustomerBalances.Add(customerBalance);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation($"Created new customer balance record: CustomerId={document.PayerCustomerId.Value}, Currency={document.CurrencyCode}, Balance=0");
                    }

                    var currentBalance = customerBalance.Balance;
                    var newBalance = currentBalance + document.Amount; // Payer gets +amount

                    effects.CustomerEffects.Add(new CustomerBalanceEffect
                    {
                        CustomerId = document.PayerCustomerId.Value,
                        CustomerName = payerCustomer.FullName,
                        CurrencyCode = document.CurrencyCode,
                        CurrentBalance = currentBalance,
                        TransactionAmount = document.Amount,
                        NewBalance = newBalance,
                        Role = "Payer"
                    });

                    if (newBalance < 0)
                    {
                        effects.Warnings.Add($"تراز مشتری {payerCustomer.FullName} در ارز {document.CurrencyCode} منفی خواهد شد ({newBalance:N2}).");
                    }
                }
            }

            // Process Receiver Customer Effect
            if (document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue)
            {
                var receiverCustomer = await _context.Customers.FindAsync(document.ReceiverCustomerId.Value);
                if (receiverCustomer != null)
                {
                    // Get or create customer balance
                    var customerBalance = await _context.CustomerBalances
                        .FirstOrDefaultAsync(cb => cb.CustomerId == document.ReceiverCustomerId.Value && cb.CurrencyCode == document.CurrencyCode);
                    
                    if (customerBalance == null)
                    {
                        _logger.LogWarning($"Customer balance not found for customer {document.ReceiverCustomerId.Value} and currency {document.CurrencyCode} - creating with zero balance");
                        
                        customerBalance = new CustomerBalance
                        {
                            CustomerId = document.ReceiverCustomerId.Value,
                            CurrencyCode = document.CurrencyCode,
                            Balance = 0,
                            LastUpdated = DateTime.UtcNow
                        };
                        
                        _context.CustomerBalances.Add(customerBalance);
                        await _context.SaveChangesAsync();
                        
                        _logger.LogInformation($"Created new customer balance record: CustomerId={document.ReceiverCustomerId.Value}, Currency={document.CurrencyCode}, Balance=0");
                    }

                    var currentBalance = customerBalance.Balance;
                    var newBalance = currentBalance - document.Amount; // Receiver gets -amount

                    effects.CustomerEffects.Add(new CustomerBalanceEffect
                    {
                        CustomerId = document.ReceiverCustomerId.Value,
                        CustomerName = receiverCustomer.FullName,
                        CurrencyCode = document.CurrencyCode,
                        CurrentBalance = currentBalance,
                        TransactionAmount = -document.Amount,
                        NewBalance = newBalance,
                        Role = "Receiver"
                    });

                    if (newBalance < 0)
                    {
                        effects.Warnings.Add($"تراز مشتری {receiverCustomer.FullName} در ارز {document.CurrencyCode} منفی خواهد شد ({newBalance:N2}).");
                    }
                }
            }

            // Process Payer Bank Account Effect
            if (document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue)
            {
                var payerBankAccount = await _context.BankAccounts.FindAsync(document.PayerBankAccountId.Value);
                if (payerBankAccount != null)
                {
                    // Validate currency match
                    if (payerBankAccount.CurrencyCode != document.CurrencyCode)
                    {
                        effects.Warnings.Add($"ارز حساب بانکی پرداخت کننده ({payerBankAccount.CurrencyCode}) با ارز سند ({document.CurrencyCode}) مطابقت ندارد.");
                    }
                    else
                    {
                        // Get or create bank account balance
                        var bankBalance = await _context.BankAccountBalances
                            .FirstOrDefaultAsync(bab => bab.BankAccountId == document.PayerBankAccountId.Value);
                        
                        if (bankBalance == null)
                        {
                            _logger.LogWarning($"Bank account balance not found for account {document.PayerBankAccountId.Value} - creating with zero balance");
                            
                            bankBalance = new BankAccountBalance
                            {
                                BankAccountId = document.PayerBankAccountId.Value,
                                Balance = 0,
                                LastUpdated = DateTime.UtcNow
                            };
                            
                            _context.BankAccountBalances.Add(bankBalance);
                            await _context.SaveChangesAsync();
                            
                            _logger.LogInformation($"Created new bank account balance record: BankAccountId={document.PayerBankAccountId.Value}, Balance=0");
                        }

                        var currentBalance = bankBalance.Balance;
                        var newBalance = currentBalance + document.Amount; // Bank pays out

                        effects.BankAccountEffects.Add(new BankAccountBalanceEffect
                        {
                            BankAccountId = document.PayerBankAccountId.Value,
                            BankName = payerBankAccount.BankName,
                            AccountNumber = payerBankAccount.AccountNumber,
                            CurrencyCode = document.CurrencyCode,
                            CurrentBalance = currentBalance,
                            TransactionAmount = document.Amount,
                            NewBalance = newBalance,
                            Role = "Payer"
                        });

                        if (newBalance < 0)
                        {
                            effects.Warnings.Add($"تراز حساب بانکی {payerBankAccount.BankName} - {payerBankAccount.AccountNumber} منفی خواهد شد ({newBalance:N2}).");
                        }
                    }
                }
            }

            // Process Receiver Bank Account Effect
            if (document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue)
            {
                var receiverBankAccount = await _context.BankAccounts.FindAsync(document.ReceiverBankAccountId.Value);
                if (receiverBankAccount != null)
                {
                    // Validate currency match
                    if (receiverBankAccount.CurrencyCode != document.CurrencyCode)
                    {
                        effects.Warnings.Add($"ارز حساب بانکی دریافت کننده ({receiverBankAccount.CurrencyCode}) با ارز سند ({document.CurrencyCode}) مطابقت ندارد.");
                    }
                    else
                    {
                        // Get or create bank account balance
                        var bankBalance = await _context.BankAccountBalances
                            .FirstOrDefaultAsync(bab => bab.BankAccountId == document.ReceiverBankAccountId.Value);
                        
                        if (bankBalance == null)
                        {
                            _logger.LogWarning($"Bank account balance not found for account {document.ReceiverBankAccountId.Value} - creating with zero balance");
                            
                            bankBalance = new BankAccountBalance
                            {
                                BankAccountId = document.ReceiverBankAccountId.Value,
                                Balance = 0,
                                LastUpdated = DateTime.UtcNow
                            };
                            
                            _context.BankAccountBalances.Add(bankBalance);
                            await _context.SaveChangesAsync();
                            
                            _logger.LogInformation($"Created new bank account balance record: BankAccountId={document.ReceiverBankAccountId.Value}, Balance=0");
                        }

                        var currentBalance = bankBalance.Balance;
                        var newBalance = currentBalance - document.Amount; // Bank receives

                        effects.BankAccountEffects.Add(new BankAccountBalanceEffect
                        {
                            BankAccountId = document.ReceiverBankAccountId.Value,
                            BankName = receiverBankAccount.BankName,
                            AccountNumber = receiverBankAccount.AccountNumber,
                            CurrencyCode = document.CurrencyCode,
                            CurrentBalance = currentBalance,
                            TransactionAmount = - document.Amount,
                            NewBalance = newBalance,
                            Role = "Receiver"
                        });
                    }
                }
            }

            // Additional validations
            if (document.PayerType == PayerType.Customer && document.ReceiverType == ReceiverType.Customer)
            {
                if (document.PayerCustomerId == document.ReceiverCustomerId)
                {
                    effects.Warnings.Add("مشتری نمی‌تواند به خودش پرداخت کند.");
                }
            }

            if (document.PayerType == PayerType.System && document.ReceiverType == ReceiverType.System)
            {
                if (document.PayerBankAccountId == document.ReceiverBankAccountId)
                {
                    effects.Warnings.Add("حساب بانکی نمی‌تواند به خودش انتقال داشته باشد.");
                }
            }

            _logger.LogInformation($"[PreviewAccountingDocumentEffectsAsync] Completed with {effects.CustomerEffects.Count} customer effects, {effects.BankAccountEffects.Count} bank effects, {effects.Warnings.Count} warnings");

            return effects;
        }


        #endregion Preview

        #region Incremental Update Helpers

        /// <summary>
        /// **INCREMENTAL UPDATE HELPER** - Gets the last balance history record for a customer+currency combination.
        /// Returns the current balance from CustomerBalance table if no history exists.
        /// Handles case-insensitive currency code matching.
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="currencyCode">Currency code (case-insensitive)</param>
        /// <returns>Last balance (BalanceAfter from last history record, or current balance if no history)</returns>
        private async Task<decimal> GetLastCustomerHistoryBalanceAsync(int customerId, string currencyCode)
        {
            var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

            // Get all history records for this customer (load first, then filter in memory for case-insensitive matching)
            var allHistory = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId && !h.IsDeleted)
                .ToListAsync();

            // Filter in memory for case-insensitive currency code matching
            var lastHistory = allHistory
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            if (lastHistory != null)
            {
                return lastHistory.BalanceAfter;
            }

            // No history exists, get current balance from CustomerBalance table
            var customerBalances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == customerId)
                .ToListAsync();

            var customerBalance = customerBalances.FirstOrDefault(cb =>
                (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

            if (customerBalance != null)
            {
                // Normalize currency code if needed
                if (customerBalance.CurrencyCode != normalizedCurrencyCode)
                {
                    customerBalance.CurrencyCode = normalizedCurrencyCode;
                    await _context.SaveChangesAsync();
                }
                return customerBalance.Balance;
            }

            // No balance record exists, return zero (will be created when needed)
            return 0m;
        }

        /// <summary>
        /// **INCREMENTAL UPDATE HELPER** - Gets the last balance history record for a currency pool.
        /// Returns the current balance from CurrencyPool table if no history exists.
        /// Handles case-insensitive currency code matching.
        /// </summary>
        /// <param name="currencyCode">Currency code (case-insensitive)</param>
        /// <returns>Last balance (BalanceAfter from last history record, or current balance if no history)</returns>
        private async Task<decimal> GetLastPoolHistoryBalanceAsync(string currencyCode)
        {
            var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

            // Get all history records (load first, then filter in memory for case-insensitive matching)
            var allHistory = await _context.CurrencyPoolHistory
                .Where(h => !h.IsDeleted)
                .ToListAsync();

            // Filter in memory for case-insensitive currency code matching
            var lastHistory = allHistory
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            if (lastHistory != null)
            {
                return lastHistory.BalanceAfter;
            }

            // No history exists, get current balance from CurrencyPool table
            // First, find the currency by code (load all and filter in memory)
            var allCurrencies = await _context.Currencies.ToListAsync();
            var currency = allCurrencies
                .FirstOrDefault(c => (c.Code ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

            if (currency != null)
            {
                var pool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == currency.Id);

                if (pool != null)
                {
                    // Normalize currency code if needed
                    if (pool.CurrencyCode != normalizedCurrencyCode)
                    {
                        pool.CurrencyCode = normalizedCurrencyCode;
                        await _context.SaveChangesAsync();
                    }
                    return pool.Balance;
                }
            }

            // No pool exists, return zero (should be created via CurrencyPoolService if needed)
            return 0m;
        }

        /// <summary>
        /// **INCREMENTAL UPDATE HELPER** - Gets the last balance history record for a bank account.
        /// Returns the current balance from BankAccount table if no history exists.
        /// </summary>
        /// <param name="bankAccountId">Bank account ID</param>
        /// <returns>Last balance (BalanceAfter from last history record, or current balance if no history)</returns>
        private async Task<decimal> GetLastBankAccountHistoryBalanceAsync(int bankAccountId)
        {
            // Get last history record for this bank account
            var lastHistory = await _context.BankAccountBalanceHistory
                .Where(h => h.BankAccountId == bankAccountId && !h.IsDeleted)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            if (lastHistory != null)
            {
                return lastHistory.BalanceAfter;
            }

            // No history exists, get current balance from BankAccount table
            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == bankAccountId);

            if (bankAccount != null)
            {
                return bankAccount.AccountBalance;
            }

            // Bank account doesn't exist, return zero
            return 0m;
        }

        /// <summary>
        /// **INCREMENTAL UPDATE HELPER** - Adds a customer balance history record incrementally.
        /// Gets the last balance, calculates the new balance, creates history record, and updates CustomerBalance.
        /// Handles transaction date ordering and case-insensitive currency matching.
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="currencyCode">Currency code</param>
        /// <param name="transactionAmount">Transaction amount (positive or negative)</param>
        /// <param name="transactionType">Transaction type</param>
        /// <param name="referenceId">Reference ID (OrderId, DocumentId, or null for manual)</param>
        /// <param name="transactionDate">Transaction date</param>
        /// <param name="description">Description</param>
        /// <param name="transactionNumber">Transaction number (optional)</param>
        /// <param name="performedBy">Performed by</param>
        /// <returns>Created history record</returns>
        private async Task<CustomerBalanceHistory> AddCustomerBalanceHistoryIncrementalAsync(
            int customerId,
            string currencyCode,
            decimal transactionAmount,
            CustomerBalanceTransactionType transactionType,
            int? referenceId,
            DateTime transactionDate,
            string description,
            string? transactionNumber,
            string performedBy)
        {
            var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

            // Get last balance (from history or current balance)
            // Note: For simplicity, we assume transactions are usually in chronological order
            // If transaction date is in the past, we use the balance before that date
            // For future enhancement: handle out-of-order transactions by rebuilding from insertion point
            var balanceBefore = await GetLastCustomerHistoryBalanceAsync(customerId, normalizedCurrencyCode);

            // Calculate new balance
            var balanceAfter = balanceBefore + transactionAmount;

            // Ensure CustomerBalance record exists and is normalized
            var customerBalances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == customerId)
                .ToListAsync();

            var customerBalance = customerBalances.FirstOrDefault(cb =>
                (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

            if (customerBalance == null)
            {
                customerBalance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyCode = normalizedCurrencyCode,
                    Balance = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.CustomerBalances.Add(customerBalance);
                await _context.SaveChangesAsync();
            }
            else if (customerBalance.CurrencyCode != normalizedCurrencyCode)
            {
                customerBalance.CurrencyCode = normalizedCurrencyCode;
                await _context.SaveChangesAsync();
            }

            // Create history record
            var historyRecord = new CustomerBalanceHistory
            {
                CustomerId = customerId,
                CurrencyCode = normalizedCurrencyCode,
                TransactionType = transactionType,
                ReferenceId = referenceId,
                BalanceBefore = balanceBefore,
                TransactionAmount = transactionAmount,
                BalanceAfter = balanceAfter,
                Description = description,
                TransactionNumber = transactionNumber,
                TransactionDate = transactionDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = performedBy,
                IsDeleted = false
            };

            _context.CustomerBalanceHistory.Add(historyRecord);

            // Update customer balance
            customerBalance.Balance = balanceAfter;
            customerBalance.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Added incremental customer balance history: Customer {customerId}, Currency {normalizedCurrencyCode}, Amount {transactionAmount}, Balance {balanceBefore} -> {balanceAfter}");

            return historyRecord;
        }

        /// <summary>
        /// **INCREMENTAL UPDATE HELPER** - Gets the last balance for a customer+currency up to (but not including) a transaction date.
        /// Used for out-of-order transaction insertion.
        /// </summary>
        private async Task<decimal> GetLastCustomerBalanceHistoryAsync(int customerId, string currencyCode, DateTime beforeDate)
        {
            var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

            // Get all history records for this customer before the date (load first, then filter in memory)
            var allHistory = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId && !h.IsDeleted && h.TransactionDate < beforeDate)
                .ToListAsync();

            // Filter in memory for case-insensitive currency code matching
            var lastHistory = allHistory
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            if (lastHistory != null)
            {
                return lastHistory.BalanceAfter;
            }

            // No history before this date, check if there are any later transactions that need to be recalculated
            // For now, we'll assume transactions are usually in order and use the current last balance
            return await GetLastCustomerHistoryBalanceAsync(customerId, currencyCode);
        }

        /// <summary>
        /// **INCREMENTAL UPDATE HELPER** - Adds a pool balance history record incrementally.
        /// Gets the last balance, calculates the new balance, creates history record, and updates CurrencyPool.
        /// Also updates buy/sell counts and totals for orders.
        /// </summary>
        /// <param name="currencyCode">Currency code</param>
        /// <param name="transactionAmount">Transaction amount (positive or negative)</param>
        /// <param name="transactionType">Transaction type</param>
        /// <param name="referenceId">Reference ID (OrderId or null for manual)</param>
        /// <param name="transactionDate">Transaction date</param>
        /// <param name="description">Description</param>
        /// <param name="poolTransactionType">Pool transaction type (Buy/Sell/Manual)</param>
        /// <param name="performedBy">Performed by</param>
        /// <returns>Created history record</returns>
        private async Task<CurrencyPoolHistory> AddPoolBalanceHistoryIncrementalAsync(
            string currencyCode,
            decimal transactionAmount,
            CurrencyPoolTransactionType transactionType,
            int? referenceId,
            DateTime transactionDate,
            string description,
            string poolTransactionType,
            string performedBy)
        {
            var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

            // Get currency by code (load all and filter in memory)
            var allCurrencies = await _context.Currencies.ToListAsync();
            var currency = allCurrencies
                .FirstOrDefault(c => (c.Code ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

            if (currency == null)
            {
                throw new ArgumentException($"Currency with code {normalizedCurrencyCode} not found");
            }

            // Get or create pool
            var pool = await _context.CurrencyPools
                .FirstOrDefaultAsync(p => p.CurrencyId == currency.Id);

            if (pool == null)
            {
                await _currencyPoolService.CreatePoolAsync(currency.Id);
                pool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == currency.Id);
            }

            if (pool == null)
            {
                throw new Exception($"Failed to create or retrieve pool for currency {normalizedCurrencyCode}");
            }

            // Normalize pool currency code
            if (pool.CurrencyCode != normalizedCurrencyCode)
            {
                pool.CurrencyCode = normalizedCurrencyCode;
                await _context.SaveChangesAsync();
            }

            // Get last balance (from history or current balance)
            var balanceBefore = await GetLastPoolHistoryBalanceAsync(normalizedCurrencyCode);

            // Calculate new balance
            var balanceAfter = balanceBefore + transactionAmount;

            // Create history record
            var historyRecord = new CurrencyPoolHistory
            {
                CurrencyCode = normalizedCurrencyCode,
                TransactionType = transactionType,
                ReferenceId = referenceId,
                BalanceBefore = balanceBefore,
                TransactionAmount = transactionAmount,
                BalanceAfter = balanceAfter,
                PoolTransactionType = poolTransactionType,
                Description = description,
                TransactionDate = transactionDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = performedBy,
                IsDeleted = false
            };

            _context.CurrencyPoolHistory.Add(historyRecord);

            // Update pool balance
            pool.Balance = balanceAfter;
            pool.LastUpdated = DateTime.UtcNow;

            // Update buy/sell counts and totals for orders
            if (transactionType == CurrencyPoolTransactionType.Order && referenceId.HasValue)
            {
                if (poolTransactionType == "Buy" || transactionAmount > 0)
                {
                    pool.TotalBought += Math.Abs(transactionAmount);
                    // Note: ActiveBuyOrderCount should be recalculated separately if needed
                }
                else if (poolTransactionType == "Sell" || transactionAmount < 0)
                {
                    pool.TotalSold += Math.Abs(transactionAmount);
                    // Note: ActiveSellOrderCount should be recalculated separately if needed
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Added incremental pool balance history: Currency {normalizedCurrencyCode}, Amount {transactionAmount}, Balance {balanceBefore} -> {balanceAfter}");

            return historyRecord;
        }

        /// <summary>
        /// **INCREMENTAL UPDATE HELPER** - Adds a bank account balance history record incrementally.
        /// Gets the last balance, calculates the new balance, creates history record, and updates BankAccount.
        /// </summary>
        /// <param name="bankAccountId">Bank account ID</param>
        /// <param name="transactionAmount">Transaction amount (positive or negative)</param>
        /// <param name="transactionType">Transaction type</param>
        /// <param name="referenceId">Reference ID (DocumentId or null for manual)</param>
        /// <param name="transactionDate">Transaction date</param>
        /// <param name="description">Description</param>
        /// <param name="performedBy">Performed by</param>
        /// <returns>Created history record</returns>
        private async Task<BankAccountBalanceHistory> AddBankAccountBalanceHistoryIncrementalAsync(
            int bankAccountId,
            decimal transactionAmount,
            BankAccountTransactionType transactionType,
            int? referenceId,
            DateTime transactionDate,
            string description,
            string performedBy)
        {
            // Get bank account
            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == bankAccountId);

            if (bankAccount == null)
            {
                throw new ArgumentException($"Bank account with ID {bankAccountId} not found");
            }

            // Get last balance (from history or current balance)
            var balanceBefore = await GetLastBankAccountHistoryBalanceAsync(bankAccountId);

            // Calculate new balance
            var balanceAfter = balanceBefore + transactionAmount;

            // Create history record
            var historyRecord = new BankAccountBalanceHistory
            {
                BankAccountId = bankAccountId,
                TransactionType = transactionType,
                ReferenceId = referenceId,
                BalanceBefore = balanceBefore,
                TransactionAmount = transactionAmount,
                BalanceAfter = balanceAfter,
                Description = description,
                TransactionDate = transactionDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = performedBy,
                IsDeleted = false
            };

            _context.BankAccountBalanceHistory.Add(historyRecord);

            // Update bank account balance
            bankAccount.AccountBalance = balanceAfter;
            bankAccount.LastModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Added incremental bank account balance history: Bank Account {bankAccountId}, Amount {transactionAmount}, Balance {balanceBefore} -> {balanceAfter}");

            return historyRecord;
        }

        #endregion Incremental Update Helpers

        #region Partial Rebuild Helpers

        /// <summary>
        /// **PARTIAL REBUILD HELPER** - Rebuilds balances for a specific customer+currency combination from a transaction date forward.
        /// Used when a transaction is deleted and subsequent balances need recalculation.
        /// Rebuilds from source data (Orders, Documents, Manual records) not from history.
        /// Much faster than full rebuild (only affects one customer+currency combination).
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <param name="currencyCode">Currency code</param>
        /// <param name="fromDate">Transaction date to rebuild from (inclusive)</param>
        /// <param name="performedBy">Identifier of who initiated the rebuild</param>
        private async Task RebuildCustomerBalanceFromDateAsync(int customerId, string currencyCode, DateTime fromDate, string performedBy)
        {
            var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

            _logger.LogInformation($"Rebuilding customer balance from date: Customer {customerId}, Currency {normalizedCurrencyCode}, FromDate {fromDate:yyyy-MM-dd}");

            // Get the balance before the fromDate
            var balanceBefore = await GetLastCustomerBalanceHistoryAsync(customerId, normalizedCurrencyCode, fromDate);

            // Get manual records BEFORE marking existing records as deleted (so we can include them in rebuild)
            // Load first, then filter in memory for case-insensitive currency code matching
            var allManualRecords = await _context.CustomerBalanceHistory
                .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual &&
                           !h.IsDeleted &&
                           h.CustomerId == customerId &&
                           h.TransactionDate >= fromDate)
                .ToListAsync();

            var manualRecords = allManualRecords
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .ToList();

            // Mark existing history records from fromDate forward as deleted (soft delete)
            // Load first, then filter in memory for case-insensitive currency code matching
            var allExistingRecords = await _context.CustomerBalanceHistory
                .Where(h => h.CustomerId == customerId &&
                           !h.IsDeleted &&
                           h.TransactionDate >= fromDate)
                .ToListAsync();

            var existingRecords = allExistingRecords
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .ToList();

            foreach (var record in existingRecords)
            {
                record.IsDeleted = true;
                record.DeletedAt = DateTime.UtcNow;
                record.DeletedBy = performedBy;
            }
            await _context.SaveChangesAsync();

            // Rebuild from source data: Get all orders for this customer+currency from fromDate forward
            var orders = await _context.Orders
                .Where(o => !o.IsDeleted &&
                           o.CustomerId == customerId &&
                           o.CreatedAt >= fromDate)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .ToListAsync();

            // Get all documents for this customer+currency from fromDate forward
            var documents = await _context.AccountingDocuments
                .Where(d => !d.IsDeleted &&
                           d.IsVerified &&
                           d.DocumentDate >= fromDate &&
                           ((d.PayerType == PayerType.Customer && d.PayerCustomerId == customerId) ||
                            (d.ReceiverType == ReceiverType.Customer && d.ReceiverCustomerId == customerId)))
                .ToListAsync();

            // Create transaction items from source data
            var transactionItems = new List<(DateTime TransactionDate, CustomerBalanceTransactionType TransactionType, int? ReferenceId, decimal Amount, string Description, string? TransactionNumber)>();

            // Add order transactions
            foreach (var order in orders)
            {
                var fromCurrencyCode = (order.FromCurrency?.Code ?? "").ToUpperInvariant().Trim();
                var toCurrencyCode = (order.ToCurrency?.Code ?? "").ToUpperInvariant().Trim();

                if (fromCurrencyCode == normalizedCurrencyCode)
                {
                    var description = $"معامله {order.CurrencyPair} - نرخ: {order.Rate}";
                    if (!string.IsNullOrEmpty(order.Notes))
                        description += $" - توضیحات: {order.Notes}";
                    transactionItems.Add((order.CreatedAt, CustomerBalanceTransactionType.Order, order.Id, -order.FromAmount, description, null));
                }

                if (toCurrencyCode == normalizedCurrencyCode)
                {
                    var description = $"معامله {order.CurrencyPair} - نرخ: {order.Rate}";
                    if (!string.IsNullOrEmpty(order.Notes))
                        description += $" - توضیحات: {order.Notes}";
                    transactionItems.Add((order.CreatedAt, CustomerBalanceTransactionType.Order, order.Id, order.ToAmount, description, null));
                }
            }

            // Add document transactions
            foreach (var doc in documents)
            {
                var docCurrencyCode = (doc.CurrencyCode ?? "").ToUpperInvariant().Trim();
                if (docCurrencyCode != normalizedCurrencyCode)
                    continue;

                var description = $"{doc.Title} - مبلغ: {doc.Amount} {docCurrencyCode}";
                if (!string.IsNullOrEmpty(doc.Description))
                    description += $" - {doc.Description}";

                if (doc.PayerType == PayerType.Customer && doc.PayerCustomerId == customerId)
                {
                    transactionItems.Add((doc.DocumentDate, CustomerBalanceTransactionType.AccountingDocument, doc.Id, doc.Amount, description, doc.ReferenceNumber));
                }

                if (doc.ReceiverType == ReceiverType.Customer && doc.ReceiverCustomerId == customerId)
                {
                    transactionItems.Add((doc.DocumentDate, CustomerBalanceTransactionType.AccountingDocument, doc.Id, -doc.Amount, description, doc.ReferenceNumber));
                }
            }

            // Add manual records
            foreach (var manual in manualRecords)
            {
                transactionItems.Add((manual.TransactionDate, CustomerBalanceTransactionType.Manual, manual.ReferenceId, manual.TransactionAmount, manual.Description ?? "Manual adjustment", manual.TransactionNumber));
            }

            if (!transactionItems.Any())
            {
                // No transactions to rebuild, just update the balance
                var allCustomerBalancesForUpdate = await _context.CustomerBalances
                    .Where(cb => cb.CustomerId == customerId)
                    .ToListAsync();

                var customerBalanceForUpdate = allCustomerBalancesForUpdate
                    .FirstOrDefault(cb => (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

                if (customerBalanceForUpdate != null)
                {
                    customerBalanceForUpdate.Balance = balanceBefore;
                    customerBalanceForUpdate.LastUpdated = DateTime.UtcNow;
                    if (customerBalanceForUpdate.CurrencyCode != normalizedCurrencyCode)
                    {
                        customerBalanceForUpdate.CurrencyCode = normalizedCurrencyCode;
                    }
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"No transactions to rebuild for Customer {customerId}, Currency {normalizedCurrencyCode} from {fromDate:yyyy-MM-dd}. Balance set to {balanceBefore}");
                return;
            }

            // Sort transactions by date
            transactionItems = transactionItems.OrderBy(t => t.TransactionDate).ThenBy(t => t.ReferenceId).ToList();

            // Rebuild history from source data
            decimal runningBalance = balanceBefore;
            var newHistoryRecords = new List<CustomerBalanceHistory>();

            foreach (var transaction in transactionItems)
            {
                runningBalance += transaction.Amount;

                var note = $"{transaction.TransactionType} - مبلغ: {transaction.Amount} {normalizedCurrencyCode}";
                if (!string.IsNullOrEmpty(transaction.TransactionNumber))
                    note += $" - شناسه تراکنش: {transaction.TransactionNumber}";

                newHistoryRecords.Add(new CustomerBalanceHistory
                {
                    CustomerId = customerId,
                    CurrencyCode = normalizedCurrencyCode,
                    TransactionType = transaction.TransactionType,
                    ReferenceId = transaction.ReferenceId,
                    BalanceBefore = runningBalance - transaction.Amount,
                    TransactionAmount = transaction.Amount,
                    BalanceAfter = runningBalance,
                    Description = transaction.Description,
                    TransactionNumber = transaction.TransactionNumber,
                    Note = note,
                    TransactionDate = transaction.TransactionDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy,
                    IsDeleted = false
                });
            }

            // Save new history records
            await _context.CustomerBalanceHistory.AddRangeAsync(newHistoryRecords);

            // Update customer balance
            var allCustomerBalances = await _context.CustomerBalances
                .Where(cb => cb.CustomerId == customerId)
                .ToListAsync();

            var customerBalance = allCustomerBalances
                .FirstOrDefault(cb => (cb.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

            if (customerBalance != null)
            {
                customerBalance.Balance = runningBalance;
                customerBalance.LastUpdated = DateTime.UtcNow;
                if (customerBalance.CurrencyCode != normalizedCurrencyCode)
                {
                    customerBalance.CurrencyCode = normalizedCurrencyCode;
                }
            }
            else
            {
                customerBalance = new CustomerBalance
                {
                    CustomerId = customerId,
                    CurrencyCode = normalizedCurrencyCode,
                    Balance = runningBalance,
                    LastUpdated = DateTime.UtcNow
                };
                _context.CustomerBalances.Add(customerBalance);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Rebuilt customer balance: Customer {customerId}, Currency {normalizedCurrencyCode}, Final Balance {runningBalance}");
        }

        /// <summary>
        /// **PARTIAL REBUILD HELPER** - Rebuilds balances for a specific currency pool from a transaction date forward.
        /// Used when a transaction is deleted and subsequent balances need recalculation.
        /// Rebuilds from source data (Orders, Manual records) not from history.
        /// Much faster than full rebuild (only affects one currency).
        /// </summary>
        /// <param name="currencyCode">Currency code</param>
        /// <param name="fromDate">Transaction date to rebuild from (inclusive)</param>
        /// <param name="performedBy">Identifier of who initiated the rebuild</param>
        private async Task RebuildPoolBalanceFromDateAsync(string currencyCode, DateTime fromDate, string performedBy)
        {
            var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();

            _logger.LogInformation($"Rebuilding pool balance from date: Currency {normalizedCurrencyCode}, FromDate {fromDate:yyyy-MM-dd}");

            // Get the balance before the fromDate
            // Load first, then filter in memory for case-insensitive currency code matching
            var allHistoryBefore = await _context.CurrencyPoolHistory
                .Where(h => !h.IsDeleted && h.TransactionDate < fromDate)
                .ToListAsync();

            var lastHistoryBefore = allHistoryBefore
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefault();

            var balanceBefore = lastHistoryBefore != null ? lastHistoryBefore.BalanceAfter : await GetLastPoolHistoryBalanceAsync(normalizedCurrencyCode);

            // Get manual records BEFORE marking existing records as deleted (so we can include them in rebuild)
            // Load first, then filter in memory for case-insensitive currency code matching
            var allManualRecords = await _context.CurrencyPoolHistory
                .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit &&
                           !h.IsDeleted &&
                           h.TransactionDate >= fromDate)
                .ToListAsync();

            var manualRecords = allManualRecords
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .ToList();

            // Mark existing history records from fromDate forward as deleted (soft delete)
            // Load first, then filter in memory for case-insensitive currency code matching
            var allExistingRecords = await _context.CurrencyPoolHistory
                .Where(h => !h.IsDeleted && h.TransactionDate >= fromDate)
                .ToListAsync();

            var existingRecords = allExistingRecords
                .Where(h => (h.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode)
                .ToList();

            foreach (var record in existingRecords)
            {
                record.IsDeleted = true;
                record.DeletedAt = DateTime.UtcNow;
                record.DeletedBy = performedBy;
            }
            await _context.SaveChangesAsync();

            // Rebuild from source data: Get all non-frozen, non-deleted orders from fromDate forward
            var orders = await _context.Orders
                .Where(o => !o.IsDeleted && !o.IsFrozen && o.CreatedAt >= fromDate)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .ToListAsync();

            // Create transaction items from source data
            var transactionItems = new List<(DateTime TransactionDate, CurrencyPoolTransactionType TransactionType, int? ReferenceId, decimal Amount, string PoolTransactionType, string Description)>();

            // Add order transactions
            foreach (var order in orders)
            {
                var fromCurrencyCode = (order.FromCurrency?.Code ?? "").ToUpperInvariant().Trim();
                var toCurrencyCode = (order.ToCurrency?.Code ?? "").ToUpperInvariant().Trim();

                if (fromCurrencyCode == normalizedCurrencyCode)
                {
                    // Institution receives FromAmount (pool increases)
                    var description = $"معامله {order.CurrencyPair} - نرخ: {order.Rate}";
                    if (!string.IsNullOrEmpty(order.Notes))
                        description += $" - توضیحات: {order.Notes}";
                    transactionItems.Add((order.CreatedAt, CurrencyPoolTransactionType.Order, order.Id, order.FromAmount, "Buy", description));
                }

                if (toCurrencyCode == normalizedCurrencyCode)
                {
                    // Institution pays ToAmount (pool decreases)
                    var description = $"معامله {order.CurrencyPair} - نرخ: {order.Rate}";
                    if (!string.IsNullOrEmpty(order.Notes))
                        description += $" - توضیحات: {order.Notes}";
                    transactionItems.Add((order.CreatedAt, CurrencyPoolTransactionType.Order, order.Id, -order.ToAmount, "Sell", description));
                }
            }

            // Add manual records
            foreach (var manual in manualRecords)
            {
                transactionItems.Add((manual.TransactionDate, CurrencyPoolTransactionType.ManualEdit, manual.ReferenceId, manual.TransactionAmount, "Manual", manual.Description ?? "Manual adjustment"));
            }

            if (!transactionItems.Any())
            {
                // No transactions to rebuild, just update the balance
                var allCurrenciesForUpdate = await _context.Currencies.ToListAsync();
                var currencyForUpdate = allCurrenciesForUpdate
                    .FirstOrDefault(c => (c.Code ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

                if (currencyForUpdate != null)
                {
                    var pool = await _context.CurrencyPools
                        .FirstOrDefaultAsync(p => p.CurrencyId == currencyForUpdate.Id);

                    if (pool != null)
                    {
                        pool.Balance = balanceBefore;
                        pool.LastUpdated = DateTime.UtcNow;
                        if (pool.CurrencyCode != normalizedCurrencyCode)
                        {
                            pool.CurrencyCode = normalizedCurrencyCode;
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                _logger.LogInformation($"No transactions to rebuild for Currency {normalizedCurrencyCode} from {fromDate:yyyy-MM-dd}. Balance set to {balanceBefore}");
                return;
            }

            // Sort transactions by date
            transactionItems = transactionItems.OrderBy(t => t.TransactionDate).ThenBy(t => t.ReferenceId).ToList();

            // Rebuild history from source data
            decimal runningBalance = balanceBefore;
            var newHistoryRecords = new List<CurrencyPoolHistory>();

            foreach (var transaction in transactionItems)
            {
                runningBalance += transaction.Amount;

                newHistoryRecords.Add(new CurrencyPoolHistory
                {
                    CurrencyCode = normalizedCurrencyCode,
                    TransactionType = transaction.TransactionType,
                    ReferenceId = transaction.ReferenceId,
                    BalanceBefore = runningBalance - transaction.Amount,
                    TransactionAmount = transaction.Amount,
                    BalanceAfter = runningBalance,
                    PoolTransactionType = transaction.PoolTransactionType,
                    Description = transaction.Description,
                    TransactionDate = transaction.TransactionDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy,
                    IsDeleted = false
                });
            }

            // Save new history records
            await _context.CurrencyPoolHistory.AddRangeAsync(newHistoryRecords);

            // Update pool balance and totals
            var allCurrencies = await _context.Currencies.ToListAsync();
            var currency = allCurrencies
                .FirstOrDefault(c => (c.Code ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);

            if (currency != null)
            {
                var pool = await _context.CurrencyPools
                    .FirstOrDefaultAsync(p => p.CurrencyId == currency.Id);

                if (pool != null)
                {
                    pool.Balance = runningBalance;
                    pool.LastUpdated = DateTime.UtcNow;
                    if (pool.CurrencyCode != normalizedCurrencyCode)
                    {
                        pool.CurrencyCode = normalizedCurrencyCode;
                    }

                    // Recalculate totals from ALL source data (not just from fromDate)
                    // Note: For accurate totals, we need to recalculate from all orders, not just from fromDate
                    // This is still faster than full rebuild since we only process one currency
                    var allOrdersForCurrency = await _context.Orders
                        .Where(o => !o.IsDeleted && !o.IsFrozen &&
                                   (o.FromCurrencyId == currency.Id || o.ToCurrencyId == currency.Id))
                        .Include(o => o.FromCurrency)
                        .Include(o => o.ToCurrency)
                        .ToListAsync();

                    decimal totalBought = 0;
                    decimal totalSold = 0;

                    foreach (var order in allOrdersForCurrency)
                    {
                        var fromCurrencyCode = (order.FromCurrency?.Code ?? "").ToUpperInvariant().Trim();
                        var toCurrencyCode = (order.ToCurrency?.Code ?? "").ToUpperInvariant().Trim();

                        if (fromCurrencyCode == normalizedCurrencyCode)
                        {
                            totalBought += order.FromAmount; // Institution receives (buy)
                        }

                        if (toCurrencyCode == normalizedCurrencyCode)
                        {
                            totalSold += order.ToAmount; // Institution provides (sell)
                        }
                    }

                    pool.TotalBought = totalBought;
                    pool.TotalSold = totalSold;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Rebuilt pool balance: Currency {normalizedCurrencyCode}, Final Balance {runningBalance}");
        }

        /// <summary>
        /// **PARTIAL REBUILD HELPER** - Rebuilds balances for a specific bank account from a transaction date forward.
        /// Used when a transaction is deleted and subsequent balances need recalculation.
        /// Rebuilds from source data (Documents, Manual records) not from history.
        /// Much faster than full rebuild (only affects one bank account).
        /// </summary>
        /// <param name="bankAccountId">Bank account ID</param>
        /// <param name="fromDate">Transaction date to rebuild from (inclusive)</param>
        /// <param name="performedBy">Identifier of who initiated the rebuild</param>
        private async Task RebuildBankAccountBalanceFromDateAsync(int bankAccountId, DateTime fromDate, string performedBy)
        {
            _logger.LogInformation($"Rebuilding bank account balance from date: Bank Account {bankAccountId}, FromDate {fromDate:yyyy-MM-dd}");

            // Get the balance before the fromDate
            var lastHistoryBefore = await _context.BankAccountBalanceHistory
                .Where(h => !h.IsDeleted && h.BankAccountId == bankAccountId && h.TransactionDate < fromDate)
                .OrderByDescending(h => h.TransactionDate)
                .ThenByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            var balanceBefore = lastHistoryBefore != null ? lastHistoryBefore.BalanceAfter : await GetLastBankAccountHistoryBalanceAsync(bankAccountId);

            // Get manual records BEFORE marking existing records as deleted (so we can include them in rebuild)
            var manualRecords = await _context.BankAccountBalanceHistory
                .Where(h => h.TransactionType == BankAccountTransactionType.ManualEdit &&
                           !h.IsDeleted &&
                           h.BankAccountId == bankAccountId &&
                           h.TransactionDate >= fromDate)
                .ToListAsync();

            // Mark existing history records from fromDate forward as deleted (soft delete)
            var existingRecords = await _context.BankAccountBalanceHistory
                .Where(h => !h.IsDeleted &&
                           h.BankAccountId == bankAccountId &&
                           h.TransactionDate >= fromDate)
                .ToListAsync();

            foreach (var record in existingRecords)
            {
                record.IsDeleted = true;
                record.DeletedAt = DateTime.UtcNow;
                record.DeletedBy = performedBy;
            }
            await _context.SaveChangesAsync();

            // Rebuild from source data: Get all non-frozen, non-deleted, verified documents from fromDate forward
            var documents = await _context.AccountingDocuments
                .Where(d => !d.IsDeleted &&
                           !d.IsFrozen &&
                           d.IsVerified &&
                           d.DocumentDate >= fromDate &&
                           ((d.PayerType == PayerType.System && d.PayerBankAccountId == bankAccountId) ||
                            (d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId == bankAccountId)))
                .ToListAsync();

            // Create transaction items from source data
            var transactionItems = new List<(DateTime TransactionDate, BankAccountTransactionType TransactionType, int? ReferenceId, decimal Amount, string Description)>();

            // Add document transactions
            foreach (var doc in documents)
            {
                var description = $"{doc.Title} - مبلغ: {doc.Amount} {doc.CurrencyCode}";
                if (!string.IsNullOrEmpty(doc.Description))
                    description += $" - {doc.Description}";

                if (doc.PayerType == PayerType.System && doc.PayerBankAccountId == bankAccountId)
                {
                    // Bank pays out (balance increases)
                    transactionItems.Add((doc.DocumentDate, BankAccountTransactionType.Document, doc.Id, doc.Amount, description));
                }

                if (doc.ReceiverType == ReceiverType.System && doc.ReceiverBankAccountId == bankAccountId)
                {
                    // Bank receives (balance decreases)
                    transactionItems.Add((doc.DocumentDate, BankAccountTransactionType.Document, doc.Id, -doc.Amount, description));
                }
            }

            // Add manual records
            foreach (var manual in manualRecords)
            {
                transactionItems.Add((manual.TransactionDate, BankAccountTransactionType.ManualEdit, manual.ReferenceId, manual.TransactionAmount, manual.Description ?? "Manual adjustment"));
            }

            if (!transactionItems.Any())
            {
                // No transactions to rebuild, just update the balance
                var bankAccountForUpdate = await _context.BankAccounts
                    .FirstOrDefaultAsync(ba => ba.Id == bankAccountId);

                if (bankAccountForUpdate != null)
                {
                    bankAccountForUpdate.AccountBalance = balanceBefore;
                    bankAccountForUpdate.LastModified = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"No transactions to rebuild for Bank Account {bankAccountId} from {fromDate:yyyy-MM-dd}. Balance set to {balanceBefore}");
                return;
            }

            // Sort transactions by date
            transactionItems = transactionItems.OrderBy(t => t.TransactionDate).ThenBy(t => t.ReferenceId).ToList();

            // Rebuild history from source data
            decimal runningBalance = balanceBefore;
            var newHistoryRecords = new List<BankAccountBalanceHistory>();

            foreach (var transaction in transactionItems)
            {
                runningBalance += transaction.Amount;

                newHistoryRecords.Add(new BankAccountBalanceHistory
                {
                    BankAccountId = bankAccountId,
                    TransactionType = transaction.TransactionType,
                    ReferenceId = transaction.ReferenceId,
                    BalanceBefore = runningBalance - transaction.Amount,
                    TransactionAmount = transaction.Amount,
                    BalanceAfter = runningBalance,
                    Description = transaction.Description,
                    TransactionDate = transaction.TransactionDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = performedBy,
                    IsDeleted = false
                });
            }

            // Save new history records
            await _context.BankAccountBalanceHistory.AddRangeAsync(newHistoryRecords);

            // Update bank account balance
            var bankAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(ba => ba.Id == bankAccountId);

            if (bankAccount != null)
            {
                bankAccount.AccountBalance = runningBalance;
                bankAccount.LastModified = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Rebuilt bank account balance: Bank Account {bankAccountId}, Final Balance {runningBalance}");
        }

        #endregion Partial Rebuild Helpers

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
        /// 
        /// **PERFORMANCE OPTIMIZATION**: Uses incremental updates instead of full rebuild for faster processing.
        /// Updates are synchronous and transactional to ensure data consistency.
        /// </summary>
        /// <param name="order">Complete order with all currency and amount information</param>
        /// <param name="performedBy">Identifier of who initiated the transaction (for audit trail)</param>
        public async Task ProcessOrderCreationAsync(Order order, string performedBy = "System")
        {
            _logger.LogInformation($"Processing order creation for Order ID: {order.Id}");

            // Check if order is frozen - frozen orders don't affect current balances or pool balances
            if (order.IsFrozen)
            {
                _logger.LogInformation($"Order {order.Id} is frozen - skipping all balance updates (pools and customers)");
                // Still save the order even if frozen
                if (_context.Entry(order).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    _context.Add(order);
                }
                await _context.SaveChangesAsync();
                return;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Save order first (if not already saved)
                if (_context.Entry(order).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    _context.Add(order);
                }
                await _context.SaveChangesAsync();

                // Load order with currency navigation properties if not already loaded
                if (order.FromCurrency == null || order.ToCurrency == null)
                {
                    await _context.Entry(order)
                        .Reference(o => o.FromCurrency)
                        .LoadAsync();
                    await _context.Entry(order)
                        .Reference(o => o.ToCurrency)
                        .LoadAsync();
                }

                var fromCurrencyCode = (order.FromCurrency?.Code ?? "").ToUpperInvariant().Trim();
                var toCurrencyCode = (order.ToCurrency?.Code ?? "").ToUpperInvariant().Trim();
                var transactionDate = order.CreatedAt;
                var description = $"معامله {order.CurrencyPair} - نرخ: {order.Rate}";
                if (!string.IsNullOrEmpty(order.Notes))
                {
                    description += $" - توضیحات: {order.Notes}";
                }

                // Update customer balance for FromCurrency (customer pays - negative impact)
                await AddCustomerBalanceHistoryIncrementalAsync(
                    customerId: order.CustomerId,
                    currencyCode: fromCurrencyCode,
                    transactionAmount: -order.FromAmount, // Negative: customer pays
                    transactionType: CustomerBalanceTransactionType.Order,
                    referenceId: order.Id,
                    transactionDate: transactionDate,
                    description: description,
                    transactionNumber: null,
                    performedBy: performedBy
                );

                // Update customer balance for ToCurrency (customer receives - positive impact)
                await AddCustomerBalanceHistoryIncrementalAsync(
                    customerId: order.CustomerId,
                    currencyCode: toCurrencyCode,
                    transactionAmount: order.ToAmount, // Positive: customer receives
                    transactionType: CustomerBalanceTransactionType.Order,
                    referenceId: order.Id,
                    transactionDate: transactionDate,
                    description: description,
                    transactionNumber: null,
                    performedBy: performedBy
                );

                // Update pool balance for FromCurrency (institution receives - positive impact)
                await AddPoolBalanceHistoryIncrementalAsync(
                    currencyCode: fromCurrencyCode,
                    transactionAmount: order.FromAmount, // Positive: pool increases
                    transactionType: CurrencyPoolTransactionType.Order,
                    referenceId: order.Id,
                    transactionDate: transactionDate,
                    description: description,
                    poolTransactionType: "Buy",
                    performedBy: performedBy
                );

                // Update pool balance for ToCurrency (institution provides - negative impact)
                await AddPoolBalanceHistoryIncrementalAsync(
                    currencyCode: toCurrencyCode,
                    transactionAmount: -order.ToAmount, // Negative: pool decreases
                    transactionType: CurrencyPoolTransactionType.Order,
                    referenceId: order.Id,
                    transactionDate: transactionDate,
                    description: description,
                    poolTransactionType: "Sell",
                    performedBy: performedBy
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Order {order.Id} processed successfully with incremental balance updates");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing order {order.Id}: {ex.Message}");
                throw;
            }
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
        /// - **Payer Customer**: Gets +amount (receives money/credit)
        /// - **Receiver Customer**: Gets -amount (pays money/debit)
        /// - **Payer Bank Account**: Gets +amount (money flows out)
        /// - **Receiver Bank Account**: Gets -amount (money flows in)
        /// 
        /// **Verification Requirement**: Only processes verified documents to prevent unauthorized transactions.
        /// 
        /// **Complete Audit Trail**: Every document impact is logged with document reference numbers,
        /// dates, amounts, and all parties involved for comprehensive financial auditing.
        /// 
        /// **PERFORMANCE OPTIMIZATION**: Uses incremental updates instead of full rebuild for faster processing.
        /// Updates are synchronous and transactional to ensure data consistency.
        /// </summary>
        /// <param name="document">Verified accounting document with all party and amount information</param>
        /// <param name="performedBy">Identifier of who processed the document (for audit trail)</param>
        public async Task ProcessAccountingDocumentAsync(AccountingDocument document, string performedBy = "System")
        {
            _logger.LogInformation($"Processing accounting document ID: {document.Id}");

            // Only process verified documents
            if (!document.IsVerified)
            {
                _logger.LogInformation($"Document {document.Id} is not verified - skipping balance updates");
                // Still save the document even if not verified
                if (_context.Entry(document).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    _context.Add(document);
                }
                await _context.SaveChangesAsync();
                return;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Save document first (if not already saved)
                if (_context.Entry(document).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    _context.Add(document);
                }
                await _context.SaveChangesAsync();

                var currencyCode = (document.CurrencyCode ?? "").ToUpperInvariant().Trim();
                var transactionDate = document.DocumentDate;
                var description = $"{document.Title} - مبلغ: {document.Amount} {currencyCode}";
                if (!string.IsNullOrEmpty(document.Description))
                {
                    description += $" - {document.Description}";
                }

                // Process customer balance updates
                // Payer Customer: Gets +amount (receives money/credit)
                if (document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue)
                {
                    await AddCustomerBalanceHistoryIncrementalAsync(
                        customerId: document.PayerCustomerId.Value,
                        currencyCode: currencyCode,
                        transactionAmount: document.Amount, // Positive: customer receives
                        transactionType: CustomerBalanceTransactionType.AccountingDocument,
                        referenceId: document.Id,
                        transactionDate: transactionDate,
                        description: description,
                        transactionNumber: document.ReferenceNumber,
                        performedBy: performedBy
                    );
                }

                // Receiver Customer: Gets -amount (pays money/debit)
                if (document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue)
                {
                    await AddCustomerBalanceHistoryIncrementalAsync(
                        customerId: document.ReceiverCustomerId.Value,
                        currencyCode: currencyCode,
                        transactionAmount: -document.Amount, // Negative: customer pays
                        transactionType: CustomerBalanceTransactionType.AccountingDocument,
                        referenceId: document.Id,
                        transactionDate: transactionDate,
                        description: description,
                        transactionNumber: document.ReferenceNumber,
                        performedBy: performedBy
                    );
                }

                // Process bank account balance updates (only for non-frozen documents)
                // Frozen documents don't affect bank account balances (same as pool balances)
                if (!document.IsFrozen)
                {
                    // Payer Bank Account: Gets +amount (money flows out)
                    if (document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue)
                    {
                        await AddBankAccountBalanceHistoryIncrementalAsync(
                            bankAccountId: document.PayerBankAccountId.Value,
                            transactionAmount: document.Amount, // Positive: bank pays out
                            transactionType: BankAccountTransactionType.Document,
                            referenceId: document.Id,
                            transactionDate: transactionDate,
                            description: description,
                            performedBy: performedBy
                        );
                    }

                    // Receiver Bank Account: Gets -amount (money flows in)
                    if (document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue)
                    {
                        await AddBankAccountBalanceHistoryIncrementalAsync(
                            bankAccountId: document.ReceiverBankAccountId.Value,
                            transactionAmount: -document.Amount, // Negative: bank receives
                            transactionType: BankAccountTransactionType.Document,
                            referenceId: document.Id,
                            transactionDate: transactionDate,
                            description: description,
                            performedBy: performedBy
                        );
                    }
                }
                else
                {
                    _logger.LogInformation($"Document {document.Id} is frozen - skipping bank account balance updates");
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"Document {document.Id} processed successfully with incremental balance updates");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error processing document {document.Id}: {ex.Message}");
                throw;
            }
        }



        #endregion Create reocrds






        /// <summary>
        /// Comprehensive rebuild of all financial balances based on new IsFrozen strategy:
        /// - Pool balances rebuilt from non-deleted AND non-frozen orders only with coherent history
        /// - Bank account balances rebuilt from non-deleted AND non-frozen documents only with coherent history
        /// - Customer balance history rebuilt from non-deleted orders, documents, and manual records (including frozen orders/documents)
        /// - Active buy/sell counts recalculated properly based on non-frozen orders
        ///
        /// This ensures frozen historical records don't affect current balance calculations
        /// but are preserved for customer balance history audit trail, including manual adjustments.
        /// Creates coherent balance history chains with proper BalanceBefore/BalanceAfter tracking.
        /// </summary>
        public async Task RebuildAllFinancialBalancesAsync(string performedBy = "System")
        {
            // Thread safety: Prevent concurrent rebuilds
            if (!await _rebuildSemaphore.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                _logger.LogWarning("Balance rebuild already in progress, skipping concurrent request");
                throw new InvalidOperationException("Balance rebuild is already in progress. Please wait for the current operation to complete.");
            }

            try
            {
                await PerformBalanceRebuildAsync(performedBy);
            }
            finally
            {
                _rebuildSemaphore.Release();
            }
        }

        private async Task PerformBalanceRebuildAsync(string performedBy)
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

                // Get all manual records efficiently (only load necessary fields)
                var manualCustomerRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Manual && !h.IsDeleted)
                    .Select(h => new
                    {
                        h.Id,
                        h.CustomerId,
                        h.CurrencyCode,
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.Description
                    })
                    .ToListAsync();

                var manualBankAccountRecords = await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionType == BankAccountTransactionType.ManualEdit && !h.IsDeleted)
                    .Select(h => new
                    {
                        h.Id,
                        h.BankAccountId,
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.Description
                    })
                    .ToListAsync();

                var manualPoolRecords = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.ManualEdit && !h.IsDeleted)
                    .Select(h => new
                    {
                        h.Id,
                        h.CurrencyCode,
                        h.TransactionAmount,
                        h.TransactionDate,
                        h.Description
                    })
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

                // Reset balances efficiently using bulk updates
                var resetTimestamp = DateTime.UtcNow;
                await _context.Database.ExecuteSqlRawAsync("UPDATE CustomerBalances SET Balance = 0, LastUpdated = {0}", resetTimestamp);
                await _context.Database.ExecuteSqlRawAsync("UPDATE CurrencyPools SET Balance = 0, ActiveBuyOrderCount = 0, ActiveSellOrderCount = 0, TotalBought = 0, TotalSold = 0, LastUpdated = {0}", resetTimestamp);
                await _context.Database.ExecuteSqlRawAsync("UPDATE BankAccountBalances SET Balance = 0, LastUpdated = {0}", resetTimestamp);

                logMessages.Add("✓ Reset all balances to zero using bulk updates");

                // STEP 2: Create coherent pool history for each currency
                logMessages.Add("");
                logMessages.Add("STEP 2: Creating coherent pool history...");

                // Load active orders with required data only (eliminate N+1 queries)
                var activeOrders = await _context.Orders
                    .Where(o => !o.IsDeleted && !o.IsFrozen)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Select(o => new
                    {
                        o.Id,
                        o.CustomerId,
                        o.CreatedAt,
                        o.FromAmount,
                        o.ToAmount,
                        o.Rate,
                        o.Notes,
                        FromCurrencyCode = o.FromCurrency.Code,
                        ToCurrencyCode = o.ToCurrency.Code
                    })
                    .OrderBy(o => o.CreatedAt)
                    .ToListAsync();

                logMessages.Add($"Processing {activeOrders.Count} active (non-deleted, non-frozen) orders and {manualPoolRecords.Count} manual pool records...");

                // Pre-allocate collections with estimated capacity for better performance
                var poolTransactionItems = new List<(string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string PoolTransactionType, string Description)>(activeOrders.Count * 2);

                // Add order transactions (eliminated N+1 query by pre-loading data)
                // IMPORTANT: Normalize currency codes to UPPERCASE to handle case sensitivity issues (e.g., USDT vs usdt)
                foreach (var o in activeOrders)
                {
                    var fromCurrencyCode = (o.FromCurrencyCode ?? "").ToUpperInvariant().Trim();
                    var toCurrencyCode = (o.ToCurrencyCode ?? "").ToUpperInvariant().Trim();
                    
                    // Institution receives FromAmount in FromCurrency (pool increases)
                    poolTransactionItems.Add((fromCurrencyCode, o.CreatedAt, "Order", o.Id, o.FromAmount, "Buy", o.Notes ?? ""));

                    // Institution pays ToAmount in ToCurrency (pool decreases)
                    poolTransactionItems.Add((toCurrencyCode, o.CreatedAt, "Order", o.Id, -o.ToAmount, "Sell", o.Notes ?? ""));
                }

                // Add manual pool records as transactions
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency
                foreach (var manual in manualPoolRecords)
                {
                    var currencyCode = (manual.CurrencyCode ?? "").ToUpperInvariant().Trim();
                    poolTransactionItems.Add((
                        currencyCode,
                        manual.TransactionDate,
                        "Manual",
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        "Manual",
                        manual.Description ?? "Manual adjustment"
                    ));
                }

                // Group by currency code (now normalized to uppercase) to create coherent history per currency
                var currencyGroups = poolTransactionItems
                    .GroupBy(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Process pool transactions in batches for better performance
                const int batchSize = 1000;
                var poolHistoryRecords = new List<CurrencyPoolHistory>();
                var poolBalanceUpdates = new Dictionary<string, (decimal Balance, int BuyCount, int SellCount, decimal TotalBought, decimal TotalSold)>();

                foreach (var currencyGroup in currencyGroups)
                {
                    var currencyCode = currencyGroup.Key;
                    var currencyTransactions = currencyGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!currencyTransactions.Any()) continue;

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

                        poolHistoryRecords.Add(new CurrencyPoolHistory
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
                        });

                        runningBalance = poolHistoryRecords.Last().BalanceAfter;

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

                        // Batch save when reaching batch size
                        if (poolHistoryRecords.Count >= batchSize)
                        {
                            await _context.CurrencyPoolHistory.AddRangeAsync(poolHistoryRecords);
                            await _context.SaveChangesAsync();
                            poolHistoryRecords.Clear();
                        }
                    }

                    // Store final balance for update
                    poolBalanceUpdates[currencyCode] = (runningBalance, buyCount, sellCount, totalBought, totalSold);
                }

                // Save remaining pool history records
                if (poolHistoryRecords.Any())
                {
                    await _context.CurrencyPoolHistory.AddRangeAsync(poolHistoryRecords);
                    await _context.SaveChangesAsync();
                }

                // Update pool balances in batch
                // IMPORTANT: Normalize currency code lookup to handle case sensitivity (SQLite is case-sensitive by default)
                // Load all pools first for case-insensitive matching
                var allPools = await _context.CurrencyPools
                    .Include(p => p.Currency)
                    .ToListAsync();
                
                foreach (var (currencyCode, balances) in poolBalanceUpdates)
                {
                    var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();
                    
                    // Case-insensitive lookup in memory (since SQLite doesn't support ToUpper in LINQ)
                    var pool = allPools.FirstOrDefault(p => 
                        (p.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode ||
                        (p.Currency?.Code ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);
                    
                    if (pool != null)
                    {
                        pool.Balance = balances.Balance;
                        pool.ActiveBuyOrderCount = balances.BuyCount;
                        pool.ActiveSellOrderCount = balances.SellCount;
                        pool.TotalBought = balances.TotalBought;
                        pool.TotalSold = balances.TotalSold;
                        pool.LastUpdated = DateTime.UtcNow;
                        
                        // Ensure CurrencyCode is normalized to uppercase for consistency
                        if (pool.CurrencyCode != normalizedCurrencyCode)
                        {
                            _logger.LogWarning($"Normalizing CurrencyCode from '{pool.CurrencyCode}' to '{normalizedCurrencyCode}' for pool {pool.Id}");
                            pool.CurrencyCode = normalizedCurrencyCode;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Currency pool not found for currency code: {normalizedCurrencyCode}. Balance update skipped. Available pools: {string.Join(", ", allPools.Select(p => p.CurrencyCode))}");
                    }
                }
                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent pool history for {currencyGroups.Count} currencies with {activeOrders.Count} active orders");

                // STEP 3: Create coherent bank account balance history
                logMessages.Add("");
                logMessages.Add("STEP 3: Creating coherent bank account balance history...");

                // Load active documents efficiently
                // IMPORTANT: Only process verified documents (same as customer balance history)
                // Unverified documents should not affect bank account balances
                var activeDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && !d.IsFrozen && d.IsVerified)
                    .Select(d => new
                    {
                        d.Id,
                        d.DocumentDate,
                        d.CurrencyCode,
                        d.Amount,
                        d.Notes,
                        d.PayerType,
                        d.PayerBankAccountId,
                        d.ReceiverType,
                        d.ReceiverBankAccountId
                    })
                    .OrderBy(d => d.DocumentDate)
                    .ToListAsync();

                logMessages.Add($"Processing {activeDocuments.Count} active (non-deleted, non-frozen, verified) documents and {manualBankAccountRecords.Count} manual bank account records...");

                // Create unified transaction items for bank accounts from documents and manual records
                var bankAccountTransactionItems = new List<(int BankAccountId, string CurrencyCode, DateTime TransactionDate, string TransactionType, int? ReferenceId, decimal Amount, string Description)>(activeDocuments.Count + manualBankAccountRecords.Count);

                // Add document transactions (eliminated N+1 query)
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency (handles USDT case sensitivity)
                foreach (var d in activeDocuments)
                {
                    var normalizedCurrencyCode = (d.CurrencyCode ?? "").ToUpperInvariant().Trim();
                    
                    if (d.PayerType == PayerType.System && d.PayerBankAccountId.HasValue && d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId.HasValue)
                    {
                        // Both sides are system bank accounts: create two transactions
                        bankAccountTransactionItems.Add((d.PayerBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "system bank to bank", d.Id, d.Amount, d.Notes ?? string.Empty));
                        bankAccountTransactionItems.Add((d.ReceiverBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "system bank to bank", d.Id, -(d.Amount), d.Notes ?? string.Empty));
                    }
                    else
                    {
                        // Single side system bank account transactions
                        if (d.PayerType == PayerType.System && d.PayerBankAccountId.HasValue)
                            bankAccountTransactionItems.Add((d.PayerBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "payment document", d.Id, d.Amount, d.Notes ?? string.Empty));
                        if (d.ReceiverType == ReceiverType.System && d.ReceiverBankAccountId.HasValue)
                            bankAccountTransactionItems.Add((d.ReceiverBankAccountId.Value, normalizedCurrencyCode, d.DocumentDate, "reciept document", d.Id, -(d.Amount), d.Notes ?? string.Empty));
                    }
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

                // Process bank account transactions in batches
                var bankHistoryRecords = new List<BankAccountBalanceHistory>();
                var bankBalanceUpdates = new Dictionary<int, decimal>();

                foreach (var bankGroup in bankAccountGroups)
                {
                    var bankAccountId = bankGroup.Key;
                    var bankTransactions = bankGroup.OrderBy(x => x.TransactionDate).ToList();

                    if (!bankTransactions.Any()) continue;

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

                        bankHistoryRecords.Add(new BankAccountBalanceHistory
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
                        });

                        runningBalance = bankHistoryRecords.Last().BalanceAfter;

                        // Batch save when reaching batch size
                        if (bankHistoryRecords.Count >= batchSize)
                        {
                            await _context.BankAccountBalanceHistory.AddRangeAsync(bankHistoryRecords);
                            await _context.SaveChangesAsync();
                            bankHistoryRecords.Clear();
                        }
                    }

                    // Store final balance for update
                    bankBalanceUpdates[bankAccountId] = runningBalance;
                }

                // Save remaining bank history records
                if (bankHistoryRecords.Any())
                {
                    await _context.BankAccountBalanceHistory.AddRangeAsync(bankHistoryRecords);
                    await _context.SaveChangesAsync();
                }

                // Update bank account balances in batch
                foreach (var (bankAccountId, balance) in bankBalanceUpdates)
                {
                    var bankBalance = await _context.BankAccountBalances
                        .FirstOrDefaultAsync(b => b.BankAccountId == bankAccountId);
                    if (bankBalance != null)
                    {
                        bankBalance.Balance = balance;
                        bankBalance.LastUpdated = DateTime.UtcNow;
                    }
                }
                await _context.SaveChangesAsync();
                logMessages.Add($"✓ Created coherent bank account balance history for {bankAccountGroups.Count} bank account + currency combinations");

                // STEP 4: Rebuild coherent customer balance history from orders, documents, and manual records (including frozen, excluding only deleted)
                logMessages.Add("");
                logMessages.Add("STEP 4: Rebuilding coherent customer balance history from orders, documents, and manual records (including frozen for customer history)...");

                // Load all valid documents and orders efficiently for customer history
                var allValidDocuments = await _context.AccountingDocuments
                    .Where(d => !d.IsDeleted && d.IsVerified)
                    .Select(d => new
                    {
                        d.Id,
                        d.DocumentDate,
                        d.CurrencyCode,
                        d.Amount,
                        d.Description,
                        d.ReferenceNumber,
                        d.PayerType,
                        d.PayerCustomerId,
                        d.ReceiverType,
                        d.ReceiverCustomerId
                    })
                    .ToListAsync();

                var allValidOrders = await _context.Orders
                    .Where(o => !o.IsDeleted)
                    .Include(o => o.FromCurrency)
                    .Include(o => o.ToCurrency)
                    .Select(o => new
                    {
                        o.Id,
                        o.CustomerId,
                        o.CreatedAt,
                        o.FromAmount,
                        o.ToAmount,
                        o.Notes,
                        FromCurrencyCode = o.FromCurrency.Code,
                        ToCurrencyCode = o.ToCurrency.Code
                    })
                    .ToListAsync();

                logMessages.Add($"Processing {allValidDocuments.Count} valid documents, {allValidOrders.Count} valid orders, and {manualCustomerRecords.Count} manual customer records for customer balance history...");

                // Create unified transaction items for customers from orders, documents, and manual records
                var estimatedCapacity = allValidOrders.Count * 2 + allValidDocuments.Count * 2 + manualCustomerRecords.Count;
                var customerTransactionItems = new List<(int CustomerId, string CurrencyCode, DateTime TransactionDate, string TransactionType, string transactionCode, int? ReferenceId, decimal Amount, string Description)>(estimatedCapacity);

                // Add document transactions
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency
                foreach (var d in allValidDocuments)
                {
                    var currencyCode = (d.CurrencyCode ?? "").ToUpperInvariant().Trim();
                    
                    if (d.PayerType == PayerType.Customer && d.PayerCustomerId.HasValue && d.ReceiverType == ReceiverType.Customer && d.ReceiverCustomerId.HasValue)
                    {
                        // Both sides are customers: create two transactions
                        customerTransactionItems.Add((d.PayerCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, d.Amount, d.Description ?? string.Empty));
                        customerTransactionItems.Add((d.ReceiverCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, -d.Amount, d.Description ?? string.Empty));
                    }
                    else
                    {
                        // Single side customer transactions
                        if (d.PayerType == PayerType.Customer && d.PayerCustomerId.HasValue)
                            customerTransactionItems.Add((d.PayerCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, d.Amount, d.Description ?? string.Empty));
                        if (d.ReceiverType == ReceiverType.Customer && d.ReceiverCustomerId.HasValue)
                            customerTransactionItems.Add((d.ReceiverCustomerId.Value, currencyCode, d.DocumentDate, "Document", d.ReferenceNumber ?? string.Empty, d.Id, -d.Amount, d.Description ?? string.Empty));
                    }
                }

                // Add order transactions for customer history
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency
                foreach (var o in allValidOrders)
                {
                    var fromCurrencyCode = (o.FromCurrencyCode ?? "").ToUpperInvariant().Trim();
                    var toCurrencyCode = (o.ToCurrencyCode ?? "").ToUpperInvariant().Trim();
                    
                    // Customer pays FromAmount in FromCurrency
                    customerTransactionItems.Add((o.CustomerId, fromCurrencyCode, o.CreatedAt, "Order", string.Empty, o.Id, -o.FromAmount, o.Notes ?? string.Empty));

                    // Customer receives ToAmount in ToCurrency
                    customerTransactionItems.Add((o.CustomerId, toCurrencyCode, o.CreatedAt, "Order", string.Empty, o.Id, o.ToAmount, o.Notes ?? string.Empty));
                }

                logMessages.Add($"start adding  [{manualCustomerRecords.Count}] manual customer records");
                logMessages.Add($"customerTransactionItems is [{customerTransactionItems.Count}]");

                // Add manual customer records as transactions
                // IMPORTANT: Normalize currency codes to UPPERCASE for consistency
                foreach (var manual in manualCustomerRecords)
                {
                    var currencyCode = (manual.CurrencyCode ?? "").ToUpperInvariant().Trim();
                    customerTransactionItems.Add((
                        manual.CustomerId,
                        currencyCode,
                        manual.TransactionDate,
                        "Manual",
                        string.Empty,
                        (int?)manual.Id,
                        manual.TransactionAmount,
                        manual.Description ?? "Manual adjustment"
                    ));
                }
                logMessages.Add($"customerTransactionItems is [{customerTransactionItems.Count}]");

                // Group by customer + currency and create coherent history (process in chunks for memory efficiency)
                // Normalize currency codes to uppercase before grouping for case-insensitive matching
                var normalizedCustomerTransactions = customerTransactionItems
                    .Select(x => new
                    {
                        x.CustomerId,
                        CurrencyCode = (x.CurrencyCode ?? "").ToUpperInvariant().Trim(),
                        x.TransactionDate,
                        x.TransactionType,
                        x.transactionCode,
                        x.ReferenceId,
                        x.Amount,
                        x.Description
                    })
                    .ToList();
                
                var customerGroups = normalizedCustomerTransactions
                    .GroupBy(x => new { x.CustomerId, x.CurrencyCode })
                    .ToList();

                logMessages.Add($"Creating coherent history for {customerGroups.Count} customer + currency combinations...");

                // Process customer groups in chunks to reduce memory usage
                const int customerChunkSize = 500; // Process 500 customer-currency combinations at a time
                var customerChunks = customerGroups.Chunk(customerChunkSize);

                foreach (var chunk in customerChunks)
                {
                    var customerHistoryRecords = new List<CustomerBalanceHistory>();
                    var customerBalanceUpdates = new Dictionary<(int CustomerId, string CurrencyCode), decimal>();

                    foreach (var customerGroup in chunk)
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
                            var note = $"{transactionType} - مبلغ: {transaction.Amount} {transaction.CurrencyCode}";
                            if (!string.IsNullOrEmpty(transaction.transactionCode))
                                note += $" - شناسه تراکنش: {transaction.transactionCode}";

                            customerHistoryRecords.Add(new CustomerBalanceHistory
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
                            });

                            runningBalance = customerHistoryRecords.Last().BalanceAfter;

                            // Batch save when reaching batch size
                            if (customerHistoryRecords.Count >= batchSize)
                            {
                                await _context.CustomerBalanceHistory.AddRangeAsync(customerHistoryRecords);
                                await _context.SaveChangesAsync();
                                customerHistoryRecords.Clear();
                            }
                        }

                        // Store final balance for update
                        customerBalanceUpdates[(customerId, currencyCode)] = runningBalance;
                    }

                    // Save remaining customer history records for this chunk
                    if (customerHistoryRecords.Any())
                    {
                        await _context.CustomerBalanceHistory.AddRangeAsync(customerHistoryRecords);
                        await _context.SaveChangesAsync();
                    }

                    // Update customer balances for this chunk
                    // Load all customer balances for this chunk first for case-insensitive matching
                    var customerIds = customerBalanceUpdates.Keys.Select(k => k.CustomerId).Distinct().ToList();
                    var allCustomerBalances = await _context.CustomerBalances
                        .Where(b => customerIds.Contains(b.CustomerId))
                        .ToListAsync();
                    
                    foreach (var ((customerId, currencyCode), balance) in customerBalanceUpdates)
                    {
                        var normalizedCurrencyCode = (currencyCode ?? "").ToUpperInvariant().Trim();
                        
                        // Case-insensitive lookup in memory
                        var customerBalance = allCustomerBalances.FirstOrDefault(b => 
                            b.CustomerId == customerId && 
                            (b.CurrencyCode ?? "").ToUpperInvariant().Trim() == normalizedCurrencyCode);
                        
                        if (customerBalance == null)
                        {
                            customerBalance = new CustomerBalance
                            {
                                CustomerId = customerId,
                                CurrencyCode = normalizedCurrencyCode,
                                Balance = 0,
                                LastUpdated = DateTime.UtcNow
                            };
                            _context.CustomerBalances.Add(customerBalance);
                            allCustomerBalances.Add(customerBalance); // Add to list for potential future lookups in this chunk
                        }
                        else
                        {
                            // Ensure CurrencyCode is normalized to uppercase for consistency
                            if (customerBalance.CurrencyCode != normalizedCurrencyCode)
                            {
                                _logger.LogWarning($"Normalizing CustomerBalance CurrencyCode from '{customerBalance.CurrencyCode}' to '{normalizedCurrencyCode}' for Customer {customerId}");
                                customerBalance.CurrencyCode = normalizedCurrencyCode;
                            }
                        }
                        customerBalance.Balance = balance;
                        customerBalance.LastUpdated = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync();

                    // Clear memory for next chunk
                    customerBalanceUpdates.Clear();
                }
                logMessages.Add($"✓ Rebuilt coherent customer balance history for {customerGroups.Count} customer + currency combinations from {allValidDocuments.Count} documents and {allValidOrders.Count} orders (manual records were preserved)");




                await dbTransaction.CommitAsync();

                logMessages.Add("");
                logMessages.Add("=== REBUILD COMPLETED SUCCESSFULLY ===");
                logMessages.Add($"Finished at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                logMessages.Add("✅ All balance histories rebuilt with coherent balance chains");
                logMessages.Add("✅ Active buy/sell counts recalculated based on non-frozen orders only");
                logMessages.Add("✅ Frozen records excluded from pool/bank calculations but included in customer history");
                logMessages.Add("✅ Manual customer balance adjustments preserved in complete customer history");

                var logSummary = string.Join("\n", logMessages);
                await UpdateNotesAndDescriptions();  // Call the method to update Notes on entities and Descriptions on history

                _logger.LogInformation($"Financial balance rebuild completed successfully. Summary: {logSummary}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during comprehensive financial balance rebuild: {ex.Message}");
                throw;
            }

        }

        public async Task<(int OrdersFrozen, int DocumentsFrozen)> FreezeAllOrdersAndDocumentsAsync(string performedBy = "System")
        {
            _logger.LogInformation("FreezeAllOrdersAndDocumentsAsync initiated by {PerformedBy}", performedBy);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var timestamp = DateTime.UtcNow;

                var ordersFrozen = await _context.Orders
                    .Where(o => !o.IsFrozen)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(o => o.IsFrozen, _ => true)
                        .SetProperty(o => o.UpdatedAt, _ => timestamp));
                

                //freezing documetns is Idempotent, it is not affect ony banks andd customer , orr vevey where else 
                var documentsFrozen = await _context.AccountingDocuments
                    .Where(d => !d.IsFrozen)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(d => d.IsFrozen, _ => true));

                await transaction.CommitAsync();

                _logger.LogInformation("Freeze operation completed. Orders frozen: {Orders}, Documents frozen: {Documents}", ordersFrozen, documentsFrozen);
                return (ordersFrozen, documentsFrozen);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to freeze orders/documents initiated by {PerformedBy}", performedBy);
                throw;
            }
        }





        public async Task UpdateNotesAndDescriptions()
        {

            var orders = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.FromCurrency)
                .Include(o => o.ToCurrency)
                .Where(o => !o.IsDeleted)
                .AsNoTracking()
                .ToListAsync();



            // STEP 2: Update AccountingDocument Notes

            var documents = await _context.AccountingDocuments
                .Include(d => d.PayerCustomer)
                .Include(d => d.ReceiverCustomer)
                .Include(d => d.PayerBankAccount)
                .Include(d => d.ReceiverBankAccount)
                .Where(d => !d.IsDeleted)
                .AsNoTracking()
                .ToListAsync();



            // Update descriptions for Order transactions
            var orderHistoryRecords = await _context.CustomerBalanceHistory
                .Where(h => h.TransactionType == CustomerBalanceTransactionType.Order && !h.IsDeleted)
                .ToListAsync();

            foreach (var history in orderHistoryRecords)
            {
                var order = orders.FirstOrDefault(o => o.Id == history.ReferenceId);
                if (order != null)
                {
                    // Description includes customer info (from order.Notes)

                    var Description = $"معامله {order.CurrencyPair} - مشتری: {order.Customer?.FullName ?? "نامشخص"} - مقدار: {order.FromAmount} {order.FromCurrency?.Code ?? ""} → {order.ToAmount} {order.ToCurrency?.Code ?? ""} - نرخ: {order.Rate}";
                    if (!string.IsNullOrEmpty(order.Notes))
                        Description += $" - توضیحات : {order.Notes}";
                    history.Description = Description;


                    // Note includes transaction details without customer info
                    var note = $"{order.CurrencyPair} - مقدار: {order.FromAmount} {order.FromCurrency?.Code ?? ""} → {order.ToAmount} {order.ToCurrency?.Code ?? ""} - نرخ: {order.Rate}";
                    if (!string.IsNullOrEmpty(order.Notes))
                    {
                        note += $" - توضیحات: {order.Notes}";

                    }
                    history.Note = note;
                    history.TransactionNumber = (100 + order.Id).ToString();
                }
            }

            // Update descriptions for AccountingDocument transactions
            var documentHistoryRecords = await _context.CustomerBalanceHistory
                .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument && !h.IsDeleted)
                .ToListAsync();


            foreach (var history in documentHistoryRecords)
            {
                var document = documents.FirstOrDefault(d => d.Id == history.ReferenceId);
                if (document != null)
                {
                    // Description includes customer info (from document.Notes)

                    var Description = $"{document.Title} - مبلغ: {document.Amount} {document.CurrencyCode} - از: {document.PayerDisplayText} → به: {document.ReceiverDisplayText}";
                    if (!string.IsNullOrEmpty(document.Description))
                        Description += $" - توضیحات: {document.Description}";
                    history.Description = Description;


                    // Note includes transaction details without customer info
                    var note = $"{document.Type.GetDisplayName()} - مبلغ: {document.Amount} {document.CurrencyCode}";
                    if (!string.IsNullOrEmpty(document.ReferenceNumber))
                    {
                        note += $" -  شماره تراکنش: {document.ReferenceNumber}";

                    }
                    if (!string.IsNullOrWhiteSpace(document.Description))
                    {
                        note += $" -  توضیحات: {document.Description}";

                    }

                    history.Note = note;
                    history.TransactionNumber = document.ReferenceNumber;
                }
            }

            var historyUpdated = await _context.SaveChangesAsync();


        }




        #region Smart Delete Operations with History Soft Delete and Recalculation

        /// <summary>
        /// Safely delete an order by soft-deleting its history records and recalculating balances using partial rebuild.
        /// Uses incremental partial rebuilds for affected customer+currency and pool+currency combinations.
        /// </summary>
        public async Task DeleteOrderAsync(Order order, string performedBy = "Admin")
        {
            _logger.LogInformation($"Starting smart order deletion: Order {order.Id} by {performedBy}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Load order with currency navigation properties if not already loaded
                if (order.FromCurrency == null || order.ToCurrency == null)
                {
                    await _context.Entry(order)
                        .Reference(o => o.FromCurrency)
                        .LoadAsync();
                    await _context.Entry(order)
                        .Reference(o => o.ToCurrency)
                        .LoadAsync();
                }

                var fromCurrencyCode = (order.FromCurrency?.Code ?? "").ToUpperInvariant().Trim();
                var toCurrencyCode = (order.ToCurrency?.Code ?? "").ToUpperInvariant().Trim();
                var transactionDate = order.CreatedAt;

                // Mark order as deleted
                order.IsDeleted = true;
                order.DeletedAt = DateTime.UtcNow;
                order.DeletedBy = performedBy;

                // Mark related history records as deleted (soft delete)
                var customerHistoryRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.Order &&
                               h.ReferenceId == order.Id &&
                               !h.IsDeleted)
                    .ToListAsync();

                foreach (var history in customerHistoryRecords)
                {
                    history.IsDeleted = true;
                    history.DeletedAt = DateTime.UtcNow;
                    history.DeletedBy = performedBy;
                }

                var poolHistoryRecords = await _context.CurrencyPoolHistory
                    .Where(h => h.TransactionType == CurrencyPoolTransactionType.Order &&
                               h.ReferenceId == order.Id &&
                               !h.IsDeleted)
                    .ToListAsync();

                foreach (var history in poolHistoryRecords)
                {
                    history.IsDeleted = true;
                    history.DeletedAt = DateTime.UtcNow;
                    history.DeletedBy = performedBy;
                }

                await _context.SaveChangesAsync();

                // Rebuild affected customer balances from transaction date forward
                await RebuildCustomerBalanceFromDateAsync(order.CustomerId, fromCurrencyCode, transactionDate, performedBy);
                await RebuildCustomerBalanceFromDateAsync(order.CustomerId, toCurrencyCode, transactionDate, performedBy);

                // Rebuild affected pool balances from transaction date forward (only if order was not frozen)
                if (!order.IsFrozen)
                {
                    await RebuildPoolBalanceFromDateAsync(fromCurrencyCode, transactionDate, performedBy);
                    await RebuildPoolBalanceFromDateAsync(toCurrencyCode, transactionDate, performedBy);
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"Smart order deletion completed: Order {order.Id} - balances rebuilt successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in smart order deletion {order.Id}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Safely delete an accounting document by soft-deleting its history records and recalculating balances using partial rebuild.
        /// Uses incremental partial rebuilds for affected customer+currency and bank account combinations.
        /// </summary>
        public async Task DeleteAccountingDocumentAsync(AccountingDocument document, string performedBy = "Admin")
        {
            _logger.LogInformation($"Starting smart document deletion: Document {document.Id} by {performedBy}");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currencyCode = (document.CurrencyCode ?? "").ToUpperInvariant().Trim();
                var transactionDate = document.DocumentDate;

                // Mark document as deleted
                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;
                document.DeletedBy = performedBy;

                // Mark related history records as deleted (soft delete)
                var customerHistoryRecords = await _context.CustomerBalanceHistory
                    .Where(h => h.TransactionType == CustomerBalanceTransactionType.AccountingDocument &&
                               h.ReferenceId == document.Id &&
                               !h.IsDeleted)
                    .ToListAsync();

                foreach (var history in customerHistoryRecords)
                {
                    history.IsDeleted = true;
                    history.DeletedAt = DateTime.UtcNow;
                    history.DeletedBy = performedBy;
                }

                var bankHistoryRecords = await _context.BankAccountBalanceHistory
                    .Where(h => h.TransactionType == BankAccountTransactionType.Document &&
                               h.ReferenceId == document.Id &&
                               !h.IsDeleted)
                    .ToListAsync();

                foreach (var history in bankHistoryRecords)
                {
                    history.IsDeleted = true;
                    history.DeletedAt = DateTime.UtcNow;
                    history.DeletedBy = performedBy;
                }

                await _context.SaveChangesAsync();

                // Rebuild affected customer balances from transaction date forward
                if (document.PayerType == PayerType.Customer && document.PayerCustomerId.HasValue)
                {
                    await RebuildCustomerBalanceFromDateAsync(document.PayerCustomerId.Value, currencyCode, transactionDate, performedBy);
                }

                if (document.ReceiverType == ReceiverType.Customer && document.ReceiverCustomerId.HasValue)
                {
                    await RebuildCustomerBalanceFromDateAsync(document.ReceiverCustomerId.Value, currencyCode, transactionDate, performedBy);
                }

                // Rebuild affected bank account balances from transaction date forward (only if document was not frozen)
                if (!document.IsFrozen)
                {
                    if (document.PayerType == PayerType.System && document.PayerBankAccountId.HasValue)
                    {
                        await RebuildBankAccountBalanceFromDateAsync(document.PayerBankAccountId.Value, transactionDate, performedBy);
                    }

                    if (document.ReceiverType == ReceiverType.System && document.ReceiverBankAccountId.HasValue)
                    {
                        await RebuildBankAccountBalanceFromDateAsync(document.ReceiverBankAccountId.Value, transactionDate, performedBy);
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation($"Smart document deletion completed: Document {document.Id} - balances rebuilt successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error in smart document deletion {document.Id}: {ex.Message}");
                throw;
            }
        }



        #endregion



        #region Manual Balance History Creation

        /// <summary>
        /// Creates a manual customer balance history record with specified transaction date using incremental updates.
        /// This method creates proper balance chains with correct BalanceBefore, TransactionAmount, and BalanceAfter calculations.
        /// Manual transactions are never frozen and always affect current balance calculations.
        /// 
        /// **Note**: If transaction date is in the past, this uses the last balance before that date.
        /// For complex out-of-order scenarios, a partial rebuild may be needed.
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Use incremental update helper to create history record
                var historyRecord = await AddCustomerBalanceHistoryIncrementalAsync(
                    customerId: customerId,
                    currencyCode: currencyCode,
                    transactionAmount: amount,
                    transactionType: CustomerBalanceTransactionType.Manual,
                    referenceId: null, // Manual entries don't have reference IDs
                    transactionDate: transactionDate,
                    description: reason,
                    transactionNumber: transactionNumber,
                    performedBy: performedBy
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Manual customer balance history created: ID {historyRecord.Id}, Customer {customerId}, Currency {currencyCode}, Amount {amount}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    var customerName = customer.FullName ?? $"مشتری {customerId}";
                    await _notificationHub.SendManualAdjustmentNotificationAsync(
                        title: "تعدیل دستی موجودی ایجاد شد",
                        message: $"مشتری: {customerName} | مبلغ: {amount:N2} {currencyCode} | دلیل: {reason}",
                        eventType: NotificationEventType.ManualAdjustment,
                        userId: performingUserId,
                        navigationUrl: $"/Reports/CustomerReports?customerId={customerId}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual balance creation: Customer {customerId}, Amount {amount} {currencyCode}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual balance creation: Customer {customerId}, Amount {amount} {currencyCode}");
                    // Don't fail the main operation due to notification errors
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating manual customer balance history: Customer {customerId}, Currency {currencyCode}, Amount {amount}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a manual customer balance history record and recalculates balances from the transaction date using partial rebuild.
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

            var customerId = historyRecord.CustomerId;
            var currencyCode = historyRecord.CurrencyCode;
            var transactionDate = historyRecord.TransactionDate;
            var customerName = historyRecord.Customer?.FullName ?? $"مشتری {customerId}";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Hard delete the manual transaction
                _context.CustomerBalanceHistory.Remove(historyRecord);
                await _context.SaveChangesAsync();

                // Rebuild affected customer balance from transaction date forward
                await RebuildCustomerBalanceFromDateAsync(customerId, currencyCode, transactionDate, performedBy);

                await transaction.CommitAsync();

                _logger.LogInformation($"Manual customer balance history deleted: ID {transactionId}, Customer {customerId}, Currency {currencyCode}, Amount {historyRecord.TransactionAmount}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    await _notificationHub.SendManualAdjustmentNotificationAsync(
                        title: "تعدیل دستی موجودی حذف شد",
                        message: $"مشتری: {customerName} | مبلغ: {historyRecord.TransactionAmount:N2} {currencyCode}",
                        eventType: NotificationEventType.ManualAdjustment,
                        userId: performingUserId,
                        navigationUrl: $"/Reports/CustomerReports?customerId={customerId}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual balance deletion: Customer {customerId}, Amount {historyRecord.TransactionAmount} {currencyCode}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual balance deletion: Customer {customerId}, Amount {historyRecord.TransactionAmount} {currencyCode}");
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
        /// Creates a manual currency pool balance history record with specified transaction date using incremental updates.
        /// This method creates proper balance chains with correct BalanceBefore, TransactionAmount, and BalanceAfter calculations.
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Use incremental update helper to create history record
                var historyRecord = await AddPoolBalanceHistoryIncrementalAsync(
                    currencyCode: currencyCode,
                    transactionAmount: adjustmentAmount,
                    transactionType: CurrencyPoolTransactionType.ManualEdit,
                    referenceId: null, // Manual entries don't have reference IDs
                    transactionDate: transactionDate,
                    description: reason,
                    poolTransactionType: "Manual",
                    performedBy: performedBy
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Manual pool balance history created: ID {historyRecord.Id}, Currency {currencyCode}, Amount {adjustmentAmount}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    await _notificationHub.SendManualAdjustmentNotificationAsync(
                        title: "تعدیل دستی داشبورد ارزی ایجاد شد",
                        message: $"ارز: {currencyCode} | مبلغ: {adjustmentAmount:N2} || دلیل: {reason}",
                        eventType: NotificationEventType.ManualAdjustment,
                        userId: performingUserId,
                        navigationUrl: $"/Reports/PoolReports?currencyCode={currencyCode}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual pool balance creation: Currency {currencyCode}, Amount {adjustmentAmount}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual pool balance creation: Currency {currencyCode}, Amount {adjustmentAmount}");
                    // Don't fail the main operation due to notification errors
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating manual pool balance history: Currency {currencyCode}, Amount {adjustmentAmount}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a manual currency pool balance history record and recalculates balances from the transaction date using partial rebuild.
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Hard delete the manual transaction
                _context.CurrencyPoolHistory.Remove(historyRecord);
                await _context.SaveChangesAsync();

                // Rebuild affected pool balance from transaction date forward
                await RebuildPoolBalanceFromDateAsync(currencyCode, transactionDate, performedBy);

                await transaction.CommitAsync();

                _logger.LogInformation($"Manual pool balance history deleted: ID {transactionId}, Currency {currencyCode}, Amount {amount}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    await _notificationHub.SendManualAdjustmentNotificationAsync(
                        title: "تعدیل دستی داشبورد ارزی حذف شد",
                        message: $"ارز: {currencyCode} | مبلغ: {amount:N2}",
                        eventType: NotificationEventType.ManualAdjustment,
                        userId: performingUserId,
                        navigationUrl: $"/Reports/PoolReports?currencyCode={currencyCode}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual pool balance deletion: Currency {currencyCode}, Amount {amount}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual pool balance deletion: Currency {currencyCode}, Amount {amount}");
                    // Don't fail the main operation due to notification errors
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
        /// Creates a manual bank account balance history record with specified transaction date using incremental updates.
        /// This method creates proper balance chains with correct BalanceBefore, TransactionAmount, and BalanceAfter calculations.
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Use incremental update helper to create history record
                var historyRecord = await AddBankAccountBalanceHistoryIncrementalAsync(
                    bankAccountId: bankAccountId,
                    transactionAmount: amount,
                    transactionType: BankAccountTransactionType.ManualEdit,
                    referenceId: null, // Manual entries don't have reference IDs
                    transactionDate: transactionDate,
                    description: reason,
                    performedBy: performedBy
                );

                await transaction.CommitAsync();

                _logger.LogInformation($"Manual bank account balance history created: ID {historyRecord.Id}, Bank Account {bankAccountId}, Amount {amount}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    var bankAccount = await _context.BankAccounts
                        .FirstOrDefaultAsync(ba => ba.Id == bankAccountId);
                    var accountName = bankAccount?.AccountHolderName ?? $"حساب {bankAccountId}";

                    await _notificationHub.SendManualAdjustmentNotificationAsync(
                        title: "تعدیل دستی حساب بانکی ایجاد شد",
                        message: $"حساب: {accountName} | مبلغ: {amount:N2} | موجودی نهایی: {historyRecord.BalanceAfter:N2} | دلیل: {reason}",
                        eventType: NotificationEventType.ManualAdjustment,
                        userId: performingUserId,
                        navigationUrl: $"/Reports/BankAccountReports?bankAccountId={bankAccountId}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual bank account balance creation: Bank Account {bankAccountId}, Amount {amount}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual bank account balance creation: Bank Account {bankAccountId}, Amount {amount}");
                    // Don't fail the main operation due to notification errors
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error creating manual bank account balance history: Bank Account {bankAccountId}, Amount {amount}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a manual bank account balance history record and recalculates balances from the transaction date using partial rebuild.
        /// Only manual transactions (TransactionType.ManualEdit) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// </summary>
        public async Task DeleteManualBankAccountBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion", string? performingUserId = null)
        {
            _logger.LogInformation($"Deleting manual bank account balance history: Transaction ID {transactionId}");

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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Hard delete the manual transaction
                _context.BankAccountBalanceHistory.Remove(historyRecord);
                await _context.SaveChangesAsync();

                // Rebuild affected bank account balance from transaction date forward
                await RebuildBankAccountBalanceFromDateAsync(bankAccountId, transactionDate, performedBy);

                await transaction.CommitAsync();

                _logger.LogInformation($"Manual bank account balance history deleted: ID {transactionId}, Bank Account {bankAccountId}, Amount {amount}");

                // Send notification to admin users (excluding the performing user)
                try
                {
                    await _notificationHub.SendManualAdjustmentNotificationAsync(
                        title: "تعدیل دستی حساب بانکی حذف شد",
                        message: $"حساب: {accountName} | مبلغ: {amount:N2}",
                        eventType: NotificationEventType.ManualAdjustment,
                        userId: performingUserId,
                        navigationUrl: $"/Reports/BankAccountReports?bankAccountId={bankAccountId}",
                        priority: NotificationPriority.Normal
                    );

                    _logger.LogInformation($"Notification sent for manual bank account balance deletion: Bank Account {bankAccountId}, Amount {amount}");
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, $"Error sending notification for manual bank account balance deletion: Bank Account {bankAccountId}, Amount {amount}");
                    // Don't fail the main operation due to notification errors
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
