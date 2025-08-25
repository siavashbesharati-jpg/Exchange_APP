using System;

namespace ForexExchange.Services
{
    public interface IRateCalculationService
    {
        // Compute reverse rate for base->X given X->base (buy/sell)
        (decimal buy, decimal sell)? ComputeReverseFromBase(decimal buyToBase, decimal sellToBase);

        // Compute cross rate A->B given A->base and B->base (buy/sell)
        (decimal buy, decimal sell)? ComputeCrossFromBase((decimal buy, decimal sell)? aToBase, (decimal buy, decimal sell)? bToBase);

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

    public decimal SafeRound(decimal value, int decimals = 4)
            => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }
}
