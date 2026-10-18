using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class PortSnapshot
    {
        public string Id { get; }
        public IReadOnlyDictionary<string, int> Inventory { get; }
        public long MarketCash { get; }
        public IReadOnlyDictionary<string, MarketQuote> Quotes { get; }
        public IReadOnlyList<BuildingInstance> Buildings { get; }

        internal PortSnapshot(string id, IReadOnlyDictionary<string, int> inventory, long marketCash,
            IReadOnlyDictionary<string, MarketQuote> quotes, IEnumerable<BuildingInstance> buildings)
        {
            Id = id;
            MarketCash = marketCash;
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var quantity in inventory) quantities.Add(quantity.Key, quantity.Value);
            var prices = new SortedDictionary<string, MarketQuote>(StringComparer.Ordinal);
            foreach (var quote in quotes) prices.Add(quote.Key, quote.Value);
            Inventory = new ReadOnlyDictionary<string, int>(quantities);
            Quotes = new ReadOnlyDictionary<string, MarketQuote>(prices);
            Buildings = Array.AsReadOnly(buildings.OrderBy(building => building.Priority)
                .ThenBy(building => building.Id, StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class ShipSnapshot
    {
        public string Id { get; }
        public int CargoCapacity { get; }
        public IReadOnlyDictionary<string, int> Cargo { get; }
        public int CargoUnits { get; }
        public string? DockedPortId { get; }
        public string? OriginPortId { get; }
        public string? DestinationPortId { get; }
        public int RemainingTravelTicks { get; }

        internal ShipSnapshot(string id, int cargoCapacity, IReadOnlyDictionary<string, int> cargo,
            string? dockedPortId, string? originPortId, string? destinationPortId, int remainingTravelTicks)
        {
            Id = id;
            CargoCapacity = cargoCapacity;
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            long total = 0;
            foreach (var quantity in cargo)
            {
                total = checked(total + quantity.Value);
                quantities.Add(quantity.Key, quantity.Value);
            }
            if (total > cargoCapacity) throw new ArgumentOutOfRangeException(nameof(cargo), "Cargo exceeds ship capacity.");
            CargoUnits = checked((int)total);
            Cargo = new ReadOnlyDictionary<string, int>(quantities);
            DockedPortId = dockedPortId;
            OriginPortId = originPortId;
            DestinationPortId = destinationPortId;
            RemainingTravelTicks = remainingTravelTicks;
        }
    }

    /// <summary>One detached view of both ports, one trader, one ship, and the global tick.</summary>
    public sealed class MaritimeWorldSnapshot
    {
        public long Tick { get; }
        public IReadOnlyDictionary<string, PortSnapshot> Ports { get; }
        public string TraderId { get; }
        public long TraderCash { get; }
        public ShipSnapshot Ship { get; }
        public int TravelTicks { get; }

        internal MaritimeWorldSnapshot(long tick, IEnumerable<PortSnapshot> ports, string traderId,
            long traderCash, ShipSnapshot ship, int travelTicks)
        {
            Tick = tick;
            var locations = new SortedDictionary<string, PortSnapshot>(StringComparer.Ordinal);
            foreach (var port in ports) locations.Add(port.Id, port);
            Ports = new ReadOnlyDictionary<string, PortSnapshot>(locations);
            TraderId = traderId;
            TraderCash = traderCash;
            Ship = ship;
            TravelTicks = travelTicks;
        }

        /// <summary>Remote market inspection is allowed; quotes do not authorize remote trading.</summary>
        public MarketPrice GetMarketQuote(string portId, string goodId)
        {
            if (portId == null) throw new ArgumentNullException(nameof(portId));
            if (goodId == null) throw new ArgumentNullException(nameof(goodId));
            var port = Ports[portId];
            return port.Quotes[goodId].AtStock(port.Inventory[goodId]);
        }

        /// <summary>
        /// Hypothetical local accounting using this snapshot's cargo and cash. Remote and
        /// in-transit inspection is allowed; only Step enforces the ship's trading location.
        /// </summary>
        public TradeAttempt PreviewTrade(string portId, TradeCommand command)
        {
            if (portId == null) throw new ArgumentNullException(nameof(portId));
            if (command == null) throw new ArgumentNullException(nameof(command));
            var port = Ports[portId];
            return TradeCalculator.Preview(port.Quotes[command.GoodId], command,
                port.Inventory[command.GoodId], Ship.Cargo[command.GoodId], port.MarketCash, TraderCash,
                checked(Ship.CargoCapacity - Ship.CargoUnits));
        }
    }
}
