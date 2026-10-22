using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldState
    {
        internal void RestoreSave(RegionalSaveData data)
        {
            if (mutating) throw new InvalidOperationException("Cannot load during an operation.");
            var restored = RegionalSaveRestore.Restore(content, state, data);
            if (restored.TotalCash != initialCombinedCash + (restored.Economy?.MintedCurrency ?? 0))
                throw new WorldSaveException("Saved all-account cash does not reconcile with the initial world.");
            foreach (var vessel in restored.Ships.Values)
            {
                if (vessel.Policy != null) RegionalAutomation.ValidatePolicy(restored, vessel.Id, vessel.Policy);
                if (vessel.IndustrialPolicy != null) IndustrialRouteEvaluator.ValidatePolicy(restored, vessel.Id, vessel.IndustrialPolicy);
            }
            if (restored.Economy != null)
            {
                foreach (var owner in restored.Companies.Keys) _ = restored.CompanyValue(owner).Total;
                if (restored.Challenge != null && ExpandingChallenges.IsExpanding(restored.Challenge.Configuration.Kind))
                {
                    // Pure validation of the saved observations/assets; never execute ChallengeRules.Evaluate
                    // or alter the saved lifecycle, counters, day, or revision during restoration.
                    var actual = ExpandingChallenges.Progress(restored);
                    if (restored.Challenge.ProducedQuantity != actual.Percent ||
                        (restored.Challenge.Status == ChallengeStatus.Succeeded && !actual.Qualified))
                        throw new WorldSaveException("Saved v3 objective progress disagrees with authoritative welfare, industry and monetary state.");
                    // Every economic commit immediately evaluates active objectives. Qualification
                    // includes the cash floor, and success takes precedence even on the deadline.
                    // Therefore neither an active run nor a failed run can already be qualified.
                    // Abandonment is an explicit lifecycle choice; retain it without evaluating it.
                    if (actual.Qualified && (restored.Challenge.Status == ChallengeStatus.Active ||
                        restored.Challenge.Status == ChallengeStatus.Failed))
                        throw new WorldSaveException("Saved v3 objective lifecycle conflicts with its already qualified state.");
                }
            }
            // This fresh world is returned only after every check passes. No daily operation or challenge
            // evaluation is invoked by restore, including when the saved challenge is already terminal.
            state = restored;
        }
    }

    internal static partial class RegionalSaveRestore
    {
        internal static void ValidateCounts(RegionalSaveData data)
        {
            Count(data.Ports, 4, 4); Count(data.Ships, 1, 64); Count(data.Companies, 1, 64);
            Count(data.Facilities, 0, WorldSaveCodec.MaximumFacilities);
            Count(data.Stores, 1, WorldSaveCodec.MaximumFacilities + 64);
            var total = 0;
            foreach (var store in data.Stores)
            {
                Count(store.Goods, 0, 64);
                foreach (var good in store.Goods)
                {
                    Count(good.Lots, 1, WorldSaveCodec.MaximumOpenLots);
                    total = checked(total + good.Lots.Length);
                    Require(total <= WorldSaveCodec.MaximumOpenLots, "Too many open FIFO lots.");
                }
            }
        }

        internal static RegionalWorldSnapshot Restore(ContentCatalog content, RegionalWorldSnapshot initial, RegionalSaveData data)
        {
            Require(data.Day >= 0 && data.Revision >= data.Day, "Invalid day or revision.");
            Require(data.NextLotId >= 1, "Invalid FIFO sequence.");
            SameIds(data.Ports.Select(p => p.Id), initial.Ports.Keys, "ports");
            SameIds(data.Companies.Select(c => c.Id), initial.Companies.Keys, "companies");
            var economy = RestoreEconomy(initial, data);
            var shipConfigurations = RestoreShipConfigurations(initial, data, economy);
            var companies = data.Companies.Select(c => new CompanySnapshot(c.Id, Nonnegative(c.Cash))).ToArray();
            var ports = data.Ports.Select(p => RestorePort(content, initial.Ports[p.Id], p, data.Day)).ToArray();
            var facilities = data.Facilities.Select(f => RestoreFacility(content, initial, f, data.Day)).ToArray();
            var facilityIds = ports.SelectMany(p => p.Facilities).Select(f => f.Configuration.Instance.Id)
                .Concat(facilities.Select(f => f.Id)).ToArray();
            Unique(facilityIds, "facilities");
            foreach (var starter in initial.PrivateFacilities.Values)
            {
                var saved = facilities.SingleOrDefault(f => f.Id == starter.Id);
                Require(saved != null && saved.OwnerId == starter.OwnerId && saved.PortId == starter.PortId &&
                    saved.Blueprint.Id == starter.Blueprint.Id && saved.IsStarterGrant && saved.Provenance == starter.Provenance,
                    "Starter facility identity changed.");
            }
            var ships = data.Ships.Select(s => RestoreShip(content, initial, shipConfigurations[s.Id], s,
                companies.Single(c => c.Id == shipConfigurations[s.Id].OwnerId).Cash)).ToArray();
            var stores = RestoreStores(content, data, ships, facilities);
            var financial = data.Companies.Select(c => RestoreCompany(content, initial, c, stores)).ToArray();
            var accounting = new AccountingSnapshot(financial, stores, data.NextLotId);
            var challenge = RestoreChallenge(initial, data, ports);
            return new RegionalWorldSnapshot(data.Day, data.Revision, ports, ships, initial.Routes, companies,
                initial.IndustryBlueprints.Values, facilities, challenge, accounting, initial.Difficulty, initial.RunIdentity, economy);
        }

        private static RegionalPortSnapshot RestorePort(ContentCatalog content, RegionalPortSnapshot initial, RegionalSavePort data, long day)
        {
            var inventory = Inventory(content, data.Inventory);
            Count(data.Quotes, 1, content.Goods.Count);
            SameIds(data.Quotes.Select(q => q.GoodId), initial.Quotes.Keys, "quotes");
            var quotes = data.Quotes.ToDictionary(q => q.GoodId, q =>
            {
                var original = initial.Quotes[q.GoodId];
                Require((q.Pricing == null) == original.IsFixed, "Quote model changed.");
                if (q.Pricing == null)
                {
                    Require(q.Bid == original.Bid && q.Ask == original.Ask, "Fixed quote changed.");
                    return original;
                }
                var p = q.Pricing;
                var b = original.Pricing!;
                Require(q.Bid == 0 && q.Ask == 0 && p.Model == b.Model && p.ReferencePrice == b.ReferencePrice &&
                    p.Sensitivity == b.Sensitivity && p.MinPrice == b.MinPrice && p.MaxPrice == b.MaxPrice && p.Spread == b.Spread,
                    "Dynamic quote parameters changed.");
                if (initial.City?.Configuration.PricingMode != LivingPricingMode.Coverage)
                    Require(p.TargetStock == b.TargetStock, "Stock pricing target changed.");
                return new MarketQuote(q.GoodId, p);
            }, StringComparer.Ordinal);
            Count(data.Facilities, 0, 64); Count(data.Consumption, 0, 64);
            SameIds(data.Facilities.Select(f => f.Id), initial.Facilities.Select(f => f.Configuration.Instance.Id), "public facilities");
            SameIds(data.Consumption.Select(c => c.Id), initial.Consumption.Select(c => c.Configuration.Id), "needs");
            var schedules = data.Facilities.Select(f =>
            {
                var original = initial.Facilities.Single(p => p.Configuration.Instance.Id == f.Id).Configuration;
                Require(f.NextDueDay >= original.FirstDueDay && f.NextDueDay <= checked(day + original.IntervalDays), "Invalid production deadline.");
                Defined(f.LastOutcome);
                return new ScheduledFacilitySnapshot(original, f.Enabled, f.NextDueDay, f.LastOutcome);
            }).ToArray();
            var consumption = data.Consumption.Select(c => RestoreConsumption(
                initial.Consumption.Single(n => n.Configuration.Id == c.Id).Configuration, c, day)).ToArray();
            var city = RestoreCity(initial.Configuration.Settlement, data.City, day);
            var buildings = schedules.Select(s => new BuildingInstance(s.Configuration.Instance.Id,
                s.Configuration.Instance.BuildingId, s.Configuration.Instance.RecipeId, s.Configuration.Instance.Priority, s.Enabled));
            return new RegionalPortSnapshot(initial.Configuration, new PortSnapshot(data.Id, inventory,
                Nonnegative(data.Cash), quotes, buildings), schedules, consumption, city);
        }

        private static ConsumptionSnapshot RestoreConsumption(ConsumptionConfiguration config, RegionalSaveConsumption c, long day)
        {
            Require(c.NextDueDay >= config.FirstDueDay && c.NextDueDay <= checked(day + config.IntervalDays), "Invalid consumption deadline.");
            Nonnegative(c.LastUnmet, c.TotalUnmet, c.LastDesired, c.LastConsumed, c.LastPayment, c.LastStock,
                c.LastAffordability, c.TotalDesired, c.TotalConsumed, c.TotalPayment, c.TotalStock, c.TotalAffordability, c.LastBaselineDesired);
            Require(c.DemandRemainder >= 0 && c.DemandRemainder < config.PopulationPerUnit &&
                c.BaselineDemandRemainder >= 0 && c.BaselineDemandRemainder < config.PopulationPerUnit, "Invalid demand remainder.");
            Require(config.Proportional || (c.DemandRemainder == 0 && c.BaselineDemandRemainder == 0), "Historical demand cannot retain fractional remainders.");
            if (config.Kind != ConsumptionKind.Legacy)
            {
                Require((long)c.LastDesired == (long)c.LastConsumed + c.LastStock + c.LastAffordability &&
                    (long)c.LastUnmet == (long)c.LastStock + c.LastAffordability, "Last need quantities do not reconcile.");
                Require((BigInteger)c.TotalDesired == (BigInteger)c.TotalConsumed + c.TotalStock + c.TotalAffordability &&
                    (BigInteger)c.TotalUnmet == (BigInteger)c.TotalStock + c.TotalAffordability, "Cumulative need quantities do not reconcile.");
                Require(c.LastDesired <= c.TotalDesired && c.LastConsumed <= c.TotalConsumed && c.LastPayment <= c.TotalPayment &&
                    c.LastStock <= c.TotalStock && c.LastAffordability <= c.TotalAffordability, "Need totals precede their last observation.");
            }
            Require(c.LastUnmet <= c.TotalUnmet, "Need shortfall total is invalid.");
            return new ConsumptionSnapshot(config, c.NextDueDay, c.LastUnmet, c.TotalUnmet, c.LastDesired,
                c.LastConsumed, c.LastPayment, c.LastStock, c.LastAffordability, c.TotalDesired, c.TotalConsumed,
                c.TotalPayment, c.TotalStock, c.TotalAffordability, c.DemandRemainder, c.BaselineDemandRemainder, c.LastBaselineDesired);
        }

        private static CitySnapshot? RestoreCity(SettlementConfiguration? config, RegionalSaveCity? c, long day)
        {
            Require((config == null) == (c == null), "Household sector presence changed.");
            if (c == null || config == null) return null;
            Count(c.Food, 0, CitySnapshot.FoodWindowCapacity);
            Require(c.Population >= config.MinimumPopulation && c.Population <= config.MaximumPopulation &&
                c.PopulationDelta >= -1 && c.PopulationDelta <= 1, "Invalid population.");
            Require(c.Prosperity >= 0 && c.Prosperity <= 100 && c.PurchasingPower >= 0 && c.PurchasingPower <= 100,
                "Invalid welfare indicator.");
            Nonnegative(c.Cash, c.Wages, c.LastWages, c.Retail, c.LastRetail, c.LastDemographicDay,
                c.Healthy, c.Deprived, c.LuxuryReady, c.LuxuryPoor);
            Require(c.LastWages <= c.Wages && c.LastRetail <= c.Retail && c.LastDemographicDay <= day &&
                c.Healthy <= 6 && c.Deprived <= 6 && c.LuxuryReady <= 3 && c.LuxuryPoor <= 3 &&
                !string.IsNullOrWhiteSpace(c.Reason), "Invalid household counters.");
            Require((BigInteger)c.Cash == (BigInteger)config.InitialHouseholdCash + c.Wages - c.Retail,
                "Household cash does not reconcile with wages and retail payments.");
            long previous = -1;
            var food = c.Food.Select(n =>
            {
                Nonnegative(n.Desired, n.Consumed, n.Baseline, n.Stock, n.Affordability, n.Payment);
                Require(n.Day > previous && n.Day <= day && (long)n.Desired == (long)n.Consumed + n.Stock + n.Affordability,
                    "Invalid bounded food observation.");
                previous = n.Day;
                return new NeedObservation(n.Day, n.Desired, n.Consumed, n.Baseline, n.Stock, n.Affordability, n.Payment);
            }).ToArray();
            return new CitySnapshot(config, c.Population, c.PopulationDelta, c.Cash, c.Prosperity, c.Reason,
                c.Luxury, c.PurchasingPower, c.Wages, c.LastWages, c.Retail, c.LastRetail,
                c.LastDemographicDay, c.Healthy, c.Deprived, c.LuxuryReady, c.LuxuryPoor, food);
        }

        private static RegionalShipSnapshot RestoreShip(ContentCatalog content, RegionalWorldSnapshot initial,
            RegionalShipConfiguration config, RegionalSaveShip s, long cash)
        {
            Defined(s.IndustrialStage);
            Require(s.Policy == null || s.IndustrialPolicy == null, "Ship has conflicting automation policies.");
            Require(!s.AutomationEnabled || s.Policy != null || s.IndustrialPolicy != null, "Enabled ship has no policy.");
            if (s.Dock != null)
            {
                Require(initial.Ports.ContainsKey(s.Dock) && s.Origin == null && s.Destination == null &&
                    s.RemainingDays == 0 && (s.VoyageDays == 0 || initial.Routes.Any(r =>
                        r.DurationDays(config.Speed) == s.VoyageDays)), "Invalid docked ship state.");
            }
            else
            {
                Require(s.Origin != null && s.Destination != null, "Voyage endpoints are missing.");
                var duration = initial.GetRoute(s.Origin!, s.Destination!).DurationDays(config.Speed);
                Require(s.VoyageDays == duration && s.RemainingDays > 0 && s.RemainingDays <= duration, "Impossible voyage duration.");
            }
            var cargo = Inventory(content, s.Cargo);
            Require(cargo.Values.All(q => q % initial.QuantityScale == 0), "Ship cargo must contain whole cargo units.");
            return new RegionalShipSnapshot(config, new ShipSnapshot(s.Id, config.Ship.CargoCapacity, cargo,
                s.Dock, s.Origin, s.Destination, s.RemainingDays), cash, s.Policy, s.AutomationEnabled,
                s.VoyageDays, s.IndustrialPolicy, s.IndustrialStage);
        }

        private static PrivateFacilitySnapshot RestoreFacility(ContentCatalog content, RegionalWorldSnapshot initial, RegionalSaveFacility f, long day)
        {
            RuntimeIdentifier.Validate(f.Id, nameof(f.Id));
            Require(initial.Companies.ContainsKey(f.OwnerId), "Unknown facility owner.");
            var blueprint = initial.IndustryBlueprints[f.BlueprintId];
            Require(blueprint.EligiblePortIds.Contains(f.PortId), "Facility is at an ineligible port.");
            if (f.Policy == null) throw new WorldSaveException("Industry policy is missing.");
            var recipe = content.GetRecipe(blueprint.RecipeId);
            Require(f.Policy.Inputs.All(r => recipe.Inputs.Any(i => i.GoodId == r.GoodId) && r.TargetQuantity <= blueprint.WarehouseCapacity) &&
                f.Policy.Outputs.All(r => recipe.Outputs.Any(i => i.GoodId == r.GoodId) && r.ReserveQuantity <= blueprint.WarehouseCapacity),
                "Industry policy is incompatible with its recipe or storage.");
            Defined(f.ConstructionStatus); Defined(f.LastOutcome); Defined(f.ProcurementWait); Defined(f.SalesWait);
            Nonnegative(f.CreatedDay, f.CompletionDay, f.NextProductionDay, f.Batches, f.Procurement, f.Revenue, f.Wages);
            Require(f.CreatedDay <= day && f.CompletionDay >= f.CreatedDay &&
                f.NextProductionDay >= checked(f.CompletionDay + blueprint.IntervalDays) &&
                f.NextProductionDay <= checked(Math.Max(day, f.CompletionDay) + blueprint.IntervalDays), "Invalid facility schedule.");
            var starter = initial.PrivateFacilities.TryGetValue(f.Id, out var original);
            Require(f.StarterGrant == starter && (!starter || f.CreatedDay == 0), "Invalid starter endowment claim.");
            var delay = starter ? 0 : initial.Difficulty == RunDifficulty.SlowFoundations &&
                initial.Ships.Values.Any(s => s.IsPlayer && s.OwnerId == f.OwnerId) ? 2 : 0;
            Require(f.CompletionDay == checked(f.CreatedDay + delay) &&
                (f.ConstructionStatus == FacilityConstructionStatus.Operational) == (f.CompletionDay <= day), "Invalid construction lifecycle.");
            var inventory = Inventory(content, f.Inventory);
            Require(inventory.Values.Sum(v => (long)v) <= blueprint.WarehouseCapacity, "Warehouse exceeds capacity.");
            Totals(content, f.Produced); Totals(content, f.Consumed);
            foreach (var good in content.Goods)
            {
                var produced = recipe.Outputs.Where(i => i.GoodId == good.Id).Sum(i => (long)i.Quantity);
                var consumed = recipe.Inputs.Where(i => i.GoodId == good.Id).Sum(i => (long)i.Quantity);
                Require((BigInteger)Get(f.Produced, good.Id) == (BigInteger)f.Batches * produced * initial.QuantityScale &&
                    (BigInteger)Get(f.Consumed, good.Id) == (BigInteger)f.Batches * consumed * initial.QuantityScale, "Facility physical batch totals are inconsistent.");
            }
            Require((BigInteger)f.Wages == (BigInteger)f.Batches * blueprint.WagePerBatch, "Facility wages do not match paid batches.");
            Require(!string.IsNullOrWhiteSpace(f.Provenance), "Facility provenance is missing.");
            return new PrivateFacilitySnapshot(f.Id, f.OwnerId, f.PortId, blueprint, f.Policy, f.Enabled,
                f.CreatedDay, f.NextProductionDay, f.LastOutcome, inventory, f.Batches, f.Procurement, f.Revenue,
                f.Produced, f.Consumed, f.ProcurementWait, f.SalesWait, f.StarterGrant, f.Provenance,
                f.ConstructionStatus, f.CompletionDay, day, f.Wages);
        }

        private static InventoryCostSnapshot[] RestoreStores(ContentCatalog content, RegionalSaveData data,
            RegionalShipSnapshot[] ships, PrivateFacilitySnapshot[] facilities)
        {
            var physical = ships.Select(s => (Id: "ship:" + s.Id, Owner: s.OwnerId, Stock: s.Cargo))
                .Concat(facilities.Select(f => (Id: "facility:" + f.Id, Owner: f.OwnerId, Stock: f.Inventory)))
                .ToDictionary(s => s.Id, StringComparer.Ordinal);
            SameIds(data.Stores.Select(s => s.Id), physical.Keys, "FIFO stores");
            var intervals = new Dictionary<long, List<(string Good, string Owner, InventoryCostLot Lot)>>();
            var stores = data.Stores.Select(s =>
            {
                var stock = physical[s.Id];
                Require(s.OwnerId == stock.Owner, "FIFO store owner mismatch.");
                Unique(s.Goods.Select(g => g.GoodId), "FIFO goods");
                var goods = s.Goods.Select(g =>
                {
                    content.GetGood(g.GoodId);
                    var lots = g.Lots.Select(l =>
                    {
                        Require(l.Id < data.NextLotId, "FIFO lot sequence is invalid.");
                        var lot = new InventoryCostLot(l.Id, l.Origin, l.Provenance, l.OriginalQuantity, l.OriginalCost, l.Start, l.Quantity);
                        Require(l.Origin != InventoryCostOrigin.StarterGrant || l.OriginalCost == 0, "Starter grants cannot invent paid basis.");
                        if (!intervals.TryGetValue(l.Id, out var list)) intervals.Add(l.Id, list = new List<(string, string, InventoryCostLot)>());
                        list.Add((g.GoodId, s.OwnerId, lot));
                        return lot;
                    }).ToArray();
                    return new GoodCostSnapshot(g.GoodId, lots);
                }).ToArray();
                var restored = new InventoryCostSnapshot(s.Id, s.OwnerId, goods);
                foreach (var good in content.Goods)
                    Require(restored.GetQuantity(good.Id) == stock.Stock[good.Id], "FIFO quantity differs from physical inventory.");
                return restored;
            }).ToArray();
            foreach (var interval in intervals.Values)
            {
                var first = interval[0];
                long end = -1;
                foreach (var slice in interval.OrderBy(i => i.Lot.Start))
                {
                    var l = slice.Lot;
                    Require(slice.Good == first.Good && slice.Owner == first.Owner && l.OriginalQuantity == first.Lot.OriginalQuantity &&
                        l.OriginalCost == first.Lot.OriginalCost && l.Provenance == first.Lot.Provenance && l.Origin == first.Lot.Origin &&
                        l.Start >= end, "FIFO slices overlap or disagree about original acquisition.");
                    end = checked(l.Start + l.Quantity);
                }
            }
            return stores;
        }

        private static CompanyFinancialSnapshot RestoreCompany(ContentCatalog content, RegionalWorldSnapshot initial,
            RegionalSaveCompany c, InventoryCostSnapshot[] stores)
        {
            Nonnegative(c.InitialCash, c.Purchases, c.Revenue, c.CostOfGoodsSold, c.Construction, c.Wages,
                c.MintReceipts, c.MintMaterialCost, c.PublicCommissionExpenditure);
            Require(c.InitialCash == initial.Companies[c.Id].Cash, "Initial company cash changed.");
            Require((BigInteger)c.Cash == (BigInteger)c.InitialCash + c.Revenue - c.Purchases - c.Construction - c.Wages +
                c.MintReceipts - c.PublicCommissionExpenditure,
                "Company cash does not reconcile with accounting.");
            foreach (var values in new[] { c.Granted, c.Purchased, c.Sold, c.Produced, c.Consumed, c.SurrenderedQuantities }) Totals(content, values);
            var owned = stores.Where(s => s.OwnerId == c.Id).ToArray();
            var held = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var good in content.Goods)
            {
                var quantity = owned.Sum(s => (long)s.GetQuantity(good.Id));
                if (quantity > 0) held.Add(good.Id, quantity);
                Require((BigInteger)Get(c.Granted, good.Id) + Get(c.Purchased, good.Id) + Get(c.Produced, good.Id) -
                    Get(c.Sold, good.Id) - Get(c.Consumed, good.Id) - Get(c.SurrenderedQuantities, good.Id) == quantity, "Company goods do not reconcile with cumulative flows.");
                Require(Get(c.Granted, good.Id) == Get(initial.Accounting.Companies[c.Id].GrantedQuantities, good.Id), "Initial grants changed.");
            }
            var result = new CompanyFinancialSnapshot(c.Id, c.InitialCash, c.Purchases, c.Revenue, c.CostOfGoodsSold,
                c.Construction, owned.Aggregate(0L, (sum, s) => checked(sum + s.TotalCostBasis)), c.Granted,
                c.Purchased, c.Sold, c.Produced, c.Consumed, held, c.Wages, c.MintReceipts, c.MintMaterialCost,
                c.PublicCommissionExpenditure, c.SurrenderedQuantities);
            _ = result.OperatingSurplusAfterWages;
            return result;
        }

        private static ChallengeSnapshot? RestoreChallenge(RegionalWorldSnapshot initial, RegionalSaveData data, RegionalPortSnapshot[] ports)
        {
            var c = data.Challenge;
            Require((c == null) == (initial.Challenge == null), "Challenge presence changed.");
            if (c == null || initial.Challenge == null) return null;
            var config = initial.Challenge.Configuration;
            Defined(c.Status); Nonnegative(c.RegionalConsumed, c.Produced);
            Require(c.Status == ChallengeStatus.Active ? c.CompletedDay == null && data.Day < config.DeadlineDay :
                c.CompletedDay == data.Day && c.CompletedDay <= config.DeadlineDay, "Invalid challenge lifecycle.");
            if (c.Status == ChallengeStatus.Failed) Require(data.Day == config.DeadlineDay, "Failure must occur on the configured deadline.");
            var recovery = config.Kind == ChallengeKind.CityRecovery ?
                new CityRecoveryProgress(config, ports.Single(p => p.Id == config.PortId).City!) : null;
            if (c.Status == ChallengeStatus.Succeeded)
            {
                var funded = data.Companies.Single(company => company.Id == config.OwnerId).Cash >= config.MinimumCash;
                var qualified = config.Kind == ChallengeKind.CityRecovery ? recovery!.Qualified :
                    ExpandingChallenges.IsExpanding(config.Kind) ? c.Produced == 100 :
                    config.Kind == ChallengeKind.RegionalRelief ? c.RegionalConsumed >= config.RequiredQuantity :
                    config.Kind == ChallengeKind.EnterpriseAmbition ? c.Produced >= config.RequiredQuantity && c.Margin >= config.RequiredMargin :
                    c.NetDelivered >= config.RequiredQuantity;
                Require(funded && qualified, "Saved success does not meet its recorded objective and solvency requirements.");
            }
            if (ExpandingChallenges.IsExpanding(config.Kind)) Require(c.Produced <= 100, "Invalid v3 objective percentage.");
            return new ChallengeSnapshot(config, c.NetDelivered, c.Status, c.CompletedDay, c.RegionalConsumed, c.Produced, c.Margin, recovery);
        }

        private static IReadOnlyDictionary<string, int> Inventory(ContentCatalog content, Dictionary<string, int> stock)
        {
            if (stock == null) throw new WorldSaveException("Inventory is missing.");
            SameIds(stock.Keys, content.Goods.Select(g => g.Id), "inventory goods");
            foreach (var quantity in stock.Values) Nonnegative(quantity);
            return new Inventory(content, stock).Snapshot();
        }
        private static void Totals(ContentCatalog content, Dictionary<string, long> values)
        {
            if (values == null || values.Count > content.Goods.Count) throw new WorldSaveException("Invalid goods totals.");
            foreach (var pair in values) { content.GetGood(pair.Key); Nonnegative(pair.Value); }
        }
        private static long Get(IReadOnlyDictionary<string, long> values, string id) => values.TryGetValue(id, out var value) ? value : 0;
        private static long Nonnegative(params long[] values)
        { foreach (var value in values) Require(value >= 0, "A balance or counter is negative."); return values[0]; }
        private static void Defined<T>(T value) where T : struct => Require(Enum.IsDefined(typeof(T), value), "Unknown state enumeration.");
        private static void Count<T>(T[] values, int minimum, int maximum)
        { Require(values != null && values.Length >= minimum && values.Length <= maximum && values.All(v => v != null), "Save collection count is invalid."); }
        private static void Unique(IEnumerable<string> values, string name)
        { var list = values.ToArray(); Require(list.All(v => !string.IsNullOrWhiteSpace(v)) && list.Distinct(StringComparer.Ordinal).Count() == list.Length, "Duplicate or missing " + name + "."); }
        private static void SameIds(IEnumerable<string> actual, IEnumerable<string> expected, string name)
        { var values = actual.ToArray(); Unique(values, name); Require(values.OrderBy(v => v, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(v => v, StringComparer.Ordinal)), "Unexpected " + name + " identities."); }
        private static void Require(bool condition, string message) { if (!condition) throw new WorldSaveException(message); }
    }
}
