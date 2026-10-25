using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SaltAndLedger.Simulation;

/// <summary>Complete runs driven solely through ordinary public commands and authored policies.</summary>
internal static class RunExperiments
{
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, TypeInfoResolver = HistoricalReportJson.Resolver(), WriteIndented = true };

    internal static int Run(ContentCatalog content, string? path, bool check)
    {
        var report = Simulate(content);
        var json = Serialize(report);
        Require(json == Serialize(Simulate(content)), "Complete runs differ on repetition.");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Require(json == Serialize(Simulate(content)), "Complete runs depend on culture.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        path ??= "docs/experiments/run-results.json";
        if (check) Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
            "Complete-run report is stale; regenerate with --runs.");
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        foreach (var run in report)
            Console.WriteLine($"{run.Challenge}/{run.Charter}/{run.Strategy}: {run.Status} day {run.Day}; cash {run.Cash}; margin {run.Margin}; score {run.Score}.");
        Console.WriteLine("Repeat, tr-TR, selected-charter retry and per-operation financial reconciliation passed.");
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    private static string Serialize(object report) => JsonSerializer.Serialize(report, Options).Replace("\r\n", "\n") + "\n";
    private static List<RunReport> Simulate(ContentCatalog content)
    {
        var reports = new List<RunReport>();
        foreach (var challenge in RunScenario.Challenges)
            foreach (var charter in RunScenario.Charters)
            {
                var strategy = charter.Id == RunScenario.IndustrialCharter || challenge.Id == RunScenario.AmbitionChallenge
                    ? "industrial" : "merchant";
                reports.Add(Observe(content, challenge.Id, charter.Id, strategy));
                reports.Add(Observe(content, challenge.Id, charter.Id, "idle"));
            }
        Require(reports.Where(r => r.Strategy == "idle").All(r => r.Status == "Failed"), "Idle behavior must not win.");
        Require(reports.Where(r => r.Strategy != "idle").All(r => r.Status == "Succeeded"),
            "Each authored package/challenge pairing needs a demonstrated viable strategy.");
        return reports;
    }

    private static RunReport Observe(ContentCatalog content, string challengeId, string charterId, string strategy)
    {
        var world = RunScenario.Create(content, challengeId, charterId);
        var initial = world.Snapshot();
        var initialJson = Serialize(InitialState(initial));
        var report = new RunReport { Challenge = challengeId, Charter = charterId, Strategy = strategy,
            InitialCash = initial.Companies[Owner].Cash, InitialCargoCapacity = initial.Ships[Player].CargoCapacity,
            InitialPort = initial.Ships[Player].DockedPortId!, Endowment = RunScenario.GetCharter(charterId).Endowment };
        long eventCash = 0, purchases = 0, revenue = 0, released = 0, construction = 0;
        void Record(RegionalResult result, bool manual = false)
        {
            if (manual) report.PlayerCommands++;
            var decisions = result.Decisions.Where(d => d.ShipId == Player).ToArray();
            report.AutomationDecisions += decisions.Length;
            if (result.After.Day != result.Before.Day && decisions.Any(d => d.Action == AutomationAction.Wait || d.Action == AutomationAction.Pause))
                report.BlockedDays++;
            foreach (var item in result.EconomicEvents.Where(e => e.CompanyId == Owner))
            {
                eventCash = checked(eventCash + item.CompanyCashDelta);
                if (item.Kind == EconomicEventKind.Purchase) purchases = checked(purchases + item.CashTotal);
                if (item.Kind == EconomicEventKind.Sale) { revenue = checked(revenue + item.CashTotal); released = checked(released + item.AllocatedCost); }
                if (item.Kind == EconomicEventKind.Construction) construction = checked(construction + item.CashTotal);
                report.Journal.Add(new { item.Day, item.Revision, Type = item.Kind.ToString(), item.SourceStoreId,
                    item.DestinationStoreId, item.PortId, item.GoodId, item.Quantity, item.CompanyCashDelta, item.AllocatedCost });
            }
            var finance = result.After.Accounting.Companies[Owner];
            Require(result.After.Companies[Owner].Cash == checked(report.InitialCash + eventCash), "Event cash reconciliation failed.");
            Require(finance.InventoryPurchases == purchases && finance.SalesRevenue == revenue &&
                finance.CostOfGoodsSold == released && finance.ConstructionExpenditure == construction,
                "Event financial aggregates differ.");
            Require(purchases == checked(released + finance.HeldInventoryCost), "Held/released basis does not reconcile.");
        }
        if (strategy != "idle")
        {
            foreach (var cargo in initial.Ships[Player].Cargo.Where(p => p.Value > 0))
                Record(world.ExecuteTrade(Player, initial.Ships[Player].DockedPortId!,
                    new TradeCommand(TradeDirection.Sell, cargo.Key, cargo.Value)), true);
            if (strategy == "industrial")
            {
                var facilityId = RunScenario.StarterBakery;
                if (!world.Snapshot().PrivateFacilities.ContainsKey(facilityId))
                {
                    facilityId = "run_bakery";
                    Record(world.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, facilityId), true);
                }
                Record(world.ConfigureFacility(Owner, facilityId, EnterpriseScenario.BakeryPolicy()), true);
                if (world.Snapshot().Ships[Player].DockedPortId != EnterpriseScenario.West)
                {
                    Record(world.Depart(Player, EnterpriseScenario.West), true);
                    while (world.Snapshot().Ships[Player].DockedPortId == null) Record(world.AdvanceDay());
                }
                Record(world.ConfigureIndustrialAutomation(Player, RunScenario.BakeryShippingPolicy(facilityId), true), true);
            }
            else Record(world.SetAutomationEnabled(Player, true), true);
        }
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) Record(world.AdvanceDay());
        var final = world.Snapshot();
        var result = new RunResults(final, charterId);
        var financial = result.Finance;
        report.Status = result.Challenge.Status.ToString(); report.Day = final.Day; report.Cash = result.Cash;
        report.ObjectiveQuantity = result.Challenge.ObjectiveQuantity;
        report.NetBreadToEast = result.Challenge.NetDeliveredQuantity;
        report.RegionalBreadConsumed = result.Challenge.RegionalConsumedQuantity;
        report.Purchases = financial.InventoryPurchases; report.Revenue = financial.SalesRevenue;
        report.CostOfGoodsSold = financial.CostOfGoodsSold; report.HeldBasis = financial.HeldInventoryCost;
        report.Margin = financial.RealizedGrossMargin; report.Construction = financial.ConstructionExpenditure;
        report.HeldGoods = financial.HeldInventoryQuantities; report.ProducedGoods = financial.ProducedQuantities;
        report.Facilities = final.PrivateFacilities.Values.Where(f => f.OwnerId == Owner)
            .Select(f => new { f.Id, f.PortId, BlueprintId = f.Blueprint.Id, f.IsStarterGrant, f.Provenance }).ToArray();
        report.Score = result.TotalScore;
        report.ScoreComponents = new { result.VictoryScore, result.ObjectiveScore, result.TimeScore, result.SolvencyScore, result.ComparisonGroup };
        Require(initialJson == Serialize(InitialState(RunScenario.Create(content, challengeId, charterId).Snapshot())),
            "Retry did not restore the selected charter.");
        try { world.AdvanceDay(); throw new Exception("Terminal world advanced."); }
        catch (InvalidOperationException) { }
        return report;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static object InitialState(RegionalWorldSnapshot state) => new
    {
        state.Day, state.Revision, state.Companies, state.PrivateFacilities, state.Accounting, state.Challenge,
        Ships = state.Ships.Values.Select(s => new { s.Id, s.OwnerId, s.Cash, s.CargoCapacity, s.Cargo, s.DockedPortId, s.AutomationEnabled }),
        Ports = state.Ports.Values.Select(p => new { p.Id, p.MarketCash, p.Inventory })
    };
    private sealed class RunReport
    {
        public string Challenge { get; set; } = "";
        public string Charter { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Endowment { get; set; } = "";
        public long InitialCash { get; set; }
        public int InitialCargoCapacity { get; set; }
        public string InitialPort { get; set; } = "";
        public string Status { get; set; } = "";
        public long Day { get; set; }
        public long Cash { get; set; }
        public long ObjectiveQuantity { get; set; }
        public long NetBreadToEast { get; set; }
        public long RegionalBreadConsumed { get; set; }
        public long Purchases { get; set; }
        public long Revenue { get; set; }
        public long CostOfGoodsSold { get; set; }
        public long HeldBasis { get; set; }
        public long Margin { get; set; }
        public long Construction { get; set; }
        public object? HeldGoods { get; set; }
        public object? ProducedGoods { get; set; }
        public object? Facilities { get; set; }
        public int PlayerCommands { get; set; }
        public int AutomationDecisions { get; set; }
        public int BlockedDays { get; set; }
        public int Score { get; set; }
        public object? ScoreComponents { get; set; }
        public List<object> Journal { get; } = new();
    }
}
