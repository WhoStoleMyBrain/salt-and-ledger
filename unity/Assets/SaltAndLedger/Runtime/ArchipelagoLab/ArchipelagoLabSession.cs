using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    public sealed class ArchipelagoActivity
    {
        public long Day { get; }
        public string PortId { get; }
        public string ShipId { get; }
        public string Message { get; }

        internal ArchipelagoActivity(long day, string portId, string shipId, string message)
        {
            Day = day;
            PortId = portId;
            ShipId = shipId;
            Message = message;
        }
    }

    /// <summary>
    /// Main-thread host choices and bounded observation. Elapsed presentation time only
    /// requests whole days; all economic changes are made by RegionalWorldState.
    /// </summary>
    public sealed class ArchipelagoLabSession
    {
        public const int MaximumActivity = 60;
        public const int MaximumDaysPerFrame = 4;
        public const double SecondsPerDay = 1.5;

        private RegionalWorldState world;
        private readonly List<ArchipelagoActivity> activity = new List<ArchipelagoActivity>();
        private readonly ReadOnlyCollection<ArchipelagoActivity> activityView;
        private readonly Dictionary<string, string> lastReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        private double accumulatedSeconds;
        private long initialPlayerCash;
        private bool hasPlayerTrade;

        public ContentCatalog Content { get; }
        public RegionalWorldSnapshot Snapshot => world.Snapshot();
        public RegionalShipSnapshot Player => Snapshot.Ships[ArchipelagoScenario.PlayerShipId];
        public TradeRoutePolicy Policy => Player.Policy;
        public RegionalDecision CurrentDecision => world.EvaluateAutomation(Player.Id);
        public IReadOnlyList<ArchipelagoActivity> Activity => activityView;
        public string SelectedPortId { get; private set; }
        public string SelectedShipId { get; private set; }
        public string PendingDestinationId { get; private set; }
        public bool NavigationArmed { get; private set; }
        public bool IsPlaying { get; private set; }
        public int PlaybackSpeed { get; private set; } = 1;
        public double PlaybackInterval => SecondsPerDay / PlaybackSpeed;
        public string LastStatus { get; private set; }
        public long CashChange => checked(Player.Cash - initialPlayerCash);
        public long? ClosedCashProfit => hasPlayerTrade && Player.CargoUnits == 0 ? CashChange : (long?)null;

        public ArchipelagoLabSession(ContentCatalog content)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            activityView = activity.AsReadOnly();
            Reset();
        }

        public void Reset()
        {
            var replacement = ArchipelagoScenario.Create(Content);
            world = replacement;
            initialPlayerCash = Player.Cash;
            hasPlayerTrade = false;
            SelectedPortId = ArchipelagoScenario.North;
            SelectedShipId = null;
            PendingDestinationId = null;
            NavigationArmed = false;
            IsPlaying = false;
            PlaybackSpeed = 1;
            accumulatedSeconds = 0;
            activity.Clear();
            lastReasons.Clear();
            LastStatus = "New archipelago. Paused on day 0. Select a port to inspect, or your ship to plan a voyage.";
            Add(null, null, LastStatus);
        }

        public void SelectPort(string portId)
        {
            var port = Snapshot.Ports[portId];
            SelectedPortId = port.Id;
            SelectedShipId = null;
            PendingDestinationId = null;
            if (NavigationArmed && Player.DockedPortId != null && Player.DockedPortId != portId &&
                HasRoute(Player.DockedPortId, portId))
                PendingDestinationId = portId;
            LastStatus = "Inspecting " + port.Name + ". Selection spends no day.";
        }

        public void SelectShip(string shipId)
        {
            var ship = Snapshot.Ships[shipId];
            SelectedShipId = ship.Id;
            NavigationArmed = ship.IsPlayer;
            PendingDestinationId = null;
            if (ship.DockedPortId != null) SelectedPortId = ship.DockedPortId;
            LastStatus = ship.IsPlayer
                ? "Your ship selected. Click a destination port, inspect the journey, then confirm departure."
                : "Inspecting " + ship.Name + ". NPC ships use their own accounts and policies.";
        }

        public bool HasRoute(string from, string to) => Snapshot.Routes.Any(route =>
            route.OriginPortId == from && route.DestinationPortId == to ||
            route.OriginPortId == to && route.DestinationPortId == from);

        public void Play()
        {
            IsPlaying = true;
            accumulatedSeconds = 0;
            LastStatus = "Playback running. Each whole day resolves merchants, production, consumption and voyages.";
        }

        public void Pause()
        {
            IsPlaying = false;
            accumulatedSeconds = 0;
            LastStatus = "Playback paused. Dockside trades and configuration spend no day.";
        }

        public void SetPlaybackSpeed(int speed)
        {
            if (speed != 1 && speed != 3 && speed != 10)
                throw new ArgumentOutOfRangeException(nameof(speed), "Choose 1, 3 or 10.");
            PlaybackSpeed = speed;
            accumulatedSeconds = 0;
        }

        public int AdvancePlayback(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (!IsPlaying) return 0;
            // A stalled renderer never leaves an unlimited backlog of economic commands.
            accumulatedSeconds = Math.Min(accumulatedSeconds + elapsedSeconds, PlaybackInterval * MaximumDaysPerFrame);
            var days = 0;
            while (accumulatedSeconds >= PlaybackInterval && days < MaximumDaysPerFrame)
            {
                accumulatedSeconds -= PlaybackInterval;
                AdvanceDay();
                days++;
            }
            return days;
        }

        public RegionalResult NextDay()
        {
            Pause();
            return AdvanceDay();
        }

        private RegionalResult AdvanceDay()
        {
            var result = world.AdvanceDay();
            Observe(result);
            LastStatus = "Day " + Number(Snapshot.Day) + " committed. All ports and ships share this day.";
            return result;
        }

        public TradeAttempt PreviewTrade(TradeDirection direction, string goodId, int quantity) =>
            Snapshot.PreviewTrade(Player.Id, SelectedPortId, new TradeCommand(direction, goodId, quantity));

        public RegionalResult Trade(TradeDirection direction, string goodId, int quantity)
        {
            Pause();
            var result = world.ExecuteTrade(Player.Id, SelectedPortId, new TradeCommand(direction, goodId, quantity));
            Observe(result);
            var trade = result.Trades[0].Trade;
            LastStatus = trade.Outcome == TradeOutcome.Traded
                ? (direction == TradeDirection.Buy ? "Bought " : "Sold ") + trade.ExecutedQuantity + " " +
                  Content.GetGood(goodId).Name + " for " + Number(trade.ExecutedAmount) +
                  ". Still day " + Number(Snapshot.Day) + "."
                : "Trade rejected: " + TradingLabText.Outcome(trade.Outcome) + ". No economic state or day changed.";
            return result;
        }

        public RegionalResult Sail()
        {
            Pause();
            if (!NavigationArmed || PendingDestinationId == null)
                throw new InvalidOperationException("Select your ship and a different connected port first.");
            var destination = PendingDestinationId;
            var result = world.Depart(Player.Id, destination);
            Observe(result);
            PendingDestinationId = null;
            LastStatus = "Departed for " + Snapshot.Ports[destination].Name + ". Advance days to travel; departure spent no day.";
            return result;
        }

        public void SetAutomationEnabled(bool enabled)
        {
            Observe(world.SetAutomationEnabled(Player.Id, enabled));
            LastStatus = enabled
                ? "Player route enabled. It acts at the next day boundary or with Run dockside actions."
                : "Player route disabled. Manual orders are available; existing voyages continue when days advance.";
        }

        public void ApplyPolicy(TradeRoutePolicy policy)
        {
            Observe(world.ConfigureAutomation(Player.Id, policy, Player.AutomationEnabled));
            LastStatus = "Route applied. No day, goods or money changed. The next evaluation uses the new policy.";
        }

        public RegionalResult RunDockside()
        {
            Pause();
            var result = world.RunAutomation(Player.Id, 4);
            Observe(result);
            LastStatus = "Up to four dockside decisions resolved without advancing a day. " + CurrentDecision.Reason;
            return result;
        }

        public RegionalDecision DecisionFor(string shipId) => world.EvaluateAutomation(shipId);

        private void Observe(RegionalResult result)
        {
            if (result.Movements.Any(movement => movement.ShipId == ArchipelagoScenario.PlayerShipId &&
                (movement.Kind == ShipMovementKind.Departure || movement.Kind == ShipMovementKind.Arrival)))
                PendingDestinationId = null;
            foreach (var item in result.Trades)
            {
                var trade = item.Trade;
                var ship = result.After.Ships[item.ShipId];
                if (trade.Outcome != TradeOutcome.Traded) continue;
                if (ship.IsPlayer) hasPlayerTrade = true;
                Add(item.PortId, item.ShipId, ship.Name + " " +
                    (trade.Direction == TradeDirection.Buy ? "bought " : "delivered ") + trade.ExecutedQuantity +
                    " " + Content.GetGood(trade.GoodId).Name + " at " + result.After.Ports[item.PortId].Name +
                    " for " + Number(trade.ExecutedAmount) + ".");
            }
            foreach (var production in result.Production)
            {
                var key = "facility/" + production.PortId + "/" + production.FacilityId;
                var reason = production.Outcome.ToString();
                if (production.Outcome == ScheduledProductionOutcome.Produced)
                    Add(production.PortId, null, result.After.Ports[production.PortId].Name + ": " +
                        Content.GetRecipe(production.RecipeId).Name + " (" + string.Join(", ", production.Changes.Select(
                            change => (change.Delta > 0 ? "+" : "") + change.Delta + " " + Content.GetGood(change.GoodId).Name)) + ").");
                else if (production.Outcome != ScheduledProductionOutcome.NotDue &&
                    (!lastReasons.TryGetValue(key, out var previous) || previous != reason))
                    Add(production.PortId, null, result.After.Ports[production.PortId].Name + ": " +
                        Content.GetRecipe(production.RecipeId).Name + " — " + FacilityStatus(production.Outcome) + ".");
                lastReasons[key] = reason;
            }
            foreach (var consumption in result.Consumption)
                Add(consumption.PortId, null, result.After.Ports[consumption.PortId].Name +
                    (consumption.UnmetQuantity > 0 ? ": unmet demand " + consumption.UnmetQuantity : ": consumed " + consumption.ConsumedQuantity) +
                    " " + Content.GetGood(consumption.GoodId).Name + ".");
            foreach (var movement in result.Movements)
            {
                if (movement.Kind != ShipMovementKind.Departure && movement.Kind != ShipMovementKind.Arrival) continue;
                Add(movement.DestinationPortId, movement.ShipId, result.After.Ships[movement.ShipId].Name +
                    (movement.Kind == ShipMovementKind.Arrival ? " arrived at " : " departed for ") +
                    result.After.Ports[movement.DestinationPortId].Name + ".");
            }
            foreach (var decision in result.Decisions)
            {
                var key = "ship/" + decision.ShipId;
                var reason = decision.Action + ": " + decision.Reason;
                if (!lastReasons.TryGetValue(key, out var previous) || previous != reason)
                    Add(null, decision.ShipId, result.After.Ships[decision.ShipId].Name + ": " + reason);
                lastReasons[key] = reason;
            }
        }

        private void Add(string portId, string shipId, string message)
        {
            activity.Insert(0, new ArchipelagoActivity(Snapshot.Day, portId, shipId, message));
            if (activity.Count > MaximumActivity) activity.RemoveAt(activity.Count - 1);
        }

        public static string FacilityStatus(ScheduledProductionOutcome outcome)
        {
            switch (outcome)
            {
                case ScheduledProductionOutcome.NotDue: return "scheduled; not due yet";
                case ScheduledProductionOutcome.Disabled: return "production disabled";
                case ScheduledProductionOutcome.Produced: return "last batch completed";
                case ScheduledProductionOutcome.InputShortage: return "waiting for inputs";
                case ScheduledProductionOutcome.OutputLimit: return "waiting for output stock space";
                default: return outcome.ToString();
            }
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
