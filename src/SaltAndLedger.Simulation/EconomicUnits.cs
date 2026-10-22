using System;

namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// Version 3 stores quantities and money in exact integer subunits. Legacy worlds use scale 1.
    /// Marginal money-subunits per goods-subunit have the same numerical ratio as coins per good.
    /// </summary>
    public static class EconomicUnits
    {
        public const int QuantityScale = 10000;
        public const long MoneyScale = 10000;

        public static int FromWhole(int quantity)
        {
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            return checked(quantity * QuantityScale);
        }

        public static long Coins(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return checked(amount * MoneyScale);
        }

        internal static void ValidateQuantityScale(int scale)
        {
            if (scale != 1 && scale != QuantityScale)
                throw new ArgumentOutOfRangeException(nameof(scale), "Supported quantity scales are 1 and 10000.");
        }
    }

    /// <summary>A pure demand proposal; the owner commits its remainder only at a due opportunity.</summary>
    public readonly struct ConsumptionDemand
    {
        public int Quantity { get; }
        public int Remainder { get; }

        internal ConsumptionDemand(int quantity, int remainder)
        {
            Quantity = quantity;
            Remainder = remainder;
        }
    }
}
