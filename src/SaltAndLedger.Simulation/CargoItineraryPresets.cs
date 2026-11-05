using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>An inspectable example, never an applied strategy, construction order or source of goods.</summary>
    public static class CargoItineraryPresets
    {
        public static CargoItineraryPolicy BreadLoop(RegionalWorldSnapshot snapshot, string shipId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var identity = snapshot.RunIdentity;
            if (identity?.ItineraryVersion != CargoItineraryPolicy.CurrentVersion || !snapshot.Ships[shipId].IsPlayer)
                throw new InvalidOperationException("This example requires an owned ship in a new v3 itinerary run.");
            var ship = snapshot.Ships[shipId];
            var bakeries = snapshot.IndustryBlueprints.Values.Where(b => b.RecipeId == "bake_bread")
                .SelectMany(b => b.EligiblePortIds).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var bakery = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == ship.OwnerId && f.Blueprint.RecipeId == "bake_bread")
                .OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => f.PortId).FirstOrDefault() ??
                bakeries.FirstOrDefault(id => id == ship.DockedPortId) ?? bakeries.FirstOrDefault() ??
                throw new InvalidOperationException("This world has no permitted Bakery for the bread route example.");
            string Source(string recipe)
            {
                var publicSources = snapshot.Ports.Values.Where(p => p.Facilities.Any(f => f.Configuration.Instance.RecipeId == recipe))
                    .Select(p => p.Id).ToArray();
                var potentialSources = snapshot.IndustryBlueprints.Values.Where(b => b.RecipeId == recipe).SelectMany(b => b.EligiblePortIds);
                return publicSources.Concat(potentialSources).Distinct(StringComparer.Ordinal).Where(id => id != bakery)
                    .OrderBy(id => publicSources.Contains(id) ? 0 : 1)
                    .ThenBy(id => snapshot.GetPath(bakery, id, ship.Configuration.Speed).DurationDays)
                    .ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault() ??
                    throw new InvalidOperationException("The bread route example needs a reachable remote source of '" + recipe + "'.");
            }
            var flourSource = Source("mill_flour");
            var logsSource = Source("harvest_logs");
            const int unit = EconomicUnits.QuantityScale;
            CargoItineraryCommand Order(CargoItineraryCommandKind kind, string good, int quantity) =>
                new CargoItineraryCommand(kind, good, checked(quantity * unit), shortagePolicy: CargoItineraryShortagePolicy.Skip,
                    cashReserve: kind == CargoItineraryCommandKind.BuyFromMarket ? 100 * EconomicUnits.MoneyScale : 0,
                    maximumExpenditure: kind == CargoItineraryCommandKind.BuyFromMarket ? 200 * EconomicUnits.MoneyScale : long.MaxValue);
            var stops = new List<CargoItineraryStop>
            {
                new CargoItineraryStop(bakery, new[]
                {
                    Order(CargoItineraryCommandKind.UnloadToCompanyTownStock, "flour", 2),
                    Order(CargoItineraryCommandKind.UnloadToCompanyTownStock, "logs", 2),
                    Order(CargoItineraryCommandKind.LoadFromCompanyTownStock, "bread", 4)
                })
            };
            foreach (var source in new[] { flourSource, logsSource }.Distinct(StringComparer.Ordinal))
            {
                var commands = new List<CargoItineraryCommand>
                {
                    Order(CargoItineraryCommandKind.SellToMarket, "bread", 4)
                };
                if (source == flourSource) commands.Add(Order(CargoItineraryCommandKind.BuyFromMarket, "flour", 2));
                if (source == logsSource) commands.Add(Order(CargoItineraryCommandKind.BuyFromMarket, "logs", 2));
                stops.Add(new CargoItineraryStop(source, commands));
            }
            return new CargoItineraryPolicy("bread_loop", stops);
        }
    }
}
