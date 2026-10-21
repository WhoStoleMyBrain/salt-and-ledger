using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SaltAndLedger.Simulation;

/// <summary>Bounded host scripts exercising real enterprise commands, never an alternative economy.</summary>
internal static class EnterpriseExperiments
{
    private const int Days = 90;
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Factory = "experiment_factory";
    private const string PathDefault = "docs/experiments/enterprise-results.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
    };

    internal static int Run(ContentCatalog content, string? path, bool check)
    {
        var report = Simulate(content, false);
        var json = Serialize(report);
        Require(json == Serialize(Simulate(content, false)), "Repeated enterprise results differ.");
        Require(json == Serialize(Simulate(content, true)), "Enterprise configuration order changes results.");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Require(json == Serialize(Simulate(content, false)), "Enterprise results depend on culture.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        path ??= PathDefault;
        if (check)
            Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
                "Enterprise report is stale; regenerate with --enterprise " + path);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        Console.WriteLine("Ninety-day enterprises: finite markets; actual capital, purchases and revenue; held goods are not valued.");
        Console.WriteLine("Strategy        Cash  Capital  Purchases  Revenue  Private batches  Bread to East");
        foreach (var s in report.Sandbox)
            Console.WriteLine(FormattableString.Invariant(
                $"{s.Strategy,-14} {s.FinalCash,5} {s.ConstructionExpenditure,8} {s.PurchaseCost,10} {s.SalesRevenue,8} {s.PrivateBatches,16} {s.NetBreadDeliveredToEast,14}"));
        foreach (var s in report.Challenges)
            Console.WriteLine(FormattableString.Invariant(
                $"Charter {s.Strategy}: {s.ChallengeStatus} day {s.FinalDay}; net bread {s.NetBreadDeliveredToEast}; cash {s.FinalCash}."));
        Console.WriteLine("Per-commit cash and six-good physical reconciliation, repeat, reversed setup and tr-TR matched.");
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    private static string Serialize(Report report) =>
        JsonSerializer.Serialize(report, Options).Replace("\r\n", "\n") + "\n";

    private static Report Simulate(ContentCatalog content, bool reverse)
    {
        var report = new Report { Configuration = Configuration(EnterpriseScenario.Create(content, reverseConfiguration: reverse).Snapshot()) };
        foreach (var strategy in new[] { "idle", "merchant", "workshop", "food", "hybrid" })
            report.Sandbox.Add(Observe(content, strategy, false, reverse));
        foreach (var strategy in new[] { "idle", "merchant", "hybrid" })
            report.Challenges.Add(Observe(content, strategy, true, reverse));
        Require(report.Challenges[0].ChallengeStatus == "Failed", "Waiting must fail the charter.");
        Require(report.Challenges.Skip(1).All(s => s.ChallengeStatus == "Succeeded"),
            "Merchant and industrial/hybrid charter solutions must both succeed.");
        Require(report.Sandbox[0].NpcActors.All(a => a.Deliveries > 0),
            "Every authored NPC route must complete physical deliveries.");
        Require(report.Sandbox[0].PublicBatches.TryGetValue("bake_bread", out var bread) && bread > 2 &&
            report.Sandbox[0].PublicBatches.TryGetValue("mill_flour", out var flour) && flour > 2 &&
            report.Sandbox[0].PublicBatches.TryGetValue("make_furniture", out var furniture) && furniture > 2,
            "The public multi-stage economy must operate without player intervention.");
        Require(report.Sandbox.Where(s => s.Strategy == "workshop" || s.Strategy == "food" || s.Strategy == "hybrid")
            .All(s => s.PrivateBatches >= 5), "Private production must operate repeatedly after one configuration.");
        return report;
    }

    private static StrategyReport Observe(ContentCatalog content, string strategy, bool challenge, bool reverse)
    {
        var world = EnterpriseScenario.Create(content, challenge, reverse);
        var initial = world.Snapshot();
        var report = new StrategyReport { Strategy = strategy, InitialState = State(initial) };
        var initialCash = Cash(initial);
        var produced = Goods(content);
        var consumed = Goods(content);
        var demand = new SortedDictionary<string, DemandTotals>(StringComparer.Ordinal);
        var npcs = initial.Ships.Values.Where(s => !s.IsPlayer).ToDictionary(s => s.Id,
            s => new NpcTotals { ShipId = s.Id }, StringComparer.Ordinal);

        void Record(RegionalResult result)
        {
            Require(Cash(result.After) == initialCash, "Company/market cash does not reconcile.");
            foreach (var good in content.Goods)
            {
                var delta = result.Production.Sum(p => p.Changes.Where(c => c.GoodId == good.Id).Sum(c => (long)c.Delta)) +
                    result.PrivateProduction.Sum(p => p.Changes.Where(c => c.GoodId == good.Id).Sum(c => (long)c.Delta));
                var sink = result.Consumption.Where(c => c.GoodId == good.Id).Sum(c => (long)c.ConsumedQuantity);
                Require(Stock(result.After, good.Id) == Stock(result.Before, good.Id) + delta - sink,
                    "Enterprise physical flows do not reconcile: " + good.Id);
            }
            foreach (var construction in result.Construction.Where(c => c.Outcome == ConstructionOutcome.Built))
                report.ConstructionExpenditure = checked(report.ConstructionExpenditure + construction.Cost);
            foreach (var t in result.Trades)
            {
                RecordTrade(t.OwnerId, t.PortId, t.Trade);
                if (t.OwnerId != Owner && t.Trade.Outcome == TradeOutcome.Traded)
                {
                    var actor = npcs[t.ShipId];
                    if (t.Trade.Direction == TradeDirection.Sell)
                    {
                        actor.Deliveries++;
                        actor.UnitsDelivered += t.Trade.ExecutedQuantity;
                        actor.Revenue += t.Trade.ExecutedAmount;
                    }
                    else actor.PurchaseCost += t.Trade.ExecutedAmount;
                }
            }
            foreach (var t in result.WarehouseTrades) RecordTrade(t.OwnerId, t.PortId, t.Trade);
            foreach (var p in result.Production)
            {
                if (p.Outcome == ScheduledProductionOutcome.Produced)
                {
                    Add(report.PublicBatches, p.RecipeId, 1);
                    RecordRecipe(p.RecipeId, report.PublicExtraction, report.PublicInputs, report.PublicOutputs);
                }
                if (p.Outcome == ScheduledProductionOutcome.InputShortage)
                    Add(report.ProductionShortages, "public:" + p.PortId + ":" + p.FacilityId, 1);
            }
            foreach (var p in result.PrivateProduction)
            {
                if (p.Outcome == ScheduledProductionOutcome.Produced)
                {
                    report.PrivateBatches++;
                    RecordRecipe(p.RecipeId, report.PrivateExtraction, report.PrivateInputs, report.PrivateOutputs);
                }
                if (p.Outcome == ScheduledProductionOutcome.InputShortage)
                    Add(report.ProductionShortages, "private:" + p.PortId + ":" + p.FacilityId, 1);
            }
            foreach (var c in result.Consumption)
            {
                var key = c.PortId + ":" + c.GoodId;
                if (!demand.TryGetValue(key, out var total))
                    demand.Add(key, total = new DemandTotals { PortId = c.PortId, GoodId = c.GoodId });
                total.Met += c.ConsumedQuantity;
                total.Unmet += c.UnmetQuantity;
                consumed[c.GoodId] += c.ConsumedQuantity;
            }
            foreach (var m in result.Movements)
                if (m.Kind == ShipMovementKind.Arrival && npcs.TryGetValue(m.ShipId, out var actor)) actor.Arrivals++;
            foreach (var d in result.Decisions)
                if ((d.Action == AutomationAction.Wait || d.Action == AutomationAction.Pause) &&
                    npcs.TryGetValue(d.ShipId, out var actor)) Add(actor.WaitReasons, d.Reason, 1);
            report.Operations++;
            Require(result.After.Companies[Owner].Cash == EnterpriseScenario.InitialPlayerCash -
                report.ConstructionExpenditure - report.PurchaseCost + report.SalesRevenue,
                "Player account does not equal actual capital and market cash flows.");
        }

        void RecordRecipe(string recipeId, SortedDictionary<string, long> extraction,
            SortedDictionary<string, long> inputs, SortedDictionary<string, long> outputs)
        {
            var recipe = content.GetRecipe(recipeId);
            foreach (var input in recipe.Inputs)
            {
                Add(inputs, input.GoodId, input.Quantity);
                produced[input.GoodId] -= input.Quantity;
            }
            foreach (var output in recipe.Outputs)
            {
                Add(recipe.Kind == RecipeKind.Extraction ? extraction : outputs, output.GoodId, output.Quantity);
                produced[output.GoodId] += output.Quantity;
            }
        }

        void RecordTrade(string owner, string port, TradeAttempt trade)
        {
            Require(trade.MarketCashChange.Delta + trade.TraderCashChange.Delta == 0,
                "Market transaction created cash.");
            Require(trade.MarketInventoryChange.Delta + trade.TraderInventoryChange.Delta == 0,
                "Market transaction created goods.");
            if (owner != Owner || trade.Outcome != TradeOutcome.Traded) return;
            if (trade.Direction == TradeDirection.Buy)
            {
                report.PurchaseCost += trade.ExecutedAmount;
                Add(report.GoodsAcquired, trade.GoodId, trade.ExecutedQuantity);
            }
            else
            {
                report.SalesRevenue += trade.ExecutedAmount;
                Add(report.GoodsSold, trade.GoodId, trade.ExecutedQuantity);
            }
            if (port == EnterpriseScenario.East && trade.GoodId == "bread")
                report.NetBreadDeliveredToEast += trade.Direction == TradeDirection.Sell ?
                    trade.ExecutedQuantity : -trade.ExecutedQuantity;
            report.PlayerTrades.Add(new
            {
                Day = world.Snapshot().Day, PortId = port, Direction = trade.Direction.ToString(),
                trade.GoodId, trade.ExecutedQuantity, trade.ExecutedAmount,
                trade.Price!.FirstMarginalPrice, trade.Price.LastMarginalPrice
            });
        }

        if (strategy == "merchant") Record(world.SetAutomationEnabled(Player, true));
        if (strategy == "workshop" || strategy == "food" || strategy == "hybrid")
        {
            var workshop = strategy == "workshop";
            Record(world.ConstructFacility(Owner, workshop ? EnterpriseScenario.South : EnterpriseScenario.West,
                workshop ? EnterpriseScenario.WorkshopBlueprint : EnterpriseScenario.BakeryBlueprint, Factory));
            Record(world.ConfigureFacility(Owner, Factory,
                workshop ? EnterpriseScenario.WorkshopPolicy() : EnterpriseScenario.BakeryPolicy(strategy == "food")));
        }
        report.Daily.Add(State(world.Snapshot()));
        var limit = challenge ? EnterpriseScenario.CharterDeadline : Days;
        for (var day = 0; day < limit; day++)
        {
            if (world.Snapshot().Challenge?.Status != null &&
                world.Snapshot().Challenge!.Status != ChallengeStatus.Active) break;
            if (strategy == "hybrid") HybridOrders(world, Record);
            if (world.Snapshot().Challenge != null && world.Snapshot().Challenge!.Status != ChallengeStatus.Active) break;
            Record(world.AdvanceDay());
            report.Daily.Add(State(world.Snapshot()));
        }
        var final = world.Snapshot();
        Require(final.Challenge == null || report.NetBreadDeliveredToEast == final.Challenge.NetDeliveredQuantity,
            "Report delivery totals diverge from authoritative challenge progress.");
        foreach (var good in content.Goods)
        {
            Require(Stock(final, good.Id) == Stock(initial, good.Id) + produced[good.Id] - consumed[good.Id],
                "Cumulative enterprise physical flow mismatch: " + good.Id);
            report.PhysicalReconciliation.Add(new
            {
                GoodId = good.Id, Initial = Stock(initial, good.Id),
                ProductionNet = produced[good.Id], Consumed = consumed[good.Id], Final = Stock(final, good.Id)
            });
        }
        report.FinalCash = final.Companies[Owner].Cash;
        report.FinalDay = final.Day;
        report.FinalState = State(final);
        report.TotalCash = initialCash.ToString(CultureInfo.InvariantCulture);
        report.ChallengeStatus = final.Challenge?.Status.ToString() ?? "Sandbox";
        report.Demand = demand.Values.ToList();
        report.NpcActors = npcs.Values.OrderBy(n => n.ShipId, StringComparer.Ordinal).ToList();
        report.PrivateProcurementCost = final.PrivateFacilities.Values.Sum(f => f.ProcurementCost);
        report.PrivateSalesRevenue = final.PrivateFacilities.Values.Sum(f => f.SalesRevenue);
        return report;
    }

    private static void HybridOrders(RegionalWorldState world, Action<RegionalResult> record)
    {
        // At most three explicit commands per observed day; ship movement still requires real days.
        for (var i = 0; i < 3; i++)
        {
            var state = world.Snapshot();
            if (state.Challenge != null && state.Challenge.Status != ChallengeStatus.Active) return;
            var ship = state.Ships[Player];
            if (ship.DockedPortId == null) return;
            if (ship.DockedPortId == EnterpriseScenario.North)
                record(world.Depart(Player, EnterpriseScenario.West));
            else if (ship.DockedPortId == EnterpriseScenario.West)
            {
                if (ship.Cargo["bread"] >= 4) record(world.Depart(Player, EnterpriseScenario.East));
                else
                {
                    var quantity = Math.Min(ship.CargoCapacity - ship.CargoUnits, state.PrivateFacilities[Factory].Inventory["bread"]);
                    if (quantity < 4) return;
                    record(world.TransferGoods(Owner, Player, Factory, TransferDirection.WarehouseToShip, "bread", quantity));
                }
            }
            else if (ship.DockedPortId == EnterpriseScenario.East)
            {
                var quantity = state.MaximumTradeQuantity(Player, EnterpriseScenario.East, TradeDirection.Sell, "bread");
                if (quantity > 0) record(world.ExecuteTrade(Player, EnterpriseScenario.East,
                    new TradeCommand(TradeDirection.Sell, "bread", quantity), state));
                else if (ship.CargoUnits == 0) record(world.Depart(Player, EnterpriseScenario.West));
                else return;
            }
            else throw new InvalidOperationException("Hybrid ship reached an unauthored port.");
        }
    }

    private static object Configuration(RegionalWorldSnapshot s) => new
    {
        Ports = s.Ports.Values.Select(p => new
        {
            p.Id, p.Name, p.X, p.Y, p.Role,
            Quotes = p.Quotes.Values.Select(q => new { q.GoodId, q.Pricing }).ToArray(),
            // Preserve the historical report contract independently of new living-city fields.
            Facilities = p.Facilities.Select(f => new
            {
                f.Configuration.Instance, f.Configuration.IntervalDays,
                f.Configuration.OutputStockLimit, f.Configuration.FirstDueDay
            }).ToArray(),
            Consumption = p.Consumption.Select(c => new
            {
                c.Configuration.Id, c.Configuration.GoodId, c.Configuration.Quantity,
                c.Configuration.IntervalDays, c.Configuration.FirstDueDay
            }).ToArray()
        }).ToArray(),
        Routes = s.Routes,
        Ships = s.Ships.Values.Select(ship => new
        {
            ship.Id, ship.OwnerId, ship.CargoCapacity, ship.Configuration.Speed, ship.Configuration.Priority,
            ship.Configuration.MinimumOriginStock, ship.Configuration.MinimumReturnStock,
            ship.AutomationEnabled, ship.Policy
        }).ToArray(),
        Blueprints = s.IndustryBlueprints.Values.Select(b => new
        {
            b.Id, b.BuildingId, b.RecipeId, b.ConstructionCost, b.IntervalDays,
            b.WarehouseCapacity, b.OutputStockLimit, b.EligiblePortIds
        }).ToArray(),
        Policies = s.IndustryBlueprints.Values.Select(b => new
        {
            BlueprintId = b.Id, DefaultPolicy = EnterpriseScenario.DefaultIndustryPolicy(b.Id)
        }).ToArray(),
        HybridPolicy = EnterpriseScenario.BakeryPolicy(false)
    };

    private static object State(RegionalWorldSnapshot s) => new
    {
        s.Day, s.Revision,
        Companies = s.Companies.Values.Select(c => new { c.Id, c.Cash }).ToArray(),
        Markets = s.Ports.Values.Select(p => new { p.Id, p.MarketCash, p.Inventory }).ToArray(),
        Ships = s.Ships.Values.Select(ship => new
        {
            ship.Id, ship.OwnerId, ship.DockedPortId, ship.DestinationPortId, ship.RemainingTravelDays, ship.Cargo
        }).ToArray(),
        PrivateFacilities = s.PrivateFacilities.Values.Select(f => new
        {
            f.Id, f.OwnerId, f.PortId, BlueprintId = f.Blueprint.Id, f.Inventory, f.Capacity,
            f.BatchesProduced, f.ProcurementCost, f.SalesRevenue, Outcome = f.LastOutcome.ToString()
        }).ToArray(),
        Challenge = s.Challenge == null ? null : new
        {
            Status = s.Challenge.Status.ToString(), s.Challenge.NetDeliveredQuantity, s.Challenge.CompletedDay
        }
    };

    private static BigInteger Cash(RegionalWorldSnapshot s) =>
        s.Companies.Values.Aggregate(BigInteger.Zero, (sum, c) => sum + c.Cash) +
        s.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.MarketCash);
    private static long Stock(RegionalWorldSnapshot s, string good) =>
        s.Ports.Values.Sum(p => (long)p.Inventory[good]) + s.Ships.Values.Sum(ship => (long)ship.Cargo[good]) +
        s.PrivateFacilities.Values.Sum(f => (long)f.Inventory[good]);
    private static SortedDictionary<string, long> Goods(ContentCatalog content) =>
        new(content.Goods.ToDictionary(g => g.Id, _ => 0L), StringComparer.Ordinal);
    private static void Add(SortedDictionary<string, long> values, string key, long amount) =>
        values[key] = checked(values.TryGetValue(key, out var before) ? before + amount : amount);
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private sealed class Report
    {
        public string ScenarioVersion { get; } = "enterprise-1";
        public int ObservationDays { get; } = Days;
        public object? Configuration { get; set; }
        public string Accounting { get; } =
            "Construction transfers company cash to the local market. Purchases and sales are actual cash flows; unsold goods have no speculative valuation. No household payments or resets.";
        public string FarmCadenceRationale { get; } =
            "Four grain every three days supports two-grain milling and the finite physical grain/flour merchant.";
        public object Charter { get; } = new
        {
            Destination = EnterpriseScenario.East, GoodId = "bread", NetQuantity = EnterpriseScenario.CharterBread,
            DeadlineDay = EnterpriseScenario.CharterDeadline, MinimumCash = EnterpriseScenario.CharterMinimumCash
        };
        public List<StrategyReport> Sandbox { get; } = new();
        public List<StrategyReport> Challenges { get; } = new();
    }

    private sealed class StrategyReport
    {
        public string Strategy { get; set; } = "";
        public object? InitialState { get; set; }
        public long FinalDay { get; set; }
        public long FinalCash { get; set; }
        public long ConstructionExpenditure { get; set; }
        public long PurchaseCost { get; set; }
        public long SalesRevenue { get; set; }
        public long PrivateProcurementCost { get; set; }
        public long PrivateSalesRevenue { get; set; }
        public long PrivateBatches { get; set; }
        public long NetBreadDeliveredToEast { get; set; }
        public string ChallengeStatus { get; set; } = "";
        public string TotalCash { get; set; } = "";
        public int Operations { get; set; }
        public SortedDictionary<string, long> GoodsAcquired { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> GoodsSold { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PublicBatches { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PublicExtraction { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PublicInputs { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PublicOutputs { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PrivateExtraction { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PrivateInputs { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> PrivateOutputs { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, long> ProductionShortages { get; } = new(StringComparer.Ordinal);
        public List<DemandTotals> Demand { get; set; } = new();
        public List<NpcTotals> NpcActors { get; set; } = new();
        public List<object> PhysicalReconciliation { get; } = new();
        public List<object> PlayerTrades { get; } = new();
        public List<object> Daily { get; } = new();
        public object? FinalState { get; set; }
    }

    private sealed class DemandTotals
    {
        public string PortId { get; set; } = "";
        public string GoodId { get; set; } = "";
        public long Met { get; set; }
        public long Unmet { get; set; }
    }

    private sealed class NpcTotals
    {
        public string ShipId { get; set; } = "";
        public long Deliveries { get; set; }
        public long UnitsDelivered { get; set; }
        public long PurchaseCost { get; set; }
        public long Revenue { get; set; }
        public long Arrivals { get; set; }
        public SortedDictionary<string, long> WaitReasons { get; } = new(StringComparer.Ordinal);
    }
}
