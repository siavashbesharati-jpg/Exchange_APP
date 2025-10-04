using System;
using ForexExchange.Extensions;

namespace ForexExchange.Services
{
    public interface IRateCalculationService
    {
        // Compute reverse rate for base->X given X->base (buy/sell)
        (decimal buy, decimal sell)? ComputeReverseFromBase(decimal buyToBase, decimal sellToBase);

        // Compute cross rate A->B given A->base and B->base (buy/sell)
        (decimal buy, decimal sell)? ComputeCrossFromBase((decimal buy, decimal sell)? aToBase, (decimal buy, decimal sell)? bToBase);

        // Calculate ToAmount based on currency direction (IRR vs foreign)
        decimal CalculateToAmount(decimal fromAmount, decimal rate, string fromCurrencyCode);

        // Calculate ToAmount with proper rounding based on target currency
        decimal CalculateToAmountWithCurrency(decimal fromAmount, decimal rate, string fromCurrencyCode, string toCurrencyCode);

        // Utility for rounding values consistently (default: 4 decimals)
        decimal SafeRound(decimal value, int decimals = 4);
    }

    public class RateCalculationService : IRateCalculationService
    {
        public (decimal buy, decimal sell)? ComputeReverseFromBase(decimal buyToBase, decimal sellToBase)
        {
            if (buyToBase <= 0 || sellToBase <= 0) return null;
            var buy = SafeRound(1m / sellToBase, 8);
            var sell = SafeRound(1m / buyToBase, 8);
            if (sell <= buy) sell = SafeRound(buy * 1.0001m, 8);
            return (buy, sell);
        }

        public (decimal buy, decimal sell)? ComputeCrossFromBase((decimal buy, decimal sell)? aToBase, (decimal buy, decimal sell)? bToBase)
        {
            if (aToBase == null || bToBase == null) return null;
            var (aBuy, aSell) = aToBase.Value;
            var (bBuy, bSell) = bToBase.Value;
            if (aBuy <= 0 || aSell <= 0 || bBuy <= 0 || bSell <= 0) return null;

            // Conservative microstructure: buy = a.buy / b.sell, sell = a.sell / b.buy
            var buy = SafeRound(aBuy / bSell);
            var sell = SafeRound(aSell / bBuy);
            if (sell <= buy)
            {
                // enforce minimal spread to avoid inversion precision issues
                sell = SafeRound(buy * 1.0001m);
            }
            return (buy, sell);
        }

        public decimal CalculateToAmount(decimal fromAmount, decimal rate, string fromCurrencyCode)
        {
            decimal result;
            
            // IRR to foreign: divide by rate
            // Foreign to IRR: multiply by rate
            if (fromCurrencyCode.ToUpper() == "IRR")
            {
                result = fromAmount / rate;
                // For IRR->Foreign, the target currency gets foreign rounding (3 decimals)
                // We don't know the target currency here, so we assume it's not IRR
                return result.TruncateToCurrencyDefaults(null); // null means non-IRR (2 decimals truncated)
            }
            else
            {
                result = fromAmount * rate;
                // For Foreign->IRR, the target currency is IRR, so use IRR rounding (nearest 1000)
                return result.TruncateToCurrencyDefaults("IRR");
            }
        }

        public decimal CalculateToAmountWithCurrency(decimal fromAmount, decimal rate, string fromCurrencyCode, string toCurrencyCode)
        {
            decimal result;
            
            // Calculate based on currency direction
            if (fromCurrencyCode.ToUpper() == "IRR")
            {
                result = fromAmount / rate;
            }
            else
            {
                result = fromAmount * rate;
            }
            
            // Round based on target currency
            return result.TruncateToCurrencyDefaults(toCurrencyCode);
        }

        public decimal SafeRound(decimal value, int decimals = 4)
            => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
