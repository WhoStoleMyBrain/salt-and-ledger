using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed class CommissionConditionProgress
    {
        public string Id { get; }
        public string Label { get; }
        public long Current { get; }
        public long Target { get; }
        public CommissionUnit Unit { get; }
        public CommissionSemantic Semantic { get; }
        public int Percent => (int)BigInteger.Min(100, BigInteger.Max(0, Current) * 100 / Target);
        public bool IsMet => Current >= Target;
        public bool IsPermanent => Semantic == CommissionSemantic.Cumulative && IsMet;
        public string Reason { get; }
        public string CurrentText => Format(Current, Unit);
        public string TargetText => Format(Target, Unit);
        internal CommissionConditionProgress(CommissionConditionDefinition definition, long current, string reason, string? label = null)
        {
            Id = definition.Id; Label = label ?? definition.Label; Current = current; Target = definition.Target;
            Unit = definition.Unit; Semantic = definition.Semantic; Reason = reason;
        }
        public static string Format(long value, CommissionUnit unit)
        {
            if (unit == CommissionUnit.Goods || unit == CommissionUnit.Coins)
                return (value / 10000m).ToString("0.####", CultureInfo.InvariantCulture) + (unit == CommissionUnit.Coins ? " coins" : "");
            return value.ToString(CultureInfo.InvariantCulture) + (unit == CommissionUnit.Percent ? "%" : "");
        }
    }

    public sealed class CommissionGoalProgress
    {
        public string Id { get; }
        public string TemplateId => Id;
        public string Title { get; }
        public string Explanation { get; }
        public IReadOnlyList<CommissionConditionProgress> Conditions { get; }
        public int Percent => Conditions.Min(c => c.Percent);
        public bool IsMet => Conditions.All(c => c.IsMet);
        public CommissionSemantic Semantic => Conditions.All(c => c.Semantic == CommissionSemantic.Cumulative)
            ? CommissionSemantic.Cumulative : CommissionSemantic.Current;
        public bool IsPermanent => IsMet && Semantic == CommissionSemantic.Cumulative;
        public string Reason => IsMet ? (IsPermanent ? "Permanently completed." : "Currently met; current conditions can change.") :
            string.Join(" ", Conditions.Where(c => !c.IsMet).Select(c => c.Reason).Distinct());
        internal CommissionGoalProgress(GrandCommissionGoal goal, IEnumerable<CommissionConditionProgress> conditions)
        { Id = goal.Id; Title = goal.Title; Explanation = goal.Explanation; Conditions = Array.AsReadOnly(conditions.ToArray()); }
    }

    public sealed class CommissionProgress
    {
        public IReadOnlyList<CommissionGoalProgress> Goals { get; }
        public bool Qualified => Goals.All(g => g.IsMet);
        public int Percent => Goals.Sum(g => g.Percent) / 4;
        public int ObjectiveScore => 500 * Goals.Sum(g => g.Percent) / 400;
        public int CurrentlyMetCount => Goals.Count(g => g.IsMet);
        public int PermanentlyFinishedCount => Goals.Count(g => g.IsPermanent);
        internal CommissionProgress(IEnumerable<CommissionGoalProgress> goals) { Goals = Array.AsReadOnly(goals.ToArray()); }
    }

    /// <summary>Pure mission evaluation over committed economic facts and bounded vessel/market history.</summary>
    public static class GrandCommission
    {
        public const string ChallengeId = "grand_commission";
        public const long DeadlineDay = 180;
        public const long MinimumCash = 1000000;
        public static RunChallenge Definition { get; } = new RunChallenge(Create(EnterpriseScenario.East),
            "Inspect the full seeded map, then choose four mandatory economic objectives in four rounds.",
            "Complete all four drafted goals together by day 180, with at least 100 liquid company coins.");
        public static ChallengeConfiguration Create(string targetPortId) => new ChallengeConfiguration(ChallengeId,
            "The Grand Commission", EnterpriseScenario.PlayerOwnerId, targetPortId, "bread", 100, DeadlineDay,
            MinimumCash, ChallengeKind.GrandCommission);

        public static CommissionProgress Progress(RegionalWorldSnapshot world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var plan = world.RunIdentity?.Commission ?? throw new InvalidOperationException("This run has no drafted commission.");
            if (world.Economy == null || world.CommissionHistory == null) throw new InvalidOperationException("Commission requires its v3 economy and authoritative history.");
            var owner = world.Challenge?.Configuration.OwnerId ?? EnterpriseScenario.PlayerOwnerId;
            var owned = world.PrivateFacilities.Values.Where(f => f.OwnerId == owner).OrderBy(f => f.Id, StringComparer.Ordinal).ToArray();
            // Every v3 Mill is legally in the sole processing town. Pick the most accomplished matching sites,
            // then an actual Bakery elsewhere. These are the same two sites for ownership and funded batches.
            var mill = owned.Where(f => f.Blueprint.RecipeId == "mill_flour").OrderByDescending(FundedBatches)
                .ThenByDescending(f => f.IsOperational).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            var bakery = owned.Where(f => f.Blueprint.RecipeId == "bake_bread" &&
                    f.PortId != (mill?.PortId ?? ExpandingEconomyGeneration.Generate(plan.Seed).ProcessingPortId))
                .OrderByDescending(FundedBatches).ThenByDescending(f => f.IsOperational).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            return new CommissionProgress(plan.Goals.Select(goal => new CommissionGoalProgress(goal,
                goal.Conditions.Select(condition => Evaluate(world, owner, owned, mill, bakery, condition)))));
        }

        private static CommissionConditionProgress Evaluate(RegionalWorldSnapshot world, string owner,
            PrivateFacilitySnapshot[] owned, PrivateFacilitySnapshot? mill, PrivateFacilitySnapshot? bakery,
            CommissionConditionDefinition definition)
        {
            var history = world.CommissionHistory!;
            var economy = world.Economy!;
            var finance = world.Accounting.Companies[owner];
            var visited = new HashSet<string>(history.Ships.SelectMany(s => s.Visits.Keys), StringComparer.Ordinal);
            long current;
            string? label = null;
            var unmet = "Requirement not yet met.";
            switch (definition.Rule)
            {
                case CommissionRule.VisitedPorts:
                    current = visited.Count; unmet = "Sail owned ships to the remaining towns; only actual initial docks and arrivals count."; break;
                case CommissionRule.NamedVisits:
                    current = definition.PortIds.Count(visited.Contains);
                    unmet = "Still to visit: " + string.Join(", ", definition.PortIds.Where(p => !visited.Contains(p)).Select(LivingCityGeneration.PortName)) + "."; break;
                case CommissionRule.CompletedShips:
                    current = world.Ships.Values.Count(s => s.OwnerId == owner && s.IsPlayer); unmet = "Commission another ship and wait for its actual completion."; break;
                case CommissionRule.SecondShipVisits:
                    var second = history.Ships.OrderBy(s => s.AcquisitionOrder).Skip(1).FirstOrDefault();
                    current = second?.Visits.Count ?? 0; unmet = "The second acquired ship must complete a voyage to a different town; its shipyard dock counts only once."; break;
                case CommissionRule.RemoteNetSale:
                    var initial = history.Ships.OrderBy(s => s.AcquisitionOrder).First().Visits.OrderBy(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).First().Key;
                    current = history.Markets.Where(m => m.PortId != initial).Select(m => m.NetQuantity).DefaultIfEmpty(0).Max();
                    unmet = "Retain positive net sales of one good away from the initial town; local buybacks reduce credit."; break;
                case CommissionRule.OperationalRecipe:
                    current = owned.Count(f => f.Blueprint.RecipeId == definition.RecipeId && f.IsOperational);
                    unmet = "Build and complete the required owned facility; pending construction does not count."; break;
                case CommissionRule.DistinctBakeries:
                    current = owned.Where(f => f.Blueprint.RecipeId == "bake_bread" && f.IsOperational).Select(f => f.PortId).Distinct().Count();
                    unmet = "Complete owned Bakeries in two distinct towns; two sites in one town count once."; break;
                case CommissionRule.PrivateProduction:
                    current = Get(finance.ProducedQuantities, definition.GoodId!); unmet = "Complete funded private batches with real materials, payroll and output storage."; break;
                case CommissionRule.NewIndustryMill:
                case CommissionRule.NewIndustryBakery:
                case CommissionRule.NewIndustryMillBatches:
                case CommissionRule.NewIndustryBakeryBatches:
                    var isMill = definition.Rule == CommissionRule.NewIndustryMill || definition.Rule == CommissionRule.NewIndustryMillBatches;
                    var site = isMill ? mill : bakery;
                    var batches = definition.Rule == CommissionRule.NewIndustryMillBatches || definition.Rule == CommissionRule.NewIndustryBakeryBatches;
                    current = site == null ? 0 : batches ? FundedBatches(site) : site.IsOperational ? 1 : 0;
                    label = definition.Label + (site == null ? "" : " — " + site.Id + " at " + LivingCityGeneration.PortName(site.PortId));
                    unmet = site == null ? (isMill ? "Construct an owned Mill at the processing town." : "Construct an owned Bakery outside the Mill's processing town.") :
                        !site.IsOperational ? "The selected " + site.Id + " is still under construction." : "Complete a funded batch in " + site.Id + "; production in another facility cannot substitute.";
                    break;
                case CommissionRule.NetMarketSales:
                    current = history.Markets.Where(m => m.PortId == definition.PortId && m.GoodId == definition.GoodId).Select(m => m.NetQuantity).SingleOrDefault();
                    unmet = current < 0 ? "Local purchases exceed sales: buybacks have reversed net delivery credit." : "Sell additional company-owned goods at this exact town. Purchases there subtract from credit."; break;
                case CommissionRule.ExportReceipts:
                    current = economy.ExternalNetInflow; unmet = "Physically fulfill an open, funded finite external contract at its harbor."; break;
                case CommissionRule.MintCommission:
                    current = economy.MintCommissionerId == owner ? 1 : 0; unmet = "Pay to commission the unique public Royal Mint."; break;
                case CommissionRule.MintIssuances:
                    current = economy.MintCommissionerId == owner ? economy.MintBatches : 0; unmet = "After Mint completion, transport Silver and execute a physical issuance."; break;
                case CommissionRule.FoodObservations:
                    current = world.Ports[definition.PortId!].City!.FoodWindow.Count;
                    unmet = "Await six real scheduled paid-food opportunities; deliveries alone are not consumption."; break;
                case CommissionRule.FoodCoverage:
                    var city = world.Ports[definition.PortId!].City!;
                    current = city.RecoveryFulfillmentPercent;
                    unmet = "Recent paid-food fulfillment is below target; it can fall with stock or household-money shortages."; break;
                case CommissionRule.Prosperity:
                    current = world.Ports[definition.PortId!].City!.Prosperity;
                    unmet = "Current prosperity is below target; sustained paid food and funded employment matter."; break;
                case CommissionRule.ValueGrowth:
                    current = world.CompanyValue(owner).Change; unmet = "Current reference Company Value growth is below target; spending, stock and capital can change it."; break;
                case CommissionRule.Cash:
                    current = world.Companies[owner].Cash; unmet = "Current liquid cash is below target; held goods and public assets are not cash."; break;
                case CommissionRule.PublicPlanks:
                    current = economy.TreasuryPlanks; unmet = "Deliver actual Planks to the public-works buyer while treasury funds are available."; break;
                default: throw new InvalidOperationException("Unknown commission evaluator.");
            }
            return new CommissionConditionProgress(definition, current, current >= definition.Target ?
                (definition.Semantic == CommissionSemantic.Cumulative ? "Completed permanently." : "Currently met; this requirement can become unmet.") : unmet, label);
        }
        private static long FundedBatches(PrivateFacilitySnapshot facility) => facility.Blueprint.WagePerBatch == 0 ?
            0 : Math.Min(facility.BatchesProduced, facility.WagesPaid / facility.Blueprint.WagePerBatch);
        private static long Get(IReadOnlyDictionary<string, long> values, string key) => values.TryGetValue(key, out var value) ? value : 0;
    }
}
