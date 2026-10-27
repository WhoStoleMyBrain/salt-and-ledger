using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Versioned, bounded economic role permutation over unchanged v1 integer geography.</summary>
    public sealed class LivingCityGeneration
    {
        public const int CurrentVersion = 2;
        public int Seed { get; }
        public int GeneratorVersion => CurrentVersion;
        public LivingPricingMode PricingMode { get; }
        public ArchipelagoGeography Geography { get; }
        public IReadOnlyList<RegionalPortConfiguration> Ports { get; }
        public IReadOnlyList<IndustryBlueprint> Blueprints { get; }
        public string FoodSourcePortId { get; }
        public string ProcessingPortId { get; }
        public string FurniturePortId { get; }
        public string RecoveryPortId { get; }
        public string Signature { get; }

        private LivingCityGeneration(int seed, LivingPricingMode pricingMode, string[] rolePorts,
            int[] populationBands, int[] liquidityBands)
        {
            Seed = seed;
            PricingMode = pricingMode;
            Geography = SeededArchipelago.Generate(seed);
            FoodSourcePortId = rolePorts[0];
            ProcessingPortId = rolePorts[1];
            FurniturePortId = rolePorts[2];
            RecoveryPortId = rolePorts[3];
            var ports = new[]
            {
                Port(rolePorts[0], "Food source: Farm, Lumber Camp and Bakery; imports Flour", 1200 + liquidityBands[0] * 100,
                    20 + populationBands[0] * 5, 30, 65, Stock(18, 2, 20, 8, 16, 1),
                    Quotes(10, 26, 7, 21, 24, 64),
                    new[] { Facility(rolePorts[0], "farm", "harvest_grain", 2, 48, 12),
                        Facility(rolePorts[0], "lumber_camp", "harvest_logs", 2, 36, 10),
                        Facility(rolePorts[0], "bakery", "bake_bread", 2, 24, 10) }),
                Port(rolePorts[1], "Processing: Mill and Sawmill; imports Grain, Logs and Bread", 1000 + liquidityBands[1] * 100,
                    20 + populationBands[1] * 5, 26, 60, Stock(12, 8, 18, 10, 6, 1),
                    Quotes(18, 18, 16, 15, 38, 64),
                    new[] { Facility(rolePorts[1], "mill", "mill_flour", 2, 24, 10),
                        Facility(rolePorts[1], "sawmill", "saw_planks", 2, 20, 10) }),
                Port(rolePorts[2], "Forest furniture: Lumber Camp and Furniture Workshop; no Sawmill; imports Planks and Bread",
                    1400 + liquidityBands[2] * 100, 30 + populationBands[2] * 5, 42, 75,
                    Stock(12, 6, 0, 0, 6, 2), Quotes(10, 30, 16, 26, 38, 52),
                    new[] { Facility(rolePorts[2], "lumber_camp", "harvest_logs", 3, 30, 12),
                        Facility(rolePorts[2], "furniture_workshop", "make_furniture", 5, 8, 16) }),
                Port(rolePorts[3], "Vulnerable coast: Lumber Camp; no public food production; imports Bread and Planks",
                    300 + liquidityBands[3] * 50, 20 + populationBands[3] * 5, 22, 40,
                    Stock(4, 1, 0, 2, 0, 0), Quotes(9, 28, 18, 23, 38, 68),
                    new[] { Facility(rolePorts[3], "lumber_camp", "harvest_logs", 3, 28, 12) })
            };
            Ports = Array.AsReadOnly(ports.OrderBy(port => port.Id, StringComparer.Ordinal).ToArray());
            Blueprints = Array.AsReadOnly(new[]
            {
                new IndustryBlueprint(WorldIds.SawmillBlueprint, "sawmill", "saw_planks", 280, 2, 40,
                    new[] { ProcessingPortId }, 20, wagePerBatch: 6),
                new IndustryBlueprint(WorldIds.BakeryBlueprint, "bakery", "bake_bread", 300, 2, 40,
                    new[] { FoodSourcePortId, ProcessingPortId, RecoveryPortId }, 30, wagePerBatch: 6),
                new IndustryBlueprint(WorldIds.WorkshopBlueprint, "furniture_workshop", "make_furniture", 360, 3, 40,
                    new[] { FurniturePortId, RecoveryPortId }, 15, wagePerBatch: 8)
            }.OrderBy(blueprint => blueprint.Id, StringComparer.Ordinal).ToArray());
            Signature = CreateSignature();
        }

        /// <summary>Exactly eleven economic draws; no strategy evaluation, retries, repairs or runtime entropy.</summary>
        public static LivingCityGeneration Generate(int seed, int version = CurrentVersion,
            LivingPricingMode pricingMode = LivingPricingMode.StockOnly)
        {
            if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version), "Unsupported economic generator version.");
            if (!Enum.IsDefined(typeof(LivingPricingMode), pricingMode)) throw new ArgumentOutOfRangeException(nameof(pricingMode));
            var sequence = new EconomicSequence(seed);
            var roles = new[] { WorldIds.East, WorldIds.North, WorldIds.South, WorldIds.West };
            for (var index = roles.Length - 1; index > 0; index--)
            {
                var selected = sequence.Next(index + 1);
                var previous = roles[index];
                roles[index] = roles[selected];
                roles[selected] = previous;
            }
            var population = new int[4];
            var liquidity = new int[4];
            for (var index = 0; index < 4; index++)
            {
                population[index] = sequence.Next(3);
                liquidity[index] = sequence.Next(3);
            }
            return new LivingCityGeneration(seed, pricingMode, roles, population, liquidity);
        }

        private RegionalPortConfiguration Port(string id, string role, long marketCash, int population,
            int householdCashPerResident, int prosperity, Dictionary<string, int> stock,
            MarketQuote[] quotes, ScheduledFacilityConfiguration[] facilities)
        {
            var position = Geography.Positions[id];
            return new RegionalPortConfiguration(new PortConfiguration(id, quotes, marketCash,
                facilities.Select(facility => facility.Instance), stock), PortName(id), position.X, position.Y,
                facilities, new[]
                {
                    new ConsumptionConfiguration(id + "_food", "bread", 1, 4,
                        kind: ConsumptionKind.Essential, populationPerUnit: 20),
                    new ConsumptionConfiguration(id + "_durables", "furniture", 1, 24,
                        kind: ConsumptionKind.Luxury, populationPerUnit: 80),
                    new ConsumptionConfiguration(id + "_municipal", "planks", 1, 12,
                        kind: ConsumptionKind.Municipal, populationPerUnit: 40)
                }, role, new SettlementConfiguration(population, checked((long)population * householdCashPerResident),
                    prosperity, minimumPopulation: 10, maximumPopulation: 80, demographicIntervalDays: 12,
                    pricingMode: PricingMode));
        }

        private string CreateSignature()
        {
            var records = new List<string>
            {
                "living-v2", Seed.ToString(CultureInfo.InvariantCulture), PricingMode.ToString(), Geography.Signature,
                "roles:" + string.Join(",", new[] { FoodSourcePortId, ProcessingPortId, FurniturePortId, RecoveryPortId })
            };
            foreach (var port in Ports)
            {
                var settlement = port.Settlement!;
                records.Add(FormattableString.Invariant($"port:{port.Id}:{port.Role}:{port.Market.InitialMarketCash}:{settlement.InitialPopulation}:{settlement.InitialHouseholdCash}:{settlement.InitialProsperity}:{settlement.MinimumPopulation}:{settlement.MaximumPopulation}:{settlement.DemographicIntervalDays}"));
                records.Add("stock:" + string.Join(",", port.Market.InitialQuantities.Select(stock => FormattableString.Invariant($"{stock.Key}={stock.Value}"))));
                foreach (var quote in port.Market.Quotes.Values)
                {
                    var pricing = quote.Pricing!;
                    records.Add(FormattableString.Invariant($"quote:{quote.GoodId}:{pricing.Model}:{pricing.ReferencePrice}:{pricing.TargetStock}:{pricing.Sensitivity}:{pricing.MinPrice}:{pricing.MaxPrice}:{pricing.Spread}"));
                }
                foreach (var facility in port.Facilities)
                    records.Add(FormattableString.Invariant($"public:{facility.Instance.Id}:{facility.Instance.BuildingId}:{facility.Instance.RecipeId}:{facility.Instance.Priority}:{facility.Instance.ProductionEnabled}:{facility.IntervalDays}:{facility.FirstDueDay}:{facility.OutputStockLimit}:{facility.WagePerBatch}"));
                foreach (var need in port.Consumption)
                    records.Add(FormattableString.Invariant($"need:{need.Id}:{need.GoodId}:{need.Kind}:{need.Quantity}:{need.PopulationPerUnit}:{need.IntervalDays}:{need.FirstDueDay}"));
            }
            foreach (var blueprint in Blueprints)
                records.Add(FormattableString.Invariant($"private:{blueprint.Id}:{blueprint.BuildingId}:{blueprint.RecipeId}:{blueprint.ConstructionCost}:{blueprint.IntervalDays}:{blueprint.WarehouseCapacity}:{blueprint.OutputStockLimit}:{blueprint.WagePerBatch}:") + string.Join(",", blueprint.EligiblePortIds));
            return string.Join("|", records);
        }

        public static string PortName(string id)
        {
            switch (id)
            {
                case WorldIds.North: return "Northport";
                case WorldIds.South: return "Southhaven";
                case WorldIds.East: return "Eastport";
                case WorldIds.West: return "Westhaven";
                default: throw new ArgumentException("Unknown living-city port.", nameof(id));
            }
        }

        private static ScheduledFacilityConfiguration Facility(string port, string building, string recipe,
            int interval, int limit, long wage) => new ScheduledFacilityConfiguration(
                new BuildingInstance(port + "_" + building, building, recipe), interval, limit, wagePerBatch: wage);

        private static Dictionary<string, int> Stock(int logs, int planks, int grain, int flour, int bread, int furniture) =>
            new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks, ["grain"] = grain,
                ["flour"] = flour, ["bread"] = bread, ["furniture"] = furniture };

        private static MarketQuote[] Quotes(long logs, long planks, long grain, long flour, long bread, long furniture) =>
            new[] { Linear("logs", logs, 16, 6, 60), Linear("planks", planks, 8, 8, 70),
                Linear("grain", grain, 16, 4, 30), Linear("flour", flour, 8, 6, 50),
                Linear("bread", bread, 8, 8, 80), Linear("furniture", furniture, 4, 12, 120) };

        private static MarketQuote Linear(string id, long reference, int target, long sensitivity, long ceiling) =>
            new MarketQuote(id, new InventoryPricing(InventoryPricingModel.Linear, reference, target,
                sensitivity, 1, ceiling, 1));

        private struct EconomicSequence
        {
            private uint state;
            internal EconomicSequence(int seed) { state = unchecked((uint)seed ^ 0xA511E9B3u); }
            internal int Next(int upperExclusive)
            {
                unchecked
                {
                    state += 0x9E3779B9u;
                    var value = state;
                    value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
                    value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                    value ^= value >> 16;
                    return (int)(value % (uint)upperExclusive);
                }
            }
        }
    }
}
