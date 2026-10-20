using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
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
        internal RegionalConsumption(string portId, ConsumptionConfiguration rule, int consumed)
        { PortId = portId; RuleId = rule.Id; GoodId = rule.GoodId; RequestedQuantity = rule.Quantity; ConsumedQuantity = consumed; }
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
        internal RegionalResult(RegionalWorldSnapshot before, RegionalWorldSnapshot after,
            IEnumerable<RegionalTrade> trades, IEnumerable<RegionalProduction> production,
            IEnumerable<RegionalConsumption> consumption, IEnumerable<RegionalMovement> movements,
            IEnumerable<RegionalDecision> decisions)
        {
            Before = before; After = after;
            Trades = Array.AsReadOnly(trades.ToArray());
            Production = Array.AsReadOnly(production.ToArray());
            Consumption = Array.AsReadOnly(consumption.ToArray());
            Movements = Array.AsReadOnly(movements.ToArray());
            Decisions = Array.AsReadOnly(decisions.ToArray());
        }
    }
}
