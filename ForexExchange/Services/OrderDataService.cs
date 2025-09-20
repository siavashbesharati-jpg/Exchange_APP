using Microsoft.EntityFrameworkCore;
using ForexExchange.Models;
using ForexExchange.Extensions;

namespace ForexExchange.Services
{
    /// <summary>
    /// Implementation of IOrderDataService that provides centralized order data preparation.
    /// Ensures both preview and create operations use identical validation and rounding logic.
    /// Follows Single Responsibility Principle (SRP) by handling only order data preparation.
    /// </summary>
    public class OrderDataService : IOrderDataService
    {
        private readonly ForexDbContext _context;
        private readonly ILogger<OrderDataService> _logger;

        public OrderDataService(ForexDbContext context, ILogger<OrderDataService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Prepares an Order object from form data with proper validation and rounding.
        /// This method ensures both PreviewOrderEffects and Create use identical data preparation.
        /// </summary>
        public async Task<OrderDataResult> PrepareOrderFromFormDataAsync(OrderFormDataDto dto)
        {
            _logger.LogInformation($"[PrepareOrderFromFormData] Processing order data - CustomerId: {dto.CustomerId}, FromCurrencyId: {dto.FromCurrencyId}, ToCurrencyId: {dto.ToCurrencyId}, FromAmount: {dto.FromAmount}, ToAmount: {dto.ToAmount}, Rate: {dto.Rate}");

            // Validate required fields
            if (dto.CustomerId <= 0)
                return new OrderDataResult { IsSuccess = false, ErrorMessage = "انتخاب مشتری الزامی است." };

            if (dto.FromCurrencyId <= 0 || dto.ToCurrencyId <= 0)
                return new OrderDataResult { IsSuccess = false, ErrorMessage = "انتخاب ارز مبدا و مقصد الزامی است." };

            if (dto.FromCurrencyId == dto.ToCurrencyId)
                return new OrderDataResult { IsSuccess = false, ErrorMessage = "ارز مبدا و مقصد نمی‌توانند یکسان باشند." };

            if (dto.FromAmount <= 0)
                return new OrderDataResult { IsSuccess = false, ErrorMessage = "مبلغ باید بزرگتر از صفر باشد." };

            if (dto.ToAmount <= 0)
                return new OrderDataResult { IsSuccess = false, ErrorMessage = "مبلغ مقصد باید بزرگتر از صفر باشد." };

            if (dto.Rate <= 0)
                return new OrderDataResult { IsSuccess = false, ErrorMessage = "نرخ ارز باید بزرگتر از صفر باشد." };

            // Fetch currencies
            var fromCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == dto.FromCurrencyId);
            var toCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == dto.ToCurrencyId);

            if (fromCurrency == null || toCurrency == null)
                return new OrderDataResult { IsSuccess = false, ErrorMessage = "ارز انتخاب شده یافت نشد." };

            // Apply currency-specific rounding (same logic for both preview and create)
            var roundedFromAmount = dto.FromAmount.RoundToCurrencyDefaults(fromCurrency.Code);
            var roundedToAmount = dto.ToAmount.RoundToCurrencyDefaults(toCurrency.Code);

            _logger.LogInformation($"[PrepareOrderFromFormData] Applied rounding - FromAmount: {dto.FromAmount} → {roundedFromAmount}, ToAmount: {dto.ToAmount} → {roundedToAmount}");

            // Create Order object with validated and rounded data
            var order = new Order
            {
                CustomerId = dto.CustomerId,
                FromCurrencyId = dto.FromCurrencyId,
                ToCurrencyId = dto.ToCurrencyId,
                FromAmount = roundedFromAmount,
                ToAmount = roundedToAmount,  // Use user-entered ToAmount (not calculated)
                Rate = dto.Rate,
                CreatedAt = dto.CreatedAt ?? DateTime.Now,
                Notes = dto.Notes,
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency
            };

            _logger.LogInformation($"[PrepareOrderFromFormData] Successfully prepared order with FromAmount={order.FromAmount}, ToAmount={order.ToAmount}, Rate={order.Rate}");

            return new OrderDataResult
            {
                IsSuccess = true,
                Order = order,
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency
            };
        }
    }
}