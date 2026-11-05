using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;
using Xunit.Abstractions;
using static SaltAndLedger.Simulation.Tests.OpportunityTestWorld;

namespace SaltAndLedger.Simulation.Tests;

public sealed class OpportunityWitnessTests
{
    private readonly ITestOutputHelper output;
    public OpportunityWitnessTests(ITestOutputHelper output) { this.output = output; }

    [Fact]
    public void CompetingCargoTenderFundsPermanentPublicIndustryWithExactContinuationAtEveryStep()
    {
        var world = Create(configuration: Configuration(json =>
        {
            json["startingInventories"]!["bread"] = new JObject { ["min"] = 30, ["max"] = 30 };
            json["startingInventories"]!["planks"] = new JObject { ["min"] = 20, ["max"] = 20 };
        }));
        var initialCash = world.Snapshot().TotalCash;
        var restored = Reload(world);
        var alternatives = world.Snapshot().Opportunities!.Entries.Where(entry => entry.ExclusiveGroupId != null)
            .GroupBy(entry => entry.ExclusiveGroupId).First(group => group.Count() >= 2).ToArray();
        var chosen = alternatives.OrderBy(entry => entry.RewardKind == OpportunityRewardKind.CommercialPrivilege ? 0 : 1)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal).First();
        Assert.Single(alternatives.Select(entry => entry.GoodId).Distinct());
        Assert.True(alternatives.Sum(entry => (long)entry.RequiredQuantity) > world.Snapshot().Ships[Ship].CargoCapacity,
            "The two overlapping commitments must exceed the existing ship's single hold.");
        Apply(current => current.AcceptOpportunity(Owner, chosen.Id));
        Assert.All(alternatives.Where(entry => entry.Id != chosen.Id), entry =>
            Assert.Equal(OpportunityStatus.Withdrawn, Entry(entry.Id).Status));

        var origin = world.Snapshot().Ships[Ship].DockedPortId!;
        var source = world.Snapshot().Ports.Values.Where(port => port.Id != chosen.PortId && port.Inventory[chosen.GoodId] >= chosen.RequiredQuantity)
            .OrderBy(port => Distance(origin, port.Id) + Distance(port.Id, chosen.PortId)).ThenBy(port => port.Id, StringComparer.Ordinal).First();
        SailTogether(source.Id);
        Apply(current => Assert.Equal(TradeOutcome.Traded, Assert.Single(current.ExecuteTrade(Ship, source.Id,
            new TradeCommand(TradeDirection.Buy, chosen.GoodId, chosen.RequiredQuantity)).Trades).Trade.Outcome));
        Assert.Equal(chosen.RequiredQuantity, world.Snapshot().Ships[Ship].Cargo[chosen.GoodId]);
        SailTogether(chosen.PortId);
        for (var cycle = 0; cycle < chosen.WindowCount; cycle++)
        {
            Assert.Equal(cycle, Entry(chosen.Id).WindowIndex);
            Apply(current => current.DeliverContract(Ship, chosen.Id, chosen.RequiredQuantity / chosen.WindowCount));
            Assert.Equal((cycle + 1) * chosen.RequiredQuantity / chosen.WindowCount, Entry(chosen.Id).DeliveredQuantity);
            if (cycle + 1 == chosen.WindowCount) break;
            var boundary = Entry(chosen.Id).CurrentWindowDeadlineDay;
            while (world.Snapshot().Day <= boundary) Apply(current => current.AdvanceDay());
        }
        Assert.Equal(OpportunityStatus.Succeeded, Entry(chosen.Id).Status);
        Assert.Equal(chosen.FundingRequired, Entry(chosen.Id).PaidCash);
        Assert.Equal(0, world.Snapshot().Ships[Ship].CargoUnits);

        var project = world.Snapshot().Opportunities!.Entries.Where(entry => entry.Kind == OpportunityKind.PublicIndustryCommission && entry.Status == OpportunityStatus.Available)
            .OrderBy(entry => Content().ForExpandingEconomy().GetRecipe(world.Snapshot().IndustryBlueprints[entry.BlueprintId!].RecipeId).Inputs.Count)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal).First();
        var blueprint = world.Snapshot().IndustryBlueprints[project.BlueprintId!];
        var recipe = Content().ForExpandingEconomy().GetRecipe(blueprint.RecipeId);
        Assert.Empty(recipe.Inputs);
        var countBefore = world.Snapshot().Ports[project.PortId].Facilities.Count;
        Apply(current => current.AcceptOpportunity(Owner, project.Id));
        SailTogether(project.PortId);
        Assert.True(world.Snapshot().Ports[project.PortId].Inventory[project.GoodId] >= project.RequiredQuantity);
        Apply(current => Assert.Equal(TradeOutcome.Traded, Assert.Single(current.ExecuteTrade(Ship, project.PortId,
            new TradeCommand(TradeDirection.Buy, project.GoodId, project.RequiredQuantity)).Trades).Trade.Outcome));
        var firstQuantity = project.RequiredQuantity / 2;
        var firstCash = project.RequiredCash / 2;
        Apply(current => current.ContributeProject(Owner, project.Id, Ship, firstQuantity, firstCash));
        Assert.Equal(OpportunityStatus.Active, Entry(project.Id).Status);
        Assert.Equal(countBefore, world.Snapshot().Ports[project.PortId].Facilities.Count);
        restored = Reload(world);
        Apply(current => current.ContributeProject(Owner, project.Id, Ship,
            project.RequiredQuantity - firstQuantity, project.RequiredCash - firstCash));
        var completed = world.Snapshot();
        Assert.Equal(OpportunityStatus.Succeeded, Entry(project.Id).Status);
        Assert.Equal(countBefore + 1, completed.Ports[project.PortId].Facilities.Count);
        Assert.Equal(0, completed.Ships[Ship].CargoUnits);
        Assert.Empty(completed.PrivateFacilities);
        Assert.Single(completed.Ports[project.PortId].Facilities, facility => facility.Configuration.Instance.Id == project.FacilityId);
        var good = recipe.Outputs.Single().GoodId;
        var beforeOutput = completed.Ports[project.PortId].Inventory[good];
        var beforeWages = completed.Ports[project.PortId].City!.TotalWagesReceived;
        RegionalResult? production = null;
        for (var day = 0; day < blueprint.IntervalDays; day++)
            Apply(current =>
            {
                var result = current.AdvanceDay();
                if (ReferenceEquals(current, world)) production = result;
            });
        Assert.NotNull(production);
        Assert.Contains(production!.Production, batch => batch.FacilityId == project.FacilityId && batch.Outcome == ScheduledProductionOutcome.Produced);
        Assert.Equal(beforeOutput + recipe.Outputs.Single().Quantity * EconomicUnits.QuantityScale,
            world.Snapshot().Ports[project.PortId].Inventory[good]);
        Assert.True(world.Snapshot().Ports[project.PortId].City!.TotalWagesReceived >= beforeWages + blueprint.WagePerBatch);
        output.WriteLine(JsonConvert.SerializeObject(new
        {
            alternatives = alternatives.Select(entry => new { entry.Id, entry.PortId, entry.GoodId, entry.RequiredQuantity, entry.FundingRequired, entry.RewardKind }),
            chosen = Entry(chosen.Id), source = source.Id,
            publicProject = Entry(project.Id), blueprint.RecipeId,
            publicOutputBefore = beforeOutput, publicOutputAfter = world.Snapshot().Ports[project.PortId].Inventory[good],
            world.Snapshot().Day, moneyConserved = world.Snapshot().TotalCash == initialCash,
            exactContinuation = WorldSaveCodec.Save(world) == WorldSaveCodec.Save(restored)
        }, Formatting.Indented));

        OpportunitySnapshot Entry(string id) => world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == id);
        int Distance(string from, string to) => from == to ? 0 : SeaNavigation.FindPath(world.Snapshot().Routes, from, to, world.Snapshot().Ships[Ship].Configuration.Speed).DurationDays;
        void Apply(Action<RegionalWorldState> command)
        {
            command(world); command(restored);
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
            Audit(world.Snapshot(), initialCash);
        }
        void SailTogether(string port)
        {
            if (world.Snapshot().Ships[Ship].DockedPortId == port) return;
            Apply(current => current.Depart(Ship, port));
            restored = Reload(world);
            while (world.Snapshot().Ships[Ship].DockedPortId == null) Apply(current => current.AdvanceDay());
        }
    }
}
