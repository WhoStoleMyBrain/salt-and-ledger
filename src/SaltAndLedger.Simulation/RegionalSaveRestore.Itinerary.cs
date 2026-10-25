using System;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    internal static partial class RegionalSaveRestore
    {
        private static void ValidateItineraryCounts(RegionalSaveShip ship)
        {
            Require((ship.Itinerary == null) == (ship.ItineraryProgress == null),
                "An itinerary and its exact progress must be saved together.");
            if (ship.Itinerary == null) return;
            Count(ship.Itinerary.Stops, 2, 4);
            var commands = 0;
            foreach (var stop in ship.Itinerary.Stops)
            {
                Count(stop.Commands, 1, 8);
                commands = checked(commands + stop.Commands.Length);
            }
            Require(commands <= 24, "An itinerary exceeds its bounded command count.");
        }

        private static CargoItineraryPolicy? RestoreItinerary(RegionalWorldSnapshot initial,
            RegionalShipConfiguration ship, RegionalSaveShip saved)
        {
            if (saved.Itinerary == null) return null;
            Require(initial.RunIdentity?.ItineraryVersion == CargoItineraryPolicy.CurrentVersion &&
                initial.UsesPooledCompanyStorage && ship.IsPlayer,
                "Itineraries require a player ship and the explicit pooled v3 itinerary identity.");
            var policy = saved.Itinerary;
            return new CargoItineraryPolicy(policy.Id, policy.Stops.Select(stop =>
                new CargoItineraryStop(stop.PortId, stop.Commands.Select(command => new CargoItineraryCommand(
                    (CargoItineraryCommandKind)command.Kind, command.GoodId, command.MaximumQuantity,
                    command.MinimumQuantity, (CargoItineraryShortagePolicy)command.ShortagePolicy,
                    command.MarginalPriceLimit, command.CashReserve, command.MaximumExpenditure,
                    command.WarehouseReserve)))), policy.Version);
        }

        private static CargoItineraryProgress? RestoreItineraryProgress(RegionalWorldSnapshot initial,
            RegionalSaveShip ship, CargoItineraryPolicy? policy, long day)
        {
            if (policy == null) return null;
            var saved = ship.ItineraryProgress!;
            Require(saved.StopIndex >= 0 && saved.StopIndex < policy.Stops.Count,
                "Saved itinerary stop is outside its policy.");
            var stop = policy.Stops[saved.StopIndex];
            Require(saved.ActionIndex >= 0 && saved.ActionIndex <= stop.Commands.Count,
                "Saved itinerary action is outside its stop.");
            Require(saved.Cycle >= 0 && (BigInteger)saved.Cycle <= (BigInteger)day + 1 && saved.OperationsDay <= day,
                "Saved itinerary progress is outside the run timeline.");
            var progress = new CargoItineraryProgress(saved.StopIndex, saved.ActionIndex, saved.Cycle,
                (CargoItineraryDecisionKind)saved.LastDecision, (CargoItineraryBlockReason)saved.LastReason,
                saved.LastQuantity, saved.OperationsDay, saved.OperationsUsed);
            Require((progress.OperationsDay == -1) == (progress.OperationsUsed == 0),
                "Saved itinerary operation date and consumed budget disagree.");
            Require(progress.OperationsUsed != 0 || (progress.ActionIndex == 0 && progress.Cycle == 0),
                "An itinerary cannot advance instructions or cycles before consuming an operation.");
            Require(progress.LastDecision == CargoItineraryDecisionKind.Execute || progress.LastQuantity == 0,
                "Only a committed executable itinerary command can have a transferred quantity.");
            if (progress.LastDecision == CargoItineraryDecisionKind.Execute)
            {
                Require(progress.ActionIndex > 0 && progress.OperationsUsed > 0 &&
                    progress.LastReason == CargoItineraryBlockReason.None,
                    "Saved executed itinerary command lacks its completed cursor and daily budget.");
                var command = stop.Commands[progress.ActionIndex - 1];
                Require(progress.LastQuantity >= command.MinimumQuantity && progress.LastQuantity <= command.MaximumQuantity,
                    "Saved itinerary quantity is outside the completed command limits.");
            }
            if (progress.LastDecision == CargoItineraryDecisionKind.Skip)
            {
                Require(progress.OperationsUsed > 0 && progress.ActionIndex > 0 && IsItineraryCommandBlock(progress.LastReason),
                    "Saved skipped itinerary instruction lacks its completed cursor, budget or shortage reason.");
                Require(stop.Commands[progress.ActionIndex - 1].ShortagePolicy == CargoItineraryShortagePolicy.Skip,
                    "Saved itinerary skipped an instruction configured to wait.");
            }
            if (progress.LastDecision == CargoItineraryDecisionKind.Depart)
                Require(progress.OperationsUsed > 0 && progress.ActionIndex == 0 && progress.LastReason == CargoItineraryBlockReason.None,
                    "Saved itinerary departure lacks its next-stop cursor or consumed operation.");
            if (progress.LastDecision == CargoItineraryDecisionKind.Wait)
            {
                if (progress.LastReason == CargoItineraryBlockReason.DailyLimit)
                    Require(progress.OperationsUsed == CargoItineraryPolicy.MaximumDailyOperations,
                        "Saved itinerary daily-limit wait has an unspent execution budget.");
                else
                    Require(IsItineraryCommandBlock(progress.LastReason) && progress.ActionIndex < stop.Commands.Count &&
                        stop.Commands[progress.ActionIndex].ShortagePolicy == CargoItineraryShortagePolicy.Wait,
                        "Saved itinerary wait disagrees with its current instruction's shortage policy.");
            }
            if (progress.LastDecision == CargoItineraryDecisionKind.ContinueSailing)
                Require(progress.LastReason == CargoItineraryBlockReason.Sailing, "Saved sailing decision has an inconsistent reason.");
            if (progress.LastDecision == CargoItineraryDecisionKind.Paused)
                Require((progress.LastReason == CargoItineraryBlockReason.None && progress.OperationsUsed == 0) ||
                    progress.LastReason == CargoItineraryBlockReason.Disabled || progress.LastReason == CargoItineraryBlockReason.WrongPort,
                    "Saved itinerary pause has an inconsistent reason.");
            // A player may pause, sail manually away from the route and save intact. Enabled
            // automation, however, can only be at its current stop or sailing its next real leg.
            if (ship.AutomationEnabled)
            {
                if (ship.Dock != null)
                    Require(ship.Dock == stop.PortId, "Enabled itinerary is docked at a mismatched stop.");
                else
                {
                    var previous = policy.Stops[(progress.StopIndex + policy.Stops.Count - 1) % policy.Stops.Count];
                    Require(progress.ActionIndex == 0 && ship.Destination == stop.PortId && ship.Origin == previous.PortId,
                        "Enabled itinerary voyage disagrees with its next stop.");
                }
            }
            return progress;
        }

        private static bool IsItineraryCommandBlock(CargoItineraryBlockReason reason) =>
            reason != CargoItineraryBlockReason.None && reason != CargoItineraryBlockReason.Disabled &&
            reason != CargoItineraryBlockReason.WrongPort && reason != CargoItineraryBlockReason.Sailing &&
            reason != CargoItineraryBlockReason.DailyLimit;
    }
}
