using System;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public enum InventoryPricingModel
    {
        Linear,
        Scarcity
    }

    /// <summary>Immutable experimental prices assigned to transitions between adjacent stock levels.</summary>
    public sealed class InventoryPricing
    {
        public InventoryPricingModel Model { get; }
        public long ReferencePrice { get; }
        public int TargetStock { get; }
        public long Sensitivity { get; }
        public long MinPrice { get; }
        public long MaxPrice { get; }
        public long Spread { get; }

        public InventoryPricing(InventoryPricingModel model, long referencePrice, int targetStock,
            long sensitivity, long minPrice, long maxPrice, long spread)
        {
            if (model != InventoryPricingModel.Linear && model != InventoryPricingModel.Scarcity)
                throw new ArgumentOutOfRangeException(nameof(model));
            if (targetStock <= 0) throw new ArgumentOutOfRangeException(nameof(targetStock));
            if (minPrice <= 0) throw new ArgumentOutOfRangeException(nameof(minPrice));
            if (maxPrice < minPrice) throw new ArgumentOutOfRangeException(nameof(maxPrice));
            if (referencePrice < minPrice || referencePrice > maxPrice)
                throw new ArgumentOutOfRangeException(nameof(referencePrice));
            if (sensitivity < 0) throw new ArgumentOutOfRangeException(nameof(sensitivity));
            if (spread < 0) throw new ArgumentOutOfRangeException(nameof(spread));
            Model = model;
            ReferencePrice = referencePrice;
            TargetStock = targetStock;
            Sensitivity = sensitivity;
            MinPrice = minPrice;
            MaxPrice = maxPrice;
            Spread = spread;
        }

        /// <summary>Price for the edge upperStock ↔ upperStock - 1, in the requested direction.</summary>
        public long PriceAtEdge(TradeDirection direction, int upperStock)
        {
            ValidateDirection(direction);
            if (upperStock <= 0) throw new ArgumentOutOfRangeException(nameof(upperStock));
            Coefficients(Model == InventoryPricingModel.Scarcity && upperStock <= ScarcityThreshold,
                out var intercept, out var slope, out var denominator);
            var reference = FloorDivide(intercept - slope * upperStock, denominator);
            var price = reference + (direction == TradeDirection.Buy ? (BigInteger)Spread : -Spread);
            return (long)BigInteger.Max(MinPrice, BigInteger.Min(MaxPrice, price));
        }

        /// <summary>Sum the exact marginal prices over a legal market stock movement.</summary>
        public TradePrice Quote(TradeDirection direction, int marketStock, int quantity)
        {
            ValidateDirection(direction);
            if (marketStock < 0) throw new ArgumentOutOfRangeException(nameof(marketStock));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            int low;
            int high;
            if (direction == TradeDirection.Buy)
            {
                if (quantity > marketStock)
                    throw new ArgumentOutOfRangeException(nameof(quantity), "The market cannot supply this quantity.");
                low = checked(marketStock - quantity + 1);
                high = marketStock;
            }
            else
            {
                high = checked(marketStock + quantity);
                low = checked(marketStock + 1);
            }

            var offset = direction == TradeDirection.Buy ? (BigInteger)Spread : -Spread;
            var total = BigInteger.Zero;
            if (Model == InventoryPricingModel.Scarcity && low <= ScarcityThreshold)
            {
                var severeHigh = Math.Min(high, ScarcityThreshold);
                total += SumSegment(low, severeHigh, offset, true);
                low = severeHigh + 1;
            }
            if (low <= high) total += SumSegment(low, high, offset, false);
            var firstEdge = direction == TradeDirection.Buy ? marketStock : checked(marketStock + 1);
            var lastEdge = direction == TradeDirection.Buy ? checked(marketStock - quantity + 1) : high;
            return new TradePrice(quantity, PriceAtEdge(direction, firstEdge), PriceAtEdge(direction, lastEdge),
                checked((long)total));
        }

        private int ScarcityThreshold => Math.Max(1, TargetStock / 4);

        // raw(n) = (intercept - slope*n) / denominator. The severe segment adds
        // 3*sensitivity*(threshold-n)/threshold continuously below target/4.
        private void Coefficients(bool severe, out BigInteger intercept, out BigInteger slope,
            out BigInteger denominator)
        {
            denominator = TargetStock;
            slope = Sensitivity;
            intercept = ((BigInteger)ReferencePrice + Sensitivity) * TargetStock;
            if (severe)
            {
                intercept = intercept * ScarcityThreshold + 3 * (BigInteger)Sensitivity * ScarcityThreshold * TargetStock;
                slope = slope * ScarcityThreshold + 3 * (BigInteger)Sensitivity * TargetStock;
                denominator *= ScarcityThreshold;
            }
        }

        private BigInteger SumSegment(int low, int high, BigInteger offset, bool severe)
        {
            Coefficients(severe, out var intercept, out var slope, out var denominator);
            var count = (BigInteger)high - low + 1;
            if (MinPrice == MaxPrice) return count * MinPrice;
            if (slope.IsZero)
            {
                var price = FloorDivide(intercept, denominator) + offset;
                return count * BigInteger.Max(MinPrice, BigInteger.Min(MaxPrice, price));
            }

            // Solve the cap/floor crossings exactly; never iterate through units or price bands.
            var capEnd = FloorDivide(intercept - ((BigInteger)MaxPrice - offset) * denominator, slope);
            var floorStart = FloorDivide(intercept - ((BigInteger)MinPrice + 1 - offset) * denominator, slope) + 1;
            var capCount = BigInteger.Max(0, BigInteger.Min(high, capEnd) - low + 1);
            var floorCount = BigInteger.Max(0, (BigInteger)high - BigInteger.Max(low, floorStart) + 1);
            var total = capCount * MaxPrice + floorCount * MinPrice;
            var interiorLow = BigInteger.Max(low, capEnd + 1);
            var interiorHigh = BigInteger.Min(high, floorStart - 1);
            if (interiorLow <= interiorHigh)
            {
                total += FloorSum(interiorHigh - interiorLow + 1, denominator, -slope,
                    intercept - slope * interiorLow + offset * denominator);
            }
            return total;
        }

        // Sum floor((a*i+b)/m), i=0..n-1, using Euclidean reduction. BigInteger
        // division truncates toward zero, so normalize negative coefficients with floor division.
        private static BigInteger FloorSum(BigInteger n, BigInteger m, BigInteger a, BigInteger b)
        {
            var aWhole = FloorDivide(a, m);
            var bWhole = FloorDivide(b, m);
            var total = aWhole * n * (n - 1) / 2 + bWhole * n;
            a -= aWhole * m;
            b -= bWhole * m;
            while (true)
            {
                total += (a / m) * n * (n - 1) / 2 + (b / m) * n;
                a %= m;
                b %= m;
                var top = a * n + b;
                if (top < m) return total;
                n = top / m;
                b = top % m;
                var previousM = m;
                m = a;
                a = previousM;
            }
        }

        private static BigInteger FloorDivide(BigInteger numerator, BigInteger denominator)
        {
            var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
            return remainder.Sign < 0 ? quotient - 1 : quotient;
        }

        private static void ValidateDirection(TradeDirection direction)
        {
            if (direction != TradeDirection.Buy && direction != TradeDirection.Sell)
                throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }
}
