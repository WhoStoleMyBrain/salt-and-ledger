using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SaltAndLedger.Simulation;

/// <summary>Reproducible observations of the real EnterpriseScenario and explicitly identified controlled variants.</summary>
internal static class IndustrialLogisticsExperiments
{
    private const string Ship = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Factory = "logistics_bakery";
    private const int Days = 90;
    private const string PathDefault = "docs/experiments/industrial-logistics-results.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
    };

    internal static int Run(ContentCatalog content, string? path, bool check)
    {
        var report = Simulate(content, false);
        var json = Serialize(report);
        Require(json == Serialize(Simulate(content, false)), "Repeated industrial logistics results differ.");
        Require(json == Serialize(Simulate(content, true)), "Industrial logistics depend on configuration order.");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Require(json == Serialize(Simulate(content, false)), "Industrial logistics depend on culture.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        path ??= PathDefault;
        if (check)
            Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
                "Industrial logistics report is stale; regenerate with --industrial-logistics " + path);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        Console.WriteLine("Ninety-day private bakery shipping: real stock, physical cargo, exact prices and attributed costs.");
        Console.WriteLine("Strategy                 Delivered   Cash  Purchases  Revenue   COGS  Held basis  Blocked days");
        foreach (var row in report.Cases)
            Console.WriteLine(FormattableString.Invariant(
                $"{row.Name,-24} {row.DeliveredBread,9} {row.FinalCash,6} {row.InventoryPurchases,10} {row.SalesRevenue,8} {row.CostOfGoodsSold,6} {row.HeldInventoryCost,11} {row.BlockedDays,13}"));
        Console.WriteLine(FormattableString.Invariant(
            $"Equivalent manual commands: {report.ManualEconomicCommands}; automatic route setup: 1; prescribed inputs avoided: {report.ManualInputsAvoided}."));
        Console.WriteLine("Per-operation cash, goods, FIFO basis, manual equivalence, repeat, reversed setup and tr-TR matched.");
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    private static string Serialize<T>(T report) => JsonSerializer.Serialize(report, Options).Replace("\r\n", "\n") + "\n";

    private static Report Simulate(ContentCatalog content, bool reverse)
    {
        var report = new Report();
        foreach (var name in new[] { "automatic", "restrictive_sale_floor", "insufficient_source_stock",
            "no_destination_cash", "slower_production", "stronger_destination_demand" })
            report.Cases.Add(Observe(content, name, reverse));
        var baseline = report.Cases[0];
        Require(baseline.DeliveredBread >= 18 && baseline.ShipmentsSold >= 3,
            "The bakery must repeat multiple complete private production/shipping/sale cycles.");
        Require(report.Cases[1].DeliveredBread == 0 && report.Cases[1].FinalCargoBread > 0,
            "A restrictive selling floor must retain its shipment.");
        Require(report.Cases[2].DeliveredBread == 0 && report.Cases[2].PrivateBatches == 0,
            "A disabled source must not manufacture or transport free output.");
        Require(report.Cases[3].DeliveredBread == 0 && report.Cases[3].FinalCargoBread > 0,
            "A cashless destination must retain the player's shipment.");
        report.ManualEconomicCommands = baseline.EquivalentManualEconomicCommands;
        report.ManualInputsAvoided = Math.Max(0, report.ManualEconomicCommands - 1);
        return report;
    }

    private static CaseReport Observe(ContentCatalog content, string name, bool reverse)
    {
        var world = CreateWorld(content, name, reverse);
        var mirror = name == "automatic" ? CreateWorld(content, name, reverse) : null;
        var report = new CaseReport { Name = name };
        var initialCash = Cash(world.Snapshot());
        var blocked = new HashSet<long>();
        var waits = new HashSet<string>(StringComparer.Ordinal);
        var floor = name == "restrictive_sale_floor" ? 1000 : 25;
        var policy = new IndustrialRoutePolicy(EnterpriseScenario.West, EnterpriseScenario.East,
            new IndustrialRouteLeg("bread", 6, 6, IndustrialEndpointKind.Warehouse,
                sourceFacilityId: Factory, minimumSourceStock: 8, warehouseReserve: 2, minimumSellPrice: floor),
            cashReserve: 100);

        void Record(RegionalResult result)
        {
            Validate(content, result, initialCash);
            foreach (var trade in result.Trades.Where(t => t.OwnerId == Owner &&
                t.Trade.Direction == TradeDirection.Sell && t.Trade.Outcome == TradeOutcome.Traded))
            {
                if (trade.Trade.GoodId == "bread" && trade.PortId == EnterpriseScenario.East)
                {
                    report.DeliveredBread += trade.Trade.ExecutedQuantity;
                    report.ShipmentsSold++;
                }
            }
            foreach (var decision in result.Decisions.Where(d => d.ShipId == Ship &&
                (d.Action == AutomationAction.Wait || d.Action == AutomationAction.Pause)))
            {
                blocked.Add(result.Before.Day);
                var key = result.Before.Day.ToString(CultureInfo.InvariantCulture) + ":" + decision.Reason;
                if (waits.Add(key))
                {
                    if (!report.WaitReasons.ContainsKey(decision.Reason)) report.WaitReasons[decision.Reason] = 0;
                    report.WaitReasons[decision.Reason]++;
                }
            }
            foreach (var demand in result.Consumption.Where(c => c.PortId == EnterpriseScenario.East && c.GoodId == "bread"))
            {
                report.EastBreadConsumed += demand.ConsumedQuantity;
                report.EastBreadUnmet += demand.UnmetQuantity;
            }
            foreach (var entry in result.EconomicEvents.Where(e => e.CompanyId == Owner))
                report.Ledger.Add(new LedgerRow
                {
                    Day = entry.Day, Revision = entry.Revision, Kind = entry.Kind.ToString(),
                    PortId = entry.PortId, SourceStoreId = entry.SourceStoreId, DestinationStoreId = entry.DestinationStoreId,
                    CashDelta = entry.CompanyCashDelta, HistoricalCost = entry.AllocatedCost,
                    GoodsIn = entry.Produced.Select(g => new GoodsRow { GoodId = g.GoodId, Quantity = g.Quantity, Cost = g.CostBasis }).ToArray(),
                    GoodsOut = entry.Consumed.Select(g => new GoodsRow { GoodId = g.GoodId, Quantity = g.Quantity, Cost = g.CostBasis }).ToArray()
                });
            report.Operations++;
        }

        void Setup(RegionalWorldState target, Action<RegionalResult>? record)
        {
            void Apply(RegionalResult r) => record?.Invoke(r);
            Apply(target.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, Factory));
            if (name == "insufficient_source_stock") Apply(target.SetFacilityEnabled(Owner, Factory, false));
            Apply(target.ConfigureFacility(Owner, Factory, EnterpriseScenario.BakeryPolicy()));
            Apply(target.Depart(Ship, EnterpriseScenario.West));
            while (target.Snapshot().Ships[Ship].DockedPortId == null) Apply(target.AdvanceDay());
        }

        Setup(world, Record);
        if (mirror != null) Setup(mirror, r => Validate(content, r, initialCash));
        Record(world.ConfigureIndustrialAutomation(Ship, policy, true));
        if (mirror != null) Require(Economy(world.Snapshot()) == Economy(mirror.Snapshot()), "Setup differs from manual mirror.");

        while (world.Snapshot().Day < Days)
        {
            // Both hosts explicitly request immediate dockside work before their next whole day.
            // The daily actor phase then observes the resulting wait/voyage, with no extra transaction day.
            var dockside = world.RunAutomation(Ship);
            Record(dockside);
            if (mirror != null)
            {
                foreach (var decision in dockside.Decisions)
                {
                    var detail = decision.IndustrialDetail!;
                    if (detail.Trade != null)
                    {
                        Validate(content, mirror.ExecuteTrade(Ship, detail.Trade.PortId, detail.Trade.Trade), initialCash);
                        report.EquivalentManualEconomicCommands++;
                    }
                    if (detail.Transfer != null)
                    {
                        var transfer = detail.Transfer;
                        Validate(content, mirror.TransferGoods(Owner, Ship, transfer.FacilityId,
                            transfer.Direction, transfer.GoodId, transfer.Quantity), initialCash);
                        report.EquivalentManualEconomicCommands++;
                    }
                    if (detail.Departure != null)
                    {
                        Validate(content, mirror.Depart(Ship, detail.Departure.DestinationPortId), initialCash);
                        report.EquivalentManualEconomicCommands++;
                    }
                }
                Require(Economy(world.Snapshot()) == Economy(mirror.Snapshot()), "Manual dockside sequence differs from automation.");
            }
            var day = world.AdvanceDay();
            Record(day);
            if (mirror != null)
            {
                Validate(content, mirror.AdvanceDay(), initialCash);
                Require(Economy(world.Snapshot()) == Economy(mirror.Snapshot()), "Manual daily state differs from automation.");
            }
            var snapshot = world.Snapshot();
            var finance = snapshot.Accounting.Companies[Owner];
            report.Daily.Add(new DayRow
            {
                Day = snapshot.Day, Cash = snapshot.Companies[Owner].Cash,
                CargoBread = snapshot.Ships[Ship].Cargo["bread"], WarehouseBread = snapshot.PrivateFacilities[Factory].Inventory["bread"],
                Dock = snapshot.Ships[Ship].DockedPortId, Destination = snapshot.Ships[Ship].DestinationPortId,
                Stage = snapshot.Ships[Ship].IndustrialStage.ToString(), SalesRevenue = finance.SalesRevenue,
                CostOfGoodsSold = finance.CostOfGoodsSold, HeldInventoryCost = finance.HeldInventoryCost,
                PrivateBatches = snapshot.PrivateFacilities[Factory].BatchesProduced
            });
        }
        var final = world.Snapshot();
        var financial = final.Accounting.Companies[Owner];
        report.FinalCash = final.Companies[Owner].Cash;
        report.InventoryPurchases = financial.InventoryPurchases;
        report.SalesRevenue = financial.SalesRevenue;
        report.CostOfGoodsSold = financial.CostOfGoodsSold;
        report.RealizedGrossMargin = financial.RealizedGrossMargin;
        report.HeldInventoryCost = financial.HeldInventoryCost;
        report.ConstructionExpenditure = financial.ConstructionExpenditure;
        report.FinalCargoBread = final.Ships[Ship].Cargo["bread"];
        report.FinalWarehouseBread = final.PrivateFacilities[Factory].Inventory["bread"];
        report.PrivateBatches = final.PrivateFacilities[Factory].BatchesProduced;
        report.BlockedDays = blocked.Count;
        report.HeldGoods = new SortedDictionary<string, long>(financial.HeldInventoryQuantities.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
        report.InitialCombinedCash = initialCash.ToString(CultureInfo.InvariantCulture);
        report.FinalCombinedCash = Cash(final).ToString(CultureInfo.InvariantCulture);
        report.ManualEquivalenceVerified = mirror != null;
        return report;
    }

    private static RegionalWorldState CreateWorld(ContentCatalog content, string variant, bool reverse)
    {
        var initial = EnterpriseScenario.Create(content, reverseConfiguration: reverse).Snapshot();
        if (variant != "no_destination_cash" && variant != "slower_production" && variant != "stronger_destination_demand")
            return EnterpriseScenario.Create(content, reverseConfiguration: reverse);
        var ports = initial.Ports.Values.Select(p =>
        {
            var config = p.Configuration;
            if (p.Id != EnterpriseScenario.East) return config;
            return new RegionalPortConfiguration(new PortConfiguration(p.Id, p.Quotes.Values,
                variant == "no_destination_cash" ? 0 : p.MarketCash, config.Market.Buildings, p.Inventory),
                config.Name, config.X, config.Y, config.Facilities,
                config.Consumption.Select(c => variant == "stronger_destination_demand" && c.GoodId == "bread"
                    ? new ConsumptionConfiguration(c.Id, c.GoodId, c.Quantity, 2) : c), config.Role);
        }).ToArray();
        var blueprints = initial.IndustryBlueprints.Values.Select(b =>
            variant == "slower_production" && b.Id == EnterpriseScenario.BakeryBlueprint
                ? new IndustryBlueprint(b.Id, b.BuildingId, b.RecipeId, b.ConstructionCost, 4,
                    b.WarehouseCapacity, b.EligiblePortIds, b.OutputStockLimit) : b).ToArray();
        return new RegionalWorldState(content, reverse ? ports.Reverse() : ports, initial.Routes,
            initial.Ships.Values.Select(s => s.Configuration),
            companies: initial.Companies.Values.Select(c => new CompanyConfiguration(c.Id, c.Cash)),
            industryBlueprints: reverse ? blueprints.Reverse() : blueprints);
    }

    private static void Validate(ContentCatalog content, RegionalResult result, BigInteger initialCash)
    {
        Require(Cash(result.After) == initialCash, "Company/market cash is not conserved.");
        foreach (var good in content.Goods)
        {
            var production = result.Production.Sum(p => p.Changes.Where(c => c.GoodId == good.Id).Sum(c => (long)c.Delta)) +
                result.PrivateProduction.Sum(p => p.Changes.Where(c => c.GoodId == good.Id).Sum(c => (long)c.Delta));
            var consumption = result.Consumption.Where(c => c.GoodId == good.Id).Sum(c => (long)c.ConsumedQuantity);
            Require(Stock(result.After, good.Id) == Stock(result.Before, good.Id) + production - consumption,
                "Physical industrial flow does not reconcile: " + good.Id);
        }
        foreach (var finance in result.After.Accounting.Companies.Values)
        {
            Require((BigInteger)finance.InventoryPurchases == (BigInteger)finance.CostOfGoodsSold + finance.HeldInventoryCost,
                "Historical purchases do not reconcile with sold and held cost.");
            Require((BigInteger)result.After.Companies[finance.OwnerId].Cash ==
                (BigInteger)finance.InitialCash + finance.SalesRevenue - finance.InventoryPurchases - finance.ConstructionExpenditure,
                "Company cash disagrees with financial summaries.");
        }
        foreach (var ship in result.After.Ships.Values)
            foreach (var good in content.Goods)
                Require(ship.Cargo[good.Id] == result.After.Accounting.Stores["ship:" + ship.Id].GetQuantity(good.Id),
                    "Ship quantities disagree with cost lots.");
        foreach (var facility in result.After.PrivateFacilities.Values)
            foreach (var good in content.Goods)
                Require(facility.Inventory[good.Id] == result.After.Accounting.Stores["facility:" + facility.Id].GetQuantity(good.Id),
                    "Warehouse quantities disagree with cost lots.");
    }

    private static string Economy(RegionalWorldSnapshot snapshot) => Serialize(new
    {
        snapshot.Day, snapshot.Companies, snapshot.Accounting,
        Ports = snapshot.Ports.Values.Select(p => new { p.Id, p.Inventory, p.MarketCash, p.Facilities, p.Consumption }),
        Ships = snapshot.Ships.Values.Select(s => new { s.Id, s.Ship, s.Cash, s.VoyageDurationDays }),
        snapshot.PrivateFacilities
    });

    private static BigInteger Cash(RegionalWorldSnapshot state) =>
        state.Companies.Values.Aggregate(BigInteger.Zero, (total, c) => total + c.Cash) +
        state.Ports.Values.Aggregate(BigInteger.Zero, (total, p) => total + p.MarketCash);
    private static long Stock(RegionalWorldSnapshot state, string good) =>
        state.Ports.Values.Sum(p => (long)p.Inventory[good]) + state.Ships.Values.Sum(s => (long)s.Cargo[good]) +
        state.PrivateFacilities.Values.Sum(f => (long)f.Inventory[good]);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Report
    {
        public int FormatVersion { get; } = 1;
        public int Days { get; } = IndustrialLogisticsExperiments.Days;
        public string Scenario { get; } = "EnterpriseScenario; Westhaven private bakery to Eastport; Northport start and real initial voyage.";
        public string Policy { get; } = "Wait for 8 bread, retain 2, load 6, sell at marginal bid >=25, empty return; local bakery procurement defaults.";
        public string Comparison { get; } = "Manual ordinary commands mirror each automatic dockside chain; compare all physical/economic/accounting state after every chain and day. Configuration, route stage, revision and event timestamps differ by command grouping.";
        public string InputMeasure { get; } = "Count actual manual trade, transfer and departure commands after identical setup; one automatic route configuration replaces those prescribed inputs. Day advancement and shared construction/procurement setup excluded.";
        public string BlockedMeasure { get; } = "Distinct economic days with a player Wait/Pause decision, including waiting for a batch threshold. Sailing is not blocked; one reason counts once per day.";
        public string Variants { get; } = "Restrictive floor=1000; insufficient stock disables the bakery; no liquidity initializes Eastport cash to 0; slower bakery interval=4; stronger Eastport bread demand=2 units every 2 days. Other EnterpriseScenario rules remain unchanged.";
        public int ManualEconomicCommands { get; set; }
        public int ManualInputsAvoided { get; set; }
        public List<CaseReport> Cases { get; } = new();
    }
    private sealed class CaseReport
    {
        public string Name { get; set; } = "";
        public long FinalCash { get; set; }
        public long ConstructionExpenditure { get; set; }
        public long InventoryPurchases { get; set; }
        public long SalesRevenue { get; set; }
        public long CostOfGoodsSold { get; set; }
        public long RealizedGrossMargin { get; set; }
        public long HeldInventoryCost { get; set; }
        public long PrivateBatches { get; set; }
        public int DeliveredBread { get; set; }
        public int ShipmentsSold { get; set; }
        public int FinalCargoBread { get; set; }
        public int FinalWarehouseBread { get; set; }
        public int BlockedDays { get; set; }
        public long EastBreadConsumed { get; set; }
        public long EastBreadUnmet { get; set; }
        public bool ManualEquivalenceVerified { get; set; }
        public int EquivalentManualEconomicCommands { get; set; }
        public int Operations { get; set; }
        public string InitialCombinedCash { get; set; } = "";
        public string FinalCombinedCash { get; set; } = "";
        public SortedDictionary<string, long> HeldGoods { get; set; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, int> WaitReasons { get; } = new(StringComparer.Ordinal);
        public List<DayRow> Daily { get; } = new();
        public List<LedgerRow> Ledger { get; } = new();
    }
    private sealed class DayRow
    {
        public long Day { get; set; }
        public long Cash { get; set; }
        public int CargoBread { get; set; }
        public int WarehouseBread { get; set; }
        public string? Dock { get; set; }
        public string? Destination { get; set; }
        public string Stage { get; set; } = "";
        public long SalesRevenue { get; set; }
        public long CostOfGoodsSold { get; set; }
        public long HeldInventoryCost { get; set; }
        public long PrivateBatches { get; set; }
    }
    private sealed class LedgerRow
    {
        public long Day { get; set; }
        public long Revision { get; set; }
        public string Kind { get; set; } = "";
        public string? PortId { get; set; }
        public string? SourceStoreId { get; set; }
        public string? DestinationStoreId { get; set; }
        public long CashDelta { get; set; }
        public long HistoricalCost { get; set; }
        public GoodsRow[] GoodsIn { get; set; } = Array.Empty<GoodsRow>();
        public GoodsRow[] GoodsOut { get; set; } = Array.Empty<GoodsRow>();
    }
    private sealed class GoodsRow
    {
        public string GoodId { get; set; } = "";
        public int Quantity { get; set; }
        public long Cost { get; set; }
    }
}
