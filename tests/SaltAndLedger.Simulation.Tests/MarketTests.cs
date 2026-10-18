using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class MarketTests
{
    private static ContentCatalog Canonical() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static Dictionary<string, int> Stock(int logs = 0, int planks = 0) =>
        new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks };
    private static MarketQuote[] Quotes() => new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 4, 5) };
    private static TradeCommand Buy(string good, int quantity) => new TradeCommand(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(string good, int quantity) => new TradeCommand(TradeDirection.Sell, good, quantity);
    private static BuildingInstance Mill(string id = "mill_a", bool enabled = true) =>
        new BuildingInstance(id, "sawmill", "saw_planks", productionEnabled: enabled);
    private static WorldState World(int marketLogs = 10, int traderLogs = 10, long marketCash = 100,
        long traderCash = 100, IEnumerable<MarketQuote>? quotes = null, int marketPlanks = 0,
        IEnumerable<BuildingInstance>? buildings = null, long tick = 0) =>
        new WorldState(Canonical(), "harbor", buildings ?? Array.Empty<BuildingInstance>(),
            Stock(marketLogs, marketPlanks), tick,
            new MarketConfiguration(quotes ?? Quotes(), marketCash, "trader", traderCash, Stock(traderLogs)));

    private static void Conserved(TradeAttempt attempt)
    {
        Assert.Equal(BigInteger.Zero, (BigInteger)attempt.MarketCashChange.Delta + attempt.TraderCashChange.Delta);
        Assert.Equal(0L, (long)attempt.MarketInventoryChange.Delta + attempt.TraderInventoryChange.Delta);
    }

    [Fact]
    public void PurchasesSalesAndRoundTripTransferActualGoodsAndCash()
    {
        var world = World();
        var before = world.Snapshot();
        var result = world.Step(trades: new[] { Buy("logs", 4), Sell("logs", 4), Buy("planks", 1) });
        var buy = result.TradeAttempts[0];
        Assert.Equal(TradeOutcome.Traded, buy.Outcome);
        Assert.Equal(3L, buy.UnitPrice);
        Assert.Equal(12L, buy.TotalAmount);
        Assert.Equal((10, 6), (buy.MarketInventoryChange.Before, buy.MarketInventoryChange.After));
        Assert.Equal((10, 14), (buy.TraderInventoryChange.Before, buy.TraderInventoryChange.After));
        Assert.Equal((100L, 112L), (buy.MarketCashChange.Before, buy.MarketCashChange.After));
        Assert.Equal((100L, 88L), (buy.TraderCashChange.Before, buy.TraderCashChange.After));
        var sell = result.TradeAttempts[1];
        Assert.Equal(TradeOutcome.Traded, sell.Outcome);
        Assert.Equal(2L, sell.UnitPrice);
        Assert.Equal(8L, sell.TotalAmount);
        Assert.Equal((6, 10), (sell.MarketInventoryChange.Before, sell.MarketInventoryChange.After));
        Assert.Equal((14, 10), (sell.TraderInventoryChange.Before, sell.TraderInventoryChange.After));
        Assert.Equal((112L, 104L), (sell.MarketCashChange.Before, sell.MarketCashChange.After));
        Assert.Equal((88L, 96L), (sell.TraderCashChange.Before, sell.TraderCashChange.After));
        Assert.Equal(TradeOutcome.InsufficientMarketInventory, result.TradeAttempts[2].Outcome);
        Assert.All(result.TradeAttempts, Conserved);
        Assert.Equal(before.Inventory, result.After.Inventory);
        Assert.Equal(before.Trading!.TraderInventory, result.After.Trading!.TraderInventory);
        Assert.True(result.After.Trading.TraderCash <= before.Trading.TraderCash);
        Assert.Equal(200L, result.After.Trading.MarketCash + result.After.Trading.TraderCash);
    }

    [Theory]
    [InlineData(TradeDirection.Buy, 1, 10, 100, 100, TradeOutcome.InsufficientMarketInventory)]
    [InlineData(TradeDirection.Sell, 10, 1, 100, 100, TradeOutcome.InsufficientTraderInventory)]
    [InlineData(TradeDirection.Buy, 10, 10, 100, 5, TradeOutcome.InsufficientTraderFunds)]
    [InlineData(TradeDirection.Sell, 10, 10, 3, 100, TradeOutcome.InsufficientMarketFunds)]
    public void ShortageRejectsWholeTradeWithoutChangingEitherAccount(TradeDirection direction,
        int marketLogs, int traderLogs, long marketCash, long traderCash, TradeOutcome expected)
    {
        var world = World(marketLogs, traderLogs, marketCash, traderCash);
        var before = world.Snapshot();
        var result = world.Step(trades: new[] { new TradeCommand(direction, "logs", 2) });
        var attempt = Assert.Single(result.TradeAttempts);
        Assert.Equal(expected, attempt.Outcome);
        Assert.Equal(0, attempt.MarketInventoryChange.Delta);
        Assert.Equal(0, attempt.TraderInventoryChange.Delta);
        Assert.Equal(0L, attempt.MarketCashChange.Delta);
        Assert.Equal(0L, attempt.TraderCashChange.Delta);
        Assert.Equal(before.Inventory, result.After.Inventory);
        Assert.Equal(before.Trading!.TraderInventory, result.After.Trading!.TraderInventory);
        Assert.Equal((marketCash, traderCash), (result.After.Trading.MarketCash, result.After.Trading.TraderCash));
        Assert.Equal(1L, result.After.Tick);
    }

    [Theory]
    [InlineData(TradeDirection.Buy)]
    [InlineData(TradeDirection.Sell)]
    public void ExactStockAndAffordabilityMayReachZero(TradeDirection direction)
    {
        var world = World(2, 2, 4, 6);
        var result = world.Step(trades: new[] { new TradeCommand(direction, "logs", 2) });
        var attempt = Assert.Single(result.TradeAttempts);
        Assert.Equal(TradeOutcome.Traded, attempt.Outcome);
        Conserved(attempt);
        Assert.Equal(0, direction == TradeDirection.Buy ? result.After.Inventory["logs"] : result.After.Trading!.TraderInventory["logs"]);
        Assert.Equal(0L, direction == TradeDirection.Buy ? result.After.Trading!.TraderCash : result.After.Trading!.MarketCash);
    }

    [Fact]
    public void RejectedTradesContinueAndStockTakesPrecedenceOverFunds()
    {
        var world = World(marketLogs: 0, traderLogs: 2, traderCash: 0);
        var result = world.Step(trades: new[] { Buy("logs", 2), Sell("logs", 2), Buy("logs", 1) });
        Assert.Equal(new[] { TradeOutcome.InsufficientMarketInventory, TradeOutcome.Traded, TradeOutcome.Traded },
            result.TradeAttempts.Select(attempt => attempt.Outcome));
        Assert.Equal((1, 1, 99L, 1L), (result.After.Inventory["logs"], result.After.Trading!.TraderInventory["logs"],
            result.After.Trading.MarketCash, result.After.Trading.TraderCash));
    }

    [Fact]
    public void DependentTradesRespectDeclaredOrder()
    {
        var first = World(marketLogs: 0, traderLogs: 2, traderCash: 0);
        var second = World(marketLogs: 0, traderLogs: 2, traderCash: 0);
        var ordered = first.Step(trades: new[] { Sell("logs", 2), Buy("logs", 1) });
        var reversed = second.Step(trades: new[] { Buy("logs", 1), Sell("logs", 2) });
        Assert.All(ordered.TradeAttempts, attempt => Assert.Equal(TradeOutcome.Traded, attempt.Outcome));
        Assert.Equal(TradeOutcome.InsufficientMarketInventory, reversed.TradeAttempts[0].Outcome);
        Assert.Equal(1, ordered.After.Inventory["logs"]);
        Assert.Equal(2, reversed.After.Inventory["logs"]);
    }

    [Fact]
    public void TradePhaseFeedsAndCompetesWithProductionAndCannotBuyFutureOutput()
    {
        var world = World(marketLogs: 0, traderLogs: 2, buildings: new[] { Mill(enabled: false) });
        var first = world.Step(new[] { new SetProductionEnabledCommand("mill_a", true) },
            new[] { Sell("logs", 2), Buy("planks", 1) });
        Assert.Equal(TradeOutcome.Traded, first.TradeAttempts[0].Outcome);
        Assert.Equal(TradeOutcome.InsufficientMarketInventory, first.TradeAttempts[1].Outcome);
        Assert.Equal(ProductionOutcome.Produced, Assert.Single(first.ProductionAttempts).Outcome);
        Assert.Equal(1, first.After.Inventory["planks"]);
        var second = world.Step(trades: new[] { Buy("planks", 1) });
        Assert.Equal(TradeOutcome.Traded, Assert.Single(second.TradeAttempts).Outcome);
        Assert.Equal(1, second.After.Trading!.TraderInventory["planks"]);

        var competing = World(marketLogs: 2, buildings: new[] { Mill() });
        var competed = competing.Step(trades: new[] { Buy("logs", 1) });
        Assert.Equal(ProductionOutcome.ResourceShortage, Assert.Single(competed.ProductionAttempts).Outcome);
        Assert.Equal(1, competed.After.Inventory["logs"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonpositiveTradeQuantityIsInvalid(int quantity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Buy("logs", quantity));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void QuotesRequirePositiveBidAndAskAtLeastBid(long bid, long ask) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarketQuote("logs", bid, ask));

    [Fact]
    public void InvalidConfigurationAndIdentifiersFailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => World(marketCash: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(traderCash: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(traderLogs: -1));
        Assert.Throws<ArgumentException>(() => new MarketConfiguration(Quotes(), 0, "Bad ID", 0));
        Assert.Throws<ArgumentException>(() => new MarketQuote("Bad ID", 1, 1));
        Assert.Throws<ArgumentException>(() => Buy("Bad ID", 1));
        Assert.Throws<ArgumentNullException>(() => Buy(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradeCommand((TradeDirection)99, "logs", 1));
        Assert.Throws<ArgumentNullException>(() => new MarketConfiguration(null!, 0, "trader", 0));
        Assert.Throws<ArgumentException>(() => World(quotes: new[] { new MarketQuote("logs", 1, 1), new MarketQuote("logs", 2, 2) }));
        Assert.Throws<ArgumentException>(() => World(quotes: new MarketQuote[] { null! }));
        Assert.Throws<KeyNotFoundException>(() => World(quotes: new[] { new MarketQuote("unknown", 1, 1) }));
        Assert.Throws<KeyNotFoundException>(() => new WorldState(Canonical(), "port", Array.Empty<BuildingInstance>(),
            market: new MarketConfiguration(Quotes(), 0, "trader", 0, new Dictionary<string, int> { ["unknown"] = 1 })));
    }

    [Fact]
    public void MissingMarketOrQuoteAndInvalidLaterCommandCannotCommitEarlierWork()
    {
        var noMarket = new WorldState(Canonical(), "port", Array.Empty<BuildingInstance>());
        var legacy = noMarket.Step(trades: Array.Empty<TradeCommand>());
        Assert.Null(legacy.After.Trading);
        Assert.Empty(legacy.TradeAttempts);
        var before = noMarket.Snapshot();
        Assert.Throws<InvalidOperationException>(() => noMarket.Step(trades: new[] { Buy("logs", 1) }));
        Assert.Same(before, noMarket.Snapshot());

        var world = World(quotes: new[] { new MarketQuote("logs", 2, 3) }, buildings: new[] { Mill() });
        var original = world.Snapshot();
        var controls = new[] { new SetProductionEnabledCommand("mill_a", false) };
        Assert.Throws<KeyNotFoundException>(() => world.Step(controls, new[] { Buy("logs", 1), Buy("unknown", 1) }));
        Assert.Same(original, world.Snapshot());
        Assert.Throws<KeyNotFoundException>(() => world.Step(controls, new[] { Buy("logs", 1), Buy("planks", 1) }));
        Assert.Same(original, world.Snapshot());
        Assert.Throws<ArgumentException>(() => world.Step(controls, new[] { Buy("logs", 1), null! }));
        Assert.Same(original, world.Snapshot());
        Assert.Throws<KeyNotFoundException>(() => world.Step(new[] { new SetProductionEnabledCommand("unknown", false) }, new[] { Buy("logs", 1) }));
        Assert.Same(original, world.Snapshot());
        Assert.Equal(0L, world.Step(controls, new[] { Buy("logs", 1) }).Tick);
    }

    [Fact]
    public void EnumerationAndReentrantFailuresPreserveAccounts()
    {
        var world = World();
        var before = world.Snapshot();
        IEnumerable<TradeCommand> Failing()
        {
            yield return Buy("logs", 1);
            throw new InvalidOperationException("enumeration failed");
        }
        IEnumerable<TradeCommand> Reentrant()
        {
            yield return Buy("logs", 1);
            world.Step();
        }
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: Failing()));
        Assert.Same(before, world.Snapshot());
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: Reentrant()));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0L, world.Step().Tick);
    }

    [Fact]
    public void LaterTotalOverflowRollsBackTradesControlsAndClock()
    {
        var world = World(quotes: new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 1, long.MaxValue) },
            buildings: new[] { Mill() });
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(new[] { new SetProductionEnabledCommand("mill_a", false) },
            new[] { Buy("logs", 1), Buy("planks", 2) }));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0L, world.Step(trades: new[] { Buy("logs", 1) }).Tick);
    }

    [Theory]
    [InlineData(TradeDirection.Buy)]
    [InlineData(TradeDirection.Sell)]
    public void CashCreditOverflowLeavesBothAccountsAndTickUntouched(TradeDirection direction)
    {
        var world = World(marketCash: long.MaxValue, traderCash: long.MaxValue);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(trades: new[] { new TradeCommand(direction, "logs", 1) }));
        Assert.Same(before, world.Snapshot());
    }

    [Theory]
    [InlineData(TradeDirection.Buy)]
    [InlineData(TradeDirection.Sell)]
    public void InventoryCreditOverflowLeavesBothAccountsUntouched(TradeDirection direction)
    {
        var world = World(marketLogs: int.MaxValue, traderLogs: int.MaxValue);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(trades: new[] { new TradeCommand(direction, "logs", 1) }));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void MaximumMoneyAndQuantityTransfersSucceedWithoutFloatingPoint()
    {
        var money = World(marketLogs: 1, traderLogs: 0, marketCash: 0, traderCash: long.MaxValue,
            quotes: new[] { new MarketQuote("logs", long.MaxValue, long.MaxValue) });
        var first = money.Step(trades: new[] { Buy("logs", 1) });
        Assert.Equal(long.MaxValue, first.After.Trading!.MarketCash);
        Assert.Equal(0L, first.After.Trading.TraderCash);
        Conserved(Assert.Single(first.TradeAttempts));
        var second = money.Step(trades: new[] { Sell("logs", 1) });
        Assert.Equal(long.MaxValue, second.After.Trading!.TraderCash);
        Conserved(Assert.Single(second.TradeAttempts));

        // Valid individual accounts may have a combined balance larger than long.MaxValue.
        var wealthy = World(marketLogs: 1, traderLogs: 0, marketCash: long.MaxValue - 3, traderCash: long.MaxValue);
        var wealthyResult = wealthy.Step(trades: new[] { Buy("logs", 1) });
        Assert.Equal(long.MaxValue, wealthyResult.After.Trading!.MarketCash);
        Assert.Equal(long.MaxValue - 3, wealthyResult.After.Trading.TraderCash);
        Conserved(Assert.Single(wealthyResult.TradeAttempts));

        var stock = World(marketLogs: int.MaxValue, traderLogs: 0, marketCash: 0, traderCash: int.MaxValue,
            quotes: new[] { new MarketQuote("logs", 1, 1) });
        var result = stock.Step(trades: new[] { Buy("logs", int.MaxValue) });
        Assert.Equal(int.MaxValue, result.After.Trading!.TraderInventory["logs"]);
        Assert.Equal(0, result.After.Inventory["logs"]);
        Assert.Equal((long)int.MaxValue, Assert.Single(result.TradeAttempts).TotalAmount);
        Conserved(Assert.Single(result.TradeAttempts));
    }

    [Fact]
    public void LaterProductionOverflowRollsBackEarlierTradeProductionAndControls()
    {
        var world = World(marketLogs: 2, traderLogs: 2, marketPlanks: int.MaxValue - 1,
            buildings: new[] { Mill(), Mill("mill_b", false) });
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(new[] { new SetProductionEnabledCommand("mill_b", true) },
            new[] { Sell("logs", 2) }));
        Assert.Same(before, world.Snapshot());
        var retry = world.Step(trades: new[] { Sell("logs", 2) });
        Assert.Equal(0L, retry.Tick);
        Assert.Equal(int.MaxValue, retry.After.Inventory["planks"]);
        Assert.False(retry.After.Buildings[1].ProductionEnabled);
    }

    [Fact]
    public void TickOverflowPreventsAnyTransfer()
    {
        var world = World(tick: long.MaxValue);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(trades: new[] { Buy("logs", 1) }));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void ConfigurationSnapshotsAndResultsAreDetachedAndReadOnly()
    {
        var quotes = Quotes();
        var stock = Stock(2);
        var config = new MarketConfiguration(quotes, 100, "trader", 100, stock);
        quotes[0] = new MarketQuote("logs", 99, 99);
        stock["logs"] = 999;
        var world = new WorldState(Canonical(), "port", Array.Empty<BuildingInstance>(), Stock(2), market: config);
        var before = world.Snapshot();
        var result = world.Step(trades: new[] { Buy("logs", 1) });
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)config.InitialTraderInventory)["logs"] = 99);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, MarketQuote>)config.Quotes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)result.After.Trading!.TraderInventory).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, MarketQuote>)result.After.Trading!.Quotes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TradeAttempt>)result.TradeAttempts).Clear());
        var historical = JsonConvert.SerializeObject(result);
        world.Step(trades: new[] { Sell("logs", 2) });
        Assert.Equal(historical, JsonConvert.SerializeObject(result));
        Assert.Equal(2, before.Trading!.TraderInventory["logs"]);
        Assert.Equal(100L, before.Trading.TraderCash);
        Assert.Equal(3L, result.TradeAttempts[0].UnitPrice);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void EquivalentSetupOrderAndCulturesProduceIdenticalResults(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var first = World(marketLogs: 2, buildings: new[] { Mill(), Mill("mill_b") });
            var reversed = ContentLoader.Load(ContentLoaderTests.Change(root =>
                root["goods"] = new JArray(((JArray)root["goods"]!).Reverse())));
            var second = new WorldState(reversed, "harbor", new[] { Mill("mill_b"), Mill() },
                new Dictionary<string, int> { ["planks"] = 0, ["logs"] = 2 }, market:
                new MarketConfiguration(Quotes().Reverse(), 100, "trader", 100,
                    new Dictionary<string, int> { ["planks"] = 0, ["logs"] = 10 }));
            for (var tick = 0; tick < 3; tick++)
            {
                var trades = new[] { Sell("logs", 2), Buy("planks", 1), Buy("logs", 1) };
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var expected = JsonConvert.SerializeObject(first.Step(trades: trades));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(expected, JsonConvert.SerializeObject(second.Step(trades: trades)));
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void DocumentedMarketScenarioMatchesEveryTick()
    {
        var world = World(marketLogs: 2, traderLogs: 4, marketCash: 20, traderCash: 7, buildings: new[] { Mill() });
        var commands = new[]
        {
            new[] { Buy("logs", 1), Sell("logs", 3), Buy("planks", 1) },
            new[] { Buy("planks", 1), Buy("logs", 2), Buy("logs", 3) },
            new[] { Sell("logs", 2), Buy("planks", 1) },
            new[] { Buy("planks", 1), Sell("planks", 2), Buy("planks", 2) }
        };
        var expected = new[] { (2, 1, 17L, 2, 0, 10L), (0, 1, 22L, 2, 1, 5L),
            (0, 1, 23L, 0, 2, 4L), (0, 1, 25L, 0, 2, 2L) };
        for (var tick = 0; tick < commands.Length; tick++)
        {
            var step = world.Step(trades: commands[tick]);
            var after = step.After;
            Assert.Equal(tick, step.Tick);
            Assert.Equal(expected[tick], (after.Inventory["logs"], after.Inventory["planks"], after.Trading!.MarketCash,
                after.Trading.TraderInventory["logs"], after.Trading.TraderInventory["planks"], after.Trading.TraderCash));
            Assert.Equal(27L, after.Trading.MarketCash + after.Trading.TraderCash);
            Assert.All(step.TradeAttempts, Conserved);
            Assert.Equal(tick == 3 ? ProductionOutcome.ResourceShortage : ProductionOutcome.Produced,
                Assert.Single(step.ProductionAttempts).Outcome);
        }
    }
}
