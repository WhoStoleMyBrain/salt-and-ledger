using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class CanonicalProductionTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    [Theory]
    [InlineData("harvest_grain", "farm", "", 0, "grain", 4)]
    [InlineData("mill_flour", "mill", "grain", 2, "flour", 1)]
    [InlineData("make_furniture", "furniture_workshop", "planks", 2, "furniture", 1)]
    public void ExpandedCanonicalRecipesExecuteDeclaredFlows(string recipe, string building,
        string input, int consumed, string output, int produced)
    {
        var content = Content();
        var stock = new Inventory(content, consumed == 0 ? null : new Dictionary<string, int> { [input] = consumed });
        Assert.True(Production.TryRunBatch(stock, building, recipe));
        if (consumed > 0) Assert.Equal(0, stock.GetQuantity(input));
        Assert.Equal(produced, stock.GetQuantity(output));
    }

    [Fact]
    public void BakeryRequiresBothInputsAndCompetesWithSawmillForLogs()
    {
        var content = Content();
        var stock = new Inventory(content, new Dictionary<string, int> { ["logs"] = 2, ["flour"] = 1 });
        Assert.True(Production.TryRunBatch(stock, "sawmill", "saw_planks"));
        var before = stock.Snapshot().ToArray();
        Assert.False(Production.TryRunBatch(stock, "bakery", "bake_bread"));
        Assert.Equal(before, stock.Snapshot().ToArray());
        var bakeryFirst = new Inventory(content, new Dictionary<string, int> { ["logs"] = 2, ["flour"] = 1 });
        Assert.True(Production.TryRunBatch(bakeryFirst, "bakery", "bake_bread"));
        Assert.Equal(2, bakeryFirst.GetQuantity("bread"));
        Assert.Equal(0, bakeryFirst.GetQuantity("flour"));
        Assert.False(Production.TryRunBatch(bakeryFirst, "sawmill", "saw_planks"));
    }

}
