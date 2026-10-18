using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// A single-location, sequential simulation. Each enabled facility attempts one
    /// batch per step, in ascending priority and then ordinal instance-ID order.
    /// Calls must come from one caller; concurrent access is not supported.
    /// </summary>
    public sealed class WorldState
    {
        private readonly ContentCatalog content;
        private WorldSnapshot state;
        private bool stepping;

        public long Tick => state.Tick;
        public string LocationId => state.LocationId;

        public WorldState(ContentCatalog content, string locationId, IEnumerable<BuildingInstance> buildings,
            IReadOnlyDictionary<string, int>? initialQuantities = null, long initialTick = 0,
            MarketConfiguration? market = null)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            RuntimeIdentifier.Validate(locationId, nameof(locationId));
            if (buildings == null) throw new ArgumentNullException(nameof(buildings));
            if (initialTick < 0) throw new ArgumentOutOfRangeException(nameof(initialTick), "Tick cannot be negative.");

            var instances = new List<BuildingInstance>();
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var building in buildings)
            {
                if (building == null) throw new ArgumentException("Building instances cannot contain null.", nameof(buildings));
                if (!instanceIds.Add(building.Id))
                    throw new ArgumentException("Duplicate building instance ID '" + building.Id + "'.", nameof(buildings));
                content.GetBuilding(building.BuildingId);
                var recipe = content.GetRecipe(building.RecipeId);
                if (!string.Equals(recipe.BuildingId, building.BuildingId, StringComparison.Ordinal))
                    throw new ArgumentException("Recipe '" + building.RecipeId + "' does not belong to building '" +
                        building.BuildingId + "'.", nameof(buildings));
                instances.Add(building);
            }

            var inventory = new Inventory(content, initialQuantities);
            state = new WorldSnapshot(initialTick, locationId, inventory.Snapshot(), instances,
                market?.CreateSnapshot(content));
        }

        public WorldSnapshot Snapshot() => state;

        /// <summary>
        /// Validates every command before applying them in declared order, then runs
        /// trades and production against staged state. Shortages are recorded and time advances.
        /// Any exception leaves both inventories, cash, controls, and tick unchanged.
        /// </summary>
        public StepResult Step(IEnumerable<SetProductionEnabledCommand>? commands = null,
            IEnumerable<TradeCommand>? trades = null)
        {
            if (stepping) throw new InvalidOperationException("A world step cannot be entered recursively.");
            stepping = true;
            try
            {
                var before = state;
                var buildingIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var index = 0; index < before.Buildings.Count; index++)
                    buildingIndexes.Add(before.Buildings[index].Id, index);

                // Enumerating caller-provided commands may throw. Complete enumeration
                // and validation before applying even the first configuration change.
                var validatedCommands = new List<SetProductionEnabledCommand>();
                if (commands != null)
                {
                    foreach (var command in commands)
                    {
                        if (command == null) throw new ArgumentException("Commands cannot contain null.", nameof(commands));
                        if (!buildingIndexes.ContainsKey(command.BuildingInstanceId))
                            throw new KeyNotFoundException("Unknown building instance '" + command.BuildingInstanceId + "'.");
                        validatedCommands.Add(command);
                    }
                }

                var validatedTrades = new List<TradeCommand>();
                if (trades != null)
                {
                    foreach (var trade in trades)
                    {
                        if (trade == null) throw new ArgumentException("Trades cannot contain null.", nameof(trades));
                        content.GetGood(trade.GoodId);
                        if (before.Trading == null)
                            throw new InvalidOperationException("Trading requires market configuration.");
                        if (!before.Trading.Quotes.ContainsKey(trade.GoodId))
                            throw new KeyNotFoundException("No market quote for good '" + trade.GoodId + "'.");
                        validatedTrades.Add(trade);
                    }
                }

                var nextTick = checked(before.Tick + 1);
                var inventory = new Inventory(content, before.Inventory);
                var trading = before.Trading == null ? null : new TradingState(content, before.Trading);
                var buildings = before.Buildings.ToArray();
                foreach (var command in validatedCommands)
                {
                    var index = buildingIndexes[command.BuildingInstanceId];
                    buildings[index] = buildings[index].WithProductionEnabled(command.Enabled);
                }

                var tradeAttempts = new List<TradeAttempt>();
                foreach (var trade in validatedTrades) tradeAttempts.Add(trading!.Execute(inventory, trade));

                // Snapshot order is already priority then ordinal ID. Controls do not
                // change that order, and eligibility is fixed before production begins.
                var eligibleBuildings = buildings.Where(building => building.ProductionEnabled).ToArray();
                var attempts = new List<ProductionAttempt>();
                foreach (var building in eligibleBuildings)
                {
                    var startingInventory = inventory.Snapshot();
                    var produced = Production.TryRunBatch(inventory, building.BuildingId, building.RecipeId);
                    var changes = new List<InventoryChange>();
                    foreach (var goodId in before.Inventory.Keys)
                    {
                        var startingQuantity = startingInventory[goodId];
                        var endingQuantity = inventory.GetQuantity(goodId);
                        if (endingQuantity != startingQuantity)
                            changes.Add(new InventoryChange(goodId, startingQuantity, endingQuantity));
                    }
                    attempts.Add(new ProductionAttempt(building,
                        produced ? ProductionOutcome.Produced : ProductionOutcome.ResourceShortage, changes));
                }

                // Build every outward-facing object before the only state mutation.
                // A later facility's overflow therefore rolls back the whole step.
                var after = new WorldSnapshot(nextTick, before.LocationId, inventory.Snapshot(), buildings, trading?.Snapshot());
                var result = new StepResult(before.Tick, after, attempts, tradeAttempts);
                state = after;
                return result;
            }
            finally
            {
                stepping = false;
            }
        }
    }
}
