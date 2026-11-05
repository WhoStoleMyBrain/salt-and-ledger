using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class SparseSaveValidationTests
{
    [Theory]
    [InlineData("remote_dock")]
    [InlineData("remote_dock_without_voyage")]
    [InlineData("elapsed_before_start")]
    [InlineData("unreachable_departure")]
    [InlineData("unearned_previous_voyage")]
    public void SandboxCannotForgeTravelBeforeItsPhysicalStartingTimeline(string corruption)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter));
        if (corruption == "remote_dock_without_voyage")
            for (var day = 0; day < 20; day++) world.AdvanceDay();
        var snapshot = world.Snapshot();
        Assert.Null(snapshot.CommissionHistory);
        var player = snapshot.Ships[WorldIds.PlayerShipId];
        var origin = player.DockedPortId!;
        var destination = snapshot.Ports.Keys.First(id => id != origin);
        var duration = snapshot.GetPath(origin, destination, player.Configuration.Speed).DurationDays;
        Assert.True(duration > 1);
        var saved = WorldSaveCodec.Save(world);
        var json = JObject.Parse(saved);
        var ship = json["Ships"]!.Single(value => (string)value["Id"]! == player.Id);
        switch (corruption)
        {
            case "remote_dock":
            case "remote_dock_without_voyage": ship["Dock"] = destination; break;
            case "unearned_previous_voyage": ship["VoyageDays"] = duration; break;
            default:
                ship["Dock"] = null;
                ship["Origin"] = corruption == "unreachable_departure" ? destination : origin;
                ship["Destination"] = corruption == "unreachable_departure" ? origin : destination;
                ship["VoyageDays"] = duration;
                ship["RemainingDays"] = corruption == "unreachable_departure" ? duration : duration - 1;
                break;
        }
        json["StateFingerprint"] = "";
        static JToken Canonical(JToken token) => token switch
        {
            JObject obj => new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new JProperty(p.Name, Canonical(p.Value)))),
            JArray array => new JArray(array.Select(Canonical)),
            _ => token.DeepClone()
        };
        json["StateFingerprint"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Canonical(json).ToString(Formatting.None)))).ToLowerInvariant();
        var error = Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(content, json.ToString(Formatting.None)));
        Assert.True(error.Message.Contains("physic", StringComparison.Ordinal) ||
            error.Message.Contains("docked ship", StringComparison.Ordinal), error.Message);
        Assert.Same(snapshot, world.Snapshot());
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }
}
