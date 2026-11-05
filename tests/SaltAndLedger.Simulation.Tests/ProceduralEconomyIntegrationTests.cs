using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ProceduralEconomyIntegrationTests
{
    private const string Owner = WorldIds.PlayerOwnerId;
    private const string Ship = WorldIds.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    [Theory]
    [InlineData(-42)]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(99)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void MerchantsFollowRealSupplyAndDemandWithFiniteAccountsAndExactContinuation(int seed)
    {
        var content = Content();
        var generation = ExpandingEconomyGeneration.Generate(seed, content: content);
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter, seed: seed));
        var initial = world.Snapshot();
        var settings = generation.Configuration.Npc;
        var expectedCount = Math.Max(settings.MinimumCount, Math.Min(settings.MaximumCount,
            (generation.Ports.Count * settings.TradersPerTownPermille + 999) / 1000));
        Assert.Equal(expectedCount, initial.Ships.Values.Count(ship => !ship.IsPlayer));
        Assert.Equal(expectedCount + 1, initial.Companies.Count);
        Assert.Equal(generation.ShipyardPortId, initial.Economy!.Configuration.ShipwrightPortId);
        Assert.Equal(generation.Configuration.FleetLimit, initial.Economy.Configuration.MaximumPlayerShips);
        foreach (var ship in initial.Ships.Values.Where(ship => !ship.IsPlayer))
        {
            var policy = ship.Policy!;
            var source = generation.Ports.Single(port => port.Id == policy.OriginPortId);
            var destination = generation.Ports.Single(port => port.Id == policy.DestinationPortId);
            var scoped = content.ForExpandingEconomy();
            Assert.Contains(source.Facilities, facility => scoped.GetRecipe(facility.Instance.RecipeId)
                .Outputs.Any(output => output.GoodId == policy.Outbound.GoodId));
            Assert.True(destination.Facilities.Any(facility => scoped.GetRecipe(facility.Instance.RecipeId)
                .Inputs.Any(input => input.GoodId == policy.Outbound.GoodId)) ||
                destination.Consumption.Any(need => need.GoodId == policy.Outbound.GoodId));
            Assert.InRange(initial.Companies[ship.OwnerId].Cash, EconomicUnits.Coins(settings.StartingCash.Minimum),
                EconomicUnits.Coins(settings.StartingCash.Maximum));
            Assert.Equal(0, ship.CargoUnits);
            Assert.True(SeaNavigation.FindPath(generation.Geography.Routes, policy.OriginPortId,
                policy.DestinationPortId, ship.Configuration.Speed).DurationDays > 0);
        }
        var reversed = RunScenario.Create(content, initial.RunIdentity!, reverseConfiguration: true);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(reversed));
        for (var day = 0; day < 24; day++)
        {
            world.AdvanceDay();
            reversed.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(reversed));
            Audit(initial.TotalCash, world.Snapshot());
        }
        var loaded = WorldSaveCodec.Load(content, WorldSaveCodec.Save(world));
        for (var day = 0; day < 7; day++)
        {
            world.AdvanceDay(); loaded.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
        }
    }

    [Fact]
    public void ConfiguredIndustrialStartingTownAlsoDefinesItsPhysicalShippingPolicy()
    {
        var initial = ExpandingEconomyGeneration.Generate(42);
        var chosenPort = initial.EligiblePorts("bake_bread").Last();
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["institutions"]!["startingPortIndex"] = int.Parse(chosenPort.Substring(5), System.Globalization.CultureInfo.InvariantCulture) - 1;
        var configuration = WorldGenerationConfiguration.Parse(json.ToString());
        var generation = ExpandingEconomyGeneration.Generate(42, configuration: configuration);
        var world = RunScenario.Create(Content(), new RunIdentity(null, RunScenario.IndustrialCharter,
            seed: 42, generationConfiguration: configuration));
        var policy = ExpandingEconomyScenario.IndustrialPolicy(generation);
        Assert.Equal(chosenPort, world.Snapshot().Ships[Ship].DockedPortId);
        Assert.Equal(chosenPort, world.Snapshot().PrivateFacilities[RunScenario.StarterBakery].PortId);
        Assert.Equal(chosenPort, policy.OriginPortId);
        Assert.NotEqual(policy.OriginPortId, policy.DestinationPortId);
        world.ConfigureIndustrialAutomation(Ship, policy, false);
        Assert.Equal(policy.OriginPortId, world.Snapshot().Ships[Ship].IndustrialPolicy!.OriginPortId);
    }

    [Fact]
    public void IndustrialStarterResolvesActualBlueprintWhenAProductionBuildingHasAdditionalRecipes()
    {
        var json = JObject.Parse(ContentLoaderTests.CanonicalJson());
        var recipes = (JArray)json["recipes"]!;
        var secondBakeryRecipe = recipes.Single(recipe => (string?)recipe["id"] == "bake_bread").DeepClone();
        secondBakeryRecipe["id"] = "bake_festival_bread";
        recipes.Add(secondBakeryRecipe);
        var content = ContentLoader.Load(json.ToString());
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.IndustrialCharter, seed: 42));
        var starter = world.Snapshot().PrivateFacilities[RunScenario.StarterBakery];
        Assert.Equal("bake_bread", starter.Blueprint.RecipeId);
        Assert.Equal("private_bakery_bake_bread", starter.Blueprint.Id);
        Assert.Contains(starter.PortId, starter.Blueprint.EligiblePortIds);
        Assert.Contains(world.Snapshot().IndustryBlueprints.Values, blueprint => blueprint.RecipeId == "bake_festival_bread");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void CustomFleetAndInstitutionsAreAuthoritativeAndContractDeadlinesRespectHorizon(int fleetLimit)
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["fleetLimit"] = fleetLimit;
        json["deadlineDays"] = 90;
        json["institutions"]!["mintPortIndex"] = 1;
        json["institutions"]!["shipyardPortIndex"] = 2;
        json["marketCash"]!["min"] = 1000;
        json["marketCash"]!["max"] = 1000;
        json["startingInventories"]!["planks"]!["min"] = 40;
        json["startingInventories"]!["planks"]!["max"] = 40;
        var configuration = WorldGenerationConfiguration.Parse(json.ToString());
        var generation = ExpandingEconomyGeneration.Generate(42, configuration: configuration);
        var content = Content();
        var identity = new RunIdentity(null, RunScenario.MerchantCharter, seed: 42, generationConfiguration: configuration);
        var world = RunScenario.Create(content, identity);
        var economy = world.Snapshot().Economy!.Configuration;
        Assert.Equal(fleetLimit, economy.MaximumPlayerShips);
        Assert.Equal(generation.ShipyardPortId, economy.ShipwrightPortId);
        Assert.NotEqual(economy.MintPortId, economy.ShipwrightPortId);
        Assert.All(economy.Contracts, contract => Assert.InRange(contract.DeadlineDay, 0, 90));
        Assert.Equal(100000, economy.Contracts.Single(contract => contract.Id == "royal_planks").Quantity);
        var initial = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.CommissionShip(Owner, economy.MintPortId));
        Assert.Same(initial, world.Snapshot());
        var attempt = world.CommissionShip(Owner, economy.ShipwrightPortId).Development.Single();
        Assert.Equal(fleetLimit == 1 ? DevelopmentOutcome.FleetLimit : DevelopmentOutcome.Started, attempt.Outcome);
        if (fleetLimit == 2)
        {
            var pending = world.Snapshot();
            Assert.Equal(DevelopmentOutcome.FleetLimit, world.CommissionShip(Owner, economy.ShipwrightPortId).Development.Single().Outcome);
            Assert.Same(pending, world.Snapshot());
        }
        var loaded = WorldSaveCodec.Load(content, WorldSaveCodec.Save(world));
        for (var day = 0; day < 5; day++)
        {
            world.AdvanceDay(); loaded.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
            Audit(initial.TotalCash, world.Snapshot());
        }
        Assert.Equal(fleetLimit == 1 ? 1 : 2, world.Snapshot().Ships.Values.Count(ship => ship.IsPlayer));
    }

    [Fact]
    public void EightToTwelveTownJourneyBuildsProducesShipsAndTradesWithRealObjectiveProgress()
    {
        var content = Content();
        var draft = Enumerable.Range(0, 64).Select(seed => GrandCommissionDraft.Generate(seed))
            .First(value => value.Rounds[1].Offers.Any(goal => goal.TemplateId == "new_industry"));
        var choices = draft.Rounds.Select(round => round.Category == CommissionCategory.Industry
            ? "new_industry" : round.Offers[0].TemplateId).ToArray();
        var identity = new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter,
            seed: draft.Seed, commission: draft.Resolve(choices));
        var generation = ExpandingEconomyGeneration.Generate(draft.Seed, content: content);
        var world = RunScenario.Create(content, identity);
        var initial = world.Snapshot();
        Assert.InRange(initial.Ports.Count, 8, 12);
        var millPort = generation.EligiblePorts("mill_flour").First();
        var bakeryPort = generation.EligiblePorts("bake_bread").First();
        Assert.NotEqual(millPort, bakeryPort);
        world.ConstructFacility(Owner, millPort, ExpandingEconomyGeneration.MillBlueprint, "journey_mill");
        world.ConstructFacility(Owner, bakeryPort, WorldIds.BakeryBlueprint, "journey_bakery");
        Assert.Equal(TradeOutcome.Traded, world.ExecuteWarehouseTrade(Owner, "journey_mill",
            new TradeCommand(TradeDirection.Buy, "grain", EconomicUnits.FromWhole(2))).WarehouseTrades.Single().Trade.Outcome);
        Assert.Equal(TradeOutcome.Traded, world.ExecuteWarehouseTrade(Owner, "journey_bakery",
            new TradeCommand(TradeDirection.Buy, "logs", EconomicUnits.FromWhole(1))).WarehouseTrades.Single().Trade.Outcome);
        Sail(millPort);
        while (world.Snapshot().PrivateFacilities["journey_mill"].BatchesProduced == 0) Day();
        world.SetFacilityEnabled(Owner, "journey_mill", false);
        var flourBefore = world.Snapshot().GetFacilityStorage("journey_mill").Inventory["flour"];
        world.TransferGoods(Owner, Ship, "journey_mill", TransferDirection.WarehouseToShip, "flour", EconomicUnits.FromWhole(1));
        Assert.Equal(flourBefore - EconomicUnits.FromWhole(1), world.Snapshot().GetFacilityStorage("journey_mill").Inventory["flour"]);
        Sail(bakeryPort);
        world.TransferGoods(Owner, Ship, "journey_bakery", TransferDirection.ShipToWarehouse, "flour", EconomicUnits.FromWhole(1));
        while (world.Snapshot().PrivateFacilities["journey_bakery"].BatchesProduced == 0) Day();
        world.SetFacilityEnabled(Owner, "journey_bakery", false);
        Assert.True(GrandCommission.Progress(world.Snapshot()).Goals.Single(goal => goal.Id == "new_industry").IsMet);
        world.TransferGoods(Owner, Ship, "journey_bakery", TransferDirection.WarehouseToShip, "bread", EconomicUnits.FromWhole(2));
        var salePort = generation.Ports.First(port => port.Id != bakeryPort && port.Id != millPort).Id;
        Sail(salePort);
        var sold = world.ExecuteTrade(Ship, salePort, new TradeCommand(TradeDirection.Sell, "bread", EconomicUnits.FromWhole(2)));
        Assert.Equal(TradeOutcome.Traded, sold.Trades.Single().Trade.Outcome);
        Assert.Equal(EconomicUnits.FromWhole(2), sold.After.Accounting.Companies[Owner].ProducedQuantities["bread"]);
        Assert.True(sold.After.Accounting.Companies[Owner].WagesPaid > 0);
        Assert.True(sold.After.Accounting.Companies[Owner].InventoryPurchases > 0);
        Assert.True(sold.After.Accounting.Companies[Owner].SalesRevenue > 0);
        Assert.Equal(0, sold.After.Ships[Ship].CargoUnits);
        Audit(initial.TotalCash, sold.After);

        void Day()
        {
            Assert.True(world.Snapshot().Day < 180, "The bounded real journey must finish well before its run deadline.");
            Audit(initial.TotalCash, world.AdvanceDay().After);
        }
        void Sail(string destination)
        {
            var before = world.Snapshot();
            if (before.Ships[Ship].DockedPortId == destination) return;
            var expected = SeaNavigation.FindPath(before.Routes, before.Ships[Ship].DockedPortId!, destination,
                before.Ships[Ship].Configuration.Speed).DurationDays;
            var started = before.Day;
            var cargo = before.Ships[Ship].Cargo.ToArray();
            world.Depart(Ship, destination);
            var loaded = WorldSaveCodec.Load(content, WorldSaveCodec.Save(world));
            while (world.Snapshot().Ships[Ship].DockedPortId == null)
            {
                Day(); loaded.AdvanceDay();
                Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
                Assert.Equal(cargo, world.Snapshot().Ships[Ship].Cargo.ToArray());
            }
            Assert.Equal(expected, world.Snapshot().Day - started);
            Assert.Equal(destination, world.Snapshot().Ships[Ship].DockedPortId);
        }
    }

    private static void Audit(BigInteger initialCash, RegionalWorldSnapshot snapshot)
    {
        Assert.Equal(initialCash + snapshot.Economy!.MintedCurrency, snapshot.TotalCash);
        foreach (var company in snapshot.Accounting.Companies.Values)
            Assert.Equal(company.InventoryPurchases, company.CostOfGoodsSold + company.HeldInventoryCost + company.MintMaterialCost);
        foreach (var store in snapshot.Accounting.Stores.Values)
            foreach (var good in store.Goods)
            {
                var inventory = store.StoreId.StartsWith("ship:", StringComparison.Ordinal)
                    ? snapshot.Ships[store.StoreId.Substring(5)].Cargo : snapshot.CompanyStockpiles[store.StoreId].Inventory;
                Assert.Equal(inventory[good.Key], store.GetQuantity(good.Key));
            }
    }
}
