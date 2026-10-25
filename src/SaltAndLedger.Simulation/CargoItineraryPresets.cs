using System;

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
            var roles = LivingCityGeneration.Generate(identity.Seed!.Value, pricingMode: identity.LivingPricingMode);
            const int unit = EconomicUnits.QuantityScale;
            CargoItineraryCommand Order(CargoItineraryCommandKind kind, string good, int quantity) =>
                new CargoItineraryCommand(kind, good, checked(quantity * unit), shortagePolicy: CargoItineraryShortagePolicy.Skip,
                    cashReserve: kind == CargoItineraryCommandKind.BuyFromMarket ? 100 * EconomicUnits.MoneyScale : 0,
                    maximumExpenditure: kind == CargoItineraryCommandKind.BuyFromMarket ? 200 * EconomicUnits.MoneyScale : long.MaxValue);
            return new CargoItineraryPolicy("bread_loop", new[]
            {
                new CargoItineraryStop(roles.RecoveryPortId, new[]
                {
                    Order(CargoItineraryCommandKind.UnloadToCompanyTownStock, "flour", 2),
                    Order(CargoItineraryCommandKind.UnloadToCompanyTownStock, "logs", 2),
                    Order(CargoItineraryCommandKind.LoadFromCompanyTownStock, "bread", 4)
                }),
                new CargoItineraryStop(roles.FoodSourcePortId, new[]
                {
                    Order(CargoItineraryCommandKind.SellToMarket, "bread", 4),
                    Order(CargoItineraryCommandKind.BuyFromMarket, "grain", 4),
                    Order(CargoItineraryCommandKind.BuyFromMarket, "logs", 2)
                }),
                new CargoItineraryStop(roles.ProcessingPortId, new[]
                {
                    Order(CargoItineraryCommandKind.SellToMarket, "grain", 4),
                    Order(CargoItineraryCommandKind.BuyFromMarket, "flour", 2)
                })
            });
        }
    }
}
