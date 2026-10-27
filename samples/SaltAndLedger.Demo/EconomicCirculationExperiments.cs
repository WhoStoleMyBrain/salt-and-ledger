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

/// <summary>Matched finite ordinary-command trials. Observers never alter economic state.</summary>
internal static class EconomicCirculationExperiments
{
    internal static readonly int[] Seeds = { -42, 42, 99, -734282136 };
    private static readonly long[] CheckpointDays = { 30, 90, 140, 180, 365 };
    private const string Owner = WorldIds.PlayerOwnerId, Ship = WorldIds.PlayerShipId;
    private const string Farm = "circulation_farm", Mill = "circulation_mill", Bakery = "circulation_bakery", Camp = "circulation_camp";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private static int Q(int value) => EconomicUnits.FromWhole(value);
    private static long M(long value) => EconomicUnits.Coins(value);
    internal static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n");
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }

    internal static int Run(ContentCatalog content, string? path, bool check)
    {
        var timer = Stopwatch.StartNew();
        var report = BuildReport(content);
        var json = Json(report) + "\n";
        path ??= "docs/experiments/economic-circulation-results.json";
        if (check) Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
            "Economic circulation report is stale; regenerate with --economic-circulation.");
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        Console.WriteLine(FormattableString.Invariant($"Circulation: {report.Runs.Count} version1 A/B worlds + {report.HistoricalControls.Count} version0 controls; {report.PrimaryWorldDays} primary days in {timer.Elapsed.TotalSeconds:F3}s."));
        Console.WriteLine("Per-operation global/regional money, physical goods, company cash and FIFO checks passed; representative replay/culture and default-mode save continuations passed.");
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    internal static Report BuildReport(ContentCatalog content)
    {
        var report = new Report();
        foreach (var seed in Seeds)
        foreach (var pricing in new[] { LivingPricingMode.StockOnly, LivingPricingMode.Coverage })
        foreach (var managed in new[] { false, true })
        {
            report.HistoricalControls.Add(RunCase(content, seed, pricing, managed, 0, CirculationMode.None, 365));
            foreach (var arm in new[] { CirculationMode.None, CirculationMode.FeesOnly, CirculationMode.MunicipalOnly, CirculationMode.Combined })
                report.Runs.Add(RunCase(content, seed, pricing, managed, 1, arm, 365));
        }
        foreach (var row in report.Runs)
        {
            var untreated = report.Runs.Single(r => r.Seed == row.Seed && r.Pricing == row.Pricing &&
                r.Strategy == row.Strategy && r.Arm == CirculationMode.None.ToString());
            var historical = report.HistoricalControls.Single(r => r.Seed == row.Seed && r.Pricing == row.Pricing && r.Strategy == row.Strategy);
            Require(Json(row.Initial.Towns) == Json(untreated.Initial.Towns) && Json(row.Initial.Companies) == Json(untreated.Initial.Companies),
                "Fiscal arms changed initial town endowments, quotes or company accounts.");
            report.Comparisons.Add(new { row.Seed, row.Pricing, row.Strategy, row.Arm,
                VersusRevision1NoFiscal = Difference(row.Final, untreated.Final),
                VersusUntouchedRevision0 = Difference(row.Final, historical.Final) });
        }
        var reference = report.Runs.Single(r => r.Seed == 42 && r.Pricing == "Coverage" && r.Strategy == "managed" && r.Arm == "Combined");
        Require(Json(reference) == Json(RunCase(content, 42, LivingPricingMode.Coverage, true, 1, CirculationMode.Combined, 365, true)),
            "Reversed configuration changed circulation results.");
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Require(Json(reference) == Json(RunCase(content, 42, LivingPricingMode.Coverage, true, 1, CirculationMode.Combined, 365)),
                "Culture/replay changed circulation results.");
        }
        finally { CultureInfo.CurrentCulture = original; }
        report.Verification.Add("All 80 primary worlds audited after every ordinary command and daily commit.");
        report.Verification.Add("Seed42 Coverage managed Combined: full 365-day reversed-configuration and Turkish-culture replay matched all measurements and final state.");
        report.Verification.Add("Each Combined world: exact round-trip plus seven further days of state/events at days 30 and 140; diagnostic nondefault modes deliberately cannot be durable named saves.");
        return report;
    }

    internal static Row RunCase(ContentCatalog content, int seed, LivingPricingMode pricing, bool managed,
        int economicVersion, CirculationMode arm, int horizon, bool reverse = false)
    {
        var identity = new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, seed,
            livingPricingMode: pricing, companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1,
            economicRulesVersion: economicVersion);
        var world = ExpandingEconomyScenario.Create(content, identity, reverse,
            economicVersion == 0 ? null : new CirculationConfiguration(mode: arm));
        var trial = new Trial(content, world, managed);
        trial.Initialize();
        while (trial.World.Snapshot().Day < horizon)
        {
            trial.Act();
            trial.Apply(w => w.AdvanceDay());
            var day = trial.World.Snapshot().Day;
            if (economicVersion == 1 && arm == CirculationMode.Combined && (day == 30 || day == 140)) trial.VerifySaveContinuation();
            if (CheckpointDays.Contains(day)) trial.Row.Checkpoints.Add(Sample(trial.World.Snapshot()));
        }
        trial.Row.Final = Sample(trial.World.Snapshot());
        trial.Row.FinalStateFingerprint = RunDiagnosticReport.StateFingerprint(trial.World.Snapshot());
        return trial.Row;
    }

    internal sealed class Trial
    {
        internal RegionalWorldState World;
        internal readonly Row Row;
        private readonly ContentCatalog content;
        private readonly ExpandingEconomyGeneration generation;
        private readonly bool managed;
        private readonly List<string>? events;
        private readonly BigInteger initialAll, initialRegional;

        internal Trial(ContentCatalog content, RegionalWorldState world, bool managed, List<string>? events = null)
        {
            this.content = content; World = world; this.managed = managed; this.events = events;
            var state = world.Snapshot(); var identity = state.RunIdentity!;
            generation = ExpandingEconomyGeneration.Generate(identity.Seed!.Value, pricingMode: identity.LivingPricingMode,
                economicRulesVersion: identity.EconomicRulesVersion);
            initialAll = state.TotalCash - state.Economy!.MintedCurrency;
            initialRegional = state.RegionalCash - state.Economy.MintedCurrency - state.Economy.ExternalNetInflow;
            Row = new Row { Seed = identity.Seed.Value, Pricing = identity.LivingPricingMode.ToString(),
                Strategy = managed ? "managed" : "idle", EconomicRulesVersion = identity.EconomicRulesVersion,
                Identity = identity.ComparisonGroup, Arm = state.Circulation?.Configuration.Mode.ToString() ?? "Historical",
                Initial = Sample(state) };
        }

        internal void Initialize()
        {
            if (!managed) return;
            Build(Farm, generation.FoodSourcePortId, ExpandingEconomyGeneration.FarmBlueprint,
                new IndustryPolicy(false, true, M(100), outputs: new[] { new OutputSaleRule("grain", 0, 1) }));
            Build(Mill, generation.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint, ExpandingEconomyScenario.MillPolicy());
            Build(Bakery, generation.RecoveryPortId, WorldIds.BakeryBlueprint,
                new IndustryPolicy(true, true, M(100), new[] { new InputStockRule("logs", Q(3), 18) },
                    new[] { new OutputSaleRule("bread", 0, 1) }));
            Build(Camp, generation.RecoveryPortId, ExpandingEconomyGeneration.LumberBlueprint,
                new IndustryPolicy(false, true, M(100), outputs: new[] { new OutputSaleRule("logs", 0, 1) }));
        }

        private void Build(string id, string port, string blueprint, IndustryPolicy policy)
        {
            Apply(w => w.ConstructFacility(Owner, port, blueprint, id));
            if (World.Snapshot().PrivateFacilities.ContainsKey(id)) Apply(w => w.ConfigureFacility(Owner, id, policy));
        }

        internal void Act()
        {
            if (!managed) return;
            Manage(Farm, "grain", 24); Manage(Mill, "flour", 12);
            Manage(Bakery, "bread", 24); Manage(Camp, "logs", 12);
            var ship = World.Snapshot().Ships[Ship];
            if (ship.AutomationEnabled || ship.DockedPortId == null) return;
            if (ship.DockedPortId != generation.ProcessingPortId)
                Apply(w => w.Depart(Ship, generation.ProcessingPortId));
            else
                Apply(w => w.ConfigureIndustrialAutomation(Ship, new IndustrialRoutePolicy(generation.ProcessingPortId,
                    generation.RecoveryPortId, new IndustrialRouteLeg("flour", Q(4), Q(1),
                        destinationKind: IndustrialEndpointKind.Warehouse, destinationFacilityId: Bakery, maximumBuyPrice: 22),
                    new IndustrialRouteLeg("logs", Q(8), Q(1), maximumBuyPrice: 12, minimumSellPrice: 1),
                    cashReserve: M(100), allowEmptyReturn: true), true));
        }

        private void Manage(string id, string output, int ceiling)
        {
            var state = World.Snapshot();
            if (!state.PrivateFacilities.TryGetValue(id, out var facility)) return;
            var enabled = state.Companies[Owner].Cash >= M(120) && state.Ports[facility.PortId].Inventory[output] < Q(ceiling);
            if (facility.Enabled != enabled) Apply(w => w.SetFacilityEnabled(Owner, id, enabled));
        }

        internal void Apply(Func<RegionalWorldState, RegionalResult> command)
        {
            var result = command(World); var after = result.After;
            Audit(result);
            Require(after.TotalCash == initialAll + after.Economy!.MintedCurrency, "All-account money boundary failed.");
            Require(after.RegionalCash == initialRegional + after.Economy.MintedCurrency + after.Economy.ExternalNetInflow,
                "Regional money boundary failed.");
            events?.Add(EventSignature(result));
            Row.Operations++;
            Row.MaximumEvents = Math.Max(Row.MaximumEvents, result.EconomicEvents.Count + result.CashTransfers.Count +
                result.Production.Count + result.PrivateProduction.Count + result.Consumption.Count + result.Movements.Count);
            Row.MaximumOpenLots = Math.Max(Row.MaximumOpenLots, after.Accounting.Stores.Values.Sum(s => s.Goods.Values.Sum(g => g.Lots.Count)));
            foreach (var production in result.Production)
            {
                Increment(Row.Attempts, "public/" + production.PortId + "/" + production.RecipeId + "/" + production.Outcome, 1);
                Flow(production.RecipeId, production.Outcome, after.QuantityScale);
            }
            foreach (var production in result.PrivateProduction)
            {
                Increment(Row.Attempts, "private/" + production.FacilityId + "/" + production.RecipeId + "/" + production.Outcome, 1);
                Flow(production.RecipeId, production.Outcome, after.QuantityScale);
            }
            foreach (var trade in result.Trades)
                ObserveTrade(trade.OwnerId, trade.PortId, trade.Trade);
            foreach (var trade in result.WarehouseTrades)
                ObserveTrade(trade.OwnerId, trade.PortId, trade.Trade);
            foreach (var transfer in result.CashTransfers)
                Increment(Row.AccountTransfers, transfer.Kind + "/" + transfer.PortId + "/" + transfer.SourceAccountId + "→" + transfer.DestinationAccountId, transfer.Amount);
            foreach (var need in result.Consumption)
            {
                Increment(Row.Flows, "consumed/" + need.GoodId, need.ConsumedQuantity);
                if (need.Kind != ConsumptionKind.Essential) continue;
                if (need.UnmetStock > 0) First("missing_food/" + need.PortId, after.Day);
                if (need.UnmetAffordability > 0) First("unaffordable_food/" + need.PortId, after.Day);
                if (need.Payment > 0) Row.LastPaidFoodDay[need.PortId] = after.Day;
            }
            foreach (var decision in result.Decisions)
                if (decision.Action == AutomationAction.Wait || decision.Action == AutomationAction.Pause)
                    Increment(Row.Waits, decision.ShipId + "/" + decision.Reason, 1);
            foreach (var movement in result.Movements)
                if (movement.Kind == ShipMovementKind.Arrival) Increment(Row.Flows, "arrivals/" + movement.ShipId + "/" + movement.DestinationPortId, 1);
        }

        private void ObserveTrade(string owner, string port, TradeAttempt trade)
        {
            var key = owner + "/" + port + "/" + trade.Direction + "/" + trade.GoodId;
            Increment(Row.Attempts, "trade/" + key + "/" + trade.Outcome, 1);
            Increment(Row.Flows, "traded/" + key, trade.ExecutedQuantity);
        }
        private void First(string key, long day) { if (!Row.FirstProblems.ContainsKey(key)) Row.FirstProblems.Add(key, day); }
        private void Flow(string recipeId, ScheduledProductionOutcome outcome, int scale)
        {
            if (outcome != ScheduledProductionOutcome.Produced) return;
            var recipe = content.ForExpandingEconomy().GetRecipe(recipeId);
            foreach (var input in recipe.Inputs) Increment(Row.Flows, "transformation_input/" + input.GoodId, checked((long)input.Quantity * scale));
            foreach (var output in recipe.Outputs) Increment(Row.Flows,
                (recipe.Kind == RecipeKind.Extraction ? "extracted/" : "transformation_output/") + output.GoodId, checked((long)output.Quantity * scale));
        }

        internal void VerifySaveContinuation()
        {
            var before = World.Snapshot(); var save = WorldSaveCodec.Save(World);
            var loaded = WorldSaveCodec.Load(content, save);
            Require(save == WorldSaveCodec.Save(loaded), "Circulation save round-trip changed state.");
            var leftEvents = new List<string>(); var rightEvents = new List<string>();
            var left = new Trial(content, World, managed, leftEvents);
            var right = new Trial(content, loaded, managed, rightEvents);
            for (var day = 0; day < 7; day++)
            {
                left.Act(); right.Act();
                left.Apply(w => w.AdvanceDay()); right.Apply(w => w.AdvanceDay());
            }
            Require(WorldSaveCodec.Save(left.World) == WorldSaveCodec.Save(right.World) && leftEvents.SequenceEqual(rightEvents),
                "Circulation continuation changed fees, fiscal reviews, accounts, goods, FIFO, prices or events.");
            Row.SaveChecks.Add(new { before.Day, before.Revision, Days = 7, Operations = leftEvents.Count,
                Fingerprint = RunDiagnosticReport.StateFingerprint(before) });
            World = WorldSaveCodec.Load(content, save);
        }
    }

    internal static void Audit(RegionalResult result)
    {
        var before = result.Before; var after = result.After;
        Require(before.TotalCash + after.Economy!.MintedCurrency - before.Economy!.MintedCurrency == after.TotalCash,
            "Operation created or lost unaccounted money.");
        Require(before.RegionalCash + after.Economy.MintedCurrency - before.Economy.MintedCurrency +
            after.Economy.ExternalNetInflow - before.Economy.ExternalNetInflow == after.RegionalCash, "Regional operation boundary mismatch.");
        var paidFees = after.Accounting.Companies.Values.Sum(c => c.FeesPaid) - before.Accounting.Companies.Values.Sum(c => c.FeesPaid);
        var feeEvents = result.CashTransfers.Where(t => t.Kind == CityCashTransferKind.ServiceFeeMarket ||
            t.Kind == CityCashTransferKind.ServiceFeeHouseholds).Sum(t => t.Amount);
        Require(paidFees == feeEvents, "Company fee expense differs from actual local receipts.");
        if (after.Circulation != null)
        {
            foreach (var town in after.Circulation.Towns.Values)
            {
                var prior = before.Circulation!.Towns[town.PortId];
                Require(town.TotalLevy - prior.TotalLevy == result.CashTransfers.Where(t => t.PortId == town.PortId &&
                    t.Kind == CityCashTransferKind.MunicipalLevy).Sum(t => t.Amount), "Levy aggregate differs from transferred cash.");
                Require(town.TotalRelief - prior.TotalRelief == result.CashTransfers.Where(t => t.PortId == town.PortId &&
                    t.Kind == CityCashTransferKind.FoodRelief).Sum(t => t.Amount), "Relief aggregate differs from transferred cash.");
            }
        }
        foreach (var good in after.Ports.First().Value.Inventory.Keys)
        {
            BigInteger Physical(RegionalWorldSnapshot state) => state.Ports.Values.Aggregate(BigInteger.Zero, (n, p) => n + p.Inventory[good]) +
                state.Ships.Values.Aggregate(BigInteger.Zero, (n, s) => n + s.Cargo[good]) +
                state.CompanyStockpiles.Values.Aggregate(BigInteger.Zero, (n, s) => n + s.Inventory[good]) +
                state.PrivateFacilities.Values.Aggregate(BigInteger.Zero, (n, f) => n + f.Inventory[good]) +
                (good == "silver" ? state.Economy!.SilverReserve : 0) + (good == "planks" ? state.Economy!.TreasuryPlanks : 0);
            var production = result.Production.SelectMany(p => p.Changes).Concat(result.PrivateProduction.SelectMany(p => p.Changes))
                .Where(c => c.GoodId == good).Aggregate(BigInteger.Zero, (n, c) => n + c.Delta);
            var consumed = result.Consumption.Where(c => c.GoodId == good).Aggregate(BigInteger.Zero, (n, c) => n + c.ConsumedQuantity);
            Require(Physical(before) + production - consumed == Physical(after), "Physical flow mismatch: " + good);
        }
        foreach (var finance in after.Accounting.Companies.Values)
        {
            Require((BigInteger)finance.InventoryPurchases == (BigInteger)finance.CostOfGoodsSold + finance.HeldInventoryCost + finance.MintMaterialCost,
                "FIFO material basis failed.");
            Require((BigInteger)finance.InitialCash - finance.InventoryPurchases + finance.SalesRevenue - finance.ConstructionExpenditure -
                finance.WagesPaid - finance.FeesPaid - finance.PublicCommissionExpenditure + finance.MintReceipts == after.Companies[finance.OwnerId].Cash,
                "Company cash does not reconcile with actual expenses.");
            Require(after.Companies[finance.OwnerId].Cash >= 0, "Negative company account.");
            foreach (var good in after.Ports.First().Value.Inventory.Keys)
            {
                long Amount(IReadOnlyDictionary<string, long> values) => values.TryGetValue(good, out var value) ? value : 0;
                Require((BigInteger)Amount(finance.GrantedQuantities) + Amount(finance.PurchasedQuantities) + Amount(finance.ProducedQuantities) -
                    Amount(finance.ConsumedQuantities) - Amount(finance.SoldQuantities) - Amount(finance.SurrenderedQuantities) == Amount(finance.HeldInventoryQuantities),
                    "Owned physical flow mismatch.");
            }
        }
        foreach (var port in after.Ports.Values)
            Require(port.MarketCash >= 0 && port.City!.HouseholdCash >= 0 && port.Inventory.Values.All(q => q >= 0), "Negative town balance/stock.");
        foreach (var store in after.Accounting.Stores.Values)
        {
            var inventory = store.StoreId.StartsWith("ship:", StringComparison.Ordinal)
                ? after.Ships[store.StoreId.Substring(5)].Cargo : after.CompanyStockpiles[store.StoreId].Inventory;
            foreach (var good in inventory) Require(good.Value >= 0 && good.Value == store.GetQuantity(good.Key), "Physical/FIFO store mismatch.");
        }
    }

    private static void Increment(IDictionary<string, long> values, string key, long value) =>
        values[key] = checked((values.TryGetValue(key, out var previous) ? previous : 0) + value);
    private static string EventSignature(RegionalResult result) => JsonConvert.SerializeObject(new {
        result.After.Day, result.After.Revision, result.EconomicEvents, result.CashTransfers, result.Consumption,
        result.Production, result.PrivateProduction, result.Trades, result.WarehouseTrades, result.Transfers,
        result.Movements, result.Construction, result.Development });

    private static SampleRow Sample(RegionalWorldSnapshot state)
    {
        var finance = state.Accounting.Companies[Owner];
        return new SampleRow { Day = state.Day, Revision = state.Revision,
            GlobalCash = state.TotalCash.ToString(CultureInfo.InvariantCulture), RegionalCash = state.RegionalCash.ToString(CultureInfo.InvariantCulture),
            PlayerCash = state.Companies[Owner].Cash, NpcCash = state.Companies.Values.Where(c => c.Id != Owner).Sum(c => c.Cash),
            MarketCash = state.Ports.Values.Sum(p => p.MarketCash), HouseholdCash = state.Ports.Values.Sum(p => p.City!.HouseholdCash),
            TreasuryCash = state.Economy!.TreasuryCash, ExternalCash = state.Economy.ExternalCash,
            Minted = state.Economy.MintedCurrency, ExternalNetInflow = state.Economy.ExternalNetInflow,
            PlayerValue = state.CompanyValue(Owner).Total, PlayerMargin = finance.RealizedGrossMargin,
            PlayerWages = finance.WagesPaid, PlayerFees = finance.FeesPaid, PlayerSurplusAfterExpenses = finance.OperatingSurplusAfterExpenses,
            NpcFees = state.Accounting.Companies.Values.Where(c => c.OwnerId != Owner).Sum(c => c.FeesPaid),
            TotalLevy = state.Circulation?.Towns.Values.Sum(t => t.TotalLevy) ?? 0,
            TotalRelief = state.Circulation?.Towns.Values.Sum(t => t.TotalRelief) ?? 0,
            TotalWages = state.Ports.Values.Sum(p => p.City!.TotalWagesReceived),
            PaidBread = state.Ports.Values.SelectMany(p => p.Consumption).Where(c => c.Configuration.Kind == ConsumptionKind.Essential).Sum(c => c.TotalConsumedQuantity),
            MissingBread = state.Ports.Values.SelectMany(p => p.Consumption).Where(c => c.Configuration.Kind == ConsumptionKind.Essential).Sum(c => c.TotalUnmetStock),
            UnaffordableBread = state.Ports.Values.SelectMany(p => p.Consumption).Where(c => c.Configuration.Kind == ConsumptionKind.Essential).Sum(c => c.TotalUnmetAffordability),
            Population = state.Ports.Values.Sum(p => p.City!.Population),
            Circulation = state.Circulation == null ? null : new {
                state.Circulation.Configuration.Version, Mode = state.Circulation.Configuration.Mode.ToString(),
                state.Circulation.Configuration.FeesEnabled, state.Circulation.Configuration.MunicipalEnabled,
                Commercial = state.Circulation.Commercial.Values,
                Towns = state.Circulation.Towns.Values.Select(t => new { t.PortId, t.NextReviewDay, t.LastReviewDay,
                    t.TotalLevy, t.TotalRelief, t.LastLevy, t.LastRelief, t.PayrollReserve, t.DuePayroll, t.FoodReserve,
                    LastReason = t.LastReason.ToString() }).ToArray() },
            Companies = state.Companies.Values.Select(c => new { c.Id, c.Cash, Finance = state.Accounting.Companies[c.Id] }).ToArray(),
            Stockpiles = state.CompanyStockpiles.Values.Select(s => new { StoreId = CompanyStockpileSnapshot.StoreId(s.OwnerId, s.PortId), s.OwnerId, s.PortId, s.Capacity, s.Inventory }).ToArray(),
            Ships = state.Ships.Values.Select(s => new { s.Id, s.OwnerId, s.IsPlayer, s.DockedPortId, s.DestinationPortId,
                s.RemainingTravelDays, s.Cargo, s.AutomationEnabled, Stage = s.IndustrialStage.ToString() }).ToArray(),
            Facilities = state.PrivateFacilities.Values.Select(f => new { f.Id, f.PortId, f.Blueprint.BuildingId,
                f.IsOperational, f.Enabled, f.NextProductionDay, f.BatchesProduced, f.WagesPaid,
                f.Blueprint.OutputStockLimit, f.Capacity, f.SharedStockpileId,
                LastOutcome = f.LastOutcome.ToString(), Procurement = f.ProcurementWaitReason.ToString(), Sales = f.SalesWaitReason.ToString() }).ToArray(),
            Towns = state.Ports.Values.Select(p => new { p.Id, p.Role, p.MarketCash, p.City!.HouseholdCash,
                MarketFeesReceived = state.Circulation?.MarketFeesReceived(p.Id) ?? 0,
                HouseholdFeesReceived = state.Circulation?.HouseholdFeesReceived(p.Id) ?? 0,
                p.City.InitialPopulation, p.City.Population, p.City.Prosperity, p.City.LuxuryEnabled,
                p.City.EssentialFulfillmentPercent, p.City.RecoveryFulfillmentPercent, p.City.TotalWagesReceived,
                p.City.TotalRetailSpend, p.Inventory,
                Quotes = p.Quotes.Values.Select(q => new { q.GoodId, q.Pricing!.ReferencePrice, q.Pricing.TargetStock,
                    q.Pricing.MinPrice, q.Pricing.MaxPrice, q.Pricing.Spread,
                    Price = state.GetMarketQuote(p.Id, q.GoodId),
                    NextWholeBuy = Quote(state, p.Id, q.GoodId, TradeDirection.Buy),
                    NextWholeSell = Quote(state, p.Id, q.GoodId, TradeDirection.Sell) }).ToArray(),
                PublicFacilities = p.Facilities.Select(f => new { f.Configuration.Instance.Id, f.Configuration.Instance.RecipeId,
                    f.Enabled, f.NextDueDay, f.Configuration.OutputStockLimit, f.Configuration.WagePerBatch, LastOutcome = f.LastOutcome.ToString() }).ToArray(),
                Needs = p.Consumption.Select(c => new { c.Configuration.Id, c.Configuration.GoodId, c.NextDueDay,
                    c.LastDesiredQuantity, c.LastConsumedQuantity, c.LastPayment, c.TotalDesiredQuantity, c.TotalConsumedQuantity,
                    c.TotalPayment, c.TotalUnmetStock, c.TotalUnmetAffordability, c.DemandRemainder, c.BaselineDemandRemainder }).ToArray() }).ToArray() };
    }

    private static object Quote(RegionalWorldSnapshot state, string port, string good, TradeDirection direction)
    {
        var quote = state.PreviewTrade(Ship, port, new TradeCommand(direction, good, Q(1)));
        return new { Outcome = quote.Outcome.ToString(), quote.TotalAmount, quote.ServiceFee, quote.NetProceeds };
    }

    private static object Difference(SampleRow value, SampleRow baseline) => new {
        PlayerCash = value.PlayerCash - baseline.PlayerCash, NpcCash = value.NpcCash - baseline.NpcCash,
        MarketCash = value.MarketCash - baseline.MarketCash, HouseholdCash = value.HouseholdCash - baseline.HouseholdCash,
        PaidBread = value.PaidBread - baseline.PaidBread, MissingBread = value.MissingBread - baseline.MissingBread,
        UnaffordableBread = value.UnaffordableBread - baseline.UnaffordableBread, Population = value.Population - baseline.Population,
        PlayerSurplusAfterExpenses = value.PlayerSurplusAfterExpenses - baseline.PlayerSurplusAfterExpenses,
        PlayerWages = value.PlayerWages - baseline.PlayerWages, PlayerFees = value.PlayerFees - baseline.PlayerFees,
        NpcFees = value.NpcFees - baseline.NpcFees, TotalWages = value.TotalWages - baseline.TotalWages,
        TotalLevy = value.TotalLevy - baseline.TotalLevy, TotalRelief = value.TotalRelief - baseline.TotalRelief };

    internal sealed class Report
    {
        public int FormatVersion => 1;
        public int QuantityScale => EconomicUnits.QuantityScale;
        public long MoneyScale => EconomicUnits.MoneyScale;
        public string Methodology => "Four seeds, two pricing modes, idle/managed, four fiscal arms: 64 revision 1 worlds + 16 untouched revision 0 pooled/itinerary controls. Merchant charter, Ascension 0, Sandbox, 365 days. All new arms share revision 1 finite initial imports and Coverage floor. Each fiscal arm is compared with revision 1 None, holding buffers and the target floor fixed. None versus historical controls measures the buffer/floor package; Combined versus historical controls measures the full revision effect. The submitted seed shares geography and roles with the read-only fixture, not its Industrial/A1 charter or unknown command history. Fixed managed policy copies the previously documented circulation Farm/Mill/Bakery/Lumber investments, stock/cash controls and physical Flour/Logs shipping. No Mint, external contracts, fabricated inventory/cash, per-seed rescue or outcome-driven tuning. All numeric goods/coins are raw 1/10,000; prices are coins per whole good. Quotes are hypothetical current-state player proposals, not permission to trade remotely. Checkpoints retain actual state only; no terminal fixture is advanced. Runtime is printed only. Positive company reference value and food consumed are not proof of universal or perpetual sustainability.";
        public int[] Seeds => EconomicCirculationExperiments.Seeds;
        public int Horizon => 365;
        public string ManagedPolicy => "At day 0 pay for food-source Farm, processor Mill, recovery Bakery and recovery Lumber Camp. Farm sells Grain; Mill buys Grain (maximum 24 coins) and sells Flour (minimum 6 coins) using existing Mill policy. Recovery Bakery buys Logs (target 3, maximum 18 coins), sells Bread (minimum 1 coin); Camp sells Logs (minimum 1 coin); industry cash reserves 100 coins except unchanged Mill 80. Enable each facility only with company cash >= 120 coins and public output stock below Grain 24 / Flour 12 / Bread 24 / Logs 12 respectively. Sail the initially empty Merchant ship to processor; apply one real Flour/Logs industrial route. Buy up to 4 Flour (minimum 1, maximum 22 coins), physically unload to recovery warehouse; buy up to 8 Logs (minimum 1, maximum 12 coins) there and sell at processor (minimum 1 coin). Route reserve 100 coins, empty returns allowed. No other ships, goods, direct stock assignments or cash interventions. The same unretuned policy can stall; checkpoints report that outcome.";
        public long PrimaryWorldDays => Runs.Sum(r => r.Final.Day) + HistoricalControls.Sum(r => r.Final.Day);
        public List<Row> Runs { get; } = new();
        public List<Row> HistoricalControls { get; } = new();
        public List<object> Comparisons { get; } = new();
        public List<string> Verification { get; } = new();
    }
    internal sealed class Row
    {
        public int Seed { get; set; }
        public string Pricing { get; set; } = "";
        public string Strategy { get; set; } = "";
        public int EconomicRulesVersion { get; set; }
        public string Identity { get; set; } = "";
        public string Arm { get; set; } = "";
        public long Operations { get; set; }
        public int MaximumEvents { get; set; }
        public int MaximumOpenLots { get; set; }
        public string FinalStateFingerprint { get; set; } = "";
        public SampleRow Initial { get; set; } = new();
        public SampleRow Final { get; set; } = new();
        public List<SampleRow> Checkpoints { get; } = new();
        public SortedDictionary<string, long> Attempts { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> Flows { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> Waits { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> AccountTransfers { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> FirstProblems { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> LastPaidFoodDay { get; } = new(StringComparer.Ordinal);
        public List<object> SaveChecks { get; } = new();
    }
    internal sealed class SampleRow
    {
        public long Day { get; set; }
        public long Revision { get; set; }
        public string GlobalCash { get; set; } = "";
        public string RegionalCash { get; set; } = "";
        public long PlayerCash { get; set; }
        public long NpcCash { get; set; }
        public long MarketCash { get; set; }
        public long HouseholdCash { get; set; }
        public long TreasuryCash { get; set; }
        public long ExternalCash { get; set; }
        public long Minted { get; set; }
        public long ExternalNetInflow { get; set; }
        public long PlayerValue { get; set; }
        public long PlayerMargin { get; set; }
        public long PlayerWages { get; set; }
        public long PlayerFees { get; set; }
        public long NpcFees { get; set; }
        public long TotalLevy { get; set; }
        public long TotalRelief { get; set; }
        public long TotalWages { get; set; }
        public long PlayerSurplusAfterExpenses { get; set; }
        public long PaidBread { get; set; }
        public long MissingBread { get; set; }
        public long UnaffordableBread { get; set; }
        public int Population { get; set; }
        public object? Circulation { get; set; }
        public object[] Companies { get; set; } = Array.Empty<object>();
        public object[] Towns { get; set; } = Array.Empty<object>();
        public object[] Stockpiles { get; set; } = Array.Empty<object>();
        public object[] Ships { get; set; } = Array.Empty<object>();
        public object[] Facilities { get; set; } = Array.Empty<object>();
    }
}
