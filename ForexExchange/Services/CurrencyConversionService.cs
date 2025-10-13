using System;
using ForexExchange.Models;

namespace ForexExchange.Services
{
    public interface ICurrencyConversionService
    {
        decimal ConvertAmount(decimal amount, int fromCurrencyId, int toCurrencyId);
    }

    public class CurrencyConversionService : ICurrencyConversionService
    {

        private readonly ForexDbContext _Context;

        public CurrencyConversionService(ForexDbContext context)
        {
            _Context = context;
        }
        public decimal ConvertAmount(decimal amount, int fromCurrencyId, int toCurrencyId)
        {
            var fromCurrency = _Context.Currencies.Find(fromCurrencyId);
            var ToCurrency = _Context.Currencies.Find(toCurrencyId);
            var exchangeRate = _Context.ExchangeRates.FirstOrDefault(c => c.FromCurrencyId == fromCurrencyId && c.ToCurrencyId == toCurrencyId);
            decimal result = 0;
            if (fromCurrency.RatePriority < ToCurrency.RatePriority)
            {
                // muliplty
                result = result * exchangeRate.Rate;
            }
            else
            {
                //devide
                result = result / exchangeRate.Rate;

            }
            return result;
        }
    }
}
