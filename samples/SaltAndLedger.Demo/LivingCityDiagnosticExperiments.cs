using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;

internal static partial class LivingCityExperiments
{
    private const string DiagnosticTown = "diagnostic_town";

    private static void AddChallenges(ContentCatalog content, Report report)
    {
        foreach (var seed in NormalSeeds)
            foreach (var difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
                foreach (var strategy in Strategies)
                {
                    var row = Observe(content, seed, LivingPricingMode.StockOnly, difficulty, strategy, 90,
                        challenge: RunScenario.RecoveryChallenge);
                    row.Interpretation = strategy == "idle" ? "No intervention: ambient contributions are genuine regional welfare, not player-attributed supply." :
                        strategy == "merchant" ? "The player enables one finite Bread shipping route from the generated food source." :
                        "The player purchases a bakery at the generated processing town and ships its actual private output; payroll is company-funded.";
                    report.Challenges.Add(row);
                }
        Require(report.Challenges.Where(row => row.Strategy == "idle").All(row => row.Outcome == "Failed"),
            "An idle welfare baseline unexpectedly succeeds.");
        foreach (var strategy in new[] { "merchant", "industry" })
            Require(report.Challenges.Any(row => row.Strategy == strategy && row.Outcome == "Succeeded"),
                "No genuine recovery victory demonstrated for " + strategy + ".");
    }

    private static void AddDiagnostics(ContentCatalog content, Report report)
    {
        Run("missing_food_with_cash", Diagnostic(content, bread: 0, householdCash: 1000), 180,
            "Stock shortage: funded households cannot buy nonexistent Bread. No employer or replacement stock is added.");
        Run("stocked_food_without_cash", Diagnostic(content, bread: 400, householdCash: 0), 180,
            "Affordability shortage: Bread remains stocked while households have no income. Decline does not mint currency.");

        var bakery = new ScheduledFacilityConfiguration(new BuildingInstance("public_bakery", "bakery", "bake_bread"),
            1, wagePerBatch: 12);
        Run("closed_public_wage_retail_cycle", Diagnostic(content, bread: 0, householdCash: 12, marketCash: 100,
            facilities: new[] { bakery }, flour: 400, logs: 400), 365,
            "A hand-calculable loop: each completed bakery batch transfers 12 market cash to households; two Bread retail at 6 each returns 12. Finite 400 Flour/400 Logs fund 365 batches physically; population is fixed 20 for this isolated accounting control. No permanent sustainability claim.");
        Run("unfunded_public_payroll", Diagnostic(content, bread: 0, householdCash: 1000, marketCash: 11,
            facilities: new[] { bakery }, flour: 10, logs: 10), 180,
            "The 12-wage public bakery cannot run with 11 market cash despite available inputs and wealthy hungry households. No Bread means no retail recapitalization; this is a genuine monetary/physical deadlock.");

        var privateWorld = Diagnostic(content, bread: 30, householdCash: 0, marketCash: 0,
            privateFactory: true, flour: 0, logs: 0);
        var privateObserver = new Observation(content, privateWorld, "private_employer_income");
        privateObserver.Apply(privateWorld.ConfigureFacility(Owner, Factory,
            new IndustryPolicy(salesEnabled: true, outputs: new[] { new OutputSaleRule("bread", 0, 5) })));
        while (privateWorld.Snapshot().Day < 180) privateObserver.Step();
        var privateRow = privateObserver.Complete();
        privateRow.Interpretation = "A granted private bakery pays 12 company cash for each batch. Households buy actual public Bread; returned market cash can fund later private sales. Two Bread earn 10 at bid while labor costs 12: the resulting supported operating loss is not hidden by zero-basis starter inputs.";
        report.Diagnostics.Add(privateRow);

        var liquidity = Diagnostic(content, bread: 10, householdCash: 60, marketCash: 0, cargoPlanks: 1, npc: true);
        var liquidityObserver = new Observation(content, liquidity, "retail_recovers_market_liquidity");
        foreach (var ship in new[] { Player, "diagnostic_npc" })
        {
            var before = liquidity.Snapshot();
            var rejected = liquidity.ExecuteTrade(ship, DiagnosticTown, new TradeCommand(TradeDirection.Sell, "planks", 1));
            Require(rejected.Trades.Single().Trade.Outcome == TradeOutcome.InsufficientMarketFunds,
                "An insolvent market gave a player/NPC special payment permission.");
            Require(ReferenceEquals(before, liquidity.Snapshot()), "Rejected sale changed snapshot identity.");
            liquidityObserver.Apply(rejected);
        }
        liquidityObserver.Step();
        Require(liquidity.Snapshot().Ports[DiagnosticTown].MarketCash == 12,
            "Two actual6-cash Bread purchases did not recapitalize the market by12.");
        foreach (var ship in new[] { Player, "diagnostic_npc" })
        {
            var sold = liquidity.ExecuteTrade(ship, DiagnosticTown, new TradeCommand(TradeDirection.Sell, "planks", 1));
            Require(sold.Trades.Single().Trade.Outcome == TradeOutcome.Traded, "Retail-funded sale failed.");
            liquidityObserver.Apply(sold);
        }
        while (liquidity.Snapshot().Day < 180) liquidityObserver.Step();
        var liquidityRow = liquidityObserver.Complete();
        liquidityRow.Interpretation = "At day 0 both ships retain one Plank because market cash is 0. Day 1 households actually buy 2 Bread for 12; both player and NPC then receive 5 for one Plank each. No wage, bailout or synthetic market funding is present.";
        report.Diagnostics.Add(liquidityRow);

        var durable = Diagnostic(content, bread: 30, furniture: 20, householdCash: 5000,
            initialProsperity: 80, luxury: true);
        var durableObserver = new Observation(content, durable, "durable_demand_then_food_decline");
        while (durable.Snapshot().Day < 180) durableObserver.Step();
        var durableRow = durableObserver.Complete();
        Require(durableRow.CityTotals.Single().LuxuryEnabledDays > 0 && !durable.Snapshot().Ports[DiagnosticTown].City!.LuxuryEnabled,
            "Controlled durable demand did not activate then weaken after sustained food deprivation.");
        Require(durable.Snapshot().Ports[DiagnosticTown].Consumption.Single(need => need.Configuration.GoodId == "furniture").TotalConsumedQuantity > 0,
            "Rich households never made a real Furniture purchase.");
        durableRow.Interpretation = "High initial prosperity 80 and 5000 household cash still require sustained food observations before durable demand enables. Finite Bread runs out; sustained deprivation later disables Furniture despite residual cash and stock. Retail spend records only actually purchased units.";
        report.Diagnostics.Add(durableRow);

        foreach (var population in new[] { 10, 30 })
            Run("population_scale_" + population, Diagnostic(content, bread: 400, householdCash: 5000,
                population: population), 180,
                "All other need rates match: one Bread per 10 residents each day, rounded up. Population is held fixed only in this control so the demand difference is attributable to size.");

        void Run(string name, RegionalWorldState world, int days, string interpretation)
        {
            var observation = new Observation(content, world, name);
            while (world.Snapshot().Day < days) observation.Step();
            var row = observation.Complete(); row.Interpretation = interpretation;
            report.Diagnostics.Add(row);
        }
    }

    private static RegionalWorldState Diagnostic(ContentCatalog content, int bread, long householdCash,
        long marketCash = 1000, ScheduledFacilityConfiguration[]? facilities = null,
        int flour = 0, int logs = 0, int furniture = 0, int population = 20, int initialProsperity = 50,
        bool luxury = false, bool privateFactory = false, int cargoPlanks = 0, bool npc = false)
    {
        facilities ??= Array.Empty<ScheduledFacilityConfiguration>();
        var quotes = content.Goods.Select(good => good.Id == "bread" ? new MarketQuote(good.Id, 5, 6) :
            good.Id == "furniture" ? new MarketQuote(good.Id, 19, 20) : new MarketQuote(good.Id, 5, 6)).ToArray();
        var needs = new List<ConsumptionConfiguration>
        {
            new("daily_food", "bread", 1, 1, kind: ConsumptionKind.Essential, populationPerUnit: 10)
        };
        if (luxury) needs.Add(new ConsumptionConfiguration("durable_furniture", "furniture", 1, 6,
            kind: ConsumptionKind.Luxury, populationPerUnit: 20));
        var port = new RegionalPortConfiguration(new PortConfiguration(DiagnosticTown, quotes, marketCash,
            facilities.Select(facility => facility.Instance), new Dictionary<string, int>
            { ["bread"] = bread, ["flour"] = flour, ["logs"] = logs, ["furniture"] = furniture }),
            "Diagnostic town", 0, 0, facilities, needs, "Controlled explicit endowments and need schedule",
            new SettlementConfiguration(population, householdCash, initialProsperity, population, population));
        var other = new RegionalPortConfiguration(new PortConfiguration("other_town", quotes, 100,
            Array.Empty<BuildingInstance>()), "Other town", 100, 0, Array.Empty<ScheduledFacilityConfiguration>(),
            Array.Empty<ConsumptionConfiguration>());
        var ships = new List<RegionalShipConfiguration>
        {
            new(new ShipConfiguration(Player, 20, DiagnosticTown, new Dictionary<string, int> { ["planks"] = cargoPlanks }),
                Owner, "Player", 0, 100, isPlayer: true)
        };
        var companies = new List<CompanyConfiguration> { new(Owner, 600) };
        if (npc)
        {
            ships.Add(new RegionalShipConfiguration(new ShipConfiguration("diagnostic_npc", 20, DiagnosticTown,
                new Dictionary<string, int> { ["planks"] = cargoPlanks }), "npc_company", "NPC", 0, 100));
            companies.Add(new CompanyConfiguration("npc_company", 50));
        }
        var blueprint = new IndustryBlueprint("diagnostic_bakery", "bakery", "bake_bread", 100, 1, 300,
            new[] { DiagnosticTown }, wagePerBatch: 12);
        var grants = privateFactory ? new[] { new PrivateFacilityConfiguration(Factory, Owner, DiagnosticTown,
            blueprint.Id, new Dictionary<string, int> { ["flour"] = 100, ["logs"] = 100 }, provenance: "diagnostic_grant") }
            : Array.Empty<PrivateFacilityConfiguration>();
        return new RegionalWorldState(content, new[] { port, other }, new[] { new SeaRoute(DiagnosticTown, other.Id, 100) },
            ships, companies: companies, industryBlueprints: new[] { blueprint }, initialFacilities: grants);
    }
}
