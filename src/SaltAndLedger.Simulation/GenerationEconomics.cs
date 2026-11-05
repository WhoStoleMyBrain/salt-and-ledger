using System;
using System.Collections.Generic;

namespace SaltAndLedger.Simulation
{
    /// <summary>Canonical existing economic specifications, independent of distribution configuration.</summary>
    internal static class GenerationEconomics
    {
        internal sealed class IndustrySpec
        {
            internal int Cost { get; }
            internal int Interval { get; }
            internal int Capacity { get; }
            internal int OutputLimit { get; }
            internal int Wages { get; }
            internal int PublicWages { get; }
            internal IndustrySpec(int cost, int interval, int capacity, int outputLimit, int wages, int publicWages)
            { Cost = cost; Interval = interval; Capacity = capacity; OutputLimit = outputLimit; Wages = wages; PublicWages = publicWages; }
        }
        internal sealed class GoodSpec
        {
            internal int Reference { get; }
            internal int Target { get; }
            internal int Sensitivity { get; }
            internal int Maximum { get; }
            internal GoodSpec(int reference, int target, int sensitivity, int maximum)
            { Reference = reference; Target = target; Sensitivity = sensitivity; Maximum = maximum; }
        }
        private static readonly IReadOnlyDictionary<string, IndustrySpec> Industries = new Dictionary<string, IndustrySpec>(StringComparer.Ordinal)
        {
            ["farm"] = new IndustrySpec(240, 2, 60, 48, 8, 12),
            ["lumber_camp"] = new IndustrySpec(220, 2, 40, 36, 6, 10),
            ["mill"] = new IndustrySpec(260, 2, 40, 24, 6, 10),
            ["sawmill"] = new IndustrySpec(280, 2, 40, 20, 6, 10),
            ["bakery"] = new IndustrySpec(300, 2, 40, 30, 6, 10),
            ["furniture_workshop"] = new IndustrySpec(360, 3, 40, 15, 8, 16),
            ["silver_mine"] = new IndustrySpec(320, 4, 20, 8, 12, 12)
        };
        private static readonly IReadOnlyDictionary<string, GoodSpec> Goods = new Dictionary<string, GoodSpec>(StringComparer.Ordinal)
        {
            ["logs"] = new GoodSpec(12, 16, 6, 60), ["planks"] = new GoodSpec(24, 8, 8, 70),
            ["grain"] = new GoodSpec(12, 16, 4, 30), ["flour"] = new GoodSpec(22, 8, 6, 50),
            ["bread"] = new GoodSpec(32, 8, 8, 80), ["furniture"] = new GoodSpec(64, 4, 12, 120),
            ["silver"] = new GoodSpec(40, 4, 10, 100)
        };
        internal static IndustrySpec Industry(string id) => Industries.TryGetValue(id, out var value) ? value :
            throw new WorldGenerationException("Industry " + id + " has no canonical generation economic specification.");
        internal static GoodSpec Good(string id) => Goods.TryGetValue(id, out var value) ? value :
            throw new WorldGenerationException("Good " + id + " has no canonical generation price specification.");
    }
}
