using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

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

    /// <summary>Fixed handcrafted choices over the existing economy, with no stacking modifiers.</summary>
    public static class RunScenario
    {
        public const string MerchantCharter = "merchant_company";
        public const string IndustrialCharter = "industrial_consortium";
        public const string IndependentCharter = "independent_traders";
        public const string StarterBakery = "charter_bakery";
        public const string CoastalChallenge = "coastal_supply_charter";
        public const string ReliefChallenge = "regional_relief";
        public const string AmbitionChallenge = "enterprise_ambition";

        public static IReadOnlyList<StartingCharter> Charters { get; } = Array.AsReadOnly(new[]
        {
            new StartingCharter(MerchantCharter, "Merchant Company",
                "Liquid capital and an empty hold for immediate public-market trading.",
                "No factory or free inputs. Construction competes directly with shipment cash.",
                "600 cash; 10-unit ship at Northport; no facility or cargo. Ship resale value is not modeled.",
                600, 10, EnterpriseScenario.North),
            new StartingCharter(IndustrialCharter, "Industrial Consortium",
                "A Westhaven bakery and starter inputs give production an immediate foothold.",
                "Only 300 liquid cash and an 8-unit hold. Procurement and shipping must fund each other.",
                "300 cash; 8-unit ship at Westhaven; granted bakery (300 construction quote), 2 Flour and 2 Logs. Grants have zero paid basis; quotes are not resale values.",
                300, 8, EnterpriseScenario.West, null,
                new PrivateFacilityConfiguration(StarterBakery, EnterpriseScenario.PlayerOwnerId,
                    EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint,
                    new Dictionary<string, int> { ["flour"] = 2, ["logs"] = 2 },
                    provenance: IndustrialCharter)),
            new StartingCharter(IndependentCharter, "Independent Traders",
                "A larger hold and useful raw cargo support a flexible opening.",
                "Less cash than the Merchant Company, no factory, and initial cargo occupies half the hold.",
                "480 cash; 12-unit ship at Northport; granted 4 Grain and 2 Logs. Goods have zero paid basis; ship resale value is not modeled.",
                480, 12, EnterpriseScenario.North, new Dictionary<string, int> { ["grain"] = 4, ["logs"] = 2 })
        });

        public static IReadOnlyList<RunChallenge> Challenges { get; } = Array.AsReadOnly(new[]
        {
            new RunChallenge(new ChallengeConfiguration(CoastalChallenge, "The Coastal Supply Charter",
                EnterpriseScenario.PlayerOwnerId, EnterpriseScenario.East, "bread", 24, 60, 100),
                "Connect food supply to Eastport through trading, private production, or both.",
                "Deliver a net 24 Bread to Eastport by day 60 with at least 100 cash. Local buybacks subtract delivery credit."),
            new RunChallenge(new ChallengeConfiguration(ReliefChallenge, "Regional Relief",
                EnterpriseScenario.PlayerOwnerId, EnterpriseScenario.East, "bread", 24, 60, 100,
                ChallengeKind.RegionalRelief),
                "Help underserved towns meet their scheduled food demand. This measures regional consumption, including public and NPC contributions.",
                "Have 24 Bread actually consumed by scheduled demand across the region by day 60; retain at least 100 cash. Sales alone do not count."),
            new RunChallenge(new ChallengeConfiguration(AmbitionChallenge, "Enterprise Ambition",
                EnterpriseScenario.PlayerOwnerId, EnterpriseScenario.East, "bread", 12, 60, 100,
                ChallengeKind.EnterpriseAmbition, 200),
                "Build a productive business with attributed sales margin. Manufacturing and cash both matter.",
                "Produce 12 Bread privately and earn 200 realized gross margin by day 60; retain at least 100 cash. Margin excludes unmodeled wages, transport costs and depreciation.")
        });

        public static StartingCharter GetCharter(string id) => Charters.SingleOrDefault(c => c.Id == id)
            ?? throw new ArgumentException("Unknown starting charter: " + id, nameof(id));
        public static RunChallenge GetChallenge(string id) => Challenges.SingleOrDefault(c => c.Id == id)
            ?? throw new ArgumentException("Unknown challenge: " + id, nameof(id));

        public static RegionalWorldState Create(ContentCatalog content, string? challengeId, string charterId) =>
            EnterpriseScenario.CreateRun(content, challengeId == null ? null : GetChallenge(challengeId).Configuration,
                GetCharter(charterId));

        public static IndustrialRoutePolicy BakeryShippingPolicy(string facilityId = StarterBakery,
            string sourcePortId = EnterpriseScenario.West, string destinationPortId = EnterpriseScenario.East,
            int quantity = 8, int reserve = 0, long sellFloor = 24) =>
            new IndustrialRoutePolicy(sourcePortId, destinationPortId,
                new IndustrialRouteLeg("bread", quantity, Math.Min(4, quantity),
                    IndustrialEndpointKind.Warehouse, sourceFacilityId: facilityId,
                    minimumSourceStock: checked(quantity + reserve), warehouseReserve: reserve,
                    minimumSellPrice: sellFloor), cashReserve: 100, allowEmptyReturn: true);
    }

    /// <summary>Bounded, reproducible result components; compare within the same challenge and charter.</summary>
    public sealed class RunResults
    {
        public RegionalWorldSnapshot Snapshot { get; }
        public StartingCharter Charter { get; }
        public ChallengeSnapshot Challenge { get; }
        public CompanyFinancialSnapshot Finance { get; }
        public long Cash { get; }
        public int ObjectiveScore { get; }
        public int VictoryScore { get; }
        public int TimeScore { get; }
        public int SolvencyScore { get; }
        public int TotalScore => ObjectiveScore + VictoryScore + TimeScore + SolvencyScore;
        public string ComparisonGroup => Challenge.Configuration.Id + "/" + Charter.Id;
        public RunResults(RegionalWorldSnapshot snapshot, string charterId)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Charter = RunScenario.GetCharter(charterId);
            Challenge = snapshot.Challenge ?? throw new ArgumentException("Sandbox runs have no scored objective.");
            if (Challenge.Status == ChallengeStatus.Active) throw new InvalidOperationException("Results require a completed run.");
            Finance = snapshot.Accounting.Companies[Challenge.Configuration.OwnerId];
            Cash = snapshot.Companies[Challenge.Configuration.OwnerId].Cash;
            ObjectiveScore = Fraction(Challenge.ObjectiveQuantity, Challenge.Configuration.RequiredQuantity, 500);
            if (Challenge.Configuration.Kind == ChallengeKind.EnterpriseAmbition)
                ObjectiveScore = Math.Min(ObjectiveScore, Fraction(Challenge.RealizedGrossMargin,
                    Challenge.Configuration.RequiredMargin, 500));
            VictoryScore = Challenge.Status == ChallengeStatus.Succeeded ? 1000 : 0;
            TimeScore = Challenge.Status == ChallengeStatus.Succeeded
                ? (int)Math.Min(60L, Math.Max(0, Challenge.Configuration.DeadlineDay - snapshot.Day)) * 10 : 0;
            SolvencyScore = (int)Math.Min(100L, Cash / 10);
        }

        private static int Fraction(long value, long target, int maximum) =>
            (int)BigInteger.Min(maximum, (BigInteger)Math.Max(0, value) * maximum / target);
    }
}
