using System;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldState
    {
        public RegionalResult ConfigureItinerary(string shipId, CargoItineraryPolicy policy, bool enabled = false,
            RegionalWorldSnapshot? expectedSnapshot = null) => Mutate(work =>
        {
            EnsureCurrent(expectedSnapshot);
            CargoItineraryEvaluator.ValidatePolicy(state, shipId, policy);
            var ship = work.Ships[shipId];
            if (policy.EquivalentTo(ship.Itinerary))
            {
                if (enabled) EnsureItineraryLocation(ship);
                if (ship.AutomationEnabled == enabled) return;
                ship.AutomationEnabled = enabled;
                work.Changed = true;
                return;
            }
            EnsureItineraryReplacement(ship, work.Day);
            var stop = Enumerable.Range(0, policy.Stops.Count).FirstOrDefault(i => policy.Stops[i].PortId == ship.Dock);
            if (ship.Dock == null || policy.Stops[stop].PortId != ship.Dock)
                throw new InvalidOperationException("Apply an itinerary while docked at one of its stops; cargo is preserved.");
            ship.Policy = null; ship.IndustrialPolicy = null;
            ship.Itinerary = policy;
            ship.ItineraryProgress = new CargoItineraryProgress(stop);
            ship.AutomationEnabled = enabled;
            work.Changed = true;
        });

        private static void EnsureItineraryReplacement(WorkingShip ship, long day)
        {
            if (ship.AutomationEnabled || ship.Dock == null)
                throw new InvalidOperationException("Enable manual control and dock before replacing the itinerary; the existing route and cargo are retained.");
            if (ship.ItineraryProgress == null || ship.Itinerary == null) return;
            var progress = ship.ItineraryProgress;
            if (progress.ActionIndex != 0 && progress.ActionIndex != ship.Itinerary.Stops[progress.StopIndex].Commands.Count)
                throw new InvalidOperationException("Finish this partially executed stop before replacing its orders. Manual control can supply missing cargo or funds.");
            if (progress.OperationsDay == day && progress.OperationsUsed != 0)
                throw new InvalidOperationException("Advance a day before replacing orders already executed today; replacement cannot reset the daily operation limit.");
        }

        private static void EnsureItineraryLocation(WorkingShip ship)
        {
            var policy = ship.Itinerary!;
            var progress = ship.ItineraryProgress!;
            var target = policy.Stops[progress.StopIndex].PortId;
            if (ship.Dock == target) return;
            var previous = policy.Stops[(progress.StopIndex + policy.Stops.Count - 1) % policy.Stops.Count].PortId;
            if (ship.Dock == null && ship.Origin == previous && ship.Destination == target && progress.ActionIndex == 0) return;
            throw new InvalidOperationException("Return to the itinerary's next stop before enabling it; manual travel did not reset its progress.");
        }

        private sealed partial class WorkingWorld
        {
            private void ApplyItineraryDecision(CargoItineraryDecision decision)
            {
                var ship = Ships[decision.ShipId];
                var policy = ship.Itinerary ?? throw new InvalidOperationException("No itinerary is applied.");
                var progress = ship.ItineraryProgress!;
                if (progress.StopIndex != decision.StopIndex || progress.ActionIndex != decision.ActionIndex)
                    throw new InvalidOperationException("The itinerary cursor changed; reevaluate the current snapshot.");
                var operation = decision.Kind == CargoItineraryDecisionKind.Execute || decision.Kind == CargoItineraryDecisionKind.Skip ||
                    decision.Kind == CargoItineraryDecisionKind.Depart;
                var used = progress.OperationsDay == Day ? progress.OperationsUsed : 0;
                if (operation && (!ship.AutomationEnabled || used >= CargoItineraryPolicy.MaximumDailyOperations ||
                    ship.Dock != policy.Stops[progress.StopIndex].PortId))
                    throw new InvalidOperationException("This itinerary action is not permitted at this dock or within today's budget.");
                if (decision.Trade != null)
                {
                    var command = decision.Trade;
                    var preview = View().PreviewTrade(decision.ShipId, command.PortId, command.Trade);
                    if (preview.Outcome != TradeOutcome.Traded || decision.Preview == null ||
                        preview.ExecutedAmount != decision.Preview.ExecutedAmount || preview.ExecutedQuantity != decision.SelectedQuantity)
                        throw new InvalidOperationException("The itinerary trade no longer matches its exact preview.");
                    var actual = Trade(decision.ShipId, command.PortId, command.Trade);
                    if (actual.Outcome != TradeOutcome.Traded) throw new InvalidOperationException("A validated itinerary trade must execute.");
                }
                if (decision.Transfer != null)
                {
                    var command = decision.Transfer;
                    Transfer(ship.Configuration.OwnerId, decision.ShipId, command.FacilityId, command.Direction, command.GoodId, command.Quantity);
                    if (Transfers[Transfers.Count - 1].Outcome != TransferOutcome.Transferred)
                        throw new InvalidOperationException("A validated itinerary transfer must execute.");
                }
                var stop = progress.StopIndex;
                var action = progress.ActionIndex;
                var cycle = progress.Cycle;
                if (decision.Kind == CargoItineraryDecisionKind.Execute || decision.Kind == CargoItineraryDecisionKind.Skip) action++;
                if (decision.Departure != null)
                {
                    if (action != policy.Stops[stop].Commands.Count) throw new InvalidOperationException("Finish all stop instructions before departure.");
                    Depart(decision.ShipId, decision.Departure.DestinationPortId);
                    stop = (stop + 1) % policy.Stops.Count;
                    action = 0;
                    if (stop == 0) cycle = checked(cycle + 1);
                }
                var nextDay = operation ? Day : progress.OperationsDay;
                var nextUsed = operation ? checked(used + 1) : progress.OperationsUsed;
                if (stop == progress.StopIndex && action == progress.ActionIndex && cycle == progress.Cycle &&
                    decision.Kind == progress.LastDecision && decision.BlockReason == progress.LastReason &&
                    decision.SelectedQuantity == progress.LastQuantity && nextDay == progress.OperationsDay && nextUsed == progress.OperationsUsed) return;
                ship.ItineraryProgress = new CargoItineraryProgress(stop, action, cycle, decision.Kind, decision.BlockReason,
                    decision.SelectedQuantity, nextDay, nextUsed);
                Changed = true;
            }
        }
    }
}
