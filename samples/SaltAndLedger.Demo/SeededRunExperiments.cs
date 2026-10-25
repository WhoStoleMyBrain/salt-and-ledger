using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SaltAndLedger.Simulation;

/// <summary>Bounded prescribed strategies over the authoritative world; no optimizer or approximate economy.</summary>
internal static class SeededRunExperiments
{
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Factory = "seeded_bakery";
    private static readonly string[] Strategies = { "idle", "merchant", "local_factory", "industrial" };
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, TypeInfoResolver = HistoricalReportJson.Resolver(), WriteIndented = true };
    private static readonly int[] StandardSeeds =
    {
        int.MinValue, int.MinValue + 1, -1000000007, -104729, -12345, -1024, -42, -7, -1,
        0, 1, 2, 3, 7, 17, 42, 99, 12345, 65535, 65536, 104729, 123456789,
        1000000007, int.MaxValue - 1, int.MaxValue
    };

    internal static int Run(ContentCatalog content, string? path, bool check, bool deep = false)
    {
        var report = BuildReport(content, deep);
        VerifyRepresentativeReplays(content, report);
        var json = Serialize(report);
        path ??= deep ? "docs/experiments/seeded-run-results-deep.json" : "docs/experiments/seeded-run-results.json";
        if (check)
            Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
                "Seeded run report is stale; regenerate with --seeded-runs" + (deep ? " --deep" : "") + ".");
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        Console.WriteLine($"Seeded v{report.GeneratorVersion}: {report.Seeds.Length} seeds plus Classic; {report.Runs.Count} finite trials.");
        foreach (var row in report.Summary)
            Console.WriteLine(FormattableString.Invariant(
                $"{row.WorldMode}/{row.Challenge}/{row.Charter}/A{row.Ascension}/{row.Strategy}: {row.Successes}/{row.Trials} wins; winning days {row.EarliestVictory}-{row.LatestVictory}."));
        Console.WriteLine($"Seed/challenge/charter/ascension combinations without a benchmark win: {report.UnsolvedCombinations.Count}.");
        Console.WriteLine("Per-operation cash, physical sources/transforms/sinks, FIFO basis, terminal freeze and selected-world retry verified.");
        Console.WriteLine("Representative Classic/boundary/-42/99 repeats, reversed configurations and tr-TR reports match byte for byte.");
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    internal static Report BuildReport(ContentCatalog content, bool deep = false, bool reverseConfiguration = false)
    {
        var seeds = StandardSeeds.AsEnumerable();
        if (deep) seeds = seeds.Concat(Enumerable.Range(1, 100).Select(index => unchecked((int)(0x9E3779B9u * (uint)index))));
        var report = new Report { Seeds = seeds.Distinct().OrderBy(seed => seed).ToArray() };
        foreach (var seed in new int?[] { null }.Concat(report.Seeds.Select(value => (int?)value)))
        {
            var mode = seed.HasValue ? RunWorldMode.Seeded : RunWorldMode.Classic;
            var preview = RunScenario.PreviewGeography(new RunIdentity(null, RunScenario.MerchantCharter, mode, seed));
            report.Layouts.Add(new LayoutReport
            {
                WorldMode = mode.ToString(), Seed = seed, GeneratorVersion = preview.GeneratorVersion, Signature = preview.Signature,
                Positions = preview.Positions.Values.Select(position => new PositionReport { Port = position.PortId, X = position.X, Y = position.Y }).ToArray(),
                Routes = preview.Routes.Select(route => new RouteReport { Origin = route.OriginPortId, Destination = route.DestinationPortId,
                    Distance = route.Distance, PlayerTravelDays = route.DurationDays(RunScenario.StartingShipSpeed) }).ToArray()
            });
            foreach (var challenge in RunScenario.Challenges)
                foreach (var charter in RunScenario.Charters)
                    foreach (var difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
                        foreach (var strategy in Strategies)
                            report.Runs.Add(Observe(content, new RunIdentity(challenge.Id, charter.Id, mode, seed, difficulty), strategy, reverseConfiguration));
        }
        report.Summary = report.Runs.GroupBy(row => (row.WorldMode, row.Challenge, row.Charter, row.Ascension, row.Strategy))
            .Select(group => new SummaryReport
            {
                WorldMode = group.Key.WorldMode, Challenge = group.Key.Challenge, Charter = group.Key.Charter,
                Ascension = group.Key.Ascension, Strategy = group.Key.Strategy, Trials = group.Count(),
                Successes = group.Count(row => row.Outcome == "Succeeded"), Failures = group.Count(row => row.Outcome == "Failed"),
                EarliestVictory = group.Where(row => row.Outcome == "Succeeded").Select(row => (long?)row.Day).Min(),
                LatestVictory = group.Where(row => row.Outcome == "Succeeded").Select(row => (long?)row.Day).Max(),
                MinimumCash = group.Min(row => row.Cash), MaximumCash = group.Max(row => row.Cash),
                MinimumMargin = group.Min(row => row.RealizedMaterialMargin), MaximumMargin = group.Max(row => row.RealizedMaterialMargin)
            }).ToList();
        report.UnsolvedCombinations = report.Runs.GroupBy(row => row.ComparisonGroup)
            .Where(group => group.All(row => row.Outcome != "Succeeded")).Select(group => group.Key).ToList();
        Require(report.Runs.Where(row => row.Strategy == "idle").All(row => row.Outcome == "Failed"), "An idle baseline unexpectedly won.");
        Require(report.Runs.Any(row => row.Outcome == "Succeeded") && report.Runs.Any(row => row.Outcome == "Failed"),
            "Experiments must expose both legitimate victory and failure.");
        return report;
    }

    private static RunReport Observe(ContentCatalog content, RunIdentity identity, string strategy, bool reverse)
    {
        var world = RunScenario.Create(content, identity, reverse);
        var initial = world.Snapshot();
        var initialCash = Cash(initial);
        var initialFingerprint = InitialFingerprint(initial);
        var flows = new SortedDictionary<string, GoodFlow>(content.Goods.ToDictionary(good => good.Id,
            good => new GoodFlow { Good = good.Id, Initial = Stock(initial, good.Id) }, StringComparer.Ordinal), StringComparer.Ordinal);
        var firstBatches = new Dictionary<string, long>(StringComparer.Ordinal);
        var report = new RunReport
        {
            WorldMode = identity.WorldMode.ToString(), Seed = identity.Seed, GeneratorVersion = identity.GeneratorVersion,
            Challenge = identity.ChallengeId!, Charter = identity.CharterId, Ascension = identity.Ascension,
            ComparisonGroup = identity.ComparisonGroup, Strategy = strategy, InitialCash = initial.Companies[Owner].Cash
        };

        void Record(RegionalResult result, bool command = false, bool policy = false)
        {
            if (command) report.PlayerCommands++;
            if (policy) report.PolicyChanges++;
            ValidateOperation(result, initialCash);
            foreach (var good in content.Goods)
            {
                var change = result.Production.Sum(batch => batch.Changes.Where(item => item.GoodId == good.Id).Sum(item => (long)item.Delta)) +
                    result.PrivateProduction.Sum(batch => batch.Changes.Where(item => item.GoodId == good.Id).Sum(item => (long)item.Delta));
                var sink = result.Consumption.Where(item => item.GoodId == good.Id).Sum(item => (long)item.ConsumedQuantity);
                Require(Stock(result.After, good.Id) == Stock(result.Before, good.Id) + change - sink, "Per-operation goods differ: " + good.Id);
            }
            foreach (var batch in result.Production.Where(batch => batch.Outcome == ScheduledProductionOutcome.Produced)) RecordRecipe(batch.RecipeId);
            foreach (var batch in result.PrivateProduction)
            {
                if (batch.Outcome == ScheduledProductionOutcome.Produced)
                {
                    RecordRecipe(batch.RecipeId);
                    if (batch.OwnerId == Owner)
                    {
                        report.PrivateBatches++;
                        if (!firstBatches.ContainsKey(batch.FacilityId)) firstBatches.Add(batch.FacilityId, result.After.Day);
                    }
                }
                else if (batch.OwnerId == Owner) Add(report.PrivateProductionOutcomes, batch.Outcome.ToString(), 1);
            }
            foreach (var batch in result.Production.Where(batch => batch.Outcome == ScheduledProductionOutcome.InputShortage)) report.PublicInputShortages++;
            foreach (var demand in result.Consumption)
            {
                flows[demand.GoodId].Consumption += demand.ConsumedQuantity;
                report.UnmetConsumption += demand.UnmetQuantity;
            }
            var playerWait = false;
            foreach (var decision in result.Decisions)
            {
                if (decision.ShipId == Player) report.AutomationDecisions++;
                if (decision.Action != AutomationAction.Wait && decision.Action != AutomationAction.Pause) continue;
                if (decision.ShipId == Player) { playerWait = true; Add(report.PlayerWaitReasons, decision.Reason, 1); }
                else report.NpcWaitDecisions++;
            }
            if (playerWait && result.After.Day > result.Before.Day) report.PlayerBlockedDays++;
            foreach (var movement in result.Movements)
            {
                if (movement.ShipId == Player)
                {
                    if (movement.Kind == ShipMovementKind.Departure && result.After.Ships[Player].CargoUnits > 0) report.LoadedDepartures++;
                    if (movement.Kind == ShipMovementKind.Arrival) report.PlayerArrivals++;
                }
                else if (movement.Kind == ShipMovementKind.Arrival) report.NpcArrivals++;
            }
            foreach (var trade in result.Trades)
            {
                if (trade.Trade.Outcome != TradeOutcome.Traded) { report.RejectedTrades++; continue; }
                if (trade.OwnerId != Owner) report.NpcTrades++;
                else if (trade.Trade.Direction == TradeDirection.Sell && trade.Trade.GoodId == "bread") report.BreadShipSales++;
            }
            report.RejectedTrades += result.WarehouseTrades.Count(trade => trade.Trade.Outcome != TradeOutcome.Traded);
            report.TransferredUnits += result.Transfers.Where(transfer => transfer.OwnerId == Owner).Sum(transfer => transfer.TransferredQuantity);
            if (result.After.Day > result.Before.Day)
                foreach (var facility in result.After.PrivateFacilities.Values.Where(facility => facility.OwnerId == Owner))
                {
                    if (facility.ProcurementWaitReason != IndustryWaitReason.None)
                        Add(report.FactoryWaitReasons, "procurement:" + facility.ProcurementWaitReason, 1);
                    if (facility.SalesWaitReason != IndustryWaitReason.None)
                        Add(report.FactoryWaitReasons, "sales:" + facility.SalesWaitReason, 1);
                }
        }

        void RecordRecipe(string recipeId)
        {
            var recipe = content.GetRecipe(recipeId);
            foreach (var input in recipe.Inputs) flows[input.GoodId].TransformationInputs += input.Quantity;
            foreach (var output in recipe.Outputs)
                if (recipe.Kind == RecipeKind.Extraction) flows[output.GoodId].Extraction += output.Quantity;
                else flows[output.GoodId].TransformationOutputs += output.Quantity;
        }

        if (strategy != "idle")
        {
            foreach (var cargo in initial.Ships[Player].Cargo.Where(item => item.Value > 0))
                Record(world.ExecuteTrade(Player, initial.Ships[Player].DockedPortId!,
                    new TradeCommand(TradeDirection.Sell, cargo.Key, cargo.Value)), true);
            if (strategy == "merchant")
            {
                SailTo(EnterpriseScenario.North);
                Record(world.SetAutomationEnabled(Player, true), true, true);
            }
            else
            {
                var facilityId = initial.PrivateFacilities.ContainsKey(RunScenario.StarterBakery) ? RunScenario.StarterBakery : Factory;
                if (facilityId == Factory)
                    Record(world.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, facilityId), true);
                Record(world.ConfigureFacility(Owner, facilityId, EnterpriseScenario.BakeryPolicy(strategy == "local_factory")), true, true);
                if (strategy == "industrial")
                {
                    SailTo(EnterpriseScenario.West);
                    Record(world.ConfigureIndustrialAutomation(Player, RunScenario.BakeryShippingPolicy(facilityId), true), true, true);
                }
            }
        }
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) Record(world.AdvanceDay());

        void SailTo(string destination)
        {
            if (world.Snapshot().Ships[Player].DockedPortId == destination) return;
            Record(world.Depart(Player, destination), true);
            while (world.Snapshot().Ships[Player].DockedPortId == null) Record(world.AdvanceDay());
        }

        var final = world.Snapshot();
        var results = new RunResults(final);
        var finance = results.Finance;
        report.Outcome = results.Challenge.Status.ToString(); report.Day = final.Day; report.Cash = results.Cash; report.Score = results.TotalScore;
        report.Objective = results.Challenge.ObjectiveQuantity; report.NetBreadToEast = results.Challenge.NetDeliveredQuantity;
        report.RegionalBreadConsumed = results.Challenge.RegionalConsumedQuantity;
        report.Purchases = finance.InventoryPurchases; report.SalesRevenue = finance.SalesRevenue; report.ReleasedBasis = finance.CostOfGoodsSold;
        report.HeldBasis = finance.HeldInventoryCost; report.RealizedMaterialMargin = finance.RealizedGrossMargin;
        report.ConstructionInvestment = finance.ConstructionExpenditure; report.HeldGoods = finance.HeldInventoryQuantities;
        report.PrivateOutput = finance.ProducedQuantities;
        report.Facilities = final.PrivateFacilities.Values.Where(facility => facility.OwnerId == Owner).Select(facility => new FacilityReport
        {
            Id = facility.Id, Port = facility.PortId, StarterGrant = facility.IsStarterGrant, OrderedDay = facility.OrderedDay,
            ScheduledCompletionDay = facility.ConstructionCompletionDay, ActualCompletionDay = facility.ConstructionCompletedDay,
            FirstEligibleProductionDay = facility.FirstProductionDay,
            FirstActualBatchDay = firstBatches.TryGetValue(facility.Id, out var first) ? first : null,
            Batches = facility.BatchesProduced, MaterialPurchases = facility.ProcurementCost, LocalSalesRevenue = facility.SalesRevenue,
            Status = facility.ConstructionStatus.ToString()
        }).ToArray();
        foreach (var flow in flows.Values)
        {
            flow.Final = Stock(final, flow.Good);
            Require(flow.Final == flow.Initial + flow.Extraction + flow.TransformationOutputs - flow.TransformationInputs - flow.Consumption,
                "Cumulative source/sink reconciliation failed: " + flow.Good);
        }
        report.Flows = flows.Values.ToArray();
        report.Bottleneck = Explain(report);
        Require(initialFingerprint == InitialFingerprint(RunScenario.Create(content, identity, !reverse).Snapshot()), "Retry changed the selected initial world.");
        try { world.AdvanceDay(); throw new Exception("Terminal world advanced."); } catch (InvalidOperationException) { }
        Require(ReferenceEquals(final, world.Snapshot()), "Terminal rejection changed snapshot identity.");
        return report;
    }

    private static void ValidateOperation(RegionalResult result, BigInteger initialCash)
    {
        Require(Cash(result.After) == initialCash, "Combined market/company cash changed.");
        foreach (var company in result.After.Companies.Values)
        {
            var finance = result.After.Accounting.Companies[company.Id];
            var before = result.Before.Accounting.Companies[company.Id];
            var events = result.EconomicEvents.Where(item => item.CompanyId == company.Id).ToArray();
            Require(company.Cash >= 0 && company.Cash == finance.InitialCash + finance.SalesRevenue - finance.InventoryPurchases - finance.ConstructionExpenditure,
                "Company cash does not reconcile.");
            Require(finance.InventoryPurchases == finance.CostOfGoodsSold + finance.HeldInventoryCost, "FIFO basis does not reconcile.");
            Require(company.Cash - result.Before.Companies[company.Id].Cash == events.Sum(item => item.CompanyCashDelta), "Event cash does not reconcile.");
            Require(finance.InventoryPurchases - before.InventoryPurchases == events.Where(item => item.Kind == EconomicEventKind.Purchase).Sum(item => item.CashTotal), "Purchase events differ.");
            Require(finance.SalesRevenue - before.SalesRevenue == events.Where(item => item.Kind == EconomicEventKind.Sale).Sum(item => item.CashTotal), "Sale events differ.");
            Require(finance.CostOfGoodsSold - before.CostOfGoodsSold == events.Where(item => item.Kind == EconomicEventKind.Sale).Sum(item => item.AllocatedCost), "Released cost events differ.");
            Require(finance.ConstructionExpenditure - before.ConstructionExpenditure == events.Where(item => item.Kind == EconomicEventKind.Construction).Sum(item => item.CashTotal), "Construction events differ.");
        }
        Require(result.After.Ports.Values.All(port => port.MarketCash >= 0 && port.Inventory.Values.All(quantity => quantity >= 0)) &&
            result.After.Ships.Values.All(ship => ship.CargoUnits <= ship.CargoCapacity && ship.Cargo.Values.All(quantity => quantity >= 0)) &&
            result.After.PrivateFacilities.Values.All(facility => facility.StoredUnits <= facility.Capacity && facility.Inventory.Values.All(quantity => quantity >= 0)),
            "Negative or over-capacity physical state.");
        foreach (var trade in result.Trades.Select(item => item.Trade).Concat(result.WarehouseTrades.Select(item => item.Trade)))
            Require(trade.MarketCashChange.Delta + trade.TraderCashChange.Delta == 0 &&
                trade.MarketInventoryChange.Delta + trade.TraderInventoryChange.Delta == 0, "Trade failed conservation.");
        foreach (var movement in result.Movements.Where(item => item.Kind == ShipMovementKind.Departure))
            Require(result.After.Ships[movement.ShipId].VoyageDurationDays == result.After.GetRoute(movement.OriginPortId, movement.DestinationPortId)
                .DurationDays(result.After.Ships[movement.ShipId].Configuration.Speed), "Departure did not use its authoritative route.");
    }

    private static void VerifyRepresentativeReplays(ContentCatalog content, Report report)
    {
        var selected = report.Runs.Where(row => row.Seed == null || row.Seed == int.MinValue || row.Seed == int.MaxValue || row.Seed == -42 || row.Seed == 99).ToArray();
        foreach (var row in selected)
        {
            var identity = new RunIdentity(row.Challenge, row.Charter, row.Seed.HasValue ? RunWorldMode.Seeded : RunWorldMode.Classic,
                row.Seed, (RunDifficulty)row.Ascension);
            Require(Serialize(row) == Serialize(Observe(content, identity, row.Strategy, false)), "Repeated seeded run differs.");
            Require(Serialize(row) == Serialize(Observe(content, identity, row.Strategy, true)), "Reversed seeded configuration differs.");
        }
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            foreach (var row in selected)
                Require(Serialize(row) == Serialize(Observe(content, new RunIdentity(row.Challenge, row.Charter,
                    row.Seed.HasValue ? RunWorldMode.Seeded : RunWorldMode.Classic, row.Seed, (RunDifficulty)row.Ascension), row.Strategy, true)),
                    "Seeded report depends on culture.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private static string Explain(RunReport row)
    {
        if (row.Strategy == "idle") return "No player supply, industry policy or shipping; objective remains unmet at the fixed deadline.";
        if (row.Challenge == RunScenario.AmbitionChallenge && row.Strategy == "merchant")
            return "Trading earns margin but performs no private production; Enterprise Ambition requires both.";
        if (row.Strategy == "local_factory" && row.Challenge == RunScenario.CoastalChallenge)
            return "Westhaven local sales do not deliver Bread to Eastport; physical export shipping is absent.";
        if (row.Strategy == "local_factory" && row.Challenge == RunScenario.ReliefChallenge && row.Outcome != "Succeeded")
            return "Westhaven demand consumes at most 20 Bread in 60 days; supplying another town requires shipping.";
        if (row.Outcome != "Succeeded") return "Fixed deadline reached with insufficient objective or cash; inspect waits, demand, geography and held inventory.";
        return row.Strategy == "industrial" ? "Victory uses paid/granted private inputs and physical shipping; compare construction dates, voyage time and held basis." :
            row.Strategy == "local_factory" ? "Local material margin and output suffice; construction capital remains separate from gross material margin." :
            "Default Bread trading succeeds; this narrow modifier does not penalize a merchant who constructs nothing.";
    }

    private static BigInteger Cash(RegionalWorldSnapshot snapshot) => snapshot.Companies.Values.Aggregate(BigInteger.Zero, (sum, company) => sum + company.Cash) +
        snapshot.Ports.Values.Aggregate(BigInteger.Zero, (sum, port) => sum + port.MarketCash);
    private static long Stock(RegionalWorldSnapshot snapshot, string good) => snapshot.Ports.Values.Sum(port => (long)port.Inventory[good]) +
        snapshot.Ships.Values.Sum(ship => (long)ship.Cargo[good]) + snapshot.PrivateFacilities.Values.Sum(facility => (long)facility.Inventory[good]);
    private static string InitialFingerprint(RegionalWorldSnapshot snapshot) => Serialize(new
    {
        snapshot.RunIdentity, snapshot.Difficulty, snapshot.Day, snapshot.Revision, snapshot.Accounting, snapshot.Companies,
        snapshot.PrivateFacilities, snapshot.IndustryBlueprints, snapshot.Challenge, snapshot.Ships, snapshot.Routes,
        Ports = snapshot.Ports.Values.Select(port => new { port.Id, port.X, port.Y, port.Inventory, port.MarketCash, port.Facilities, port.Consumption })
    });
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n") + "\n";
    private static void Add(SortedDictionary<string, long> values, string key, long amount) => values[key] = checked(values.TryGetValue(key, out var current) ? current + amount : amount);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal sealed class Report
    {
        public int FormatVersion { get; } = 1;
        public int GeneratorVersion { get; } = SeededArchipelago.CurrentVersion;
        public string Methodology { get; } = "25 stable signed seeds (125 in deep mode), Classic control, three challenges, three charters, both ascensions and four prescribed strategies. No economy tuning, optimization or hidden rescue. All runs use the actual atomic world. Manual command counts exclude AdvanceDay; policy submissions are counted separately. Gross material margin excludes wages, transport operating costs and depreciation; held basis is not liquidation value.";
        public int[] Seeds { get; set; } = Array.Empty<int>();
        public List<LayoutReport> Layouts { get; } = new();
        public List<SummaryReport> Summary { get; set; } = new();
        public List<string> UnsolvedCombinations { get; set; } = new();
        public List<RunReport> Runs { get; } = new();
    }
    internal sealed class LayoutReport
    {
        public string WorldMode { get; set; } = "";
        public int? Seed { get; set; }
        public int GeneratorVersion { get; set; }
        public string Signature { get; set; } = "";
        public PositionReport[] Positions { get; set; } = Array.Empty<PositionReport>();
        public RouteReport[] Routes { get; set; } = Array.Empty<RouteReport>();
    }
    internal sealed class PositionReport { public string Port { get; set; } = ""; public int X { get; set; } public int Y { get; set; } }
    internal sealed class RouteReport
    {
        public string Origin { get; set; } = ""; public string Destination { get; set; } = "";
        public int Distance { get; set; } public int PlayerTravelDays { get; set; }
    }
    internal sealed class SummaryReport
    {
        public string WorldMode { get; set; } = ""; public string Challenge { get; set; } = "";
        public string Charter { get; set; } = ""; public int Ascension { get; set; } public string Strategy { get; set; } = "";
        public int Trials { get; set; } public int Successes { get; set; } public int Failures { get; set; }
        public long? EarliestVictory { get; set; } public long? LatestVictory { get; set; }
        public long MinimumCash { get; set; } public long MaximumCash { get; set; }
        public long MinimumMargin { get; set; } public long MaximumMargin { get; set; }
    }
    internal sealed class GoodFlow
    {
        public string Good { get; set; } = "";
        public long Initial { get; set; } public long Extraction { get; set; }
        public long TransformationInputs { get; set; } public long TransformationOutputs { get; set; }
        public long Consumption { get; set; } public long Final { get; set; }
    }
    internal sealed class FacilityReport
    {
        public string Id { get; set; } = ""; public string Port { get; set; } = ""; public string Status { get; set; } = "";
        public bool StarterGrant { get; set; } public long OrderedDay { get; set; }
        public long ScheduledCompletionDay { get; set; } public long? ActualCompletionDay { get; set; }
        public long FirstEligibleProductionDay { get; set; } public long? FirstActualBatchDay { get; set; }
        public long Batches { get; set; } public long MaterialPurchases { get; set; } public long LocalSalesRevenue { get; set; }
    }
    internal sealed class RunReport
    {
        public string WorldMode { get; set; } = ""; public int? Seed { get; set; } public int GeneratorVersion { get; set; }
        public string Challenge { get; set; } = ""; public string Charter { get; set; } = ""; public int Ascension { get; set; }
        public string ComparisonGroup { get; set; } = ""; public string Strategy { get; set; } = "";
        public string Outcome { get; set; } = ""; public long Day { get; set; } public long InitialCash { get; set; }
        public long Cash { get; set; } public int Score { get; set; } public long Objective { get; set; }
        public long NetBreadToEast { get; set; } public long RegionalBreadConsumed { get; set; }
        public long Purchases { get; set; } public long SalesRevenue { get; set; } public long ReleasedBasis { get; set; }
        public long HeldBasis { get; set; } public long RealizedMaterialMargin { get; set; } public long ConstructionInvestment { get; set; }
        public object? HeldGoods { get; set; } public object? PrivateOutput { get; set; }
        public int PlayerCommands { get; set; } public int PolicyChanges { get; set; } public int AutomationDecisions { get; set; }
        public int PlayerBlockedDays { get; set; } public int LoadedDepartures { get; set; } public int PlayerArrivals { get; set; }
        public int BreadShipSales { get; set; } public long TransferredUnits { get; set; } public int RejectedTrades { get; set; }
        public int NpcTrades { get; set; } public int NpcArrivals { get; set; } public int NpcWaitDecisions { get; set; }
        public int PrivateBatches { get; set; } public int PublicInputShortages { get; set; } public long UnmetConsumption { get; set; }
        public SortedDictionary<string, long> PlayerWaitReasons { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> FactoryWaitReasons { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PrivateProductionOutcomes { get; } = new(StringComparer.Ordinal);
        public FacilityReport[] Facilities { get; set; } = Array.Empty<FacilityReport>();
        public GoodFlow[] Flows { get; set; } = Array.Empty<GoodFlow>();
        public string Bottleneck { get; set; } = "";
    }
}
