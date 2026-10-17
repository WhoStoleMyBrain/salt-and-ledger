using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ProductionTests
{
    private static ContentCatalog Canonical() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static Inventory Stock(ContentCatalog content, int logs, int planks = 0) =>
        new Inventory(content, new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks });

    private static void EqualBalances(IReadOnlyDictionary<string, int> expected, Inventory actual) =>
        Assert.Equal(expected.OrderBy(item => item.Key), actual.Snapshot().OrderBy(item => item.Key));

    [Fact]
    public void ProductionUsesCanonicalRecipeQuantities()
    {
        var content = Canonical();
        var recipe = content.GetRecipe("saw_planks");
        var inventory = Stock(content, 7, 3);
        var before = inventory.Snapshot();

        Assert.True(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));

        foreach (var input in recipe.Inputs)
            Assert.Equal(before[input.GoodId] - input.Quantity, inventory.GetQuantity(input.GoodId));
        foreach (var output in recipe.Outputs)
            Assert.Equal(before[output.GoodId] + output.Quantity, inventory.GetQuantity(output.GoodId));
    }

    [Fact]
    public void ProductionReadsChangedRecipeInsteadOfHardcodingSampleRatio()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
        {
            root["recipes"]![0]!["inputs"]![0]!["quantity"] = 3;
            root["recipes"]![0]!["outputs"]![0]!["quantity"] = 2;
        }));
        var inventory = Stock(content, 7);
        Assert.True(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.True(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.False(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.Equal(1, inventory.GetQuantity("logs"));
        Assert.Equal(4, inventory.GetQuantity("planks"));
    }

    [Fact]
    public void SameContentStateAndCommandsReplayDeterministically()
    {
        var left = Stock(Canonical(), 7);
        var reordered = ContentLoader.Load(ContentLoaderTests.Change(root =>
            root["goods"] = new JArray(((JArray)root["goods"]!).Reverse())));
        var right = new Inventory(reordered, new Dictionary<string, int> { ["planks"] = 0, ["logs"] = 7 });
        var outcomes = new List<bool>();
        for (var step = 0; step < 6; step++)
        {
            var result = Production.TryRunBatch(left, "sawmill", "saw_planks");
            outcomes.Add(result);
            Assert.Equal(result, Production.TryRunBatch(right, "sawmill", "saw_planks"));
            EqualBalances(left.Snapshot(), right);
        }
        Assert.Equal(new[] { true, true, true, false, false, false }, outcomes);
        Assert.Equal(1, left.GetQuantity("logs"));
        Assert.Equal(3, left.GetQuantity("planks"));
    }

    [Fact]
    public void ShortageOfALaterInputDoesNotConsumeEarlierInputs()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
            ((JArray)root["recipes"]![0]!["inputs"]!).Add(new JObject { ["goodId"] = "planks", ["quantity"] = 2 })));
        var inventory = Stock(content, 6, 1);
        var before = inventory.Snapshot();
        Assert.False(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        EqualBalances(before, inventory);
    }

    [Fact]
    public void OverflowOfALaterOutputDoesNotCommitEarlierChanges()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
            root["recipes"]![0]!["outputs"] = new JArray(
                new JObject { ["goodId"] = "logs", ["quantity"] = 1 },
                new JObject { ["goodId"] = "planks", ["quantity"] = 1 })));
        var inventory = Stock(content, 6, int.MaxValue);
        var before = inventory.Snapshot();
        Assert.Throws<OverflowException>(() => Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        EqualBalances(before, inventory);
    }

    [Theory]
    [InlineData(1, 1, int.MaxValue, int.MaxValue)]
    [InlineData(2, 1, 3, 2)]
    [InlineData(1, 2, 3, 4)]
    public void SameGoodOnBothSidesUsesFinalBalance(int consumed, int produced, int initial, int expected)
    {
        var content = SameGoodRecipe(consumed, produced);
        var inventory = Stock(content, initial);
        Assert.True(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.Equal(expected, inventory.GetQuantity("logs"));
        Assert.Equal(0, inventory.GetQuantity("planks"));
    }

    [Fact]
    public void SameGoodOutputCannotFundMissingInput()
    {
        var inventory = Stock(SameGoodRecipe(2, 3), 1);
        var before = inventory.Snapshot();
        Assert.False(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        EqualBalances(before, inventory);
    }

    [Fact]
    public void SameGoodOverflowIsAtomic()
    {
        var inventory = Stock(SameGoodRecipe(1, 2), int.MaxValue);
        var before = inventory.Snapshot();
        Assert.Throws<OverflowException>(() => Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        EqualBalances(before, inventory);
    }

    [Fact]
    public void RejectsWrongBuildingAssociationWithoutChangingStock()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
            ((JArray)root["buildings"]!).Add(new JObject
                { ["id"] = "other_building", ["name"] = "Other building", ["description"] = "Test association." })));
        var inventory = Stock(content, 6);
        var before = inventory.Snapshot();
        Assert.Throws<ArgumentException>(() => Production.TryRunBatch(inventory, "other_building", "saw_planks"));
        EqualBalances(before, inventory);
    }

    [Theory]
    [InlineData("missing", "saw_planks")]
    [InlineData("sawmill", "missing")]
    [InlineData("Sawmill", "saw_planks")]
    public void RejectsUnknownCommandIdsWithoutChangingStock(string building, string recipe)
    {
        var inventory = Stock(Canonical(), 6);
        var before = inventory.Snapshot();
        Assert.Throws<KeyNotFoundException>(() => Production.TryRunBatch(inventory, building, recipe));
        EqualBalances(before, inventory);
    }

    [Fact]
    public void InventoryDefaultsToZeroAndRejectsNegativeOrUnknownStock()
    {
        var content = Canonical();
        var empty = new Inventory(content);
        Assert.All(content.Goods, good => Assert.Equal(0, empty.GetQuantity(good.Id)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Stock(content, -1));
        Assert.Throws<KeyNotFoundException>(() => new Inventory(content, new Dictionary<string, int> { ["missing"] = 1 }));
        Assert.Throws<KeyNotFoundException>(() => empty.GetQuantity("missing"));
    }

    [Fact]
    public void InitialStockAndSnapshotsCannotMutateInventory()
    {
        var original = new Dictionary<string, int> { ["logs"] = 6 };
        var inventory = new Inventory(Canonical(), original);
        var snapshot = inventory.Snapshot();
        original["logs"] = 100;
        Assert.Equal(6, inventory.GetQuantity("logs"));
        var mutableView = Assert.IsAssignableFrom<IDictionary<string, int>>(snapshot);
        Assert.Throws<NotSupportedException>(() => mutableView["logs"] = 100);
        Assert.True(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.Equal(6, snapshot["logs"]);
        Assert.Equal(4, inventory.GetQuantity("logs"));
    }

    private static ContentCatalog SameGoodRecipe(int consumed, int produced) =>
        ContentLoader.Load(ContentLoaderTests.Change(root =>
        {
            root["recipes"]![0]!["inputs"]![0]!["quantity"] = consumed;
            root["recipes"]![0]!["outputs"]![0]!["goodId"] = "logs";
            root["recipes"]![0]!["outputs"]![0]!["quantity"] = produced;
        }));
}
