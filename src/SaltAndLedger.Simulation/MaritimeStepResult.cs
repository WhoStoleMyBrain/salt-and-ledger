using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class PortTradeAttempt
    {
        public string PortId { get; }
        public TradeAttempt Trade { get; }

        internal PortTradeAttempt(string portId, TradeAttempt trade)
        {
            PortId = portId;
            Trade = trade;
        }
    }

    public sealed class PortProductionAttempt
    {
        public string PortId { get; }
        public ProductionAttempt Production { get; }

        internal PortProductionAttempt(string portId, ProductionAttempt production)
        {
            PortId = portId;
            Production = production;
        }
    }

    public enum ShipMovementKind
    {
        Departure,
        Progress,
        Arrival
    }

    public sealed class ShipMovementEvent
    {
        public ShipMovementKind Kind { get; }
        public string ShipId { get; }
        public string OriginPortId { get; }
        public string DestinationPortId { get; }
        public int RemainingTicksBefore { get; }
        public int RemainingTicksAfter { get; }

        internal ShipMovementEvent(ShipMovementKind kind, string shipId, string originPortId,
            string destinationPortId, int remainingTicksBefore, int remainingTicksAfter)
        {
            Kind = kind;
            ShipId = shipId;
            OriginPortId = originPortId;
            DestinationPortId = destinationPortId;
            RemainingTicksBefore = remainingTicksBefore;
            RemainingTicksAfter = remainingTicksAfter;
        }
    }

    /// <summary>One atomic global step. The host decides which results to retain.</summary>
    public sealed class MaritimeStepResult
    {
        public long Tick { get; }
        public MaritimeWorldSnapshot After { get; }
        public IReadOnlyList<PortTradeAttempt> TradeAttempts { get; }
        public IReadOnlyList<PortProductionAttempt> ProductionAttempts { get; }
        public IReadOnlyList<ShipMovementEvent> MovementEvents { get; }

        internal MaritimeStepResult(long tick, MaritimeWorldSnapshot after, IEnumerable<PortTradeAttempt> tradeAttempts,
            IEnumerable<PortProductionAttempt> productionAttempts, IEnumerable<ShipMovementEvent> movementEvents)
        {
            Tick = tick;
            After = after;
            TradeAttempts = Array.AsReadOnly(tradeAttempts.ToArray());
            ProductionAttempts = Array.AsReadOnly(productionAttempts.ToArray());
            MovementEvents = Array.AsReadOnly(movementEvents.ToArray());
        }
    }
}
