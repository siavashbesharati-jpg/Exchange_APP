using ForexExchange.Models;

namespace ForexExchange.Services
{
    /// <summary>
    /// Service responsible for preparing and validating Order data from form inputs.
    /// Ensures both preview and actual order creation use identical data preparation logic.
    /// Follows Single Responsibility Principle (SRP) by centralizing order data handling.
    /// </summary>
    public interface IOrderDataService
    {
        /// <summary>
        /// Prepares an Order object from form data with proper validation and rounding.
        /// Used by both PreviewOrderEffects and Create methods to ensure consistency.
        /// </summary>
        /// <param name="dto">Raw form data from frontend</param>
        /// <returns>Validated and properly rounded Order object ready for processing</returns>
        Task<OrderDataResult> PrepareOrderFromFormDataAsync(OrderFormDataDto dto);
    }

    /// <summary>
    /// DTO representing raw form data from frontend (same for both preview and submit)
    /// </summary>
    public class OrderFormDataDto
    {
        public int CustomerId { get; set; }
        public int FromCurrencyId { get; set; }
        public int ToCurrencyId { get; set; }
        public decimal FromAmount { get; set; }
        public decimal ToAmount { get; set; }  // User-entered ToAmount (not calculated)
        public decimal Rate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Result of order data preparation containing validated Order and metadata
    /// </summary>
    public class OrderDataResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public Order? Order { get; set; }
        public Currency? FromCurrency { get; set; }
        public Currency? ToCurrency { get; set; }
    }
}