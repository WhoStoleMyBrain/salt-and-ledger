using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class PortPosition
    {
        public string PortId { get; }
        public int X { get; }
        public int Y { get; }
        internal PortPosition(string portId, int x, int y) { PortId = portId; X = x; Y = y; }
    }

    /// <summary>Geography only. Economy, ship speed and display layout are not generated here.</summary>
    public sealed class ArchipelagoGeography
    {
        public int GeneratorVersion { get; }
        public IReadOnlyDictionary<string, PortPosition> Positions { get; }
        public IReadOnlyList<SeaRoute> Routes { get; }
        public string Signature { get; }

        internal ArchipelagoGeography(int generatorVersion, IEnumerable<PortPosition> positions,
            IEnumerable<SeaRoute> routes)
        {
            GeneratorVersion = generatorVersion;
            Positions = new ReadOnlyDictionary<string, PortPosition>(new SortedDictionary<string, PortPosition>(
                positions.ToDictionary(position => position.PortId), StringComparer.Ordinal));
            Routes = Array.AsReadOnly(routes.OrderBy(route => route.Key, StringComparer.Ordinal).ToArray());
            Signature = "v" + generatorVersion.ToString(CultureInfo.InvariantCulture) + "|" +
                string.Join(";", Positions.Values.Select(position => position.PortId + "@" +
                    position.X.ToString(CultureInfo.InvariantCulture) + "," + position.Y.ToString(CultureInfo.InvariantCulture))) +
                "|" + string.Join(";", Routes.Select(route => route.Key + ":" + route.Distance.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>The exact authored coordinates and independently authored distances from Task 1.7.</summary>
        public static ArchipelagoGeography Classic { get; } = new ArchipelagoGeography(0,
            new[]
            {
                new PortPosition(EnterpriseScenario.North, 340, 120),
                new PortPosition(EnterpriseScenario.South, 570, 550),
                new PortPosition(EnterpriseScenario.East, 850, 250),
                new PortPosition(EnterpriseScenario.West, 120, 400)
            },
            new[]
            {
                new SeaRoute(EnterpriseScenario.North, EnterpriseScenario.West, 200),
                new SeaRoute(EnterpriseScenario.North, EnterpriseScenario.South, 300),
                new SeaRoute(EnterpriseScenario.North, EnterpriseScenario.East, 400),
                new SeaRoute(EnterpriseScenario.South, EnterpriseScenario.East, 300),
                new SeaRoute(EnterpriseScenario.South, EnterpriseScenario.West, 400),
                new SeaRoute(EnterpriseScenario.East, EnterpriseScenario.West, 600)
            });
    }

    /// <summary>
    /// Version 1: eleven uint draws, four separated slots, role permutation, integer jitter and ceil-sqrt routes.
    /// No rejection sampling, floating point, runtime Random implementation or mutable global state.
    /// </summary>
    public static class SeededArchipelago
    {
        public const int CurrentVersion = 1;
        public const int MinimumX = 210;
        public const int MaximumX = 790;
        public const int MinimumY = 160;
        public const int MaximumY = 540;
        public const int MinimumSeparation = 220;
        public const int MaximumDistance = 694;

        /// <summary>ASCII decimal int32; optional leading +/-, no whitespace, grouping or exponent notation.</summary>
        public static bool TryParseSeed(string? text, out int seed)
        {
            seed = 0;
            if (string.IsNullOrEmpty(text)) return false;
            var firstDigit = text![0] == '+' || text[0] == '-' ? 1 : 0;
            if (firstDigit == text.Length) return false;
            for (var index = firstDigit; index < text.Length; index++)
                if (text[index] < '0' || text[index] > '9') return false;
            return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out seed);
        }

        public static ArchipelagoGeography Generate(int seed, int generatorVersion = CurrentVersion)
        {
            if (generatorVersion != CurrentVersion)
                throw new ArgumentOutOfRangeException(nameof(generatorVersion), "Unsupported map generator version.");
            var random = new IntegerSequence(seed);
            var slots = new[] { (X: 250, Y: 200), (X: 750, Y: 200), (X: 250, Y: 500), (X: 750, Y: 500) };
            for (var index = slots.Length - 1; index > 0; index--)
            {
                var target = random.Next(index + 1);
                var previous = slots[index];
                slots[index] = slots[target];
                slots[target] = previous;
            }
            // Fixed ordinal IDs, never collection enumeration order or names, select each economic role.
            var ids = new[] { EnterpriseScenario.East, EnterpriseScenario.North, EnterpriseScenario.South, EnterpriseScenario.West };
            var positions = new PortPosition[ids.Length];
            for (var index = 0; index < ids.Length; index++)
                positions[index] = new PortPosition(ids[index], slots[index].X + random.Next(81) - 40,
                    slots[index].Y + random.Next(81) - 40);
            var routes = new List<SeaRoute>();
            for (var first = 0; first < positions.Length - 1; first++)
                for (var second = first + 1; second < positions.Length; second++)
                {
                    var dx = positions[first].X - positions[second].X;
                    var dy = positions[first].Y - positions[second].Y;
                    var square = checked(dx * dx + dy * dy);
                    if (square < MinimumSeparation * MinimumSeparation)
                        throw new InvalidOperationException("Generated towns violate the separation contract.");
                    var distance = CeilingSquareRoot(square);
                    routes.Add(new SeaRoute(positions[first].PortId, positions[second].PortId, distance));
                }
            return new ArchipelagoGeography(CurrentVersion, positions, routes);
        }

        private static int CeilingSquareRoot(int square)
        {
            // At most ten bisections for the proven [1,694] distance range.
            var lower = 1;
            var upper = MaximumDistance;
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (middle * middle < square) lower = middle + 1;
                else upper = middle;
            }
            if (lower * lower < square) throw new InvalidOperationException("Generated route exceeds its distance bound.");
            return lower;
        }

        private struct IntegerSequence
        {
            private uint state;
            internal IntegerSequence(int seed) { state = unchecked((uint)seed); }
            internal int Next(int upperExclusive)
            {
                unchecked
                {
                    // Weyl sequence followed by Murmur3's integer avalanche. Modulo is deliberate and versioned.
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
