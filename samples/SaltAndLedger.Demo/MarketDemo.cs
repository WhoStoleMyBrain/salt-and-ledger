using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SaltAndLedger.Simulation;

internal static class MarketDemo
{
    internal static int Run(ContentCatalog content)
    {
        var transcript = Simulate(content, false);
        if (!string.Equals(transcript, Simulate(content, false), StringComparison.Ordinal) ||
            !string.Equals(transcript, Simulate(content, true), StringComparison.Ordinal))
            throw new InvalidOperationException("Market demonstration was not deterministic.");
        Console.Write(transcript);
        Console.WriteLine("Verified identical repeat and reversed setup order. Fixed experimental prices; no replenishment.");
        return 0;
    }

    private static string Simulate(ContentCatalog content, bool reverseSetup)
    {
        var quotes = new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 4, 5) };
        var stock = new[] { new KeyValuePair<string, int>("logs", 2), new KeyValuePair<string, int>("planks", 0) };
        var world = new WorldState(content, "harbor", new[] { new BuildingInstance("sawmill_a", "sawmill", "saw_planks") },
            (reverseSetup ? stock.Reverse() : stock).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            market: new MarketConfiguration(reverseSetup ? quotes.Reverse() : quotes, 20, "trader", 7,
                new Dictionary<string, int> { ["logs"] = 4 }));
        var commands = new[]
        {
            new[] { Buy("logs", 1), Sell("logs", 3), Buy("planks", 1) },
            new[] { Buy("planks", 1), Buy("logs", 2), Buy("logs", 3) },
            new[] { Sell("logs", 2), Buy("planks", 1) },
            new[] { Buy("planks", 1), Sell("planks", 2), Buy("planks", 2) }
        };
        var output = new StringBuilder();
        output.AppendLine("Finite market at harbor; trader buys at ask and sells at bid.");
        output.AppendLine("Quotes (bid/ask): logs=2/3, planks=4/5. Cash uses indivisible integer units.");
        AppendState(output, world.Snapshot());
        foreach (var trades in commands)
        {
            var step = world.Step(trades: trades);
            output.AppendLine(FormattableString.Invariant($"Tick {step.Tick}: trades, then production"));
            foreach (var attempt in step.TradeAttempts)
            {
                output.AppendLine(FormattableString.Invariant(
                    $"  {attempt.Direction} {attempt.Quantity} {attempt.GoodId} @ {attempt.UnitPrice}, requested total={attempt.TotalAmount}: {attempt.Outcome}"));
                output.AppendLine(FormattableString.Invariant(
                    $"    goods market {attempt.MarketInventoryChange.Before}->{attempt.MarketInventoryChange.After}, trader {attempt.TraderInventoryChange.Before}->{attempt.TraderInventoryChange.After}; cash market {attempt.MarketCashChange.Before}->{attempt.MarketCashChange.After}, trader {attempt.TraderCashChange.Before}->{attempt.TraderCashChange.After}"));
            }
            foreach (var attempt in step.ProductionAttempts)
            {
                var changes = string.Join(", ", attempt.InventoryChanges.Select(change =>
                    FormattableString.Invariant($"{change.GoodId} {change.Before}->{change.After}")));
                output.AppendLine($"  Production {attempt.BuildingInstanceId}: {attempt.Outcome}; {changes}");
            }
            AppendState(output, step.After);
        }
        var final = world.Snapshot();
        if (final.Tick != 4 || final.Inventory["logs"] != 0 || final.Inventory["planks"] != 1 ||
            final.Trading!.MarketCash != 25 || final.Trading.TraderCash != 2 ||
            final.Trading.TraderInventory["logs"] != 0 || final.Trading.TraderInventory["planks"] != 2)
            throw new InvalidOperationException("Market result differs from the documented scenario.");
        return output.ToString();
    }

    private static TradeCommand Buy(string good, int quantity) => new TradeCommand(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(string good, int quantity) => new TradeCommand(TradeDirection.Sell, good, quantity);

    private static void AppendState(StringBuilder output, WorldSnapshot state)
    {
        var trading = state.Trading!;
        output.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "State {0}: market logs={1}, planks={2}, cash={3}; trader logs={4}, planks={5}, cash={6}",
            state.Tick, state.Inventory["logs"], state.Inventory["planks"], trading.MarketCash,
            trading.TraderInventory["logs"], trading.TraderInventory["planks"], trading.TraderCash));
    }
}
