using System;

namespace SaltAndLedger.Simulation
{
    /// <summary>Sequential execution of immutable policy commands through the ordinary world step.</summary>
    public static class TradeRouteAutomation
    {
        public static MaritimeStepResult Execute(MaritimeWorldState world, AutomationDecision decision)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            if (!ReferenceEquals(world.Snapshot(), decision.Before))
                throw new InvalidOperationException("The automation decision is stale or belongs to another world.");
            if (decision.Trade != null)
            {
                var preview = world.Snapshot().PreviewTrade(decision.Trade.PortId, decision.Trade.Trade);
                if (preview.Outcome != TradeOutcome.Traded || decision.Preview == null ||
                    preview.ExecutedQuantity != decision.Preview.ExecutedQuantity ||
                    preview.ExecutedAmount != decision.Preview.ExecutedAmount)
                    throw new InvalidOperationException("The automation trade no longer matches its successful preview.");
            }
            // No caller-owned command enumeration, callback, or intervening manual action.
            // Waits and pauses also advance a normal empty tick when explicitly executed.
            return world.Step(trades: decision.Trade == null ? null : new[] { decision.Trade },
                sailCommands: decision.Departure == null ? null : new[] { decision.Departure });
        }

        public static AutomationStep Advance(MaritimeWorldState world, TradeRoutePolicy policy, bool enabled = true)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var decision = RoutePolicyEvaluator.Evaluate(world.Snapshot(), policy, enabled);
            return new AutomationStep(decision, Execute(world, decision));
        }
    }
}
