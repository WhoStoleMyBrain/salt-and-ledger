using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;
using static SaltAndLedger.Simulation.Tests.OpportunityTestWorld;

namespace SaltAndLedger.Simulation.Tests;

public sealed class OpportunitySourceTests
{
    [Fact]
    public void MissingUpstreamAndExhaustedInputsDoNotCreateImpossibleDownstreamOffers()
    {
        var configuration = Configuration(json =>
        {
            json["requireSupplyChains"] = false;
            json["industries"] = new JArray(((JArray)json["industries"]!).Where(industry =>
                (string?)industry["industryId"] != "mill"));
            foreach (var good in new[] { "flour", "bread" })
                json["startingInventories"]![good] = new JObject { ["min"] = 0, ["max"] = 0 };
        });
        var world = Create(configuration: configuration);
        var initialCash = world.Snapshot().TotalCash;
        for (var day = 0; day <= 48; day++)
        {
            var snapshot = world.Snapshot();
            Assert.All(snapshot.Ports.Values, port =>
            {
                Assert.Equal(0, port.Inventory["flour"]);
                Assert.Equal(0, port.Inventory["bread"]);
            });
            Assert.DoesNotContain(snapshot.Opportunities!.Entries, entry =>
                entry.Status == OpportunityStatus.Available && (entry.GoodId == "bread" || entry.GoodId == "flour"));
            var royal = Assert.Single(snapshot.Opportunities.Entries, entry => entry.Id == "royal_flour");
            Assert.Equal(OpportunityStatus.Withdrawn, royal.Status);
            Audit(snapshot, initialCash);
            if (day == 48) break;
            world.AdvanceDay();
        }
        Reload(world);
    }

    [Fact]
    public void MissingTimberInputsCannotPromiseWoodenProjectsOrDownstreamDelivery()
    {
        var configuration = Configuration(json =>
        {
            json["requireSupplyChains"] = false;
            json["industries"] = new JArray(((JArray)json["industries"]!).Where(industry =>
                (string?)industry["industryId"] != "lumber_camp"));
            foreach (var good in new[] { "logs", "planks", "furniture" })
                json["startingInventories"]![good] = new JObject { ["min"] = 0, ["max"] = 0 };
        });
        var world = Create(configuration: configuration);
        Assert.DoesNotContain(world.Snapshot().Opportunities!.Entries, entry => entry.IsProject);
        Assert.DoesNotContain(world.Snapshot().Opportunities!.Entries, entry => entry.Status == OpportunityStatus.Available &&
            (entry.GoodId == "logs" || entry.GoodId == "planks" || entry.GoodId == "furniture"));
        Assert.Contains(world.Snapshot().Opportunities!.Entries, entry => entry.Status == OpportunityStatus.Available &&
            entry.Kind == OpportunityKind.RecurringSupply && entry.GoodId == "bread");
        Reload(world);
    }
}
