using System;
using System.Globalization;

namespace SaltAndLedger.Simulation
{
    public enum RunWorldMode { Classic, Seeded, LivingCities }
    public enum RunDifficulty { Normal = 0, SlowFoundations = 1 }

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
        public bool ShouldSerializeLivingPricingMode() => WorldMode == RunWorldMode.LivingCities;
        public int Ascension => (int)Difficulty;
        public int ConstructionDelayDays => Difficulty == RunDifficulty.SlowFoundations ? 2 : 0;
        public string ComparisonGroup => (ChallengeId ?? "sandbox") + "/" + CharterId + "/ascension" +
            Ascension.ToString(CultureInfo.InvariantCulture) + "/" +
            (WorldMode == RunWorldMode.Classic ? "classic" :
                (WorldMode == RunWorldMode.LivingCities ? "living-v" : "seeded-v") +
                GeneratorVersion.ToString(CultureInfo.InvariantCulture) + "-seed" +
                Seed!.Value.ToString(CultureInfo.InvariantCulture) +
                (WorldMode == RunWorldMode.LivingCities ? "-" + LivingPricingMode.ToString().ToLowerInvariant() : ""));

        public RunIdentity(string? challengeId, string charterId, RunWorldMode worldMode = RunWorldMode.Classic,
            int? seed = null, RunDifficulty difficulty = RunDifficulty.Normal, int? generatorVersion = null,
            LivingPricingMode livingPricingMode = LivingPricingMode.StockOnly)
        {
            if (challengeId != null) RunScenario.GetChallenge(challengeId);
            RunScenario.GetCharter(charterId);
            if (!Enum.IsDefined(typeof(RunWorldMode), worldMode))
                throw new ArgumentOutOfRangeException(nameof(worldMode));
            if (difficulty != RunDifficulty.Normal && difficulty != RunDifficulty.SlowFoundations)
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            if ((worldMode != RunWorldMode.Classic) != seed.HasValue)
                throw new ArgumentException("Seeded worlds require a seed; Classic worlds have no seed.", nameof(seed));
            if (!Enum.IsDefined(typeof(LivingPricingMode), livingPricingMode) ||
                (worldMode != RunWorldMode.LivingCities && livingPricingMode != LivingPricingMode.StockOnly))
                throw new ArgumentOutOfRangeException(nameof(livingPricingMode));
            if (challengeId == RunScenario.RecoveryChallenge && worldMode != RunWorldMode.LivingCities)
                throw new ArgumentException("City Recovery requires a Living Cities world.", nameof(challengeId));
            var supportedVersion = worldMode == RunWorldMode.Classic ? 0 :
                worldMode == RunWorldMode.Seeded ? SeededArchipelago.CurrentVersion : LivingCityGeneration.CurrentVersion;
            if (generatorVersion.HasValue && generatorVersion.Value != supportedVersion)
                throw new ArgumentOutOfRangeException(nameof(generatorVersion), "Unsupported map generator version.");
            ChallengeId = challengeId;
            CharterId = charterId;
            WorldMode = worldMode;
            Seed = seed;
            GeneratorVersion = supportedVersion;
            Difficulty = difficulty;
            LivingPricingMode = livingPricingMode;
        }

        public bool Equals(RunIdentity? other) => other != null &&
            ChallengeId == other.ChallengeId && CharterId == other.CharterId && WorldMode == other.WorldMode &&
            Seed == other.Seed && GeneratorVersion == other.GeneratorVersion && Difficulty == other.Difficulty &&
            LivingPricingMode == other.LivingPricingMode;

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
