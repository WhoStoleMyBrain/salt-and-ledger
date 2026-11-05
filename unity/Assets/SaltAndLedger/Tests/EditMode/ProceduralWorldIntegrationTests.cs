using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ProceduralWorldIntegrationTests
    {
        [Test]
        public void FileConfigurationIsExplicitValidatedAndDoesNotReplaceAnExistingRun()
        {
            var directory = Path.Combine(Path.GetTempPath(), "salt-ledger-world-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "world-generation.json");
                var bundled = WorldGenerationConfiguration.Default;
                Assert.That(LocalWorldGenerationConfiguration.Load(path, bundled), Is.SameAs(bundled));
                var custom = Configuration(12);
                File.WriteAllText(path, custom.ToJson());
                var loaded = LocalWorldGenerationConfiguration.Load(path, bundled);
                Assert.That(loaded.Fingerprint, Is.EqualTo(custom.Fingerprint));
                var session = Session(loaded);
                var before = session.SaveText();
                File.WriteAllText(path, "{ invalid configuration }");
                Assert.Throws<WorldGenerationException>(() => LocalWorldGenerationConfiguration.Load(path, bundled));
                Assert.That(session.SaveText(), Is.EqualTo(before));
                Assert.That(session.Snapshot.Ports.Count, Is.EqualTo(12));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void ReusingTheMapAfterChangingTownCountReplacesMarkersAndSeaLanes()
        {
            var session = Session(Configuration(12));
            var map = new ArchipelagoMap { ShowAllRoutes = true };
            map.Present(session.Snapshot, null, null, false, 1);
            var oldIds = session.Snapshot.Ports.Keys.ToArray();
            Assert.That(map.PortMarkerCount, Is.EqualTo(12));
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, seed: 99,
                generationConfiguration: Configuration(8)));
            var before = session.SaveText();
            map.Present(session.Snapshot, null, null, false, 1);
            Assert.That(map.PortMarkerCount, Is.EqualTo(8));
            Assert.That(map.ShipMarkerCount, Is.EqualTo(session.Snapshot.Ships.Count));
            Assert.That(map.VisibleRouteCount, Is.EqualTo(session.Snapshot.Routes.Count));
            Assert.That(map.Query<Label>(className: "route-distance").ToList().Count,
                Is.EqualTo(session.Snapshot.Routes.Count));
            foreach (var id in oldIds.Where(id => !session.Snapshot.Ports.ContainsKey(id)))
                Assert.That(map.Q<Button>("port_" + id), Is.Null);
            foreach (var town in session.Snapshot.Ports.Values)
                Assert.That(map.Q<Button>("port_" + town.Id).text, Does.Contain(town.Name));
            Assert.That(session.SaveText(), Is.EqualTo(before));
        }

        [Test]
        public void MultiLaneVoyageMarkersFollowPhysicalLegsWithoutIntermediateDocking()
        {
            var session = Session(Configuration(10));
            var draft = GrandCommissionDraft.Generate(42, configuration: session.Identity.GenerationConfiguration);
            session.StartRun(new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter, seed: 42,
                commission: draft.Resolve(draft.Rounds.Select(round => round.Offers[0].TemplateId))));
            var origin = session.Player.DockedPortId;
            var destination = session.Snapshot.Ports.Keys.Where(id => id != origin)
                .OrderByDescending(id => session.Snapshot.GetPath(origin, id, session.Player.Configuration.Speed).Legs.Count).First();
            var path = session.Snapshot.GetPath(origin, destination, session.Player.Configuration.Speed);
            Assert.That(path.Legs.Count, Is.GreaterThan(1));
            session.SailTo(destination);
            var map = new ArchipelagoMap();
            var elapsed = 0;
            foreach (var leg in path.Legs.Take(path.Legs.Count - 1))
            {
                for (var day = 0; day < leg.DurationDays(session.Player.Configuration.Speed); day++)
                { session.NextDay(); elapsed++; }
                var snapshot = session.Snapshot;
                var saved = session.SaveText();
                map.Present(snapshot, null, session.Player.Id, false, 1);
                Assert.That(session.Player.DockedPortId, Is.Null);
                Assert.That(session.Player.RemainingTravelDays, Is.EqualTo(path.DurationDays - elapsed));
                Assert.That(session.SaveText(), Is.EqualTo(saved));
                Assert.That(snapshot.CommissionHistory.Ships.Single(ship => ship.ShipId == session.Player.Id).Visits.ContainsKey(path.PortIds[1]), Is.False,
                    "Transit is not a player dock visit.");
            }
            Assert.That(map.VisibleRouteCount, Is.GreaterThan(1));
        }

        private static WorldGenerationConfiguration Configuration(int towns)
        {
            var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
            json["townCount"] = new JObject { ["min"] = towns, ["max"] = towns };
            json["startingInventories"]["planks"] = new JObject { ["min"] = 8, ["max"] = 8 };
            return WorldGenerationConfiguration.Parse(json.ToString());
        }

        private static ArchipelagoLabSession Session(WorldGenerationConfiguration configuration)
        {
            var content = ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text);
            var session = new ArchipelagoLabSession(content, configuration);
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, seed: 42,
                generationConfiguration: configuration));
            return session;
        }
    }
}
