using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SaltAndLedger.Simulation;

/// <summary>Reproducible observations of the same world used by the Unity map.</summary>
internal static class ArchipelagoExperiments
{
    private const int ObservationDays = 60;
    private const string Player = ArchipelagoScenario.PlayerShipId;
    private const string DefaultPath = "docs/experiments/archipelago-results.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static int Run(ContentCatalog content, string? outputPath, bool check)
    {
        var report = Simulate(content, false);
        var json = Serialize(report);
        Require(json == Serialize(Simulate(content, false)), "Repeated archipelago observations differ.");
        Require(json == Serialize(Simulate(content, true)), "Configuration ordering changes the archipelago.");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Require(json == Serialize(Simulate(content, false)), "Archipelago output depends on culture.");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        var path = outputPath ?? DefaultPath;
        if (check)
        {
            Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
                "Archipelago report is stale; regenerate with --archipelago " + path);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        Console.WriteLine("Sixty-day living archipelago: identical initial worlds; no cash or stock resets.");
        Console.WriteLine("Strategy              Cash   Held logs/planks  Closed profit  NPC deliveries  Unmet planks  Player inputs");
        foreach (var scenario in report.Scenarios)
        {
            var s = scenario.Summary;
            Console.WriteLine(FormattableString.Invariant(
                $"{scenario.Strategy,-21} {s.PlayerCash,5}  {s.PlayerLogs,5}/{s.PlayerPlanks,-6} {s.RealizedClosedProfit,12}  {s.NpcDeliveries,14}  {s.UnmetPlanks,12}  {s.PlayerInputs,13}"));
        }
        Console.WriteLine(FormattableString.Invariant(
            $"Verified {report.Scenarios.Count} strategies × {ObservationDays} days; per-commit goods/cash reconciliation; repeat, reversed configuration, and tr-TR output match."));
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    private static string Serialize(Report report) =>
        JsonSerializer.Serialize(report, Options).Replace("\r\n", "\n") + "\n";

    private static Report Simulate(ContentCatalog content, bool reverse)
    {
        var report = new Report();
        foreach (var strategy in new[] { "idle", "manual_west", "restrictive", "aggressive", "switch_to_west" })
            report.Scenarios.Add(Observe(content, strategy, reverse));
        var idle = report.Scenarios[0];
        Require(idle.Summary.NpcDeliveries >= 4, "The baseline must contain recurring NPC deliveries.");
        Require(idle.Summary.ActorTotals.All(actor => actor.ShipId == Player || actor.CompletedShipments >= 2),
            "Both NPC merchants must complete repeated deliveries.");
        Require(report.Scenarios.Select(s => s.Summary.PlayerCash).Distinct().Count() >= 3,
            "The strategies must produce meaningfully different accounts.");
        return report;
    }

    private static ScenarioReport Observe(ContentCatalog content, string strategy, bool reverse)
    {
        var world = ArchipelagoScenario.Create(content, reverse);
        var initial = world.Snapshot();
        var observation = new ScenarioReport { Strategy = strategy, InitialConfiguration = DescribeConfiguration(initial) };
        var actors = initial.Ships.Values.ToDictionary(ship => ship.Id, ship => new ActorTotals { ShipId = ship.Id }, StringComparer.Ordinal);
        var cash = TotalCash(initial);
        long extractionLogs = 0, transformedLogs = 0, producedPlanks = 0, consumedPlanks = 0, unmetPlanks = 0;
        long realizedClosedProfit = 0, closedCash = initial.Ships[Player].Cash;
        var pendingPlayerCosts = false;
        var playerInputs = 0;
        var longestInactive = 0;
        var inactiveDays = 0;
        var currentInactive = 0;
        var routeChanged = false;
        long? routeChangedDay = null;
        var repositioning = false;
        var lastPlayerInputDay = -1L;
        var activityToday = false;
        observation.Daily.Add(DescribeState(initial, world));

        void Record(RegionalResult result)
        {
            var cargo = result.Before.Ships.Values.ToDictionary(ship => ship.Id,
                ship => ship.Cargo.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal);
            Require(TotalCash(result.After) == cash, "Cash changed outside ordinary transfers.");
            foreach (var good in content.Goods)
            {
                var production = result.Production.Sum(p => p.Changes.Where(c => c.GoodId == good.Id).Sum(c => (long)c.Delta));
                var consumption = result.Consumption.Where(c => c.GoodId == good.Id).Sum(c => (long)c.ConsumedQuantity);
                Require(TotalGoods(result.After, good.Id) == TotalGoods(result.Before, good.Id) + production - consumption,
                    "Physical stock flow does not reconcile for " + good.Id);
            }
            foreach (var trade in result.Trades)
            {
                var t = trade.Trade;
                Require(t.MarketCashChange.Delta + t.TraderCashChange.Delta == 0, "Trade cash is not conserved.");
                Require(t.MarketInventoryChange.Delta + t.TraderInventoryChange.Delta == 0, "Trade goods are not conserved.");
                if (t.Outcome == TradeOutcome.Traded)
                {
                    activityToday = true;
                    var totals = actors[trade.ShipId];
                    if (t.Direction == TradeDirection.Buy)
                    {
                        totals.PurchasedUnits += t.ExecutedQuantity;
                        totals.PurchaseCost += t.ExecutedAmount;
                        if (trade.ShipId == Player) pendingPlayerCosts = true;
                    }
                    else
                    {
                        totals.SoldUnits += t.ExecutedQuantity;
                        totals.SaleRevenue += t.ExecutedAmount;
                        totals.SaleOrders++;
                    }
                    cargo[trade.ShipId][t.GoodId] = t.TraderInventoryChange.After;
                    var emptyCargo = cargo[trade.ShipId].Values.All(quantity => quantity == 0);
                    if (t.Direction == TradeDirection.Sell && emptyCargo) totals.CompletedShipments++;
                    if (trade.ShipId == Player)
                    {
                        // Close at the actual empty-cargo point, including a sale followed by
                        // another purchase inside the same bounded daily action chain.
                        if (pendingPlayerCosts && emptyCargo)
                        {
                            realizedClosedProfit += t.TraderCashChange.After - closedCash;
                            closedCash = t.TraderCashChange.After;
                            pendingPlayerCosts = false;
                        }
                    }
                }
            }
            foreach (var production in result.Production.Where(p => p.Outcome == ScheduledProductionOutcome.Produced))
            {
                activityToday = true;
                var recipe = content.GetRecipe(production.RecipeId);
                if (recipe.Kind == RecipeKind.Extraction)
                    extractionLogs += production.Changes.Where(c => c.GoodId == "logs").Sum(c => c.Delta);
                else
                {
                    transformedLogs -= production.Changes.Where(c => c.GoodId == "logs").Sum(c => c.Delta);
                    producedPlanks += production.Changes.Where(c => c.GoodId == "planks").Sum(c => c.Delta);
                }
            }
            consumedPlanks += result.Consumption.Sum(c => c.ConsumedQuantity);
            unmetPlanks += result.Consumption.Sum(c => c.UnmetQuantity);
            if (result.Consumption.Any(c => c.ConsumedQuantity > 0) || result.Movements.Count > 0) activityToday = true;
            foreach (var movement in result.Movements)
            {
                if (movement.Kind == ShipMovementKind.Progress) actors[movement.ShipId].SailingDays++;
                if (movement.Kind == ShipMovementKind.Arrival)
                {
                    actors[movement.ShipId].Arrivals++;
                    actors[movement.ShipId].TransportedUnits += result.After.Ships[movement.ShipId].CargoUnits;
                }
            }
            foreach (var decision in result.Decisions)
            {
                actors[decision.ShipId].PolicyEvaluations++;
                if (decision.Action == AutomationAction.Wait || decision.Action == AutomationAction.Pause)
                {
                    var reasons = actors[decision.ShipId].WaitingReasons;
                    reasons[decision.Reason] = reasons.TryGetValue(decision.Reason, out var count) ? count + 1 : 1;
                }
            }
            observation.Events.Add(new
            {
                Day = result.Before.Day, Revision = result.After.Revision,
                AdvancedDay = result.After.Day != result.Before.Day,
                Trades = result.Trades.Select(t => new
                {
                    t.ShipId, t.OwnerId, t.PortId, Direction = t.Trade.Direction.ToString(),
                    t.Trade.GoodId, t.Trade.Quantity, Outcome = t.Trade.Outcome.ToString(),
                    t.Trade.ExecutedAmount, t.Trade.UnitPrice, LastMarginalPrice = t.Trade.Price?.LastMarginalPrice
                }).ToArray(),
                Production = result.Production.Select(p => new
                {
                    p.PortId, p.FacilityId, p.RecipeId, Outcome = p.Outcome.ToString(),
                    Changes = p.Changes.Select(c => new { c.GoodId, c.Delta }).ToArray()
                }).ToArray(),
                Consumption = result.Consumption.Select(c => new
                {
                    c.PortId, c.RuleId, c.GoodId, c.RequestedQuantity, c.ConsumedQuantity, c.UnmetQuantity
                }).ToArray(),
                Movements = result.Movements.Select(m => new
                {
                    m.ShipId, Kind = m.Kind.ToString(), m.OriginPortId, m.DestinationPortId, m.RemainingDays
                }).ToArray(),
                Decisions = result.Decisions.Select(d => new
                {
                    d.ShipId, Stage = d.Stage.ToString(), Action = d.Action.ToString(), d.Reason, d.SelectedQuantity
                }).ToArray()
            });
        }

        void Input(RegionalResult result)
        {
            playerInputs++;
            lastPlayerInputDay = result.Before.Day;
            Record(result);
        }

        bool SellAll(string good)
        {
            var snapshot = world.Snapshot();
            var ship = snapshot.Ships[Player];
            var quantity = ship.Cargo[good];
            if (quantity == 0 || ship.DockedPortId == null) return quantity == 0;
            var order = new TradeCommand(TradeDirection.Sell, good, quantity);
            if (snapshot.PreviewTrade(Player, ship.DockedPortId, order).Outcome != TradeOutcome.Traded) return false;
            Input(world.ExecuteTrade(Player, ship.DockedPortId, order));
            return true;
        }

        if (strategy == "restrictive" || strategy == "aggressive" || strategy == "switch_to_west")
            Input(world.ConfigureAutomation(Player, ArchipelagoScenario.PlayerPolicy(strategy == "restrictive"), true));
        if (strategy == "manual_west") Input(world.Depart(Player, ArchipelagoScenario.West));

        for (var day = 0; day < ObservationDays; day++)
        {
            activityToday = false;
            var snapshot = world.Snapshot();
            var player = snapshot.Ships[Player];
            if (strategy == "manual_west" && player.DockedPortId != null)
            {
                if (player.DockedPortId == ArchipelagoScenario.West && player.CargoUnits == 0)
                {
                    var order = new TradeCommand(TradeDirection.Buy, "logs", 6);
                    var preview = snapshot.PreviewTrade(Player, ArchipelagoScenario.West, order);
                    if (preview.Outcome == TradeOutcome.Traded && preview.Price!.LastMarginalPrice <= 14 &&
                        player.Cash - preview.ExecutedAmount >= 20)
                    {
                        Input(world.ExecuteTrade(Player, ArchipelagoScenario.West, order));
                        Input(world.Depart(Player, ArchipelagoScenario.South));
                    }
                }
                else if (player.DockedPortId == ArchipelagoScenario.South && SellAll("logs"))
                    Input(world.Depart(Player, ArchipelagoScenario.West));
            }
            if (strategy == "switch_to_west" && !routeChanged && day >= 20 &&
                player.DockedPortId == ArchipelagoScenario.North)
            {
                Input(world.SetAutomationEnabled(Player, false));
                if (SellAll("planks") && world.Snapshot().Ships[Player].CargoUnits == 0)
                {
                    Input(world.Depart(Player, ArchipelagoScenario.West));
                    routeChanged = true;
                    repositioning = true;
                    routeChangedDay = snapshot.Day;
                }
            }
            if (repositioning && world.Snapshot().Ships[Player].DockedPortId == ArchipelagoScenario.West)
            {
                Input(world.ConfigureAutomation(Player, new TradeRoutePolicy(ArchipelagoScenario.West,
                    ArchipelagoScenario.South, new RouteLeg("logs", 6, 2, 14, 14, 100),
                    new RouteLeg("planks", 3, 1, 24, 18, 72), 20, true), true));
                repositioning = false;
            }
            Record(world.AdvanceDay());
            Require(world.Snapshot().Day == day + 1, "A daily advance did not increment exactly once.");
            if (activityToday) currentInactive = 0;
            else
            {
                inactiveDays++;
                currentInactive++;
                longestInactive = Math.Max(longestInactive, currentInactive);
            }
            observation.Daily.Add(DescribeState(world.Snapshot(), world));
        }
        var final = world.Snapshot();
        var finalPlayer = final.Ships[Player];
        observation.Summary = new Summary
        {
            PlayerCash = finalPlayer.Cash,
            PlayerLogs = finalPlayer.Cargo["logs"],
            PlayerPlanks = finalPlayer.Cargo["planks"],
            RealizedClosedProfit = realizedClosedProfit,
            FinalEmptyCargoProfit = finalPlayer.CargoUnits == 0 && actors[Player].PurchasedUnits > 0
                ? finalPlayer.Cash - initial.Ships[Player].Cash : null,
            OpenCargoCashChange = finalPlayer.CargoUnits > 0 ? finalPlayer.Cash - closedCash : 0,
            PlayerInputs = playerInputs,
            LastPlayerInputDay = lastPlayerInputDay < 0 ? null : lastPlayerInputDay,
            RouteChangedDay = routeChangedDay,
            ExtractedLogs = extractionLogs,
            LogsTransformed = transformedLogs,
            PlanksProduced = producedPlanks,
            PlanksConsumed = consumedPlanks,
            UnmetPlanks = unmetPlanks,
            NpcDeliveries = actors.Values.Where(a => a.ShipId != Player).Sum(a => a.CompletedShipments),
            TotalTransportedUnits = actors.Values.Sum(a => a.TransportedUnits),
            InactiveDays = inactiveDays,
            LongestInactivePeriod = longestInactive,
            TotalCash = cash.ToString(CultureInfo.InvariantCulture),
            TotalLogs = TotalGoods(final, "logs"),
            TotalPlanks = TotalGoods(final, "planks"),
            ActorTotals = actors.Values.OrderBy(actor => actor.ShipId, StringComparer.Ordinal).ToList()
        };
        Require(TotalGoods(final, "logs") == TotalGoods(initial, "logs") + extractionLogs - transformedLogs,
            "Whole-run logs do not reconcile.");
        Require(TotalGoods(final, "planks") == TotalGoods(initial, "planks") + producedPlanks - consumedPlanks,
            "Whole-run planks do not reconcile.");
        if (strategy == "switch_to_west") Require(routeChangedDay != null, "The route-switch strategy never changed route.");
        return observation;
    }

    private static object DescribeConfiguration(RegionalWorldSnapshot snapshot) => new
    {
        Ports = snapshot.Ports.Values.Select(p => new
        {
            p.Id, p.Name, p.X, p.Y, p.Role, p.MarketCash, p.Inventory,
            Quotes = p.Quotes.Values.Select(q => new { q.GoodId, q.Pricing }).ToArray(),
            // Historical report schemas stay explicit as opt-in living configuration grows.
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
        Routes = snapshot.Routes,
        Ships = snapshot.Ships.Values.Select(s => new
        {
            s.Id, s.OwnerId, s.Cash, s.CargoCapacity, s.Configuration.Speed,
            s.Configuration.Priority, s.Configuration.MinimumOriginStock,
            s.Configuration.MinimumReturnStock, s.AutomationEnabled, s.Policy
        }).ToArray()
    };

    private static object DescribeState(RegionalWorldSnapshot snapshot, RegionalWorldState world) => new
    {
        snapshot.Day, snapshot.Revision,
        Ports = snapshot.Ports.Values.Select(p => new
        {
            p.Id, Logs = p.Inventory["logs"], Planks = p.Inventory["planks"], Cash = p.MarketCash,
            Prices = p.Quotes.Keys.Select(g => snapshot.GetMarketQuote(p.Id, g)).ToArray(),
            DockedShips = snapshot.Ships.Values.Where(s => s.DockedPortId == p.Id).Select(s => s.Id).ToArray(),
            Facilities = p.Facilities.Select(f => new
            {
                Id = f.Configuration.Instance.Id, f.Enabled, f.NextDueDay, LastOutcome = f.LastOutcome.ToString()
            }).ToArray(),
            Demand = p.Consumption.Select(c => new
            {
                Id = c.Configuration.Id, c.NextDueDay, c.LastUnmetQuantity, c.TotalUnmetQuantity
            }).ToArray()
        }).ToArray(),
        Ships = snapshot.Ships.Values.Select(s => new
        {
            s.Id, s.Cash, s.Cargo, s.DockedPortId, s.OriginPortId, s.DestinationPortId,
            s.RemainingTravelDays, s.AutomationEnabled,
            Decision = world.EvaluateAutomation(s.Id).Reason
        }).ToArray()
    };

    private static BigInteger TotalCash(RegionalWorldSnapshot snapshot) =>
        snapshot.Ports.Values.Aggregate(BigInteger.Zero, (total, port) => total + port.MarketCash) +
        snapshot.Ships.Values.Aggregate(BigInteger.Zero, (total, ship) => total + ship.Cash);

    private static long TotalGoods(RegionalWorldSnapshot snapshot, string good) =>
        snapshot.Ports.Values.Sum(port => (long)port.Inventory[good]) +
        snapshot.Ships.Values.Sum(ship => (long)ship.Cargo[good]);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Report
    {
        public int Version { get; } = 1;
        public int ObservationDays { get; } = ArchipelagoExperiments.ObservationDays;
        public string ConsumptionModel { get; } = "Physical sink; no household payments or cash replenishment.";
        public string DeliveryMetric { get; } = "Completed shipments end with empty cargo after a sale; partial sale orders are counted separately.";
        public string PlayerInputMetric { get; } = "Submitted manual orders or policy/enablement changes; daily advances excluded.";
        public List<ScenarioReport> Scenarios { get; } = new();
    }

    private sealed class ScenarioReport
    {
        public string Strategy { get; set; } = "";
        public object InitialConfiguration { get; set; } = new();
        public Summary Summary { get; set; } = new();
        public List<object> Daily { get; } = new();
        public List<object> Events { get; } = new();
    }

    private sealed class Summary
    {
        public long PlayerCash { get; set; }
        public int PlayerLogs { get; set; }
        public int PlayerPlanks { get; set; }
        public long RealizedClosedProfit { get; set; }
        public long? FinalEmptyCargoProfit { get; set; }
        public long OpenCargoCashChange { get; set; }
        public int PlayerInputs { get; set; }
        public long? LastPlayerInputDay { get; set; }
        public long? RouteChangedDay { get; set; }
        public long ExtractedLogs { get; set; }
        public long LogsTransformed { get; set; }
        public long PlanksProduced { get; set; }
        public long PlanksConsumed { get; set; }
        public long UnmetPlanks { get; set; }
        public int NpcDeliveries { get; set; }
        public long TotalTransportedUnits { get; set; }
        public int InactiveDays { get; set; }
        public int LongestInactivePeriod { get; set; }
        public string TotalCash { get; set; } = "";
        public long TotalLogs { get; set; }
        public long TotalPlanks { get; set; }
        public List<ActorTotals> ActorTotals { get; set; } = new();
    }

    private sealed class ActorTotals
    {
        public string ShipId { get; set; } = "";
        public long PurchasedUnits { get; set; }
        public long SoldUnits { get; set; }
        public long PurchaseCost { get; set; }
        public long SaleRevenue { get; set; }
        public int CompletedShipments { get; set; }
        public int SaleOrders { get; set; }
        public int Arrivals { get; set; }
        public long TransportedUnits { get; set; }
        public int SailingDays { get; set; }
        public int PolicyEvaluations { get; set; }
        public SortedDictionary<string, int> WaitingReasons { get; } = new(StringComparer.Ordinal);
    }
}
