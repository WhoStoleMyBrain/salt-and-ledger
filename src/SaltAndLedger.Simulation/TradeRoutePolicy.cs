using System;

namespace SaltAndLedger.Simulation
{
    /// <summary>Player-selected commodity, source shipment target, and marginal price limits.</summary>
    public sealed class RouteLeg
    {
        public string GoodId { get; }
        public int MaxPurchaseQuantity { get; }
        public int MinShipmentQuantity { get; }
        public long MaxBuyPrice { get; }
        public long MinSellPrice { get; }

        public RouteLeg(string goodId, int maxPurchaseQuantity, int minShipmentQuantity,
            long maxBuyPrice, long minSellPrice)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (maxPurchaseQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(maxPurchaseQuantity));
            if (minShipmentQuantity <= 0 || minShipmentQuantity > maxPurchaseQuantity)
                throw new ArgumentOutOfRangeException(nameof(minShipmentQuantity));
            if (maxBuyPrice <= 0) throw new ArgumentOutOfRangeException(nameof(maxBuyPrice));
            if (minSellPrice <= 0) throw new ArgumentOutOfRangeException(nameof(minSellPrice));
            GoodId = goodId;
            MaxPurchaseQuantity = maxPurchaseQuantity;
            MinShipmentQuantity = minShipmentQuantity;
            MaxBuyPrice = maxBuyPrice;
            MinSellPrice = minSellPrice;
        }
    }

    /// <summary>One immutable two-port policy. Distinct commodities make its stage derivable from cargo.</summary>
    public sealed class TradeRoutePolicy
    {
        public string OriginPortId { get; }
        public string DestinationPortId { get; }
        public RouteLeg Outbound { get; }
        public RouteLeg? ReturnLeg { get; }
        public long CashReserve { get; }
        public bool AllowEmptyReturn { get; }

        public TradeRoutePolicy(string originPortId, string destinationPortId, RouteLeg outbound,
            RouteLeg? returnLeg = null, long cashReserve = 0, bool allowEmptyReturn = false)
        {
            RuntimeIdentifier.Validate(originPortId, nameof(originPortId));
            RuntimeIdentifier.Validate(destinationPortId, nameof(destinationPortId));
            if (string.Equals(originPortId, destinationPortId, StringComparison.Ordinal))
                throw new ArgumentException("Route ports must differ.", nameof(destinationPortId));
            if (outbound == null) throw new ArgumentNullException(nameof(outbound));
            if (returnLeg != null && string.Equals(outbound.GoodId, returnLeg.GoodId, StringComparison.Ordinal))
                throw new ArgumentException("Outbound and return commodities must differ.", nameof(returnLeg));
            if (returnLeg == null && !allowEmptyReturn)
                throw new ArgumentException("A route without return purchases must allow an empty return.", nameof(allowEmptyReturn));
            if (cashReserve < 0) throw new ArgumentOutOfRangeException(nameof(cashReserve));
            OriginPortId = originPortId;
            DestinationPortId = destinationPortId;
            Outbound = outbound;
            ReturnLeg = returnLeg;
            CashReserve = cashReserve;
            AllowEmptyReturn = allowEmptyReturn;
        }
    }
}
