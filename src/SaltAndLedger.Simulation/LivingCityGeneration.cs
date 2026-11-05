using System;
using System.Collections.Generic;
using System.Globalization;

namespace SaltAndLedger.Simulation
{
    /// <summary>Read-only economic view of the one generalized generator; no separate role-placement algorithm.</summary>
    public sealed class LivingCityGeneration
    {
        private readonly ExpandingEconomyGeneration generation;
        public const int CurrentVersion = ExpandingEconomyGeneration.CurrentVersion;
        public int Seed => generation.Seed;
        public int GeneratorVersion => CurrentVersion;
        public LivingPricingMode PricingMode => generation.PricingMode;
        public ArchipelagoGeography Geography => generation.Geography;
        public IReadOnlyList<RegionalPortConfiguration> Ports => generation.Ports;
        public IReadOnlyList<IndustryBlueprint> Blueprints => generation.Blueprints;
        public string FoodSourcePortId => generation.FoodSourcePortId;
        public string ProcessingPortId => generation.ProcessingPortId;
        public string FurniturePortId => generation.FurniturePortId;
        public string RecoveryPortId => generation.RecoveryPortId;
        public string Signature => generation.Signature;
        internal LivingCityGeneration(ExpandingEconomyGeneration generation) { this.generation = generation; }
        public static LivingCityGeneration Generate(int seed, int version = CurrentVersion,
            LivingPricingMode pricingMode = LivingPricingMode.StockOnly, WorldGenerationConfiguration? configuration = null,
            ContentCatalog? content = null) => ExpandingEconomyGeneration.Generate(seed, version, pricingMode,
                configuration: configuration, content: content).SourceRoles;
        public static string PortName(string id)
        {
            if (id == null || !id.StartsWith("town_", StringComparison.Ordinal) ||
                !int.TryParse(id.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1 || number > 64)
                throw new ArgumentException("Unknown generated town ID: " + id, nameof(id));
            var prefixes = new[] { "Alder", "Brine", "Cedar", "Dun", "Elm", "Fair", "Glen", "High", "Iron", "Juniper", "Kings", "Laurel", "Mist", "Oak", "Pine", "Reed" };
            var suffixes = new[] { "haven", "port", "bay", "mouth" };
            return prefixes[(number - 1) % prefixes.Length] + suffixes[(number - 1) / prefixes.Length];
        }
    }
}
