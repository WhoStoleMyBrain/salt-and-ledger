using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class InventoryPricingTests
{
    private static InventoryPricing Prices(InventoryPricingModel model, long spread = 2) =>
        new InventoryPricing(model, 100, 20, 60, 5, 300, spread);

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void BulkSumsMatchIndependentSmallStateMarginalEnumeration(InventoryPricingModel model)
    {
        foreach (var target in new[] { 1, 4, 7, 12 })
        foreach (var sensitivity in new long[] { 0, 1, 3, 17 })
        foreach (var spread in new long[] { 0, 1, 5, 80 })
        {
            var pricing = new InventoryPricing(model, 10, target, sensitivity, 2, 31, spread);
            for (var stock = 0; stock <= 20; stock++)
            for (var quantity = 1; quantity <= 10; quantity++)
            foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
            {
                if (direction == TradeDirection.Buy && quantity > stock) continue;
                var quote = pricing.Quote(direction, stock, quantity);
                long expected = 0;
                for (var unit = 0; unit < quantity; unit++)
                {
                    var edge = direction == TradeDirection.Buy ? stock - unit : stock + unit + 1;
                    var marginal = EnumeratedPrice(pricing, direction, edge);
                    expected = checked(expected + marginal);
                    Assert.Equal(marginal, pricing.PriceAtEdge(direction, edge));
                    if (unit == 0) Assert.Equal(marginal, quote.FirstMarginalPrice);
                    if (unit == quantity - 1) Assert.Equal(marginal, quote.LastMarginalPrice);
                }
                Assert.Equal(expected, quote.TotalAmount);
                Assert.Equal(quantity, quote.Quantity);
                Assert.Equal(quote.FirstMarginalPrice == quote.LastMarginalPrice, quote.IsUniform);
            }
        }
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void PricesAreMonotoneBoundedAndRespectEveryReversedEdge(InventoryPricingModel model)
    {
        var pricing = Prices(model);
        long previousAsk = pricing.MaxPrice;
        long previousBid = pricing.MaxPrice;
        for (var edge = 1; edge <= 100; edge++)
        {
            var ask = pricing.PriceAtEdge(TradeDirection.Buy, edge);
            var bid = pricing.PriceAtEdge(TradeDirection.Sell, edge);
            Assert.InRange(bid, pricing.MinPrice, pricing.MaxPrice);
            Assert.InRange(ask, bid, pricing.MaxPrice);
            Assert.True(ask <= previousAsk);
            Assert.True(bid <= previousBid);
            previousAsk = ask;
            previousBid = bid;
        }
        Assert.Equal(pricing.MinPrice, previousAsk);
        Assert.Equal(pricing.MinPrice, previousBid);
        Assert.Equal(102, pricing.PriceAtEdge(TradeDirection.Buy, pricing.TargetStock));
        Assert.Equal(98, pricing.PriceAtEdge(TradeDirection.Sell, pricing.TargetStock));
    }

    [Fact]
    public void SevereScarcityAddsPressureContinuouslyAtTheDeclaredThreshold()
    {
        var linear = Prices(InventoryPricingModel.Linear);
        var scarcity = Prices(InventoryPricingModel.Scarcity);
        Assert.Equal(147, linear.PriceAtEdge(TradeDirection.Buy, 5));
        Assert.Equal(147, scarcity.PriceAtEdge(TradeDirection.Buy, 5));
        Assert.Equal(150, linear.PriceAtEdge(TradeDirection.Buy, 4));
        Assert.Equal(186, scarcity.PriceAtEdge(TradeDirection.Buy, 4));
        Assert.Equal(159, linear.PriceAtEdge(TradeDirection.Buy, 1));
        Assert.Equal(300, scarcity.PriceAtEdge(TradeDirection.Buy, 1));
        Assert.Equal(299, scarcity.PriceAtEdge(TradeDirection.Sell, 1));
        Assert.Equal(297, linear.Quote(TradeDirection.Buy, 5, 2).TotalAmount);
        Assert.Equal(333, scarcity.Quote(TradeDirection.Buy, 5, 2).TotalAmount);
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void SplittingEverySmallOrderPreservesExecutionTotals(InventoryPricingModel model)
    {
        var pricing = Prices(model);
        for (var stock = 1; stock <= 24; stock++)
        for (var quantity = 2; quantity <= stock; quantity++)
        for (var firstQuantity = 1; firstQuantity < quantity; firstQuantity++)
        {
            var buy = pricing.Quote(TradeDirection.Buy, stock, quantity);
            var buyFirst = pricing.Quote(TradeDirection.Buy, stock, firstQuantity);
            var buySecond = pricing.Quote(TradeDirection.Buy, stock - firstQuantity, quantity - firstQuantity);
            Assert.Equal(buy.TotalAmount, checked(buyFirst.TotalAmount + buySecond.TotalAmount));

            var sell = pricing.Quote(TradeDirection.Sell, stock, quantity);
            var sellFirst = pricing.Quote(TradeDirection.Sell, stock, firstQuantity);
            var sellSecond = pricing.Quote(TradeDirection.Sell, stock + firstQuantity, quantity - firstQuantity);
            Assert.Equal(sell.TotalAmount, checked(sellFirst.TotalAmount + sellSecond.TotalAmount));
        }
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear, 0)]
    [InlineData(InventoryPricingModel.Linear, 1)]
    [InlineData(InventoryPricingModel.Scarcity, 0)]
    [InlineData(InventoryPricingModel.Scarcity, 1)]
    public void ExhaustiveClosedSmallStockWalksCannotEarnCash(InventoryPricingModel model, long spread)
    {
        // Exhaust all legal ±1 walks of up to eight trades on stocks 0..8.
        // Split invariance above extends the same edge accounting to bulk trades.
        var pricing = new InventoryPricing(model, 5, 8, 3, 2, 9, spread);
        var checkedCycles = 0;
        for (var initialStock = 0; initialStock <= 8; initialStock++)
        {
            void Walk(int stock, int remaining, long cashChange)
            {
                if (remaining < 8 && stock == initialStock)
                {
                    Assert.True(cashChange <= 0, $"Closed cycle gained {cashChange} at stock {stock}.");
                    checkedCycles++;
                }
                if (remaining == 0) return;
                if (stock > 0)
                    Walk(stock - 1, remaining - 1, cashChange - pricing.Quote(TradeDirection.Buy, stock, 1).TotalAmount);
                if (stock < 8)
                    Walk(stock + 1, remaining - 1, cashChange + pricing.Quote(TradeDirection.Sell, stock, 1).TotalAmount);
            }
            Walk(initialStock, 8, 0);
        }
        Assert.True(checkedCycles > 500);
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void RoundTripsAcrossFloorsCeilingsAndThresholdsCannotEarnCash(InventoryPricingModel model)
    {
        var pricing = Prices(model);
        for (var stock = 1; stock <= 80; stock++)
        for (var quantity = 1; quantity <= stock; quantity++)
        {
            var buy = pricing.Quote(TradeDirection.Buy, stock, quantity);
            var reverse = pricing.Quote(TradeDirection.Sell, stock - quantity, quantity);
            Assert.True(reverse.TotalAmount <= buy.TotalAmount);
            var sell = pricing.Quote(TradeDirection.Sell, stock, quantity);
            var reverseSale = pricing.Quote(TradeDirection.Buy, stock + quantity, quantity);
            Assert.True(sell.TotalAmount <= reverseSale.TotalAmount);
        }
    }

    [Fact]
    public void LargeTradesHavePriceImpactRatherThanUniformInitialPrice()
    {
        var pricing = Prices(InventoryPricingModel.Linear);
        var small = pricing.Quote(TradeDirection.Buy, 20, 1);
        var large = pricing.Quote(TradeDirection.Buy, 20, 10);
        Assert.Equal(102, small.TotalAmount);
        Assert.Equal(1155, large.TotalAmount);
        Assert.Equal(102, large.FirstMarginalPrice);
        Assert.Equal(129, large.LastMarginalPrice);
        Assert.False(large.IsUniform);
        Assert.True(large.TotalAmount > small.TotalAmount * large.Quantity);
    }

    [Fact]
    public void NegativeRawValuesRoundDownBeforeSpreadAndClamping()
    {
        var pricing = new InventoryPricing(InventoryPricingModel.Linear, 10, 7, 17, 1, 30, 5);
        // At edge 12 the raw value is -15/7: floor(-15/7) + 5 = 2.
        Assert.Equal(2L, pricing.PriceAtEdge(TradeDirection.Buy, 12));
        Assert.Equal(1L, pricing.PriceAtEdge(TradeDirection.Sell, 12));
        Assert.Equal(3L, pricing.Quote(TradeDirection.Buy, 13, 2).TotalAmount);
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void ExtremeQuantitiesAndCoefficientsUseBoundedSummation(InventoryPricingModel model)
    {
        var pricing = new InventoryPricing(model, 2, int.MaxValue, 1, 1, 4, 0);
        var timer = Stopwatch.StartNew();
        var buy = pricing.Quote(TradeDirection.Buy, int.MaxValue, int.MaxValue);
        var sell = pricing.Quote(TradeDirection.Sell, 0, int.MaxValue);
        Assert.Equal(buy.TotalAmount, sell.TotalAmount);
        if (model == InventoryPricingModel.Linear) Assert.Equal(2L * int.MaxValue, buy.TotalAmount);
        Assert.InRange(buy.TotalAmount, (long)int.MaxValue, 4L * int.MaxValue);

        var extreme = new InventoryPricing(model, long.MaxValue, int.MaxValue, long.MaxValue, 1, long.MaxValue, long.MaxValue);
        Assert.Equal(long.MaxValue, extreme.Quote(TradeDirection.Buy, int.MaxValue, 1).TotalAmount);
        Assert.Equal(1L, extreme.Quote(TradeDirection.Sell, int.MaxValue - 1, 1).TotalAmount);
        Assert.Throws<OverflowException>(() => extreme.Quote(TradeDirection.Buy, int.MaxValue, int.MaxValue));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5), $"Bounded quotes took {timer.Elapsed}.");
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void ZeroSensitivityAndEqualBoundsRemainValidConstants(InventoryPricingModel model)
    {
        var constant = new InventoryPricing(model, 10, 1, 0, 1, 20, 2);
        Assert.Equal(12L * int.MaxValue, constant.Quote(TradeDirection.Buy, int.MaxValue, int.MaxValue).TotalAmount);
        Assert.Equal(8L * int.MaxValue, constant.Quote(TradeDirection.Sell, 0, int.MaxValue).TotalAmount);
        var collapsed = new InventoryPricing(model, 7, 1, long.MaxValue, 7, 7, long.MaxValue);
        Assert.Equal(7L * int.MaxValue, collapsed.Quote(TradeDirection.Buy, int.MaxValue, int.MaxValue).TotalAmount);
        Assert.Equal(7L * int.MaxValue, collapsed.Quote(TradeDirection.Sell, 0, int.MaxValue).TotalAmount);
    }

    [Fact]
    public void InvalidConfigurationAndStockMovementAreRejectedExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing((InventoryPricingModel)2, 10, 1, 1, 1, 20, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing(InventoryPricingModel.Linear, 10, 0, 1, 1, 20, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing(InventoryPricingModel.Linear, 10, 1, 1, 0, 20, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing(InventoryPricingModel.Linear, 10, 1, 1, 20, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing(InventoryPricingModel.Linear, 0, 1, 1, 1, 20, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing(InventoryPricingModel.Linear, 21, 1, 1, 1, 20, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing(InventoryPricingModel.Linear, 10, 1, -1, 1, 20, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryPricing(InventoryPricingModel.Linear, 10, 1, 1, 1, 20, -1));
        var pricing = Prices(InventoryPricingModel.Linear);
        Assert.Throws<ArgumentOutOfRangeException>(() => pricing.PriceAtEdge(TradeDirection.Buy, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => pricing.PriceAtEdge((TradeDirection)2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pricing.Quote(TradeDirection.Buy, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pricing.Quote(TradeDirection.Buy, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => pricing.Quote(TradeDirection.Buy, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pricing.Quote((TradeDirection)2, 1, 1));
        Assert.Throws<OverflowException>(() => pricing.Quote(TradeDirection.Sell, int.MaxValue, 1));
    }

    [Fact]
    public void PricingIsIndependentOfCultureAndRepeatedCalls()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var pricing = Prices(InventoryPricingModel.Scarcity);
            var expected = pricing.Quote(TradeDirection.Buy, 19, 18);
            foreach (var culture in new[] { "en-US", "de-DE", "tr-TR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                for (var repeat = 0; repeat < 3; repeat++)
                {
                    var actual = pricing.Quote(TradeDirection.Buy, 19, 18);
                    Assert.Equal(expected.TotalAmount, actual.TotalAmount);
                    Assert.Equal(expected.FirstMarginalPrice, actual.FirstMarginalPrice);
                    Assert.Equal(expected.LastMarginalPrice, actual.LastMarginalPrice);
                }
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static long EnumeratedPrice(InventoryPricing pricing, TradeDirection direction, int edge)
    {
        var threshold = Math.Max(1, pricing.TargetStock / 4);
        var denominator = (BigInteger)pricing.TargetStock * threshold;
        var numerator = (BigInteger)pricing.ReferencePrice * denominator +
            (BigInteger)pricing.Sensitivity * (pricing.TargetStock - edge) * threshold;
        if (pricing.Model == InventoryPricingModel.Scarcity && edge < threshold)
            numerator += 3 * (BigInteger)pricing.Sensitivity * (threshold - edge) * pricing.TargetStock;
        var reference = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (remainder < 0) reference--;
        var price = reference + (direction == TradeDirection.Buy ? pricing.Spread : -pricing.Spread);
        return (long)BigInteger.Min(pricing.MaxPrice, BigInteger.Max(pricing.MinPrice, price));
    }
}
