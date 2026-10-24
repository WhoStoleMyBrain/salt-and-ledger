using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SaltAndLedger.Simulation
{
    public enum CommissionCategory { Navigation, Industry, Commerce, Civic }
    public enum CommissionUnit { Count, Goods, Coins, Percent }
    public enum CommissionSemantic { Cumulative, Current }
    public enum CommissionRule
    {
        VisitedPorts, CompletedShips, SecondShipVisits, NamedVisits, RemoteNetSale,
        OperationalRecipe, DistinctBakeries, PrivateProduction, NewIndustryMill,
        NewIndustryBakery, NewIndustryMillBatches, NewIndustryBakeryBatches,
        NetMarketSales, ExportReceipts, MintCommission, MintIssuances,
        FoodObservations, FoodCoverage, Prosperity, ValueGrowth, Cash, PublicPlanks
    }

    /// <summary>Versioned authored parameters, independent of display prose and live progress.</summary>
    public sealed class CommissionConditionDefinition
    {
        public string Id { get; }
        public CommissionRule Rule { get; }
        public string Label { get; }
        public long Target { get; }
        public CommissionUnit Unit { get; }
        public CommissionSemantic Semantic { get; }
        public string? PortId { get; }
        public string? Role { get; }
        public string? GoodId { get; }
        public string? RecipeId { get; }
        public IReadOnlyList<string> PortIds { get; }
        public string TargetText => CommissionConditionProgress.Format(Target, Unit);
        internal string Canonical => string.Join(":", Id, (int)Rule, Target.ToString(CultureInfo.InvariantCulture),
            (int)Unit, (int)Semantic, PortId ?? "-", Role ?? "-", GoodId ?? "-", RecipeId ?? "-", string.Join(",", PortIds));
        internal CommissionConditionDefinition(string id, CommissionRule rule, string label, long target,
            CommissionUnit unit = CommissionUnit.Count, CommissionSemantic semantic = CommissionSemantic.Current,
            string? portId = null, string? role = null, string? goodId = null, string? recipeId = null,
            IEnumerable<string>? portIds = null)
        {
            Id = id; Rule = rule; Label = label; Target = target; Unit = unit; Semantic = semantic;
            PortId = portId; Role = role; GoodId = goodId; RecipeId = recipeId;
            PortIds = Array.AsReadOnly((portIds ?? Array.Empty<string>()).ToArray());
        }
    }

    public sealed class GrandCommissionGoal
    {
        public string Id { get; }
        public string TemplateId { get; }
        public CommissionCategory Category { get; }
        public string Title { get; }
        public string Explanation { get; }
        public IReadOnlyList<CommissionConditionDefinition> Conditions { get; }
        public string CanonicalParameters => string.Join("|", Id, TemplateId, ((int)Category).ToString(CultureInfo.InvariantCulture),
            string.Join(";", Conditions.Select(c => c.Canonical)));
        internal GrandCommissionGoal(string id, CommissionCategory category, string title, string explanation,
            params CommissionConditionDefinition[] conditions)
        {
            Id = id; TemplateId = id; Category = category; Title = title; Explanation = explanation;
            Conditions = Array.AsReadOnly(conditions.ToArray());
        }
    }

    public sealed class GrandCommissionRound
    {
        public CommissionCategory Category { get; }
        public string Title { get; }
        public IReadOnlyList<GrandCommissionGoal> Catalog { get; }
        public IReadOnlyList<GrandCommissionGoal> Offers { get; }
        internal GrandCommissionRound(CommissionCategory category, string title, GrandCommissionGoal[] catalog, uint state)
        {
            Category = category; Title = title; Catalog = Array.AsReadOnly(catalog);
            var order = Enumerable.Range(0, catalog.Length).ToArray();
            for (var index = order.Length - 1; index > 0; index--)
            {
                state = unchecked(state * 1664525u + 1013904223u);
                var chosen = (int)((ulong)state * (uint)(index + 1) >> 32);
                var old = order[index]; order[index] = order[chosen]; order[chosen] = old;
            }
            Offers = Array.AsReadOnly(order.Take(3).Select(i => catalog[i]).ToArray());
        }
    }

    public sealed class GrandCommissionPlan
    {
        public int Seed { get; }
        public int Version { get; }
        public IReadOnlyList<GrandCommissionGoal> Goals { get; }
        public string Signature { get; }
        internal GrandCommissionPlan(int seed, int version, IEnumerable<GrandCommissionGoal> goals)
        {
            Seed = seed; Version = version; Goals = Array.AsReadOnly(goals.ToArray());
            var canonical = "grand-commission:" + version.ToString(CultureInfo.InvariantCulture) + ":" +
                seed.ToString(CultureInfo.InvariantCulture) + "|" + string.Join("|", Goals.Select(g => g.CanonicalParameters));
            using (var hash = SHA256.Create())
                Signature = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
        public static GrandCommissionPlan Restore(int seed, int version, IEnumerable<string> templateIds, string signature)
        {
            var plan = GrandCommissionDraft.Generate(seed, version).Resolve(templateIds);
            if (!StringComparer.Ordinal.Equals(plan.Signature, signature)) throw new ArgumentException("Commission signature does not match its resolved objectives.", nameof(signature));
            return plan;
        }
        public static GrandCommissionPlan Restore(int seed, int version, IEnumerable<GrandCommissionGoal> goals, string signature)
        {
            if (goals == null) throw new ArgumentNullException(nameof(goals));
            var supplied = goals.ToArray();
            var plan = Restore(seed, version, supplied.Select(g => g.TemplateId), signature);
            if (!plan.Goals.Select(g => g.CanonicalParameters).SequenceEqual(supplied.Select(g => g.CanonicalParameters), StringComparer.Ordinal))
                throw new ArgumentException("Commission parameters do not match the known versioned catalogue.", nameof(goals));
            return plan;
        }
        public void Validate() => Restore(Seed, Version, Goals, Signature);
    }

    /// <summary>Four independent salted streams; choosing an offer never consumes economic randomness.</summary>
    public sealed class GrandCommissionDraft
    {
        public const int CurrentVersion = 1;
        public int Seed { get; }
        public int Version { get; }
        public IReadOnlyList<GrandCommissionRound> Rounds { get; }
        private GrandCommissionDraft(int seed, int version)
        {
            Seed = seed; Version = version;
            var generation = ExpandingEconomyGeneration.Generate(seed);
            var rounds = new List<GrandCommissionRound>();
            var titles = new[] { "Navigation / Fleet", "Production / Industry", "Markets / Commerce", "Civic / Capital Development" };
            foreach (CommissionCategory category in Enum.GetValues(typeof(CommissionCategory)))
            {
                var salt = "salt-and-ledger:grand-commission:" + version.ToString(CultureInfo.InvariantCulture) + ":" + ((int)category).ToString(CultureInfo.InvariantCulture);
                uint state = unchecked((uint)seed ^ 2166136261u);
                foreach (var character in salt) state = unchecked((state ^ character) * 16777619u);
                rounds.Add(new GrandCommissionRound(category, titles[(int)category], Catalog(category, generation), state));
            }
            Rounds = Array.AsReadOnly(rounds.ToArray());
        }
        public static GrandCommissionDraft Generate(int seed, int version = CurrentVersion)
        {
            if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version), "Unsupported mission draft version.");
            return new GrandCommissionDraft(seed, version);
        }
        public GrandCommissionPlan Resolve(IEnumerable<string> templateIds)
        {
            if (templateIds == null) throw new ArgumentNullException(nameof(templateIds));
            var ids = templateIds.Take(5).ToArray();
            if (ids.Length != 4) throw new ArgumentException("Select exactly one objective per round.", nameof(templateIds));
            var goals = new GrandCommissionGoal[4];
            for (var i = 0; i < 4; i++) goals[i] = Rounds[i].Offers.SingleOrDefault(o => o.TemplateId == ids[i]) ??
                throw new ArgumentException("The selected objective was not offered in its round.", nameof(templateIds));
            return new GrandCommissionPlan(Seed, Version, goals);
        }

        private static GrandCommissionGoal[] Catalog(CommissionCategory category, ExpandingEconomyGeneration g)
        {
            var coast = LivingCityGeneration.PortName(g.RecoveryPortId);
            var forest = LivingCityGeneration.PortName(g.FurniturePortId);
            var food = LivingCityGeneration.PortName(g.FoodSourcePortId);
            CommissionConditionDefinition C(string id, CommissionRule rule, string label, long target = 1,
                CommissionSemantic semantic = CommissionSemantic.Current, CommissionUnit unit = CommissionUnit.Count,
                string? port = null, string? role = null, string? good = null, string? recipe = null, string[]? ports = null) =>
                new CommissionConditionDefinition(id, rule, label, target, unit, semantic, port, role, good, recipe, ports);
            GrandCommissionGoal G(string id, string title, string explanation, params CommissionConditionDefinition[] conditions) =>
                new GrandCommissionGoal(id, category, title, explanation, conditions);
            CommissionConditionDefinition Output(string good, long quantity) => C("production", CommissionRule.PrivateProduction,
                "Private " + good + " produced", quantity * EconomicUnits.QuantityScale, CommissionSemantic.Cumulative, CommissionUnit.Goods, good: good);
            CommissionConditionDefinition Facility(string recipe, string name) => C(recipe, CommissionRule.OperationalRecipe, "Operational " + name, recipe: recipe);
            CommissionConditionDefinition[] Food(int target) => new[] {
                C("food_window", CommissionRule.FoodObservations, "Recent paid-food opportunities at " + coast, 6, port: g.RecoveryPortId, role: "recovery"),
                C("food_coverage", CommissionRule.FoodCoverage, "Paid food coverage at " + coast, target, unit: CommissionUnit.Percent, port: g.RecoveryPortId, role: "recovery", good: "bread") };
            switch (category)
            {
                case CommissionCategory.Navigation: return new[] {
                    G("all_harbors", "All Harbors", "Visit all four towns with owned ships. Your initial dock counts.", C("visits", CommissionRule.VisitedPorts, "Distinct towns visited", 4, CommissionSemantic.Cumulative)),
                    G("second_command", "Second Command", "Own two completed ships and sail your second acquired ship to another town.", C("fleet", CommissionRule.CompletedShips, "Completed owned ships", 2), C("second_visits", CommissionRule.SecondShipVisits, "Second ship's distinct towns", 2, CommissionSemantic.Cumulative)),
                    G("regional_survey", "Regional Survey", "Visit the recovery, processing and forest towns with owned ships.", C("role_visits", CommissionRule.NamedVisits,
                        "Visit " + string.Join(", ", new[] { g.RecoveryPortId, g.ProcessingPortId, g.FurniturePortId }.Select(LivingCityGeneration.PortName)), 3,
                        CommissionSemantic.Cumulative, ports: new[] { g.RecoveryPortId, g.ProcessingPortId, g.FurniturePortId })),
                    G("open_routes", "Open the Routes", "Visit three towns and retain positive net sales of a good at a town other than your initial dock. Buybacks reverse credit.", C("visits", CommissionRule.VisitedPorts, "Distinct towns visited", 3, CommissionSemantic.Cumulative), C("remote_sale", CommissionRule.RemoteNetSale, "Positive net sale away from initial town", 1, unit: CommissionUnit.Goods)) };
                case CommissionCategory.Industry: return new[] {
                    G("new_industry", "A New Industry", "Operate a private Mill and Bakery in different towns, each completing its own funded batch.",
                        C("mill", CommissionRule.NewIndustryMill, "Operational Mill in the selected pair", recipe: "mill_flour"),
                        C("mill_batch", CommissionRule.NewIndustryMillBatches, "Funded batches in that Mill", semantic: CommissionSemantic.Cumulative, recipe: "mill_flour"),
                        C("bakery", CommissionRule.NewIndustryBakery, "Operational Bakery in a different town", recipe: "bake_bread"),
                        C("bakery_batch", CommissionRule.NewIndustryBakeryBatches, "Funded batches in that Bakery", semantic: CommissionSemantic.Cumulative, recipe: "bake_bread")),
                    G("timber_chain", "Timber Chain", "Operate a Lumber Camp and Sawmill, and privately produce four Planks.", Facility("harvest_logs", "Lumber Camp"), Facility("saw_planks", "Sawmill"), Output("planks", 4)),
                    G("artisan_guild", "Artisan Guild", "Operate a Sawmill and Furniture Workshop, and privately produce two Furniture.", Facility("saw_planks", "Sawmill"), Facility("make_furniture", "Furniture Workshop"), Output("furniture", 2)),
                    G("breadworks", "Breadworks", "Operate two Bakeries in different towns and privately produce four Bread.", C("bakeries", CommissionRule.DistinctBakeries, "Towns with operational owned Bakeries", 2, recipe: "bake_bread"), Output("bread", 4)) };
                case CommissionCategory.Commerce: return new[] {
                    G("bread_for_coast", "Bread for the Coast", "Supply four net Bread to " + coast + ". Local purchases subtract credit.", C("net_sales", CommissionRule.NetMarketSales, "Net Bread sold to " + coast, 40000, unit: CommissionUnit.Goods, port: g.RecoveryPortId, role: "recovery", good: "bread")),
                    G("planks_for_forest", "Planks for the Forest", "Supply four net Planks to " + forest + ". Local purchases subtract credit.", C("net_sales", CommissionRule.NetMarketSales, "Net Planks sold to " + forest, 40000, unit: CommissionUnit.Goods, port: g.FurniturePortId, role: "furniture", good: "planks")),
                    G("flour_for_bakers", "Flour for the Bakers", "Supply four net Flour to " + food + ". Local purchases subtract credit.", C("net_sales", CommissionRule.NetMarketSales, "Net Flour sold to " + food, 40000, unit: CommissionUnit.Goods, port: g.FoodSourcePortId, role: "food_source", good: "flour")),
                    G("royal_exporter", "Royal Exporter", "Receive 100 coins for real cargo fulfilled against finite external contracts.", C("exports", CommissionRule.ExportReceipts, "Paid finite export receipts", 1000000, CommissionSemantic.Cumulative, CommissionUnit.Coins)) };
                case CommissionCategory.Civic: return new[] {
                    G("royal_institution", "Royal Institution", "Commission the public Royal Mint and complete one physical Silver issuance.", C("commission", CommissionRule.MintCommission, "Public Mint commissioned by your company", semantic: CommissionSemantic.Cumulative, port: g.MintPortId), C("issuance", CommissionRule.MintIssuances, "Physical Silver issuances", semantic: CommissionSemantic.Cumulative, port: g.MintPortId, good: "silver")),
                    G("city_back_in_trade", "A City Back in Trade", "Maintain six recent paid-food opportunities at 80% coverage and prosperity 50 in " + coast + ". This can fall again.", Food(80).Concat(new[] { C("prosperity", CommissionRule.Prosperity, "Prosperity at " + coast, 50, unit: CommissionUnit.Percent, port: g.RecoveryPortId, role: "recovery") }).ToArray()),
                    G("prosperous_enterprise", "Prosperous Enterprise", "Simultaneously grow reference Company Value by 100 coins and retain 100 liquid coins.", C("value", CommissionRule.ValueGrowth, "Company Value growth", 1000000, unit: CommissionUnit.Coins), C("cash", CommissionRule.Cash, "Company cash", 1000000, unit: CommissionUnit.Coins)),
                    G("public_works", "Public Works", "Deliver two treasury-funded Planks and maintain six recent paid-food opportunities at 60% in " + coast + ".", new[] { C("public_planks", CommissionRule.PublicPlanks, "Funded public-works Planks", 20000, CommissionSemantic.Cumulative, CommissionUnit.Goods, port: g.MintPortId, good: "planks") }.Concat(Food(60)).ToArray()) };
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
        }
    }
}
