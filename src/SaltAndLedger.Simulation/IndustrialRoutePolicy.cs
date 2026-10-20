using System;

namespace SaltAndLedger.Simulation
{
    public enum IndustrialEndpointKind { Market, Warehouse }
    public enum IndustrialRouteStage { AcquireOutbound, DeliverOutbound, AcquireReturn, DeliverReturn }
    public enum IndustrialRouteAction
    {
        BuyAndDepart, LoadAndDepart, Sell, Unload, Depart, AdvanceStage, ContinueSailing, Wait, Pause
    }

    /// <summary>One commodity's physical acquisition and delivery between the policy's two ports.</summary>
    public sealed class IndustrialRouteLeg
    {
        public string GoodId { get; }
        public int MaximumShipmentQuantity { get; }
        public int MinimumShipmentQuantity { get; }
        public IndustrialEndpointKind SourceKind { get; }
        public IndustrialEndpointKind DestinationKind { get; }
        public string? SourceFacilityId { get; }
        public string? DestinationFacilityId { get; }
        public int MinimumSourceStock { get; }
        public int WarehouseReserve { get; }
        public long MaximumBuyPrice { get; }
        public long MinimumSellPrice { get; }
        public long MaximumPurchaseExpenditure { get; }

        public IndustrialRouteLeg(string goodId, int maximumShipmentQuantity, int minimumShipmentQuantity,
            IndustrialEndpointKind sourceKind = IndustrialEndpointKind.Market,
            IndustrialEndpointKind destinationKind = IndustrialEndpointKind.Market,
            string? sourceFacilityId = null, string? destinationFacilityId = null,
            int minimumSourceStock = 0, int warehouseReserve = 0,
            long maximumBuyPrice = long.MaxValue, long minimumSellPrice = 1,
            long maximumPurchaseExpenditure = long.MaxValue)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (maximumShipmentQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(maximumShipmentQuantity));
            if (minimumShipmentQuantity <= 0 || minimumShipmentQuantity > maximumShipmentQuantity)
                throw new ArgumentOutOfRangeException(nameof(minimumShipmentQuantity));
            ValidateEndpoint(sourceKind, sourceFacilityId, nameof(sourceFacilityId));
            ValidateEndpoint(destinationKind, destinationFacilityId, nameof(destinationFacilityId));
            if (minimumSourceStock < 0 || warehouseReserve < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumSourceStock));
            if (sourceKind != IndustrialEndpointKind.Warehouse && warehouseReserve != 0)
                throw new ArgumentException("A private stock reserve requires a warehouse source.", nameof(warehouseReserve));
            if (maximumBuyPrice <= 0 || minimumSellPrice <= 0 || maximumPurchaseExpenditure <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBuyPrice));
            GoodId = goodId;
            MaximumShipmentQuantity = maximumShipmentQuantity;
            MinimumShipmentQuantity = minimumShipmentQuantity;
            SourceKind = sourceKind;
            DestinationKind = destinationKind;
            SourceFacilityId = sourceFacilityId;
            DestinationFacilityId = destinationFacilityId;
            MinimumSourceStock = minimumSourceStock;
            WarehouseReserve = warehouseReserve;
            MaximumBuyPrice = maximumBuyPrice;
            MinimumSellPrice = minimumSellPrice;
            MaximumPurchaseExpenditure = maximumPurchaseExpenditure;
        }

        private static void ValidateEndpoint(IndustrialEndpointKind kind, string? facilityId, string parameter)
        {
            if (kind != IndustrialEndpointKind.Market && kind != IndustrialEndpointKind.Warehouse)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == IndustrialEndpointKind.Warehouse) RuntimeIdentifier.Validate(facilityId!, parameter);
            else if (facilityId != null) throw new ArgumentException("A market endpoint cannot name a private facility.", parameter);
        }
    }

    /// <summary>An immutable two-port policy; direction is explicit even when both legs carry the same good.</summary>
    public sealed class IndustrialRoutePolicy
    {
        public string OriginPortId { get; }
        public string DestinationPortId { get; }
        public IndustrialRouteLeg Outbound { get; }
        public IndustrialRouteLeg? ReturnLeg { get; }
        public long CashReserve { get; }
        public bool AllowEmptyReturn { get; }

        public IndustrialRoutePolicy(string originPortId, string destinationPortId, IndustrialRouteLeg outbound,
            IndustrialRouteLeg? returnLeg = null, long cashReserve = 0, bool allowEmptyReturn = true)
        {
            RuntimeIdentifier.Validate(originPortId, nameof(originPortId));
            RuntimeIdentifier.Validate(destinationPortId, nameof(destinationPortId));
            if (originPortId == destinationPortId) throw new ArgumentException("Route ports must differ.", nameof(destinationPortId));
            if (outbound == null) throw new ArgumentNullException(nameof(outbound));
            if (returnLeg == null && !allowEmptyReturn)
                throw new ArgumentException("A route without a return shipment must allow an empty return.", nameof(allowEmptyReturn));
            if (cashReserve < 0) throw new ArgumentOutOfRangeException(nameof(cashReserve));
            OriginPortId = originPortId;
            DestinationPortId = destinationPortId;
            Outbound = outbound;
            ReturnLeg = returnLeg;
            CashReserve = cashReserve;
            AllowEmptyReturn = allowEmptyReturn;
        }
    }

    public sealed class IndustrialTransferCommand
    {
        public string FacilityId { get; }
        public TransferDirection Direction { get; }
        public string GoodId { get; }
        public int Quantity { get; }
        internal IndustrialTransferCommand(string facilityId, TransferDirection direction, string goodId, int quantity)
        { FacilityId = facilityId; Direction = direction; GoodId = goodId; Quantity = quantity; }
    }

    /// <summary>Commands selected from one exact snapshot; the regional owner alone executes and advances stage.</summary>
    public sealed class IndustrialRouteDecision
    {
        public RegionalWorldSnapshot Before { get; }
        public string ShipId { get; }
        public IndustrialRouteStage Stage { get; }
        public IndustrialRouteStage NextStage { get; }
        public IndustrialRouteAction Action { get; }
        public string Reason { get; }
        public PortTradeCommand? Trade { get; }
        public IndustrialTransferCommand? Transfer { get; }
        public SailCommand? Departure { get; }
        public TradeAttempt? Preview { get; }
        public int SelectedQuantity => Trade?.Trade.Quantity ?? Transfer?.Quantity ?? 0;
        internal IndustrialRouteDecision(RegionalWorldSnapshot before, string shipId, IndustrialRouteStage stage,
            IndustrialRouteStage nextStage, IndustrialRouteAction action, string reason,
            PortTradeCommand? trade = null, IndustrialTransferCommand? transfer = null,
            SailCommand? departure = null, TradeAttempt? preview = null)
        {
            Before = before; ShipId = shipId; Stage = stage; NextStage = nextStage; Action = action;
            Reason = reason; Trade = trade; Transfer = transfer; Departure = departure; Preview = preview;
        }
    }
}
