using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Pure v3 configuration: unchanged v1 geography and v2 role draws, explicit scaled economics.</summary>
    public sealed class ExpandingEconomyGeneration
    {
        public const int CurrentVersion = 3;
        public const string FarmBlueprint = "private_farm";
        public const string LumberBlueprint = "private_lumber_camp";
        public const string MillBlueprint = "private_mill";
        public const string SilverMineBlueprint = "private_silver_mine";
        public int Seed => SourceRoles.Seed;
        public int GeneratorVersion => CurrentVersion;
        public LivingPricingMode PricingMode => SourceRoles.PricingMode;
        public LivingCityGeneration SourceRoles { get; }
        public ArchipelagoGeography Geography => SourceRoles.Geography;
        public string FoodSourcePortId => SourceRoles.FoodSourcePortId;
        public string ProcessingPortId => SourceRoles.ProcessingPortId;
        public string FurniturePortId => SourceRoles.FurniturePortId;
        public string RecoveryPortId => SourceRoles.RecoveryPortId;
        public string SilverPortId => FurniturePortId;
        public string MintPortId => ProcessingPortId;
        public IReadOnlyList<RegionalPortConfiguration> Ports { get; }
        public IReadOnlyList<IndustryBlueprint> Blueprints { get; }
        public string Signature { get; }

        private ExpandingEconomyGeneration(LivingCityGeneration source)
        {
            SourceRoles = source;
            Ports = Array.AsReadOnly(source.Ports.Select(ScalePort).ToArray());
            Blueprints = Array.AsReadOnly(new[]
            {
                Blueprint(FarmBlueprint, "farm", "harvest_grain", 240, 2, 60, 48, 8, FoodSourcePortId),
                Blueprint(LumberBlueprint, "lumber_camp", "harvest_logs", 220, 2, 40, 36, 6,
                    FoodSourcePortId, FurniturePortId, RecoveryPortId),
                Blueprint(MillBlueprint, "mill", "mill_flour", 260, 2, 40, 24, 6, ProcessingPortId),
                Blueprint(EnterpriseScenario.SawmillBlueprint, "sawmill", "saw_planks", 280, 2, 40, 20, 6, ProcessingPortId),
                Blueprint(EnterpriseScenario.BakeryBlueprint, "bakery", "bake_bread", 300, 2, 40, 30, 6,
                    FoodSourcePortId, ProcessingPortId, RecoveryPortId),
                Blueprint(EnterpriseScenario.WorkshopBlueprint, "furniture_workshop", "make_furniture", 360, 3, 40, 15, 8,
                    FurniturePortId, RecoveryPortId),
                Blueprint(SilverMineBlueprint, "silver_mine", "mine_silver", 320, 4, 20, 8, 12, SilverPortId)
            }.OrderBy(blueprint => blueprint.Id, StringComparer.Ordinal).ToArray());
            var records = new List<string> { "expanding-v3", "content3", "value1", "quantity10000", "money10000", source.Signature };
            foreach (var blueprint in Blueprints)
                records.Add(FormattableString.Invariant($"private:{blueprint.Id}:{blueprint.BuildingId}:{blueprint.RecipeId}:{blueprint.ConstructionCost}:{blueprint.IntervalDays}:{blueprint.WarehouseCapacity}:{blueprint.OutputStockLimit}:{blueprint.WagePerBatch}:") + string.Join(",", blueprint.EligiblePortIds));
            records.Add("silver:stock0:reference40:target40000:sensitivity10:min1:max100:spread1");
            records.Add("household:proportional:bread4:furniture24:municipal12");
            Signature = string.Join("|", records);
        }

        public static ExpandingEconomyGeneration Generate(int seed, int version = CurrentVersion,
            LivingPricingMode pricingMode = LivingPricingMode.StockOnly)
        {
            if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version), "Unsupported expanding economy generator version.");
            return new ExpandingEconomyGeneration(LivingCityGeneration.Generate(seed, pricingMode: pricingMode));
        }

        private RegionalPortConfiguration ScalePort(RegionalPortConfiguration source)
        {
            var stocks = source.Market.InitialQuantities.ToDictionary(pair => pair.Key,
                pair => EconomicUnits.FromWhole(pair.Value), StringComparer.Ordinal);
            stocks.Add("silver", 0);
            var quotes = source.Market.Quotes.Values.Select(ScaleQuote).ToList();
            quotes.Add(new MarketQuote("silver", new InventoryPricing(InventoryPricingModel.Linear,
                40, EconomicUnits.FromWhole(4), 10, 1, 100, 1)));
            var facilities = source.Facilities.Select(facility => new ScheduledFacilityConfiguration(
                facility.Instance, facility.IntervalDays,
                facility.OutputStockLimit.HasValue ? EconomicUnits.FromWhole(facility.OutputStockLimit.Value) : (int?)null,
                facility.FirstDueDay, EconomicUnits.Coins(facility.WagePerBatch))).ToArray();
            var needs = source.Consumption.Select(need => new ConsumptionConfiguration(need.Id, need.GoodId,
                EconomicUnits.FromWhole(need.Quantity), need.IntervalDays, need.FirstDueDay,
                need.Kind, need.PopulationPerUnit,
                proportional: need.Kind == ConsumptionKind.Essential || need.Kind == ConsumptionKind.Luxury));
            var settlement = source.Settlement!;
            var role = source.Role + (source.Id == SilverPortId ? "; eligible Silver Mine, exports Silver to public Mint" :
                source.Id == MintPortId ? "; sole public Royal Mint and shipwright harbor" : "");
            return new RegionalPortConfiguration(new PortConfiguration(source.Id, quotes,
                EconomicUnits.Coins(source.Market.InitialMarketCash), facilities.Select(facility => facility.Instance), stocks),
                source.Name, source.X, source.Y, facilities, needs, role,
                new SettlementConfiguration(settlement.InitialPopulation, EconomicUnits.Coins(settlement.InitialHouseholdCash),
                    settlement.InitialProsperity, settlement.MinimumPopulation, settlement.MaximumPopulation,
                    settlement.DemographicIntervalDays, PricingMode, EconomicUnits.QuantityScale));
        }

        private static MarketQuote ScaleQuote(MarketQuote source)
        {
            if (source.Pricing == null) return new MarketQuote(source.GoodId, source.Bid, source.Ask);
            var pricing = source.Pricing;
            return new MarketQuote(source.GoodId, new InventoryPricing(pricing.Model, pricing.ReferencePrice,
                EconomicUnits.FromWhole(pricing.TargetStock), pricing.Sensitivity,
                pricing.MinPrice, pricing.MaxPrice, pricing.Spread));
        }

        private static IndustryBlueprint Blueprint(string id, string building, string recipe, long cost,
            int interval, int capacity, int outputLimit, long wages, params string[] ports) =>
            new IndustryBlueprint(id, building, recipe, EconomicUnits.Coins(cost), interval,
                EconomicUnits.FromWhole(capacity), ports, EconomicUnits.FromWhole(outputLimit), EconomicUnits.Coins(wages));
    }
}
