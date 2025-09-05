using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface ICustomerBalanceService
    {
        /// <summary>
        /// Get customer balance for specific currency
        /// </summary>
        Task<CustomerBalance> GetCustomerBalanceAsync(int customerId, string currencyCode);
        
        /// <summary>
        /// Get all balances for a customer
        /// </summary>
        Task<List<CustomerBalance>> GetCustomerBalancesAsync(int customerId);
        
        /// <summary>
        /// Get customer balance summary
        /// </summary>
        Task<CustomerBalanceSummary> GetCustomerBalanceSummaryAsync(int customerId);
        
        /// <summary>
        /// Get all customer balance summaries
        /// </summary>
        Task<List<CustomerBalanceSummary>> GetAllCustomerBalanceSummariesAsync();
        
        /// <summary>
        /// Update customer balance (used by orders)
        /// </summary>
        Task UpdateCustomerBalanceAsync(int customerId, string currencyCode, decimal amount, string reason);
        
        /// <summary>
        /// Process order creation - update customer balances
        /// </summary>
        Task ProcessOrderCreationAsync(Order order);
        
        /// <summary>
        /// Process order edit - update customer balances
        /// </summary>
        Task ProcessOrderEditAsync(Order oldOrder, Order newOrder);
        
        /// <summary>
        /// Process accounting document - update customer balance
        /// </summary>
        Task ProcessAccountingDocumentAsync(AccountingDocument document);
        
        /// <summary>
        /// Set initial balance for customer (admin function)
        /// </summary>
        Task SetInitialBalanceAsync(int customerId, string currencyCode, decimal amount, string notes);
    }
}
