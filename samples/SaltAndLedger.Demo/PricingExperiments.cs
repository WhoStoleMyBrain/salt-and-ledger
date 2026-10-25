using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SaltAndLedger.Simulation;

/// <summary>Finite, reproducible comparisons. All quotes come from the simulation API.</summary>
internal static class PricingExperiments
{
    private const string DefaultReportPath = "docs/experiments/pricing-results.json";
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = HistoricalReportJson.Resolver(), WriteIndented = true
    };

    internal static int Run(ContentCatalog content, string? outputPath, bool check)
    {
        var report = Simulate(content);
        var json = Serialize(report);
        if (!string.Equals(json, Serialize(Simulate(content)), StringComparison.Ordinal))
            throw new InvalidOperationException("Pricing experiments changed across repeated runs.");
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            if (!string.Equals(json, Serialize(Simulate(content)), StringComparison.Ordinal))
                throw new InvalidOperationException("Pricing experiments depend on the current culture.");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        var path = outputPath ?? DefaultReportPath;
        if (check)
        {
            if (!File.Exists(path) || !string.Equals(File.ReadAllText(path).Replace("\r\n", "\n"), json, StringComparison.Ordinal))
                throw new InvalidOperationException("Pricing report is stale: regenerate it with --pricing " + path);
        }
        else
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (directory != null) Directory.CreateDirectory(directory);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        Console.WriteLine("Scenario/case                    Model       Action      Stock       Total/paid      Outcome");
        foreach (var row in report.Rows)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-32} {1,-11} {2,-11} {3,-11} {4,-15} {5}", row.Scenario + "/" + row.Case,
                row.Model, row.Direction == null ? row.GoodId : row.Direction + " " + row.RequestedQuantity,
                row.StartingStock + "->" + row.EndingStock, (row.QuotedTotal?.ToString(CultureInfo.InvariantCulture) ?? "-") +
                "/" + row.ExecutedAmount, row.Outcome));
        }
        Console.WriteLine(FormattableString.Invariant(
            $"Verified {report.Rows.Count} observations and {report.AssertionsPassed} invariant checks per run; repeat and tr-TR reports are identical."));
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    private static string Serialize(ExperimentReport report) => JsonSerializer.Serialize(report, JsonOptions).Replace("\r\n", "\n") + "\n";

    private static ExperimentReport Simulate(ContentCatalog content)
    {
        var report = new ExperimentReport();
        foreach (var model in new[] { "Fixed", "Linear", "Scarcity" })
        {
            report.Models.Add(new ModelParameters(model));
            HighInventory(content, model, report);
            ModerateScarcity(content, model, report);
            SevereScarcity(content, model, report);
            Production(content, model, report);
            CyclesAndSplitting(content, model, report);
            Liquidity(content, model, report);
            Extremes(content, model, report);
        }
        return report;
    }

    private static void HighInventory(ContentCatalog content, string model, ExperimentReport report)
    {
        Observe(report, "A", "small", model, World(content, model, 200), Buy(1));
        Observe(report, "A", "large", model, World(content, model, 200), Buy(100));
        Observe(report, "A", "very_abundant", model, World(content, model, 2000), Buy(100));
    }

    private static void ModerateScarcity(ContentCatalog content, string model, ExperimentReport report)
    {
        Observe(report, "B", "small_buy", model, World(content, model, 60), Buy(5));
        Observe(report, "B", "large_buy", model, World(content, model, 60), Buy(25));
        Observe(report, "B", "sale", model, World(content, model, 60), Sell(25));
    }

    private static void SevereScarcity(ContentCatalog content, string model, ExperimentReport report)
    {
        Observe(report, "C", "first_unit", model, World(content, model, 10), Buy(1));
        Observe(report, "C", "half_stock", model, World(content, model, 10), Buy(5));
        var depleted = World(content, model, 10);
        Observe(report, "C", "all_stock", model, depleted, Buy(10));
        Observe(report, "C", "empty_market", model, depleted, Buy(1));
        Observe(report, "C", "limited_buyer_cash", model, World(content, model, 10, traderCash: 2000), Buy(10));
        Observe(report, "C", "last_unit", model, World(content, model, 1), Buy(1));
        foreach (var stock in new[] { 26, 25, 24 })
            Observe(report, "C", "threshold_" + stock, model, World(content, model, stock), Buy(1));
    }

    private static void Production(ContentCatalog content, string model, ExperimentReport report)
    {
        var world = World(content, model, 12, planks: 45, mill: true);
        for (var tick = 0; tick < 4; tick++)
        {
            var before = world.Snapshot();
            var step = world.Step();
            Require(report, step.ProductionAttempts.Single().Outcome == ProductionOutcome.Produced, "Production must complete.");
            Require(report, step.After.Trading!.MarketCash == before.Trading!.MarketCash &&
                step.After.Trading.TraderCash == before.Trading.TraderCash, "Production cannot create cash.");
            foreach (var goodId in new[] { "logs", "planks" })
            {
                CheckQuoteMovement(report, before, step.After, goodId, false);
                report.Rows.Add(Row("D", "production_" + tick, model, before, step.After, goodId, "Produced"));
            }
        }
        var idleBefore = world.Snapshot();
        var idleAfter = world.Step(new[] { new SetProductionEnabledCommand("sawmill_a", false) }).After;
        foreach (var goodId in new[] { "logs", "planks" })
        {
            var start = idleBefore.GetMarketQuote(goodId);
            var end = idleAfter.GetMarketQuote(goodId);
            Require(report, start.Bid == end.Bid && start.Ask == end.Ask, "Time alone cannot move inventory-sensitive quotes.");
            report.Rows.Add(Row("D", "idle", model, idleBefore, idleAfter, goodId, "NoProduction"));
        }
    }

    private static void CyclesAndSplitting(ContentCatalog content, string model, ExperimentReport report)
    {
        var world = World(content, model, 30);
        foreach (var quantity in new[] { 1, 3, 7 })
        {
            var cash = world.Snapshot().Trading!.TraderCash;
            Observe(report, "E", "roundtrip_" + quantity + "_buy", model, world, Buy(quantity));
            Observe(report, "E", "roundtrip_" + quantity + "_sell", model, world, Sell(quantity));
            Require(report, world.Snapshot().Inventory["logs"] == 30 && world.Snapshot().Trading!.TraderCash <= cash,
                "A closed round trip cannot earn cash.");
        }
        var closedCash = world.Snapshot().Trading!.TraderCash;
        var closedCommands = new[] { Buy(4), Buy(3), Sell(2), Sell(5) };
        for (var index = 0; index < closedCommands.Length; index++)
            Observe(report, "E", "closed_cycle_" + index, model, world, closedCommands[index]);
        Require(report, world.Snapshot().Inventory["logs"] == 30 && world.Snapshot().Trading!.TraderCash <= closedCash,
            "A multi-command closed inventory cycle cannot earn cash.");

        foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
        {
            var bulkWorld = World(content, model, 30);
            var splitWorld = World(content, model, 30);
            var bulk = Observe(report, "E", "bulk_" + direction, model, bulkWorld, new TradeCommand(direction, "logs", 7));
            var first = Observe(report, "E", "split_" + direction + "_3", model, splitWorld, new TradeCommand(direction, "logs", 3));
            var last = Observe(report, "E", "split_" + direction + "_4", model, splitWorld, new TradeCommand(direction, "logs", 4));
            Require(report, bulk.ExecutedAmount == checked(first.ExecutedAmount + last.ExecutedAmount), "Splitting changed the total.");
            Require(report, bulkWorld.Snapshot().Trading!.TraderCash == splitWorld.Snapshot().Trading!.TraderCash &&
                bulkWorld.Snapshot().Inventory["logs"] == splitWorld.Snapshot().Inventory["logs"], "Splitting changed final balances.");
        }
        foreach (var stock in new[] { 1, 2000 })
        {
            var boundaryWorld = World(content, model, stock);
            var cash = boundaryWorld.Snapshot().Trading!.TraderCash;
            Observe(report, "E", "boundary_" + stock + "_buy", model, boundaryWorld, Buy(1));
            Observe(report, "E", "boundary_" + stock + "_sell", model, boundaryWorld, Sell(1));
            Require(report, boundaryWorld.Snapshot().Inventory["logs"] == stock &&
                boundaryWorld.Snapshot().Trading!.TraderCash <= cash, "Price clamps introduced a profitable cycle.");
        }
    }

    private static void Liquidity(ContentCatalog content, string model, ExperimentReport report)
    {
        foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
        {
            var command = new TradeCommand(direction, "logs", 5);
            var quote = World(content, model, 60).Snapshot().PreviewTrade(command);
            var total = quote.Price!.TotalAmount;
            var exact = World(content, model, 60,
                marketCash: direction == TradeDirection.Sell ? total : 100000,
                traderCash: direction == TradeDirection.Buy ? total : 100000);
            var shortCash = World(content, model, 60,
                marketCash: direction == TradeDirection.Sell ? total - 1 : 100000,
                traderCash: direction == TradeDirection.Buy ? total - 1 : 100000);
            var accepted = Observe(report, "F", direction + "_exact_cash", model, exact, command);
            var rejected = Observe(report, "F", direction + "_one_cash_short", model, shortCash, command);
            Require(report, accepted.Outcome == "Traded" && rejected.Outcome ==
                (direction == TradeDirection.Buy ? "InsufficientTraderFunds" : "InsufficientMarketFunds"), "Exact liquidity boundary changed.");
        }
    }

    private static void Extremes(ContentCatalog content, string model, ExperimentReport report)
    {
        Observe(report, "G", "maximum_buy_quantity", model,
            World(content, model, int.MaxValue, marketCash: 0, traderCash: long.MaxValue, traderLogs: 0), Buy(int.MaxValue));
        Observe(report, "G", "maximum_sell_quantity", model,
            World(content, model, 0, marketCash: long.MaxValue, traderCash: 0, traderLogs: int.MaxValue), Sell(int.MaxValue));
        Observe(report, "G", "maximum_unit_price", model,
            World(content, model, 1, marketCash: 0, traderCash: long.MaxValue, traderLogs: 0, maximumPrices: true), Buy(1), maximumPrices: true);
        Observe(report, "G", "total_overflow", model,
            World(content, model, 2, marketCash: 0, traderCash: long.MaxValue, maximumPrices: true), Buy(2), true, true);
        Observe(report, "G", "cash_credit_overflow", model,
            World(content, model, 1, marketCash: 1, traderCash: long.MaxValue, maximumPrices: true), Buy(1), true, true);
        Observe(report, "G", "stock_credit_overflow", model,
            World(content, model, int.MaxValue, marketCash: long.MaxValue, traderCash: 0, traderLogs: 1), Sell(1), true);
    }

    private static Observation Observe(ExperimentReport report, string scenario, string caseName, string model,
        WorldState world, TradeCommand command, bool expectOverflow = false, bool maximumPrices = false)
    {
        var before = world.Snapshot();
        var historicalQuote = before.GetMarketQuote(command.GoodId);
        TradeAttempt? preview = null;
        var previewOverflow = false;
        try { preview = before.PreviewTrade(command); }
        catch (OverflowException) { previewOverflow = true; }
        Require(report, ReferenceEquals(world.Snapshot(), before), "Preview mutated the world.");

        StepResult? step = null;
        var executionOverflow = false;
        try { step = world.Step(trades: new[] { command }); }
        catch (OverflowException) { executionOverflow = true; }
        Require(report, executionOverflow == expectOverflow && previewOverflow == executionOverflow,
            "Preview and execution overflow must agree with the experiment.");
        Observation row;
        if (executionOverflow)
        {
            Require(report, ReferenceEquals(before, world.Snapshot()), "Exceptional trade failed to roll back the entire tick.");
            row = Row(scenario, caseName, model, before, before, command.GoodId, "OverflowException");
            row.Direction = command.Direction.ToString();
            row.RequestedQuantity = command.Quantity;
        }
        else
        {
            var attempt = step!.TradeAttempts.Single();
            Require(report, preview != null && SameAttempt(preview, attempt), "Preview differs from execution at unchanged state.");
            var after = step.After;
            CheckQuoteMovement(report, before, after, command.GoodId, maximumPrices);
            if (attempt.Price != null)
                Require(report, command.Direction == TradeDirection.Buy
                    ? attempt.Price.LastMarginalPrice >= attempt.Price.FirstMarginalPrice
                    : attempt.Price.LastMarginalPrice <= attempt.Price.FirstMarginalPrice, "Marginal execution prices move in the wrong direction.");
            Require(report, (BigInteger)before.Inventory[command.GoodId] + before.Trading!.TraderInventory[command.GoodId] ==
                (BigInteger)after.Inventory[command.GoodId] + after.Trading!.TraderInventory[command.GoodId], "Trading did not conserve goods.");
            Require(report, (BigInteger)before.Trading!.MarketCash + before.Trading.TraderCash ==
                (BigInteger)after.Trading!.MarketCash + after.Trading.TraderCash, "Trading did not conserve cash.");
            Require(report, before.GetMarketQuote(command.GoodId).Bid == historicalQuote.Bid &&
                before.GetMarketQuote(command.GoodId).Ask == historicalQuote.Ask, "A historical snapshot quote changed.");
            if (attempt.Outcome != TradeOutcome.Traded)
                Require(report, before.Inventory[command.GoodId] == after.Inventory[command.GoodId] &&
                    before.Trading!.TraderInventory[command.GoodId] == after.Trading!.TraderInventory[command.GoodId] &&
                    before.Trading.MarketCash == after.Trading.MarketCash && before.Trading.TraderCash == after.Trading.TraderCash,
                    "Rejected trade changed balances.");
            row = Row(scenario, caseName, model, before, after, command.GoodId, attempt.Outcome.ToString());
            row.Direction = command.Direction.ToString();
            row.RequestedQuantity = command.Quantity;
            row.FirstMarginalPrice = attempt.Price?.FirstMarginalPrice;
            row.LastMarginalPrice = attempt.Price?.LastMarginalPrice;
            row.QuotedTotal = attempt.Price?.TotalAmount;
            row.UniformExecutionPrice = attempt.Price?.IsUniform;
            row.ExecutedQuantity = attempt.ExecutedQuantity;
            row.ExecutedAmount = attempt.ExecutedAmount;
            row.MarginalExecutionImpact = attempt.Price == null ? null : checked(attempt.Price.LastMarginalPrice - attempt.Price.FirstMarginalPrice);
        }
        row.ParameterSet = maximumPrices ? "maximum" : "standard";
        report.Rows.Add(row);
        return row;
    }

    private static bool SameAttempt(TradeAttempt left, TradeAttempt right) =>
        left.Outcome == right.Outcome && left.Price?.FirstMarginalPrice == right.Price?.FirstMarginalPrice &&
        left.Price?.LastMarginalPrice == right.Price?.LastMarginalPrice && left.Price?.TotalAmount == right.Price?.TotalAmount &&
        left.ExecutedQuantity == right.ExecutedQuantity && left.ExecutedAmount == right.ExecutedAmount &&
        left.MarketInventoryChange.After == right.MarketInventoryChange.After && left.TraderInventoryChange.After == right.TraderInventoryChange.After &&
        left.MarketCashChange.After == right.MarketCashChange.After && left.TraderCashChange.After == right.TraderCashChange.After;

    private static void CheckQuoteMovement(ExperimentReport report, WorldSnapshot before, WorldSnapshot after, string goodId, bool maximumPrices)
    {
        var first = before.GetMarketQuote(goodId);
        var last = after.GetMarketQuote(goodId);
        var parameters = Parameters(goodId, maximumPrices);
        foreach (var price in new[] { first.Bid, first.Ask, last.Bid, last.Ask })
            Require(report, !price.HasValue || (price >= parameters.MinPrice && price <= parameters.MaxPrice), "Quote exceeded its configured bounds.");
        var stockMovement = after.Inventory[goodId].CompareTo(before.Inventory[goodId]);
        foreach (var impact in new[] { Difference(first.Bid, last.Bid), Difference(first.Ask, last.Ask) })
            Require(report, !impact.HasValue || (stockMovement == 0 ? impact == 0 : stockMovement < 0 ? impact >= 0 : impact <= 0),
                "Quote movement contradicts the stock change.");
    }

    private static Observation Row(string scenario, string caseName, string model, WorldSnapshot before,
        WorldSnapshot after, string goodId, string outcome)
    {
        var start = before.GetMarketQuote(goodId);
        var end = after.GetMarketQuote(goodId);
        return new Observation
        {
            Scenario = scenario, Case = caseName, Model = model, GoodId = goodId,
            StartingTick = before.Tick, EndingTick = after.Tick, TargetStock = goodId == "logs" ? 100 : 50,
            StartingStock = before.Inventory[goodId], EndingStock = after.Inventory[goodId],
            StartingTraderStock = before.Trading!.TraderInventory[goodId], EndingTraderStock = after.Trading!.TraderInventory[goodId],
            StartingBid = start.Bid, StartingAsk = start.Ask, EndingBid = end.Bid, EndingAsk = end.Ask,
            BidImpact = Difference(start.Bid, end.Bid), AskImpact = Difference(start.Ask, end.Ask),
            StartingMarketCash = before.Trading.MarketCash, EndingMarketCash = after.Trading.MarketCash,
            MarketCashChange = checked(after.Trading.MarketCash - before.Trading.MarketCash),
            StartingTraderCash = before.Trading.TraderCash, EndingTraderCash = after.Trading.TraderCash,
            TraderCashChange = checked(after.Trading.TraderCash - before.Trading.TraderCash), Outcome = outcome
        };
    }

    private static long? Difference(long? before, long? after) => before.HasValue && after.HasValue ? checked(after.Value - before.Value) : null;

    private static WorldState World(ContentCatalog content, string model, int logs, int planks = 0,
        long marketCash = 100000, long traderCash = 100000, int traderLogs = 100, bool mill = false, bool maximumPrices = false)
    {
        var quotes = new[] { Quote(model, "logs", maximumPrices), Quote(model, "planks", maximumPrices) };
        return new WorldState(content, "experiment", mill ? new[] { new BuildingInstance("sawmill_a", "sawmill", "saw_planks") } : Array.Empty<BuildingInstance>(),
            new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks },
            market: new MarketConfiguration(quotes, marketCash, "trader", traderCash, new Dictionary<string, int> { ["logs"] = traderLogs }));
    }

    private static MarketQuote Quote(string model, string goodId, bool maximum)
    {
        var parameters = Parameters(goodId, maximum);
        if (model == "Fixed") return new MarketQuote(goodId,
            maximum ? long.MaxValue : parameters.ReferencePrice - parameters.Spread,
            maximum ? long.MaxValue : parameters.ReferencePrice + parameters.Spread);
        return new MarketQuote(goodId, new InventoryPricing(model == "Linear" ? InventoryPricingModel.Linear : InventoryPricingModel.Scarcity,
            parameters.ReferencePrice, parameters.TargetStock, parameters.Sensitivity, parameters.MinPrice, parameters.MaxPrice, parameters.Spread));
    }

    private static PriceParameters Parameters(string goodId, bool maximum) => maximum
        ? new PriceParameters(long.MaxValue, 100, long.MaxValue, long.MaxValue, long.MaxValue, 0)
        : goodId == "logs" ? new PriceParameters(100, 100, 80, 5, 400, 2) : new PriceParameters(200, 50, 160, 10, 800, 4);

    private static TradeCommand Buy(int quantity) => new TradeCommand(TradeDirection.Buy, "logs", quantity);
    private static TradeCommand Sell(int quantity) => new TradeCommand(TradeDirection.Sell, "logs", quantity);

    private static void Require(ExperimentReport report, bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Pricing experiment invariant failed: " + message);
        report.AssertionsPassed++;
    }

    private sealed class ExperimentReport
    {
        public int FormatVersion { get; } = 1;
        public string Description { get; } = "Task 1.2b controlled finite-market experiments; values are hypotheses, not final balance.";
        public List<ModelParameters> Models { get; } = new List<ModelParameters>();
        public List<Observation> Rows { get; } = new List<Observation>();
        public int AssertionsPassed { get; set; }
        public string[] InvariantViolations { get; } = Array.Empty<string>();
    }

    private sealed class ModelParameters
    {
        public string Model { get; }
        public SortedDictionary<string, PriceParameters> Standard { get; } = new SortedDictionary<string, PriceParameters>(StringComparer.Ordinal)
        { ["logs"] = Parameters("logs", false), ["planks"] = Parameters("planks", false) };
        public PriceParameters Maximum { get; } = Parameters("logs", true);
        public string FixedRule { get; } = "Fixed uses reference minus/plus spread; maximum profile uses long.MaxValue on both sides.";
        public ModelParameters(string model) { Model = model; }
    }

    private sealed class PriceParameters
    {
        public long ReferencePrice { get; }
        public int TargetStock { get; }
        public long Sensitivity { get; }
        public long MinPrice { get; }
        public long MaxPrice { get; }
        public long Spread { get; }
        public PriceParameters(long referencePrice, int targetStock, long sensitivity, long minPrice, long maxPrice, long spread)
        {
            ReferencePrice = referencePrice; TargetStock = targetStock; Sensitivity = sensitivity;
            MinPrice = minPrice; MaxPrice = maxPrice; Spread = spread;
        }
    }

    private sealed class Observation
    {
        public string Scenario { get; set; } = "";
        public string Case { get; set; } = "";
        public string Model { get; set; } = "";
        public string ParameterSet { get; set; } = "standard";
        public string GoodId { get; set; } = "";
        public long StartingTick { get; set; }
        public long EndingTick { get; set; }
        public int TargetStock { get; set; }
        public int StartingStock { get; set; }
        public int EndingStock { get; set; }
        public int StartingTraderStock { get; set; }
        public int EndingTraderStock { get; set; }
        public long? StartingBid { get; set; }
        public long? StartingAsk { get; set; }
        public string? Direction { get; set; }
        public int RequestedQuantity { get; set; }
        public long? FirstMarginalPrice { get; set; }
        public long? LastMarginalPrice { get; set; }
        public long? QuotedTotal { get; set; }
        public bool? UniformExecutionPrice { get; set; }
        public int ExecutedQuantity { get; set; }
        public long ExecutedAmount { get; set; }
        public string Outcome { get; set; } = "";
        public long? EndingBid { get; set; }
        public long? EndingAsk { get; set; }
        public long? BidImpact { get; set; }
        public long? AskImpact { get; set; }
        public long? MarginalExecutionImpact { get; set; }
        public long StartingMarketCash { get; set; }
        public long EndingMarketCash { get; set; }
        public long MarketCashChange { get; set; }
        public long StartingTraderCash { get; set; }
        public long EndingTraderCash { get; set; }
        public long TraderCashChange { get; set; }
    }
}
