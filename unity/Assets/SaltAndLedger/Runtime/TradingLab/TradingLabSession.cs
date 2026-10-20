using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    public sealed class LabActivity
    {
        public long Tick { get; }
        public string Kind { get; }
        public string Message { get; }

        internal LabActivity(long tick, string kind, string message)
        {
            Tick = tick;
            Kind = kind;
            Message = message;
        }
    }

    /// <summary>
    /// Sequential Unity host interaction state. Every economic transition remains a normal
    /// maritime step; policy, enablement, messages and bounded history belong to this host.
    /// </summary>
    public sealed class TradingLabSession
    {
        public const int MaximumAdvanceTicks = 20;
        public const int MaximumActivity = 40;
        public const int NoProgressLimit = 3;

        private MaritimeWorldState world;
        private readonly List<LabActivity> activity = new List<LabActivity>();
        private readonly ReadOnlyCollection<LabActivity> activityView;
        private readonly Dictionary<string, ProductionOutcome> lastProduction =
            new Dictionary<string, ProductionOutcome>(StringComparer.Ordinal);
        private string lastDecisionSummary;
        private bool hasSuccessfulTrade;

        public ContentCatalog Content { get; }
        public TradingLabScenario Scenario { get; private set; }
        public MaritimeWorldSnapshot Snapshot => world.Snapshot();
        public TradeRoutePolicy Policy { get; private set; }
        public bool AutomationEnabled { get; private set; }
        public AutomationDecision CurrentDecision => RoutePolicyEvaluator.Evaluate(Snapshot, Policy, AutomationEnabled);
        public IReadOnlyList<LabActivity> Activity => activityView;
        public string LastStatus { get; private set; }
        public long CashChange => checked(Snapshot.TraderCash - TradingLabScenarios.InitialTraderCash);

        // Starting cargo is empty and trades are the only cash flow. Close the comparison
        // only back at its starting port with no unsold trader goods; do not value cargo.
        public long? RealizedGrossProfit => hasSuccessfulTrade && Snapshot.Ship.CargoUnits == 0 &&
            Snapshot.Ship.DockedPortId == TradingLabScenarios.North ? CashChange : (long?)null;

        public TradingLabSession(ContentCatalog content, TradingLabScenario scenario = TradingLabScenario.Standard)
        {
            // Preserve the historical two-good scene while reusing the same canonical definition objects.
            Content = LegacyContent.Timber(content ?? throw new ArgumentNullException(nameof(content)));
            activityView = activity.AsReadOnly();
            Reset(scenario);
        }

        public void Reset(TradingLabScenario scenario)
        {
            // Construct before replacing anything: invalid setup must preserve the session.
            var nextWorld = TradingLabScenarios.CreateWorld(Content, scenario);
            var nextPolicy = TradingLabScenarios.CreatePolicy(scenario == TradingLabScenario.Restrictive);
            world = nextWorld;
            Policy = nextPolicy;
            Scenario = scenario;
            AutomationEnabled = false;
            hasSuccessfulTrade = false;
            activity.Clear();
            lastProduction.Clear();
            lastDecisionSummary = null;
            LastStatus = "New scenario at tick 0. Automation is disabled. Reset replaces the world; it is not an in-world transfer.";
            AddActivity(0, "Scenario", LastStatus);
        }

        public void SetAutomationEnabled(bool enabled)
        {
            if (AutomationEnabled == enabled) return;
            AutomationEnabled = enabled;
            lastDecisionSummary = null;
            LastStatus = enabled ? "Automation enabled. Advance a tick to act; no time has passed." :
                "Automation paused. Manual controls are available; no time has passed.";
            AddActivity(Snapshot.Tick, "Policy", LastStatus);
        }

        public void ApplyPolicy(TradeRoutePolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            // Resolve references before installing; evaluation itself is read-only.
            RoutePolicyEvaluator.Evaluate(Snapshot, policy, AutomationEnabled);
            Policy = policy;
            lastDecisionSummary = null;
            LastStatus = "Policy applied. Markets, cargo, and tick are unchanged; the next action uses a fresh decision.";
            AddActivity(Snapshot.Tick, "Policy", LastStatus);
        }

        public TradeAttempt PreviewTrade(string portId, TradeDirection direction, string goodId, int quantity)
        {
            return Snapshot.PreviewTrade(portId, new TradeCommand(direction, goodId, quantity));
        }

        public MaritimeStepResult ExecuteTrade(string portId, TradeDirection direction, string goodId,
            int quantity, bool departAfter = false)
        {
            EnsureManual();
            var before = Snapshot;
            if (before.Ship.DockedPortId != portId)
                throw new InvalidOperationException("Manual trading requires the ship to be docked at the selected port.");
            var command = new PortTradeCommand(portId, direction, goodId, quantity);
            SailCommand departure = null;
            if (departAfter)
            {
                if (direction != TradeDirection.Buy)
                    throw new ArgumentException("The combined action supports purchases only.", nameof(direction));
                var preview = before.PreviewTrade(portId, command.Trade);
                if (preview.Outcome != TradeOutcome.Traded)
                    throw new InvalidOperationException("Buy and depart cancelled: " + TradingLabText.Outcome(preview.Outcome) + ". No tick was consumed.");
                departure = new SailCommand(portId == TradingLabScenarios.North ?
                    TradingLabScenarios.South : TradingLabScenarios.North);
                if (!ReferenceEquals(before, Snapshot))
                    throw new InvalidOperationException("The purchase preview is stale. Preview again before departure.");
                // No callback or externally accessible world can intervene before the normal step.
                var fresh = Snapshot.PreviewTrade(portId, command.Trade);
                if (fresh.Outcome != TradeOutcome.Traded || fresh.ExecutedQuantity != preview.ExecutedQuantity ||
                    fresh.ExecutedAmount != preview.ExecutedAmount)
                    throw new InvalidOperationException("The purchase changed. Buy and depart cancelled without advancing time.");
            }
            var result = world.Step(trades: new[] { command },
                sailCommands: departure == null ? null : new[] { departure });
            Observe(result);
            var attempt = result.TradeAttempts[0].Trade;
            LastStatus = attempt.Outcome == TradeOutcome.Traded ?
                $"{(direction == TradeDirection.Buy ? "Bought" : "Sold")} {quantity} {Content.GetGood(goodId).Name} for {Number(attempt.ExecutedAmount)}. Tick {result.After.Tick}." :
                $"Trade rejected: {TradingLabText.Outcome(attempt.Outcome)}. The ordinary tick still completed production and travel.";
            return result;
        }

        public MaritimeStepResult Sail(string destinationPortId)
        {
            EnsureManual();
            var result = world.Step(sailCommands: new[] { new SailCommand(destinationPortId) });
            Observe(result);
            LastStatus = $"Departure consumed one tick. Sailing to {TradingLabText.PortName(destinationPortId)}; trade after committed arrival.";
            return result;
        }

        /// <summary>Exactly one ordinary tick, with the current policy's commands when enabled.</summary>
        public MaritimeStepResult NextTick()
        {
            MaritimeStepResult result;
            if (AutomationEnabled)
            {
                var decision = CurrentDecision;
                result = TradeRouteAutomation.Execute(world, decision);
                ObserveDecision(decision, result.After.Tick);
            }
            else
            {
                result = world.Step();
            }
            Observe(result);
            LastStatus = $"Advanced exactly one tick to {result.After.Tick}.";
            return result;
        }

        public int AdvanceTicks(int ticks)
        {
            ValidateTickCount(ticks);
            if (AutomationEnabled) return AdvanceAutomation(ticks).StepsExecuted;
            for (var i = 0; i < ticks; i++) NextTick();
            LastStatus = $"Advanced {ticks} ordinary ticks to {Snapshot.Tick}.";
            return ticks;
        }

        public AutomationRunResult AdvanceAutomation(int ticks)
        {
            ValidateTickCount(ticks);
            var result = AutomationRunner.Run(world, Policy, ticks, NoProgressLimit, step =>
            {
                ObserveDecision(step.Decision, step.Result.After.Tick);
                Observe(step.Result);
            }, AutomationEnabled);
            LastStatus = $"Automation advanced {result.StepsExecuted} tick(s). {StopReason(result.StopReason)}";
            AddActivity(Snapshot.Tick, "Run", LastStatus);
            return result;
        }

        private void Observe(MaritimeStepResult result)
        {
            foreach (var portTrade in result.TradeAttempts)
            {
                var trade = portTrade.Trade;
                var successful = trade.Outcome == TradeOutcome.Traded;
                hasSuccessfulTrade |= successful;
                var verb = trade.Direction == TradeDirection.Buy ? "Bought" : "Sold";
                var detail = successful ?
                    $"{verb} {trade.ExecutedQuantity} {Content.GetGood(trade.GoodId).Name} at {TradingLabText.PortName(portTrade.PortId)} for {Number(trade.ExecutedAmount)} (first {Number(trade.Price.FirstMarginalPrice)}, last {Number(trade.Price.LastMarginalPrice)})." :
                    $"{TradingLabText.PortName(portTrade.PortId)} rejected {trade.Direction.ToString().ToLowerInvariant()} {trade.Quantity} {Content.GetGood(trade.GoodId).Name}: {TradingLabText.Outcome(trade.Outcome)}.";
                AddActivity(result.After.Tick, successful ? "Trade" : "Rejected", detail);
            }
            foreach (var production in result.ProductionAttempts)
            {
                var attempt = production.Production;
                var key = production.PortId + "/" + attempt.BuildingInstanceId;
                if (attempt.Outcome == ProductionOutcome.Produced)
                {
                    var changes = string.Join(", ", attempt.InventoryChanges.Select(change =>
                        $"{(change.Delta > 0 ? "+" : "")}{change.Delta} {Content.GetGood(change.GoodId).Name}"));
                    AddActivity(result.After.Tick, "Production", $"{TradingLabText.PortName(production.PortId)} {Content.GetBuilding(attempt.BuildingId).Name}: {changes}. Quotes now reflect the changed stock.");
                }
                else if (!lastProduction.TryGetValue(key, out var previous) || previous != attempt.Outcome)
                {
                    AddActivity(result.After.Tick, "Production", $"{TradingLabText.PortName(production.PortId)} {Content.GetBuilding(attempt.BuildingId).Name}: insufficient inputs; no batch produced.");
                }
                lastProduction[key] = attempt.Outcome;
            }
            foreach (var movement in result.MovementEvents)
            {
                if (movement.Kind == ShipMovementKind.Departure)
                    AddActivity(result.After.Tick, "Travel", $"Departed {TradingLabText.PortName(movement.OriginPortId)} for {TradingLabText.PortName(movement.DestinationPortId)}.");
                else if (movement.Kind == ShipMovementKind.Arrival)
                    AddActivity(result.After.Tick, "Travel", $"Arrived at {TradingLabText.PortName(movement.DestinationPortId)}. Trading is available on the next step.");
            }
        }

        private void ObserveDecision(AutomationDecision decision, long committedTick)
        {
            var summary = $"{TradingLabText.Stage(decision.Stage)}: {TradingLabText.Action(decision.Action)}. {TradingLabText.Reason(decision.Reason)}";
            if (decision.Trade != null)
                summary += $" {decision.SelectedQuantity} {Content.GetGood(decision.Trade.Trade.GoodId).Name}, total {Number(decision.Preview.ExecutedAmount)}.";
            if (summary == lastDecisionSummary) return;
            lastDecisionSummary = summary;
            AddActivity(committedTick, "Automation", summary);
        }

        private void EnsureManual()
        {
            if (AutomationEnabled)
                throw new InvalidOperationException("Pause automation before using manual trade or sailing controls. Pausing consumes no tick.");
        }

        private void AddActivity(long tick, string kind, string message)
        {
            activity.Insert(0, new LabActivity(tick, kind, message));
            if (activity.Count > MaximumActivity) activity.RemoveAt(activity.Count - 1);
        }

        private static void ValidateTickCount(int ticks)
        {
            if (ticks < 1 || ticks > MaximumAdvanceTicks)
                throw new ArgumentOutOfRangeException(nameof(ticks), $"Choose between 1 and {MaximumAdvanceTicks} ticks.");
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

        private static string StopReason(AutomationStopReason reason)
        {
            switch (reason)
            {
                case AutomationStopReason.TickLimit: return "Requested tick limit reached.";
                case AutomationStopReason.NoProgress: return "Stopped after three ticks without trade, movement, or changed production. Inspect the waiting reason; no replenishment is automatic.";
                case AutomationStopReason.Disabled: return "Automation is disabled; no tick consumed.";
                case AutomationStopReason.Paused: return "Policy needs intervention; the paused decision consumed no tick.";
                default: return reason.ToString();
            }
        }
    }
}
