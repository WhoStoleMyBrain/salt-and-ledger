using System;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionAtomicTests
{
    private const string Owner = WorldIds.PlayerOwnerId;
    private const string Ship = WorldIds.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World()
    {
        var plan = GrandCommissionDraft.Generate(42).Resolve(new[]
            { "all_harbors", "timber_chain", "royal_exporter", "royal_institution" });
        return RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId,
            RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42, commission: plan));
    }

    private static RegionalWorldState OverflowFixture(long revision, bool oneDayRoutes = false)
    {
        var initial = World().Snapshot();
        return new RegionalWorldState(Content().ForExpandingEconomy(), initial.Ports.Values.Select(p => p.Configuration),
            oneDayRoutes ? initial.Routes.Select(r => new SeaRoute(r.OriginPortId, r.DestinationPortId, 1)) : initial.Routes,
            initial.Ships.Values.Select(s => s.Configuration), initialRevision: revision,
            companies: initial.Companies.Values.Select(c => new CompanyConfiguration(c.Id, c.Cash)),
            industryBlueprints: initial.IndustryBlueprints.Values, challenge: initial.Challenge!.Configuration,
            runIdentity: initial.RunIdentity, economy: initial.Economy!.Configuration, circulation: initial.Circulation!.Configuration);
    }

    [Fact]
    public void LateCommitOverflowRollsBackRealLocalTradeAndMissionHistoryTogether()
    {
        var world = OverflowFixture(long.MaxValue);
        var before = world.Snapshot();
        var port = before.Ships[Ship].DockedPortId!;
        Assert.Equal(TradeOutcome.Traded, before.PreviewTrade(Ship, port,
            new TradeCommand(TradeDirection.Buy, "bread", 10000)).Outcome);
        Assert.Throws<OverflowException>(() => world.ExecuteTrade(Ship, port,
            new TradeCommand(TradeDirection.Buy, "bread", 10000)));
        Assert.Same(before, world.Snapshot());
        Assert.Empty(world.Snapshot().CommissionHistory!.Markets);
        Assert.Equal(0, world.Snapshot().Accounting.Companies[Owner].InventoryPurchases);
    }

    [Fact]
    public void LateDailyOverflowRollsBackStagedArrivalVisitAndEveryEarlierDailyPhase()
    {
        var world = OverflowFixture(long.MaxValue - 1, oneDayRoutes: true);
        var destination = world.Snapshot().Ports.Keys.First(p => p != world.Snapshot().Ships[Ship].DockedPortId);
        world.Depart(Ship, destination);
        var before = world.Snapshot();
        Assert.Equal(1, before.Ships[Ship].RemainingTravelDays);
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Null(world.Snapshot().Ships[Ship].DockedPortId);
        Assert.DoesNotContain(destination, world.Snapshot().CommissionHistory!.Ships.Single().Visits.Keys);
        Assert.Equal(0, world.Snapshot().Day);
    }

    [Fact]
    public void GenuineFinalHarborArrivalOnDay180WinsBeforeDeadlineFailureAndRestoresFrozen()
    {
        var world = World();
        var generation = ExpandingEconomyGeneration.Generate(42);
        world.ConstructFacility(Owner, generation.RecoveryPortId, ExpandingEconomyGeneration.LumberBlueprint, "deadline_camp");
        world.ConstructFacility(Owner, generation.ProcessingPortId, WorldIds.SawmillBlueprint, "deadline_sawmill");
        world.ExecuteWarehouseTrade(Owner, "deadline_sawmill", new TradeCommand(TradeDirection.Buy, "logs", 80000));
        world.ConstructFacility(Owner, generation.FurniturePortId, ExpandingEconomyGeneration.SilverMineBlueprint, "deadline_mine");
        world.CommissionMint(Owner);
        void Step()
        {
            world.AdvanceDay();
            if (world.Snapshot().Challenge!.Status != ChallengeStatus.Active) return;
            foreach (var facility in world.Snapshot().PrivateFacilities.Values.ToArray())
                if (facility.Enabled && facility.BatchesProduced >= (facility.Id == "deadline_sawmill" ? 4 : 1))
                    world.SetFacilityEnabled(Owner, facility.Id, false);
        }
        void Sail(string port)
        {
            world.Depart(Ship, port);
            while (world.Snapshot().Ships[Ship].DockedPortId == null) Step();
        }
        Sail(generation.FurniturePortId);
        while (world.Snapshot().PrivateFacilities["deadline_mine"].BatchesProduced == 0) Step();
        world.TransferGoods(Owner, Ship, "deadline_mine", TransferDirection.WarehouseToShip, "silver", 10000);
        Assert.Equal(TradeOutcome.Traded, world.ExecuteTrade(Ship, generation.FurniturePortId,
            new TradeCommand(TradeDirection.Buy, "furniture", 10000)).Trades.Single().Trade.Outcome);
        Sail(generation.ProcessingPortId);
        world.FulfillExternalContract(Ship, "royal_furniture", 10000);
        world.MintSilver(Ship);
        while (world.Snapshot().PrivateFacilities["deadline_sawmill"].BatchesProduced < 4) Step();
        Assert.Equal(3, GrandCommission.Progress(world.Snapshot()).CurrentlyMetCount);
        Assert.Equal(3, world.Snapshot().CommissionHistory!.Ships.Single().Visits.Count);
        var duration = world.Snapshot().GetRoute(generation.ProcessingPortId, generation.RecoveryPortId)
            .DurationDays(world.Snapshot().Ships[Ship].Configuration.Speed);
        while (world.Snapshot().Day < GrandCommission.DeadlineDay - duration) Step();
        world.Depart(Ship, generation.RecoveryPortId);
        while (world.Snapshot().Day < GrandCommission.DeadlineDay - 1) Step();
        var pending = WorldSaveCodec.Save(world);
        var resumed = WorldSaveCodec.Load(Content(), pending);
        Assert.Equal(pending, WorldSaveCodec.Save(resumed));
        Step(); resumed.AdvanceDay();
        Assert.Equal(180, world.Snapshot().Day);
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.Equal(4, GrandCommission.Progress(world.Snapshot()).CurrentlyMetCount);
        Assert.True(world.Snapshot().Companies[Owner].Cash >= GrandCommission.MinimumCash);
        var terminal = WorldSaveCodec.Save(world);
        Assert.Equal(terminal, WorldSaveCodec.Save(resumed));
        Assert.Equal(terminal, WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), terminal)));
        Assert.Throws<InvalidOperationException>(() => resumed.Depart(Ship, generation.FoodSourcePortId));
        Assert.Throws<InvalidOperationException>(() => resumed.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => resumed.AbandonChallenge());
        Assert.Equal(terminal, WorldSaveCodec.Save(resumed));
    }
}
