using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// One serialized authority for regional accounts, schedules and voyages.
    /// Instant commands and complete daily advances share privately staged domain operations.
    /// </summary>
    public sealed partial class RegionalWorldState
    {
        public const int MaximumDocksideActions = 4;
        private readonly ContentCatalog content;
        private RegionalWorldSnapshot state;
        private bool mutating;
        private readonly BigInteger initialCombinedCash;

        public RegionalWorldState(ContentCatalog content, IEnumerable<RegionalPortConfiguration> ports,
            IEnumerable<SeaRoute> routes, IEnumerable<RegionalShipConfiguration> ships,
            long initialDay = 0, long initialRevision = 0, IEnumerable<CompanyConfiguration>? companies = null,
            IEnumerable<IndustryBlueprint>? industryBlueprints = null, ChallengeConfiguration? challenge = null,
            IEnumerable<PrivateFacilityConfiguration>? initialFacilities = null,
            string initialGrantProvenance = "initial_inventory", RunDifficulty difficulty = RunDifficulty.Normal,
            RunIdentity? runIdentity = null, ExpandingEconomyConfiguration? economy = null)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            if (ports == null) throw new ArgumentNullException(nameof(ports));
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            if (ships == null) throw new ArgumentNullException(nameof(ships));
            if (!Enum.IsDefined(typeof(RunDifficulty), difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            if (runIdentity != null && runIdentity.Difficulty != difficulty)
                throw new ArgumentException("Run identity and world difficulty must agree.", nameof(runIdentity));
            if (runIdentity != null && runIdentity.ChallengeId != challenge?.Id)
                throw new ArgumentException("Run identity and world challenge must agree.", nameof(runIdentity));
            var pooledStorage = runIdentity?.CompanyStorageRules == CompanyStorageRules.Pooled;
            if (pooledStorage && economy == null)
                throw new ArgumentException("Pooled storage requires the v3 economy.", nameof(economy));
            if (challenge?.Kind == ChallengeKind.GrandCommission &&
                (runIdentity?.Commission == null || economy == null))
                throw new ArgumentException("A Grand Commission needs its resolved v3 run identity.", nameof(runIdentity));
            if (initialDay < 0 || initialRevision < 0) throw new ArgumentOutOfRangeException(nameof(initialDay));
            var locations = new List<RegionalPortSnapshot>();
            foreach (var port in ports)
            {
                if (port == null) throw new ArgumentException("Ports cannot contain null.");
                var market = port.Market.CreateSnapshot(content);
                var city = port.Settlement == null ? null : CitySnapshot.Initial(port.Settlement, initialDay);
                if (city != null)
                    market = new PortSnapshot(port.Id, market.Inventory, market.MarketCash,
                        LivingCityPricing.Derive(port, city), market.Buildings);
                foreach (var demand in port.Consumption) content.GetGood(demand.GoodId);
                locations.Add(new RegionalPortSnapshot(port, market,
                    port.Facilities.Select(f => new ScheduledFacilitySnapshot(f, f.Instance.ProductionEnabled,
                        f.FirstDueDay, ScheduledProductionOutcome.NotDue)),
                    port.Consumption.Select(c => new ConsumptionSnapshot(c, c.FirstDueDay, 0, 0)), city));
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
            var accounts = new SortedDictionary<string, CompanySnapshot>(StringComparer.Ordinal);
            if (companies != null)
                foreach (var company in companies)
                {
                    if (company == null) throw new ArgumentException("Companies cannot contain null.");
                    accounts.Add(company.Id, new CompanySnapshot(company.Id, company.InitialCash));
                }
            foreach (var ship in ships)
            {
                if (ship == null) throw new ArgumentException("Ships cannot contain null.");
                if (companies == null)
                    accounts.Add(ship.OwnerId, new CompanySnapshot(ship.OwnerId, ship.InitialCash));
                else if (ship.InitialCash != 0)
                    throw new ArgumentException("Explicit company accounts require zero ship InitialCash.");
                if (!accounts.ContainsKey(ship.OwnerId)) throw new ArgumentException("Unknown ship owner.");
                if (!locations.Any(p => p.Id == ship.Ship.InitialPortId)) throw new ArgumentException("Unknown initial dock.");
                var cargo = new Inventory(content, ship.Ship.InitialCargo, economy == null ? 1 : EconomicUnits.QuantityScale);
                vessels.Add(new RegionalShipSnapshot(ship, new ShipSnapshot(ship.Ship.Id, ship.Ship.CargoCapacity,
                    cargo.Snapshot(), ship.Ship.InitialPortId, null, null, 0), accounts[ship.OwnerId].Cash, ship.Policy,
                    ship.AutomationEnabled, 0));
            }
            if (vessels.Count == 0) throw new ArgumentException("A region needs at least one ship.");
            var blueprints = (industryBlueprints ?? Array.Empty<IndustryBlueprint>()).ToArray();
            foreach (var blueprint in blueprints)
            {
                if (blueprint == null) throw new ArgumentException("Blueprints cannot contain null.");
                content.GetBuilding(blueprint.BuildingId);
                var recipe = content.GetRecipe(blueprint.RecipeId);
                if (recipe.BuildingId != blueprint.BuildingId) throw new ArgumentException("Blueprint recipe belongs to another building.");
                if (blueprint.EligiblePortIds.Any(id => !locations.Any(p => p.Id == id)))
                    throw new ArgumentException("Blueprint has an unknown eligible port.");
                if (blueprint.WagePerBatch > 0 && blueprint.EligiblePortIds.Any(id => locations.Single(p => p.Id == id).City == null))
                    throw new ArgumentException("Paid private industries require local households.");
            }
            if (challenge != null)
            {
                if (!accounts.ContainsKey(challenge.OwnerId) || !locations.Any(p => p.Id == challenge.PortId))
                    throw new ArgumentException("Unknown challenge owner or destination.");
                content.GetGood(challenge.GoodId);
                if (challenge.DeadlineDay <= initialDay) throw new ArgumentException("Challenge deadline must follow the starting day.");
                if (challenge.Kind == ChallengeKind.CityRecovery && locations.Single(p => p.Id == challenge.PortId).City == null)
                    throw new ArgumentException("Recovery objectives require a living target city.");
            }
            var accounting = new WorkingAccounting();
            foreach (var account in accounts.Values) accounting.RegisterCompany(account.Id, account.Cash);
            foreach (var vessel in vessels.OrderBy(v => v.Id, StringComparer.Ordinal))
            {
                accounting.RegisterStore("ship:" + vessel.Id, vessel.OwnerId);
                foreach (var stock in vessel.Cargo.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal))
                    accounting.Grant("ship:" + vessel.Id, stock.Key, stock.Value, initialGrantProvenance);
            }
            var privateFacilities = new List<PrivateFacilitySnapshot>();
            var stockpiles = new SortedDictionary<string, WorkingStockpile>(StringComparer.Ordinal);
            var facilityIds = new HashSet<string>(locations.SelectMany(p => p.Facilities)
                .Select(f => f.Configuration.Instance.Id), StringComparer.Ordinal);
            foreach (var configured in (initialFacilities ?? Array.Empty<PrivateFacilityConfiguration>())
                .OrderBy(f => f.Id, StringComparer.Ordinal))
            {
                if (!accounts.ContainsKey(configured.OwnerId)) throw new ArgumentException("Unknown starter facility owner.");
                var blueprint = blueprints.SingleOrDefault(b => b.Id == configured.BlueprintId)
                    ?? throw new ArgumentException("Unknown starter facility blueprint.");
                if (!blueprint.EligiblePortIds.Contains(configured.PortId))
                    throw new ArgumentException("Starter facility is not eligible at its port.");
                if (!facilityIds.Add(configured.Id)) throw new ArgumentException("Duplicate starter facility ID.");
                if (economy == null && privateFacilities.Any(f => f.OwnerId == configured.OwnerId && f.PortId == configured.PortId &&
                    f.Blueprint.Id == configured.BlueprintId)) throw new ArgumentException("Duplicate starter industry at a port.");
                ValidateIndustryPolicy(content.GetRecipe(blueprint.RecipeId), blueprint, configured.Policy);
                var inventory = new Inventory(content, configured.InitialInventory,
                    economy == null ? 1 : EconomicUnits.QuantityScale).Snapshot();
                if (inventory.Values.Sum(v => (long)v) > blueprint.WarehouseCapacity)
                    throw new ArgumentException("Starter inventory exceeds warehouse capacity.");
                var storeId = pooledStorage ? CompanyStockpileSnapshot.StoreId(configured.OwnerId, configured.PortId) :
                    "facility:" + configured.Id;
                if (pooledStorage)
                {
                    if (!stockpiles.TryGetValue(storeId, out var store))
                    {
                        store = new WorkingStockpile(content, storeId, configured.OwnerId, configured.PortId, 0,
                            new Inventory(content, quantityScale: EconomicUnits.QuantityScale).Snapshot(), EconomicUnits.QuantityScale);
                        stockpiles.Add(storeId, store);
                        accounting.RegisterStore(storeId, configured.OwnerId);
                    }
                    store.Capacity = checked(store.Capacity + blueprint.WarehouseCapacity);
                    var quantities = inventory.ToDictionary(p => p.Key,
                        p => checked(p.Value + store.Inventory.GetQuantity(p.Key)), StringComparer.Ordinal);
                    store.Inventory.Commit(quantities);
                    store.StoredUnits = checked(store.StoredUnits + (int)inventory.Values.Sum(v => (long)v));
                }
                else accounting.RegisterStore(storeId, configured.OwnerId);
                var totals = content.Goods.ToDictionary(g => g.Id, _ => 0L, StringComparer.Ordinal);
                privateFacilities.Add(new PrivateFacilitySnapshot(configured.Id, configured.OwnerId, configured.PortId,
                    blueprint, configured.Policy, configured.Enabled, initialDay, checked(initialDay + blueprint.IntervalDays),
                    ScheduledProductionOutcome.NotDue, pooledStorage ? new Inventory(content).Snapshot() : inventory, 0, 0, 0, totals, totals,
                    IndustryWaitReason.None, IndustryWaitReason.None, true, configured.Provenance,
                    sharedStockpileId: pooledStorage ? storeId : null));
                foreach (var stock in inventory.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal))
                    accounting.Grant(storeId, stock.Key, stock.Value, configured.Provenance);
            }
            var initialChallenge = challenge == null ? null : ChallengeRules.Create(challenge);
            if (challenge?.Kind == ChallengeKind.CityRecovery)
                initialChallenge = ChallengeRules.Evaluate(initialChallenge!, initialDay, accounts[challenge.OwnerId].Cash,
                    accounting.Snapshot().Companies[challenge.OwnerId], locations.Single(p => p.Id == challenge.PortId).City);
            state = new RegionalWorldSnapshot(initialDay, initialRevision, locations, vessels, connections,
                accounts.Values, blueprints, privateFacilities,
                initialChallenge, accounting.Snapshot(), difficulty, runIdentity,
                economy == null ? null : ExpandingEconomySnapshot.Initial(economy),
                runIdentity?.Commission == null ? null : CommissionHistory.Initial(vessels, challenge!.OwnerId, initialDay),
                stockpiles.Values.Select(s => s.Snapshot()));
            if (economy != null)
            {
                ValidateExpandingConfiguration(economy);
                var working = new WorkingWorld(content, state);
                foreach (var company in state.Companies.Values)
                    working.Economy!.InitialValues.Add(company.Id, state.CompanyValue(company.Id).Total);
                if (runIdentity?.Commission != null) working.EvaluateChallenge();
                state = working.View();
            }
            initialCombinedCash = state.TotalCash;
            foreach (var vessel in state.Ships.Values)
                if (vessel.Policy != null) RegionalAutomation.ValidatePolicy(state, vessel.Id, vessel.Policy);
        }

        public RegionalWorldSnapshot Snapshot() => state;
        public RegionalDecision EvaluateAutomation(string shipId) => RegionalAutomation.Evaluate(state, shipId);

        public RegionalResult AbandonChallenge() => Mutate(work =>
        {
            if (work.Challenge == null) throw new InvalidOperationException("The sandbox has no challenge to abandon.");
            work.Challenge = ChallengeRules.Abandon(work.Challenge, work.Day);
            work.Changed = true;
        });

        public RegionalResult ExecuteTrade(string shipId, string portId, TradeCommand command,
            RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work =>
            {
                EnsureCurrent(expectedSnapshot);
                EnsureManual(work.Ships[shipId]);
                work.Trade(shipId, portId, command);
            });

        public RegionalResult ConstructFacility(string ownerId, string portId, string blueprintId, string facilityId) =>
            Mutate(work => work.Construct(ownerId, portId, blueprintId, facilityId));

        public RegionalResult SetFacilityEnabled(string ownerId, string facilityId, bool enabled) =>
            Mutate(work =>
            {
                var facility = work.OwnedFacility(ownerId, facilityId);
                if (facility.Enabled == enabled) return;
                facility.Enabled = enabled;
                if (!enabled && facility.ConstructionStatus == FacilityConstructionStatus.Operational)
                    facility.LastOutcome = ScheduledProductionOutcome.Disabled;
                work.Changed = true;
            });

        public RegionalResult ConfigureFacility(string ownerId, string facilityId, IndustryPolicy policy) =>
            Mutate(work =>
            {
                if (policy == null) throw new ArgumentNullException(nameof(policy));
                var facility = work.OwnedFacility(ownerId, facilityId);
                var recipe = content.GetRecipe(facility.Blueprint.RecipeId);
                ValidateIndustryPolicy(recipe, facility.Blueprint, policy);
                if (ReferenceEquals(facility.Policy, policy)) return;
                facility.Policy = policy;
                work.Changed = true;
            });

        private static void ValidateIndustryPolicy(RecipeDefinition recipe, IndustryBlueprint blueprint, IndustryPolicy policy)
        {
            if (policy.Inputs.Any(r => !recipe.Inputs.Any(i => i.GoodId == r.GoodId)) ||
                policy.Outputs.Any(r => !recipe.Outputs.Any(o => o.GoodId == r.GoodId)))
                throw new ArgumentException("Industry policy rules must match the fixed canonical recipe.");
            if (policy.Inputs.Any(r => r.TargetQuantity > blueprint.WarehouseCapacity) ||
                policy.Outputs.Any(r => r.ReserveQuantity > blueprint.WarehouseCapacity))
                throw new ArgumentException("Policy stock levels cannot exceed warehouse capacity.");
        }

        public RegionalResult TransferGoods(string ownerId, string shipId, string facilityId,
            TransferDirection direction, string goodId, int quantity, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work =>
            {
                EnsureCurrent(expectedSnapshot);
                EnsureManual(work.Ships[shipId]);
                work.Transfer(ownerId, shipId, facilityId, direction, goodId, quantity);
            });

        public RegionalResult ExecuteWarehouseTrade(string ownerId, string facilityId, TradeCommand command,
            RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work =>
            {
                EnsureCurrent(expectedSnapshot);
                work.WarehouseTrade(work.OwnedFacility(ownerId, facilityId), command);
            });

        private void EnsureCurrent(RegionalWorldSnapshot? expected)
        {
            if (expected != null && !ReferenceEquals(expected, state))
                throw new InvalidOperationException("The preview is stale; refresh the current revision.");
        }

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
                ship.IndustrialPolicy = null;
                ship.AutomationEnabled = enabled;
                work.Changed = true;
            });
        }

        public RegionalResult ConfigureIndustrialAutomation(string shipId, IndustrialRoutePolicy policy, bool enabled)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            return Mutate(work =>
            {
                IndustrialRouteEvaluator.ValidatePolicy(state, shipId, policy);
                var ship = work.Ships[shipId];
                if (ReferenceEquals(ship.IndustrialPolicy, policy))
                {
                    if (ship.AutomationEnabled != enabled) { ship.AutomationEnabled = enabled; work.Changed = true; }
                    return;
                }
                if (ship.CargoUnits != 0 || (ship.Dock != policy.OriginPortId && ship.Dock != policy.DestinationPortId))
                    throw new InvalidOperationException("Configure an industrial route with an empty ship docked at either route port.");
                ship.Policy = null;
                ship.IndustrialPolicy = policy;
                ship.IndustrialStage = ship.Dock == policy.OriginPortId
                    ? IndustrialRouteStage.AcquireOutbound : IndustrialRouteStage.AcquireReturn;
                ship.AutomationEnabled = enabled;
                work.Changed = true;
            });
        }

        public RegionalResult SetAutomationEnabled(string shipId, bool enabled) =>
            Mutate(work =>
            {
                var ship = work.Ships[shipId];
                if (enabled && ship.Policy == null && ship.IndustrialPolicy == null)
                    throw new InvalidOperationException("Configure a route first.");
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
            work.BeginCityDay();
            foreach (var ship in work.Ships.Values.OrderBy(s => s.Configuration.Priority)
                .ThenBy(s => s.Configuration.Ship.Id, StringComparer.Ordinal))
                if (ship.AutomationEnabled) work.RunAutomation(ship.Configuration.Ship.Id, MaximumDocksideActions);
            work.Procure();
            work.Produce(nextDay);
            work.ProducePrivate(nextDay);
            work.SellPrivate();
            work.Consume(nextDay);
            work.FinishCityDay(nextDay);
            work.AdvanceVoyages();
            // A completion commits only after this day's automatic phases. Its full production
            // interval begins now; local warehouse access never grants an early batch.
            work.CompleteConstruction(nextDay);
            work.CompleteDevelopment(nextDay);
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
            if (state.Challenge != null && state.Challenge.Status != ChallengeStatus.Active)
                throw new InvalidOperationException("The challenge has ended. Reset to begin another world.");
            mutating = true;
            try
            {
                var before = state;
                var work = new WorkingWorld(content, before);
                operation(work);
                work.EvaluateChallenge();
                var after = work.Changed ? work.View(checked(before.Revision + 1)) : before;
                if (after.TotalCash != initialCombinedCash + (after.Economy?.MintedCurrency ?? 0))
                    throw new InvalidOperationException("Market, household and company cash must remain conserved.");
                foreach (var company in after.Companies.Values)
                {
                    var finance = after.Accounting.Companies[company.Id];
                    if ((BigInteger)company.Cash != (BigInteger)finance.InitialCash + finance.SalesRevenue -
                        finance.InventoryPurchases - finance.ConstructionExpenditure - finance.WagesPaid +
                        finance.MintReceipts - finance.PublicCommissionExpenditure)
                        throw new InvalidOperationException("Company cash must reconcile with recorded actual payments.");
                    _ = finance.OperatingSurplusAfterWages;
                }
                var result = new RegionalResult(before, after, work.Trades, work.Production, work.Consumption,
                    work.Movements, work.Decisions, work.WarehouseTrades, work.PrivateProduction,
                    work.Construction, work.Transfers, work.Accounting.Events(after.Day, after.Revision),
                    work.CashTransfers.Select(t => t.AtCommit(after.Day, after.Revision)), work.CityChanges,
                    work.Development);
                AuditExpandingCommit(result);
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
            internal WorkingCity? City { get; }
            internal IReadOnlyDictionary<string, MarketQuote> Quotes { get; set; }
            internal WorkingPort(ContentCatalog content, RegionalPortSnapshot before)
            {
                Configuration = before.Configuration;
                Inventory = new Inventory(content, before.Inventory, before.City?.Configuration.QuantityScale ?? 1);
                Cash = before.MarketCash;
                Facilities = before.Facilities.ToArray();
                Consumption = before.Consumption.ToArray();
                City = before.City == null ? null : new WorkingCity(before.City);
                Quotes = before.Quotes;
            }
            internal RegionalPortSnapshot Snapshot() => new RegionalPortSnapshot(Configuration,
                new PortSnapshot(Configuration.Id, Inventory.Snapshot(), Cash, Quotes,
                    Facilities.Select(f => f.Configuration.Instance.WithProductionEnabled(f.Enabled))),
                Facilities, Consumption, City?.Snapshot());
        }

        private sealed class WorkingShip
        {
            internal RegionalShipConfiguration Configuration { get; }
            internal Inventory Cargo { get; }
            internal int CargoUnits { get; set; }
            internal TradeRoutePolicy? Policy { get; set; }
            internal IndustrialRoutePolicy? IndustrialPolicy { get; set; }
            internal IndustrialRouteStage IndustrialStage { get; set; }
            internal bool AutomationEnabled { get; set; }
            internal string? Dock { get; set; }
            internal string? Origin { get; set; }
            internal string? Destination { get; set; }
            internal int Remaining { get; set; }
            internal int Duration { get; set; }
            internal WorkingShip(ContentCatalog content, RegionalShipSnapshot before, int quantityScale = 1)
            {
                Configuration = before.Configuration;
                Cargo = new Inventory(content, before.Cargo, quantityScale);
                CargoUnits = before.CargoUnits;
                Policy = before.Policy;
                IndustrialPolicy = before.IndustrialPolicy;
                IndustrialStage = before.IndustrialStage;
                AutomationEnabled = before.AutomationEnabled;
                Dock = before.DockedPortId;
                Origin = before.OriginPortId;
                Destination = before.DestinationPortId;
                Remaining = before.RemainingTravelDays;
                Duration = before.VoyageDurationDays;
            }
            internal RegionalShipSnapshot Snapshot(long cash) => new RegionalShipSnapshot(Configuration,
                new ShipSnapshot(Configuration.Ship.Id, Configuration.Ship.CargoCapacity, Cargo.Snapshot(),
                    Dock, Origin, Destination, Remaining), cash, Policy, AutomationEnabled, Duration,
                    IndustrialPolicy, IndustrialStage);
        }

        private sealed class WorkingCompany
        {
            internal string Id { get; }
            internal long Cash { get; set; }
            internal WorkingCompany(CompanySnapshot before) { Id = before.Id; Cash = before.Cash; }
            internal CompanySnapshot Snapshot() => new CompanySnapshot(Id, Cash);
        }

        private sealed class WorkingStockpile
        {
            internal string Id { get; }
            internal string OwnerId { get; }
            internal string PortId { get; }
            internal int Capacity { get; set; }
            internal Inventory Inventory { get; }
            internal int StoredUnits { get; set; }
            internal WorkingStockpile(ContentCatalog content, string id, string ownerId, string portId,
                int capacity, IReadOnlyDictionary<string, int> inventory, int quantityScale)
            {
                Id = id; OwnerId = ownerId; PortId = portId; Capacity = capacity;
                Inventory = new Inventory(content, inventory, quantityScale);
                StoredUnits = checked((int)inventory.Values.Sum(v => (long)v));
            }
            internal CompanyStockpileSnapshot Snapshot() => new CompanyStockpileSnapshot(Id, OwnerId, PortId, Capacity, Inventory.Snapshot());
        }

        private sealed class WorkingFacility
        {
            internal string Id { get; }
            internal string OwnerId { get; }
            internal string PortId { get; }
            internal IndustryBlueprint Blueprint { get; }
            internal IndustryPolicy Policy { get; set; }
            internal bool Enabled { get; set; }
            internal long CreatedDay { get; }
            internal FacilityConstructionStatus ConstructionStatus { get; set; }
            internal long ConstructionCompletionDay { get; }
            internal bool IsStarterGrant { get; }
            internal string Provenance { get; }
            internal long NextProductionDay { get; set; }
            internal ScheduledProductionOutcome LastOutcome { get; set; }
            private readonly Inventory privateInventory;
            private readonly WorkingStockpile? sharedStore;
            private int privateStoredUnits;
            internal Inventory Inventory => sharedStore?.Inventory ?? privateInventory;
            internal string StoreId => sharedStore?.Id ?? "facility:" + Id;
            internal int Capacity => sharedStore?.Capacity ?? Blueprint.WarehouseCapacity;
            internal int StoredUnits
            {
                get => sharedStore?.StoredUnits ?? privateStoredUnits;
                set { if (sharedStore == null) privateStoredUnits = value; else sharedStore.StoredUnits = value; }
            }
            internal long BatchesProduced { get; set; }
            internal long ProcurementCost { get; set; }
            internal long SalesRevenue { get; set; }
            internal long WagesPaid { get; set; }
            internal Dictionary<string, long> Produced { get; }
            internal Dictionary<string, long> Consumed { get; }
            internal IndustryWaitReason ProcurementWaitReason { get; set; }
            internal IndustryWaitReason SalesWaitReason { get; set; }
            internal WorkingFacility(ContentCatalog content, PrivateFacilitySnapshot before, int quantityScale = 1,
                WorkingStockpile? sharedStore = null)
            {
                Id = before.Id; OwnerId = before.OwnerId; PortId = before.PortId; Blueprint = before.Blueprint;
                Policy = before.Policy; Enabled = before.Enabled; CreatedDay = before.CreatedDay;
                IsStarterGrant = before.IsStarterGrant; Provenance = before.Provenance;
                ConstructionStatus = before.ConstructionStatus; ConstructionCompletionDay = before.ConstructionCompletionDay;
                NextProductionDay = before.NextProductionDay; LastOutcome = before.LastOutcome;
                privateInventory = new Inventory(content, before.Inventory, quantityScale);
                privateStoredUnits = before.StoredUnits; this.sharedStore = sharedStore;
                BatchesProduced = before.BatchesProduced; ProcurementCost = before.ProcurementCost; SalesRevenue = before.SalesRevenue;
                WagesPaid = before.WagesPaid;
                Produced = before.ProducedQuantities.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                Consumed = before.ConsumedQuantities.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                ProcurementWaitReason = before.ProcurementWaitReason; SalesWaitReason = before.SalesWaitReason;
            }
            internal PrivateFacilitySnapshot Snapshot(long day) => new PrivateFacilitySnapshot(Id, OwnerId, PortId, Blueprint,
                Policy, Enabled, CreatedDay, NextProductionDay, LastOutcome, privateInventory.Snapshot(), BatchesProduced,
                ProcurementCost, SalesRevenue, Produced, Consumed, ProcurementWaitReason, SalesWaitReason,
                IsStarterGrant, Provenance, ConstructionStatus, ConstructionCompletionDay, day, WagesPaid, sharedStore?.Id);
        }

        private sealed partial class WorkingWorld
        {
            private readonly ContentCatalog content;
            private readonly RegionalWorldSnapshot before;
            internal long Day { get; set; }
            internal bool Changed { get; set; }
            internal SortedDictionary<string, WorkingPort> Ports { get; } = new SortedDictionary<string, WorkingPort>(StringComparer.Ordinal);
            internal SortedDictionary<string, WorkingShip> Ships { get; } = new SortedDictionary<string, WorkingShip>(StringComparer.Ordinal);
            internal SortedDictionary<string, WorkingCompany> Companies { get; } = new SortedDictionary<string, WorkingCompany>(StringComparer.Ordinal);
            internal SortedDictionary<string, WorkingFacility> PrivateFacilities { get; } = new SortedDictionary<string, WorkingFacility>(StringComparer.Ordinal);
            internal SortedDictionary<string, WorkingStockpile> CompanyStockpiles { get; } = new SortedDictionary<string, WorkingStockpile>(StringComparer.Ordinal);
            internal ChallengeSnapshot? Challenge { get; set; }
            internal WorkingAccounting Accounting { get; }
            internal WorkingCommissionHistory? Commission { get; }
            internal List<RegionalTrade> Trades { get; } = new List<RegionalTrade>();
            internal List<RegionalProduction> Production { get; } = new List<RegionalProduction>();
            internal List<RegionalConsumption> Consumption { get; } = new List<RegionalConsumption>();
            internal List<RegionalMovement> Movements { get; } = new List<RegionalMovement>();
            internal List<RegionalDecision> Decisions { get; } = new List<RegionalDecision>();
            internal List<RegionalWarehouseTrade> WarehouseTrades { get; } = new List<RegionalWarehouseTrade>();
            internal List<RegionalPrivateProduction> PrivateProduction { get; } = new List<RegionalPrivateProduction>();
            internal List<RegionalConstruction> Construction { get; } = new List<RegionalConstruction>();
            internal List<RegionalTransfer> Transfers { get; } = new List<RegionalTransfer>();
            internal List<CityCashTransfer> CashTransfers { get; } = new List<CityCashTransfer>();
            internal List<CityChange> CityChanges { get; } = new List<CityChange>();

            internal WorkingWorld(ContentCatalog content, RegionalWorldSnapshot before)
            {
                this.content = content;
                this.before = before;
                Day = before.Day;
                foreach (var port in before.Ports.Values) Ports.Add(port.Id, new WorkingPort(content, port));
                foreach (var ship in before.Ships.Values) Ships.Add(ship.Id, new WorkingShip(content, ship, before.QuantityScale));
                foreach (var company in before.Companies.Values) Companies.Add(company.Id, new WorkingCompany(company));
                foreach (var store in before.CompanyStockpiles.Values)
                    CompanyStockpiles.Add(store.Id, new WorkingStockpile(content, store.Id, store.OwnerId, store.PortId,
                        store.Capacity, store.Inventory, before.QuantityScale));
                foreach (var facility in before.PrivateFacilities.Values)
                    PrivateFacilities.Add(facility.Id, new WorkingFacility(content, facility, before.QuantityScale,
                        before.UsesPooledCompanyStorage ? CompanyStockpiles[CompanyStockpileSnapshot.StoreId(facility.OwnerId, facility.PortId)] : null));
                Challenge = before.Challenge;
                Accounting = new WorkingAccounting(before.Accounting);
                Economy = before.Economy == null ? null : new WorkingEconomy(before.Economy);
                Commission = before.CommissionHistory == null ? null : new WorkingCommissionHistory(before.CommissionHistory);
            }
            internal RegionalWorldSnapshot View(long? revision = null) => new RegionalWorldSnapshot(Day,
                revision ?? before.Revision, Ports.Values.Select(p => p.Snapshot()),
                Ships.Values.Select(s => s.Snapshot(Companies[s.Configuration.OwnerId].Cash)), before.Routes,
                Companies.Values.Select(c => c.Snapshot()), before.IndustryBlueprints.Values,
                PrivateFacilities.Values.Select(f => f.Snapshot(Day)), Challenge, Accounting.Snapshot(),
                before.Difficulty, before.RunIdentity, Economy?.Snapshot(), Commission?.Snapshot(),
                CompanyStockpiles.Values.Select(s => s.Snapshot()));

            internal void EvaluateChallenge()
            {
                if (Challenge == null) return;
                var after = ChallengeRules.Evaluate(Challenge, Day, Companies[Challenge.Configuration.OwnerId].Cash,
                    Accounting.Snapshot().Companies[Challenge.Configuration.OwnerId], Ports[Challenge.Configuration.PortId].City?.Snapshot(),
                    Economy == null ? null : View());
                if (!ReferenceEquals(Challenge, after)) { Challenge = after; Changed = true; }
            }

            internal TradeAttempt Trade(string shipId, string portId, TradeCommand command)
            {
                if (command == null) throw new ArgumentNullException(nameof(command));
                before.ValidateWholeCargo(command.Quantity);
                var ship = Ships[shipId];
                var port = Ports[portId];
                var company = Companies[ship.Configuration.OwnerId];
                if (ship.Dock == null || ship.Dock != portId)
                    throw new InvalidOperationException("Trade requires the ship to be docked at the selected port.");
                var attempt = TradeCalculator.Preview(port.Quotes[command.GoodId], command,
                    port.Inventory.GetQuantity(command.GoodId), ship.Cargo.GetQuantity(command.GoodId),
                    port.Cash, company.Cash, ship.Configuration.Ship.CargoCapacity - ship.CargoUnits);
                TradeCalculator.Apply(attempt, port.Inventory, ship.Cargo);
                ship.CargoUnits = checked(ship.CargoUnits + attempt.TraderInventoryChange.Delta);
                port.Cash = attempt.MarketCashChange.After;
                company.Cash = attempt.TraderCashChange.After;
                Trades.Add(new RegionalTrade(shipId, ship.Configuration.OwnerId, portId, attempt));
                if (Challenge != null) Challenge = ChallengeRules.ApplyTrade(Challenge, company.Id, portId, attempt);
                if (attempt.Outcome == TradeOutcome.Traded)
                {
                    AccountTrade("ship:" + shipId, portId, attempt);
                    if (company.Id == Challenge?.Configuration.OwnerId) Commission?.Trade(portId, attempt);
                    Changed = true;
                }
                return attempt;
            }

            private void AccountTrade(string storeId, string portId, TradeAttempt attempt)
            {
                if (attempt.Direction == TradeDirection.Buy)
                    Accounting.Purchase(storeId, portId, attempt.GoodId, attempt.ExecutedQuantity, attempt.ExecutedAmount);
                else Accounting.Sell(storeId, portId, attempt.GoodId, attempt.ExecutedQuantity, attempt.ExecutedAmount);
            }

            internal WorkingFacility OwnedFacility(string ownerId, string facilityId)
            {
                var facility = PrivateFacilities[facilityId];
                if (facility.OwnerId != ownerId) throw new InvalidOperationException("The facility belongs to another company.");
                return facility;
            }

            internal void Construct(string ownerId, string portId, string blueprintId, string facilityId)
            {
                RuntimeIdentifier.Validate(facilityId, nameof(facilityId));
                var company = Companies[ownerId];
                var port = Ports[portId];
                var blueprint = before.IndustryBlueprints[blueprintId];
                if (!blueprint.EligiblePortIds.Contains(portId)) throw new InvalidOperationException("This industry cannot be built at this port.");
                if (PrivateFacilities.ContainsKey(facilityId) ||
                    Ports.Values.Any(p => p.Facilities.Any(f => f.Configuration.Instance.Id == facilityId)))
                    throw new ArgumentException("A facility with this ID already exists.");
                if (Economy == null && PrivateFacilities.Values.Any(f => f.OwnerId == ownerId && f.PortId == portId && f.Blueprint.Id == blueprintId))
                    throw new InvalidOperationException("This company already owns that industry at this port.");
                if (company.Cash < blueprint.ConstructionCost)
                {
                    Construction.Add(new RegionalConstruction(facilityId, ownerId, portId, blueprintId,
                        ConstructionOutcome.InsufficientFunds, 0, Day));
                    return;
                }
                var nextCash = checked(port.Cash + blueprint.ConstructionCost);
                var schedule = before.PreviewConstructionSchedule(ownerId, blueprintId);
                var completionDay = schedule.CompletionDay;
                var due = schedule.FirstProductionDay;
                var pending = schedule.DelayDays != 0;
                var totals = content.Goods.ToDictionary(g => g.Id, _ => 0L, StringComparer.Ordinal);
                var facility = new PrivateFacilitySnapshot(facilityId, ownerId, portId, blueprint, new IndustryPolicy(),
                    true, Day, due, pending ? ScheduledProductionOutcome.UnderConstruction : ScheduledProductionOutcome.NotDue,
                    new Inventory(content, quantityScale: before.QuantityScale).Snapshot(), 0, 0, 0, totals, totals,
                    pending ? IndustryWaitReason.UnderConstruction : IndustryWaitReason.Disabled,
                    pending ? IndustryWaitReason.UnderConstruction : IndustryWaitReason.Disabled,
                    constructionStatus: pending ? FacilityConstructionStatus.UnderConstruction : FacilityConstructionStatus.Operational,
                    constructionCompletionDay: completionDay, snapshotDay: Day);
                WorkingStockpile? sharedStore = null;
                if (before.UsesPooledCompanyStorage)
                {
                    var storeId = CompanyStockpileSnapshot.StoreId(ownerId, portId);
                    if (!CompanyStockpiles.TryGetValue(storeId, out sharedStore))
                    {
                        sharedStore = new WorkingStockpile(content, storeId, ownerId, portId, 0,
                            new Inventory(content, quantityScale: before.QuantityScale).Snapshot(), before.QuantityScale);
                        CompanyStockpiles.Add(storeId, sharedStore);
                        Accounting.RegisterStore(storeId, ownerId);
                    }
                    sharedStore.Capacity = checked(sharedStore.Capacity + blueprint.WarehouseCapacity);
                }
                else Accounting.RegisterStore("facility:" + facilityId, ownerId);
                PrivateFacilities.Add(facilityId, new WorkingFacility(content, facility, before.QuantityScale, sharedStore));
                Accounting.Construct(ownerId, facilityId, portId, blueprint.ConstructionCost, PrivateFacilities[facilityId].StoreId);
                company.Cash -= blueprint.ConstructionCost;
                port.Cash = nextCash;
                Construction.Add(new RegionalConstruction(facilityId, ownerId, portId, blueprintId,
                    pending ? ConstructionOutcome.Started : ConstructionOutcome.Built,
                    blueprint.ConstructionCost, Day, completionDay, due));
                Changed = true;
            }

            internal void CompleteConstruction(long nextDay)
            {
                foreach (var facility in OrderedFacilities())
                {
                    if (facility.ConstructionStatus != FacilityConstructionStatus.UnderConstruction ||
                        nextDay < facility.ConstructionCompletionDay) continue;
                    facility.NextProductionDay = checked(nextDay + facility.Blueprint.IntervalDays);
                    facility.ConstructionStatus = FacilityConstructionStatus.Operational;
                    facility.LastOutcome = facility.Enabled ? ScheduledProductionOutcome.NotDue : ScheduledProductionOutcome.Disabled;
                    facility.ProcurementWaitReason = facility.Enabled && facility.Policy.ProcurementEnabled
                        ? IndustryWaitReason.None : IndustryWaitReason.Disabled;
                    facility.SalesWaitReason = facility.Enabled && facility.Policy.SalesEnabled
                        ? IndustryWaitReason.None : IndustryWaitReason.Disabled;
                    Accounting.CompleteConstruction(facility.OwnerId, facility.Id, facility.PortId, facility.StoreId);
                    Construction.Add(new RegionalConstruction(facility.Id, facility.OwnerId, facility.PortId,
                        facility.Blueprint.Id, ConstructionOutcome.Completed, 0, facility.CreatedDay,
                        nextDay, facility.NextProductionDay));
                    Changed = true;
                }
            }

            internal void Transfer(string ownerId, string shipId, string facilityId, TransferDirection direction,
                string goodId, int quantity)
            {
                before.ValidateWholeCargo(quantity);
                if (direction != TransferDirection.ShipToWarehouse && direction != TransferDirection.WarehouseToShip)
                    throw new ArgumentOutOfRangeException(nameof(direction));
                content.GetGood(goodId);
                var facility = OwnedFacility(ownerId, facilityId);
                var ship = Ships[shipId];
                if (ship.Configuration.OwnerId != ownerId) throw new InvalidOperationException("The ship belongs to another company.");
                if (ship.Dock != facility.PortId) throw new InvalidOperationException("Transfer requires docking at the warehouse's port.");
                var toWarehouse = direction == TransferDirection.ShipToWarehouse;
                var source = toWarehouse ? ship.Cargo : facility.Inventory;
                var destination = toWarehouse ? facility.Inventory : ship.Cargo;
                var free = toWarehouse ? facility.Capacity - facility.StoredUnits
                    : ship.Configuration.Ship.CargoCapacity - ship.CargoUnits;
                var outcome = RegionalWorldSnapshot.TransferOutcomeFor(source.GetQuantity(goodId), free, quantity);
                if (outcome == TransferOutcome.Transferred)
                {
                    var nextSource = checked(source.GetQuantity(goodId) - quantity);
                    var nextDestination = checked(destination.GetQuantity(goodId) + quantity);
                    source.Commit(new Dictionary<string, int> { [goodId] = nextSource });
                    destination.Commit(new Dictionary<string, int> { [goodId] = nextDestination });
                    facility.StoredUnits = checked(facility.StoredUnits + (toWarehouse ? quantity : -quantity));
                    ship.CargoUnits = checked(ship.CargoUnits + (toWarehouse ? -quantity : quantity));
                    Accounting.Transfer(toWarehouse ? "ship:" + shipId : facility.StoreId,
                        toWarehouse ? facility.StoreId : "ship:" + shipId, facility.PortId, goodId, quantity);
                    Changed = true;
                }
                Transfers.Add(new RegionalTransfer(ownerId, shipId, facilityId, direction, goodId, quantity, outcome));
            }

            internal TradeAttempt WarehouseTrade(WorkingFacility facility, TradeCommand command)
            {
                if (command == null) throw new ArgumentNullException(nameof(command));
                var port = Ports[facility.PortId];
                var company = Companies[facility.OwnerId];
                var attempt = TradeCalculator.Preview(port.Quotes[command.GoodId], command,
                    port.Inventory.GetQuantity(command.GoodId), facility.Inventory.GetQuantity(command.GoodId),
                    port.Cash, company.Cash, facility.Capacity - facility.StoredUnits);
                TradeCalculator.Apply(attempt, port.Inventory, facility.Inventory);
                facility.StoredUnits = checked(facility.StoredUnits + attempt.TraderInventoryChange.Delta);
                port.Cash = attempt.MarketCashChange.After;
                company.Cash = attempt.TraderCashChange.After;
                WarehouseTrades.Add(new RegionalWarehouseTrade(facility.Id, facility.OwnerId, facility.PortId, attempt));
                if (attempt.Outcome == TradeOutcome.Traded)
                {
                    AccountTrade(facility.StoreId, facility.PortId, attempt);
                    if (company.Id == Challenge?.Configuration.OwnerId) Commission?.Trade(facility.PortId, attempt);
                    if (command.Direction == TradeDirection.Buy)
                        facility.ProcurementCost = checked(facility.ProcurementCost + attempt.ExecutedAmount);
                    else facility.SalesRevenue = checked(facility.SalesRevenue + attempt.ExecutedAmount);
                    Changed = true;
                }
                if (Challenge != null) Challenge = ChallengeRules.ApplyTrade(Challenge, facility.OwnerId, facility.PortId, attempt);
                return attempt;
            }

            private IEnumerable<WorkingFacility> OrderedFacilities() =>
                PrivateFacilities.Values.OrderBy(f => f.PortId, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal);

            internal void Procure()
            {
                foreach (var facility in OrderedFacilities())
                {
                    if (facility.ConstructionStatus == FacilityConstructionStatus.UnderConstruction)
                    { facility.ProcurementWaitReason = IndustryWaitReason.UnderConstruction; continue; }
                    facility.ProcurementWaitReason = IndustryWaitReason.Disabled;
                    if (!facility.Enabled || !facility.Policy.ProcurementEnabled) continue;
                    facility.ProcurementWaitReason = IndustryWaitReason.TargetSatisfied;
                    foreach (var rule in facility.Policy.Inputs)
                    {
                        var wanted = Math.Max(0, rule.TargetQuantity - facility.Inventory.GetQuantity(rule.GoodId));
                        if (wanted == 0) continue;
                        var port = Ports[facility.PortId];
                        var company = Companies[facility.OwnerId];
                        var quote = port.Quotes[rule.GoodId];
                        var marketStock = port.Inventory.GetQuantity(rule.GoodId);
                        var free = facility.Capacity - facility.StoredUnits;
                        var quantity = TradeQuantityCalculator.Maximum(quote, TradeDirection.Buy, marketStock,
                            facility.Inventory.GetQuantity(rule.GoodId), port.Cash, company.Cash, free, wanted,
                            rule.MaxBuyPrice, facility.Policy.CashReserve);
                        if (quantity > 0)
                        {
                            WarehouseTrade(facility, new TradeCommand(TradeDirection.Buy, rule.GoodId, quantity));
                            if (facility.ProcurementWaitReason == IndustryWaitReason.TargetSatisfied)
                                facility.ProcurementWaitReason = IndustryWaitReason.None;
                        }
                        else if (facility.ProcurementWaitReason == IndustryWaitReason.TargetSatisfied ||
                            facility.ProcurementWaitReason == IndustryWaitReason.None)
                        {
                            facility.ProcurementWaitReason = marketStock == 0 ? IndustryWaitReason.InsufficientMarketInventory :
                                free == 0 ? IndustryWaitReason.WarehouseFull :
                                !TradeQuantityCalculator.MeetsPrice(quote, TradeDirection.Buy, marketStock, 1, rule.MaxBuyPrice)
                                    ? IndustryWaitReason.BuyPriceLimit :
                                !quote.FitsBudget(TradeDirection.Buy, marketStock, 1, long.MaxValue - port.Cash)
                                    ? IndustryWaitReason.CashAccountFull :
                                quote.FitsBudget(TradeDirection.Buy, marketStock, 1, company.Cash)
                                    ? IndustryWaitReason.CashReserve : IndustryWaitReason.InsufficientCompanyFunds;
                        }
                    }
                }
            }

            internal void SellPrivate()
            {
                foreach (var facility in OrderedFacilities())
                {
                    if (facility.ConstructionStatus == FacilityConstructionStatus.UnderConstruction)
                    { facility.SalesWaitReason = IndustryWaitReason.UnderConstruction; continue; }
                    facility.SalesWaitReason = IndustryWaitReason.Disabled;
                    if (!facility.Enabled || !facility.Policy.SalesEnabled) continue;
                    facility.SalesWaitReason = IndustryWaitReason.ReserveRetained;
                    foreach (var rule in facility.Policy.Outputs)
                    {
                        var wanted = Math.Max(0, facility.Inventory.GetQuantity(rule.GoodId) - rule.ReserveQuantity);
                        if (wanted == 0) continue;
                        var port = Ports[facility.PortId];
                        var company = Companies[facility.OwnerId];
                        var quote = port.Quotes[rule.GoodId];
                        var marketStock = port.Inventory.GetQuantity(rule.GoodId);
                        var quantity = TradeQuantityCalculator.Maximum(quote, TradeDirection.Sell, marketStock,
                            facility.Inventory.GetQuantity(rule.GoodId), port.Cash, company.Cash, 0, wanted, rule.MinSellPrice);
                        if (quantity > 0)
                        {
                            WarehouseTrade(facility, new TradeCommand(TradeDirection.Sell, rule.GoodId, quantity));
                            if (facility.SalesWaitReason == IndustryWaitReason.ReserveRetained)
                                facility.SalesWaitReason = IndustryWaitReason.None;
                        }
                        else if (facility.SalesWaitReason == IndustryWaitReason.ReserveRetained ||
                            facility.SalesWaitReason == IndustryWaitReason.None)
                        {
                            facility.SalesWaitReason = marketStock == int.MaxValue ? IndustryWaitReason.MarketInventoryFull :
                                !TradeQuantityCalculator.MeetsPrice(quote, TradeDirection.Sell, marketStock, 1, rule.MinSellPrice)
                                    ? IndustryWaitReason.SellPriceLimit :
                                !quote.FitsBudget(TradeDirection.Sell, marketStock, 1, long.MaxValue - company.Cash)
                                    ? IndustryWaitReason.CashAccountFull : IndustryWaitReason.InsufficientMarketFunds;
                        }
                    }
                }
            }

            internal void ProducePrivate(long nextDay)
            {
                foreach (var facility in OrderedFacilities())
                {
                    if (facility.ConstructionStatus == FacilityConstructionStatus.UnderConstruction) continue;
                    if (nextDay < facility.NextProductionDay) continue;
                    var recipe = content.GetRecipe(facility.Blueprint.RecipeId);
                    var scale = facility.Inventory.QuantityScale;
                    var starting = facility.Inventory.Snapshot();
                    var outcome = PrivateProductionRules.Preview(recipe, facility.Blueprint,
                        facility.Inventory.GetQuantity, scale, facility.StoredUnits, Companies[facility.OwnerId].Cash,
                        facility.Enabled, facility.ConstructionStatus, facility.NextProductionDay, nextDay,
                        facility.Capacity, before.UsesPooledCompanyStorage,
                        before.UsesPooledCompanyStorage ? RegionalWorldSnapshot.AggregateOutputLimit(PrivateFacilities.Values
                            .Where(f => f.OwnerId == facility.OwnerId && f.PortId == facility.PortId &&
                                f.Blueprint.RecipeId == facility.Blueprint.RecipeId && f.ConstructionStatus == FacilityConstructionStatus.Operational)
                            .Select(f => f.Blueprint.OutputStockLimit)) : null);
                    if (outcome == ScheduledProductionOutcome.Produced)
                    {
                        outcome = Simulation.Production.TryRunBatch(facility.Inventory,
                            facility.Blueprint.BuildingId, facility.Blueprint.RecipeId)
                            ? ScheduledProductionOutcome.Produced : ScheduledProductionOutcome.InputShortage;
                        if (outcome == ScheduledProductionOutcome.Produced)
                        {
                            PayWage(Ports[facility.PortId], facility.Blueprint.WagePerBatch, facility.Id, facility.OwnerId);
                            facility.WagesPaid = checked(facility.WagesPaid + facility.Blueprint.WagePerBatch);
                            Accounting.Produce(facility.StoreId, facility.PortId, recipe, scale, facility.Id);
                            facility.BatchesProduced = checked(facility.BatchesProduced + 1);
                            foreach (var input in recipe.Inputs)
                                facility.Consumed[input.GoodId] = checked(facility.Consumed[input.GoodId] + checked(input.Quantity * scale));
                            foreach (var output in recipe.Outputs)
                                facility.Produced[output.GoodId] = checked(facility.Produced[output.GoodId] + checked(output.Quantity * scale));
                            facility.StoredUnits = checked((int)facility.Inventory.Snapshot().Values.Sum(v => (long)v));
                            facility.NextProductionDay = checked(nextDay + facility.Blueprint.IntervalDays);
                        }
                    }
                    facility.LastOutcome = outcome;
                    var changes = starting.Keys.Where(id => starting[id] != facility.Inventory.GetQuantity(id))
                        .Select(id => new InventoryChange(id, starting[id], facility.Inventory.GetQuantity(id)));
                    PrivateProduction.Add(new RegionalPrivateProduction(facility.Id, facility.OwnerId, facility.PortId,
                        facility.Blueprint.RecipeId, outcome, changes));
                }
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
                if (decision.IndustrialDetail != null)
                {
                    ApplyIndustrialDecision(decision.IndustrialDetail);
                    return;
                }
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

            private void ApplyIndustrialDecision(IndustrialRouteDecision decision)
            {
                var ship = Ships[decision.ShipId];
                if (decision.Stage != ship.IndustrialStage)
                    throw new InvalidOperationException("The industrial route stage has changed.");
                if (decision.Trade != null)
                {
                    var fresh = View().PreviewTrade(decision.ShipId, decision.Trade.PortId, decision.Trade.Trade);
                    if (fresh.Outcome != TradeOutcome.Traded || decision.Preview == null ||
                        fresh.ExecutedAmount != decision.Preview.ExecutedAmount ||
                        fresh.ExecutedQuantity != decision.Preview.ExecutedQuantity)
                        throw new InvalidOperationException("Industrial trade no longer matches its preview.");
                    Trade(decision.ShipId, decision.Trade.PortId, decision.Trade.Trade);
                }
                if (decision.Transfer != null)
                {
                    var command = decision.Transfer;
                    Transfer(ship.Configuration.OwnerId, decision.ShipId, command.FacilityId,
                        command.Direction, command.GoodId, command.Quantity);
                    if (Transfers[Transfers.Count - 1].Outcome != TransferOutcome.Transferred)
                        throw new InvalidOperationException("A validated industrial transfer must execute.");
                }
                if (decision.Departure != null) Depart(decision.ShipId, decision.Departure.DestinationPortId);
                if (ship.IndustrialStage != decision.NextStage)
                {
                    ship.IndustrialStage = decision.NextStage;
                    Changed = true;
                }
            }

            internal void RunAutomation(string shipId, int limit)
            {
                for (var action = 0; action < limit; action++)
                {
                    var decision = RegionalAutomation.Evaluate(View(), shipId);
                    var tradeCount = Trades.Count;
                    var movementCount = Movements.Count;
                    var transferCount = Transfers.Count;
                    var stage = Ships[shipId].IndustrialStage;
                    ApplyDecision(decision);
                    if (Ships[shipId].Dock == null || (tradeCount == Trades.Count && movementCount == Movements.Count &&
                        transferCount == Transfers.Count && stage == Ships[shipId].IndustrialStage)) break;
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
                        var scale = port.Inventory.QuantityScale;
                        var starting = port.Inventory.Snapshot();
                        var outcome = ScheduledProductionOutcome.Disabled;
                        if (facility.Enabled)
                        {
                            if (recipe.Inputs.Any(i => port.Inventory.GetQuantity(i.GoodId) < checked(i.Quantity * scale)))
                                outcome = ScheduledProductionOutcome.InputShortage;
                            else if (config.OutputStockLimit.HasValue && recipe.Outputs.Any(output =>
                                (long)port.Inventory.GetQuantity(output.GoodId) -
                                recipe.Inputs.Where(i => i.GoodId == output.GoodId).Sum(i => (long)checked(i.Quantity * scale)) +
                                checked(output.Quantity * scale) > config.OutputStockLimit.Value))
                                outcome = ScheduledProductionOutcome.OutputLimit;
                            else if (port.Cash < config.WagePerBatch)
                                outcome = ScheduledProductionOutcome.PayrollShortage;
                            else
                                outcome = Simulation.Production.TryRunBatch(port.Inventory, instance.BuildingId, instance.RecipeId)
                                    ? ScheduledProductionOutcome.Produced : ScheduledProductionOutcome.InputShortage;
                        }
                        if (outcome == ScheduledProductionOutcome.Produced)
                            PayWage(port, config.WagePerBatch, instance.Id, null);
                        var due = outcome == ScheduledProductionOutcome.Produced
                            ? checked(nextDay + config.IntervalDays) : facility.NextDueDay;
                        port.Facilities[index] = new ScheduledFacilitySnapshot(config, facility.Enabled, due, outcome);
                        var changes = starting.Keys.Where(id => starting[id] != port.Inventory.GetQuantity(id))
                            .Select(id => new InventoryChange(id, starting[id], port.Inventory.GetQuantity(id)));
                        Production.Add(new RegionalProduction(entry.Key, instance.Id, instance.RecipeId, outcome, changes));
                    }
                }
            }

            internal void BeginCityDay()
            {
                foreach (var port in Ports.Values) port.City?.BeginDay();
            }

            private void PayWage(WorkingPort port, long amount, string facilityId, string? ownerId)
            {
                if (amount == 0) return;
                var city = port.City ?? throw new InvalidOperationException("A wage requires local households.");
                if (ownerId == null) port.Cash = checked(port.Cash - amount);
                else
                {
                    Companies[ownerId].Cash = checked(Companies[ownerId].Cash - amount);
                    Accounting.PayWage(ownerId, facilityId, port.Configuration.Id, amount, PrivateFacilities[facilityId].StoreId);
                }
                city.ReceiveWage(amount);
                CashTransfers.Add(new CityCashTransfer(CityCashTransferKind.Wage,
                    ownerId == null ? "market:" + port.Configuration.Id : "company:" + ownerId,
                    "households:" + port.Configuration.Id, port.Configuration.Id, amount, facilityId));
            }

            private void ConsumeLiving(WorkingPort port, long nextDay)
            {
                var city = port.City!;
                var indices = Enumerable.Range(0, port.Consumption.Length).OrderBy(i =>
                    port.Consumption[i].Configuration.Kind == ConsumptionKind.Essential ? 0 :
                    port.Consumption[i].Configuration.Kind == ConsumptionKind.Luxury ? 1 : 2)
                    .ThenBy(i => port.Consumption[i].Configuration.Id, StringComparer.Ordinal);
                foreach (var index in indices)
                {
                    var previous = port.Consumption[index];
                    if (nextDay < previous.NextDueDay) continue;
                    var rule = previous.Configuration;
                    var eligible = rule.Kind != ConsumptionKind.Luxury || city.Luxury;
                    var demand = eligible ? rule.PreviewDemand(city.Population, previous.DemandRemainder) :
                        new ConsumptionDemand(0, previous.DemandRemainder);
                    var baseline = eligible ? rule.PreviewDemand(city.Configuration.InitialPopulation,
                        previous.BaselineDemandRemainder) : new ConsumptionDemand(0, previous.BaselineDemandRemainder);
                    var foodRemainder = port.Consumption.Single(c => c.Configuration.Kind == ConsumptionKind.Essential).DemandRemainder;
                    var result = LivingRetail.Preview(port.Configuration, city.Snapshot(), rule,
                        port.Quotes, port.Inventory.Snapshot(), port.Cash, previous.DemandRemainder, foodRemainder);
                    port.Inventory.Commit(new Dictionary<string, int> {
                        [rule.GoodId] = checked(port.Inventory.GetQuantity(rule.GoodId) - result.ConsumedQuantity) });
                    if (result.Payment > 0)
                    {
                        city.PayRetail(result.Payment);
                        port.Cash = checked(port.Cash + result.Payment);
                        CashTransfers.Add(new CityCashTransfer(CityCashTransferKind.Retail,
                            "households:" + port.Configuration.Id, "market:" + port.Configuration.Id,
                            port.Configuration.Id, result.Payment, good: rule.GoodId));
                    }
                    port.Consumption[index] = new ConsumptionSnapshot(rule, checked(nextDay + rule.IntervalDays),
                        result.UnmetQuantity, checked(previous.TotalUnmetQuantity + result.UnmetQuantity),
                        result.RequestedQuantity, result.ConsumedQuantity, result.Payment, result.UnmetStock,
                        result.UnmetAffordability, checked(previous.TotalDesiredQuantity + result.RequestedQuantity),
                        checked(previous.TotalConsumedQuantity + result.ConsumedQuantity),
                        checked(previous.TotalPayment + result.Payment), checked(previous.TotalUnmetStock + result.UnmetStock),
                        checked(previous.TotalUnmetAffordability + result.UnmetAffordability),
                        demand.Remainder, baseline.Remainder, baseline.Quantity);
                    Consumption.Add(result);
                    if (Challenge != null) Challenge = ChallengeRules.ApplyConsumption(Challenge, rule.GoodId, result.ConsumedQuantity);
                }
            }

            internal void FinishCityDay(long nextDay)
            {
                foreach (var port in Ports.Values)
                {
                    var city = port.City;
                    if (city == null) continue;
                    var previous = city.Snapshot();
                    var rule = port.Configuration.Consumption.Single(c => c.Kind == ConsumptionKind.Essential);
                    var food = Consumption.SingleOrDefault(c => c.PortId == port.Configuration.Id && c.RuleId == rule.Id);
                    var foodState = port.Consumption.Single(c => c.Configuration.Id == rule.Id);
                    if (food != null)
                        city.Observe(new NeedObservation(nextDay, food.RequestedQuantity, food.ConsumedQuantity,
                            foodState.LastBaselineDesiredQuantity,
                            food.UnmetStock, food.UnmetAffordability, food.Payment),
                            LivingRetail.FoodBudget(port.Configuration, city.Population, port.Quotes,
                                port.Inventory.Snapshot(), foodState.DemandRemainder));
                    city.ReviewPopulation(nextDay);
                    if (food != null || city.Population != previous.Population)
                        CityChanges.Add(new CityChange(port.Configuration.Id, nextDay, previous, city.Snapshot()));
                    port.Quotes = LivingCityPricing.Derive(port.Configuration, city.Snapshot());
                }
            }

            internal void Consume(long nextDay)
            {
                foreach (var entry in Ports)
                {
                    var port = entry.Value;
                    if (port.City != null) { ConsumeLiving(port, nextDay); continue; }
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
                        if (Challenge != null) Challenge = ChallengeRules.ApplyConsumption(Challenge, config.GoodId, consumed);
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
                    if (ship.Configuration.OwnerId == Challenge?.Configuration.OwnerId)
                        Commission?.Visit(entry.Key, ship.Dock, checked(Day + 1));
                    ship.Origin = null;
                    ship.Destination = null;
                }
            }
        }
    }
}
