using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;

internal static class GrandCommissionWitness
{
    internal static int Run(ContentCatalog content)
    {
        foreach (var seed in new[] { 42, -42, 99, int.MinValue, int.MaxValue })
        {
            Console.WriteLine("Seed " + seed);
            foreach (var round in GrandCommissionDraft.Generate(seed).Rounds)
                Console.WriteLine(round.Category + ": " + string.Join(",", round.Offers.Select(o => o.Id)));
        }
        foreach (var seed in new[] { 99, 42 })
        {
            var world = Victory(content, seed, Console.WriteLine);
            Console.WriteLine(JsonConvert.SerializeObject(Summary(world.Snapshot()), Formatting.Indented));
        }
        var automated = Victory(content, 99, Console.WriteLine, itinerary: true);
        Console.WriteLine("Itinerary commission: " + JsonConvert.SerializeObject(Summary(automated.Snapshot())));
        var idle = Create(content, 99);
        while (idle.Snapshot().Challenge!.Status == ChallengeStatus.Active) idle.AdvanceDay();
        Require(idle.Snapshot().Challenge!.Status == ChallengeStatus.Failed, "Idle control must fail.");
        Console.WriteLine("Idle control: day " + idle.Snapshot().Day + ", Failed, " + GrandCommission.Progress(idle.Snapshot()).CurrentlyMetCount + "/4 goals.");
        return 0;
    }

    internal static RegionalWorldState Create(ContentCatalog content, int seed)
    {
        var goals = seed == 99 ? new[] { "all_harbors", "new_industry", "flour_for_bakers", "royal_institution" } :
            new[] { "all_harbors", "timber_chain", "royal_exporter", "royal_institution" };
        var plan = GrandCommissionDraft.Generate(seed).Resolve(goals);
        return RunScenario.Create(content, new RunIdentity(GrandCommission.ChallengeId,
            seed == 99 ? RunScenario.MerchantCharter : RunScenario.IndependentCharter,
            RunWorldMode.ExpandingEconomy, seed, commission: plan));
    }

    /// <summary>Two short prescribed journeys. Every state change uses an ordinary world command.</summary>
    internal static RegionalWorldState Victory(ContentCatalog content, int seed, Action<string>? observe = null, bool itinerary = false, long? finalArrivalDay = null)
    {
        var world = Create(content, seed);
        var g = ExpandingEconomyGeneration.Generate(seed);
        const string owner = WorldIds.PlayerOwnerId, ship = WorldIds.PlayerShipId;
        var stopAfter = new Dictionary<string, long>(StringComparer.Ordinal);
        void Apply(string text, Func<RegionalWorldState, RegionalResult> operation)
        {
            var result = operation(world);
            Require(!ReferenceEquals(result.Before, result.After), "Command did not commit: " + text);
            observe?.Invoke("D" + result.After.Day + " R" + result.After.Revision + ": " + text);
        }
        void Build(string id, string port, string blueprint, int batches)
        {
            Apply("Build " + id + " at " + port, w => w.ConstructFacility(owner, port, blueprint, id));
            stopAfter.Add(id, batches);
        }
        void BuyInput(string facility, string good, int whole)
        {
            Apply("Buy " + whole + " " + good + " for " + facility, w =>
            {
                var result = w.ExecuteWarehouseTrade(owner, facility, new TradeCommand(TradeDirection.Buy, good, whole * 10000));
                Require(result.WarehouseTrades.Single().Trade.Outcome == TradeOutcome.Traded, "Warehouse purchase rejected.");
                return result;
            });
        }
        void Trade(TradeDirection direction, string good, int whole)
        {
            Apply(direction + " " + whole + " " + good + " at " + world.Snapshot().Ships[ship].DockedPortId, w =>
            {
                var result = w.ExecuteTrade(ship, w.Snapshot().Ships[ship].DockedPortId!, new TradeCommand(direction, good, whole * 10000));
                Require(result.Trades.Single().Trade.Outcome == TradeOutcome.Traded, "Ship market order rejected.");
                return result;
            });
        }
        void Step()
        {
            Apply("Advance day", w => w.AdvanceDay());
            if (world.Snapshot().Challenge!.Status != ChallengeStatus.Active) return;
            foreach (var pair in stopAfter)
                if (world.Snapshot().PrivateFacilities[pair.Key].Enabled &&
                    world.Snapshot().PrivateFacilities[pair.Key].BatchesProduced >= pair.Value)
                    Apply("Disable completed production at " + pair.Key, w => w.SetFacilityEnabled(owner, pair.Key, false));
        }
        void Sail(string port, bool save = false)
        {
            if (world.Snapshot().Ships[ship].DockedPortId == port) return;
            Apply("Depart to " + port, w => w.Depart(ship, port));
            Step();
            if (save)
            {
                var encoded = WorldSaveCodec.Save(world);
                var loaded = WorldSaveCodec.Load(content, encoded);
                Require(WorldSaveCodec.Save(loaded) == encoded, "Mid-voyage save must round-trip exactly.");
                world = loaded;
                observe?.Invoke("Exact mid-voyage save/load at day " + world.Snapshot().Day);
            }
            while (world.Snapshot().Ships[ship].DockedPortId == null) Step();
        }
        string Site(string recipe, string? good = null) => g.EligiblePorts(recipe)
            .OrderByDescending(id => good == null ? 0 : world.Snapshot().Ports[id].Inventory[good])
            .ThenBy(id => id, StringComparer.Ordinal).First();
        if (seed == 42)
        {
            Trade(TradeDirection.Sell, "grain", 4);
            Trade(TradeDirection.Sell, "logs", 2);
            Build("witness_camp", Site("harvest_logs"), ExpandingEconomyGeneration.LumberBlueprint, 1);
            Build("witness_sawmill", Site("saw_planks", "logs"), WorldIds.SawmillBlueprint, 4);
            BuyInput("witness_sawmill", "logs", 8);
        }
        else
        {
            Build("witness_mill", Site("mill_flour", "grain"), ExpandingEconomyGeneration.MillBlueprint, 1);
            Build("witness_bakery", Site("bake_bread", "flour"), WorldIds.BakeryBlueprint, 1);
            BuyInput("witness_mill", "grain", 2);
            BuyInput("witness_bakery", "flour", 1);
            BuyInput("witness_bakery", "logs", 1);
        }
        var minePort = Site("mine_silver");
        Build("witness_mine", minePort, ExpandingEconomyGeneration.SilverMineBlueprint, 1);
        Apply("Commission public Mint", w => w.CommissionMint(owner));
        if (itinerary)
        {
            var origin = world.Snapshot().Ships[ship].DockedPortId!;
            var destination = world.Snapshot().Ports.Keys.Where(id => id != origin)
                .OrderBy(id => world.Snapshot().GetPath(origin, id, RunScenario.StartingShipSpeed).DurationDays)
                .ThenBy(id => id, StringComparer.Ordinal).First();
            var route = new CargoItineraryPolicy("witness_food", new[] {
                new CargoItineraryStop(origin, new[] {
                    new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "bread", 10000, 10000) }),
                new CargoItineraryStop(destination, new[] {
                    new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "bread", 10000, 10000) }) });
            Apply("Apply and enable food itinerary", w => w.ConfigureItinerary(ship, route, true));
            Apply("Itinerary buys Bread and departs", w => w.RunAutomation(ship));
            Require(world.Snapshot().Ships[ship].Cargo["bread"] == 10000 &&
                world.Snapshot().Ships[ship].DestinationPortId == destination, "Itinerary must buy and sail physically.");
            Step();
            var saved = WorldSaveCodec.Save(world);
            world = WorldSaveCodec.Load(content, saved);
            Require(WorldSaveCodec.Save(world) == saved, "Enabled mid-voyage itinerary save changed.");
            while (world.Snapshot().Ships[ship].DockedPortId == null) Step();
            Apply("Itinerary sells its Bread at the real dock", w => w.RunAutomation(ship, 1));
            Require(world.Snapshot().Ships[ship].CargoUnits == 0, "Itinerary must sell the actual delivery.");
            Apply("Pause itinerary for the remaining commission", w => w.SetAutomationEnabled(ship, false));
        }
        else
        {
            Trade(TradeDirection.Buy, "bread", 1);
            var destination = world.Snapshot().Ports.Keys.First(id => id != world.Snapshot().Ships[ship].DockedPortId);
            Sail(destination, save: true);
            Trade(TradeDirection.Sell, "bread", 1);
        }
        if (seed == 99)
        {
            var net = world.Snapshot().CommissionHistory!.Markets.FirstOrDefault(m => m.PortId == g.FoodSourcePortId && m.GoodId == "flour")?.NetQuantity ?? 0;
            var flourNeeded = checked((int)((40000 - net + 9999) / 10000));
            var source = world.Snapshot().Ports.Keys.Where(id => id != g.FoodSourcePortId &&
                    world.Snapshot().MaximumTradeQuantity(ship, id, TradeDirection.Buy, "flour") >= flourNeeded * 10000)
                .OrderByDescending(id => world.Snapshot().Ports[id].Inventory["flour"]).First();
            Sail(source);
            Trade(TradeDirection.Buy, "flour", flourNeeded);
            Sail(g.FoodSourcePortId, save: true);
            Trade(TradeDirection.Sell, "flour", flourNeeded);
        }
        else
        {
            var source = world.Snapshot().Ports.Keys.Where(id => world.Snapshot().MaximumTradeQuantity(ship, id, TradeDirection.Buy, "furniture") >= 10000)
                .OrderBy(id => id == world.Snapshot().Ships[ship].DockedPortId ? 0 : world.Snapshot().GetPath(world.Snapshot().Ships[ship].DockedPortId!, id, RunScenario.StartingShipSpeed).DurationDays)
                .ThenBy(id => id, StringComparer.Ordinal).First();
            Sail(source);
            Trade(TradeDirection.Buy, "furniture", 1);
            Sail(g.MintPortId);
            Apply("Fulfill finite Furniture contract", w => w.FulfillExternalContract(ship, "royal_furniture", 10000));
        }
        Sail(minePort);
        while (world.Snapshot().PrivateFacilities["witness_mine"].BatchesProduced == 0) Step();
        Apply("Load one physically produced Silver", w => w.TransferGoods(owner, ship, "witness_mine", TransferDirection.WarehouseToShip, "silver", 10000));
        Sail(g.MintPortId);
        Apply("Issue coins against physical Silver", w => w.MintSilver(ship));
        foreach (var port in world.Snapshot().Ports.Keys)
        {
            if (world.Snapshot().Challenge!.Status != ChallengeStatus.Active) break;
            if (!world.Snapshot().CommissionHistory!.Ships.Single().Visits.ContainsKey(port))
            {
                if (finalArrivalDay.HasValue && world.Snapshot().CommissionHistory!.Ships.Single().Visits.Count == world.Snapshot().Ports.Count - 1)
                {
                    var duration = world.Snapshot().GetPath(world.Snapshot().Ships[ship].DockedPortId!, port, RunScenario.StartingShipSpeed).DurationDays;
                    while (world.Snapshot().Day < finalArrivalDay.Value - duration) Step();
                }
                Sail(port, save: true);
            }
        }
        var final = world.Snapshot();
        Require(final.Challenge!.Status == ChallengeStatus.Succeeded && GrandCommission.Progress(final).Qualified,
            "Prescribed journey did not complete all four objectives: " + JsonConvert.SerializeObject(Summary(final)));
        var terminal = WorldSaveCodec.Save(world);
        Require(WorldSaveCodec.Save(WorldSaveCodec.Load(content, terminal)) == terminal, "Terminal save changed.");
        return world;
    }

    internal static object Summary(RegionalWorldSnapshot world)
    {
        var finance = world.Accounting.Companies[WorldIds.PlayerOwnerId];
        string Coins(long amount) => (amount / 10000m).ToString("0.####", CultureInfo.InvariantCulture);
        return new { Identity = world.RunIdentity!.ComparisonGroup, world.Day, world.Revision,
            Status = world.Challenge!.Status.ToString(), Cash = Coins(world.Companies[WorldIds.PlayerOwnerId].Cash),
            PaidInputs = Coins(finance.InventoryPurchases), PaidWages = Coins(finance.WagesPaid),
            PrivateCapital = Coins(finance.ConstructionExpenditure), PublicCommission = Coins(finance.PublicCommissionExpenditure),
            Production = finance.ProducedQuantities, Goals = GrandCommission.Progress(world).Goals,
            Score = world.Challenge.Status == ChallengeStatus.Active ? (int?)null : new RunResults(world).TotalScore };
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
