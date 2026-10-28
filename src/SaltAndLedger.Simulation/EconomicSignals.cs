using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>One next household opportunity evaluated against committed stock, quotes and funds.</summary>
    public sealed class EssentialNeedSignal
    {
        public string PortId { get; }
        public bool Modeled { get; }
        public bool HasNeed => RuleId != null;
        public bool HasObservation { get; }
        public string? RuleId { get; }
        public string? GoodId { get; }
        public long? ScheduledDueDay { get; }
        public long? NextDueDay { get; }
        public int Population { get; }
        public int DemandRemainder { get; }
        public int NextDemandRemainder { get; }
        public int DesiredQuantity { get; }
        public int MarketStock { get; }
        public int StockShortfall { get; }
        public bool PreviewAvailable { get; }
        public bool NumericCapacityBlocked { get; }
        public int? AffordableQuantity { get; }
        public int? AffordabilityShortfall { get; }
        public long? CurrentPayment { get; }
        public long HouseholdCash { get; }

        internal EssentialNeedSignal(RegionalWorldSnapshot snapshot, RegionalPortSnapshot port,
            ConsumptionSnapshot? need, RegionalConsumption? preview, bool numericCapacityBlocked = false)
        {
            PortId = port.Id;
            Modeled = port.City != null;
            Population = port.City?.Population ?? 0;
            HouseholdCash = port.City?.HouseholdCash ?? 0;
            HasObservation = port.City != null && port.City.FoodWindow.Count != 0;
            if (need == null) return;
            RuleId = need.Configuration.Id;
            GoodId = need.Configuration.GoodId;
            ScheduledDueDay = need.NextDueDay;
            NextDueDay = Math.Max(checked(snapshot.Day + 1), need.NextDueDay);
            DemandRemainder = need.DemandRemainder;
            var demand = need.Configuration.PreviewDemand(Population, DemandRemainder);
            NextDemandRemainder = demand.Remainder;
            DesiredQuantity = demand.Quantity;
            MarketStock = port.Inventory[GoodId];
            StockShortfall = Math.Max(0, DesiredQuantity - MarketStock);
            PreviewAvailable = preview != null;
            NumericCapacityBlocked = numericCapacityBlocked;
            if (preview == null) return;
            AffordableQuantity = preview.ConsumedQuantity;
            AffordabilityShortfall = preview.UnmetAffordability;
            CurrentPayment = preview.Payment;
        }
    }

    /// <summary>Canonical batch quantity and current holdings in the actual material store.</summary>
    public sealed class ProductionGoodSignal
    {
        public string GoodId { get; }
        public int Quantity { get; }
        public int CurrentStock { get; }
        public int Shortfall => Math.Max(0, Quantity - CurrentStock);

        internal ProductionGoodSignal(string goodId, int quantity, int stock)
        { GoodId = goodId; Quantity = quantity; CurrentStock = stock; }
    }

    /// <summary>
    /// A conditional single batch, never a reservation or simulation of intervening operations.
    /// Outputs describe the canonical complete batch, including when current blockers prevent it.
    /// </summary>
    public sealed class ProductionOpportunitySignal
    {
        public string PortId { get; }
        public string FacilityId { get; }
        public string BuildingId { get; }
        public string RecipeId { get; }
        public string? OwnerId { get; }
        public bool IsPrivate => OwnerId != null;
        public string StoreId { get; }
        public bool UsesSharedStock { get; }
        public bool Enabled { get; }
        public bool UnderConstruction { get; }
        public int OperatingRate { get; }
        public ProductionPriority Priority { get; }
        public int ProductionCredit { get; }
        public long? NextRateAdmittedDay { get; }
        public long ScheduledDueDay { get; }
        public long NextEligibleDay { get; }
        public ScheduledProductionOutcome CurrentReadiness { get; }
        public ScheduledProductionOutcome NextDayOutcome { get; }
        public ScheduledProductionOutcome LastOutcome { get; }
        public long EmployerCash { get; }
        public long WagePerBatch { get; }
        public long StoredUnits { get; }
        public int? StorageCapacity { get; }
        public int? OutputStockLimit { get; }
        public bool HasNumericHeadroom { get; }
        public bool ReadyFromCurrentState => CurrentReadiness == ScheduledProductionOutcome.Produced && HasNumericHeadroom;
        public IReadOnlyList<ProductionGoodSignal> Inputs { get; }
        public IReadOnlyList<ProductionGoodSignal> Outputs { get; }

        internal ProductionOpportunitySignal(RegionalWorldSnapshot snapshot, string portId, string facilityId,
            string buildingId, RecipeDefinition recipe, string? ownerId, string storeId,
            IReadOnlyDictionary<string, int> stock, bool shared, bool enabled, bool underConstruction,
            long dueDay, long employerCash, long wage, int? capacity, int? outputLimit,
            ScheduledProductionOutcome readiness, ScheduledProductionOutcome nextDay,
            ScheduledProductionOutcome last, int intervalDays = 1, int operatingRate = 100,
            ProductionPriority priority = ProductionPriority.Normal, int productionCredit = 0)
        {
            PortId = portId; FacilityId = facilityId; BuildingId = buildingId; RecipeId = recipe.Id;
            OwnerId = ownerId; StoreId = storeId; UsesSharedStock = shared;
            Enabled = enabled; UnderConstruction = underConstruction;
            ScheduledDueDay = dueDay; NextEligibleDay = Math.Max(checked(snapshot.Day + 1), dueDay);
            OperatingRate = operatingRate; Priority = priority; ProductionCredit = productionCredit;
            NextRateAdmittedDay = FactoryProduction.NextAdmittedDay(NextEligibleDay, intervalDays,
                enabled && !underConstruction, operatingRate, productionCredit);
            EmployerCash = employerCash; WagePerBatch = wage;
            StoredUnits = stock.Values.Sum(q => (long)q);
            StorageCapacity = capacity; OutputStockLimit = outputLimit;
            CurrentReadiness = readiness; NextDayOutcome = nextDay; LastOutcome = last;
            Inputs = Array.AsReadOnly(recipe.Inputs.Select(i => new ProductionGoodSignal(i.GoodId,
                checked(i.Quantity * snapshot.QuantityScale), stock[i.GoodId])).ToArray());
            Outputs = Array.AsReadOnly(recipe.Outputs.Select(o => new ProductionGoodSignal(o.GoodId,
                checked(o.Quantity * snapshot.QuantityScale), stock[o.GoodId])).ToArray());
            HasNumericHeadroom = Outputs.All(o => (long)o.CurrentStock -
                Inputs.Where(i => i.GoodId == o.GoodId).Sum(i => (long)i.Quantity) + o.Quantity <= int.MaxValue);
        }
    }

    /// <summary>Pure, bounded read models. They never step, reserve, mutate or construct a world.</summary>
    public static partial class EconomicSignals
    {
        public static EssentialNeedSignal NextEssentialNeed(RegionalWorldSnapshot snapshot, string portId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var port = snapshot.Ports[portId];
            var need = port.City == null ? null : port.Consumption.FirstOrDefault(c => c.Configuration.Kind == ConsumptionKind.Essential);
            if (need == null) return new EssentialNeedSignal(snapshot, port, null, null);
            // Validate the physical request independently. Only a failed monetary proposal is
            // represented as unavailable; invalid demand arithmetic is not silently repaired.
            _ = need.Configuration.PreviewDemand(port.City!.Population, need.DemandRemainder);
            RegionalConsumption preview;
            try
            {
                preview = snapshot.PreviewHouseholdConsumption(portId, need.Configuration.Id);
            }
            catch (OverflowException)
            {
                return new EssentialNeedSignal(snapshot, port, need, null, numericCapacityBlocked: true);
            }
            return new EssentialNeedSignal(snapshot, port, need, preview);
        }

        public static ProductionOpportunitySignal PublicProduction(RegionalWorldSnapshot snapshot,
            ContentCatalog content, string portId, string facilityId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (content == null) throw new ArgumentNullException(nameof(content));
            var port = snapshot.Ports[portId];
            var facility = port.Facilities.Single(f => f.Configuration.Instance.Id == facilityId);
            var config = facility.Configuration;
            var recipe = content.GetRecipe(config.Instance.RecipeId);
            var nextDay = checked(snapshot.Day + 1);
            ScheduledProductionOutcome Preview(long day) => PublicProductionRules.Preview(recipe, facility,
                id => port.Inventory[id], snapshot.QuantityScale, day, port.MarketCash);
            return new ProductionOpportunitySignal(snapshot, portId, facilityId, config.Instance.BuildingId,
                recipe, null, "market:" + portId, port.Inventory, false, facility.Enabled, false,
                facility.NextDueDay, port.MarketCash, config.WagePerBatch, null, config.OutputStockLimit,
                Preview(Math.Max(nextDay, facility.NextDueDay)), Preview(nextDay), facility.LastOutcome);
        }

        public static ProductionOpportunitySignal PrivateProduction(RegionalWorldSnapshot snapshot,
            ContentCatalog content, string facilityId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (content == null) throw new ArgumentNullException(nameof(content));
            var facility = snapshot.PrivateFacilities[facilityId];
            var plan = facility.Blueprint;
            var recipe = content.GetRecipe(plan.RecipeId);
            var storage = snapshot.GetFacilityStorage(facilityId);
            var cash = snapshot.Companies[facility.OwnerId].Cash;
            var nextDay = checked(snapshot.Day + 1);
            var outputLimit = snapshot.FacilityOutputStockLimit(facilityId);
            var readiness = PrivateProductionRules.Preview(recipe, plan, id => storage.Inventory[id],
                snapshot.QuantityScale, storage.StoredUnits, cash, facility.Enabled, facility.ConstructionStatus,
                facility.NextProductionDay, Math.Max(nextDay, facility.NextProductionDay),
                storage.Capacity, snapshot.UsesPooledCompanyStorage, outputLimit, facility.OperatingRate, facility.ProductionCredit);
            return new ProductionOpportunitySignal(snapshot, facility.PortId, facilityId, plan.BuildingId,
                recipe, facility.OwnerId, storage.Id, storage.Inventory, snapshot.UsesPooledCompanyStorage,
                facility.Enabled, !facility.IsOperational, facility.NextProductionDay, cash, plan.WagePerBatch,
                storage.Capacity, outputLimit, readiness, snapshot.PreviewFacilityProduction(facilityId, content), facility.LastOutcome,
                plan.IntervalDays, facility.OperatingRate, facility.Priority, facility.ProductionCredit);
        }
    }

    internal static class PublicProductionRules
    {
        internal static ScheduledProductionOutcome Preview(RecipeDefinition recipe, ScheduledFacilitySnapshot facility,
            Func<string, int> quantity, int scale, long attemptDay, long employerCash)
        {
            if (attemptDay < facility.NextDueDay) return ScheduledProductionOutcome.NotDue;
            if (!facility.Enabled) return ScheduledProductionOutcome.Disabled;
            var config = facility.Configuration;
            if (recipe.Inputs.Any(i => quantity(i.GoodId) < checked(i.Quantity * scale)))
                return ScheduledProductionOutcome.InputShortage;
            if (config.OutputStockLimit.HasValue && recipe.Outputs.Any(output =>
                (long)quantity(output.GoodId) -
                recipe.Inputs.Where(i => i.GoodId == output.GoodId).Sum(i => (long)checked(i.Quantity * scale)) +
                checked(output.Quantity * scale) > config.OutputStockLimit.Value))
                return ScheduledProductionOutcome.OutputLimit;
            return employerCash < config.WagePerBatch ? ScheduledProductionOutcome.PayrollShortage : ScheduledProductionOutcome.Produced;
        }
    }
}
