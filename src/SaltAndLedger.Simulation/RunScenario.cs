using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class StartingCharter
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string Tradeoff { get; }
        public string Endowment { get; }
        public long Cash { get; }
        public int CargoCapacity { get; }
        public string StartingPortId { get; }
        public IReadOnlyDictionary<string, int> Cargo { get; }
        public IReadOnlyList<PrivateFacilityConfiguration> Facilities { get; }
        internal StartingCharter(string id, string name, string description, string tradeoff, string endowment,
            long cash, int cargoCapacity, string portId, IDictionary<string, int>? cargo = null,
            params PrivateFacilityConfiguration[] facilities)
        {
            Id = id; Name = name; Description = description; Tradeoff = tradeoff; Endowment = endowment;
            Cash = cash; CargoCapacity = cargoCapacity; StartingPortId = portId;
            Cargo = new ReadOnlyDictionary<string, int>(new SortedDictionary<string, int>(
                cargo ?? new Dictionary<string, int>(), StringComparer.Ordinal));
            Facilities = Array.AsReadOnly(facilities.ToArray());
        }
    }

    public sealed class RunChallenge
    {
        public ChallengeConfiguration Configuration { get; }
        public string Id => Configuration.Id;
        public string Name => Configuration.Title;
        public string Description { get; }
        public string Objective { get; }
        internal RunChallenge(ChallengeConfiguration configuration, string description, string objective)
        { Configuration = configuration; Description = description; Objective = objective; }
    }

    /// <summary>Authored economic choices with explicit opt-in geography and run difficulty.</summary>
    public static class RunScenario
    {
        public const string MerchantCharter = "merchant_company";
        public const string IndustrialCharter = "industrial_consortium";
        public const string IndependentCharter = "independent_traders";
        public const string StarterBakery = "charter_bakery";
        public const int StartingShipSpeed = 100;

        public static IReadOnlyList<StartingCharter> Charters { get; } = Array.AsReadOnly(
            new[] { MerchantCharter, IndustrialCharter, IndependentCharter }.Select(id =>
                ExpandingEconomyScenario.GetCharter(ExpandingEconomyGeneration.Generate(42, economicRulesVersion: 1), id)).ToArray());

        public static StartingCharter GetCharter(string id) => Charters.SingleOrDefault(c => c.Id == id)
            ?? throw new ArgumentException("Unknown starting charter: " + id, nameof(id));

        public static StartingCharter GetCharter(RunIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            return ExpandingEconomyScenario.GetCharter(ExpandingEconomyGeneration.Generate(identity.Seed!.Value,
                identity.GeneratorVersion, identity.LivingPricingMode, identity.EconomicRulesVersion), identity.CharterId);
        }

        public static IReadOnlyList<RunChallenge> Challenges { get; } = Array.AsReadOnly(new[] { GrandCommission.Definition });
        public static IReadOnlyList<RunChallenge> GetChallenges(RunWorldMode mode)
        {
            if (mode != RunWorldMode.ExpandingEconomy) throw new ArgumentOutOfRangeException(nameof(mode));
            return Challenges;
        }
        public static RunChallenge GetChallenge(string id) => id == GrandCommission.ChallengeId
            ? GrandCommission.Definition : throw new ArgumentException("Unknown challenge: " + id, nameof(id));

        public static RegionalWorldState Create(ContentCatalog content, RunIdentity identity, bool reverseConfiguration = false) =>
            ExpandingEconomyScenario.Create(content, identity, reverseConfiguration);

        public static ArchipelagoGeography PreviewGeography(RunIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            return ExpandingEconomyGeneration.Generate(identity.Seed!.Value, identity.GeneratorVersion,
                identity.LivingPricingMode, identity.EconomicRulesVersion).Geography;
        }
    }

    /// <summary>Bounded result components; identity-aware runs compare only matching difficulty and world.</summary>
    public sealed class RunResults
    {
        public RegionalWorldSnapshot Snapshot { get; }
        public StartingCharter Charter { get; }
        public RunIdentity Identity { get; }
        public ChallengeSnapshot Challenge { get; }
        public CompanyFinancialSnapshot Finance { get; }
        public long Cash { get; }
        public int ObjectiveScore { get; }
        public int VictoryScore { get; }
        public int TimeScore { get; }
        public int SolvencyScore { get; }
        public int TotalScore => ObjectiveScore + VictoryScore + TimeScore + SolvencyScore;
        public string ComparisonGroup => Identity.ComparisonGroup;
        public RunResults(RegionalWorldSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Identity = snapshot.RunIdentity ?? throw new ArgumentException("Results require a named run.", nameof(snapshot));
            Charter = RunScenario.GetCharter(Identity);
            Challenge = snapshot.Challenge ?? throw new ArgumentException("Sandbox runs have no scored objective.");
            if (Identity.ChallengeId != Challenge.Configuration.Id)
                throw new ArgumentException("Result challenge must match the authoritative run.", nameof(snapshot));
            if (Challenge.Status == ChallengeStatus.Active) throw new InvalidOperationException("Results require a completed run.");
            Finance = snapshot.Accounting.Companies[Challenge.Configuration.OwnerId];
            Cash = snapshot.Companies[Challenge.Configuration.OwnerId].Cash;
            ObjectiveScore = GrandCommission.Progress(snapshot).ObjectiveScore;
            VictoryScore = Challenge.Status == ChallengeStatus.Succeeded ? 1000 : 0;
            TimeScore = Challenge.Status == ChallengeStatus.Succeeded
                ? (int)Math.Min(60L, Math.Max(0, Challenge.Configuration.DeadlineDay - snapshot.Day)) * 10 : 0;
            SolvencyScore = (int)Math.Min(100L, Cash / 100000);
        }

    }
}
