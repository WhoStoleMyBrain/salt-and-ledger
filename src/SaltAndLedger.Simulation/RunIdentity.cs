using System;
using System.Globalization;

namespace SaltAndLedger.Simulation
{
    public enum RunWorldMode { Classic, Seeded, LivingCities, ExpandingEconomy }
    public enum RunDifficulty { Normal = 0, SlowFoundations = 1 }
    public enum CompanyStorageRules { Legacy = 1, Pooled = 2 }

    /// <summary>Immutable setup retained by snapshots, retries and result comparison groups.</summary>
    public sealed class RunIdentity : IEquatable<RunIdentity>
    {
        public string? ChallengeId { get; }
        public string CharterId { get; }
        public RunWorldMode WorldMode { get; }
        public int? Seed { get; }
        public int GeneratorVersion { get; }
        public RunDifficulty Difficulty { get; }
        public LivingPricingMode LivingPricingMode { get; }
        public GrandCommissionPlan? Commission { get; }
        public CompanyStorageRules CompanyStorageRules { get; }
        public int ItineraryVersion { get; }
        public int EconomicRulesVersion { get; }
        public bool ShouldSerializeEconomicRulesVersion() => EconomicRulesVersion != 0;
        public bool ShouldSerializeItineraryVersion() => ItineraryVersion != 0;
        public bool ShouldSerializeCompanyStorageRules() => CompanyStorageRules != CompanyStorageRules.Legacy;
        public bool ShouldSerializeCommission() => Commission != null;
        public bool ShouldSerializeLivingPricingMode() => WorldMode == RunWorldMode.LivingCities || WorldMode == RunWorldMode.ExpandingEconomy;
        public int ContentVersion => WorldMode == RunWorldMode.ExpandingEconomy ? 3 : 1;
        public int ValuationVersion => WorldMode == RunWorldMode.ExpandingEconomy ? 1 : 0;
        public bool ShouldSerializeContentVersion() => WorldMode == RunWorldMode.ExpandingEconomy;
        public bool ShouldSerializeValuationVersion() => WorldMode == RunWorldMode.ExpandingEconomy;
        public int Ascension => (int)Difficulty;
        public int ConstructionDelayDays => Difficulty == RunDifficulty.SlowFoundations ? 2 : 0;
        public string ComparisonGroup => (ChallengeId ?? "sandbox") + "/" + CharterId + "/ascension" +
            Ascension.ToString(CultureInfo.InvariantCulture) + "/" +
            (WorldMode == RunWorldMode.Classic ? "classic" :
                (WorldMode == RunWorldMode.ExpandingEconomy ? "expanding-v" : WorldMode == RunWorldMode.LivingCities ? "living-v" : "seeded-v") +
                GeneratorVersion.ToString(CultureInfo.InvariantCulture) + "-seed" +
                Seed!.Value.ToString(CultureInfo.InvariantCulture) +
                (ShouldSerializeLivingPricingMode() ? "-" + LivingPricingMode.ToString().ToLowerInvariant() : "") +
                (WorldMode == RunWorldMode.ExpandingEconomy ? "-content3-value1" : "")) +
            (Commission == null ? "" : "/commission-v" + Commission.Version.ToString(CultureInfo.InvariantCulture) + "-" + Commission.Signature) +
            (CompanyStorageRules == CompanyStorageRules.Pooled ? "/storage-v2" : "") +
            (ItineraryVersion == 0 ? "" : "/itinerary-v" + ItineraryVersion.ToString(CultureInfo.InvariantCulture)) +
            (EconomicRulesVersion == 0 ? "" : "/economy-v" + EconomicRulesVersion.ToString(CultureInfo.InvariantCulture));

        public RunIdentity(string? challengeId, string charterId, RunWorldMode worldMode = RunWorldMode.Classic,
            int? seed = null, RunDifficulty difficulty = RunDifficulty.Normal, int? generatorVersion = null,
            LivingPricingMode livingPricingMode = LivingPricingMode.StockOnly,
            int? contentVersion = null, int? valuationVersion = null, GrandCommissionPlan? commission = null,
            CompanyStorageRules companyStorageRules = CompanyStorageRules.Legacy, int itineraryVersion = 0,
            int economicRulesVersion = 0)
        {
            if (challengeId != null) RunScenario.GetChallenge(challengeId);
            RunScenario.GetCharter(charterId);
            if (!Enum.IsDefined(typeof(RunWorldMode), worldMode))
                throw new ArgumentOutOfRangeException(nameof(worldMode));
            if (!Enum.IsDefined(typeof(CompanyStorageRules), companyStorageRules) ||
                (companyStorageRules == CompanyStorageRules.Pooled && worldMode != RunWorldMode.ExpandingEconomy))
                throw new ArgumentOutOfRangeException(nameof(companyStorageRules));
            if (difficulty != RunDifficulty.Normal && difficulty != RunDifficulty.SlowFoundations)
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (itineraryVersion != 0 && (itineraryVersion != CargoItineraryPolicy.CurrentVersion ||
                worldMode != RunWorldMode.ExpandingEconomy || companyStorageRules != CompanyStorageRules.Pooled))
                throw new ArgumentOutOfRangeException(nameof(itineraryVersion));
            if (economicRulesVersion != 0 && (economicRulesVersion != 1 ||
                worldMode != RunWorldMode.ExpandingEconomy || companyStorageRules != CompanyStorageRules.Pooled ||
                itineraryVersion != CargoItineraryPolicy.CurrentVersion))
                throw new ArgumentOutOfRangeException(nameof(economicRulesVersion));
            if ((worldMode != RunWorldMode.Classic) != seed.HasValue)
                throw new ArgumentException("Seeded worlds require a seed; Classic worlds have no seed.", nameof(seed));
            if (!Enum.IsDefined(typeof(LivingPricingMode), livingPricingMode) ||
                (worldMode != RunWorldMode.LivingCities && worldMode != RunWorldMode.ExpandingEconomy && livingPricingMode != LivingPricingMode.StockOnly))
                throw new ArgumentOutOfRangeException(nameof(livingPricingMode));
            if (challengeId == RunScenario.RecoveryChallenge && worldMode != RunWorldMode.LivingCities)
                throw new ArgumentException("City Recovery requires a Living Cities world.", nameof(challengeId));
            if (challengeId != null && RunScenario.IsExpandingChallenge(challengeId) != (worldMode == RunWorldMode.ExpandingEconomy))
                throw new ArgumentException("Expanding Economy uses its own v3 objectives.", nameof(challengeId));
            if ((challengeId == GrandCommission.ChallengeId) != (commission != null))
                throw new ArgumentException("Grand Commission requires exactly one complete mission plan; other runs cannot carry one.", nameof(commission));
            if (commission != null && (worldMode != RunWorldMode.ExpandingEconomy || commission.Seed != seed))
                throw new ArgumentException("The commission must belong to this exact v3 seed.", nameof(commission));
            var supportedVersion = worldMode == RunWorldMode.Classic ? 0 :
                worldMode == RunWorldMode.Seeded ? SeededArchipelago.CurrentVersion :
                worldMode == RunWorldMode.LivingCities ? LivingCityGeneration.CurrentVersion : ExpandingEconomyGeneration.CurrentVersion;
            if (generatorVersion.HasValue && generatorVersion.Value != supportedVersion)
                throw new ArgumentOutOfRangeException(nameof(generatorVersion), "Unsupported map generator version.");
            if (contentVersion.HasValue && contentVersion.Value != (worldMode == RunWorldMode.ExpandingEconomy ? 3 : 1))
                throw new ArgumentOutOfRangeException(nameof(contentVersion), "Unsupported content version.");
            if (valuationVersion.HasValue && valuationVersion.Value != (worldMode == RunWorldMode.ExpandingEconomy ? 1 : 0))
                throw new ArgumentOutOfRangeException(nameof(valuationVersion), "Unsupported valuation version.");
            ChallengeId = challengeId;
            CharterId = charterId;
            WorldMode = worldMode;
            Seed = seed;
            GeneratorVersion = supportedVersion;
            Difficulty = difficulty;
            LivingPricingMode = livingPricingMode;
            Commission = commission;
            CompanyStorageRules = companyStorageRules;
            ItineraryVersion = itineraryVersion;
            EconomicRulesVersion = economicRulesVersion;
        }

        public bool Equals(RunIdentity? other) => other != null &&
            ChallengeId == other.ChallengeId && CharterId == other.CharterId && WorldMode == other.WorldMode &&
            Seed == other.Seed && GeneratorVersion == other.GeneratorVersion && Difficulty == other.Difficulty &&
            LivingPricingMode == other.LivingPricingMode && Commission?.Signature == other.Commission?.Signature &&
            CompanyStorageRules == other.CompanyStorageRules && ItineraryVersion == other.ItineraryVersion &&
            EconomicRulesVersion == other.EconomicRulesVersion;

        public override bool Equals(object? obj) => obj is RunIdentity other && Equals(other);

        public override int GetHashCode()
        {
            // Stable hashing is convenient for hosts, but the full comparison group remains the identity.
            unchecked
            {
                uint hash = 2166136261;
                foreach (var character in ComparisonGroup) hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }

        public override string ToString() => ComparisonGroup;
    }
}
