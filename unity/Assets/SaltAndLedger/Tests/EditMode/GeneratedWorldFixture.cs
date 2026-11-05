using System;
using System.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost.Tests
{
    internal static class GeneratedWorldFixture
    {
        internal static string Port(RegionalWorldSnapshot world, int index) =>
            world.Ports.Keys.OrderBy(id => id, StringComparer.Ordinal).ElementAt(index % world.Ports.Count);

        internal static string Destination(RegionalWorldSnapshot world)
        {
            var start = RunScenario.GetCharter(world.RunIdentity).StartingPortId;
            return world.Ports.Keys.OrderBy(id => id, StringComparer.Ordinal).First(id => id != start);
        }

        internal static string Npc(RegionalWorldSnapshot world) =>
            world.Ships.Values.Where(ship => !ship.IsPlayer).OrderBy(ship => ship.Id, StringComparer.Ordinal).First().Id;
    }
}
