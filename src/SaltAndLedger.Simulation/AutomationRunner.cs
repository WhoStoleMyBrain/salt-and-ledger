using System;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public enum AutomationStopReason
    {
        TickLimit,
        NoProgress,
        Paused,
        Disabled
    }

    /// <summary>A bounded host observation, not persistent policy state or a history of decisions.</summary>
    public sealed class AutomationRunResult
    {
        public AutomationStopReason StopReason { get; }
        public int StepsExecuted { get; }
        public int ConsecutiveNoProgressTicks { get; }
        public MaritimeWorldSnapshot Before { get; }
        public MaritimeWorldSnapshot After { get; }
        public AutomationDecision? LastDecision { get; }

        internal AutomationRunResult(AutomationStopReason stopReason, int stepsExecuted,
            int consecutiveNoProgressTicks, MaritimeWorldSnapshot before, MaritimeWorldSnapshot after,
            AutomationDecision? lastDecision)
        {
            StopReason = stopReason;
            StepsExecuted = stepsExecuted;
            ConsecutiveNoProgressTicks = consecutiveNoProgressTicks;
            Before = before;
            After = after;
            LastDecision = lastDecision;
        }
    }

    /// <summary>Optional synchronous host loop over ordinary atomic maritime steps.</summary>
    public static class AutomationRunner
    {
        /// <summary>
        /// Observe at most maxTicks steps, stopping earlier for disabled/paused policy or
        /// consecutive steps without trade, movement, or changed production inventory.
        /// The observer runs after commit, must not mutate the world, and owns any retained history.
        /// Observer exceptions propagate after that step has committed; a run is not one transaction.
        /// </summary>
        public static AutomationRunResult Run(MaritimeWorldState world, TradeRoutePolicy policy,
            int maxTicks, int maxNoProgressTicks, Action<AutomationStep>? observer = null, bool enabled = true)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (maxTicks <= 0) throw new ArgumentOutOfRangeException(nameof(maxTicks));
            if (maxNoProgressTicks <= 0) throw new ArgumentOutOfRangeException(nameof(maxNoProgressTicks));
            var before = world.Snapshot();
            var steps = 0;
            var noProgress = 0;
            AutomationDecision? decision = null;
            while (steps < maxTicks)
            {
                decision = RoutePolicyEvaluator.Evaluate(world.Snapshot(), policy, enabled);
                if (decision.Action == AutomationAction.Pause)
                    return new AutomationRunResult(decision.Reason == AutomationReason.Disabled
                        ? AutomationStopReason.Disabled : AutomationStopReason.Paused,
                        steps, noProgress, before, world.Snapshot(), decision);

                var result = TradeRouteAutomation.Execute(world, decision);
                steps = checked(steps + 1);
                var meaningfulProgress = result.MovementEvents.Count != 0 ||
                    result.TradeAttempts.Any(attempt => attempt.Trade.Outcome == TradeOutcome.Traded) ||
                    result.ProductionAttempts.Any(attempt => attempt.Production.InventoryChanges.Count != 0);
                noProgress = meaningfulProgress ? 0 : checked(noProgress + 1);
                observer?.Invoke(new AutomationStep(decision, result));
                if (!ReferenceEquals(world.Snapshot(), result.After))
                    throw new InvalidOperationException("The automation observer must not mutate the world.");
                if (noProgress >= maxNoProgressTicks)
                    return new AutomationRunResult(AutomationStopReason.NoProgress, steps, noProgress,
                        before, result.After, decision);
            }
            return new AutomationRunResult(AutomationStopReason.TickLimit, steps, noProgress,
                before, world.Snapshot(), decision);
        }
    }
}
