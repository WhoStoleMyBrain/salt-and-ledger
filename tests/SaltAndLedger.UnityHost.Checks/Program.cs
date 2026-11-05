using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using SaltAndLedger.UnityHost;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
while (!File.Exists(Path.Combine(root, "SaltAndLedger.sln")))
    root = Directory.GetParent(root)?.FullName ?? throw new ArgumentException("Run from the repository, or supply its path.");
Directory.SetCurrentDirectory(root);
var content = ContentLoader.Load(File.ReadAllText("data/content.json"));
void Check(bool value, string message) { if (!value) throw new Exception(message); }
var parsed = 0;
foreach (var path in Directory.EnumerateFiles("unity/Assets/SaltAndLedger", "*.cs", SearchOption.AllDirectories))
{
    var errors = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).ToArray();
    Check(errors.Length == 0, string.Join("\n", errors.Select(item => item.ToString())));
    parsed++;
}
var temporary = Path.Combine(Path.GetTempPath(), "salt-ledger-host-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    foreach (var townCount in new[] { 8, 10, 12 })
    {
        var data = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        data["townCount"] = new JObject { ["min"] = townCount, ["max"] = townCount };
        data["deadlineDays"] = 730;
        var config = WorldGenerationConfiguration.Parse(data.ToString());
        var path = Path.Combine(temporary, "world-generation.json");
        File.WriteAllText(path, config.ToJson());
        config = LocalWorldGenerationConfiguration.Load(path, WorldGenerationConfiguration.Default);
        var session = new ArchipelagoLabSession(content, config);
        session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, seed: 42, generationConfiguration: config));
        Check(session.Snapshot.Ports.Count == townCount, "Configuration lost town count");
        var boardBefore = session.Snapshot;
        var firstContract = boardBefore.Opportunities.Entries.First(item => item.Id == "royal_planks");
        session.Play();
        session.AcceptOpportunity(firstContract.Id, boardBefore);
        Check(!session.IsPlaying && session.Snapshot.Day == boardBefore.Day, "Accepting a contract must pause and spend no day");
        Check(session.Snapshot.Opportunities.Entries.Single(item => item.Id == firstContract.Id).Status == OpportunityStatus.Active,
            "Host did not accept its real contract");
        Check(session.Snapshot.TotalCash == boardBefore.TotalCash, "Acceptance changed all-account money");
        var acceptedBoard = session.Snapshot;
        var decline = acceptedBoard.Opportunities.Entries.First(item => !item.IsProject && item.Status == OpportunityStatus.Available);
        var staleRejected = false;
        try { session.DeclineOpportunity(decline.Id, boardBefore); }
        catch (InvalidOperationException) { staleRejected = true; }
        Check(staleRejected && ReferenceEquals(acceptedBoard, session.Snapshot), "Stale board terms must preserve the active world");
        session.DeclineOpportunity(decline.Id, acceptedBoard);
        Check(session.Snapshot.Opportunities.Entries.Single(item => item.Id == decline.Id).Status == OpportunityStatus.Declined,
            "Declined opportunities disappeared instead of remaining inspectable");
        Check(session.Snapshot.Day == boardBefore.Day, "Declining advanced economic time");
        var origin = session.Player.DockedPortId;
        var destination = session.Snapshot.Ports.Keys.Where(id => id != origin)
            .OrderByDescending(id => session.Snapshot.GetPath(origin, id, session.Player.Configuration.Speed).Legs.Count).First();
        var route = session.Snapshot.GetPath(origin, destination, session.Player.Configuration.Speed);
        Check(route.Legs.Count > 1 && session.HasRoute(origin, destination), "Expanded host must allow multihop navigation");
        var docked = session.Snapshot;
        session.SailTo(destination);
        var departed = session.Snapshot;
        var departureVisual = VoyageVisualInterval.Between(docked, departed, session.Player.Id, true);
        Check(departureVisual.StartDays == 0 && departureVisual.EndDays == 0, "Departure invented visual travel time");
        session.NextDay();
        var progressed = session.Snapshot;
        var dayVisual = VoyageVisualInterval.Between(departed, progressed, session.Player.Id, true);
        Check(dayVisual.StartDays == 0 && dayVisual.EndDays == 1, "Voyage interpolation did not bind committed days");
        var blueprint = session.Snapshot.IndustryBlueprints.Values.First(item => item.EligiblePortIds.Count > 0);
        session.SelectPort(blueprint.EligiblePortIds[0]);
        session.Construct(blueprint.Id);
        var commandVisual = VoyageVisualInterval.Between(progressed, session.Snapshot, session.Player.Id, true);
        Check(session.Snapshot.Revision > progressed.Revision && session.Snapshot.Day == progressed.Day,
            "Construction check did not produce a real same-day revision");
        Check(commandVisual.StartDays == 1 && commandVisual.EndDays == 1, "Same-day command replayed visual travel");
        var beforeCatchUp = session.Snapshot;
        while (session.Snapshot.Day < route.DurationDays / 2) session.NextDay();
        var catchUpVisual = VoyageVisualInterval.Between(beforeCatchUp, session.Snapshot, session.Player.Id, true);
        Check(catchUpVisual.StartDays == 1 && catchUpVisual.EndDays == session.Snapshot.Day,
            "Multi-day presentation lost committed voyage progress");
        var save = session.SaveText();
        var loaded = new ArchipelagoLabSession(content);
        loaded.LoadSaveText(save);
        Check(loaded.Identity.GenerationConfiguration.Fingerprint == config.Fingerprint, "Save lost custom configuration");
        Check(loaded.SaveText() == save, "Host save/load was not exact");
        while (session.Player.DockedPortId == null)
        {
            var beforeDay = session.Snapshot;
            session.NextDay(); loaded.NextDay();
            var visual = VoyageVisualInterval.Between(beforeDay, session.Snapshot, session.Player.Id, true);
            Check(visual.StartDays == beforeDay.Ships[session.Player.Id].VoyageDurationDays -
                beforeDay.Ships[session.Player.Id].RemainingTravelDays &&
                visual.EndDays == route.DurationDays - session.Player.RemainingTravelDays,
                "Voyage or arrival animation exceeded committed progress");
            Check(session.SaveText() == loaded.SaveText(), "Host voyage continuation diverged");
        }
        Check(session.Player.DockedPortId == destination, "Host did not arrive at selected town");
        var beforeInvalid = session.SaveText();
        File.WriteAllText(path, "{ broken }");
        try { LocalWorldGenerationConfiguration.Load(path, WorldGenerationConfiguration.Default); throw new Exception("Invalid config accepted"); }
        catch (WorldGenerationException) { }
        Check(session.SaveText() == beforeInvalid, "Configuration loading mutated active session");
        session.Reset();
        Check(session.Snapshot.Ports.Count == townCount && session.Snapshot.Day == 0, "Retry lost custom configuration");
        Console.WriteLine($"Host {townCount} towns: board acceptance/decline/stale terms; {route.Legs.Count} physical lanes, {route.DurationDays} days; exact save/continuation/retry passed");
    }
}
finally { Directory.Delete(temporary, true); }
Console.WriteLine($"Parsed {parsed} Unity C# files without syntax errors; compiled and exercised actual portable host classes.");
