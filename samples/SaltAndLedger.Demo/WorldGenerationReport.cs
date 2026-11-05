using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;

/// <summary>Headless generation evidence from real worlds and ordinary daily simulation.</summary>
internal static class WorldGenerationReport
{
    internal static int Run(ContentCatalog content, string[] options)
    {
        var configuration = WorldGenerationConfiguration.Default;
        int? selectedSeed = null;
        var days = 30;
        var check = false;
        string? output = null;
        for (var index = 0; index < options.Length; index++)
        {
            string Value()
            {
                if (++index == options.Length) throw new ArgumentException("Missing value after " + options[index - 1]);
                return options[index];
            }
            switch (options[index])
            {
                case "--config": configuration = WorldGenerationConfiguration.Parse(File.ReadAllText(Value())); break;
                case "--seed":
                    if (!SeededArchipelago.TryParseSeed(Value(), out var seed)) throw new ArgumentException("--seed requires an ASCII int32.");
                    selectedSeed = seed; break;
                case "--days":
                    if (!int.TryParse(Value(), NumberStyles.None, CultureInfo.InvariantCulture, out days) || days < 0 || days > 10000)
                        throw new ArgumentException("--days must be between 0 and 10000.");
                    break;
                case "--check": check = true; break;
                default:
                    if (options[index].StartsWith("--", StringComparison.Ordinal) || output != null)
                        throw new ArgumentException("Unknown or repeated world-generation option: " + options[index]);
                    output = options[index]; break;
            }
        }
        var rows = new List<object>();
        foreach (var seed in selectedSeed.HasValue ? new[] { selectedSeed.Value } : new[] { -42, 42, 99, int.MinValue, int.MaxValue })
        {
            var generation = ExpandingEconomyGeneration.Generate(seed, economicRulesVersion: 1,
                configuration: configuration, content: content);
            var identity = new RunIdentity(null, RunScenario.MerchantCharter, seed: seed,
                generationConfiguration: configuration);
            var world = RunScenario.Create(content, identity);
            var initial = world.Snapshot();
            var initialSave = WorldSaveCodec.Save(world);
            if (check)
            {
                Require(initialSave == WorldSaveCodec.Save(RunScenario.Create(content, identity, true)),
                    "Reversed configuration changed initial state for seed " + seed);
                Require(initialSave == WorldSaveCodec.Save(WorldSaveCodec.Load(content, initialSave)),
                    "Initial save did not round-trip for seed " + seed);
            }
            for (var day = 0; day < days; day++) world.AdvanceDay();
            var final = world.Snapshot();
            Require(final.TotalCash == initial.TotalCash + final.Economy!.MintedCurrency,
                "All-account monetary conservation failed.");
            var saved = WorldSaveCodec.Save(world);
            if (check)
            {
                var restored = WorldSaveCodec.Load(content, saved);
                Require(saved == WorldSaveCodec.Save(restored), "Checkpoint save did not round-trip.");
                for (var next = 0; next < 7; next++) { world.AdvanceDay(); restored.AdvanceDay(); }
                Require(WorldSaveCodec.Save(world) == WorldSaveCodec.Save(restored), "Seven-day deterministic continuation diverged.");
            }
            rows.Add(new
            {
                Seed = seed, generation.GeneratorVersion, ConfigurationFingerprint = configuration.Fingerprint,
                generation.Quality,
                Towns = generation.Ports.Select(port => new
                {
                    port.Id, port.Name, port.X, port.Y, port.Role,
                    Population = port.Settlement!.InitialPopulation, Prosperity = port.Settlement.InitialProsperity,
                    MarketCash = port.Market.InitialMarketCash, HouseholdCash = port.Settlement.InitialHouseholdCash,
                    PublicRecipes = port.Facilities.Select(f => f.Instance.RecipeId),
                    PrivateBlueprints = generation.Blueprints.Where(b => b.EligiblePortIds.Contains(port.Id)).Select(b => b.Id)
                }),
                generation.Geography.Routes,
                Institutions = new { generation.StartPortId, generation.MintPortId, generation.SilverPortId, generation.ShipyardPortId },
                NpcMerchants = initial.Ships.Values.Count(ship => !ship.Configuration.IsPlayer),
                InitialAllAccountCash = initial.TotalCash.ToString(CultureInfo.InvariantCulture),
                Checkpoint = new
                {
                    final.Day, final.Revision, AllAccountCash = final.TotalCash.ToString(CultureInfo.InvariantCulture),
                    Population = final.Ports.Values.Sum(port => port.City!.Population),
                    PlayerCash = final.Companies[WorldIds.PlayerOwnerId].Cash,
                    StateChecksum = Hash(saved)
                },
                DeterministicContinuationChecked = check
            });
        }
        var json = JsonConvert.SerializeObject(new
        {
            ReportVersion = 1, GeneratorVersion = ExpandingEconomyGeneration.CurrentVersion,
            Configuration = JsonConvert.DeserializeObject(configuration.ToJson()), SimulatedDays = days,
            ConstraintFailures = Array.Empty<string>(), Worlds = rows
        }, Formatting.Indented) + "\n";
        if (output == null) Console.Write(json);
        else
        {
            File.WriteAllText(output, json, new UTF8Encoding(false));
            Console.WriteLine("Verified " + rows.Count + " generated worlds; report: " + output);
        }
        return 0;
    }

    private static string Hash(string value)
    {
        using var hash = SHA256.Create();
        return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
