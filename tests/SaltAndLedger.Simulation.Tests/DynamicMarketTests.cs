using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class DynamicMarketTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static InventoryPricing Curve(InventoryPricingModel model = InventoryPricingModel.Linear) =>
        new InventoryPricing(model, 10, 10, 10, 1, 100, 1);
    private static TradeCommand Buy(string good = "logs", int quantity = 1) => new TradeCommand(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(string good = "logs", int quantity = 1) => new TradeCommand(TradeDirection.Sell, good, quantity);
    private static BuildingInstance Mill(string id = "mill_a", bool enabled = true) =>
        new BuildingInstance(id, "sawmill", "saw_planks", productionEnabled: enabled);
    private static WorldState World(int logs = 10, int traderLogs = 10, int planks = 2,
        long marketCash = 1000, long traderCash = 1000, IEnumerable<BuildingInstance>? buildings = null,
        InventoryPricingModel model = InventoryPricingModel.Linear, MarketQuote[]? quotes = null) =>
        new WorldState(Content(), "harbor", buildings ?? Array.Empty<BuildingInstance>(),
            new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks }, market:
            new MarketConfiguration(quotes ?? new[] { new MarketQuote("logs", Curve(model)),
                new MarketQuote("planks", new InventoryPricing(model, 20, 4, 4, 2, 100, 1)) },
                marketCash, "trader", traderCash, new Dictionary<string, int> { ["logs"] = traderLogs }));

    private static void SameAttempt(TradeAttempt expected, TradeAttempt actual)
    {
        Assert.Equal((expected.Direction, expected.GoodId, expected.Quantity, expected.Outcome),
            (actual.Direction, actual.GoodId, actual.Quantity, actual.Outcome));
        Assert.Equal((expected.Price?.FirstMarginalPrice, expected.Price?.LastMarginalPrice, expected.Price?.TotalAmount),
            (actual.Price?.FirstMarginalPrice, actual.Price?.LastMarginalPrice, actual.Price?.TotalAmount));
        Assert.Equal((expected.MarketInventoryChange.After, expected.TraderInventoryChange.After,
            expected.MarketCashChange.After, expected.TraderCashChange.After),
            (actual.MarketInventoryChange.After, actual.TraderInventoryChange.After,
            actual.MarketCashChange.After, actual.TraderCashChange.After));
        Assert.Equal(BigInteger.Zero, (BigInteger)actual.MarketCashChange.Delta + actual.TraderCashChange.Delta);
        Assert.Equal(0L, (long)actual.MarketInventoryChange.Delta + actual.TraderInventoryChange.Delta);
    }

    [Fact]
    public void LinearBulkPreviewMatchesExecutionAndReportsDifferentMarginalPrices()
    {
        var world = World();
        var before = world.Snapshot();
        var marginal = before.GetMarketQuote("logs");
        Assert.Equal((8L, 11L), (marginal.Bid, marginal.Ask));
        var preview = before.PreviewTrade(Buy(quantity: 3));
        Assert.Same(before, world.Snapshot());
        SameAttempt(preview, before.PreviewTrade(Buy(quantity: 3)));
        Assert.Equal((11L, 13L, 36L), (preview.Price!.FirstMarginalPrice, preview.Price.LastMarginalPrice, preview.Price.TotalAmount));
        Assert.False(preview.Price.IsUniform);
        Assert.NotEqual(preview.UnitPrice * preview.Quantity, preview.TotalAmount);
        var actual = Assert.Single(world.Step(trades: new[] { Buy(quantity: 3) }).TradeAttempts);
        SameAttempt(preview, actual);
        Assert.Equal((3, 36L), (actual.ExecutedQuantity, actual.ExecutedAmount));
        Assert.Equal((7, 13, 1036L, 964L), (world.Snapshot().Inventory["logs"], world.Snapshot().Trading!.TraderInventory["logs"],
            world.Snapshot().Trading!.MarketCash, world.Snapshot().Trading!.TraderCash));
        var after = world.Snapshot().GetMarketQuote("logs");
        Assert.Equal((11L, 14L), (after.Bid, after.Ask));
        Assert.Equal((8L, 11L), (before.GetMarketQuote("logs").Bid, before.GetMarketQuote("logs").Ask));
        Assert.Equal(10, marginal.MarketStock);
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void SplittingAndReversingTradesHaveIdenticalTotalsAndCannotProfit(InventoryPricingModel model)
    {
        var bulk = World(logs: 8, model: model);
        var split = World(logs: 8, model: model);
        var bulkBuy = Assert.Single(bulk.Step(trades: new[] { Buy(quantity: 7) }).TradeAttempts);
        var splitBuy = split.Step(trades: new[] { Buy(quantity: 2), Buy(quantity: 5) });
        Assert.Equal(bulkBuy.TotalAmount, splitBuy.TradeAttempts.Sum(attempt => attempt.TotalAmount));
        var bulkSale = Assert.Single(bulk.Step(trades: new[] { Sell(quantity: 7) }).TradeAttempts);
        var splitSale = split.Step(trades: new[] { Sell(quantity: 3), Sell(quantity: 4) });
        Assert.Equal(bulkSale.TotalAmount, splitSale.TradeAttempts.Sum(attempt => attempt.TotalAmount));
        Assert.Equal(bulk.Snapshot().Trading!.TraderCash, split.Snapshot().Trading!.TraderCash);
        Assert.True(bulk.Snapshot().Trading!.TraderCash <= 1000);
        Assert.Equal(8, bulk.Snapshot().Inventory["logs"]);
        Assert.Equal(10, bulk.Snapshot().Trading!.TraderInventory["logs"]);
        Assert.Equal(2000L, bulk.Snapshot().Trading!.TraderCash + bulk.Snapshot().Trading!.MarketCash);
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void ProductionChangesOnlyRelevantGoodsPricesAndIdleTicksLeavePricesAlone(InventoryPricingModel model)
    {
        var world = World(logs: 6, planks: 2, buildings: new[] { Mill() }, model: model);
        var before = world.Snapshot();
        var after = world.Step().After;
        Assert.True(after.GetMarketQuote("logs").Ask > before.GetMarketQuote("logs").Ask);
        Assert.True(after.GetMarketQuote("planks").Ask < before.GetMarketQuote("planks").Ask);
        Assert.Equal(before.Trading!.MarketCash, after.Trading!.MarketCash);
        var idle = world.Step(new[] { new SetProductionEnabledCommand("mill_a", false) }).After;
        foreach (var good in new[] { "logs", "planks" })
        {
            Assert.Equal(after.GetMarketQuote(good).Bid, idle.GetMarketQuote(good).Bid);
            Assert.Equal(after.GetMarketQuote(good).Ask, idle.GetMarketQuote(good).Ask);
        }
        var planksBeforeTrade = idle.GetMarketQuote("planks");
        world.Step(trades: new[] { Buy() });
        Assert.Equal(planksBeforeTrade.Ask, world.Snapshot().GetMarketQuote("planks").Ask);
        Assert.Equal(planksBeforeTrade.Bid, world.Snapshot().GetMarketQuote("planks").Bid);
    }

    [Fact]
    public void TradesStillPrecedeProductionAndFutureOutputCannotBePurchased()
    {
        var world = World(logs: 0, traderLogs: 2, planks: 0, buildings: new[] { Mill() });
        var step = world.Step(trades: new[] { Sell(quantity: 2), Buy("planks") });
        Assert.Equal(TradeOutcome.Traded, step.TradeAttempts[0].Outcome);
        Assert.Equal(TradeOutcome.InsufficientMarketInventory, step.TradeAttempts[1].Outcome);
        Assert.Null(step.TradeAttempts[1].Price);
        Assert.Equal(ProductionOutcome.Produced, Assert.Single(step.ProductionAttempts).Outcome);
        Assert.NotNull(step.After.GetMarketQuote("planks").Ask);
        Assert.Null(step.After.GetMarketQuote("logs").Ask);
        Assert.Equal(TradeOutcome.Traded, Assert.Single(world.Step(trades: new[] { Buy("planks") }).TradeAttempts).Outcome);
        var competing = World(logs: 2, buildings: new[] { Mill() });
        Assert.Equal(ProductionOutcome.ResourceShortage, Assert.Single(competing.Step(trades: new[] { Buy() }).ProductionAttempts).Outcome);
    }

    [Theory]
    [InlineData(TradeDirection.Buy, 0, 5, 1000, 1000, TradeOutcome.InsufficientMarketInventory)]
    [InlineData(TradeDirection.Sell, 5, 0, 1000, 1000, TradeOutcome.InsufficientTraderInventory)]
    [InlineData(TradeDirection.Buy, 5, 5, 1000, 0, TradeOutcome.InsufficientTraderFunds)]
    [InlineData(TradeDirection.Sell, 5, 5, 0, 1000, TradeOutcome.InsufficientMarketFunds)]
    public void RejectedPreviewAndExecutionAgreeAndLeaveBothAccountsUnchanged(TradeDirection direction,
        int logs, int traderLogs, long marketCash, long traderCash, TradeOutcome outcome)
    {
        var world = World(logs, traderLogs, marketCash: marketCash, traderCash: traderCash);
        var before = world.Snapshot();
        var command = new TradeCommand(direction, "logs", 1);
        var preview = before.PreviewTrade(command);
        var result = world.Step(trades: new[] { command });
        var attempt = Assert.Single(result.TradeAttempts);
        SameAttempt(preview, attempt);
        Assert.Equal(outcome, attempt.Outcome);
        Assert.Equal((0, 0L), (attempt.ExecutedQuantity, attempt.ExecutedAmount));
        Assert.Equal(before.Inventory, result.After.Inventory);
        Assert.Equal(before.Trading!.TraderInventory, result.After.Trading!.TraderInventory);
        Assert.Equal((marketCash, traderCash), (result.After.Trading.MarketCash, result.After.Trading.TraderCash));
        Assert.Equal(outcome == TradeOutcome.InsufficientMarketInventory || outcome == TradeOutcome.InsufficientTraderInventory,
            attempt.Price == null);
    }

    [Fact]
    public void ExactDynamicAffordabilityAndLaterCommandsAfterRejectionWork()
    {
        var world = World(traderCash: 36);
        var result = world.Step(trades: new[] { Buy(quantity: 4), Buy(quantity: 3), Sell(quantity: 3) });
        Assert.Equal(new[] { TradeOutcome.InsufficientTraderFunds, TradeOutcome.Traded, TradeOutcome.Traded },
            result.TradeAttempts.Select(attempt => attempt.Outcome));
        Assert.Equal(0L, result.TradeAttempts[1].TraderCashChange.After);
        Assert.Equal(30L, result.After.Trading!.TraderCash);
        var saleWorld = World(marketCash: 30, logs: 7);
        Assert.Equal(0L, saleWorld.Step(trades: new[] { Sell(quantity: 3) }).After.Trading!.MarketCash);
    }

    [Fact]
    public void BoundaryQuotesAndUnpriceableRejectionsAreExplicit()
    {
        var expensive = new MarketQuote("logs", new InventoryPricing(InventoryPricingModel.Linear,
            long.MaxValue, 1, 0, long.MaxValue, long.MaxValue, 0));
        var empty = World(logs: 0, quotes: new[] { expensive });
        Assert.Null(empty.Snapshot().GetMarketQuote("logs").Ask);
        Assert.Equal(long.MaxValue, empty.Snapshot().GetMarketQuote("logs").Bid);
        var rejected = empty.Snapshot().PreviewTrade(Buy(quantity: 2));
        Assert.Equal(TradeOutcome.InsufficientMarketInventory, rejected.Outcome);
        Assert.Null(rejected.Price); // No impossible path is priced; fixed mode still overflows first.
        Assert.Equal((0L, 0L), (rejected.UnitPrice, rejected.TotalAmount));
        var fixedEmpty = World(logs: 0, quotes: new[] { new MarketQuote("logs", long.MaxValue, long.MaxValue) });
        Assert.Throws<OverflowException>(() => fixedEmpty.Snapshot().PreviewTrade(Buy(quantity: 2)));
        var full = World(logs: int.MaxValue, marketCash: 0);
        Assert.Null(full.Snapshot().GetMarketQuote("logs").Bid);
        Assert.NotNull(full.Snapshot().GetMarketQuote("logs").Ask);
        var before = full.Snapshot();
        Assert.Throws<OverflowException>(() => before.PreviewTrade(Sell()));
        Assert.Throws<OverflowException>(() => full.Step(trades: new[] { Sell() }));
        Assert.Same(before, full.Snapshot());
    }

    [Fact]
    public void PreviewValidatesConfigurationWithoutMutatingTheWorld()
    {
        var noMarket = new WorldState(Content(), "port", Array.Empty<BuildingInstance>()).Snapshot();
        Assert.Throws<InvalidOperationException>(() => noMarket.GetMarketQuote("logs"));
        Assert.Throws<InvalidOperationException>(() => noMarket.PreviewTrade(Buy()));
        var world = World(quotes: new[] { new MarketQuote("logs", Curve()) });
        var before = world.Snapshot();
        Assert.Throws<ArgumentNullException>(() => before.PreviewTrade(null!));
        Assert.Throws<ArgumentNullException>(() => before.GetMarketQuote(null!));
        Assert.Throws<KeyNotFoundException>(() => before.GetMarketQuote("planks"));
        Assert.Throws<KeyNotFoundException>(() => before.PreviewTrade(Buy("unknown")));
        Assert.Throws<ArgumentNullException>(() => new MarketQuote("logs", null!));
        Assert.Throws<InvalidOperationException>(() => before.Trading!.Quotes["logs"].Bid);
        Assert.Throws<InvalidOperationException>(() => before.Trading!.Quotes["logs"].Ask);
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void LaterDynamicTotalOverflowRollsBackCashStockControlsAndTick()
    {
        var quotes = new[] { new MarketQuote("logs", Curve()), new MarketQuote("planks",
            new InventoryPricing(InventoryPricingModel.Linear, long.MaxValue, 1, 0, long.MaxValue, long.MaxValue, 0)) };
        var world = World(quotes: quotes, buildings: new[] { Mill() });
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(new[] { new SetProductionEnabledCommand("mill_a", false) },
            new[] { Buy(), Buy("planks", 2) }));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0L, world.Step(trades: new[] { Buy() }).Tick);
    }

    [Fact]
    public void LaterProductionFailureRollsBackDynamicTradesAndQuotes()
    {
        var world = World(logs: 2, traderLogs: 2, planks: int.MaxValue - 1,
            buildings: new[] { Mill(), Mill("mill_b", false) });
        var before = world.Snapshot();
        var preview = before.PreviewTrade(Sell(quantity: 2));
        Assert.Throws<OverflowException>(() => world.Step(new[] { new SetProductionEnabledCommand("mill_b", true) },
            new[] { Sell(quantity: 2) }));
        Assert.Same(before, world.Snapshot());
        SameAttempt(preview, world.Snapshot().PreviewTrade(Sell(quantity: 2)));
        Assert.Equal(0L, world.Step(trades: new[] { Sell(quantity: 2) }).Tick);
    }

    [Fact]
    public void MalformedLaterTradeAndCreditOverflowsCannotCommitEarlierWork()
    {
        var world = World();
        var before = world.Snapshot();
        Assert.Throws<KeyNotFoundException>(() => world.Step(trades: new[] { Buy(), Buy("unknown") }));
        Assert.Throws<ArgumentException>(() => world.Step(trades: new[] { Buy(), null! }));
        Assert.Same(before, world.Snapshot());
        foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
        {
            var rich = World(marketCash: long.MaxValue, traderCash: long.MaxValue);
            var richBefore = rich.Snapshot();
            Assert.Throws<OverflowException>(() => rich.Step(trades: new[] { new TradeCommand(direction, "logs", 1) }));
            Assert.Same(richBefore, rich.Snapshot());
        }
        var fullTrader = World(traderLogs: int.MaxValue);
        var fullBefore = fullTrader.Snapshot();
        Assert.Throws<OverflowException>(() => fullTrader.Step(trades: new[] { Buy() }));
        Assert.Same(fullBefore, fullTrader.Snapshot());
    }

    [Fact]
    public void MaximumInventoryCanBeQuotedAndTradedWithoutAUnitLoop()
    {
        var world = World(logs: int.MaxValue, traderLogs: 0, marketCash: 0, traderCash: long.MaxValue);
        var before = world.Snapshot();
        var quote = before.PreviewTrade(Buy(quantity: int.MaxValue));
        var actual = Assert.Single(world.Step(trades: new[] { Buy(quantity: int.MaxValue) }).TradeAttempts);
        SameAttempt(quote, actual);
        Assert.Equal(int.MaxValue, actual.ExecutedQuantity);
        Assert.Equal(0, world.Snapshot().Inventory["logs"]);
        Assert.Equal(int.MaxValue, world.Snapshot().Trading!.TraderInventory["logs"]);
        Assert.Null(world.Snapshot().GetMarketQuote("logs").Ask);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void CulturesAndEquivalentSetupOrderDoNotChangeDynamicOutcomes(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var quotes = new[] { new MarketQuote("logs", Curve(InventoryPricingModel.Scarcity)),
                new MarketQuote("planks", Curve()) };
            var first = World(quotes: quotes);
            var second = World(quotes: quotes.Reverse().ToArray());
            for (var tick = 0; tick < 3; tick++)
            {
                var commands = new[] { Buy(quantity: 3), Sell(quantity: 2), Buy("planks") };
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var expected = first.Step(trades: commands);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var actual = second.Step(trades: commands);
                for (var index = 0; index < commands.Length; index++) SameAttempt(expected.TradeAttempts[index], actual.TradeAttempts[index]);
                Assert.Equal(expected.After.Inventory, actual.After.Inventory);
                Assert.Equal(expected.After.GetMarketQuote("logs").Ask, actual.After.GetMarketQuote("logs").Ask);
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
