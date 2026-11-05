using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;
using static SaltAndLedger.Simulation.Tests.OpportunityTestWorld;

namespace SaltAndLedger.Simulation.Tests;

public sealed class OpportunityTests
{
    [Theory]
    [InlineData(-42)]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(99)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void GeneratedBoardIsBoundedReachableAndExactlyReproducibleAcrossDays(int seed)
    {
        var world = Create(seed);
        var reversed = RunScenario.Create(Content(), world.Snapshot().RunIdentity!, reverseConfiguration: true);
        var initialCash = world.Snapshot().TotalCash;
        for (var day = 0; day <= 80; day++)
        {
            var snapshot = world.Snapshot();
            var board = snapshot.Opportunities!;
            if (day % 16 == 0) Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(reversed));
            Assert.Equal(board.Entries.Count, board.Entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
            Assert.True(board.Entries.Count(entry => !entry.IsProject && !entry.Id.StartsWith("royal_", StringComparison.Ordinal) && entry.Status == OpportunityStatus.Available)
                <= board.Configuration.MaximumAvailableContracts);
            foreach (var entry in board.Entries.Where(entry => entry.Status == OpportunityStatus.Available))
            {
                Assert.Contains(entry.PortId, snapshot.Ports.Keys);
                Assert.Contains(entry.GoodId, snapshot.RunIdentity!.GenerationConfiguration.EnabledGoods);
                Assert.True(entry.RequiredQuantity > 0);
                Assert.True(entry.FundingRequired >= 0);
                if (snapshot.Ships[Ship].DockedPortId != entry.PortId)
                    Assert.True(SeaNavigation.FindPath(snapshot.Routes,
                        snapshot.Ships[Ship].DockedPortId!, entry.PortId, snapshot.Ships[Ship].Configuration.Speed).DurationDays > 0);
            }
            Audit(snapshot, initialCash);
            if (day == 80) break;
            world.AdvanceDay(); reversed.AdvanceDay();
        }
    }

    [Theory]
    [InlineData("sparse-eight.json")]
    [InlineData("narrow-specialization.json")]
    [InlineData("twelve-town-commerce.json")]
    public void CustomWorldOffersOnlyReferenceEnabledReachableEconomicContent(string file)
    {
        var configuration = WorldGenerationConfiguration.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "WorldGenerationExamples", file)));
        var world = Create(99, configuration);
        var original = WorldSaveCodec.Save(world);
        Assert.Equal(original, WorldSaveCodec.Save(Reload(world)));
        foreach (var entry in world.Snapshot().Opportunities!.Entries)
        {
            Assert.Contains(entry.GoodId, configuration.EnabledGoods);
            Assert.Contains(entry.PortId, world.Snapshot().Ports.Keys);
            if (entry.BlueprintId != null)
                Assert.Contains(entry.BlueprintId, world.Snapshot().IndustryBlueprints.Keys);
        }
    }

    [Fact]
    public void ReducedIndustryWorldNeverOffersDisabledGoodsOrUnavailableFacilities()
    {
        var configuration = Configuration(json =>
        {
            json["enabledGoods"] = new JArray(((JArray)json["enabledGoods"]!).Where(good => (string?)good != "silver" && (string?)good != "furniture"));
            json["industries"] = new JArray(((JArray)json["industries"]!).Where(industry =>
                (string?)industry["industryId"] != "silver_mine" && (string?)industry["industryId"] != "furniture_workshop"));
            ((JObject)json["startingInventories"]!).Remove("silver");
            ((JObject)json["startingInventories"]!).Remove("furniture");
        });
        var world = Create(99, configuration);
        var initialCash = world.Snapshot().TotalCash;
        for (var day = 0; day < 48; day++)
        {
            foreach (var entry in world.Snapshot().Opportunities!.Entries)
            {
                Assert.Contains(entry.GoodId, configuration.EnabledGoods);
                if (entry.BlueprintId != null)
                    Assert.Contains(entry.BlueprintId, world.Snapshot().IndustryBlueprints.Keys);
            }
            Audit(world.AdvanceDay().After, initialCash);
        }
        Reload(world);
    }

    [Fact]
    public void BulkDeliveryRequiresAcceptanceAndRealCargoAndCannotPayTwiceOrCountAnOrdinarySale()
    {
        var world = Create(configuration: Configuration(json =>
            json["startingInventories"]!["planks"] = new JObject { ["min"] = 30, ["max"] = 30 }));
        var contract = Entry(world, "royal_planks");
        var original = world.Snapshot();
        world.DeliverContract(Ship, contract.Id, EconomicUnits.FromWhole(1));
        Assert.Same(original, world.Snapshot());
        world.AcceptOpportunity(Owner, contract.Id);
        var accepted = world.Snapshot();
        Assert.Equal(OpportunityStatus.Active, Entry(world, contract.Id).Status);
        Assert.Equal(original.TotalCash, accepted.TotalCash);
        Buy(world, contract.PortId, contract.GoodId, contract.RequiredQuantity);
        world.ExecuteTrade(Ship, contract.PortId, new TradeCommand(TradeDirection.Sell, contract.GoodId, EconomicUnits.FromWhole(1)));
        Assert.Equal(0, Entry(world, contract.Id).DeliveredQuantity);
        Buy(world, contract.PortId, contract.GoodId, EconomicUnits.FromWhole(1));
        var before = world.Snapshot();
        world.DeliverContract(Ship, contract.Id, EconomicUnits.FromWhole(1), before);
        var partial = world.Snapshot();
        Assert.Equal(before.Ships[Ship].Cargo[contract.GoodId] - EconomicUnits.FromWhole(1), partial.Ships[Ship].Cargo[contract.GoodId]);
        Assert.Equal(before.Companies[Owner].Cash + EconomicUnits.FromWhole(1) * contract.UnitPrice, partial.Companies[Owner].Cash);
        Assert.Equal(before.TotalCash, partial.TotalCash);
        Assert.Throws<InvalidOperationException>(() => world.DeliverContract(Ship, contract.Id, EconomicUnits.FromWhole(1), before));
        Assert.Same(partial, world.Snapshot());
        world = Reload(world);
        world.DeliverContract(Ship, contract.Id, Entry(world, contract.Id).RemainingQuantity);
        var finished = world.Snapshot();
        Assert.Equal(OpportunityStatus.Succeeded, Entry(world, contract.Id).Status);
        Assert.Equal(contract.FundingRequired, Entry(world, contract.Id).PaidCash);
        Assert.Equal(0, Entry(world, contract.Id).ReservedCash);
        Assert.Equal(0, finished.Ships[Ship].Cargo[contract.GoodId]);
        world.DeliverContract(Ship, contract.Id, EconomicUnits.FromWhole(1));
        Assert.Same(finished, world.Snapshot());
        Assert.Equal(original.TotalCash, finished.TotalCash);
        Audit(finished, original.TotalCash);
    }

    [Fact]
    public void AcceptingAnExclusiveTenderWithdrawsItsAlternativeWithoutRemovingHistory()
    {
        var world = Create();
        var competitors = world.Snapshot().Opportunities!.Entries
            .Where(entry => entry.ExclusiveGroupId != null).GroupBy(entry => entry.ExclusiveGroupId)
            .First(group => group.Count() >= 2).ToArray();
        Assert.Single(competitors.Select(entry => entry.GoodId).Distinct());
        Assert.True(competitors.Select(entry => entry.PortId).Distinct().Count() >= 2);
        var before = world.Snapshot();
        world.AcceptOpportunity(Owner, competitors[0].Id);
        Assert.Equal(OpportunityStatus.Active, Entry(world, competitors[0].Id).Status);
        Assert.All(competitors.Skip(1), entry => Assert.Equal(OpportunityStatus.Withdrawn, Entry(world, entry.Id).Status));
        Assert.Equal(before.TotalCash, world.Snapshot().TotalCash);
        var chosen = world.Snapshot();
        world.AcceptOpportunity(Owner, competitors[1].Id);
        Assert.Same(chosen, world.Snapshot());
        Reload(world);
    }

    [Fact]
    public void ActiveContractLimitRejectsFurtherAcceptanceAtomicallyAndProjectsUseSeparateCapacity()
    {
        var world = Create(configuration: Configuration(json => json["opportunities"]!["activeContractLimit"] = 1));
        var contracts = world.Snapshot().Opportunities!.Entries.Where(entry => !entry.IsProject && entry.ExclusiveGroupId == null).Take(2).ToArray();
        Assert.Equal(2, contracts.Length);
        world.AcceptOpportunity(Owner, contracts[0].Id);
        var before = world.Snapshot();
        world.AcceptOpportunity(Owner, contracts[1].Id);
        Assert.Same(before, world.Snapshot());
        var project = before.Opportunities!.Entries.First(entry => entry.IsProject);
        world.AcceptOpportunity(Owner, project.Id);
        Assert.Equal(OpportunityStatus.Active, Entry(world, project.Id).Status);
        Assert.Single(world.Snapshot().Opportunities!.Entries, entry => !entry.IsProject && entry.Status == OpportunityStatus.Active);
    }

    [Fact]
    public void SmallOfferLimitRetainsTheCompetingPairAndOrdinaryBulkDoesNotDominate()
    {
        var small = Create(configuration: Configuration(json => json["opportunities"]!["maximumAvailableContracts"] = 2));
        var ordinary = small.Snapshot().Opportunities!.Entries.Where(entry => !entry.IsProject &&
            !entry.Id.StartsWith("royal_", StringComparison.Ordinal) && entry.Status == OpportunityStatus.Available).ToArray();
        Assert.Equal(2, ordinary.Length);
        Assert.All(ordinary, entry => Assert.Equal(OpportunityKind.RecurringSupply, entry.Kind));
        Assert.NotNull(ordinary[0].CompetitionGroupId);
        Assert.Equal(ordinary[0].CompetitionGroupId, ordinary[1].CompetitionGroupId);
        var normal = Create().Snapshot().Opportunities!.Entries.Where(entry => !entry.IsProject && entry.Status == OpportunityStatus.Available).ToArray();
        Assert.True(normal.Count(entry => entry.Kind != OpportunityKind.BulkProcurement) > normal.Count(entry => entry.Kind == OpportunityKind.BulkProcurement));
    }

    [Fact]
    public void RoyalCompatibilityPreviewCannotBypassTheActiveCommitmentLimit()
    {
        var world = Create(configuration: Configuration(json =>
        {
            json["opportunities"]!["activeContractLimit"] = 1;
            json["startingInventories"]!["planks"] = new JObject { ["min"] = 30, ["max"] = 30 };
        }));
        var active = world.Snapshot().Opportunities!.Entries.First(entry => entry.Kind == OpportunityKind.RecurringSupply);
        world.AcceptOpportunity(Owner, active.Id);
        var royal = Entry(world, "royal_planks");
        Buy(world, royal.PortId, royal.GoodId, EconomicUnits.FromWhole(1));
        var before = world.Snapshot();
        Assert.Equal(DevelopmentOutcome.NotReady, before.PreviewExternalContract(Ship, royal.Id, EconomicUnits.FromWhole(1)).Outcome);
        Assert.Equal(DevelopmentOutcome.NotReady, Assert.Single(world.FulfillExternalContract(Ship, royal.Id, EconomicUnits.FromWhole(1)).Development).Outcome);
        Assert.Same(before, world.Snapshot());
        Assert.Equal(OpportunityStatus.Available, Entry(world, royal.Id).Status);
    }

    [Fact]
    public void AcceptanceReservesFiniteFundingAndRejectsAnUnfundedAdditionalCommitment()
    {
        var world = Create(configuration: Configuration(json => json["opportunities"]!["activeContractLimit"] = 8));
        var initial = world.Snapshot();
        foreach (var royal in initial.Opportunities!.Entries.Where(entry => entry.Id.StartsWith("royal_", StringComparison.Ordinal)))
            world.AcceptOpportunity(Owner, royal.Id);
        foreach (var additional in initial.Opportunities.Entries.Where(entry => !entry.IsProject && entry.ExclusiveGroupId == null &&
            !entry.Id.StartsWith("royal_", StringComparison.Ordinal)).OrderBy(entry => entry.FundingRequired))
            world.AcceptOpportunity(Owner, additional.Id);
        var reserved = world.Snapshot();
        Assert.Equal(initial.Economy!.ExternalCash, reserved.Economy!.ExternalCash + reserved.Opportunities!.ReservedCash);
        Assert.Equal(initial.TotalCash, reserved.TotalCash);
        var unfunded = reserved.Opportunities.Entries.First(entry => !entry.IsProject && entry.Status == OpportunityStatus.Available &&
            entry.FundingRequired > reserved.Economy.ExternalCash);
        world.AcceptOpportunity(Owner, unfunded.Id);
        Assert.Same(reserved, world.Snapshot());
        Assert.Equal(OpportunityStatus.Available, Entry(world, unfunded.Id).Status);
        Assert.Equal(0, Entry(world, unfunded.Id).ReservedCash);
        Reload(world);
    }

    [Fact]
    public void DeclinedAndExpiredOffersStayInspectableAndReceiveNoReward()
    {
        var world = Create();
        var offers = world.Snapshot().Opportunities!.Entries.Where(entry => !entry.IsProject && entry.Id != "royal_planks").ToArray();
        var declined = offers[0];
        world.DeclineOpportunity(Owner, declined.Id);
        Assert.Equal(OpportunityStatus.Declined, Entry(world, declined.Id).Status);
        var expiring = offers.First(entry => entry.Id != declined.Id && entry.ExpiresDay <= 30);
        var initialCash = world.Snapshot().TotalCash;
        while (world.Snapshot().Day <= expiring.ExpiresDay) world.AdvanceDay();
        Assert.Equal(OpportunityStatus.Expired, Entry(world, expiring.Id).Status);
        Assert.Equal(0, Entry(world, expiring.Id).PaidCash);
        Assert.Equal(OpportunityStatus.Declined, Entry(world, declined.Id).Status);
        Audit(world.Snapshot(), initialCash);
    }

    [Fact]
    public void RecurringWindowsRejectEarlyOverdeliveryAndMissingOneReturnsOnlyUnspentEscrow()
    {
        var world = Create(configuration: Configuration(json =>
        {
            json["startingInventories"]!["bread"] = new JObject { ["min"] = 30, ["max"] = 30 };
            json["startingInventories"]!["planks"] = new JObject { ["min"] = 30, ["max"] = 30 };
        }));
        var contract = world.Snapshot().Opportunities!.Entries.First(entry => entry.Kind == OpportunityKind.RecurringSupply);
        world.AcceptOpportunity(Owner, contract.Id);
        Buy(world, contract.PortId, contract.GoodId, contract.RequiredQuantity);
        var perWindow = contract.RequiredQuantity / contract.WindowCount;
        var initialCash = world.Snapshot().TotalCash;
        world.DeliverContract(Ship, contract.Id, perWindow);
        var first = world.Snapshot();
        Assert.Equal(perWindow, Entry(world, contract.Id).WindowDelivered);
        Assert.Equal(perWindow * contract.UnitPrice, Entry(world, contract.Id).PaidCash);
        Assert.Equal(ContractDeliveryOutcome.ExcessQuantity,
            first.PreviewContractDelivery(Ship, contract.Id, EconomicUnits.FromWhole(1)).Outcome);
        world.DeliverContract(Ship, contract.Id, EconomicUnits.FromWhole(1));
        Assert.Same(first, world.Snapshot());
        var firstDeadline = Entry(world, contract.Id).CurrentWindowDeadlineDay;
        while (world.Snapshot().Day <= firstDeadline) world.AdvanceDay();
        Assert.Equal(1, Entry(world, contract.Id).WindowIndex);
        Assert.Equal(0, Entry(world, contract.Id).WindowDelivered);
        var secondDeadline = Entry(world, contract.Id).CurrentWindowDeadlineDay;
        var restored = Reload(world);
        while (world.Snapshot().Day <= secondDeadline)
        {
            world.AdvanceDay(); restored.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        }
        var failed = Entry(world, contract.Id);
        Assert.Equal(OpportunityStatus.Failed, failed.Status);
        Assert.Equal(perWindow * contract.UnitPrice, failed.PaidCash);
        Assert.Equal(0, failed.ReservedCash);
        Assert.Equal(contract.RequiredQuantity - perWindow, world.Snapshot().Ships[Ship].Cargo[contract.GoodId]);
        Assert.False(world.Snapshot().Opportunities!.HasCommercialPrivilege(Owner, contract.PortId, world.Snapshot().Day));
        Audit(world.Snapshot(), initialCash);
    }

    [Fact]
    public void IndustrialAgreementRequiresPlayerInputsAndActualPaidCanonicalBatches()
    {
        var world = Create();
        var contract = world.Snapshot().Opportunities!.Entries.First(entry => entry.Kind == OpportunityKind.IndustrialSupply);
        var recipe = Content().ForExpandingEconomy().GetRecipe(world.Snapshot().Ports[contract.PortId].Facilities
            .Single(facility => facility.Configuration.Instance.Id == contract.FacilityId).Configuration.Instance.RecipeId);
        world.AcceptOpportunity(Owner, contract.Id);
        var initialCash = world.Snapshot().TotalCash;
        var unrelatedBatches = 0;
        for (var day = 0; day < 4; day++)
        {
            var result = world.AdvanceDay();
            unrelatedBatches += result.Production.Count(batch => batch.FacilityId == contract.FacilityId && batch.Outcome == ScheduledProductionOutcome.Produced);
            Assert.Equal(0, Entry(world, contract.Id).EvidenceCount);
        }
        Assert.True(unrelatedBatches > 0, "Pre-existing public input stocks must not grant player fulfillment credit.");
        var source = world.Snapshot().Ports.Values.First(port => port.Id != contract.PortId && port.Inventory[contract.GoodId] >= contract.RequiredQuantity);
        Buy(world, source.Id, contract.GoodId, contract.RequiredQuantity);
        Sail(world, contract.PortId);
        world.DeliverContract(Ship, contract.Id, contract.RequiredQuantity);
        Assert.Equal(OpportunityStatus.Active, Entry(world, contract.Id).Status);
        Assert.Equal(0, Entry(world, contract.Id).EvidenceCount);
        var observed = 0;
        var restored = Reload(world);
        while (Entry(world, contract.Id).Status == OpportunityStatus.Active)
        {
            Assert.True(world.Snapshot().Day <= Entry(world, contract.Id).DeadlineDay);
            var result = world.AdvanceDay(); restored.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
            var batch = result.Production.SingleOrDefault(production => production.FacilityId == contract.FacilityId && production.Outcome == ScheduledProductionOutcome.Produced);
            if (batch != null)
            {
                observed++;
                Assert.Contains(batch.Changes, change => change.GoodId == contract.GoodId && change.Delta == -recipe.Inputs.Single().Quantity * EconomicUnits.QuantityScale);
                Assert.True(result.After.Ports[contract.PortId].City!.TotalWagesReceived > result.Before.Ports[contract.PortId].City!.TotalWagesReceived);
            }
            Audit(result.After, initialCash);
        }
        Assert.Equal(OpportunityStatus.Succeeded, Entry(world, contract.Id).Status);
        Assert.Equal(contract.EvidenceRequired, observed);
        Assert.Equal(contract.EvidenceRequired, Entry(world, contract.Id).EvidenceCount);
        Assert.Equal(contract.FundingRequired, Entry(world, contract.Id).PaidCash);
    }

    [Fact]
    public void IndustrialEvidenceOnInclusiveDeadlineSucceedsButOneDayLateFailsWithoutBonus()
    {
        var world = Create();
        var contract = world.Snapshot().Opportunities!.Entries.First(entry => entry.Kind == OpportunityKind.IndustrialSupply);
        world.SetProductionEnabled(contract.PortId, contract.FacilityId!, false);
        world.AcceptOpportunity(Owner, contract.Id);
        var source = world.Snapshot().Ports.Values.First(port => port.Id != contract.PortId && port.Inventory[contract.GoodId] >= contract.RequiredQuantity);
        Buy(world, source.Id, contract.GoodId, contract.RequiredQuantity);
        Sail(world, contract.PortId);
        world.DeliverContract(Ship, contract.Id, contract.RequiredQuantity);
        world.SetProductionEnabled(contract.PortId, contract.FacilityId!, true);
        while (Entry(world, contract.Id).EvidenceCount < contract.EvidenceRequired - 1)
        {
            Assert.Equal(OpportunityStatus.Active, Entry(world, contract.Id).Status);
            world.AdvanceDay();
        }
        world.SetProductionEnabled(contract.PortId, contract.FacilityId!, false);
        var deadline = Entry(world, contract.Id).DeadlineDay;
        while (world.Snapshot().Day < deadline - 1) world.AdvanceDay();
        var late = Reload(world);
        world.SetProductionEnabled(contract.PortId, contract.FacilityId!, true);
        var exact = world.AdvanceDay();
        Assert.Equal(deadline, exact.After.Day);
        Assert.Contains(exact.Production, batch => batch.FacilityId == contract.FacilityId && batch.Outcome == ScheduledProductionOutcome.Produced);
        Assert.Equal(OpportunityStatus.Succeeded, Entry(world, contract.Id).Status);
        Assert.Equal(contract.FundingRequired, Entry(world, contract.Id).PaidCash);
        late.AdvanceDay();
        Assert.Equal(OpportunityStatus.Active, Entry(late, contract.Id).Status);
        late.SetProductionEnabled(contract.PortId, contract.FacilityId!, true);
        var overdue = late.AdvanceDay();
        Assert.Equal(deadline + 1, overdue.After.Day);
        Assert.Contains(overdue.Production, batch => batch.FacilityId == contract.FacilityId && batch.Outcome == ScheduledProductionOutcome.Produced);
        Assert.Equal(OpportunityStatus.Failed, Entry(late, contract.Id).Status);
        Assert.Equal(contract.EvidenceRequired - 1, Entry(late, contract.Id).EvidenceCount);
        Assert.Equal((long)contract.RequiredQuantity * contract.UnitPrice, Entry(late, contract.Id).PaidCash);
        Assert.Equal(0, Entry(late, contract.Id).ReservedCash);
        Reload(late);
    }

    [Fact]
    public void EmergencyReliefRequiresDedicatedPlayerContributionAndSubsequentPaidHouseholdRecovery()
    {
        var world = Create(configuration: Configuration(json =>
        {
            json["startingInventories"]!["bread"] = new JObject { ["min"] = 0, ["max"] = 0 };
            json["startingInventories"]!["flour"] = new JObject { ["min"] = 50, ["max"] = 50 };
            json["startingInventories"]!["logs"] = new JObject { ["min"] = 50, ["max"] = 50 };
            json["population"] = new JObject { ["min"] = 20, ["max"] = 20 };
            json["householdCashPerResident"] = new JObject { ["min"] = 100, ["max"] = 100 };
        }));
        var initialCash = world.Snapshot().TotalCash;
        OpportunitySnapshot? relief = null;
        while (world.Snapshot().Day < 96 && relief == null)
        {
            world.AdvanceDay();
            relief = world.Snapshot().Opportunities!.Entries.FirstOrDefault(entry => entry.Kind == OpportunityKind.EmergencyRelief && entry.Status == OpportunityStatus.Available);
        }
        Assert.NotNull(relief);
        var contract = relief!;
        Assert.True(world.Snapshot().Ports[contract.PortId].Consumption.Any(need => need.Configuration.GoodId == contract.GoodId && need.LastUnmetQuantity > 0) ||
            world.Snapshot().Ports[contract.PortId].Inventory[contract.GoodId] < EconomicUnits.FromWhole(1));
        world.AcceptOpportunity(Owner, contract.Id);
        Supply(false);
        var paidWithoutContract = 0;
        for (var day = 0; day < 8; day++)
        {
            var result = world.AdvanceDay();
            paidWithoutContract += result.Consumption.Count(need => need.PortId == contract.PortId && need.GoodId == contract.GoodId && need.Payment > 0);
        }
        Assert.True(paidWithoutContract > 0);
        Assert.Equal(0, Entry(world, contract.Id).DeliveredQuantity);
        Assert.Equal(0, Entry(world, contract.Id).EvidenceCount);
        Assert.Equal(OpportunityStatus.Active, Entry(world, contract.Id).Status);
        Supply(true);
        Assert.Equal(OpportunityStatus.Active, Entry(world, contract.Id).Status);
        Assert.Equal(0, Entry(world, contract.Id).EvidenceCount);
        var paidRecovery = 0;
        var restored = Reload(world);
        while (Entry(world, contract.Id).Status == OpportunityStatus.Active)
        {
            var result = world.AdvanceDay(); restored.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
            paidRecovery += result.Consumption.Count(need => need.PortId == contract.PortId && need.GoodId == contract.GoodId &&
                need.Payment > 0 && (long)need.ConsumedQuantity * 5 >= (long)need.RequestedQuantity * 4);
            Audit(result.After, initialCash);
        }
        Assert.Equal(OpportunityStatus.Succeeded, Entry(world, contract.Id).Status);
        Assert.Equal(contract.EvidenceRequired, Entry(world, contract.Id).EvidenceCount);
        Assert.Equal(contract.EvidenceRequired, paidRecovery);

        void Supply(bool dedicated)
        {
            var origin = world.Snapshot().Ships[Ship].DockedPortId!;
            int Travel(string from, string to) => from == to ? 0 : SeaNavigation.FindPath(world.Snapshot().Routes, from, to, world.Snapshot().Ships[Ship].Configuration.Speed).DurationDays;
            RegionalPortSnapshot? source = null;
            while (source == null)
            {
                source = world.Snapshot().Ports.Values.Where(port => port.Id != contract.PortId && port.Inventory[contract.GoodId] >= contract.RequiredQuantity)
                    .OrderBy(port => Travel(origin, port.Id) + Travel(port.Id, contract.PortId)).FirstOrDefault();
                if (source != null) break;
                Assert.Equal(OpportunityStatus.Active, Entry(world, contract.Id).Status);
                world.AdvanceDay();
            }
            Buy(world, source.Id, contract.GoodId, contract.RequiredQuantity);
            Sail(world, contract.PortId);
            if (dedicated) world.DeliverContract(Ship, contract.Id, contract.RequiredQuantity);
            else Assert.Equal(TradeOutcome.Traded, Assert.Single(world.ExecuteTrade(Ship, contract.PortId,
                new TradeCommand(TradeDirection.Sell, contract.GoodId, contract.RequiredQuantity)).Trades).Trade.Outcome);
        }
    }

    [Fact]
    public void EarnedCommercialPrivilegeHalvesRealSalesFeesPreservesSplittingAndExpires()
    {
        var world = Create(configuration: Configuration(json =>
        {
            json["startingInventories"]!["bread"] = new JObject { ["min"] = 30, ["max"] = 30 };
            json["startingInventories"]!["planks"] = new JObject { ["min"] = 30, ["max"] = 30 };
        }));
        var contract = world.Snapshot().Opportunities!.Entries.First(entry => entry.Kind == OpportunityKind.RecurringSupply &&
            entry.RewardKind == OpportunityRewardKind.CommercialPrivilege);
        world.AcceptOpportunity(Owner, contract.Id);
        Buy(world, contract.PortId, contract.GoodId, contract.RequiredQuantity);
        for (var window = 0; window < contract.WindowCount; window++)
        {
            world.DeliverContract(Ship, contract.Id, contract.RequiredQuantity / contract.WindowCount);
            if (window + 1 == contract.WindowCount) break;
            var boundary = Entry(world, contract.Id).CurrentWindowDeadlineDay;
            while (world.Snapshot().Day <= boundary) world.AdvanceDay();
        }
        Assert.Equal(OpportunityStatus.Succeeded, Entry(world, contract.Id).Status);
        Assert.True(world.Snapshot().Opportunities!.HasCommercialPrivilege(Owner, contract.PortId, world.Snapshot().Day));
        Buy(world, contract.PortId, contract.GoodId, EconomicUnits.FromWhole(3));
        var split = Reload(world);
        var before = world.Snapshot();
        var fullTrade = Assert.Single(world.ExecuteTrade(Ship, contract.PortId,
            new TradeCommand(TradeDirection.Sell, contract.GoodId, EconomicUnits.FromWhole(3))).Trades).Trade;
        var first = Assert.Single(split.ExecuteTrade(Ship, contract.PortId,
            new TradeCommand(TradeDirection.Sell, contract.GoodId, EconomicUnits.FromWhole(1))).Trades).Trade;
        var second = Assert.Single(split.ExecuteTrade(Ship, contract.PortId,
            new TradeCommand(TradeDirection.Sell, contract.GoodId, EconomicUnits.FromWhole(2))).Trades).Trade;
        Assert.Equal(TradeOutcome.Traded, fullTrade.Outcome);
        Assert.Equal(fullTrade.ExecutedAmount / 100, fullTrade.ServiceFee);
        Assert.Equal(fullTrade.ServiceFee, first.ServiceFee + second.ServiceFee);
        Assert.Equal(fullTrade.ExecutedAmount, first.ExecutedAmount + second.ExecutedAmount);
        Assert.Equal(world.Snapshot().Companies[Owner].Cash, split.Snapshot().Companies[Owner].Cash);
        Assert.Equal(world.Snapshot().Ports[contract.PortId].Inventory, split.Snapshot().Ports[contract.PortId].Inventory);
        var key = CommercialCirculationSnapshot.Key(Owner, contract.PortId);
        Assert.Equal(world.Snapshot().Circulation!.Commercial[key].FeesPaid, split.Snapshot().Circulation!.Commercial[key].FeesPaid);
        Audit(world.Snapshot(), before.TotalCash);
        Audit(split.Snapshot(), before.TotalCash);

        Buy(world, contract.PortId, contract.GoodId, EconomicUnits.FromWhole(1));
        var expiry = Entry(world, contract.Id).PrivilegeUntilDay;
        var restored = Reload(world);
        while (world.Snapshot().Day <= expiry)
        {
            world.AdvanceDay(); restored.AdvanceDay();
        }
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        Assert.False(world.Snapshot().Opportunities!.HasCommercialPrivilege(Owner, contract.PortId, world.Snapshot().Day));
        var prior = world.Snapshot().Circulation!.Commercial[key];
        var normal = Assert.Single(world.ExecuteTrade(Ship, contract.PortId,
            new TradeCommand(TradeDirection.Sell, contract.GoodId, EconomicUnits.FromWhole(1))).Trades).Trade;
        var after = world.Snapshot().Circulation!.Commercial[key];
        Assert.Equal(prior.DiscountedGrossSales, after.DiscountedGrossSales);
        Assert.Equal((after.GrossLocalSales * 2 - after.DiscountedGrossSales) / 100 - prior.FeesPaid, normal.ServiceFee);
        Assert.True(normal.ServiceFee >= normal.ExecutedAmount / 100);
    }

    [Fact]
    public void LongIdleRunRegeneratesOffersWithinFundingAndHistoryBoundsWithoutMintingMoney()
    {
        var world = Create();
        var initial = world.Snapshot();
        var originalIds = initial.Opportunities!.Entries.Select(entry => entry.Id).ToArray();
        for (var day = 0; day < 365; day++)
        {
            world.AdvanceDay();
            Audit(world.Snapshot(), initial.TotalCash);
        }
        var final = world.Snapshot();
        Assert.Contains(final.Opportunities!.Entries, entry => !originalIds.Contains(entry.Id));
        Assert.Equal(0, final.Economy!.MintedCurrency);
        Assert.Equal(initial.Economy!.ExternalCash, final.Economy.ExternalCash);
        Assert.Equal(0, final.Opportunities.CumulativePayments);
        Assert.True(final.Opportunities.Entries.Count(entry => !entry.IsProject && !entry.Id.StartsWith("royal_", StringComparison.Ordinal) && entry.IsTerminal)
            <= final.Opportunities.Configuration.HistoryLimit);
        Assert.True(final.Opportunities.Entries.Count(entry => !entry.IsProject && !entry.Id.StartsWith("royal_", StringComparison.Ordinal) && entry.Status == OpportunityStatus.Available)
            <= final.Opportunities.Configuration.MaximumAvailableContracts);
        var restored = Reload(world);
        for (var day = 0; day < 16; day++) { world.AdvanceDay(); restored.AdvanceDay(); }
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
    }

    private static OpportunitySnapshot Entry(RegionalWorldState world, string id) =>
        world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == id);
}
