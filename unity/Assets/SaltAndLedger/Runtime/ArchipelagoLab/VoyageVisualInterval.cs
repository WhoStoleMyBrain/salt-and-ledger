using System;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Presentation bounds derived only from committed voyage progress.</summary>
    public sealed class VoyageVisualInterval
    {
        public string OriginPortId { get; }
        public string DestinationPortId { get; }
        public int Speed { get; }
        public int StartDays { get; }
        public int EndDays { get; }

        private VoyageVisualInterval(RegionalShipSnapshot ship, int startDays, int endDays)
        {
            OriginPortId = ship.OriginPortId;
            DestinationPortId = ship.DestinationPortId;
            Speed = ship.Configuration.Speed;
            StartDays = startDays;
            EndDays = endDays;
        }

        public static VoyageVisualInterval Between(RegionalWorldSnapshot previous,
            RegionalWorldSnapshot next, string shipId, bool animate)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            var ship = next.Ships[shipId];
            RegionalShipSnapshot before = null;
            var advancing = animate && previous != null && next.Day > previous.Day &&
                previous.Ships.TryGetValue(shipId, out before);
            if (ship.DockedPortId != null)
            {
                // Keep the last physical path until arrival interpolation finishes, then show the berth.
                if (!advancing || before.DockedPortId != null || before.DestinationPortId != ship.DockedPortId)
                    return null;
                return new VoyageVisualInterval(before, before.VoyageDurationDays - before.RemainingTravelDays,
                    before.VoyageDurationDays);
            }
            var end = ship.VoyageDurationDays - ship.RemainingTravelDays;
            var start = end;
            if (advancing)
            {
                if (before.DockedPortId == ship.OriginPortId) start = 0;
                else if (before.OriginPortId == ship.OriginPortId && before.DestinationPortId == ship.DestinationPortId &&
                    before.VoyageDurationDays == ship.VoyageDurationDays)
                    start = Math.Min(end, before.VoyageDurationDays - before.RemainingTravelDays);
            }
            // Departure and every same-day revision have equal endpoints; rendering cannot invent a day.
            return new VoyageVisualInterval(ship, start, end);
        }
    }
}
