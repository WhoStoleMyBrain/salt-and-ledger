using System;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class LivingCityGenerationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void EconomicViewUsesTheSingleGeneralizedGenerator(int seed)
    {
        var view = LivingCityGeneration.Generate(seed);
        var generated = ExpandingEconomyGeneration.Generate(seed);
        Assert.Equal(4, view.GeneratorVersion);
        Assert.Equal(generated.Signature, view.Signature);
        Assert.Equal(generated.Geography.Signature, view.Geography.Signature);
        Assert.Equal(generated.Ports.Select(p => p.Id), view.Ports.Select(p => p.Id));
        Assert.Equal(generated.Blueprints.Select(b => b.Id), view.Blueprints.Select(b => b.Id));
        Assert.Equal(generated.FoodSourcePortId, view.FoodSourcePortId);
        Assert.Equal(generated.ProcessingPortId, view.ProcessingPortId);
    }

    [Fact]
    public void GeneratedNamesAreStableUniqueAndIndependentOfOldRoleIdentities()
    {
        var names = Enumerable.Range(1, 64).Select(i => LivingCityGeneration.PortName("town_" + i.ToString("000"))).ToArray();
        Assert.Equal(64, names.Distinct(StringComparer.Ordinal).Count());
        Assert.All(names, name => Assert.Matches("^[A-Z][a-z]+$", name));
        Assert.Throws<ArgumentException>(() => LivingCityGeneration.PortName("northport"));
        Assert.Throws<ArgumentException>(() => LivingCityGeneration.PortName("town_000"));
        Assert.Throws<ArgumentException>(() => LivingCityGeneration.PortName("town_065"));
    }
}
