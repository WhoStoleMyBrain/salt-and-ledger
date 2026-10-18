using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SaltAndLedger.Simulation;

internal static class AutomationDemo
{
    private const string North = ShippingScenario.North;
    private const string South = ShippingScenario.South;

    internal static int Run(ContentCatalog content)
    {
        Console.WriteLine("Configured automation: the same manual shipping fixture, prices, production, and travel rules.");
        foreach (var scenario in new[] { "A", "B", "C" })
        {
            var transcript = Simulate(content, scenario, false);
            Require(transcript == Simulate(content, scenario, false), "Automation repeat differed.");
            Require(transcript == Simulate(content, scenario, true), "Reversed port setup differed.");
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
                Require(transcript == Simulate(content, scenario, false), "Automation culture changed output.");
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
            Console.Write(transcript);
        }
        Console.WriteLine("Verified repeated, reversed-port, and tr-TR transcripts, plus equivalent manual states at every tick.");
        Console.WriteLine("Attention metric: manual = 4 explicit action batches; automation = 1 route configuration. Both still need 6 host advances and automation makes 6 evaluations.");
        Console.WriteLine("Manual fault demonstrations add 1 capacity rejection and 1 invalid transit call; automated scenarios issue no rejected trades. No rejected payment is counted as revenue.");
        Console.WriteLine("A completes with gross profit 119; B with 69. C retains 6 unsold logs and stops for no progress; its cash change is not a completed-interval profit.");
        return 0;
    }

    private static string Simulate(ContentCatalog content, string scenario, bool reversePorts)
    {
        var blocked = scenario == "C";
        var maxLogPrice = scenario == "B" ? 8L : 10L;
        var expectedLogs = scenario == "B" ? 2 : 6;
        var world = ShippingScenario.Create(content, reversePorts, blocked ? 0 : 1000);
        var policy = new TradeRoutePolicy(North, South,
            new RouteLeg("logs", 6, 2, maxLogPrice, 19),
            new RouteLeg("planks", 2, 2, 18, 31), cashReserve: 0, allowEmptyReturn: false);
        var manualStates = blocked ? null : ManualStates(content, expectedLogs);
        var output = new StringBuilder();
        output.AppendLine($"Scenario {scenario}: {(blocked ? "destination has no cash" : scenario == "B" ? "restrictive outbound buying limit" : "full outbound shipment")}");
        output.AppendLine(FormattableString.Invariant(
            $"  Policy: {North}->{South}->{North}; logs target=6, minimum=2, maximum buy={maxLogPrice}, minimum sell=19; planks target=2, minimum=2, maximum buy=18, minimum sell=31; reserve=0; empty return=false."));
        output.AppendLine(FormattableString.Invariant(
            $"  Bounds: max ticks={(blocked ? 12 : 6)}, consecutive no-progress limit=3; Southhaven initial cash={(blocked ? 0 : 1000)}."));
        AppendState(output, world.Snapshot());
        var successful = new List<TradeAttempt>();
        var rejected = 0;
        var decisions = 0;
        var departures = 0;
        var expectedCash = blocked ? 1100L : 2100L;

        var run = AutomationRunner.Run(world, policy, blocked ? 12 : 6, 3, step =>
        {
            var decision = step.Decision;
            var result = step.Result;
            decisions++;
            output.AppendLine(FormattableString.Invariant(
                $"Tick {decision.Tick}: stage={decision.Stage}; action={decision.Action}; reason={decision.Reason}; selected={decision.SelectedQuantity}."));
            if (decision.Trade != null)
            {
                var trade = decision.Trade;
                output.AppendLine(FormattableString.Invariant(
                    $"  Command: {trade.Trade.Direction} {trade.Trade.Quantity} {trade.Trade.GoodId} at {trade.PortId}; {Price(decision.Preview!)}."));
            }
            if (decision.Departure != null)
                output.AppendLine($"  Command: sail to {decision.Departure.DestinationPortId}.");
            if (decision.Trade == null && decision.Departure == null)
                output.AppendLine("  Commands: none; advance the ordinary world step.");
            foreach (var attempt in result.TradeAttempts)
            {
                var trade = attempt.Trade;
                output.AppendLine(FormattableString.Invariant(
                    $"  Trade: {attempt.PortId} {trade.Direction} {trade.Quantity} {trade.GoodId}; {trade.Outcome}; executed quantity={trade.ExecutedQuantity}, payment={trade.ExecutedAmount}; {Price(trade)}."));
                if (trade.Outcome == TradeOutcome.Traded) successful.Add(trade);
                else rejected++;
            }
            foreach (var movement in result.MovementEvents)
            {
                if (movement.Kind == ShipMovementKind.Departure) departures++;
                output.AppendLine(FormattableString.Invariant(
                    $"  Movement: {movement.Kind} {movement.OriginPortId}->{movement.DestinationPortId}, remaining={movement.RemainingTicksAfter}."));
            }
            AppendState(output, result.After);
            Require(result.After.Ship.CargoUnits <= ShippingScenario.Capacity, "Automation exceeded capacity.");
            Require(checked(result.After.TraderCash + result.After.Ports[North].MarketCash +
                result.After.Ports[South].MarketCash) == expectedCash, "Automation changed combined cash.");
            if (manualStates != null)
                Require(State(result.After) == manualStates[decisions - 1], "Automatic and manual step states differ.");
        });

        var final = run.After;
        var cost = successful.Where(trade => trade.Direction == TradeDirection.Buy).Sum(trade => trade.ExecutedAmount);
        var revenue = successful.Where(trade => trade.Direction == TradeDirection.Sell).Sum(trade => trade.ExecutedAmount);
        var logsBought = Units(successful, TradeDirection.Buy, "logs");
        var logsSold = Units(successful, TradeDirection.Sell, "logs");
        var planksBought = Units(successful, TradeDirection.Buy, "planks");
        var planksSold = Units(successful, TradeDirection.Sell, "planks");
        Require(rejected == 0 && logsBought == expectedLogs, "Unexpected automated transaction outcome.");
        Require(final.TraderCash == checked(100 - cost + revenue), "Trader accounting differs from executed amounts.");
        var next = RoutePolicyEvaluator.Evaluate(final, policy);
        if (blocked)
        {
            Require(run.StopReason == AutomationStopReason.NoProgress && run.StepsExecuted == 5 &&
                run.ConsecutiveNoProgressTicks == 3 && decisions == 5, "Blocked runner did not stop at its idle bound.");
            Require(final.Tick == 5 && final.Ship.DockedPortId == South && final.Ship.Cargo["logs"] == 6 &&
                final.Ship.Cargo["planks"] == 0 && final.TraderCash == 46 && cost == 54 && revenue == 0 &&
                successful.Count == 1 && departures == 1 && logsSold == 0 && planksBought == 0 && planksSold == 0,
                "Blocked route lost cargo, sailed again, or reported a nonexistent sale.");
            Require(final.Ports[North].Inventory["logs"] == 12 && final.Ports[North].Inventory["planks"] == 0 &&
                final.Ports[North].MarketCash == 1054 && final.Ports[South].Inventory["logs"] == 0 &&
                final.Ports[South].Inventory["planks"] == 1 && final.Ports[South].MarketCash == 0,
                "Blocked final markets differ from the documented scenario.");
            Require(next.Action == AutomationAction.Wait && next.Reason == AutomationReason.InsufficientMarketFunds,
                "Blocked route must explain unavailable destination liquidity.");
        }
        else
        {
            var full = expectedLogs == 6;
            Require(run.StopReason == AutomationStopReason.TickLimit && run.StepsExecuted == 6 && decisions == 6 &&
                final.Tick == 6 && final.Ship.DockedPortId == North && final.Ship.CargoUnits == 0 && departures == 2,
                "Completed automation changed the six-tick itinerary.");
            Require(successful.Count == 4 && logsSold == expectedLogs && planksBought == 2 && planksSold == 2 &&
                cost == (full ? 89 : 51) && revenue == (full ? 208 : 120) && revenue - cost == (full ? 119 : 69),
                "Measured automation costs, revenues, or gross profit changed.");
            Require(final.TraderCash == (full ? 219 : 169) && final.Ports[North].MarketCash == (full ? 990 : 952) &&
                final.Ports[South].MarketCash == (full ? 891 : 979) &&
                final.Ports[North].Inventory["logs"] == (full ? 12 : 16) && final.Ports[North].Inventory["planks"] == 2 &&
                final.Ports[South].Inventory["logs"] == 0 && final.Ports[South].Inventory["planks"] == (full ? 2 : 0),
                "Final automation accounts differ from the manual comparison.");
            Require(next.Action == AutomationAction.Wait && next.Reason == AutomationReason.BuyPriceLimit,
                "A subsequent expedition should wait at the depleted source's higher ask.");
            output.AppendLine(FormattableString.Invariant(
                $"  Manual comparison: {expectedLogs} logs then 2 planks; identical states at all 6 ticks; 4 action batches; 1 deliberate rejected trade; 1 invalid transit request (no tick)."));
        }
        output.AppendLine(FormattableString.Invariant(
            $"  Accounting: bought logs={logsBought}, planks={planksBought}; sold logs={logsSold}, planks={planksSold}; cost={cost}; revenue={revenue}; successful trades={successful.Count}; rejected trades={rejected}."));
        output.AppendLine(blocked
            ? "  Realized gross profit: not reported; the expedition retains 6 unsold logs (cash change=-54)."
            : FormattableString.Invariant($"  Realized gross profit={revenue - cost}; completed empty-cargo accounting interval."));
        output.AppendLine(FormattableString.Invariant(
            $"  Stop={run.StopReason}; ticks={run.StepsExecuted}; decisions={decisions}; consecutive no-progress ticks={run.ConsecutiveNoProgressTicks}; host status={(blocked ? "stopped for no progress" : "finished comparison interval")}; policy remains enabled; next action={next.Action}, reason={next.Reason}."));
        return output.ToString();
    }

    private static List<string> ManualStates(ContentCatalog content, int logs)
    {
        var world = ShippingScenario.Create(content);
        var states = new List<string>();
        var outward = world.Step(trades: new[]
        {
            new PortTradeCommand(North, TradeDirection.Buy, "logs", 7),
            new PortTradeCommand(North, TradeDirection.Buy, "logs", logs)
        }, sailCommands: new[] { new SailCommand(South) });
        Require(outward.TradeAttempts[0].Trade.Outcome == TradeOutcome.InsufficientCargoCapacity,
            "The manual capacity demonstration changed.");
        states.Add(State(outward.After));
        var sailing = world.Snapshot();
        var invalid = false;
        try
        {
            world.Step(trades: new[] { new PortTradeCommand(South, TradeDirection.Sell, "logs", logs) });
        }
        catch (InvalidOperationException)
        {
            invalid = true;
        }
        Require(invalid && ReferenceEquals(sailing, world.Snapshot()), "Manual invalid request must preserve the whole snapshot.");
        states.Add(State(world.Step().After));
        states.Add(State(world.Step(trades: new[] { new PortTradeCommand(South, TradeDirection.Sell, "logs", logs) }).After));
        states.Add(State(world.Step(trades: new[] { new PortTradeCommand(South, TradeDirection.Buy, "planks", 2) },
            sailCommands: new[] { new SailCommand(North) }).After));
        states.Add(State(world.Step().After));
        states.Add(State(world.Step(trades: new[] { new PortTradeCommand(North, TradeDirection.Sell, "planks", 2) }).After));
        return states;
    }

    private static int Units(IEnumerable<TradeAttempt> trades, TradeDirection direction, string goodId) =>
        trades.Where(trade => trade.Direction == direction && trade.GoodId == goodId).Sum(trade => trade.ExecutedQuantity);

    private static string Price(TradeAttempt trade) => trade.Price == null ? "unpriced" : FormattableString.Invariant(
        $"first={trade.Price.FirstMarginalPrice}, last={trade.Price.LastMarginalPrice}, total={trade.Price.TotalAmount}");

    private static void AppendState(StringBuilder output, MaritimeWorldSnapshot state) => output.Append(State(state));

    private static string State(MaritimeWorldSnapshot state)
    {
        var output = new StringBuilder();
        var ship = state.Ship;
        var position = ship.DockedPortId ?? $"{ship.OriginPortId}->{ship.DestinationPortId}";
        output.AppendLine(FormattableString.Invariant(
            $"  State {state.Tick}: trader={state.TraderId}, cash={state.TraderCash}; ship={ship.Id}, position={position}, remaining={ship.RemainingTravelTicks}, travel duration={state.TravelTicks}, cargo={ship.CargoUnits}/{ship.CargoCapacity}."));
        output.AppendLine("    Cargo: " + string.Join(", ", ship.Cargo.Select(item => FormattableString.Invariant($"{item.Key}={item.Value}"))));
        foreach (var pair in state.Ports)
        {
            var port = pair.Value;
            output.AppendLine(FormattableString.Invariant($"    {port.Id}: cash={port.MarketCash}; ") +
                string.Join(", ", port.Inventory.Select(item => FormattableString.Invariant($"{item.Key}={item.Value}"))));
            foreach (var good in port.Quotes.Keys)
            {
                var quote = state.GetMarketQuote(port.Id, good);
                output.AppendLine($"      {good} bid/ask={Number(quote.Bid)}/{Number(quote.Ask)}");
            }
            foreach (var building in port.Buildings)
                output.AppendLine(FormattableString.Invariant(
                    $"      Building {building.Id}/{building.BuildingId}/{building.RecipeId}, priority={building.Priority}, enabled={building.ProductionEnabled}."));
        }
        return output.ToString();
    }

    private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "none";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
