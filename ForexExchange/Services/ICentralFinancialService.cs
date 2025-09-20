using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Centralized financial service interface that combines customer balance management,
    /// currency pool operations, and bank account balance tracking with complete audit trail.
    /// This service ensures data consistency and provides event sourcing capabilities.
    /// </summary>
    public interface ICentralFinancialService
    {


        /// <summary>
        /// Simulates the effects of an order on customer and pool balances (no DB changes)
        /// </summary>
        Task<OrderPreviewEffectsDto> PreviewOrderEffectsAsync(Order order);

        #region Customer Balance Operations

        /// <summary>
        /// Gets current balance for a specific customer and currency
        /// </summary>
        Task<decimal> GetCustomerBalanceAsync(int customerId, string currencyCode);

        /// <summary>
        /// Gets all currency balances for a customer
        /// </summary>
        Task<List<CustomerBalance>> GetCustomerBalancesAsync(int customerId);

        /// <summary>
        /// Processes order creation - creates dual-currency impact (payment + receipt transactions)
        /// Preserves exact logic from existing CustomerFinancialHistoryService
        /// </summary>
        Task ProcessOrderCreationAsync(Order order, string performedBy = "System");

        /// <summary>
        /// Processes accounting document verification - updates customer balances
        /// Preserves exact logic from existing document processing
        /// </summary>
        Task ProcessAccountingDocumentAsync(AccountingDocument document, string performedBy = "System");

        /// <summary>
        /// Manually adjusts customer balance with audit trail
        /// </summary>
        Task AdjustCustomerBalanceAsync(int customerId, string currencyCode, decimal adjustmentAmount,
            string reason, string performedBy);

        /// <summary>
        /// Safely deletes an order by reversing its financial impacts
        /// </summary>
        Task DeleteOrderAsync(Order order, string performedBy = "Admin");

        /// <summary>
        /// Safely deletes an accounting document by reversing its financial impacts
        /// </summary>
        Task DeleteAccountingDocumentAsync(AccountingDocument document, string performedBy = "Admin");

        #endregion

        #region Currency Pool Operations

        /// <summary>
        /// Gets current balance for a currency pool
        /// </summary>
        Task<decimal> GetCurrencyPoolBalanceAsync(string currencyCode);

        /// <summary>
        /// Gets all currency pool balances
        /// </summary>
        Task<List<CurrencyPool>> GetAllCurrencyPoolsAsync();

        /// <summary>
        /// Increases currency pool balance (buying from customer)
        /// Preserves exact logic from existing pool management
        /// </summary>
        Task IncreaseCurrencyPoolAsync(string currencyCode, decimal amount, CurrencyPoolTransactionType transactionType,
            string reason, string performedBy = "System", int? referenceId = null, DateTime? transactionDate = null);

        /// <summary>
        /// Decreases currency pool balance (selling to customer)
        /// Preserves exact logic from existing pool management
        /// </summary>
        Task DecreaseCurrencyPoolAsync(string currencyCode, decimal amount, CurrencyPoolTransactionType transactionType,
            string reason, string performedBy = "System", int? referenceId = null, DateTime? transactionDate = null);

        /// <summary>
        /// Manually adjusts currency pool balance with audit trail
        /// </summary>
        Task AdjustCurrencyPoolAsync(string currencyCode, decimal adjustmentAmount,
            string reason, string performedBy);

        #endregion

        #region Bank Account Balance Operations

        /// <summary>
        /// Gets current balance for a bank account
        /// </summary>
        Task<decimal> GetBankAccountBalanceAsync(int bankAccountId);

        /// <summary>
        /// Gets all bank account balances
        /// </summary>
        Task<List<BankAccountBalance>> GetAllBankAccountBalancesAsync();

        /// <summary>
        /// Processes bank account transaction from accounting document
        /// Preserves exact logic from existing bank account processing
        /// </summary>
        Task ProcessBankAccountTransactionAsync(int bankAccountId, decimal amount, BankAccountTransactionType transactionType,
            int? relatedDocumentId, string reason, string performedBy = "System", DateTime? transactionDate = null, string? transactionNumber = null);

        /// <summary>
        /// Manually adjusts bank account balance with audit trail
        /// </summary>
        Task AdjustBankAccountBalanceAsync(int bankAccountId, decimal adjustmentAmount,
            string reason, string performedBy);

        #endregion

        #region Balance History and Audit

        /// <summary>
        /// Gets complete financial history for a customer within date range
        /// Preserves exact logic from CustomerFinancialHistoryService
        /// </summary>
        Task<CustomerFinancialHistoryDto> GetCustomerFinancialHistoryAsync(int customerId,
            DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Gets balance history for a customer and currency
        /// </summary>
        Task<List<CustomerBalanceHistory>> GetCustomerBalanceHistoryAsync(int customerId,
            string currencyCode, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Gets balance history for a currency pool
        /// </summary>
        Task<List<CurrencyPoolHistory>> GetCurrencyPoolHistoryAsync(string currencyCode,
            DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Gets balance history for a bank account
        /// </summary>
        Task<List<BankAccountBalanceHistory>> GetBankAccountHistoryAsync(int bankAccountId,
            DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Validates that current balances match the latest balance history entries
        /// Used for data integrity checks
        /// </summary>
        Task<bool> ValidateBalanceConsistencyAsync();

        /// <summary>
        /// Recalculates all current balances from history tables
        /// Used for balance reconciliation and initial data migration
        /// </summary>
        Task RecalculateAllBalancesFromHistoryAsync();

        /// <summary>
        /// Recalculates all balances based on transaction dates in chronological order.
        /// This method should be used after fixing transaction dates to ensure balance accuracy.
        /// </summary>
        Task RecalculateAllBalancesFromTransactionDatesAsync(string performedBy = "System");

        /// <summary>
        /// Creates a manual customer balance history record with specified transaction date.
        /// This is useful for manual adjustments, corrections, or importing historical data.
        /// After creating manual records, use RecalculateAllBalancesFromTransactionDatesAsync to ensure coherence.
        /// </summary>
        Task CreateManualCustomerBalanceHistoryAsync(int customerId, string currencyCode, decimal amount,
            string reason, DateTime transactionDate, string performedBy = "Manual Entry", string? transactionNumber = null);

        /// <summary>
        /// Deletes a manual customer balance history record and recalculates balances from the transaction date.
        /// Only manual transactions (TransactionType.Manual) can be deleted for safety.
        /// After deletion, balances are automatically recalculated to maintain coherence.
        /// </summary>
        Task DeleteManualCustomerBalanceHistoryAsync(long transactionId, string performedBy = "Manual Deletion");

        /// <summary>
        /// TEMPORARY METHOD: Recalculate IRR pool balance based on existing orders
        /// This method should be called once to fix missing IRR pool updates, then removed
        /// </summary>
        Task RecalculateIRRPoolFromOrdersAsync();

        #endregion

        #region Admin Methods for Deleted Records

        /// <summary>
        /// Get all orders including deleted ones (for admin purposes)
        /// </summary>
        Task<List<Order>> GetAllOrdersIncludingDeletedAsync();

        /// <summary>
        /// Get all accounting documents including deleted ones (for admin purposes)
        /// </summary>
        Task<List<AccountingDocument>> GetAllDocumentsIncludingDeletedAsync();

        /// <summary>
        /// Get only deleted orders (for admin recovery purposes)
        /// </summary>
        Task<List<Order>> GetDeletedOrdersAsync();

        /// <summary>
        /// Get only deleted accounting documents (for admin recovery purposes)
        /// </summary>
        Task<List<AccountingDocument>> GetDeletedDocumentsAsync();

        /// <summary>
        /// Restore a soft-deleted order (for admin recovery)
        /// </summary>
        Task RestoreOrderAsync(int orderId, string performedBy = "Admin");

        /// <summary>
        /// Restore a soft-deleted accounting document (for admin recovery)
        /// </summary>
        Task RestoreDocumentAsync(int documentId, string performedBy = "Admin");

        #endregion
    }
}


/// <summary>
/// DTO for customer financial history to preserve existing API contract
/// </summary>
public class CustomerFinancialHistoryDto
{
    public required Customer Customer { get; set; }
    public required List<CustomerBalance> Balances { get; set; }
    public required List<FinancialTransactionDto> Transactions { get; set; }
    public required Dictionary<string, decimal> InitialBalances { get; set; }
}

/// <summary>
/// DTO for financial transactions to preserve existing display format
/// </summary>
public class FinancialTransactionDto
{
    public DateTime Date { get; set; }
    public required string Type { get; set; } // "Order" or "Document"
    public required string Description { get; set; }
    public required string CurrencyCode { get; set; }
    public decimal Amount { get; set; }
    public decimal RunningBalance { get; set; }
    public int? OrderId { get; set; }
    public int? DocumentId { get; set; }
    public string? Notes { get; set; }
}
