using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>A physical route through existing sea lanes. Intermediate ports are transit points, not docks.</summary>
    public sealed class SeaPath
    {
        public IReadOnlyList<string> PortIds { get; }
        public IReadOnlyList<SeaRoute> Legs { get; }
        public long Distance { get; }
        public int DurationDays { get; }
        public int Speed { get; }

        internal SeaPath(IEnumerable<string> portIds, IEnumerable<SeaRoute> legs, int speed)
        {
            PortIds = Array.AsReadOnly(portIds.ToArray());
            Legs = Array.AsReadOnly(legs.ToArray());
            Speed = speed;
            Distance = Legs.Sum(leg => (long)leg.Distance);
            DurationDays = checked((int)Legs.Sum(leg => (long)leg.DurationDays(speed)));
        }
    }

    /// <summary>Integer Dijkstra search; travel days, distance, then the ordinal port sequence resolve ties.</summary>
    public static class SeaNavigation
    {
        public static SeaPath FindPath(IEnumerable<SeaRoute> routes, string from, string to, int speed)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            RuntimeIdentifier.Validate(from, nameof(from));
            RuntimeIdentifier.Validate(to, nameof(to));
            if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (from == to) throw new InvalidOperationException("A voyage must end at another port.");
            var adjacency = new SortedDictionary<string, List<SeaRoute>>(StringComparer.Ordinal);
            foreach (var route in routes)
            {
                if (route == null) throw new ArgumentException("Sea lanes cannot contain null.", nameof(routes));
                if (!adjacency.TryGetValue(route.OriginPortId, out var origin))
                    adjacency.Add(route.OriginPortId, origin = new List<SeaRoute>());
                if (!adjacency.TryGetValue(route.DestinationPortId, out var destination))
                    adjacency.Add(route.DestinationPortId, destination = new List<SeaRoute>());
                origin.Add(route);
                destination.Add(route);
            }
            if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to))
                throw new InvalidOperationException("No traversable sea path connects '" + from + "' to '" + to + "'.");
            var best = new Dictionary<string, Candidate>(StringComparer.Ordinal)
            {
                [from] = new Candidate(0, 0, new[] { from }, Array.Empty<SeaRoute>())
            };
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                string? selected = null;
                foreach (var port in adjacency.Keys)
                    if (!visited.Contains(port) && best.TryGetValue(port, out var candidate) &&
                        (selected == null || Compare(candidate, best[selected]) < 0)) selected = port;
                if (selected == null)
                    throw new InvalidOperationException("No traversable sea path connects '" + from + "' to '" + to + "'.");
                var current = best[selected];
                if (selected == to) return new SeaPath(current.Ports, current.Legs, speed);
                visited.Add(selected);
                foreach (var leg in adjacency[selected].OrderBy(route => route.Key, StringComparer.Ordinal))
                {
                    var next = leg.OriginPortId == selected ? leg.DestinationPortId : leg.OriginPortId;
                    if (visited.Contains(next)) continue;
                    var candidate = new Candidate(checked(current.Days + leg.DurationDays(speed)),
                        checked(current.Distance + leg.Distance), current.Ports.Concat(new[] { next }).ToArray(),
                        current.Legs.Concat(new[] { leg }).ToArray());
                    if (!best.TryGetValue(next, out var previous) || Compare(candidate, previous) < 0)
                        best[next] = candidate;
                }
            }
        }

        private static int Compare(Candidate first, Candidate second)
        {
            var comparison = first.Days.CompareTo(second.Days);
            if (comparison != 0) return comparison;
            comparison = first.Distance.CompareTo(second.Distance);
            if (comparison != 0) return comparison;
            for (var index = 0; index < Math.Min(first.Ports.Length, second.Ports.Length); index++)
            {
                comparison = string.CompareOrdinal(first.Ports[index], second.Ports[index]);
                if (comparison != 0) return comparison;
            }
            return first.Ports.Length.CompareTo(second.Ports.Length);
        }

        private sealed class Candidate
        {
            internal long Days { get; }
            internal long Distance { get; }
            internal string[] Ports { get; }
            internal SeaRoute[] Legs { get; }
            internal Candidate(long days, long distance, string[] ports, SeaRoute[] legs)
            { Days = days; Distance = distance; Ports = ports; Legs = legs; }
        }
    }
}
