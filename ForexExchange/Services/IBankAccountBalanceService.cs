using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface IBankAccountBalanceService
    {
        /// <summary>
        /// Get bank account balance for specific currency
        /// </summary>
        Task<BankAccountBalance> GetBankAccountBalanceAsync(int bankAccountId, string currencyCode);
        
        /// <summary>
        /// Get all balances for a bank account
        /// </summary>
        Task<List<BankAccountBalance>> GetBankAccountBalancesAsync(int bankAccountId);
        
        /// <summary>
        /// Get bank account balance summary
        /// </summary>
        Task<BankAccountBalanceSummary> GetBankAccountBalanceSummaryAsync(int bankAccountId);
        
        /// <summary>
        /// Get all bank account balance summaries
        /// </summary>
        Task<List<BankAccountBalanceSummary>> GetAllBankAccountBalanceSummariesAsync();
        
        /// <summary>
        /// Update bank account balance
        /// </summary>
        Task UpdateBankAccountBalanceAsync(int bankAccountId, string currencyCode, decimal amount, string reason);
        
        /// <summary>
        /// Process accounting document - update bank account balance
        /// </summary>
        Task ProcessAccountingDocumentAsync(AccountingDocument document);
        
        /// <summary>
        /// Set initial balance for bank account (admin function)
        /// </summary>
        Task SetInitialBalanceAsync(int bankAccountId, string currencyCode, decimal amount, string notes);
    }
}
