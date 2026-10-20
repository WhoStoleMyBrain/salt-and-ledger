using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// One serialized authority for regional accounts, schedules and voyages.
    /// Instant commands and complete daily advances share privately staged domain operations.
    /// </summary>
    public sealed class RegionalWorldState
    {
        public const int MaximumDocksideActions = 4;
        private readonly ContentCatalog content;
        private RegionalWorldSnapshot state;
        private bool mutating;

        public RegionalWorldState(ContentCatalog content, IEnumerable<RegionalPortConfiguration> ports,
            IEnumerable<SeaRoute> routes, IEnumerable<RegionalShipConfiguration> ships,
            long initialDay = 0, long initialRevision = 0)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            if (ports == null) throw new ArgumentNullException(nameof(ports));
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            if (ships == null) throw new ArgumentNullException(nameof(ships));
            if (initialDay < 0 || initialRevision < 0) throw new ArgumentOutOfRangeException(nameof(initialDay));
            var locations = new List<RegionalPortSnapshot>();
            foreach (var port in ports)
            {
                if (port == null) throw new ArgumentException("Ports cannot contain null.");
                var market = port.Market.CreateSnapshot(content);
                foreach (var demand in port.Consumption) content.GetGood(demand.GoodId);
                locations.Add(new RegionalPortSnapshot(port, market,
                    port.Facilities.Select(f => new ScheduledFacilitySnapshot(f, f.Instance.ProductionEnabled,
                        f.FirstDueDay, ScheduledProductionOutcome.NotDue)),
                    port.Consumption.Select(c => new ConsumptionSnapshot(c, c.FirstDueDay, 0, 0))));
            }
            if (locations.Count < 2) throw new ArgumentException("A region needs at least two ports.");
            var connections = routes.ToArray();
            var routeKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var route in connections)
            {
                if (route == null) throw new ArgumentException("Routes cannot contain null.");
                if (!locations.Any(p => p.Id == route.OriginPortId) || !locations.Any(p => p.Id == route.DestinationPortId))
                    throw new ArgumentException("A sea route references an unknown port.");
                if (!routeKeys.Add(route.Key)) throw new ArgumentException("Duplicate undirected sea route.");
            }
            var vessels = new List<RegionalShipSnapshot>();
            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ship in ships)
            {
                if (ship == null) throw new ArgumentException("Ships cannot contain null.");
                if (!owners.Add(ship.OwnerId)) throw new ArgumentException("Each trader owns exactly one ship in this region.");
                if (!locations.Any(p => p.Id == ship.Ship.InitialPortId)) throw new ArgumentException("Unknown initial dock.");
                var cargo = new Inventory(content, ship.Ship.InitialCargo);
                vessels.Add(new RegionalShipSnapshot(ship, new ShipSnapshot(ship.Ship.Id, ship.Ship.CargoCapacity,
                    cargo.Snapshot(), ship.Ship.InitialPortId, null, null, 0), ship.InitialCash, ship.Policy,
                    ship.AutomationEnabled, 0));
            }
            if (vessels.Count == 0) throw new ArgumentException("A region needs at least one ship.");
            state = new RegionalWorldSnapshot(initialDay, initialRevision, locations, vessels, connections);
            foreach (var vessel in state.Ships.Values)
                if (vessel.Policy != null) RegionalAutomation.ValidatePolicy(state, vessel.Id, vessel.Policy);
        }

        public RegionalWorldSnapshot Snapshot() => state;
        public RegionalDecision EvaluateAutomation(string shipId) => RegionalAutomation.Evaluate(state, shipId);

        public RegionalResult ExecuteTrade(string shipId, string portId, TradeCommand command) =>
            Mutate(work =>
            {
                EnsureManual(work.Ships[shipId]);
                work.Trade(shipId, portId, command);
            });

        public RegionalResult Depart(string shipId, string destinationPortId) =>
            Mutate(work =>
            {
                EnsureManual(work.Ships[shipId]);
                work.Depart(shipId, destinationPortId);
            });

        public RegionalResult ConfigureAutomation(string shipId, TradeRoutePolicy policy, bool enabled)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            return Mutate(work =>
            {
                RegionalAutomation.ValidatePolicy(state, shipId, policy);
                var ship = work.Ships[shipId];
                if (ReferenceEquals(ship.Policy, policy) && ship.AutomationEnabled == enabled) return;
                ship.Policy = policy;
                ship.AutomationEnabled = enabled;
                work.Changed = true;
            });
        }

        public RegionalResult SetAutomationEnabled(string shipId, bool enabled) =>
            Mutate(work =>
            {
                var ship = work.Ships[shipId];
                if (enabled && ship.Policy == null) throw new InvalidOperationException("Configure a route first.");
                if (ship.AutomationEnabled == enabled) return;
                ship.AutomationEnabled = enabled;
                work.Changed = true;
            });

        public RegionalResult SetProductionEnabled(string portId, string facilityId, bool enabled) =>
            Mutate(work =>
            {
                var port = work.Ports[portId];
                var index = Array.FindIndex(port.Facilities, f => f.Configuration.Instance.Id == facilityId);
                if (index < 0) throw new KeyNotFoundException("Unknown scheduled facility.");
                var before = port.Facilities[index];
                if (before.Enabled == enabled) return;
                port.Facilities[index] = new ScheduledFacilitySnapshot(before.Configuration, enabled,
                    before.NextDueDay, enabled ? before.LastOutcome : ScheduledProductionOutcome.Disabled);
                work.Changed = true;
            });

        public RegionalResult ExecuteAutomation(RegionalDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            return Mutate(work =>
            {
                if (!ReferenceEquals(decision.Before, state))
                    throw new InvalidOperationException("The decision is stale; reevaluate the current revision.");
                work.ApplyDecision(decision);
            });
        }

        public RegionalResult RunAutomation(string shipId, int maxActions = MaximumDocksideActions)
        {
            if (maxActions < 1 || maxActions > MaximumDocksideActions)
                throw new ArgumentOutOfRangeException(nameof(maxActions));
            return Mutate(work => work.RunAutomation(shipId, maxActions));
        }

        public RegionalResult AdvanceDay() => Mutate(work =>
        {
            var nextDay = checked(state.Day + 1);
            // Fail before performing any work if the final public revision cannot be represented.
            _ = checked(state.Revision + 1);
            foreach (var ship in work.Ships.Values.OrderBy(s => s.Configuration.Priority)
                .ThenBy(s => s.Configuration.Ship.Id, StringComparer.Ordinal))
                if (ship.AutomationEnabled) work.RunAutomation(ship.Configuration.Ship.Id, MaximumDocksideActions);
            work.Produce(nextDay);
            work.Consume(nextDay);
            work.AdvanceVoyages();
            work.Day = nextDay;
            work.Changed = true;
        });

        private static void EnsureManual(WorkingShip ship)
        {
            if (ship.AutomationEnabled) throw new InvalidOperationException("Pause automation before issuing manual orders.");
        }

        private RegionalResult Mutate(Action<WorkingWorld> operation)
        {
            if (mutating) throw new InvalidOperationException("Regional commands cannot be entered recursively.");
            mutating = true;
            try
            {
                var before = state;
                var work = new WorkingWorld(content, before);
                operation(work);
                var after = work.Changed ? work.View(checked(before.Revision + 1)) : before;
                var result = new RegionalResult(before, after, work.Trades, work.Production, work.Consumption,
                    work.Movements, work.Decisions);
                state = after;
                return result;
            }
            finally { mutating = false; }
        }

        private sealed class WorkingPort
        {
            internal RegionalPortConfiguration Configuration { get; }
            internal Inventory Inventory { get; }
            internal long Cash { get; set; }
            internal ScheduledFacilitySnapshot[] Facilities { get; }
            internal ConsumptionSnapshot[] Consumption { get; }
            internal WorkingPort(ContentCatalog content, RegionalPortSnapshot before)
            {
                Configuration = before.Configuration;
                Inventory = new Inventory(content, before.Inventory);
                Cash = before.MarketCash;
                Facilities = before.Facilities.ToArray();
                Consumption = before.Consumption.ToArray();
            }
            internal RegionalPortSnapshot Snapshot() => new RegionalPortSnapshot(Configuration,
                new PortSnapshot(Configuration.Id, Inventory.Snapshot(), Cash, Configuration.Market.Quotes,
                    Facilities.Select(f => f.Configuration.Instance.WithProductionEnabled(f.Enabled))),
                Facilities, Consumption);
        }

        private sealed class WorkingShip
        {
            internal RegionalShipConfiguration Configuration { get; }
            internal Inventory Cargo { get; }
            internal int CargoUnits { get; set; }
            internal long Cash { get; set; }
            internal TradeRoutePolicy? Policy { get; set; }
            internal bool AutomationEnabled { get; set; }
            internal string? Dock { get; set; }
            internal string? Origin { get; set; }
            internal string? Destination { get; set; }
            internal int Remaining { get; set; }
            internal int Duration { get; set; }
            internal WorkingShip(ContentCatalog content, RegionalShipSnapshot before)
            {
                Configuration = before.Configuration;
                Cargo = new Inventory(content, before.Cargo);
                CargoUnits = before.CargoUnits;
                Cash = before.Cash;
                Policy = before.Policy;
                AutomationEnabled = before.AutomationEnabled;
                Dock = before.DockedPortId;
                Origin = before.OriginPortId;
                Destination = before.DestinationPortId;
                Remaining = before.RemainingTravelDays;
                Duration = before.VoyageDurationDays;
            }
            internal RegionalShipSnapshot Snapshot() => new RegionalShipSnapshot(Configuration,
                new ShipSnapshot(Configuration.Ship.Id, Configuration.Ship.CargoCapacity, Cargo.Snapshot(),
                    Dock, Origin, Destination, Remaining), Cash, Policy, AutomationEnabled, Duration);
        }

        private sealed class WorkingWorld
        {
            private readonly ContentCatalog content;
            private readonly RegionalWorldSnapshot before;
            internal long Day { get; set; }
            internal bool Changed { get; set; }
            internal SortedDictionary<string, WorkingPort> Ports { get; } = new SortedDictionary<string, WorkingPort>(StringComparer.Ordinal);
            internal SortedDictionary<string, WorkingShip> Ships { get; } = new SortedDictionary<string, WorkingShip>(StringComparer.Ordinal);
            internal List<RegionalTrade> Trades { get; } = new List<RegionalTrade>();
            internal List<RegionalProduction> Production { get; } = new List<RegionalProduction>();
            internal List<RegionalConsumption> Consumption { get; } = new List<RegionalConsumption>();
            internal List<RegionalMovement> Movements { get; } = new List<RegionalMovement>();
            internal List<RegionalDecision> Decisions { get; } = new List<RegionalDecision>();

            internal WorkingWorld(ContentCatalog content, RegionalWorldSnapshot before)
            {
                this.content = content;
                this.before = before;
                Day = before.Day;
                foreach (var port in before.Ports.Values) Ports.Add(port.Id, new WorkingPort(content, port));
                foreach (var ship in before.Ships.Values) Ships.Add(ship.Id, new WorkingShip(content, ship));
            }
            internal RegionalWorldSnapshot View(long? revision = null) => new RegionalWorldSnapshot(Day,
                revision ?? before.Revision, Ports.Values.Select(p => p.Snapshot()),
                Ships.Values.Select(s => s.Snapshot()), before.Routes);

            internal TradeAttempt Trade(string shipId, string portId, TradeCommand command)
            {
                if (command == null) throw new ArgumentNullException(nameof(command));
                var ship = Ships[shipId];
                var port = Ports[portId];
                if (ship.Dock == null || ship.Dock != portId)
                    throw new InvalidOperationException("Trade requires the ship to be docked at the selected port.");
                var attempt = TradeCalculator.Preview(port.Configuration.Market.Quotes[command.GoodId], command,
                    port.Inventory.GetQuantity(command.GoodId), ship.Cargo.GetQuantity(command.GoodId),
                    port.Cash, ship.Cash, ship.Configuration.Ship.CargoCapacity - ship.CargoUnits);
                TradeCalculator.Apply(attempt, port.Inventory, ship.Cargo);
                ship.CargoUnits = checked(ship.CargoUnits + attempt.TraderInventoryChange.Delta);
                port.Cash = attempt.MarketCashChange.After;
                ship.Cash = attempt.TraderCashChange.After;
                Trades.Add(new RegionalTrade(shipId, ship.Configuration.OwnerId, portId, attempt));
                if (attempt.Outcome == TradeOutcome.Traded) Changed = true;
                return attempt;
            }

            internal void Depart(string shipId, string destinationPortId)
            {
                var ship = Ships[shipId];
                if (ship.Dock == null) throw new InvalidOperationException("A sailing ship cannot depart again.");
                var route = before.GetRoute(ship.Dock, destinationPortId);
                ship.Duration = route.DurationDays(ship.Configuration.Speed);
                ship.Remaining = ship.Duration;
                ship.Origin = ship.Dock;
                ship.Destination = destinationPortId;
                ship.Dock = null;
                Movements.Add(new RegionalMovement(shipId, ship.Origin, destinationPortId,
                    ShipMovementKind.Departure, ship.Remaining));
                Changed = true;
            }

            internal void ApplyDecision(RegionalDecision decision)
            {
                Decisions.Add(decision);
                if (decision.Action == AutomationAction.Wait || decision.Action == AutomationAction.Pause ||
                    decision.Action == AutomationAction.ContinueSailing) return;
                var detail = decision.Detail ?? throw new InvalidOperationException("An actionable decision needs commands.");
                if (detail.Trade != null)
                {
                    var fresh = View().PreviewTrade(decision.ShipId, detail.Trade.PortId, detail.Trade.Trade);
                    if (fresh.Outcome != TradeOutcome.Traded || detail.Preview == null ||
                        fresh.ExecutedAmount != detail.Preview.ExecutedAmount ||
                        fresh.ExecutedQuantity != detail.Preview.ExecutedQuantity)
                        throw new InvalidOperationException("Automation purchase/sale no longer matches its preview.");
                    var attempt = Trade(decision.ShipId, detail.Trade.PortId, detail.Trade.Trade);
                    if (attempt.Outcome != TradeOutcome.Traded)
                        throw new InvalidOperationException("A validated automated trade must execute before departure.");
                }
                if (detail.Departure != null) Depart(decision.ShipId, detail.Departure.DestinationPortId);
            }

            internal void RunAutomation(string shipId, int limit)
            {
                for (var action = 0; action < limit; action++)
                {
                    var decision = RegionalAutomation.Evaluate(View(), shipId);
                    var tradeCount = Trades.Count;
                    var movementCount = Movements.Count;
                    ApplyDecision(decision);
                    if (Ships[shipId].Dock == null || (tradeCount == Trades.Count && movementCount == Movements.Count)) break;
                }
            }

            internal void Produce(long nextDay)
            {
                foreach (var entry in Ports)
                {
                    var port = entry.Value;
                    for (var index = 0; index < port.Facilities.Length; index++)
                    {
                        var facility = port.Facilities[index];
                        if (nextDay < facility.NextDueDay) continue;
                        var config = facility.Configuration;
                        var instance = config.Instance;
                        var recipe = content.GetRecipe(instance.RecipeId);
                        var starting = port.Inventory.Snapshot();
                        var outcome = ScheduledProductionOutcome.Disabled;
                        if (facility.Enabled)
                        {
                            if (recipe.Inputs.Any(i => port.Inventory.GetQuantity(i.GoodId) < i.Quantity))
                                outcome = ScheduledProductionOutcome.InputShortage;
                            else if (config.OutputStockLimit.HasValue && recipe.Outputs.Any(output =>
                                (long)port.Inventory.GetQuantity(output.GoodId) -
                                recipe.Inputs.Where(i => i.GoodId == output.GoodId).Sum(i => (long)i.Quantity) +
                                output.Quantity > config.OutputStockLimit.Value))
                                outcome = ScheduledProductionOutcome.OutputLimit;
                            else
                                outcome = Simulation.Production.TryRunBatch(port.Inventory, instance.BuildingId, instance.RecipeId)
                                    ? ScheduledProductionOutcome.Produced : ScheduledProductionOutcome.InputShortage;
                        }
                        var due = outcome == ScheduledProductionOutcome.Produced
                            ? checked(nextDay + config.IntervalDays) : facility.NextDueDay;
                        port.Facilities[index] = new ScheduledFacilitySnapshot(config, facility.Enabled, due, outcome);
                        var changes = starting.Keys.Where(id => starting[id] != port.Inventory.GetQuantity(id))
                            .Select(id => new InventoryChange(id, starting[id], port.Inventory.GetQuantity(id)));
                        Production.Add(new RegionalProduction(entry.Key, instance.Id, instance.RecipeId, outcome, changes));
                    }
                }
            }

            internal void Consume(long nextDay)
            {
                foreach (var entry in Ports)
                {
                    var port = entry.Value;
                    for (var index = 0; index < port.Consumption.Length; index++)
                    {
                        var demand = port.Consumption[index];
                        if (nextDay < demand.NextDueDay) continue;
                        var config = demand.Configuration;
                        var stock = port.Inventory.GetQuantity(config.GoodId);
                        var consumed = stock >= config.Quantity ? config.Quantity : 0;
                        var unmet = config.Quantity - consumed;
                        port.Inventory.Commit(new Dictionary<string, int> { [config.GoodId] = stock - consumed });
                        port.Consumption[index] = new ConsumptionSnapshot(config, checked(nextDay + config.IntervalDays),
                            unmet, checked(demand.TotalUnmetQuantity + unmet));
                        Consumption.Add(new RegionalConsumption(entry.Key, config, consumed));
                    }
                }
            }

            internal void AdvanceVoyages()
            {
                foreach (var entry in Ships)
                {
                    var ship = entry.Value;
                    if (ship.Destination == null) continue;
                    ship.Remaining = checked(ship.Remaining - 1);
                    Movements.Add(new RegionalMovement(entry.Key, ship.Origin!, ship.Destination,
                        ShipMovementKind.Progress, ship.Remaining));
                    if (ship.Remaining != 0) continue;
                    Movements.Add(new RegionalMovement(entry.Key, ship.Origin!, ship.Destination, ShipMovementKind.Arrival, 0));
                    ship.Dock = ship.Destination;
                    ship.Origin = null;
                    ship.Destination = null;
                }
            }
        }
    }
}
