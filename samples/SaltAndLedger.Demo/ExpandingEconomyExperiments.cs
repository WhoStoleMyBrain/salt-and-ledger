using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;

/// <summary>Prescribed ordinary-command trials; observations never repair economic state.</summary>
internal static class ExpandingEconomyExperiments
{
    internal const string Farm = "experiment_farm", Mill = "experiment_mill", Bakery = "experiment_bakery", Mine = "experiment_mine", Camp = "experiment_camp";
    private const string Player = EnterpriseScenario.PlayerShipId, Owner = EnterpriseScenario.PlayerOwnerId;
    private static readonly int[] Seeds = { -42, 42, 99 };
    private static readonly string[] Strategies = { "idle", "imports", "balanced", "industry", "overbuilt", "external", "external_control", "monetary", "monetary_control", "network2", "network3", "circulation", "fleet3" };
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static int Q(int value) => EconomicUnits.FromWhole(value);
    private static long M(long value) => EconomicUnits.Coins(value);
    private static bool Active(RegionalWorldSnapshot state) => state.Challenge == null || state.Challenge.Status == ChallengeStatus.Active;
    private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal static int Run(ContentCatalog content, string? path, bool check, bool deep = false, bool reinvestment = false)
    {
        var timer = Stopwatch.StartNew();
        var report = reinvestment ? BuildReinvestmentReport(content, deep) : BuildReport(content, deep);
        var json = Json(report) + "\n";
        path ??= reinvestment ? (deep ? ".cache/expanding-reinvestment-deep.json" : "docs/experiments/expanding-reinvestment-results.json") :
            deep ? ".cache/expanding-economy-deep.json" : "docs/experiments/expanding-economy-results.json";
        if (check) Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
            "Expanding-economy report is stale; regenerate with --expanding-economy" + (reinvestment ? " --reinvestment" : "") + (deep ? " --deep" : "") + ".");
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        var rows = report.Runs.Concat(report.Challenges).ToArray();
        Console.WriteLine(FormattableString.Invariant($"Expanding v3: {report.Seeds.Length} seeds, {report.Runs.Count} comparisons, {report.Challenges.Count} challenges; {rows.Sum(r => r.Days)} primary v3 world-days in {timer.Elapsed.TotalSeconds:F3}s."));
        Console.WriteLine(FormattableString.Invariant($"Every commit audits physical/account/FIFO flows; peak {rows.Max(r => r.MaximumEvents)} events and {rows.Max(r => r.MaximumOpenLots)} open lots. Timing excludes no work and is not a large-world certification."));
        foreach (var row in report.Challenges)
            Console.WriteLine(FormattableString.Invariant($"{row.Challenge} seed{row.Seed} A{row.Ascension} {row.Strategy}: {row.Outcome} D{row.Days}, cash {row.Final.Cash}, value {row.Final.Value}."));
        if (!reinvestment) Measure(content);
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    internal static Report BuildReinvestmentReport(ContentCatalog content, bool deep)
    {
        var report = new Report { Seeds = Seeds, Horizon = deep ? 1095 : 365,
            Methodology = "Controlled follow-up to the completed original normal/deep matrices; one fixed reinvestment policy, three seeds, both pricing hypotheses and ascensions. Finite mining/issuance funds actual Farm/Mill/Bakery and three ships; post-authorization metal returns physically to Mine before ship2 carries Logs/Bread. All towns receive real Bread routes; a second Mill/Bakery pair is conditional on actual three-ship completion and retained1000coin capital. No rules, starting state, thresholds or accounts are repaired. Exact continuation/event checks at days3/17/61/181; every commit audits money/physical/FIFO flows. All v3 values are raw 1/10000 units; runtime printed only." };
        foreach (var seed in Seeds)
        foreach (var pricing in new[] { LivingPricingMode.StockOnly, LivingPricingMode.Coverage })
        foreach (var difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
            report.Runs.Add(RunCase(content, seed, pricing, difficulty, "reinvestment", report.Horizon));
        var reference = report.Runs.Single(r => r.Seed == 42 && r.Pricing == "Coverage" && r.Ascension == 1);
        Require(Json(reference) == Json(RunCase(content, 42, LivingPricingMode.Coverage, RunDifficulty.SlowFoundations,
            "reinvestment", report.Horizon, reverse: true)), "Reversed reinvestment configuration differs.");
        return report;
    }

    internal static Report BuildReport(ContentCatalog content, bool deep)
    {
        var seeds = deep ? Seeds.Concat(new[] { int.MinValue, int.MaxValue, 0, 1, -1 })
            .Concat(Enumerable.Range(1, 7).Select(i => unchecked((int)(0x9E3779B9u * (uint)i)))).Distinct().OrderBy(s => s).ToArray() : Seeds;
        var report = new Report { Seeds = seeds, Horizon = deep ? 1095 : 365 };
        foreach (var seed in seeds)
        foreach (var pricing in new[] { LivingPricingMode.StockOnly, LivingPricingMode.Coverage })
        foreach (var difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
        foreach (var strategy in deep ? new[] { "idle", "balanced", "circulation", "network2", "fleet3", "monetary" } : Strategies)
            report.Runs.Add(RunCase(content, seed, pricing, difficulty, strategy, report.Horizon));
        foreach (var seed in Seeds)
        foreach (var difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
        foreach (var challenge in new[] { RunScenario.SustainableChallenge, RunScenario.NetworkChallenge, RunScenario.RoyalChallenge })
        {
            foreach (var strategy in challenge == RunScenario.RoyalChallenge ? new[] { "idle", "monetary" } : new[] { "idle", "industry", "network2", "circulation" })
                report.Challenges.Add(RunCase(content, seed, LivingPricingMode.StockOnly, difficulty, strategy,
                    180, challenge));
        }
        foreach (var charter in new[] { RunScenario.IndustrialCharter, RunScenario.IndependentCharter })
            report.Runs.Add(RunCase(content, 42, LivingPricingMode.StockOnly, RunDifficulty.Normal,
                "industry", report.Horizon, charter: charter));
        foreach (var seed in Seeds)
            foreach (var merchant in new[] { false, true }) report.V2Controls.Add(V2Control(content, seed, merchant, report.Horizon));
        var reference = report.Runs.Single(r => r.Seed == 42 && r.Pricing == "Coverage" && r.Ascension == 1 && r.Strategy == "network2");
        Require(Json(reference) == Json(RunCase(content, 42, LivingPricingMode.Coverage, RunDifficulty.SlowFoundations,
            "network2", report.Horizon, reverse: true)), "Reversed v3 configuration differs.");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Require(Json(reference) == Json(RunCase(content, 42, LivingPricingMode.Coverage, RunDifficulty.SlowFoundations,
                "network2", report.Horizon)), "V3 replay/culture changed economic results.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return report;
    }

    internal static Row RunCase(ContentCatalog content, int seed, LivingPricingMode pricing, RunDifficulty difficulty,
        string strategy, int horizon, string? challenge = null, string charter = RunScenario.MerchantCharter,
        bool reverse = false)
    {
        var id = new RunIdentity(challenge, charter, RunWorldMode.ExpandingEconomy, seed, difficulty, livingPricingMode: pricing);
        var trial = new Trial(content, RunScenario.Create(content, id, reverse), strategy);
        trial.Initialize();
        while (trial.World.Snapshot().Day < horizon && Active(trial.World.Snapshot()))
        {
            trial.Act();
            if (Active(trial.World.Snapshot())) trial.Apply(w => w.AdvanceDay());
            var day = trial.World.Snapshot().Day;
            if ((strategy == "network2" || strategy == "network3" || strategy == "fleet3" || strategy == "monetary") &&
                (day == 3 || day == 17 || day == 61)) trial.VerifySaveContinuation();
            if (strategy == "reinvestment" && (day == 3 || day == 17 || day == 61 || day == 181)) trial.VerifySaveContinuation();
            if (new long[] { 30, 90, 180, 365, 1095 }.Contains(day)) trial.Row.Checkpoints.Add(Sample(trial.World.Snapshot()));
        }
        trial.Row.Final = Sample(trial.World.Snapshot());
        trial.Row.Days = trial.World.Snapshot().Day;
        trial.Row.Outcome = trial.World.Snapshot().Challenge?.Status.ToString() ?? "Sandbox";
        trial.Row.FinalStateFingerprint = Fingerprint(WorldSaveCodec.Save(trial.World));
        if (!Active(trial.World.Snapshot()))
        {
            var before = trial.World.Snapshot();
            try { trial.World.AdvanceDay(); throw new Exception("Ended v3 run advanced."); }
            catch (InvalidOperationException) { }
            Require(ReferenceEquals(before, trial.World.Snapshot()), "Terminal rejection replaced state.");
        }
        return trial.Row;
    }

    internal sealed class Trial
    {
        internal RegionalWorldState World;
        internal readonly Row Row;
        private readonly ContentCatalog content;
        private readonly ExpandingEconomyGeneration generation;
        private readonly string strategy;
        private readonly BigInteger initialCash, initialRegional;
        private readonly List<string>? eventSignatures;
        private bool Industry => strategy == "industry" || strategy == "overbuilt" || strategy == "network2" || strategy == "network3";
        private bool Network => strategy == "network2" || strategy == "network3";
        private bool Monetary => strategy == "monetary" || strategy == "monetary_control" || strategy == "reinvestment";
        private string BakeryId => World.Snapshot().PrivateFacilities.ContainsKey(RunScenario.StarterBakery) ? RunScenario.StarterBakery : Bakery;
        internal Trial(ContentCatalog content, RegionalWorldState world, string strategy, List<string>? eventSignatures = null)
        {
            this.content = content; World = world; this.strategy = strategy; this.eventSignatures = eventSignatures;
            var state = world.Snapshot(); var id = state.RunIdentity!;
            generation = ExpandingEconomyGeneration.Generate(id.Seed!.Value, pricingMode: id.LivingPricingMode);
            initialCash = state.TotalCash - state.Economy!.MintedCurrency;
            initialRegional = state.RegionalCash - state.Economy.MintedCurrency - state.Economy.ExternalNetInflow;
            Row = new Row { Seed = id.Seed.Value, Ascension = id.Ascension, Pricing = id.LivingPricingMode.ToString(),
                Strategy = strategy, Charter = id.CharterId, Challenge = id.ChallengeId, Identity = id.ComparisonGroup,
                Signature = generation.Signature, Initial = Sample(state) };
        }

        internal void Apply(Func<RegionalWorldState, RegionalResult> command)
        {
            if (!Active(World.Snapshot())) return;
            var result = command(World);
            var after = result.After;
            Require(after.TotalCash == initialCash + after.Economy!.MintedCurrency, "Global issuance did not reconcile.");
            Require(after.RegionalCash == initialRegional + after.Economy.MintedCurrency + after.Economy.ExternalNetInflow,
                "Regional external boundary did not reconcile.");
            if (eventSignatures != null) eventSignatures.Add(EventSignature(result));
            Row.Operations++;
            Row.MaximumEvents = Math.Max(Row.MaximumEvents, result.EconomicEvents.Count + result.CashTransfers.Count +
                result.Production.Count + result.PrivateProduction.Count + result.Consumption.Count + result.Development.Count + result.Movements.Count);
            Row.MaximumOpenLots = Math.Max(Row.MaximumOpenLots, after.Accounting.Stores.Values.Sum(s => s.Goods.Values.Sum(g => g.Lots.Count)));
            foreach (var attempt in result.Production)
            {
                Increment(Row.Attempts, "public/" + attempt.PortId + "/" + attempt.RecipeId + "/" + attempt.Outcome, 1);
                if (attempt.Outcome == ScheduledProductionOutcome.PayrollShortage) First("public_payroll/" + attempt.PortId, after.Day);
                if (attempt.Outcome == ScheduledProductionOutcome.InputShortage) First("public_input/" + attempt.PortId, after.Day);
                Flow(attempt.RecipeId, attempt.Outcome, after.QuantityScale);
            }
            foreach (var attempt in result.PrivateProduction)
            {
                Increment(Row.Attempts, "private/" + attempt.FacilityId + "/" + attempt.RecipeId + "/" + attempt.Outcome, 1);
                if (attempt.Outcome == ScheduledProductionOutcome.PayrollShortage) First("private_payroll/" + attempt.FacilityId, after.Day);
                if (attempt.Outcome == ScheduledProductionOutcome.InputShortage) First("private_input/" + attempt.FacilityId, after.Day);
                Flow(attempt.RecipeId, attempt.Outcome, after.QuantityScale);
            }
            foreach (var need in result.Consumption)
            {
                Increment(Row.Flows, "consumed/" + need.GoodId, need.ConsumedQuantity);
                if (need.Kind == ConsumptionKind.Essential && need.UnmetStock > 0) First("food_stock/" + need.PortId, after.Day);
                if (need.Kind == ConsumptionKind.Essential && need.UnmetAffordability > 0) First("food_funds/" + need.PortId, after.Day);
                if (need.Payment > 0) Row.LastRetailDay[need.PortId] = after.Day;
            }
            foreach (var movement in result.Movements)
                if (movement.Kind == ShipMovementKind.Departure) Increment(Row.Flows, "voyages/" + movement.ShipId, 1);
            foreach (var attempt in result.Development)
            {
                Increment(Row.Attempts, "development/" + attempt.Kind + "/" + attempt.Outcome, 1);
                if ((attempt.Outcome == DevelopmentOutcome.Started || attempt.Outcome == DevelopmentOutcome.Completed) && Row.Development.Count < 100)
                    Row.Development.Add(new { after.Day, after.Revision, Kind = attempt.Kind.ToString(), attempt.AssetId,
                        attempt.GoodId, attempt.Quantity, attempt.Payment, attempt.TreasuryAmount, attempt.HouseholdAmount, attempt.CompletionDay });
            }
            foreach (var decision in result.Decisions)
                if (decision.Action == AutomationAction.Wait || decision.Action == AutomationAction.Pause)
                    Increment(Row.Waits, decision.ShipId + "/" + decision.Reason, 1);
        }

        private void Flow(string recipeId, ScheduledProductionOutcome outcome, int scale)
        {
            if (outcome != ScheduledProductionOutcome.Produced) return;
            var recipe = content.ForExpandingEconomy().GetRecipe(recipeId);
            foreach (var input in recipe.Inputs) Increment(Row.Flows, "transformation_input/" + input.GoodId, checked((long)input.Quantity * scale));
            foreach (var output in recipe.Outputs) Increment(Row.Flows,
                (recipe.Kind == RecipeKind.Extraction ? "extracted/" : "transformation_output/") + output.GoodId, checked((long)output.Quantity * scale));
        }
        private void First(string key, long day) { if (!Row.FirstProblems.ContainsKey(key)) Row.FirstProblems.Add(key, day); }

        internal void Initialize()
        {
            if (strategy == "circulation")
            {
                Build(Farm, generation.FoodSourcePortId, ExpandingEconomyGeneration.FarmBlueprint,
                    new IndustryPolicy(false, true, M(100), outputs: new[] { new OutputSaleRule("grain", 0, 1) }));
                Build(Mill, generation.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint, ExpandingEconomyScenario.MillPolicy());
                Build(Bakery, generation.RecoveryPortId, EnterpriseScenario.BakeryBlueprint,
                    new IndustryPolicy(true, true, M(100), new[] { new InputStockRule("logs", Q(3), 18) },
                        new[] { new OutputSaleRule("bread", 0, 1) }));
                Build(Camp, generation.RecoveryPortId, ExpandingEconomyGeneration.LumberBlueprint,
                    new IndustryPolicy(false, true, M(100), outputs: new[] { new OutputSaleRule("logs", 0, 1) }));
            }
            else if (strategy == "fleet3")
            {
                Commission(); Commission();
                Build(Mill, generation.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint, ExpandingEconomyScenario.MillPolicy());
                Apply(w => w.ConfigureAutomation(Player, ExpandingEconomyScenario.MerchantPolicy(generation, true), true));
            }
            else if (Industry)
            {
                if (strategy != "overbuilt") Build(Farm, generation.FoodSourcePortId, ExpandingEconomyGeneration.FarmBlueprint,
                    new IndustryPolicy(false, !Network, M(80), outputs: new[] { new OutputSaleRule("grain", 0, 1) }));
                Build(Mill, generation.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint, ExpandingEconomyScenario.MillPolicy());
                if (strategy == "overbuilt")
                    for (var extra = 1; extra <= 3; extra++) Build("extra_mill_" + extra, generation.ProcessingPortId,
                        ExpandingEconomyGeneration.MillBlueprint, ExpandingEconomyScenario.MillPolicy());
                if (BakeryId == Bakery) Build(Bakery, generation.ProcessingPortId, EnterpriseScenario.BakeryBlueprint, ExpandingEconomyScenario.BakeryPolicy());
                else Apply(w => w.ConfigureFacility(Owner, BakeryId, ExpandingEconomyScenario.BakeryPolicy()));
                if (Network) Commission();
            }
            else if (Monetary)
            {
                Build(Mine, generation.SilverPortId, ExpandingEconomyGeneration.SilverMineBlueprint, new IndustryPolicy());
                if (strategy != "monetary_control") Apply(w => w.CommissionMint(Owner));
                Commission();
                Apply(w => w.ConfigureAutomation(Player, ExpandingEconomyScenario.MerchantPolicy(generation, true), true));
            }
            else if (strategy == "imports" || strategy == "balanced")
                Apply(w => w.ConfigureAutomation(Player, ExpandingEconomyScenario.MerchantPolicy(generation, strategy == "balanced"), true));
        }

        private void Build(string id, string port, string blueprint, IndustryPolicy policy)
        {
            Apply(w => w.ConstructFacility(Owner, port, blueprint, id));
            if (World.Snapshot().PrivateFacilities.ContainsKey(id)) Apply(w => w.ConfigureFacility(Owner, id, policy));
        }
        private void Commission()
        {
            var state = World.Snapshot();
            if (state.PreviewShipCommission(Owner, generation.ProcessingPortId).Outcome == DevelopmentOutcome.Started)
                Apply(w => w.CommissionShip(Owner, generation.ProcessingPortId));
        }

        internal void Act()
        {
            var state = World.Snapshot();
            if (!Active(state)) return;
            if (strategy == "reinvestment") Reinvest();
            else if (strategy == "circulation")
            {
                Manage(Farm, "grain", 24);
                Manage(Mill, "flour", 12);
                Manage(Bakery, "bread", 24);
                Manage(Camp, "logs", 12);
                PositionIndustrial(Player, generation.ProcessingPortId, new IndustrialRoutePolicy(generation.ProcessingPortId,
                    generation.RecoveryPortId, new IndustrialRouteLeg("flour", Q(4), Q(1),
                        destinationKind: IndustrialEndpointKind.Warehouse, destinationFacilityId: Bakery, maximumBuyPrice: 22),
                    new IndustrialRouteLeg("logs", Q(8), Q(1), maximumBuyPrice: 12, minimumSellPrice: 1),
                    cashReserve: M(100), allowEmptyReturn: true));
            }
            else if (strategy == "fleet3")
            {
                Manage(Mill, "flour", 12);
                if (state.Ships.ContainsKey("player_ship_2")) PositionMerchant("player_ship_2", generation.FoodSourcePortId,
                    new TradeRoutePolicy(generation.FoodSourcePortId, generation.ProcessingPortId,
                        new RouteLeg("bread", Q(4), Q(1), 34, 1), new RouteLeg("flour", Q(4), Q(1), 22, 1), M(100), true));
                if (state.Ships.ContainsKey("player_ship_3")) PositionMerchant("player_ship_3", generation.FoodSourcePortId,
                    new TradeRoutePolicy(generation.FoodSourcePortId, generation.FurniturePortId,
                        new RouteLeg("bread", Q(4), Q(1), 34, 1), new RouteLeg("logs", Q(6), Q(1), 12, 1), M(100), true));
            }
            else if (Industry)
            {
                PositionIndustrial(Player, generation.ProcessingPortId, new IndustrialRoutePolicy(generation.ProcessingPortId,
                    generation.RecoveryPortId, new IndustrialRouteLeg("bread", Q(8), Q(2), IndustrialEndpointKind.Warehouse,
                        sourceFacilityId: BakeryId, minimumSourceStock: Q(2), minimumSellPrice: 1),
                    new IndustrialRouteLeg("logs", Q(4), Q(1), destinationKind: IndustrialEndpointKind.Warehouse,
                        destinationFacilityId: BakeryId, maximumBuyPrice: 18), cashReserve: M(80), allowEmptyReturn: true));
                if (Network)
                {
                    if (World.Snapshot().Ships.ContainsKey("player_ship_2"))
                        PositionIndustrial("player_ship_2", generation.FoodSourcePortId,
                            new IndustrialRoutePolicy(generation.FoodSourcePortId, generation.ProcessingPortId,
                                new IndustrialRouteLeg("grain", Q(10), Q(4), IndustrialEndpointKind.Warehouse,
                                    IndustrialEndpointKind.Warehouse, Farm, Mill), allowEmptyReturn: true));
                    if (strategy == "network3")
                    {
                        var current = World.Snapshot();
                        if (current.Ships.Values.Count(s => s.IsPlayer) + current.Economy!.ShipOrders.Count < 3 && current.Companies[Owner].Cash >= M(800)) Commission();
                        if (World.Snapshot().Ships.ContainsKey("player_ship_3"))
                            PositionMerchant("player_ship_3", generation.ProcessingPortId,
                                new TradeRoutePolicy(generation.ProcessingPortId, generation.FurniturePortId,
                                    new RouteLeg("bread", Q(4), Q(1), 50, 1),
                                    new RouteLeg("logs", Q(6), Q(1), 18, 1), M(100), true));
                    }
                }
            }
            else if (Monetary)
            {
                if (state.Ships.ContainsKey("player_ship_2")) RoyalVoyage("player_ship_2", true, strategy == "monetary");
            }
            else if (strategy == "external" || strategy == "external_control") RoyalVoyage(Player, false, strategy == "external");
        }

        private void Manage(string facilityId, string output, int marketCeiling)
        {
            var state = World.Snapshot();
            if (!state.PrivateFacilities.TryGetValue(facilityId, out var facility)) return;
            var enabled = state.Companies[Owner].Cash >= M(120) && state.Ports[facility.PortId].Inventory[output] < Q(marketCeiling);
            if (facility.Enabled != enabled) Apply(w => w.SetFacilityEnabled(Owner, facilityId, enabled));
        }

        private void Reinvest()
        {
            var state = World.Snapshot();
            if (state.Ships.ContainsKey("player_ship_2"))
            {
                if (state.Economy!.MintedCurrency < M(1920)) RoyalVoyage("player_ship_2", true, true);
                else ReinvestRoyalShip();
            }
            state = World.Snapshot();
            if (state.Day >= 12 && !state.PrivateFacilities.ContainsKey(Bakery) && state.Companies[Owner].Cash >= M(550))
                Build(Bakery, generation.RecoveryPortId, EnterpriseScenario.BakeryBlueprint,
                    new IndustryPolicy(true, true, M(100), new[] { new InputStockRule("logs", Q(3), 18) },
                        new[] { new OutputSaleRule("bread", 0, 1) }));
            state = World.Snapshot();
            if (state.PrivateFacilities.ContainsKey(Bakery) && !state.PrivateFacilities.ContainsKey(Camp) && state.Companies[Owner].Cash >= M(470))
                Build(Camp, generation.RecoveryPortId, ExpandingEconomyGeneration.LumberBlueprint,
                    new IndustryPolicy(false, true, M(100), outputs: new[] { new OutputSaleRule("logs", 0, 1) }));
            state = World.Snapshot();
            if (state.Day >= 24 && !state.PrivateFacilities.ContainsKey(Farm) && state.Companies[Owner].Cash >= M(1250))
            {
                Build(Farm, generation.FoodSourcePortId, ExpandingEconomyGeneration.FarmBlueprint,
                    new IndustryPolicy(false, true, M(100), outputs: new[] { new OutputSaleRule("grain", Q(12), 1) }));
                Build(Mill, generation.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint, new IndustryPolicy());
                Commission();
            }
            state = World.Snapshot();
            if (state.PrivateFacilities.ContainsKey(Farm) && state.Ships.Values.Count(s => s.IsPlayer) + state.Economy!.ShipOrders.Count < 3 &&
                state.Companies[Owner].Cash >= M(750)) Commission();
            if (state.Ships.Values.Count(s => s.IsPlayer) == 3 && !state.PrivateFacilities.ContainsKey("reinvestment_mill") && state.Companies[Owner].Cash >= M(1000))
            {
                Build("reinvestment_mill", generation.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint,
                    new IndustryPolicy(true, true, M(100), new[] { new InputStockRule("grain", Q(4), 24) }, new[] { new OutputSaleRule("flour", 0, 1) }));
                Build("reinvestment_bakery", generation.ProcessingPortId, EnterpriseScenario.BakeryBlueprint,
                    new IndustryPolicy(true, true, M(100), new[] { new InputStockRule("flour", Q(3), 30), new InputStockRule("logs", Q(3), 24) }, new[] { new OutputSaleRule("bread", 0, 1) }));
            }
            Manage(Bakery, "bread", 34);
            Manage("reinvestment_bakery", "bread", 34);
            Manage("reinvestment_mill", "flour", 12);
            Manage(Camp, "logs", 14);
            ManageWarehouse(Farm, "grain", 16);
            ManageWarehouse(Mill, "flour", 10);
            state = World.Snapshot();
            if (state.Ships.ContainsKey("player_ship_3"))
                PositionIndustrial("player_ship_3", generation.FoodSourcePortId,
                    new IndustrialRoutePolicy(generation.FoodSourcePortId, generation.ProcessingPortId,
                        new IndustrialRouteLeg("grain", Q(10), Q(4), IndustrialEndpointKind.Warehouse,
                            IndustrialEndpointKind.Warehouse, Farm, Mill),
                        new IndustrialRouteLeg("bread", Q(4), Q(1), maximumBuyPrice: 50, minimumSellPrice: 1),
                        cashReserve: M(100), allowEmptyReturn: true));
            if (!state.PrivateFacilities.ContainsKey(Bakery)) return;
            var ownFlour = state.PrivateFacilities.TryGetValue(Mill, out var mill) && mill.BatchesProduced > 0;
            var sourceKind = ownFlour ? IndustrialEndpointKind.Warehouse : IndustrialEndpointKind.Market;
            var ship = state.Ships[Player];
            if (ship.DockedPortId == null || (ship.AutomationEnabled && ship.IndustrialPolicy?.Outbound.SourceKind == sourceKind)) return;
            if (ship.AutomationEnabled) Apply(w => w.SetAutomationEnabled(Player, false));
            foreach (var good in World.Snapshot().Ships[Player].Cargo.Where(g => g.Value > 0).Select(g => g.Key).ToArray())
            {
                var current = World.Snapshot(); var currentShip = current.Ships[Player];
                if (good == "flour" && currentShip.DockedPortId == generation.RecoveryPortId)
                {
                    var facility = current.PrivateFacilities[Bakery];
                    var quantity = Math.Min(currentShip.Cargo[good], facility.Blueprint.WarehouseCapacity - facility.Inventory.Values.Sum());
                    quantity -= quantity % EconomicUnits.QuantityScale;
                    if (quantity > 0) Apply(w => w.TransferGoods(Owner, Player, Bakery, TransferDirection.ShipToWarehouse, good, quantity));
                }
                else Sell(Player, good);
            }
            ship = World.Snapshot().Ships[Player];
            if (ship.CargoUnits != 0) return;
            if (ship.DockedPortId != generation.ProcessingPortId && ship.DockedPortId != generation.RecoveryPortId)
            { Apply(w => w.Depart(Player, generation.ProcessingPortId)); return; }
            Apply(w => w.ConfigureIndustrialAutomation(Player, new IndustrialRoutePolicy(generation.ProcessingPortId,
                generation.RecoveryPortId, new IndustrialRouteLeg("flour", Q(6), Q(1), sourceKind,
                    IndustrialEndpointKind.Warehouse, sourceFacilityId: ownFlour ? Mill : null,
                    destinationFacilityId: Bakery, maximumBuyPrice: 22),
                new IndustrialRouteLeg("bread", Q(4), Q(1), maximumBuyPrice: 50, minimumSellPrice: 1),
                cashReserve: M(100), allowEmptyReturn: true), true));
        }

        private void ReinvestRoyalShip()
        {
            const string shipId = "player_ship_2";
            var state = World.Snapshot(); var ship = state.Ships[shipId];
            if (ship.DockedPortId == null || ship.AutomationEnabled) return;
            if (ship.Cargo["silver"] > 0)
            {
                if (ship.DockedPortId != generation.SilverPortId) { Apply(w => w.Depart(shipId, generation.SilverPortId)); return; }
                var held = ship.Cargo["silver"];
                Apply(w => w.TransferGoods(Owner, shipId, Mine, TransferDirection.ShipToWarehouse, "silver", held));
            }
            if (World.Snapshot().PrivateFacilities.ContainsKey(Bakery))
                PositionIndustrial(shipId, generation.SilverPortId,
                    new IndustrialRoutePolicy(generation.SilverPortId, generation.RecoveryPortId,
                        new IndustrialRouteLeg("logs", Q(3), Q(1), destinationKind: IndustrialEndpointKind.Warehouse,
                            destinationFacilityId: Bakery, maximumBuyPrice: 18),
                        new IndustrialRouteLeg("bread", Q(4), Q(1), maximumBuyPrice: 50, minimumSellPrice: 1), M(100), true));
        }

        private void ManageWarehouse(string facilityId, string output, int ceiling)
        {
            var state = World.Snapshot();
            if (!state.PrivateFacilities.TryGetValue(facilityId, out var facility)) return;
            var enabled = state.Companies[Owner].Cash >= M(120) && facility.Inventory[output] < Q(ceiling);
            if (facility.Enabled != enabled) Apply(w => w.SetFacilityEnabled(Owner, facilityId, enabled));
        }

        private void PositionIndustrial(string shipId, string source, IndustrialRoutePolicy policy)
        {
            var ship = World.Snapshot().Ships[shipId];
            if (ship.AutomationEnabled || ship.DockedPortId == null) return;
            if (ship.DockedPortId != source) { Apply(w => w.Depart(shipId, source)); return; }
            // Granted independent cargo is sold locally through the normal finite market before a route replacement.
            foreach (var good in ship.Cargo.Where(g => g.Value > 0).Select(g => g.Key).ToArray()) Sell(shipId, good);
            if (World.Snapshot().Ships[shipId].CargoUnits == 0) Apply(w => w.ConfigureIndustrialAutomation(shipId, policy, true));
        }
        private void PositionMerchant(string shipId, string source, TradeRoutePolicy policy)
        {
            var ship = World.Snapshot().Ships[shipId];
            if (ship.AutomationEnabled || ship.DockedPortId == null) return;
            if (ship.DockedPortId != source) Apply(w => w.Depart(shipId, source));
            else if (ship.CargoUnits == 0) Apply(w => w.ConfigureAutomation(shipId, policy, true));
        }

        private void RoyalVoyage(string shipId, bool mining, bool publicTrade)
        {
            var ship = World.Snapshot().Ships[shipId];
            if (ship.DockedPortId == null) return;
            if (ship.DockedPortId == generation.SilverPortId)
            {
                if (mining && World.Snapshot().PrivateFacilities.TryGetValue(Mine, out var mine))
                {
                    var quantity = Math.Min(Q(3) - ship.Cargo["silver"], Math.Min(mine.Inventory["silver"], ship.CargoCapacity - ship.CargoUnits));
                    quantity -= quantity % EconomicUnits.QuantityScale;
                    if (quantity > 0) Apply(w => w.TransferGoods(Owner, shipId, Mine, TransferDirection.WarehouseToShip, "silver", quantity));
                }
                var remaining = World.Snapshot().Economy!.Contracts.Single(c => c.Configuration.Id == "royal_furniture").RemainingQuantity;
                if (remaining > 0) Buy(shipId, "furniture", Math.Min(Q(2), remaining));
                if (World.Snapshot().Ships[shipId].CargoUnits > 0) Apply(w => w.Depart(shipId, generation.MintPortId));
                return;
            }
            if (ship.DockedPortId != generation.MintPortId) { Apply(w => w.Depart(shipId, generation.SilverPortId)); return; }
            if (publicTrade)
            {
                foreach (var contract in World.Snapshot().Economy!.Contracts)
                {
                    var current = World.Snapshot();
                    var available = Math.Min(current.Ships[shipId].Cargo[contract.Configuration.GoodId], contract.RemainingQuantity);
                    available -= available % EconomicUnits.QuantityScale;
                    if (available > 0 && current.PreviewExternalContract(shipId, contract.Configuration.Id, available).Outcome == DevelopmentOutcome.Completed)
                        Apply(w => w.FulfillExternalContract(shipId, contract.Configuration.Id, available));
                }
                if (mining)
                {
                    if (World.Snapshot().PreviewMintExchange(shipId).Outcome == DevelopmentOutcome.Completed)
                        Apply(w => w.MintSilver(shipId));
                    if (World.Snapshot().Economy!.TreasuryPlanks < Q(2))
                    {
                        Buy(shipId, "planks", Q(2) - World.Snapshot().Economy!.TreasuryPlanks);
                        var held = World.Snapshot().Ships[shipId].Cargo["planks"];
                        if (held > 0 && World.Snapshot().PreviewTreasuryProcurement(shipId, held).Outcome == DevelopmentOutcome.Completed)
                            Apply(w => w.SupplyPublicWorks(shipId, held));
                    }
                }
                else
                    foreach (var name in new[] { "royal_planks", "royal_flour" })
                    {
                        var contract = World.Snapshot().Economy!.Contracts.Single(c => c.Configuration.Id == name);
                        if (contract.RemainingQuantity == 0) continue;
                        Buy(shipId, contract.Configuration.GoodId, Math.Min(Q(4), contract.RemainingQuantity));
                        var held = World.Snapshot().Ships[shipId].Cargo[contract.Configuration.GoodId];
                        if (held > 0 && World.Snapshot().PreviewExternalContract(shipId, name, held).Outcome == DevelopmentOutcome.Completed)
                            Apply(w => w.FulfillExternalContract(shipId, name, held));
                    }
            }
            foreach (var good in World.Snapshot().Ships[shipId].Cargo.Where(g => g.Value > 0 && g.Key != "silver").Select(g => g.Key).ToArray()) Sell(shipId, good);
            if (World.Snapshot().Ships[shipId].Cargo["silver"] == 0)
                Apply(w => w.Depart(shipId, generation.SilverPortId));
        }

        private void Buy(string shipId, string good, int requested)
        {
            var state = World.Snapshot(); var ship = state.Ships[shipId];
            if (!Active(state) || ship.DockedPortId == null || requested <= 0) return;
            var quantity = Math.Min(requested, state.MaximumTradeQuantity(shipId, ship.DockedPortId, TradeDirection.Buy, good));
            quantity -= quantity % EconomicUnits.QuantityScale;
            if (quantity <= 0) return;
            var command = new TradeCommand(TradeDirection.Buy, good, quantity);
            var quote = state.PreviewTrade(shipId, ship.DockedPortId, command);
            if (state.Companies[Owner].Cash - quote.ExecutedAmount < M(100)) return;
            Apply(w => w.ExecuteTrade(shipId, ship.DockedPortId, command, state));
        }
        private void Sell(string shipId, string good)
        {
            var state = World.Snapshot(); var ship = state.Ships[shipId];
            if (!Active(state) || ship.DockedPortId == null) return;
            var quantity = state.MaximumTradeQuantity(shipId, ship.DockedPortId, TradeDirection.Sell, good);
            if (quantity > 0) Apply(w => w.ExecuteTrade(shipId, ship.DockedPortId,
                new TradeCommand(TradeDirection.Sell, good, quantity), state));
        }

        internal void VerifySaveContinuation()
        {
            var before = World.Snapshot();
            var save = WorldSaveCodec.Save(World);
            var loaded = WorldSaveCodec.Load(content, save);
            Require(save == WorldSaveCodec.Save(loaded), "Save/load changed committed authoritative state.");
            var leftEvents = new List<string>(); var rightEvents = new List<string>();
            var left = new Trial(content, World, strategy, leftEvents);
            var right = new Trial(content, loaded, strategy, rightEvents);
            for (var day = 0; day < 7 && Active(left.World.Snapshot()); day++)
            {
                left.Act(); right.Act();
                if (Active(left.World.Snapshot())) { left.Apply(w => w.AdvanceDay()); right.Apply(w => w.AdvanceDay()); }
            }
            Require(WorldSaveCodec.Save(left.World) == WorldSaveCodec.Save(right.World) && leftEvents.SequenceEqual(rightEvents),
                "Save continuation changed operations, accounts, physical state, FIFO or challenge progress.");
            Row.SaveChecks.Add(new { before.Day, before.Revision, Characters = save.Length,
                SailingShips = before.Ships.Values.Count(s => s.DockedPortId == null),
                PendingShips = before.Economy!.ShipOrders.Count,
                PendingFactories = before.PrivateFacilities.Values.Count(f => !f.IsOperational),
                VerifiedContinuationDays = left.World.Snapshot().Day - before.Day,
                VerifiedOperations = leftEvents.Count, State = Fingerprint(save) });
            World = WorldSaveCodec.Load(content, save);
        }
    }

    private static void Increment(IDictionary<string, long> values, string key, long value) =>
        values[key] = checked((values.TryGetValue(key, out var previous) ? previous : 0) + value);
    private static string EventSignature(RegionalResult result) => JsonConvert.SerializeObject(new {
        result.After.Day, result.After.Revision, result.EconomicEvents, result.CashTransfers, result.Consumption,
        result.Production, result.PrivateProduction, result.Trades, result.WarehouseTrades, result.Transfers,
        result.Movements, result.Construction, result.Development });
    private static string Fingerprint(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static SnapshotSample Sample(RegionalWorldSnapshot state)
    {
        var finance = state.Accounting.Companies[Owner]; var value = state.CompanyValue(Owner); var economy = state.Economy!;
        return new SnapshotSample { Day = state.Day, Revision = state.Revision, Cash = state.Companies[Owner].Cash,
            Value = value.Total, ValueChange = value.Change, ValueBreakdown = value,
            MaterialMargin = finance.RealizedGrossMargin, Wages = finance.WagesPaid, OperatingSurplus = finance.OperatingSurplusAfterWages,
            MintReceipts = finance.MintReceipts, MintMaterialCost = finance.MintMaterialCost, HeldBasis = finance.HeldInventoryCost,
            PrivateProduced = finance.ProducedQuantities, PrivateSold = finance.SoldQuantities,
            Companies = state.Companies.Values.Select(c => new { c.Id, c.Cash,
                Accounting = state.Accounting.Companies[c.Id] }).ToArray(),
            RegionalCash = state.RegionalCash.ToString(CultureInfo.InvariantCulture), GlobalCash = state.TotalCash.ToString(CultureInfo.InvariantCulture),
            TreasuryCash = economy.TreasuryCash, ExternalCash = economy.ExternalCash, ExternalInflow = economy.ExternalNetInflow,
            Minted = economy.MintedCurrency, SupplierIssuance = economy.SupplierIssuance, TreasuryIssuance = economy.TreasuryIssuance,
            SilverReserve = economy.SilverReserve, PublicWorksPlanks = economy.TreasuryPlanks, TreasurySpent = economy.TreasuryExpenditure,
            MintStatus = economy.MintStatus.ToString(), Contracts = economy.Contracts.Select(c => new {
                c.Configuration.Id, c.Configuration.GoodId, c.DeliveredQuantity, c.RemainingQuantity, c.AmountPaid }).ToArray(),
            PlayerShips = state.Ships.Values.Count(s => s.IsPlayer), PendingShips = economy.ShipOrders.Count,
            Ships = state.Ships.Values.Select(s => new { s.Id, s.OwnerId, s.IsPlayer, s.DockedPortId, s.DestinationPortId,
                s.RemainingTravelDays, s.Cargo, s.AutomationEnabled, Stage = s.IndustrialStage.ToString() }).ToArray(),
            Facilities = state.PrivateFacilities.Values.Select(f => new { f.Id, f.PortId, f.Blueprint.BuildingId,
                f.IsOperational, f.NextProductionDay, f.Inventory, f.BatchesProduced, f.WagesPaid,
                LastOutcome = f.LastOutcome.ToString(), Procurement = f.ProcurementWaitReason.ToString(), Sales = f.SalesWaitReason.ToString() }).ToArray(),
            Towns = state.Ports.Values.Select(p => new { p.Id, p.MarketCash, HouseholdCash = p.City!.HouseholdCash,
                p.City.InitialPopulation, p.City.Population, p.City.Prosperity, p.City.EssentialFulfillmentPercent,
                p.City.RecoveryFulfillmentPercent, p.City.TotalWagesReceived, p.City.TotalRetailSpend, p.Inventory,
                Needs = p.Consumption.Select(c => new { c.Configuration.Id, c.Configuration.GoodId, c.LastDesiredQuantity,
                    c.LastConsumedQuantity, c.LastPayment, c.TotalDesiredQuantity, c.TotalConsumedQuantity, c.TotalPayment,
                    c.TotalUnmetStock, c.TotalUnmetAffordability, c.DemandRemainder, c.BaselineDemandRemainder }).ToArray() }).ToArray(),
            Objective = state.Challenge == null ? null : ExpandingChallenges.Progress(state).Components };
    }

    private static object V2Control(ContentCatalog content, int seed, bool merchant, int horizon)
    {
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.LivingCities, seed));
        if (merchant) world.SetAutomationEnabled(Player, true);
        for (var day = 0; day < horizon; day++) world.AdvanceDay();
        var state = world.Snapshot();
        return new { Seed = seed, Ruleset = 2, Strategy = merchant ? "historical_merchant" : "historical_idle", state.Day,
            Cash = state.Companies[Owner].Cash, Towns = state.Ports.Values.Select(p => new { p.Id, p.City!.Population,
                p.City.Prosperity, p.MarketCash, p.City.HouseholdCash, Bread = p.Inventory["bread"] }).ToArray() };
    }

    private static void Measure(ContentCatalog content)
    {
        var baseline = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.LivingCities, 42));
        baseline.SetAutomationEnabled(Player, true);
        MeasureWorld(baseline, "V2 baseline, 1 owned ship +3 NPCs");
        foreach (var fleet in new[] { 1, 2, 3 })
        {
            var trial = new Trial(content, RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter,
                RunWorldMode.ExpandingEconomy, 42)), "idle");
            for (var i = 1; i < fleet; i++) trial.Apply(w => w.CommissionShip(Owner, trial.World.Snapshot().Economy!.Configuration.ShipwrightPortId));
            for (var day = 0; day < 4; day++) trial.World.AdvanceDay();
            var geography = ExpandingEconomyGeneration.Generate(42);
            trial.World.ConfigureAutomation(Player, ExpandingEconomyScenario.MerchantPolicy(geography, true), true);
            foreach (var ship in trial.World.Snapshot().Ships.Values.Where(s => s.IsPlayer && s.Id != Player))
                trial.World.ConfigureAutomation(ship.Id, new TradeRoutePolicy(geography.ProcessingPortId,
                    geography.FurniturePortId, new RouteLeg("planks", Q(2), Q(1), 30, 1),
                    new RouteLeg("logs", Q(4), Q(1), 18, 1), M(100), true), true);
            MeasureWorld(trial.World, "V3 " + fleet + " owned ships +3 NPCs");
        }
    }

    private static void MeasureWorld(RegionalWorldState world, string label)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew();
        for (var day = 0; day < 180; day++) world.AdvanceDay();
        timer.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var snapshotAllocated = GC.GetAllocatedBytesForCurrentThread(); var snapshotTimer = Stopwatch.StartNew();
        for (var read = 0; read < 10000; read++) GC.KeepAlive(world.Snapshot());
        snapshotTimer.Stop(); snapshotAllocated = GC.GetAllocatedBytesForCurrentThread() - snapshotAllocated;
        var state = world.Snapshot(); var lots = state.Accounting.Stores.Values.Sum(s => s.Goods.Values.Sum(g => g.Lots.Count));
        Console.WriteLine(FormattableString.Invariant($"Measured {label}: {timer.Elapsed.TotalMilliseconds / 180:F4}ms/day, {allocated / 180} allocated bytes/day; snapshot reads {snapshotTimer.Elapsed.TotalMilliseconds / 10000:F6}ms/read, {snapshotAllocated} bytes/10000 reads; {lots} retained FIFO lots (180 daily commits; no observer serialization)."));
    }

    internal sealed class Report
    {
        public int FormatVersion => 1;
        public int RulesetVersion => 3;
        public int QuantityScale => 10000;
        public int MoneyScale => 10000;
        public string Methodology { get; set; } = "Fixed ordinary-command policies; both pricing hypotheses and ascensions. Deep matrix extends 15 seeds through 1095 days with idle/balanced/circulation/network2/fleet3/monetary policies. Circulation uses local paid employers and stock/cash-gated batch controls; fleet3 buys both additional ships before expansion, exposing their opportunity cost. No state repair, subsidies or forced success. Every world commit audits all-account/regional issuance, physical flows and FIFO. Save checks compare exact states and seven further days of committed events. V2 controls retain historical whole units; all v3 integers are raw 1/10000 units. Runtime/memory observations are printed, never serialized.";
        public int[] Seeds { get; set; } = Array.Empty<int>();
        public int Horizon { get; set; }
        public List<Row> Runs { get; } = new();
        public List<Row> Challenges { get; } = new();
        public List<object> V2Controls { get; } = new();
    }
    internal sealed class Row
    {
        public int Seed { get; set; }
        public int Ascension { get; set; }
        public string Pricing { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Charter { get; set; } = "";
        public string? Challenge { get; set; }
        public string Identity { get; set; } = "";
        public string Signature { get; set; } = "";
        public string Outcome { get; set; } = "";
        public string FinalStateFingerprint { get; set; } = "";
        public long Days { get; set; }
        public long Operations { get; set; }
        public int MaximumEvents { get; set; }
        public int MaximumOpenLots { get; set; }
        public SnapshotSample Initial { get; set; } = new();
        public SnapshotSample Final { get; set; } = new();
        public List<SnapshotSample> Checkpoints { get; } = new();
        public SortedDictionary<string, long> Flows { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> Attempts { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> Waits { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> FirstProblems { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> LastRetailDay { get; } = new(StringComparer.Ordinal);
        public List<object> Development { get; } = new();
        public List<object> SaveChecks { get; } = new();
    }
    internal sealed class SnapshotSample
    {
        public long Day { get; set; }
        public long Revision { get; set; }
        public long Cash { get; set; }
        public long Value { get; set; }
        public long ValueChange { get; set; }
        public object? ValueBreakdown { get; set; }
        public long MaterialMargin { get; set; }
        public long Wages { get; set; }
        public long OperatingSurplus { get; set; }
        public long MintReceipts { get; set; }
        public long MintMaterialCost { get; set; }
        public long HeldBasis { get; set; }
        public IReadOnlyDictionary<string, long>? PrivateProduced { get; set; }
        public IReadOnlyDictionary<string, long>? PrivateSold { get; set; }
        public string RegionalCash { get; set; } = "";
        public string GlobalCash { get; set; } = "";
        public long TreasuryCash { get; set; }
        public long ExternalCash { get; set; }
        public long ExternalInflow { get; set; }
        public long Minted { get; set; }
        public long SupplierIssuance { get; set; }
        public long TreasuryIssuance { get; set; }
        public long SilverReserve { get; set; }
        public int PublicWorksPlanks { get; set; }
        public long TreasurySpent { get; set; }
        public string MintStatus { get; set; } = "";
        public object[] Contracts { get; set; } = Array.Empty<object>();
        public object[] Companies { get; set; } = Array.Empty<object>();
        public int PlayerShips { get; set; }
        public int PendingShips { get; set; }
        public object[] Ships { get; set; } = Array.Empty<object>();
        public object[] Facilities { get; set; } = Array.Empty<object>();
        public object[] Towns { get; set; } = Array.Empty<object>();
        public IReadOnlyDictionary<string, int>? Objective { get; set; }
    }
}
