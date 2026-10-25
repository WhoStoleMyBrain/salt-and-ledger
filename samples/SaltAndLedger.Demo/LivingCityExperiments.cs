using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SaltAndLedger.Simulation;

/// <summary>Finite prescribed experiments over the same authoritative world used by Unity.</summary>
internal static partial class LivingCityExperiments
{
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Factory = "living_bakery";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, TypeInfoResolver = HistoricalReportJson.Resolver(), WriteIndented = true };
    private static readonly int[] NormalSeeds = { -42, 42, 99 };
    private static readonly string[] Strategies = { "idle", "merchant", "industry" };

    internal static int Run(ContentCatalog content, string? path, bool check, bool deep = false)
    {
        var timer = Stopwatch.StartNew();
        var report = BuildReport(content, deep);
        timer.Stop();
        var json = Serialize(report);
        path ??= deep ? ".cache/living-city-results-deep.json" : "docs/experiments/living-city-results.json";
        if (check)
            Require(File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == json,
                "Living-city report is stale; regenerate with --living-cities" + (deep ? " --deep" : "") + ".");
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        var all = report.Runs.Concat(report.Stability).Concat(report.Challenges).Concat(report.Diagnostics).ToArray();
        Console.WriteLine(FormattableString.Invariant($"Living cities v2: {report.Seeds.Length} seeds; {report.Runs.Count} comparative runs, {report.Stability.Count} multi-year runs, {report.Challenges.Count} challenge runs, {report.Diagnostics.Count} diagnostics."));
        Console.WriteLine(FormattableString.Invariant($"Measured {all.Sum(row => row.Days)} primary world-days in {timer.Elapsed.TotalSeconds:F3}s including diagnostic setup, audits and representative replay; this is not a large-world benchmark."));
        Console.WriteLine(FormattableString.Invariant($"Peak committed operation: {all.Max(row => row.MaximumEventsPerOperation)} structured events; {all.Max(row => row.MaximumOpenCostLots)} open private FIFO lots; bounded city food window {CitySnapshot.FoodWindowCapacity}."));
        foreach (var row in report.Challenges)
            Console.WriteLine(FormattableString.Invariant($"Recovery seed {row.Seed}, {row.Strategy}, A{row.Ascension}, {row.Pricing}: {row.Outcome}, day {row.Days}, player cash {row.FinalPlayerCash}."));
        Console.WriteLine("Every operation reconciled all market/household/company cash, explicit transfers, company material FIFO, paid wages and physical source/transform/sink flows.");
        Console.WriteLine("Replay, reversed configuration and tr-TR serialization verified; elapsed performance is deliberately excluded from golden JSON.");
        MeasureStepping(content);
        Console.WriteLine((check ? "Fresh report: " : "Wrote report: ") + path);
        return 0;
    }

    private static void MeasureStepping(ContentCatalog content)
    {
        var elapsed = TimeSpan.Zero;
        var days = 0;
        foreach (var seed in NormalSeeds)
            foreach (var pricing in new[] { LivingPricingMode.StockOnly, LivingPricingMode.Coverage })
            {
                var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter,
                    RunWorldMode.LivingCities, seed, livingPricingMode: pricing));
                var timer = Stopwatch.StartNew();
                for (var day = 0; day < 365; day++) world.AdvanceDay();
                timer.Stop(); elapsed += timer.Elapsed; days += 365;
            }
        Console.WriteLine(FormattableString.Invariant($"Stepping only: six warmed four-town/four-ship samples, {days} days in {elapsed.TotalMilliseconds:F3}ms ({elapsed.TotalMilliseconds / days:F4}ms/day mean). No observers/serialization in this timed loop; machine/load dependent, not a large-world scalability claim."));
    }

    internal static Report BuildReport(ContentCatalog content, bool deep = false)
    {
        var seeds = NormalSeeds.AsEnumerable();
        if (deep)
            seeds = seeds.Concat(new[] { int.MinValue, int.MaxValue, 0, 1, -1 })
                .Concat(Enumerable.Range(1, 17).Select(index => unchecked((int)(0x9E3779B9u * (uint)index))));
        var report = new Report { Seeds = seeds.Distinct().OrderBy(seed => seed).ToArray(), HorizonDays = deep ? 1095 : 365 };
        foreach (var seed in report.Seeds)
            foreach (var pricing in new[] { LivingPricingMode.StockOnly, LivingPricingMode.Coverage })
                foreach (var ascension in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
                    foreach (var strategy in Strategies)
                        report.Runs.Add(Observe(content, seed, pricing, ascension, strategy, report.HorizonDays));
        foreach (var pricing in new[] { LivingPricingMode.StockOnly, LivingPricingMode.Coverage })
            report.Stability.Add(Observe(content, 42, pricing, RunDifficulty.Normal, "idle", deep ? 3650 : 1825));

        AddDiagnostics(content, report);
        AddChallenges(content, report);
        var reference = report.Runs.First(row => row.Seed == 42 && row.Strategy == "industry" && row.Ascension == 1 && row.Pricing == "Coverage");
        Require(Serialize(reference) == Serialize(Observe(content, 42, LivingPricingMode.Coverage,
            RunDifficulty.SlowFoundations, "industry", report.HorizonDays, true)), "Reversed living-world configuration differs.");
        Require(Serialize(reference) == Serialize(Observe(content, 42, LivingPricingMode.Coverage,
            RunDifficulty.SlowFoundations, "industry", report.HorizonDays)), "Repeated living-world run differs.");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Require(Serialize(reference) == Serialize(Observe(content, 42, LivingPricingMode.Coverage,
                RunDifficulty.SlowFoundations, "industry", report.HorizonDays, true)), "Living economy depends on current culture.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return report;
    }

    private static RunReport Observe(ContentCatalog content, int seed, LivingPricingMode pricing,
        RunDifficulty difficulty, string strategy, int horizon, bool reverse = false, string? challenge = null,
        string charter = RunScenario.MerchantCharter)
    {
        var identity = new RunIdentity(challenge, charter, RunWorldMode.LivingCities, seed, difficulty,
            livingPricingMode: pricing);
        var generated = LivingCityGeneration.Generate(seed, pricingMode: pricing);
        var world = RunScenario.Create(content, identity, reverse);
        var observation = new Observation(content, world, strategy, seed, pricing, difficulty);
        observation.Report.Signature = generated.Signature;
        observation.Report.Identity = identity.ComparisonGroup;
        observation.Report.RecoveryPort = generated.RecoveryPortId;
        var initialFingerprint = Fingerprint(world.Snapshot());
        if (strategy == "merchant") observation.Apply(world.SetAutomationEnabled(Player, true));
        if (strategy == "industry")
        {
            var facility = world.Snapshot().PrivateFacilities.ContainsKey(RunScenario.StarterBakery) ? RunScenario.StarterBakery : Factory;
            if (facility == Factory)
                observation.Apply(world.ConstructFacility(Owner, generated.ProcessingPortId, EnterpriseScenario.BakeryBlueprint, facility));
            observation.Apply(world.ConfigureFacility(Owner, facility, LivingCityScenario.BakeryPolicy()));
            if (world.Snapshot().Ships[Player].DockedPortId != generated.ProcessingPortId)
            {
                observation.Apply(world.Depart(Player, generated.ProcessingPortId));
                while (world.Snapshot().Ships[Player].DockedPortId == null && IsActive(world.Snapshot())) observation.Step();
            }
            if (IsActive(world.Snapshot()))
                observation.Apply(world.ConfigureIndustrialAutomation(Player, LivingCityScenario.IndustrialPolicy(generated, facility), true));
        }
        while (world.Snapshot().Day < horizon && IsActive(world.Snapshot())) observation.Step();
        var row = observation.Complete();
        Require(initialFingerprint == Fingerprint(RunScenario.Create(content, identity, !reverse).Snapshot()), "Living retry changed initial accounts, capabilities or identity.");
        if (world.Snapshot().Challenge != null && !IsActive(world.Snapshot()))
        {
            var before = world.Snapshot();
            try { world.AdvanceDay(); throw new Exception("Terminal living run advanced."); }
            catch (InvalidOperationException) { }
            Require(ReferenceEquals(before, world.Snapshot()), "Terminal rejection changed snapshot identity.");
        }
        return row;
    }

    private static bool IsActive(RegionalWorldSnapshot snapshot) => snapshot.Challenge == null || snapshot.Challenge.Status == ChallengeStatus.Active;
    private static string Fingerprint(RegionalWorldSnapshot state) => Serialize(new
    {
        state.RunIdentity, state.Day, state.Revision, state.Companies,
        Ports = state.Ports.Values.Select(port => new
        {
            port.Id, port.X, port.Y, port.Role, port.Inventory, port.MarketCash, port.City, port.Consumption, port.Facilities,
            Quotes = port.Quotes.Values.Select(quote => new { quote.GoodId, quote.Pricing,
                FixedBid = quote.IsFixed ? (long?)quote.Bid : null, FixedAsk = quote.IsFixed ? (long?)quote.Ask : null })
        }), state.Ships,
        state.PrivateFacilities, state.IndustryBlueprints, state.Accounting, state.Challenge, state.Routes
    });
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options).Replace("\r\n", "\n") + "\n";
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Add(IDictionary<string, long> values, string key, long amount) =>
        values[key] = checked((values.TryGetValue(key, out var before) ? before : 0) + amount);
    private static long Stock(RegionalWorldSnapshot state, string good) =>
        state.Ports.Values.Sum(port => (long)port.Inventory[good]) + state.Ships.Values.Sum(ship => (long)ship.Cargo[good]) +
        state.PrivateFacilities.Values.Sum(facility => (long)facility.Inventory[good]);

    private sealed class Observation
    {
        private readonly ContentCatalog content;
        internal readonly RegionalWorldState World;
        internal readonly RunReport Report;
        private readonly BigInteger initialCash;
        private readonly SortedDictionary<string, GoodFlow> flows;
        private readonly Dictionary<string, CityTotals> cities;
        private readonly HashSet<string> milestones = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> firstBatches = new(StringComparer.Ordinal);

        internal Observation(ContentCatalog content, RegionalWorldState world, string strategy, int? seed = null,
            LivingPricingMode pricing = LivingPricingMode.StockOnly, RunDifficulty difficulty = RunDifficulty.Normal)
        {
            this.content = content; World = world;
            var state = world.Snapshot();
            initialCash = state.TotalCash;
            Report = new RunReport { Strategy = strategy, Seed = seed, Pricing = pricing.ToString(), Ascension = (int)difficulty,
                InitialTotalCash = initialCash.ToString(CultureInfo.InvariantCulture), InitialPlayerCash = state.Companies[Owner].Cash };
            flows = new SortedDictionary<string, GoodFlow>(content.Goods.ToDictionary(good => good.Id,
                good => new GoodFlow { Good = good.Id, Initial = Stock(state, good.Id) }, StringComparer.Ordinal), StringComparer.Ordinal);
            cities = state.Ports.Values.Where(port => port.City != null).ToDictionary(port => port.Id,
                port => new CityTotals { Port = port.Id, Role = port.Role, MinimumMarketCash = port.MarketCash,
                    MinimumHouseholdCash = port.City!.HouseholdCash, MinimumPopulation = port.City.Population,
                    MaximumPopulation = port.City.Population, MinimumProsperity = port.City.Prosperity, MaximumProsperity = port.City.Prosperity }, StringComparer.Ordinal);
            Capture();
        }

        internal void Step() => Apply(World.AdvanceDay());
        internal void Apply(RegionalResult result)
        {
            ValidateCash(result);
            foreach (var trade in result.Trades)
            {
                if (trade.Trade.Outcome != TradeOutcome.Traded) { Add(Report.Waits, "trade:" + trade.Trade.Outcome, 1); continue; }
                if (trade.OwnerId == Owner) Report.PlayerTrades++;
                else Report.NpcTrades++;
                if (cities.TryGetValue(trade.PortId, out var city))
                {
                    city.MerchantCashIn += Math.Max(0, trade.Trade.MarketCashChange.Delta);
                    city.MerchantCashOut += Math.Max(0, -trade.Trade.MarketCashChange.Delta);
                }
            }
            foreach (var production in result.Production)
                RecordProduction(production.RecipeId, production.Outcome, production.PortId + "/" + production.FacilityId, false);
            foreach (var production in result.PrivateProduction)
                RecordProduction(production.RecipeId, production.Outcome, production.FacilityId, true);
            foreach (var consumption in result.Consumption)
            {
                flows[consumption.GoodId].Consumed += consumption.ConsumedQuantity;
                var need = result.After.Ports[consumption.PortId].Consumption.Single(rule => rule.Configuration.Id == consumption.RuleId);
                Require(need.LastDesiredQuantity == need.LastConsumedQuantity + need.LastUnmetStock + need.LastUnmetAffordability,
                    "Need stock/affordability partition does not reconcile.");
                if (need.Configuration.Kind == ConsumptionKind.Municipal)
                    Require(need.LastPayment == 0, "Municipal own-use invented a retail payment.");
            }
            foreach (var flow in flows.Values)
            {
                Require(Stock(result.After, flow.Good) == flow.Initial + flow.Extracted + flow.TransformedOutputs - flow.TransformedInputs - flow.Consumed,
                    "Physical source/transform/sink reconciliation failed for " + flow.Good);
            }
            foreach (var decision in result.Decisions.Where(decision => decision.Action == AutomationAction.Wait || decision.Action == AutomationAction.Pause))
                Add(Report.Waits, "ship:" + decision.ShipId + ":" + decision.Reason, 1);
            foreach (var facility in result.After.PrivateFacilities.Values)
            {
                if (facility.ProcurementWaitReason != IndustryWaitReason.None) Add(Report.Waits, "procurement:" + facility.ProcurementWaitReason, 1);
                if (facility.SalesWaitReason != IndustryWaitReason.None) Add(Report.Waits, "private_sale:" + facility.SalesWaitReason, 1);
            }
            var events = result.Trades.Count + result.Production.Count + result.Consumption.Count + result.Movements.Count +
                result.Decisions.Count + result.WarehouseTrades.Count + result.PrivateProduction.Count + result.Construction.Count +
                result.Transfers.Count + result.EconomicEvents.Count + result.CashTransfers.Count + result.CityChanges.Count;
            Report.EventCount += events;
            Report.MaximumEventsPerOperation = Math.Max(Report.MaximumEventsPerOperation, events);
            Report.MaximumOpenCostLots = Math.Max(Report.MaximumOpenCostLots,
                result.After.Accounting.Stores.Values.Sum(store => store.Goods.Values.Sum(good => good.Lots.Count)));
            Report.Operations++;
            foreach (var port in result.After.Ports.Values.Where(port => port.City != null))
            {
                var city = port.City!; var totals = cities[port.Id];
                totals.MinimumMarketCash = Math.Min(totals.MinimumMarketCash, port.MarketCash);
                totals.MinimumHouseholdCash = Math.Min(totals.MinimumHouseholdCash, city.HouseholdCash);
                totals.MinimumPopulation = Math.Min(totals.MinimumPopulation, city.Population);
                totals.MaximumPopulation = Math.Max(totals.MaximumPopulation, city.Population);
                totals.MinimumProsperity = Math.Min(totals.MinimumProsperity, city.Prosperity);
                totals.MaximumProsperity = Math.Max(totals.MaximumProsperity, city.Prosperity);
                if (result.After.Day != result.Before.Day)
                {
                    if (city.LuxuryEnabled) totals.LuxuryEnabledDays++;
                    if (port.MarketCash == 0) totals.ZeroMarketCashDays++;
                    if (city.HouseholdCash == 0) totals.ZeroHouseholdCashDays++;
                    if (city.Population != result.Before.Ports[port.Id].City!.Population)
                        Milestone("population:" + port.Id + ":" + Math.Sign(city.Population - result.Before.Ports[port.Id].City!.Population), port.Id);
                    if (city.LuxuryEnabled != result.Before.Ports[port.Id].City!.LuxuryEnabled)
                        Milestone("luxury:" + port.Id + ":" + city.LuxuryEnabled, port.Id);
                    if (port.Consumption.Any(need => need.LastUnmetStock > 0)) Milestone("stock_shortage:" + port.Id, port.Id);
                    if (port.Consumption.Any(need => need.LastUnmetAffordability > 0)) Milestone("affordability_shortage:" + port.Id, port.Id);
                }
                Require(city.FoodWindow.Count <= CitySnapshot.FoodWindowCapacity, "Unbounded city need history.");
            }
            if (new long[] { 1, 2, 3, 4, 7, 12, 30, 60, 90, 180, 365, 730, 1095, 1825, 3650 }.Contains(result.After.Day) &&
                !Report.Timeline.Any(sample => sample.Day == result.After.Day)) Capture();

            void RecordProduction(string recipeId, ScheduledProductionOutcome outcome, string facilityId, bool privatelyOwned)
            {
                if (outcome != ScheduledProductionOutcome.Produced) { Add(Report.Waits, (privatelyOwned ? "private_batch:" : "public_batch:") + outcome, 1); return; }
                if (privatelyOwned && !firstBatches.ContainsKey(facilityId)) firstBatches.Add(facilityId, result.After.Day);
                var recipe = content.GetRecipe(recipeId);
                foreach (var input in recipe.Inputs) flows[input.GoodId].TransformedInputs += input.Quantity;
                foreach (var output in recipe.Outputs)
                    if (recipe.Inputs.Count == 0) flows[output.GoodId].Extracted += output.Quantity;
                    else flows[output.GoodId].TransformedOutputs += output.Quantity;
            }
        }

        private void ValidateCash(RegionalResult result)
        {
            Require(result.After.TotalCash == initialCash && result.Before.TotalCash == initialCash, "Combined named accounts changed money.");
            var accountDeltas = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
            void Delta(string account, BigInteger amount) => accountDeltas[account] = accountDeltas.GetValueOrDefault(account) + amount;
            foreach (var trade in result.Trades.Select(trade => (trade.OwnerId, trade.PortId, trade.Trade))
                .Concat(result.WarehouseTrades.Select(trade => (trade.OwnerId, trade.PortId, trade.Trade))))
            {
                Delta("company:" + trade.OwnerId, trade.Trade.TraderCashChange.Delta);
                Delta("market:" + trade.PortId, trade.Trade.MarketCashChange.Delta);
                Require(trade.Trade.TraderCashChange.Delta + trade.Trade.MarketCashChange.Delta == 0,
                    "Trade cash not conserved.");
            }
            foreach (var construction in result.Construction.Where(item => item.Cost > 0 &&
                (item.Outcome == ConstructionOutcome.Built || item.Outcome == ConstructionOutcome.Started)))
            { Delta("company:" + construction.OwnerId, -construction.Cost); Delta("market:" + construction.PortId, construction.Cost); }
            foreach (var transfer in result.CashTransfers)
            {
                Delta(transfer.SourceAccountId, -transfer.Amount); Delta(transfer.DestinationAccountId, transfer.Amount);
                Require(transfer.Amount >= 0 && transfer.Day == result.After.Day && transfer.Revision == result.After.Revision,
                    "Cash transfer does not describe its atomic commit.");
            }
            foreach (var company in result.After.Companies.Values)
            {
                var finance = result.After.Accounting.Companies[company.Id];
                Require((BigInteger)company.Cash == (BigInteger)finance.InitialCash + finance.SalesRevenue - finance.InventoryPurchases -
                    finance.ConstructionExpenditure - finance.WagesPaid, "Company cash failed material/capital/wage reconciliation.");
                Require((BigInteger)finance.InventoryPurchases == (BigInteger)finance.CostOfGoodsSold + finance.HeldInventoryCost,
                    "Company material FIFO expenditure does not reconcile.");
                Require(finance.OperatingSurplusAfterWages == finance.RealizedGrossMargin - finance.WagesPaid,
                    "Supported operating costs were mislabeled.");
                Require(company.Cash - (BigInteger)result.Before.Companies[company.Id].Cash == accountDeltas.GetValueOrDefault("company:" + company.Id),
                    "Named company cash transfers differ from actual balance.");
                Require(company.Cash - (BigInteger)result.Before.Companies[company.Id].Cash ==
                    result.EconomicEvents.Where(item => item.CompanyId == company.Id).Aggregate(BigInteger.Zero, (sum, item) => sum + item.CompanyCashDelta),
                    "Private economic ledger does not reconcile cash.");
            }
            foreach (var ship in result.After.Ships.Values)
                foreach (var good in ship.Cargo)
                    Require(good.Value == result.After.Accounting.Stores["ship:" + ship.Id].GetQuantity(good.Key),
                        "Ship FIFO quantities do not match physical goods.");
            foreach (var facility in result.After.PrivateFacilities.Values)
                foreach (var good in facility.Inventory)
                    Require(good.Value == result.After.Accounting.Stores["facility:" + facility.Id].GetQuantity(good.Key),
                        "Private warehouse FIFO quantities do not match physical goods.");
            foreach (var port in result.After.Ports.Values)
            {
                Require(port.MarketCash - (BigInteger)result.Before.Ports[port.Id].MarketCash == accountDeltas.GetValueOrDefault("market:" + port.Id),
                    "Named market transfers differ from actual balance.");
                if (port.City == null) continue;
                var before = result.Before.Ports[port.Id].City!;
                Require(port.City.HouseholdCash - (BigInteger)before.HouseholdCash == accountDeltas.GetValueOrDefault("households:" + port.Id),
                    "Named household transfers differ from actual balance.");
                Require(port.City.HouseholdCash == (BigInteger)port.City.Configuration.InitialHouseholdCash +
                    port.City.TotalWagesReceived - port.City.TotalRetailSpend, "Household finite income/payments do not reconcile.");
            }
        }

        private void Milestone(string key, string port)
        {
            if (!milestones.Add(key)) return;
            Report.Milestones.Add(new Milestone { Day = World.Snapshot().Day, Event = key, Port = port });
        }
        internal void Capture() => Report.Timeline.Add(new TimelineSample
        {
            Day = World.Snapshot().Day,
            PlayerCash = World.Snapshot().Companies[Owner].Cash,
            Towns = World.Snapshot().Ports.Values.Where(port => port.City != null).Select(port =>
            {
                var city = port.City!;
                var food = port.Consumption.Single(need => need.Configuration.Kind == ConsumptionKind.Essential);
                return new TownTrend
                {
                    Port = port.Id, Population = city.Population, Prosperity = city.Prosperity,
                    MarketCash = port.MarketCash, HouseholdCash = city.HouseholdCash,
                    Wages = city.TotalWagesReceived, RetailSpend = city.TotalRetailSpend,
                    BreadStock = port.Inventory["bread"], BreadAsk = World.Snapshot().GetMarketQuote(port.Id, "bread").Ask,
                    FoodDesired = food.TotalDesiredQuantity, FoodConsumed = food.TotalConsumedQuantity,
                    FoodUnmetStock = food.TotalUnmetStock, FoodUnmetAffordability = food.TotalUnmetAffordability,
                    LuxuryEnabled = city.LuxuryEnabled,
                    FurnitureConsumed = port.Consumption.Where(need => need.Configuration.Kind == ConsumptionKind.Luxury).Sum(need => need.TotalConsumedQuantity)
                };
            }).ToArray()
        });
        internal RunReport Complete()
        {
            var state = World.Snapshot();
            if (Report.Timeline.Last().Day != state.Day) Capture();
            Report.Days = state.Day; Report.Outcome = state.Challenge?.Status.ToString() ?? "Sandbox";
            Report.FinalPlayerCash = state.Companies[Owner].Cash;
            Report.FinalTotalCash = state.TotalCash.ToString(CultureInfo.InvariantCulture);
            var finance = state.Accounting.Companies[Owner];
            Report.PlayerFinance = new { finance.InventoryPurchases, finance.SalesRevenue, finance.CostOfGoodsSold,
                finance.RealizedGrossMargin, finance.WagesPaid, finance.OperatingSurplusAfterWages,
                finance.HeldInventoryCost, finance.ConstructionExpenditure, finance.HeldInventoryQuantities };
            Report.Challenge = state.Challenge;
            Report.Towns = state.Ports.Values.Where(port => port.City != null).Select(Town).ToArray();
            Report.CityTotals = cities.Values.OrderBy(city => city.Port, StringComparer.Ordinal).ToArray();
            Report.PrivateFacilities = state.PrivateFacilities.Values.Select(facility => new
            {
                facility.Id, facility.PortId, facility.OrderedDay, facility.ConstructionCompletionDay,
                facility.FirstProductionDay, FirstActualBatch = firstBatches.TryGetValue(facility.Id, out var day) ? (long?)day : null,
                facility.BatchesProduced, facility.ProcurementCost, facility.SalesRevenue,
                Status = facility.ConstructionStatus.ToString(), Inventory = facility.Inventory
            }).ToArray();
            foreach (var flow in flows.Values) flow.Final = Stock(state, flow.Good);
            Report.Goods = flows.Values.ToArray();
            return Report;
        }
        private TownSample Town(RegionalPortSnapshot port)
        {
            var city = port.City!;
            return new TownSample
            {
                Port = port.Id, Role = port.Role, Population = city.Population, PopulationDelta = city.PopulationDelta,
                Prosperity = city.Prosperity, ProsperityReason = city.ProsperityReason, MarketCash = port.MarketCash,
                HouseholdCash = city.HouseholdCash, Wages = city.TotalWagesReceived, RetailSpend = city.TotalRetailSpend,
                LuxuryEnabled = city.LuxuryEnabled, EssentialFulfillmentPercent = city.EssentialFulfillmentPercent,
                PurchasingPowerPercent = city.PurchasingPowerPercent, Stock = port.Inventory,
                PublicIndustries = port.Facilities.Select(facility => new
                {
                    facility.Configuration.Instance.BuildingId, facility.Configuration.Instance.RecipeId,
                    facility.Configuration.IntervalDays, facility.Configuration.WagePerBatch, facility.NextDueDay,
                    LastOutcome = facility.LastOutcome.ToString()
                }).ToArray(),
                EligiblePrivateBlueprints = World.Snapshot().IndustryBlueprints.Values.Where(blueprint => blueprint.EligiblePortIds.Contains(port.Id))
                    .Select(blueprint => blueprint.Id).ToArray(),
                Needs = port.Consumption.Select(need => new
                {
                    Id = need.Configuration.Id, Good = need.Configuration.GoodId, Kind = need.Configuration.Kind.ToString(),
                    need.NextDueDay, need.LastDesiredQuantity, need.LastConsumedQuantity, need.LastPayment, need.LastUnmetStock, need.LastUnmetAffordability,
                    need.TotalDesiredQuantity, need.TotalConsumedQuantity, need.TotalPayment, need.TotalUnmetStock, need.TotalUnmetAffordability
                }).ToArray(),
                Quotes = port.Inventory.Keys.Select(good =>
                {
                    var quote = World.Snapshot().GetMarketQuote(port.Id, good);
                    var configured = port.Quotes[good];
                    var pricing = configured.Pricing;
                    var buyQuantity = Math.Min(5, port.Inventory[good]);
                    var sellQuantity = Math.Min(5, int.MaxValue - port.Inventory[good]);
                    long? Depth(TradeDirection direction, int quantity) => quantity == 0 ? null :
                        pricing != null ? pricing.Quote(direction, port.Inventory[good], quantity).TotalAmount :
                        checked(quantity * (direction == TradeDirection.Buy ? configured.Ask : configured.Bid));
                    var bidTotal = Depth(TradeDirection.Sell, sellQuantity);
                    return new { Good = good, quote.Bid, quote.Ask, Target = pricing?.TargetStock,
                        BuyDepthQuantity = buyQuantity, BuyDepthTotal = Depth(TradeDirection.Buy, buyQuantity),
                        SellDepthQuantity = sellQuantity, SellDepthTotal = bidTotal,
                        MarketCanFundSellDepth = bidTotal.HasValue && bidTotal.Value <= port.MarketCash,
                        PhysicalHouseholdCoverageDays = LivingCityPricing.HouseholdCoverageDays(port.Configuration, city, good, port.Inventory[good]),
                        PlayerBuyMaximum = World.Snapshot().MaximumTradeQuantity(Player, port.Id, TradeDirection.Buy, good),
                        PlayerSellMaximum = World.Snapshot().MaximumTradeQuantity(Player, port.Id, TradeDirection.Sell, good) };
                }).ToArray()
            };
        }
    }

    internal sealed class Report
    {
        public int FormatVersion { get; } = 1;
        public int GeneratorVersion { get; } = 2;
        public string Methodology { get; } = "Three fixed v2 seeds, 365-day same-charter idle/merchant/constructed industry strategies, both ascensions, and stock-only versus finite-household seven-day coverage targets. Public/NPC activity remains enabled. Deep mode uses 25 seeds/1095 days. Two multi-year idle runs and controlled diagnostic scenarios separate missing food from affordability, finite payroll, liquidity recovery and durable demand. No hidden rescue, seed retuning or universal-win assertion. Wages are immediate period expense, not capitalized; material FIFO remains separate. Performance is printed, never part of deterministic JSON.";
        public int[] Seeds { get; set; } = Array.Empty<int>();
        public int HorizonDays { get; set; }
        public List<RunReport> Runs { get; } = new();
        public List<RunReport> Stability { get; } = new();
        public List<RunReport> Diagnostics { get; } = new();
        public List<RunReport> Challenges { get; } = new();
    }
    internal sealed class RunReport
    {
        public int? Seed { get; set; }
        public string Signature { get; set; } = "";
        public string Identity { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Pricing { get; set; } = "";
        public int Ascension { get; set; }
        public string RecoveryPort { get; set; } = "";
        public string InitialTotalCash { get; set; } = "";
        public string FinalTotalCash { get; set; } = "";
        public long InitialPlayerCash { get; set; }
        public long FinalPlayerCash { get; set; }
        public long Days { get; set; }
        public string Outcome { get; set; } = "";
        public int Operations { get; set; }
        public long EventCount { get; set; }
        public int MaximumEventsPerOperation { get; set; }
        public int MaximumOpenCostLots { get; set; }
        public long NpcTrades { get; set; }
        public long PlayerTrades { get; set; }
        public object? PlayerFinance { get; set; }
        public object? Challenge { get; set; }
        public object[] PrivateFacilities { get; set; } = Array.Empty<object>();
        public GoodFlow[] Goods { get; set; } = Array.Empty<GoodFlow>();
        public TownSample[] Towns { get; set; } = Array.Empty<TownSample>();
        public CityTotals[] CityTotals { get; set; } = Array.Empty<CityTotals>();
        public SortedDictionary<string, long> Waits { get; } = new(StringComparer.Ordinal);
        public List<TimelineSample> Timeline { get; } = new();
        public List<Milestone> Milestones { get; } = new();
        public string Interpretation { get; set; } = "";
    }
    internal sealed class GoodFlow
    {
        public string Good { get; set; } = "";
        public long Initial { get; set; }
        public long Extracted { get; set; }
        public long TransformedInputs { get; set; }
        public long TransformedOutputs { get; set; }
        public long Consumed { get; set; }
        public long Final { get; set; }
    }
    internal sealed class TownSample
    {
        public string Port { get; set; } = "";
        public string Role { get; set; } = "";
        public int Population { get; set; }
        public int PopulationDelta { get; set; }
        public int Prosperity { get; set; }
        public string ProsperityReason { get; set; } = "";
        public long MarketCash { get; set; }
        public long HouseholdCash { get; set; }
        public long Wages { get; set; }
        public long RetailSpend { get; set; }
        public bool LuxuryEnabled { get; set; }
        public int EssentialFulfillmentPercent { get; set; }
        public int PurchasingPowerPercent { get; set; }
        public IReadOnlyDictionary<string, int> Stock { get; set; } = new Dictionary<string, int>();
        public object[] Needs { get; set; } = Array.Empty<object>();
        public object[] Quotes { get; set; } = Array.Empty<object>();
        public object[] PublicIndustries { get; set; } = Array.Empty<object>();
        public string[] EligiblePrivateBlueprints { get; set; } = Array.Empty<string>();
    }
    internal sealed class CityTotals
    {
        public string Port { get; set; } = "";
        public string Role { get; set; } = "";
        public long MinimumMarketCash { get; set; }
        public long MinimumHouseholdCash { get; set; }
        public int MinimumPopulation { get; set; }
        public int MaximumPopulation { get; set; }
        public int MinimumProsperity { get; set; }
        public int MaximumProsperity { get; set; }
        public long LuxuryEnabledDays { get; set; }
        public long ZeroMarketCashDays { get; set; }
        public long ZeroHouseholdCashDays { get; set; }
        public long MerchantCashIn { get; set; }
        public long MerchantCashOut { get; set; }
    }
    internal sealed class TimelineSample
    {
        public long Day { get; set; }
        public long PlayerCash { get; set; }
        public TownTrend[] Towns { get; set; } = Array.Empty<TownTrend>();
    }
    internal sealed class TownTrend
    {
        public string Port { get; set; } = "";
        public int Population { get; set; }
        public int Prosperity { get; set; }
        public long MarketCash { get; set; }
        public long HouseholdCash { get; set; }
        public long Wages { get; set; }
        public long RetailSpend { get; set; }
        public int BreadStock { get; set; }
        public long? BreadAsk { get; set; }
        public long FoodDesired { get; set; }
        public long FoodConsumed { get; set; }
        public long FoodUnmetStock { get; set; }
        public long FoodUnmetAffordability { get; set; }
        public bool LuxuryEnabled { get; set; }
        public long FurnitureConsumed { get; set; }
    }
    internal sealed class Milestone
    {
        public long Day { get; set; }
        public string Event { get; set; } = "";
        public string Port { get; set; } = "";
    }
}
