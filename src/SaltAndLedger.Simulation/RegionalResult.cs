using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class RegionalWarehouseTrade
    {
        public string FacilityId { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public TradeAttempt Trade { get; }
        internal RegionalWarehouseTrade(string facilityId, string ownerId, string portId, TradeAttempt trade)
        { FacilityId = facilityId; OwnerId = ownerId; PortId = portId; Trade = trade; }
    }

    public sealed class RegionalPrivateProduction
    {
        public string FacilityId { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public string RecipeId { get; }
        public ScheduledProductionOutcome Outcome { get; }
        public IReadOnlyList<InventoryChange> Changes { get; }
        internal RegionalPrivateProduction(string facilityId, string ownerId, string portId, string recipeId,
            ScheduledProductionOutcome outcome, IEnumerable<InventoryChange> changes)
        {
            FacilityId = facilityId; OwnerId = ownerId; PortId = portId; RecipeId = recipeId; Outcome = outcome;
            Changes = Array.AsReadOnly(changes.ToArray());
        }
    }

    public sealed class RegionalConstruction
    {
        public string FacilityId { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public string BlueprintId { get; }
        public ConstructionOutcome Outcome { get; }
        public long Cost { get; }
        public long OrderedDay { get; }
        public long? CompletionDay { get; }
        public long? FirstProductionDay { get; }
        internal RegionalConstruction(string facilityId, string ownerId, string portId, string blueprintId,
            ConstructionOutcome outcome, long cost, long orderedDay = 0,
            long? completionDay = null, long? firstProductionDay = null)
        {
            FacilityId = facilityId; OwnerId = ownerId; PortId = portId; BlueprintId = blueprintId;
            Outcome = outcome; Cost = cost; OrderedDay = orderedDay;
            CompletionDay = completionDay; FirstProductionDay = firstProductionDay;
        }
    }

    public sealed class RegionalTransfer
    {
        public string OwnerId { get; }
        public string ShipId { get; }
        public string FacilityId { get; }
        public TransferDirection Direction { get; }
        public string GoodId { get; }
        public int RequestedQuantity { get; }
        public int TransferredQuantity => Outcome == TransferOutcome.Transferred ? RequestedQuantity : 0;
        public TransferOutcome Outcome { get; }
        internal RegionalTransfer(string ownerId, string shipId, string facilityId, TransferDirection direction,
            string goodId, int quantity, TransferOutcome outcome)
        {
            OwnerId = ownerId; ShipId = shipId; FacilityId = facilityId; Direction = direction;
            GoodId = goodId; RequestedQuantity = quantity; Outcome = outcome;
        }
    }

    public sealed class RegionalTrade
    {
        public string ShipId { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public TradeAttempt Trade { get; }
        internal RegionalTrade(string shipId, string ownerId, string portId, TradeAttempt trade)
        { ShipId = shipId; OwnerId = ownerId; PortId = portId; Trade = trade; }
    }

    public sealed class RegionalProduction
    {
        public string PortId { get; }
        public string FacilityId { get; }
        public string RecipeId { get; }
        public ScheduledProductionOutcome Outcome { get; }
        public IReadOnlyList<InventoryChange> Changes { get; }
        internal RegionalProduction(string portId, string facilityId, string recipeId,
            ScheduledProductionOutcome outcome, IEnumerable<InventoryChange> changes)
        {
            PortId = portId; FacilityId = facilityId; RecipeId = recipeId; Outcome = outcome;
            Changes = Array.AsReadOnly(changes.ToArray());
        }
    }

    public sealed class RegionalConsumption
    {
        public string PortId { get; }
        public string RuleId { get; }
        public string GoodId { get; }
        public int RequestedQuantity { get; }
        public int ConsumedQuantity { get; }
        public int UnmetQuantity => RequestedQuantity - ConsumedQuantity;
        public ConsumptionKind Kind { get; }
        public long Payment { get; }
        public int UnmetStock { get; }
        public int UnmetAffordability { get; }
        public bool ShouldSerializeKind() => Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializePayment() => Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeUnmetStock() => Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeUnmetAffordability() => Kind != ConsumptionKind.Legacy;
        internal RegionalConsumption(string portId, ConsumptionConfiguration rule, int consumed,
            int? desired = null, long payment = 0, int unmetStock = 0, int unmetAffordability = 0)
        {
            PortId = portId; RuleId = rule.Id; GoodId = rule.GoodId; RequestedQuantity = desired ?? rule.Quantity;
            ConsumedQuantity = consumed; Kind = rule.Kind; Payment = payment;
            UnmetStock = unmetStock; UnmetAffordability = unmetAffordability;
        }
    }

    public sealed class RegionalMovement
    {
        public string ShipId { get; }
        public string OriginPortId { get; }
        public string DestinationPortId { get; }
        public ShipMovementKind Kind { get; }
        public int RemainingDays { get; }
        internal RegionalMovement(string shipId, string origin, string destination, ShipMovementKind kind, int remaining)
        { ShipId = shipId; OriginPortId = origin; DestinationPortId = destination; Kind = kind; RemainingDays = remaining; }
    }

    public sealed class RegionalResult
    {
        public RegionalWorldSnapshot Before { get; }
        public RegionalWorldSnapshot After { get; }
        public IReadOnlyList<RegionalTrade> Trades { get; }
        public IReadOnlyList<RegionalProduction> Production { get; }
        public IReadOnlyList<RegionalConsumption> Consumption { get; }
        public IReadOnlyList<RegionalMovement> Movements { get; }
        public IReadOnlyList<RegionalDecision> Decisions { get; }
        public IReadOnlyList<RegionalWarehouseTrade> WarehouseTrades { get; }
        public IReadOnlyList<RegionalPrivateProduction> PrivateProduction { get; }
        public IReadOnlyList<RegionalConstruction> Construction { get; }
        public IReadOnlyList<RegionalTransfer> Transfers { get; }
        public IReadOnlyList<EconomicEvent> EconomicEvents { get; }
        public IReadOnlyList<CityCashTransfer> CashTransfers { get; }
        public bool ShouldSerializeCashTransfers() => CashTransfers.Count != 0;
        public IReadOnlyList<CityChange> CityChanges { get; }
        public bool ShouldSerializeCityChanges() => CityChanges.Count != 0;
        internal RegionalResult(RegionalWorldSnapshot before, RegionalWorldSnapshot after,
            IEnumerable<RegionalTrade> trades, IEnumerable<RegionalProduction> production,
            IEnumerable<RegionalConsumption> consumption, IEnumerable<RegionalMovement> movements,
            IEnumerable<RegionalDecision> decisions, IEnumerable<RegionalWarehouseTrade> warehouseTrades,
            IEnumerable<RegionalPrivateProduction> privateProduction, IEnumerable<RegionalConstruction> construction,
            IEnumerable<RegionalTransfer> transfers, IEnumerable<EconomicEvent> economicEvents,
            IEnumerable<CityCashTransfer>? cashTransfers = null, IEnumerable<CityChange>? cityChanges = null)
        {
            Before = before; After = after;
            Trades = Array.AsReadOnly(trades.ToArray());
            Production = Array.AsReadOnly(production.ToArray());
            Consumption = Array.AsReadOnly(consumption.ToArray());
            Movements = Array.AsReadOnly(movements.ToArray());
            Decisions = Array.AsReadOnly(decisions.ToArray());
            WarehouseTrades = Array.AsReadOnly(warehouseTrades.ToArray());
            PrivateProduction = Array.AsReadOnly(privateProduction.ToArray());
            Construction = Array.AsReadOnly(construction.ToArray());
            Transfers = Array.AsReadOnly(transfers.ToArray());
            EconomicEvents = Array.AsReadOnly(economicEvents.ToArray());
            CashTransfers = Array.AsReadOnly((cashTransfers ?? Array.Empty<CityCashTransfer>()).ToArray());
            CityChanges = Array.AsReadOnly((cityChanges ?? Array.Empty<CityChange>()).ToArray());
        }
    }
}
