using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ProductionSpecializationTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();

    [Theory]
    [InlineData("harvest_grain", "mill_flour")]
    [InlineData("mill_flour", "bake_bread")]
    [InlineData("harvest_logs", "saw_planks")]
    [InlineData("harvest_logs", "bake_bread")]
    [InlineData("saw_planks", "make_furniture")]
    public void EveryCanonicalDirectDependencyIsSymmetricAndExplained(string producer, string consumer)
    {
        var content = Content();
        Assert.True(ProductionSpecialization.AreIncompatible(content.GetRecipe(producer), content.GetRecipe(consumer)));
        Assert.True(ProductionSpecialization.AreIncompatible(content.GetRecipe(consumer), content.GetRecipe(producer)));
        var first = ProductionSpecialization.Conflict(content, new[] { producer, consumer });
        Assert.NotNull(first);
        Assert.Contains(producer, first);
        Assert.Contains(consumer, first);
        Assert.Equal(first, ProductionSpecialization.Conflict(content, new[] { consumer, producer, consumer }));
    }

    [Theory]
    [InlineData("harvest_grain", "bake_bread")]
    [InlineData("harvest_logs", "make_furniture")]
    [InlineData("mill_flour", "saw_planks")]
    [InlineData("bake_bread", "bake_bread")]
    public void IndirectChainsAndRepeatedFacilitiesAreAllowed(string first, string second) =>
        Assert.Null(ProductionSpecialization.Conflict(Content(), new[] { first, second }));

    [Fact]
    public void NewlyAuthoredRecipeParticipatesWithoutAnIndustryPairTable()
    {
        var json = ContentLoaderTests.Change(root =>
        {
            ((JArray)root["buildings"]!).Add(new JObject
            {
                ["id"] = "bread_reclaimer", ["name"] = "Bread Reclaimer", ["description"] = "Test canonical extension."
            });
            ((JArray)root["recipes"]!).Add(new JObject
            {
                ["id"] = "reclaim_grain", ["name"] = "Reclaim Grain", ["description"] = "Test canonical extension.",
                ["buildingId"] = "bread_reclaimer", ["inputs"] = new JArray(new JObject { ["goodId"] = "bread", ["quantity"] = 2 }),
                ["outputs"] = new JArray(new JObject { ["goodId"] = "grain", ["quantity"] = 1 })
            });
        });
        var content = ContentLoader.Load(json);
        Assert.NotNull(ProductionSpecialization.Conflict(content, new[] { "reclaim_grain", "bake_bread" }));
        Assert.NotNull(ProductionSpecialization.Conflict(content, new[] { "reclaim_grain", "mill_flour" }));
        Assert.Null(ProductionSpecialization.Conflict(content, new[] { "reclaim_grain", "harvest_grain" }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructorRejectsPublicOrPotentialProducerBesidePotentialConsumerUnlessExplicitlyDisabled(bool publicProducer)
    {
        var content = Content();
        RegionalWorldState Create(bool separation)
        {
            var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
            json["enforceProductionSeparation"] = separation;
            var identity = new RunIdentity(null, RunScenario.MerchantCharter,
                companyStorageRules: CompanyStorageRules.Legacy, itineraryVersion: 0, economicRulesVersion: 0,
                generationConfiguration: WorldGenerationConfiguration.Parse(json.ToString()));
            RegionalPortConfiguration Port(string id)
            {
                var facilities = id == "a" && publicProducer
                    ? new[] { new ScheduledFacilityConfiguration(new BuildingInstance("camp", "lumber_camp", "harvest_logs"), 2) }
                    : Array.Empty<ScheduledFacilityConfiguration>();
                return new RegionalPortConfiguration(new PortConfiguration(id,
                    content.Goods.Select(g => new MarketQuote(g.Id, 1, 2)), 100, facilities.Select(f => f.Instance)),
                    id, 0, 0, facilities, Array.Empty<ConsumptionConfiguration>());
            }
            var blueprints = new List<IndustryBlueprint>
            {
                new("saw", "sawmill", "saw_planks", 10, 2, 20, new[] { "a" })
            };
            if (!publicProducer) blueprints.Add(new IndustryBlueprint("camp_plan", "lumber_camp", "harvest_logs", 10, 2, 20, new[] { "a" }));
            return new RegionalWorldState(content, new[] { Port("a"), Port("b") }, new[] { new SeaRoute("a", "b", 100) },
                new[] { new RegionalShipConfiguration(new ShipConfiguration("ship", 4, "a"), "owner", "Ship", 100, 100) },
                industryBlueprints: blueprints, runIdentity: identity);
        }
        var failure = Assert.Throws<ArgumentException>(() => Create(true));
        Assert.Contains("harvest_logs", failure.Message);
        Assert.Contains("saw_planks", failure.Message);
        var unrestricted = Create(false);
        unrestricted.ConstructFacility("owner", "a", "saw", "paid_sawmill");
        Assert.True(unrestricted.Snapshot().PrivateFacilities.ContainsKey("paid_sawmill"));
    }

    [Theory]
    [InlineData("mine_silver", false, "Royal Mint")]
    [InlineData("saw_planks", true, "Shipyard")]
    public void RoyalInstitutionsRejectPublicOrFuturePrivateLocalInputProduction(string recipe, bool shipyard, string institution)
    {
        var content = Content();
        var identity = new RunIdentity(null, RunScenario.MerchantCharter);
        var initial = RunScenario.Create(content, identity).Snapshot();
        var economy = initial.Economy!.Configuration;
        var target = shipyard ? economy.ShipwrightPortId : economy.MintPortId;
        var source = initial.IndustryBlueprints.Values.Single(b => b.RecipeId == recipe);
        var blueprint = new IndustryBlueprint(source.Id, source.BuildingId, source.RecipeId, source.ConstructionCost,
            source.IntervalDays, source.WarehouseCapacity, new[] { target }, source.OutputStockLimit, source.WagePerBatch);
        Assert.Contains(institution, ProductionSpecialization.InstitutionConflict(content, new[] { recipe }, target, economy));
        var ports = initial.Ports.Values.Select(port => new RegionalPortConfiguration(new PortConfiguration(port.Id,
                port.Configuration.Market.Quotes.Values, port.Configuration.Market.InitialMarketCash,
                Array.Empty<BuildingInstance>(), port.Configuration.Market.InitialQuantities), port.Name, port.X, port.Y,
            Array.Empty<ScheduledFacilityConfiguration>(), port.Configuration.Consumption,
            settlement: port.Configuration.Settlement));
        var error = Assert.Throws<ArgumentException>(() => new RegionalWorldState(content, ports, initial.Routes,
            initial.Ships.Values.Select(ship => ship.Configuration),
            companies: initial.Companies.Values.Select(company => new CompanyConfiguration(company.Id, company.Cash)),
            industryBlueprints: new[] { blueprint }, runIdentity: identity, economy: economy,
            circulation: initial.Circulation!.Configuration));
        Assert.Contains(institution, error.Message);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(99)]
    public void PublicPrivateAndGrantedProductionRemainCompatibleAfterConstruction(int seed)
    {
        var content = Content();
        var identity = new RunIdentity(null, RunScenario.IndustrialCharter, seed: seed);
        var world = RunScenario.Create(content, identity);
        foreach (var port in world.Snapshot().Ports.Values)
        {
            var recipes = port.Facilities.Select(f => f.Configuration.Instance.RecipeId)
                .Concat(world.Snapshot().IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(port.Id)).Select(b => b.RecipeId))
                .Concat(world.Snapshot().PrivateFacilities.Values.Where(f => f.PortId == port.Id).Select(f => f.Blueprint.RecipeId));
            Assert.Null(ProductionSpecialization.Conflict(content, recipes));
            Assert.Null(ProductionSpecialization.InstitutionConflict(content, recipes, port.Id, world.Snapshot().Economy!.Configuration));
        }
        foreach (var blueprint in world.Snapshot().IndustryBlueprints.Values)
        {
            var permitted = blueprint.EligiblePortIds[0];
            var forbidden = world.Snapshot().Ports.Keys.FirstOrDefault(port => !blueprint.EligiblePortIds.Contains(port));
            if (forbidden != null)
            {
                var unchanged = world.Snapshot();
                Assert.Throws<InvalidOperationException>(() => world.ConstructFacility(WorldIds.PlayerOwnerId, forbidden,
                    blueprint.Id, "forbidden_" + blueprint.Id));
                Assert.Same(unchanged, world.Snapshot());
            }
            // Each ordinary construction still enforces the generated local capability set.
            world.ConstructFacility(WorldIds.PlayerOwnerId, permitted, blueprint.Id, "allowed_" + blueprint.Id);
        }
        foreach (var port in world.Snapshot().Ports.Values)
            Assert.Null(ProductionSpecialization.Conflict(content, port.Facilities.Select(f => f.Configuration.Instance.RecipeId)
                .Concat(world.Snapshot().PrivateFacilities.Values.Where(f => f.PortId == port.Id).Select(f => f.Blueprint.RecipeId))));
    }
}
