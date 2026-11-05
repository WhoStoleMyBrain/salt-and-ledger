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
        public int SelectedCandidate { get; }
        public int CandidateCount { get; }
        public int RejectedCandidateCount => RejectionReasons.Count;
        public IReadOnlyList<string> RejectionReasons { get; }
        public long PlacementSpread { get; }

        internal ArchipelagoGeography(int generatorVersion, IEnumerable<PortPosition> positions,
            IEnumerable<SeaRoute> routes, int selectedCandidate = 0, int candidateCount = 1,
            IEnumerable<string>? rejectionReasons = null, long placementSpread = 0)
        {
            GeneratorVersion = generatorVersion;
            SelectedCandidate = selectedCandidate;
            CandidateCount = candidateCount;
            RejectionReasons = Array.AsReadOnly((rejectionReasons ?? Array.Empty<string>()).ToArray());
            PlacementSpread = placementSpread;
            Positions = new ReadOnlyDictionary<string, PortPosition>(new SortedDictionary<string, PortPosition>(
                positions.ToDictionary(position => position.PortId), StringComparer.Ordinal));
            Routes = Array.AsReadOnly(routes.OrderBy(route => route.Key, StringComparer.Ordinal).ToArray());
            Signature = "v" + generatorVersion.ToString(CultureInfo.InvariantCulture) + "|" +
                string.Join(";", Positions.Values.Select(position => position.PortId + "@" +
                    position.X.ToString(CultureInfo.InvariantCulture) + "," + position.Y.ToString(CultureInfo.InvariantCulture))) +
                "|" + string.Join(";", Routes.Select(route => route.Key + ":" + route.Distance.ToString(CultureInfo.InvariantCulture)));
        }


    }

    /// <summary>
    /// Version 4: bounded integer blue-noise placement, a distance-constrained minimum
    /// spanning tree and selected shortcut lanes. No runtime entropy or economic repair.
    /// </summary>
    public static class SeededArchipelago
    {
        public const int CurrentVersion = 4;
        // Default map bounds only. Custom worlds use their immutable configuration.
        public const int MinimumX = 60;
        public const int MaximumX = 940;
        public const int MinimumY = 60;
        public const int MaximumY = 640;
        public const int MinimumSeparation = 110;
        public const int MaximumDistance = 450;

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

        public static ArchipelagoGeography Generate(int seed, int generatorVersion = CurrentVersion,
            WorldGenerationConfiguration? configuration = null, ContentCatalog? content = null)
        {
            if (generatorVersion != CurrentVersion)
                throw new ArgumentOutOfRangeException(nameof(generatorVersion), "Unsupported map generator version.");
            configuration = configuration ?? WorldGenerationConfiguration.Default;
            configuration.Validate(content);
            var settings = configuration.Geography;
            var countSequence = new IntegerSequence(unchecked((uint)seed ^ 0x5B4C7A91u));
            var townCount = configuration.TownCount.Min + countSequence.Next(
                configuration.TownCount.Max - configuration.TownCount.Min + 1);
            var failures = new List<string>();
            PortPosition[]? bestPositions = null;
            List<CandidateEdge>? bestEdges = null;
            long bestSpread = -1;
            long bestDiameter = long.MaxValue;
            var selectedCandidate = -1;
            for (var candidate = 0; candidate < settings.CandidateCount; candidate++)
            {
                // Candidate streams are isolated: rejection never shifts another layout's draws.
                var random = new IntegerSequence(unchecked((uint)seed ^ 0x71D4A5B3u ^
                    ((uint)candidate + 1u) * 0x9E3779B9u));
                var placed = new List<PortPosition>();
                var minimumSquare = (long)settings.MinimumTownSpacing * settings.MinimumTownSpacing;
                for (var attempt = 0; attempt < settings.PlacementAttempts && placed.Count < townCount; attempt++)
                {
                    var x = settings.Margin + random.Next(settings.Width - 2 * settings.Margin + 1);
                    var y = settings.Margin + random.Next(settings.Height - 2 * settings.Margin + 1);
                    if (placed.Any(position => SquaredDistance(position.X, position.Y, x, y) < minimumSquare))
                        continue;
                    placed.Add(new PortPosition("town_" + (placed.Count + 1).ToString("D3", CultureInfo.InvariantCulture), x, y));
                }
                if (placed.Count != townCount)
                {
                    failures.Add(FormattableString.Invariant($"Candidate {candidate}: placed {placed.Count}/{townCount} towns within {settings.PlacementAttempts} attempts; spacing {settings.MinimumTownSpacing}."));
                    continue;
                }
                var positions = placed.ToArray();
                var edges = new List<CandidateEdge>();
                for (var first = 0; first < positions.Length - 1; first++)
                    for (var second = first + 1; second < positions.Length; second++)
                    {
                        var distance = CeilingSquareRoot(SquaredDistance(positions[first].X, positions[first].Y,
                            positions[second].X, positions[second].Y));
                        if (distance >= settings.MinimumLaneDistance && distance <= settings.MaximumLaneDistance)
                            edges.Add(new CandidateEdge(first, second, distance));
                    }
                edges.Sort(CompareEdges);
                var components = Enumerable.Range(0, townCount).ToArray();
                var selected = new List<CandidateEdge>();
                foreach (var edge in edges)
                {
                    var firstRoot = Root(components, edge.First);
                    var secondRoot = Root(components, edge.Second);
                    if (firstRoot == secondRoot) continue;
                    components[secondRoot] = firstRoot;
                    selected.Add(edge);
                    if (selected.Count == townCount - 1) break;
                }
                if (selected.Count != townCount - 1)
                {
                    failures.Add(FormattableString.Invariant($"Candidate {candidate}: allowed lanes of {settings.MinimumLaneDistance}..{settings.MaximumLaneDistance} cannot connect all {townCount} towns."));
                    continue;
                }
                if (edges.Count < townCount - 1 + settings.ExtraLaneCount)
                {
                    failures.Add(FormattableString.Invariant($"Candidate {candidate}: {edges.Count} valid lanes cannot supply a tree plus {settings.ExtraLaneCount} additional lanes."));
                    continue;
                }
                var distances = ShortestDistances(townCount, selected);
                for (var extra = 0; extra < settings.ExtraLaneCount; extra++)
                {
                    // A shortcut earns its actual saving on the current connected network.
                    // Stable edge order breaks ties; added lanes never bypass the distance limits.
                    CandidateEdge? shortcut = null;
                    long bestSaving = -1;
                    foreach (var edge in edges)
                    {
                        if (selected.Contains(edge)) continue;
                        var saving = distances[edge.First, edge.Second] - edge.Distance;
                        if (saving <= bestSaving) continue;
                        bestSaving = saving;
                        shortcut = edge;
                    }
                    if (shortcut == null) throw new InvalidOperationException("Validated shortcut capacity was lost.");
                    selected.Add(shortcut);
                    AddShortcut(distances, shortcut);
                }
                long spread = 0;
                long diameter = 0;
                for (var first = 0; first < townCount; first++)
                {
                    var nearest = int.MaxValue;
                    for (var second = 0; second < townCount; second++)
                    {
                        if (first == second) continue;
                        nearest = Math.Min(nearest, CeilingSquareRoot(SquaredDistance(positions[first].X,
                            positions[first].Y, positions[second].X, positions[second].Y)));
                        diameter = Math.Max(diameter, distances[first, second]);
                    }
                    spread += nearest;
                }
                if (spread < bestSpread || spread == bestSpread && diameter >= bestDiameter) continue;
                bestPositions = positions;
                bestEdges = selected;
                bestSpread = spread;
                bestDiameter = diameter;
                selectedCandidate = candidate;
            }
            if (bestPositions == null || bestEdges == null)
                throw new WorldGenerationException(FormattableString.Invariant(
                    $"Geography constraints could not be satisfied for seed {seed}, {townCount} towns after {settings.CandidateCount} bounded candidates. Increase the map or lane-distance range, reduce spacing/extra lanes, or increase candidate/placement limits. ") +
                    string.Join(" ", failures));
            return new ArchipelagoGeography(CurrentVersion, bestPositions,
                bestEdges.Select(edge => new SeaRoute(bestPositions[edge.First].PortId,
                    bestPositions[edge.Second].PortId, edge.Distance)), selectedCandidate,
                settings.CandidateCount, failures, bestSpread);
        }

        private static long SquaredDistance(int firstX, int firstY, int secondX, int secondY)
        {
            var dx = (long)firstX - secondX;
            var dy = (long)firstY - secondY;
            return checked(dx * dx + dy * dy);
        }

        internal static int CeilingSquareRoot(long square)
        {
            if (square < 0) throw new ArgumentOutOfRangeException(nameof(square));
            long lower = 0;
            long upper = Math.Min(square, int.MaxValue);
            while (lower < upper)
            {
                var middle = lower + (upper - lower) / 2;
                if (middle * middle < square) lower = middle + 1;
                else upper = middle;
            }
            if (lower * lower < square) throw new OverflowException("Distance exceeds the supported integer range.");
            return checked((int)lower);
        }

        private static int Root(int[] components, int index)
        {
            while (components[index] != index)
            {
                components[index] = components[components[index]];
                index = components[index];
            }
            return index;
        }

        private static int CompareEdges(CandidateEdge first, CandidateEdge second)
        {
            var distance = first.Distance.CompareTo(second.Distance);
            if (distance != 0) return distance;
            var origin = first.First.CompareTo(second.First);
            return origin != 0 ? origin : first.Second.CompareTo(second.Second);
        }

        private static long[,] ShortestDistances(int count, IEnumerable<CandidateEdge> edges)
        {
            var distances = new long[count, count];
            for (var first = 0; first < count; first++)
                for (var second = 0; second < count; second++)
                    distances[first, second] = first == second ? 0 : long.MaxValue / 4;
            foreach (var edge in edges)
                distances[edge.First, edge.Second] = distances[edge.Second, edge.First] = edge.Distance;
            for (var through = 0; through < count; through++)
                for (var first = 0; first < count; first++)
                    for (var second = 0; second < count; second++)
                        distances[first, second] = Math.Min(distances[first, second],
                            distances[first, through] + distances[through, second]);
            return distances;
        }

        private static void AddShortcut(long[,] distances, CandidateEdge edge)
        {
            var count = distances.GetLength(0);
            // Take the old endpoint columns before the in-place all-pairs update.
            var toFirst = new long[count];
            var toSecond = new long[count];
            for (var index = 0; index < count; index++)
            {
                toFirst[index] = distances[index, edge.First];
                toSecond[index] = distances[index, edge.Second];
            }
            for (var first = 0; first < count; first++)
                for (var second = 0; second < count; second++)
                    distances[first, second] = Math.Min(distances[first, second], Math.Min(
                        toFirst[first] + edge.Distance + toSecond[second],
                        toSecond[first] + edge.Distance + toFirst[second]));
        }

        private sealed class CandidateEdge
        {
            internal int First { get; }
            internal int Second { get; }
            internal int Distance { get; }
            internal CandidateEdge(int first, int second, int distance)
            { First = first; Second = second; Distance = distance; }
        }

        private struct IntegerSequence
        {
            private uint state;
            internal IntegerSequence(uint seed) { state = seed; }
            internal int Next(int upperExclusive)
            {
                if (upperExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(upperExclusive));
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
