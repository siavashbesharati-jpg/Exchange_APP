using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface IReceiptService
    {
        /// <summary>
        /// Processes a receipt upload and creates corresponding transaction
        /// </summary>
        /// <param name="receipt">The receipt to process</param>
        /// <returns>The created transaction if successful</returns>
        Task<Transaction?> ProcessReceiptUploadAsync(Receipt receipt);
        
        /// <summary>
        /// Validates that a receipt can be processed
        /// </summary>
        /// <param name="receipt">The receipt to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        Task<bool> ValidateReceiptAsync(Receipt receipt);
    }
}
