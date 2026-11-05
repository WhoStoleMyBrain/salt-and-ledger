using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ProjectTests
{
    private const string Owner = WorldIds.PlayerOwnerId;
    private const string Ship = WorldIds.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    private static RegionalWorldState World(int seed = 42)
    {
        var settings = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        settings["npc"]!["minimumCount"] = 0;
        settings["npc"]!["maximumCount"] = 0;
        settings["npc"]!["tradersPerTownPermille"] = 0;
        foreach (var good in new[] { "planks", "logs", "bread" })
        {
            settings["startingInventories"]![good]!["min"] = 20;
            settings["startingInventories"]![good]!["max"] = 20;
        }
        return RunScenario.Create(Content(), new RunIdentity(null, RunScenario.MerchantCharter,
            seed: seed, generationConfiguration: WorldGenerationConfiguration.Parse(settings.ToString())));
    }

    private static void Sail(RegionalWorldState world, string port)
    {
        if (world.Snapshot().Ships[Ship].DockedPortId == port) return;
        world.Depart(Ship, port);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) world.AdvanceDay();
    }

    private static OpportunitySnapshot Project(RegionalWorldState world, string id) => world.Snapshot().Opportunities!.Entries.Single(e => e.Id == id);

    private static OpportunitySnapshot Prepare(RegionalWorldState world, OpportunityKind kind)
    {
        var project = world.Snapshot().Opportunities!.Entries.First(e => e.Kind == kind);
        world.AcceptOpportunity(Owner, project.Id);
        var source = world.Snapshot().Ships[Ship].DockedPortId!;
        var bought = world.ExecuteTrade(Ship, source, new TradeCommand(TradeDirection.Buy, project.GoodId, project.RequiredQuantity));
        Assert.Equal(TradeOutcome.Traded, bought.Trades.Single().Trade.Outcome);
        Sail(world, project.PortId);
        return project;
    }

    [Fact]
    public void PartialInvestmentPersistsAndCreatesStandaloneWarehouseExactlyOnce()
    {
        var world = World();
        var project = Prepare(world, OpportunityKind.TradingDepot);
        Assert.Empty(world.Snapshot().PrivateFacilities);
        var before = world.Snapshot();
        var money = before.TotalCash;
        world.ContributeProject(Owner, project.Id, Ship, EconomicUnits.FromWhole(2), project.RequiredCash / 2, before);
        var partial = world.Snapshot();
        Assert.Equal(OpportunityStatus.Active, Project(world, project.Id).Status);
        Assert.False(partial.CompanyStockpiles.ContainsKey(CompanyStockpileSnapshot.StoreId(Owner, project.PortId)));
        Assert.Equal(money, partial.TotalCash);
        Assert.Equal(project.RequiredCash / 2 + EconomicUnits.FromWhole(2) * partial.Economy!.Configuration.GoodsReferencePrices[project.GoodId],
            partial.CompanyValue(Owner).PendingCapital);
        var loaded = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
        foreach (var current in new[] { world, loaded })
            current.ContributeProject(Owner, project.Id, Ship, EconomicUnits.FromWhole(2), project.RequiredCash / 2);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
        var completed = world.Snapshot();
        var store = completed.CompanyStockpiles[CompanyStockpileSnapshot.StoreId(Owner, project.PortId)];
        Assert.Equal(OpportunityStatus.Succeeded, Project(world, project.Id).Status);
        Assert.Equal(DevelopmentProjects.DepotCapacity, store.Capacity);
        Assert.Equal(0, store.StoredUnits);
        Assert.Equal(0, completed.CompanyValue(Owner).PendingCapital);
        Assert.Equal(money, completed.TotalCash);
        Assert.Equal(project.RequiredCash, completed.Accounting.Companies[Owner].ProjectExpenditure);
        Assert.True(completed.Accounting.Companies[Owner].ProjectMaterialCost > 0);
        world.ContributeProject(Owner, project.Id, Ship, 0, 1);
        Assert.Same(completed, world.Snapshot());
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world))));
    }

    [Fact]
    public void StandaloneDepotUsesOrdinaryTradingFifoTransfersAndWarehouseAutomation()
    {
        var world = World();
        var project = Prepare(world, OpportunityKind.TradingDepot);
        world.ContributeProject(Owner, project.Id, Ship, project.RequiredQuantity, project.RequiredCash);
        var id = CompanyStockpileSnapshot.StoreId(Owner, project.PortId);
        var snapshot = world.Snapshot();
        var buy = new TradeCommand(TradeDirection.Buy, "logs", EconomicUnits.FromWhole(3));
        var preview = snapshot.PreviewWarehouseTrade(Owner, id, buy);
        var result = world.ExecuteWarehouseTrade(Owner, id, buy, snapshot);
        Assert.Equal(TradeOutcome.Traded, preview.Outcome);
        Assert.Equal(preview.ExecutedAmount, result.WarehouseTrades.Single().Trade.ExecutedAmount);
        var purchaseBasis = result.After.Accounting.Stores[id].TotalCostBasis;
        var totalMoney = result.After.TotalCash;
        var transfer = world.TransferGoods(Owner, Ship, id, TransferDirection.WarehouseToShip, "logs", EconomicUnits.FromWhole(2));
        Assert.Equal(TransferOutcome.Transferred, transfer.Transfers.Single().Outcome);
        Assert.Equal(totalMoney, transfer.After.TotalCash);
        Assert.Equal(purchaseBasis, transfer.After.Accounting.Stores[id].TotalCostBasis + transfer.After.Accounting.Stores["ship:" + Ship].TotalCostBasis);
        world.TransferGoods(Owner, Ship, id, TransferDirection.ShipToWarehouse, "logs", EconomicUnits.FromWhole(2));
        world.ConfigureWarehouse(Owner, project.PortId, new WarehousePolicy(rules: new[] {
            new WarehouseCommodityRule("logs", false, 0, 0, true, EconomicUnits.FromWhole(1), 0)
        }));
        world.AdvanceDay();
        Assert.Equal(EconomicUnits.FromWhole(1), world.Snapshot().CompanyStockpiles[id].Inventory["logs"]);
        Assert.True(world.Snapshot().CompanyStockpiles[id].SalesRevenue > 0);
        var loaded = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        for (var day = 0; day < 4; day++)
        {
            world.AdvanceDay(); loaded.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
        }
    }

    [Theory]
    [InlineData(42)]
    [InlineData(99)]
    [InlineData(-42)]
    public void PublicCommissionCreatesCanonicalFacilityAndActualFundedProduction(int seed)
    {
        var world = World(seed);
        var project = Prepare(world, OpportunityKind.PublicIndustryCommission);
        var before = world.Snapshot();
        var blueprint = before.IndustryBlueprints[project.BlueprintId!];
        var count = before.Ports[project.PortId].Facilities.Count;
        var value = before.CompanyValue(Owner).Total;
        world.ContributeProject(Owner, project.Id, Ship, project.RequiredQuantity, project.RequiredCash);
        var completed = world.Snapshot();
        Assert.Equal(count + 1, completed.Ports[project.PortId].Facilities.Count);
        Assert.Equal(before.TotalCash, completed.TotalCash);
        Assert.Equal(value - project.RequiredCash - (long)project.RequiredQuantity * completed.Economy!.Configuration.GoodsReferencePrices[project.GoodId],
            completed.CompanyValue(Owner).Total);
        var facility = completed.Ports[project.PortId].Facilities.Single(f => f.Configuration.Instance.Id == project.FacilityId);
        Assert.Equal(blueprint.RecipeId, facility.Configuration.Instance.RecipeId);
        Assert.Equal(completed.Day + blueprint.IntervalDays, facility.NextDueDay);
        Assert.Equal(blueprint.WagePerBatch, facility.Configuration.WagePerBatch);
        var content = Content().ForExpandingEconomy();
        Assert.Null(ProductionSpecialization.Conflict(content, completed.Ports[project.PortId].Facilities.Select(f => f.Configuration.Instance.RecipeId)
            .Concat(completed.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(project.PortId)).Select(b => b.RecipeId))));
        var loaded = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
        var produced = false;
        for (var day = 0; day < blueprint.IntervalDays + 4; day++)
        {
            var advance = world.AdvanceDay(); loaded.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
            produced |= advance.Production.Any(p => p.FacilityId == project.FacilityId && p.Outcome == ScheduledProductionOutcome.Produced);
        }
        Assert.True(produced, "The commissioned facility must execute an ordinary funded canonical production batch.");
    }

    [Fact]
    public void InvalidContributionPreservesExactSnapshotAndDoesNotDuplicateInvestment()
    {
        var world = World();
        var project = Prepare(world, OpportunityKind.TradingDepot);
        var before = world.Snapshot();
        Assert.Equal(ProjectContributionOutcome.ExcessContribution,
            before.PreviewProjectContribution(Owner, project.Id, Ship, project.RequiredQuantity + 10000, 0).Outcome);
        world.ContributeProject(Owner, project.Id, Ship, project.RequiredQuantity + 10000, 0);
        Assert.Same(before, world.Snapshot());
        var other = before.Ports.Keys.First(port => port != project.PortId);
        Sail(world, other);
        var remote = world.Snapshot();
        Assert.Equal(ProjectContributionOutcome.WrongPort,
            remote.PreviewProjectContribution(Owner, project.Id, Ship, 10000, 0).Outcome);
        world.ContributeProject(Owner, project.Id, Ship, 10000, 0);
        Assert.Same(remote, world.Snapshot());
        Assert.Throws<InvalidOperationException>(() => world.ContributeProject(Owner, project.Id, Ship, 0, 1, before));
        Assert.Same(remote, world.Snapshot());
    }

    [Fact]
    public void DepotCapacityRejectsPaidTradesAndCargoTransfersWithoutMovingGoodsOrMoney()
    {
        var world = World();
        var project = Prepare(world, OpportunityKind.TradingDepot);
        world.ContributeProject(Owner, project.Id, Ship, project.RequiredQuantity, project.RequiredCash);
        var id = CompanyStockpileSnapshot.StoreId(Owner, project.PortId);
        // Reserve one real cargo unit so the final unloading attempt has a physical source.
        world.ExecuteTrade(Ship, project.PortId, new TradeCommand(TradeDirection.Buy, "logs", EconomicUnits.FromWhole(1)));
        foreach (var good in world.Snapshot().Ports[project.PortId].Inventory.Keys)
        {
            var quantity = world.Snapshot().MaximumWarehouseTradeQuantity(Owner, id, TradeDirection.Buy, good);
            if (quantity > 0) world.ExecuteWarehouseTrade(Owner, id, new TradeCommand(TradeDirection.Buy, good, quantity));
            if (world.Snapshot().CompanyStockpiles[id].StoredUnits == DevelopmentProjects.DepotCapacity) break;
        }
        var full = world.Snapshot();
        Assert.Equal(DevelopmentProjects.DepotCapacity, full.CompanyStockpiles[id].StoredUnits);
        var available = full.Ports[project.PortId].Inventory.First(g => g.Value >= EconomicUnits.QuantityScale).Key;
        var trade = world.ExecuteWarehouseTrade(Owner, id, new TradeCommand(TradeDirection.Buy, available, 1));
        Assert.Equal(TradeOutcome.InsufficientCargoCapacity, trade.WarehouseTrades.Single().Trade.Outcome);
        Assert.Same(full, world.Snapshot());
        var transfer = world.TransferGoods(Owner, Ship, id, TransferDirection.ShipToWarehouse, "logs", EconomicUnits.FromWhole(1));
        Assert.Equal(TransferOutcome.InsufficientCapacity, transfer.Transfers.Single().Outcome);
        Assert.Same(full, world.Snapshot());
    }

    [Fact]
    public void EarnedInfrastructurePermitOffersOtherwiseUnavailableCompatiblePublicIndustry()
    {
        var world = World();
        var contract = world.Snapshot().Opportunities!.Entries.First(e => !e.IsProject &&
            e.RewardKind == OpportunityRewardKind.InfrastructureAccess);
        Assert.False(world.Snapshot().Opportunities!.HasInfrastructureAccess(Owner, contract.PortId));
        world.AcceptOpportunity(Owner, contract.Id);
        var origin = world.Snapshot().Ships[Ship].DockedPortId!;
        Assert.Equal(TradeOutcome.Traded, world.ExecuteTrade(Ship, origin,
            new TradeCommand(TradeDirection.Buy, contract.GoodId, contract.RequiredQuantity)).Trades.Single().Trade.Outcome);
        Sail(world, contract.PortId);
        var content = Content().ForExpandingEconomy();
        if (contract.Kind == OpportunityKind.RecurringSupply)
        {
            for (var window = 0; window < contract.WindowCount; window++)
            {
                world.DeliverContract(Ship, contract.Id, contract.RequiredQuantity / contract.WindowCount);
                if (window + 1 == contract.WindowCount) break;
                var deadline = Project(world, contract.Id).CurrentWindowDeadlineDay;
                while (world.Snapshot().Day <= deadline) world.AdvanceDay();
            }
        }
        else
        {
            Assert.Equal(OpportunityKind.IndustrialSupply, contract.Kind);
            world.DeliverContract(Ship, contract.Id, contract.RequiredQuantity);
            // Buy real output to make room for the funded public batches required by the contract.
            var facility = world.Snapshot().Ports[contract.PortId].Facilities.Single(f => f.Configuration.Instance.Id == contract.FacilityId);
            var output = content.GetRecipe(facility.Configuration.Instance.RecipeId).Outputs.Single().GoodId;
            var take = Math.Min(EconomicUnits.FromWhole(10), world.Snapshot().MaximumTradeQuantity(Ship, contract.PortId, TradeDirection.Buy, output));
            if (take > 0) world.ExecuteTrade(Ship, contract.PortId, new TradeCommand(TradeDirection.Buy, output, take));
            for (var day = 0; day < 32 && Project(world, contract.Id).Status == OpportunityStatus.Active; day++) world.AdvanceDay();
        }
        Assert.Equal(OpportunityStatus.Succeeded, Project(world, contract.Id).Status);
        Assert.True(world.Snapshot().Opportunities!.HasInfrastructureAccess(Owner, contract.PortId));
        // Make board space for this new permission; declining has no financial effect.
        foreach (var offer in world.Snapshot().Opportunities!.Entries.Where(e => e.IsProject && e.Status == OpportunityStatus.Available).ToArray())
            world.DeclineOpportunity(Owner, offer.Id);
        var until = world.Snapshot().Opportunities!.NextGenerationDay;
        while (world.Snapshot().Day < until) world.AdvanceDay();
        var unlocked = world.Snapshot().Opportunities!.Entries.First(e => e.Kind == OpportunityKind.PublicIndustryCommission &&
            e.Status == OpportunityStatus.Available && e.PortId == contract.PortId &&
            !world.Snapshot().IndustryBlueprints[e.BlueprintId!].EligiblePortIds.Contains(contract.PortId));
        var blueprint = world.Snapshot().IndustryBlueprints[unlocked.BlueprintId!];
        Assert.Null(ProductionSpecialization.Conflict(content, world.Snapshot().Ports[contract.PortId].Facilities
            .Select(f => f.Configuration.Instance.RecipeId).Concat(world.Snapshot().IndustryBlueprints.Values
                .Where(b => b.EligiblePortIds.Contains(contract.PortId)).Select(b => b.RecipeId)).Append(blueprint.RecipeId)));
        world.AcceptOpportunity(Owner, unlocked.Id);
        world.ContributeProject(Owner, unlocked.Id, null, 0, 1);
        Assert.Equal(1, Project(world, unlocked.Id).ContributedCash);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world))));
    }
}
