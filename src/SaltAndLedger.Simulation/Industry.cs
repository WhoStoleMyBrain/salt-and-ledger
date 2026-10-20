using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class CompanyConfiguration
    {
        public string Id { get; }
        public long InitialCash { get; }
        public CompanyConfiguration(string id, long initialCash)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            if (initialCash < 0) throw new ArgumentOutOfRangeException(nameof(initialCash));
            Id = id; InitialCash = initialCash;
        }
    }

    public sealed class CompanySnapshot
    {
        public string Id { get; }
        public long Cash { get; }
        internal CompanySnapshot(string id, long cash) { Id = id; Cash = cash; }
    }

    public sealed class IndustryBlueprint
    {
        public string Id { get; }
        public string BuildingId { get; }
        public string RecipeId { get; }
        public long ConstructionCost { get; }
        public int IntervalDays { get; }
        public int WarehouseCapacity { get; }
        public int? OutputStockLimit { get; }
        public IReadOnlyList<string> EligiblePortIds { get; }
        public IndustryBlueprint(string id, string buildingId, string recipeId, long constructionCost,
            int intervalDays, int warehouseCapacity, IEnumerable<string> eligiblePortIds, int? outputStockLimit = null)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            RuntimeIdentifier.Validate(buildingId, nameof(buildingId));
            RuntimeIdentifier.Validate(recipeId, nameof(recipeId));
            if (constructionCost <= 0) throw new ArgumentOutOfRangeException(nameof(constructionCost));
            if (intervalDays <= 0) throw new ArgumentOutOfRangeException(nameof(intervalDays));
            if (warehouseCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(warehouseCapacity));
            if (outputStockLimit < 0) throw new ArgumentOutOfRangeException(nameof(outputStockLimit));
            if (eligiblePortIds == null) throw new ArgumentNullException(nameof(eligiblePortIds));
            var ports = eligiblePortIds.ToArray();
            if (ports.Length == 0) throw new ArgumentException("A blueprint needs an eligible port.");
            foreach (var port in ports) RuntimeIdentifier.Validate(port, nameof(eligiblePortIds));
            if (ports.Distinct(StringComparer.Ordinal).Count() != ports.Length)
                throw new ArgumentException("Eligible ports must be unique.");
            Id = id; BuildingId = buildingId; RecipeId = recipeId; ConstructionCost = constructionCost;
            IntervalDays = intervalDays; WarehouseCapacity = warehouseCapacity; OutputStockLimit = outputStockLimit;
            EligiblePortIds = Array.AsReadOnly(ports.OrderBy(p => p, StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class InputStockRule
    {
        public string GoodId { get; }
        public int TargetQuantity { get; }
        public long MaxBuyPrice { get; }
        public InputStockRule(string goodId, int targetQuantity, long maxBuyPrice)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (targetQuantity < 0) throw new ArgumentOutOfRangeException(nameof(targetQuantity));
            if (maxBuyPrice <= 0) throw new ArgumentOutOfRangeException(nameof(maxBuyPrice));
            GoodId = goodId; TargetQuantity = targetQuantity; MaxBuyPrice = maxBuyPrice;
        }
    }

    public sealed class OutputSaleRule
    {
        public string GoodId { get; }
        public int ReserveQuantity { get; }
        public long MinSellPrice { get; }
        public OutputSaleRule(string goodId, int reserveQuantity, long minSellPrice)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (reserveQuantity < 0) throw new ArgumentOutOfRangeException(nameof(reserveQuantity));
            if (minSellPrice <= 0) throw new ArgumentOutOfRangeException(nameof(minSellPrice));
            GoodId = goodId; ReserveQuantity = reserveQuantity; MinSellPrice = minSellPrice;
        }
    }

    public sealed class IndustryPolicy
    {
        public bool ProcurementEnabled { get; }
        public bool SalesEnabled { get; }
        public long CashReserve { get; }
        public IReadOnlyList<InputStockRule> Inputs { get; }
        public IReadOnlyList<OutputSaleRule> Outputs { get; }
        public IndustryPolicy(bool procurementEnabled = false, bool salesEnabled = false, long cashReserve = 0,
            IEnumerable<InputStockRule>? inputs = null, IEnumerable<OutputSaleRule>? outputs = null)
        {
            if (cashReserve < 0) throw new ArgumentOutOfRangeException(nameof(cashReserve));
            var purchases = (inputs ?? Array.Empty<InputStockRule>()).ToArray();
            var sales = (outputs ?? Array.Empty<OutputSaleRule>()).ToArray();
            if (purchases.Any(r => r == null) || sales.Any(r => r == null))
                throw new ArgumentException("Policy rules cannot contain null.");
            if (purchases.Select(r => r.GoodId).Distinct(StringComparer.Ordinal).Count() != purchases.Length ||
                sales.Select(r => r.GoodId).Distinct(StringComparer.Ordinal).Count() != sales.Length)
                throw new ArgumentException("A policy needs unique rules per good.");
            ProcurementEnabled = procurementEnabled; SalesEnabled = salesEnabled; CashReserve = cashReserve;
            Inputs = Array.AsReadOnly(purchases.OrderBy(r => r.GoodId, StringComparer.Ordinal).ToArray());
            Outputs = Array.AsReadOnly(sales.OrderBy(r => r.GoodId, StringComparer.Ordinal).ToArray());
        }
    }

    public enum IndustryWaitReason
    {
        None, Disabled, TargetSatisfied, ReserveRetained, InsufficientMarketInventory,
        WarehouseFull, BuyPriceLimit, SellPriceLimit, CashReserve, InsufficientCompanyFunds,
        InsufficientMarketFunds, MarketInventoryFull, CashAccountFull, UnderConstruction
    }
    public enum ConstructionOutcome { Built, InsufficientFunds, Started, Completed }
    public enum FacilityConstructionStatus { Operational, UnderConstruction }
    public enum TransferDirection { ShipToWarehouse, WarehouseToShip }
    public enum TransferOutcome { Transferred, InsufficientInventory, InsufficientCapacity }

    /// <summary>Authoritative construction dates; a preview does not reserve cash or grant permission to build.</summary>
    public sealed class ConstructionSchedule
    {
        public long OrderedDay { get; }
        public int DelayDays { get; }
        public long CompletionDay { get; }
        public long FirstProductionDay { get; }
        internal ConstructionSchedule(long orderedDay, int delayDays, int productionInterval)
        {
            OrderedDay = orderedDay; DelayDays = delayDays;
            CompletionDay = checked(orderedDay + delayDays);
            FirstProductionDay = checked(CompletionDay + productionInterval);
        }
    }

    public sealed class PrivateFacilitySnapshot
    {
        public string Id { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public IndustryBlueprint Blueprint { get; }
        public IndustryPolicy Policy { get; }
        public bool Enabled { get; }
        public long CreatedDay { get; }
        public long OrderedDay => CreatedDay;
        public FacilityConstructionStatus ConstructionStatus { get; }
        public bool IsOperational => ConstructionStatus == FacilityConstructionStatus.Operational;
        public long ConstructionCompletionDay { get; }
        public long? ConstructionCompletedDay => IsOperational ? ConstructionCompletionDay : (long?)null;
        public int ConstructionDaysRemaining { get; }
        public long FirstProductionDay => checked(ConstructionCompletionDay + Blueprint.IntervalDays);
        public long NextProductionDay { get; }
        public ScheduledProductionOutcome LastOutcome { get; }
        public IReadOnlyDictionary<string, int> Inventory { get; }
        public int StoredUnits { get; }
        public int Capacity => Blueprint.WarehouseCapacity;
        public long ConstructionCost => Blueprint.ConstructionCost;
        public bool IsStarterGrant { get; }
        public string Provenance { get; }
        public long BatchesProduced { get; }
        public long ProcurementCost { get; }
        public long SalesRevenue { get; }
        public IReadOnlyDictionary<string, long> ProducedQuantities { get; }
        public IReadOnlyDictionary<string, long> ConsumedQuantities { get; }
        public IndustryWaitReason ProcurementWaitReason { get; }
        public IndustryWaitReason SalesWaitReason { get; }
        internal PrivateFacilitySnapshot(string id, string ownerId, string portId, IndustryBlueprint blueprint,
            IndustryPolicy policy, bool enabled, long createdDay, long nextProductionDay,
            ScheduledProductionOutcome lastOutcome, IReadOnlyDictionary<string, int> inventory,
            long batchesProduced, long procurementCost, long salesRevenue,
            IReadOnlyDictionary<string, long> produced, IReadOnlyDictionary<string, long> consumed,
            IndustryWaitReason procurementWaitReason, IndustryWaitReason salesWaitReason,
            bool isStarterGrant = false, string provenance = "constructed",
            FacilityConstructionStatus constructionStatus = FacilityConstructionStatus.Operational,
            long? constructionCompletionDay = null, long? snapshotDay = null)
        {
            Id = id; OwnerId = ownerId; PortId = portId; Blueprint = blueprint; Policy = policy;
            Enabled = enabled; CreatedDay = createdDay; NextProductionDay = nextProductionDay; LastOutcome = lastOutcome;
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var quantity in inventory) quantities.Add(quantity.Key, quantity.Value);
            Inventory = new ReadOnlyDictionary<string, int>(quantities);
            StoredUnits = inventory.Values.Aggregate(0, (sum, value) => checked(sum + value));
            BatchesProduced = batchesProduced; ProcurementCost = procurementCost; SalesRevenue = salesRevenue;
            var producedTotals = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var quantity in produced) producedTotals.Add(quantity.Key, quantity.Value);
            var consumedTotals = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var quantity in consumed) consumedTotals.Add(quantity.Key, quantity.Value);
            ProducedQuantities = new ReadOnlyDictionary<string, long>(producedTotals);
            ConsumedQuantities = new ReadOnlyDictionary<string, long>(consumedTotals);
            ProcurementWaitReason = procurementWaitReason; SalesWaitReason = salesWaitReason;
            IsStarterGrant = isStarterGrant; Provenance = provenance;
            ConstructionStatus = constructionStatus;
            ConstructionCompletionDay = constructionCompletionDay ?? createdDay;
            ConstructionDaysRemaining = IsOperational ? 0 : checked((int)Math.Max(0,
                ConstructionCompletionDay - (snapshotDay ?? createdDay)));
        }
    }

    /// <summary>Bounded selection using the same exact marginal curves and totals as execution.</summary>
    internal static class TradeQuantityCalculator
    {
        internal static int Maximum(MarketQuote quote, TradeDirection direction, int marketStock, int privateStock,
            long marketCash, long companyCash, int freeCapacity, int limit = int.MaxValue,
            long marginalLimit = 0, long reserve = 0)
        {
            if (direction != TradeDirection.Buy && direction != TradeDirection.Sell)
                throw new ArgumentOutOfRangeException(nameof(direction));
            var buy = direction == TradeDirection.Buy;
            var upper = Math.Min(limit, buy ? Math.Min(marketStock, Math.Min(freeCapacity, int.MaxValue - privateStock))
                : Math.Min(privateStock, int.MaxValue - marketStock));
            var budget = buy ? Math.Min(Math.Max(0, companyCash - reserve), long.MaxValue - marketCash)
                : Math.Min(marketCash, long.MaxValue - companyCash);
            var lower = 0;
            while (lower < upper)
            {
                var middle = lower + (int)(((long)upper - lower + 1) / 2);
                if ((marginalLimit == 0 || MeetsPrice(quote, direction, marketStock, middle, marginalLimit)) &&
                    quote.FitsBudget(direction, marketStock, middle, budget))
                    lower = middle;
                else upper = middle - 1;
            }
            return lower;
        }

        internal static bool MeetsPrice(MarketQuote quote, TradeDirection direction, int stock, int quantity, long limit)
        {
            var edge = direction == TradeDirection.Buy ? checked(stock - quantity + 1) : checked(stock + quantity);
            var price = quote.Pricing != null ? quote.Pricing.PriceAtEdge(direction, edge) :
                direction == TradeDirection.Buy ? quote.Ask : quote.Bid;
            return direction == TradeDirection.Buy ? price <= limit : price >= limit;
        }
    }
}
