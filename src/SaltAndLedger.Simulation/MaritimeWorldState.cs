using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// Exactly two ports and one trader-owned ship, advanced by one sequential caller.
    /// Every successful step commits both markets, shared cash, cargo, movement, and time together.
    /// </summary>
    public sealed class MaritimeWorldState
    {
        private readonly ContentCatalog content;
        private MaritimeWorldSnapshot state;
        private bool stepping;

        public long Tick => state.Tick;

        public MaritimeWorldState(ContentCatalog content, IEnumerable<PortConfiguration> ports,
            string traderId, long initialTraderCash, ShipConfiguration ship, int travelTicks, long initialTick = 0)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            if (ports == null) throw new ArgumentNullException(nameof(ports));
            RuntimeIdentifier.Validate(traderId, nameof(traderId));
            if (initialTraderCash < 0) throw new ArgumentOutOfRangeException(nameof(initialTraderCash));
            if (ship == null) throw new ArgumentNullException(nameof(ship));
            if (travelTicks <= 0) throw new ArgumentOutOfRangeException(nameof(travelTicks));
            if (initialTick < 0) throw new ArgumentOutOfRangeException(nameof(initialTick));

            var locations = new SortedDictionary<string, PortSnapshot>(StringComparer.Ordinal);
            foreach (var port in ports)
            {
                if (port == null) throw new ArgumentException("Ports cannot contain null.", nameof(ports));
                locations.Add(port.Id, port.CreateSnapshot(content));
            }
            if (locations.Count != 2) throw new ArgumentException("The maritime prototype requires exactly two ports.", nameof(ports));
            if (!locations.ContainsKey(ship.InitialPortId))
                throw new KeyNotFoundException("Unknown initial port '" + ship.InitialPortId + "'.");
            var cargo = new Inventory(content, ship.InitialCargo);
            var vessel = new ShipSnapshot(ship.Id, ship.CargoCapacity, cargo.Snapshot(), ship.InitialPortId,
                null, null, 0);
            state = new MaritimeWorldSnapshot(initialTick, locations.Values, traderId, initialTraderCash, vessel, travelTicks);
        }

        public MaritimeWorldSnapshot Snapshot() => state;

        /// <summary>
        /// Validate all commands, apply controls and trades at the starting dock, depart,
        /// run each port's production, then advance travel once, including departure ticks.
        /// Invalid operations or arithmetic failure preserve the complete previous snapshot.
        /// </summary>
        public MaritimeStepResult Step(IEnumerable<PortProductionCommand>? commands = null,
            IEnumerable<PortTradeCommand>? trades = null, IEnumerable<SailCommand>? sailCommands = null)
        {
            if (stepping) throw new InvalidOperationException("A world step cannot be entered recursively.");
            stepping = true;
            try
            {
                var before = state;
                var buildingIndexes = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
                foreach (var port in before.Ports.Values)
                {
                    var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
                    for (var index = 0; index < port.Buildings.Count; index++) indexes.Add(port.Buildings[index].Id, index);
                    buildingIndexes.Add(port.Id, indexes);
                }

                // Caller enumeration can fail or attempt reentry. Nothing is staged or
                // committed until every supplied sequence and reference is validated.
                var validatedControls = new List<PortProductionCommand>();
                if (commands != null)
                {
                    foreach (var command in commands)
                    {
                        if (command == null) throw new ArgumentException("Commands cannot contain null.", nameof(commands));
                        var indexes = buildingIndexes[command.PortId];
                        if (!indexes.ContainsKey(command.BuildingInstanceId))
                            throw new KeyNotFoundException("Unknown building instance '" + command.BuildingInstanceId +
                                "' in port '" + command.PortId + "'.");
                        validatedControls.Add(command);
                    }
                }

                var validatedTrades = new List<PortTradeCommand>();
                if (trades != null)
                {
                    foreach (var command in trades)
                    {
                        if (command == null) throw new ArgumentException("Trades cannot contain null.", nameof(trades));
                        var port = before.Ports[command.PortId];
                        content.GetGood(command.Trade.GoodId);
                        if (!port.Quotes.ContainsKey(command.Trade.GoodId))
                            throw new KeyNotFoundException("No market quote for good '" + command.Trade.GoodId +
                                "' in port '" + command.PortId + "'.");
                        if (before.Ship.DockedPortId == null)
                            throw new InvalidOperationException("The ship cannot trade while sailing.");
                        if (!string.Equals(before.Ship.DockedPortId, command.PortId, StringComparison.Ordinal))
                            throw new InvalidOperationException("The ship must be docked at the intended trading port.");
                        validatedTrades.Add(command);
                    }
                }

                SailCommand? departure = null;
                if (sailCommands != null)
                {
                    foreach (var command in sailCommands)
                    {
                        if (command == null) throw new ArgumentException("Sail commands cannot contain null.", nameof(sailCommands));
                        if (!before.Ports.ContainsKey(command.DestinationPortId))
                            throw new KeyNotFoundException("Unknown destination port '" + command.DestinationPortId + "'.");
                        if (departure != null) throw new ArgumentException("Only one departure can be ordered per step.", nameof(sailCommands));
                        if (before.Ship.DockedPortId == null)
                            throw new InvalidOperationException("A sailing ship cannot depart again.");
                        if (string.Equals(before.Ship.DockedPortId, command.DestinationPortId, StringComparison.Ordinal))
                            throw new InvalidOperationException("The destination must be the other port.");
                        departure = command;
                    }
                }

                var nextTick = checked(before.Tick + 1);
                var workingPorts = new SortedDictionary<string, WorkingPort>(StringComparer.Ordinal);
                foreach (var port in before.Ports.Values) workingPorts.Add(port.Id, new WorkingPort(content, port));
                var cargo = new Inventory(content, before.Ship.Cargo);
                var cargoUnits = before.Ship.CargoUnits;
                var traderCash = before.TraderCash;
                var dockedPortId = before.Ship.DockedPortId;
                var originPortId = before.Ship.OriginPortId;
                var destinationPortId = before.Ship.DestinationPortId;
                var remainingTicks = before.Ship.RemainingTravelTicks;

                foreach (var command in validatedControls)
                {
                    var port = workingPorts[command.PortId];
                    var index = buildingIndexes[command.PortId][command.BuildingInstanceId];
                    port.Buildings[index] = port.Buildings[index].WithProductionEnabled(command.Enabled);
                }

                var tradeAttempts = new List<PortTradeAttempt>();
                foreach (var command in validatedTrades)
                {
                    var port = workingPorts[command.PortId];
                    var trade = command.Trade;
                    var attempt = TradeCalculator.Preview(before.Ports[command.PortId].Quotes[trade.GoodId], trade,
                        port.Inventory.GetQuantity(trade.GoodId), cargo.GetQuantity(trade.GoodId), port.Cash, traderCash,
                        checked(before.Ship.CargoCapacity - cargoUnits));
                    TradeCalculator.Apply(attempt, port.Inventory, cargo);
                    cargoUnits = checked(cargoUnits + attempt.TraderInventoryChange.Delta);
                    port.Cash = attempt.MarketCashChange.After;
                    traderCash = attempt.TraderCashChange.After;
                    tradeAttempts.Add(new PortTradeAttempt(command.PortId, attempt));
                }

                var movementEvents = new List<ShipMovementEvent>();
                if (departure != null)
                {
                    originPortId = dockedPortId;
                    destinationPortId = departure.DestinationPortId;
                    dockedPortId = null;
                    remainingTicks = before.TravelTicks;
                    movementEvents.Add(new ShipMovementEvent(ShipMovementKind.Departure, before.Ship.Id,
                        originPortId!, destinationPortId, 0, remainingTicks));
                }

                var productionAttempts = new List<PortProductionAttempt>();
                // Ports are ordinal; each immutable setup already orders buildings by
                // priority then ordinal ID. Controls cannot change that ordering.
                foreach (var entry in workingPorts)
                {
                    var port = entry.Value;
                    foreach (var building in port.Buildings)
                    {
                        if (!building.ProductionEnabled) continue;
                        var startingInventory = port.Inventory.Snapshot();
                        var produced = Production.TryRunBatch(port.Inventory, building.BuildingId, building.RecipeId);
                        var changes = new List<InventoryChange>();
                        foreach (var goodId in before.Ports[entry.Key].Inventory.Keys)
                        {
                            var startingQuantity = startingInventory[goodId];
                            var endingQuantity = port.Inventory.GetQuantity(goodId);
                            if (startingQuantity != endingQuantity)
                                changes.Add(new InventoryChange(goodId, startingQuantity, endingQuantity));
                        }
                        productionAttempts.Add(new PortProductionAttempt(entry.Key, new ProductionAttempt(building,
                            produced ? ProductionOutcome.Produced : ProductionOutcome.ResourceShortage, changes)));
                    }
                }

                if (destinationPortId != null)
                {
                    var previousRemainingTicks = remainingTicks;
                    remainingTicks = checked(remainingTicks - 1);
                    movementEvents.Add(new ShipMovementEvent(ShipMovementKind.Progress, before.Ship.Id,
                        originPortId!, destinationPortId, previousRemainingTicks, remainingTicks));
                    if (remainingTicks == 0)
                    {
                        movementEvents.Add(new ShipMovementEvent(ShipMovementKind.Arrival, before.Ship.Id,
                            originPortId!, destinationPortId, 0, 0));
                        dockedPortId = destinationPortId;
                        originPortId = null;
                        destinationPortId = null;
                    }
                }

                var portsAfter = new List<PortSnapshot>();
                foreach (var entry in workingPorts)
                    portsAfter.Add(new PortSnapshot(entry.Key, entry.Value.Inventory.Snapshot(), entry.Value.Cash,
                        before.Ports[entry.Key].Quotes, entry.Value.Buildings));
                var shipAfter = new ShipSnapshot(before.Ship.Id, before.Ship.CargoCapacity, cargo.Snapshot(),
                    dockedPortId, originPortId, destinationPortId, remainingTicks);
                var after = new MaritimeWorldSnapshot(nextTick, portsAfter, before.TraderId, traderCash, shipAfter, before.TravelTicks);
                var result = new MaritimeStepResult(before.Tick, after, tradeAttempts, productionAttempts, movementEvents);
                // No outward object can fail construction after this single commit.
                state = after;
                return result;
            }
            finally
            {
                stepping = false;
            }
        }

        private sealed class WorkingPort
        {
            internal Inventory Inventory { get; }
            internal long Cash { get; set; }
            internal BuildingInstance[] Buildings { get; }

            internal WorkingPort(ContentCatalog content, PortSnapshot before)
            {
                Inventory = new Inventory(content, before.Inventory);
                Cash = before.MarketCash;
                Buildings = before.Buildings.ToArray();
            }
        }
    }
}
