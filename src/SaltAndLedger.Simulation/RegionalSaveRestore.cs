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
                if (vessel.Itinerary != null) CargoItineraryEvaluator.ValidatePolicy(restored, vessel.Id, vessel.Itinerary);
            }
            if (restored.Economy != null)
            {
                foreach (var owner in restored.Companies.Keys) _ = restored.CompanyValue(owner).Total;
                if (restored.RunIdentity?.Commission != null)
                {
                    var challenge = restored.Challenge!;
                    var actual = GrandCommission.Progress(restored);
                    var qualified = actual.Qualified &&
                        restored.Companies[challenge.Configuration.OwnerId].Cash >= challenge.Configuration.MinimumCash;
                    RequireCommission(challenge.NetDeliveredQuantity == 0 && challenge.RegionalConsumedQuantity == 0 &&
                        challenge.RealizedGrossMargin == restored.Accounting.Companies[challenge.Configuration.OwnerId].RealizedGrossMargin,
                        "Saved commission accounting counters disagree with authoritative company accounting.");
                    RequireCommission(challenge.ProducedQuantity == actual.Percent &&
                        (challenge.Status != ChallengeStatus.Succeeded || qualified),
                        "Saved commission progress disagrees with authoritative mission conditions.");
                    RequireCommission(!qualified || (challenge.Status != ChallengeStatus.Active && challenge.Status != ChallengeStatus.Failed),
                        "Saved commission lifecycle conflicts with its already qualified state.");
                }

            }
            // This fresh world is returned only after every check passes. No daily operation or challenge
            // evaluation is invoked by restore, including when the saved challenge is already terminal.
            state = restored;
        }

        private static void RequireCommission(bool condition, string message)
        { if (!condition) throw new WorldSaveException(message); }
    }

    internal static partial class RegionalSaveRestore
    {
        internal static void ValidateCounts(RegionalSaveData data)
        {
            Count(data.Ports, 4, 4); Count(data.Ships, 1, 64); Count(data.Companies, 1, 64);
            Count(data.Facilities, 0, WorldSaveCodec.MaximumFacilities);
            if (data.CompanyStockpiles != null)
            {
                Count(data.CompanyStockpiles, 0, WorldSaveCodec.MaximumFacilities);
                foreach (var stockpile in data.CompanyStockpiles)
                {
                    if (stockpile.Policy == null) throw new WorldSaveException("Warehouse policy is missing.");
                    Count(stockpile.Policy.Rules, 0, 64);
                }
            }
            Count(data.Stores, 1, WorldSaveCodec.MaximumFacilities + 64);
            if (data.Circulation != null)
            {
                Count(data.Circulation.Commercial, 1, 256);
                Count(data.Circulation.Towns, 4, 4);
            }
            foreach (var ship in data.Ships) ValidateItineraryCounts(ship);
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
            if (data.Identity?.Commission != null) Count(data.Identity.Commission.Goals, 4, 4);
            if (data.CommissionHistory != null)
            {
                Count(data.CommissionHistory.Ships, 1, 3);
                Count(data.CommissionHistory.Markets, 0, 28);
                foreach (var ship in data.CommissionHistory.Ships)
                    Require(ship.Visits != null && ship.Visits.Count >= 1 && ship.Visits.Count <= 4,
                        "Invalid bounded ship visit count.");
            }
        }

        internal static RegionalWorldSnapshot Restore(ContentCatalog content, RegionalWorldSnapshot initial, RegionalSaveData data)
        {
            Require(data.Day >= 0 && data.Revision >= data.Day, "Invalid day or revision.");
            Require(data.NextLotId >= 1, "Invalid FIFO sequence.");
            SameIds(data.Ports.Select(p => p.Id), initial.Ports.Keys, "ports");
            SameIds(data.Companies.Select(c => c.Id), initial.Companies.Keys, "companies");
            var economy = RestoreEconomy(initial, data);
            var circulation = RestoreCirculation(initial, data);
            var shipConfigurations = RestoreShipConfigurations(initial, data, economy);
            var companies = data.Companies.Select(c => new CompanySnapshot(c.Id, Nonnegative(c.Cash))).ToArray();
            var ports = data.Ports.Select(p => RestorePort(content, initial.Ports[p.Id], p, data.Day,
                circulation)).ToArray();
            var facilities = data.Facilities.Select(f => RestoreFacility(content, initial, f, data.Day)).ToArray();
            var stockpiles = RestoreCompanyStockpiles(content, initial, data, facilities);
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
                companies.Single(c => c.Id == shipConfigurations[s.Id].OwnerId).Cash, data.Day)).ToArray();
            var stores = RestoreStores(content, data, ships, facilities, stockpiles);
            var financial = data.Companies.Select(c => RestoreCompany(content, initial, c, stores)).ToArray();
            var accounting = new AccountingSnapshot(financial, stores, data.NextLotId);
            var challenge = RestoreChallenge(initial, data, ports);
            var commissionHistory = RestoreCommissionHistory(content, initial, data, ships, economy);
            return new RegionalWorldSnapshot(data.Day, data.Revision, ports, ships, initial.Routes, companies,
                initial.IndustryBlueprints.Values, facilities, challenge, accounting, initial.Difficulty, initial.RunIdentity, economy,
                commissionHistory, stockpiles, circulation);
        }

        private static RegionalPortSnapshot RestorePort(ContentCatalog content, RegionalPortSnapshot initial, RegionalSavePort data,
            long day, CirculationSnapshot? circulation)
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
                else if (circulation != null)
                {
                    var needs = initial.Configuration.Consumption.Where(n => n.GoodId == q.GoodId &&
                        (n.Kind == ConsumptionKind.Essential || n.Kind == ConsumptionKind.Luxury)).ToArray();
                    var authored = initial.Configuration.Market.Quotes[q.GoodId].Pricing!;
                    if (needs.Length == 0) Require(p.TargetStock == authored.TargetStock, "Non-household target changed.");
                    else
                    {
                        var minimum = needs.Any(n => n.Kind == ConsumptionKind.Luxury) ?
                            Math.Max(initial.City!.Configuration.QuantityScale, authored.TargetStock / 2) : 1;
                        Require(p.TargetStock >= minimum && p.TargetStock <=
                            checked(LivingCityPricing.MaximumTargetStock * initial.City!.Configuration.QuantityScale),
                            "Coverage target is outside its versioned bounds.");
                    }
                }
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
                Require(f.LastOutcome != ScheduledProductionOutcome.RateLimited,
                    "Public production cannot have a private operating-rate outcome.");
                return new ScheduledFacilitySnapshot(original, f.Enabled, f.NextDueDay, f.LastOutcome);
            }).ToArray();
            var consumption = data.Consumption.Select(c => RestoreConsumption(
                initial.Consumption.Single(n => n.Configuration.Id == c.Id).Configuration, c, day)).ToArray();
            var fiscal = circulation?.Towns[data.Id];
            var householdTransfers = circulation == null ? BigInteger.Zero :
                (BigInteger)circulation.HouseholdFeesReceived(data.Id) + fiscal!.TotalRelief - fiscal.TotalLevy;
            var city = RestoreCity(initial.Configuration.Settlement, data.City, day, householdTransfers);
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

        private static CitySnapshot? RestoreCity(SettlementConfiguration? config, RegionalSaveCity? c, long day,
            BigInteger householdTransfers)
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
            Require((BigInteger)c.Cash == (BigInteger)config.InitialHouseholdCash + c.Wages - c.Retail + householdTransfers,
                "Household cash does not reconcile with wages, retail and explicit circulation transfers.");
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
            RegionalShipConfiguration config, RegionalSaveShip s, long cash, long day)
        {
            Defined(s.IndustrialStage);
            Require((s.Policy == null ? 0 : 1) + (s.IndustrialPolicy == null ? 0 : 1) +
                (s.Itinerary == null ? 0 : 1) <= 1, "Ship has conflicting automation policies.");
            Require(!s.AutomationEnabled || s.Policy != null || s.IndustrialPolicy != null || s.Itinerary != null,
                "Enabled ship has no policy.");
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
            var itinerary = RestoreItinerary(initial, config, s);
            var progress = RestoreItineraryProgress(initial, s, itinerary, day);
            return new RegionalShipSnapshot(config, new ShipSnapshot(s.Id, config.Ship.CargoCapacity, cargo,
                s.Dock, s.Origin, s.Destination, s.RemainingDays), cash, s.Policy, s.AutomationEnabled,
                s.VoyageDays, s.IndustrialPolicy, s.IndustrialStage, itinerary, progress);
        }

        private static PrivateFacilitySnapshot RestoreFacility(ContentCatalog content, RegionalWorldSnapshot initial, RegionalSaveFacility f, long day)
        {
            RuntimeIdentifier.Validate(f.Id, nameof(f.Id));
            Require(initial.Companies.ContainsKey(f.OwnerId), "Unknown facility owner.");
            var blueprint = initial.IndustryBlueprints[f.BlueprintId];
            Require(blueprint.EligiblePortIds.Contains(f.PortId), "Facility is at an ineligible port.");
            var recipe = content.GetRecipe(blueprint.RecipeId);
            Defined(f.ConstructionStatus); Defined(f.LastOutcome); Defined(f.Priority);
            Nonnegative(f.CreatedDay, f.CompletionDay, f.NextProductionDay, f.Batches, f.Wages);
            Require(f.OperatingRate >= 0 && f.OperatingRate <= 100 && f.ProductionCredit >= 0 && f.ProductionCredit < 200,
                "Factory operating rate or production credit is outside its bounded range.");
            Require(f.CreatedDay <= day && f.CompletionDay >= f.CreatedDay &&
                f.NextProductionDay >= checked(f.CompletionDay + blueprint.IntervalDays) &&
                f.NextProductionDay <= checked(Math.Max(day, f.CompletionDay) + blueprint.IntervalDays), "Invalid facility schedule.");
            var starter = initial.PrivateFacilities.TryGetValue(f.Id, out var original);
            Require(f.StarterGrant == starter && (!starter || f.CreatedDay == 0), "Invalid starter endowment claim.");
            var delay = starter ? 0 : initial.Difficulty == RunDifficulty.SlowFoundations &&
                initial.Ships.Values.Any(s => s.IsPlayer && s.OwnerId == f.OwnerId) ? 2 : 0;
            Require(f.CompletionDay == checked(f.CreatedDay + delay) &&
                (f.ConstructionStatus == FacilityConstructionStatus.Operational) == (f.CompletionDay <= day), "Invalid construction lifecycle.");
            Require((f.ProductionCredit == 0 || day >= checked(f.CompletionDay + blueprint.IntervalDays)) &&
                (f.ProductionCredit < 100 || f.NextProductionDay <= day),
                "Factory production credit precedes its scheduled opportunity.");
            var inventory = Inventory(content, f.Inventory);
            Require(f.SharedStockpileId == "company:" + f.OwnerId + ":" + f.PortId,
                "Facility does not reference its owning company's local stockpile.");
            Require(inventory.Values.All(v => v == 0), "A pooled facility cannot duplicate physical company stock.");
            Require(f.Batches <= Math.Max(0, day - f.CompletionDay) / blueprint.IntervalDays,
                "Pooled facility production precedes its funded batch schedule.");
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
            return new PrivateFacilitySnapshot(f.Id, f.OwnerId, f.PortId, blueprint, new IndustryPolicy(), f.Enabled,
                f.CreatedDay, f.NextProductionDay, f.LastOutcome, inventory, f.Batches, 0, 0,
                f.Produced, f.Consumed, IndustryWaitReason.Disabled, IndustryWaitReason.Disabled, f.StarterGrant, f.Provenance,
                f.ConstructionStatus, f.CompletionDay, day, f.Wages, f.SharedStockpileId,
                f.OperatingRate, f.Priority, f.ProductionCredit);
        }

        private static CompanyStockpileSnapshot[] RestoreCompanyStockpiles(ContentCatalog content,
            RegionalWorldSnapshot initial, RegionalSaveData data, PrivateFacilitySnapshot[] facilities)
        {
            Require(data.CompanyStockpiles != null, "Company stockpiles are missing.");
            var capacities = facilities.GroupBy(f => "company:" + f.OwnerId + ":" + f.PortId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => new { Owner = g.First().OwnerId, Port = g.First().PortId,
                    Capacity = g.Aggregate(0, (sum, f) => checked(sum + f.Blueprint.WarehouseCapacity)) }, StringComparer.Ordinal);
            SameIds(data.CompanyStockpiles!.Select(s => s.Id), capacities.Keys, "company stockpiles");
            var stockpiles = data.CompanyStockpiles!.Select(s =>
            {
                var expected = capacities[s.Id];
                Require(s.OwnerId == expected.Owner && s.PortId == expected.Port && s.Capacity == expected.Capacity,
                    "Company stockpile ownership or installed capacity is inconsistent.");
                var inventory = Inventory(content, s.Inventory);
                Require(inventory.Values.Sum(v => (long)v) <= s.Capacity, "Company stockpile exceeds installed capacity.");
                Nonnegative(s.Procurement, s.Revenue);
                Defined(s.ProcurementWait); Defined(s.SalesWait);
                var policy = RestoreWarehousePolicy(content, s.Policy, s.Capacity);
                return new CompanyStockpileSnapshot(s.Id, s.OwnerId, s.PortId, s.Capacity, inventory,
                    policy, s.Procurement, s.Revenue, s.ProcurementWait, s.SalesWait);
            }).ToArray();
            // Facility labor and output remain individually attributed even when physical materials
            // share one store. No saved facility can invent production already absent from company totals.
            foreach (var company in data.Companies)
            {
                var owned = facilities.Where(f => f.OwnerId == company.Id).ToArray();
                var ownedStockpiles = stockpiles.Where(s => s.OwnerId == company.Id).ToArray();
                Require(ownedStockpiles.Aggregate(BigInteger.Zero, (sum, s) => sum + s.ProcurementCost) <= company.Purchases &&
                    ownedStockpiles.Aggregate(BigInteger.Zero, (sum, s) => sum + s.SalesRevenue) <= company.Revenue,
                    "Warehouse trading counters exceed company accounting.");
                Require(owned.Aggregate(BigInteger.Zero, (sum, f) => sum + f.WagesPaid) == company.Wages,
                    "Pooled facility payroll does not reconcile with company accounting.");
                foreach (var good in content.Goods)
                    Require(owned.Aggregate(BigInteger.Zero, (sum, f) => sum + Get(f.ProducedQuantities, good.Id)) == Get(company.Produced, good.Id) &&
                        owned.Aggregate(BigInteger.Zero, (sum, f) => sum + Get(f.ConsumedQuantities, good.Id)) == Get(company.Consumed, good.Id),
                        "Pooled facility production does not reconcile with company accounting.");
            }
            return stockpiles;
        }

        private static WarehousePolicy RestoreWarehousePolicy(ContentCatalog content, RegionalSaveWarehousePolicy saved, int capacity)
        {
            if (saved == null) throw new WorldSaveException("Warehouse policy is missing.");
            Count(saved.Rules, 0, content.Goods.Count);
            Unique(saved.Rules.Select(r => r.GoodId), "warehouse commodity rules");
            Nonnegative(saved.CashReserve);
            var rules = saved.Rules.Select(r =>
            {
                content.GetGood(r.GoodId);
                Nonnegative(r.BuyTarget, r.MaximumBuyPrice, r.SellReserve, r.MinimumSellPrice);
                Require(r.BuyTarget <= capacity && r.SellReserve <= capacity,
                    "Warehouse policy exceeds installed storage capacity.");
                Require(!r.BuyEnabled || !r.SellEnabled || r.BuyTarget <= r.SellReserve,
                    "Warehouse buying and selling thresholds overlap.");
                return new WarehouseCommodityRule(r.GoodId, r.BuyEnabled, r.BuyTarget, r.MaximumBuyPrice,
                    r.SellEnabled, r.SellReserve, r.MinimumSellPrice);
            }).ToArray();
            return new WarehousePolicy(saved.CashReserve, rules);
        }

        private static InventoryCostSnapshot[] RestoreStores(ContentCatalog content, RegionalSaveData data,
            RegionalShipSnapshot[] ships, PrivateFacilitySnapshot[] facilities, CompanyStockpileSnapshot[] stockpiles)
        {
            var local = data.Identity.CompanyStorageRules == CompanyStorageRules.Pooled ?
                stockpiles.Select(s => (Id: s.Id, Owner: s.OwnerId, Stock: s.Inventory)) :
                facilities.Select(f => (Id: "facility:" + f.Id, Owner: f.OwnerId, Stock: f.Inventory));
            var physical = ships.Select(s => (Id: "ship:" + s.Id, Owner: s.OwnerId, Stock: s.Cargo))
                .Concat(local)
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
                c.MintReceipts, c.MintMaterialCost, c.PublicCommissionExpenditure, c.FeesPaid);
            Require(c.InitialCash == initial.Companies[c.Id].Cash, "Initial company cash changed.");
            Require((BigInteger)c.Cash == (BigInteger)c.InitialCash + c.Revenue - c.Purchases - c.Construction - c.Wages +
                c.MintReceipts - c.PublicCommissionExpenditure - c.FeesPaid,
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
                c.PublicCommissionExpenditure, c.SurrenderedQuantities, c.FeesPaid);
            _ = result.OperatingSurplusAfterWages;
            _ = result.OperatingSurplusAfterExpenses;
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
            if (c.Status == ChallengeStatus.Succeeded)
            {
                var funded = data.Companies.Single(company => company.Id == config.OwnerId).Cash >= config.MinimumCash;
                Require(funded && c.Produced == 100, "Saved success does not meet its recorded objective and solvency requirements.");
            }
            Require(c.Produced <= 100, "Invalid commission percentage.");
            return new ChallengeSnapshot(config, c.NetDelivered, c.Status, c.CompletedDay, c.RegionalConsumed, c.Produced, c.Margin);
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
