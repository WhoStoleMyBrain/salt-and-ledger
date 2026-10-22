using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed class ExpandingObjectiveProgress
    {
        public IReadOnlyDictionary<string, int> Components { get; }
        public int Percent => Components.Values.Min();
        public bool Qualified => Percent == 100;
        public string Reason => string.Join("; ", Components.Where(c => c.Value < 100).Select(c => c.Key + ": " + c.Value + "%"));
        internal ExpandingObjectiveProgress(IDictionary<string, int> components)
        { Components = AccountingCollections.Dictionary(components); }
    }

    /// <summary>V3 objectives use committed production, paid welfare and public flows, never stock transfers as income.</summary>
    public static class ExpandingChallenges
    {
        public static IReadOnlyList<RunChallenge> Definitions { get; } = Array.AsReadOnly(new[]
        {
            new RunChallenge(Config(RunScenario.SustainableChallenge, EnterpriseScenario.East),
                "Sustain paid food with productive employment. Public and NPC contributions count toward regional welfare.",
                "By day 120: six paid-food opportunities at 80% in the target, retain its population and 50 prosperity; privately produce 8 Bread and pay 60 coins in wages. Keep 100 cash."),
            new RunChallenge(Config(RunScenario.NetworkChallenge, EnterpriseScenario.East),
                "Operate complementary industry and sell its output while building a solvent company.",
                "By day 180: own operational Mill + Bakery or Sawmill + Furniture Workshop; produce 6 Flour + 8 Bread or 6 Planks + 3 Furniture, sell 4 Bread or 2 Furniture, grow Company Value by 100 coins and keep 100 cash."),
            new RunChallenge(Config(RunScenario.RoyalChallenge, EnterpriseScenario.East),
                "Commission a public monetary institution and connect physical exports to regional paid-food benefit.",
                "By day 180: commission the Mint, deliver Silver for 3 issuances, earn 100 coins from finite external exports and deliver 2 Planks for public works. Target paid food must reach 60% across six needs; keep 100 cash.")
        });

        internal static bool IsExpanding(ChallengeKind kind) => kind == ChallengeKind.SustainableProvisioning ||
            kind == ChallengeKind.IndustrialNetwork || kind == ChallengeKind.RoyalCommission;

        public static ChallengeConfiguration? Create(RunIdentity identity, string targetPortId) =>
            identity.ChallengeId == null ? null : Config(identity.ChallengeId, targetPortId);

        private static ChallengeConfiguration Config(string id, string port)
        {
            switch (id)
            {
                case RunScenario.SustainableChallenge: return new ChallengeConfiguration(id, "Sustainable Provisioning",
                    EnterpriseScenario.PlayerOwnerId, port, "bread", 100, 120, 1000000, ChallengeKind.SustainableProvisioning);
                case RunScenario.NetworkChallenge: return new ChallengeConfiguration(id, "Industrial Network",
                    EnterpriseScenario.PlayerOwnerId, port, "bread", 100, 180, 1000000, ChallengeKind.IndustrialNetwork);
                case RunScenario.RoyalChallenge: return new ChallengeConfiguration(id, "Royal Commission",
                    EnterpriseScenario.PlayerOwnerId, port, "bread", 100, 180, 1000000, ChallengeKind.RoyalCommission);
                default: throw new ArgumentException("Unknown v3 objective: " + id, nameof(id));
            }
        }

        public static ExpandingObjectiveProgress Progress(RegionalWorldSnapshot world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var goal = world.Challenge?.Configuration ?? throw new InvalidOperationException("This run has no objective.");
            var economy = world.Economy ?? throw new InvalidOperationException("V3 objectives need a v3 world.");
            var finance = world.Accounting.Companies[goal.OwnerId];
            var components = new Dictionary<string, int>(StringComparer.Ordinal)
            { ["Company cash (100 coins)"] = Fraction(world.Companies[goal.OwnerId].Cash, goal.MinimumCash) };
            var city = world.Ports[goal.PortId].City!;
            if (goal.Kind == ChallengeKind.SustainableProvisioning || goal.Kind == ChallengeKind.RoyalCommission)
            {
                var desired = city.FoodWindow.Sum(n => (long)Math.Max(n.DesiredQuantity, n.BaselineDesiredQuantity));
                var consumed = city.FoodWindow.Sum(n => (long)n.ConsumedQuantity);
                var target = goal.Kind == ChallengeKind.SustainableProvisioning ? 80 : 60;
                components["Six paid food opportunities"] = Fraction(city.FoodWindow.Count, 6);
                components["Paid food coverage (" + target + "%)"] = desired == 0 ? 0 : Fraction((BigInteger)consumed * 100, (BigInteger)desired * target);
            }
            switch (goal.Kind)
            {
                case ChallengeKind.SustainableProvisioning:
                    components["Retained initial population"] = Fraction(city.Population, city.InitialPopulation);
                    components["Prosperity (50)"] = Fraction(city.Prosperity, 50);
                    components["Private Bread output (8)"] = Fraction(Get(finance.ProducedQuantities, "bread"), 80000);
                    components["Actual private wages (60 coins)"] = Fraction(finance.WagesPaid, 600000);
                    break;
                case ChallengeKind.IndustrialNetwork:
                    var owned = world.PrivateFacilities.Values.Where(f => f.OwnerId == goal.OwnerId && f.IsOperational).ToArray();
                    bool Has(string recipe) => owned.Any(f => f.Blueprint.RecipeId == recipe);
                    var food = Math.Min(Math.Min(Fraction(Get(finance.ProducedQuantities, "flour"), 60000),
                        Fraction(Get(finance.ProducedQuantities, "bread"), 80000)), Fraction(Get(finance.SoldQuantities, "bread"), 40000));
                    var wood = Math.Min(Math.Min(Fraction(Get(finance.ProducedQuantities, "planks"), 60000),
                        Fraction(Get(finance.ProducedQuantities, "furniture"), 30000)), Fraction(Get(finance.SoldQuantities, "furniture"), 20000));
                    components["Complementary output and sales"] = Math.Max(
                        Has("mill_flour") && Has("bake_bread") ? food : 0,
                        Has("saw_planks") && Has("make_furniture") ? wood : 0);
                    components["Company Value growth (100 coins)"] = Fraction(world.CompanyValue(goal.OwnerId).Change, 1000000);
                    break;
                case ChallengeKind.RoyalCommission:
                    components["Public Mint commissioned by company"] = economy.MintCommissionerId == goal.OwnerId ? 100 : 0;
                    components["Physical Silver issuances (3)"] = Fraction(economy.MintBatches, 3);
                    components["Finite external exports (100 coins)"] = Fraction(economy.ExternalNetInflow, 1000000);
                    components["Delivered public works Planks (2)"] = Fraction(economy.TreasuryPlanks, 20000);
                    break;
                default: throw new InvalidOperationException("This is not a v3 objective.");
            }
            return new ExpandingObjectiveProgress(components);
        }
        private static long Get(IReadOnlyDictionary<string, long> values, string id) => values.TryGetValue(id, out var value) ? value : 0;
        private static int Fraction(BigInteger value, BigInteger target) => target <= 0 ? 100 :
            (int)BigInteger.Min(100, BigInteger.Max(0, value) * 100 / target);
    }
}
