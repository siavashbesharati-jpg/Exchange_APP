using ForexExchange.Models;

namespace ForexExchange.Services
{
    // TODO: Reimplement settlement with AccountingDocument in new architecture
    /*
    public interface ITransactionSettlementService
    {
        /// <summary>
        /// Creates a transaction from matched buy/sell orders
        /// </summary>
        Task<Transaction> CreateTransactionAsync(Order buyOrder, Order sellOrder, decimal matchedAmount);
        
        /// <summary>
        /// Initiates settlement process for a transaction
        /// </summary>
        Task<bool> InitiateSettlementAsync(int transactionId);
        
        /// <summary>
        /// Confirms buyer payment based on uploaded receipt
        /// </summary>
        Task<bool> ConfirmBuyerPaymentAsync(int transactionId, int receiptId);
        
        /// <summary>
        /// Confirms seller payment/transfer
        /// </summary>
        Task<bool> ConfirmSellerPaymentAsync(int transactionId, string bankReference);
        
        /// <summary>
        /// Marks transaction as completed
        /// </summary>
        Task<bool> CompleteTransactionAsync(int transactionId);
        
        /// <summary>
        /// Handles transaction failure and rollback
        /// </summary>
        Task<bool> FailTransactionAsync(int transactionId, string reason);
        
        /// <summary>
        /// Gets transactions requiring settlement action
        /// </summary>
        Task<List<Transaction>> GetPendingSettlementsAsync();
        
        /// <summary>
        /// Calculates settlement fees and commissions
        /// </summary>
        Task<SettlementCalculation> CalculateSettlementAsync(Transaction transaction);
        
        /// <summary>
        /// Sends settlement notifications to customers
        /// </summary>
        Task SendSettlementNotificationAsync(Transaction transaction, SettlementStatus status);
    }
    
    public class SettlementCalculation
    {
        public decimal GrossAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal ExchangeFee { get; set; }
        public decimal BuyerTotalPayment { get; set; }
        public decimal SellerNetReceived { get; set; }
    }
    
    public enum SettlementStatus
    {
        Initiated,
        AwaitingBuyerPayment,
        BuyerPaymentConfirmed,
        AwaitingSellerTransfer,
        SellerTransferConfirmed,
        Completed,
        Failed
    }
    */
}
