using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public enum InventoryCostOrigin { StarterGrant, Purchased, Manufactured }
    public enum EconomicEventKind { StarterGrant, Purchase, Sale, Transfer, Production, Construction, ConstructionCompleted, Wage, MintExchange, PublicCommission, ExternalSale, TreasurySale }

    /// <summary>
    /// An open interval of an original acquisition. Prefix-floor allocation keeps every integer
    /// of its actual expenditure attached to exactly one unit, even after repeated partial transfers.
    /// </summary>
    public sealed class InventoryCostLot
    {
        public long LotId { get; }
        public InventoryCostOrigin Origin { get; }
        public string Provenance { get; }
        public long OriginalQuantity { get; }
        public long OriginalCost { get; }
        public long Start { get; }
        public int Quantity { get; }
        public long CostBasis => CostThrough(checked(Start + Quantity)) - CostThrough(Start);

        internal InventoryCostLot(long lotId, InventoryCostOrigin origin, string provenance,
            long originalQuantity, long originalCost, long start, int quantity)
        {
            if (lotId < 1 || originalQuantity < 1 || originalCost < 0 || start < 0 || quantity < 1 ||
                start > originalQuantity - quantity) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (!Enum.IsDefined(typeof(InventoryCostOrigin), origin)) throw new ArgumentOutOfRangeException(nameof(origin));
            if (string.IsNullOrWhiteSpace(provenance)) throw new ArgumentException("Cost provenance is required.", nameof(provenance));
            LotId = lotId; Origin = origin; Provenance = provenance; OriginalQuantity = originalQuantity;
            OriginalCost = originalCost; Start = start; Quantity = quantity;
        }

        private long CostThrough(long end) => checked((long)((BigInteger)OriginalCost * end / OriginalQuantity));
        internal InventoryCostLot Slice(int offset, int quantity)
        {
            if (offset < 0 || quantity < 1 || offset > Quantity - quantity)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            return new InventoryCostLot(LotId, Origin, Provenance, OriginalQuantity, OriginalCost,
                checked(Start + offset), quantity);
        }
    }

    public sealed class GoodCostSnapshot
    {
        public string GoodId { get; }
        public int Quantity { get; }
        public long CostBasis { get; }
        public IReadOnlyList<InventoryCostLot> Lots { get; }
        internal GoodCostSnapshot(string goodId, IEnumerable<InventoryCostLot> lots)
        {
            GoodId = goodId;
            Lots = Array.AsReadOnly(lots.ToArray());
            Quantity = Lots.Aggregate(0, (total, lot) => checked(total + lot.Quantity));
            CostBasis = Lots.Aggregate(0L, (total, lot) => checked(total + lot.CostBasis));
        }
    }

    public sealed class InventoryCostSnapshot
    {
        public string StoreId { get; }
        public string OwnerId { get; }
        public IReadOnlyDictionary<string, GoodCostSnapshot> Goods { get; }
        public int TotalUnits { get; }
        public long TotalCostBasis { get; }
        internal InventoryCostSnapshot(string storeId, string ownerId, IEnumerable<GoodCostSnapshot> goods)
        {
            StoreId = storeId; OwnerId = ownerId;
            Goods = AccountingCollections.Dictionary(goods.ToDictionary(g => g.GoodId, StringComparer.Ordinal));
            TotalUnits = Goods.Values.Aggregate(0, (total, good) => checked(total + good.Quantity));
            TotalCostBasis = Goods.Values.Aggregate(0L, (total, good) => checked(total + good.CostBasis));
        }
        public int GetQuantity(string goodId) => Goods.TryGetValue(goodId, out var good) ? good.Quantity : 0;
        public long GetCostBasis(string goodId) => Goods.TryGetValue(goodId, out var good) ? good.CostBasis : 0;
    }

    public sealed class CompanyFinancialSnapshot
    {
        public string OwnerId { get; }
        public long InitialCash { get; }
        public long InventoryPurchases { get; }
        public long SalesRevenue { get; }
        public long CostOfGoodsSold { get; }
        public long RealizedGrossMargin => checked(SalesRevenue - CostOfGoodsSold);
        public long ConstructionExpenditure { get; }
        public long HeldInventoryCost { get; }
        public long WagesPaid { get; }
        public long MintReceipts { get; }
        public long MintMaterialCost { get; }
        public long PublicCommissionExpenditure { get; }
        public long MintNetReceipts => checked(MintReceipts - MintMaterialCost);
        public IReadOnlyDictionary<string, long> SurrenderedQuantities { get; }
        public bool ShouldSerializeMintReceipts() => MintReceipts != 0;
        public bool ShouldSerializeMintMaterialCost() => MintMaterialCost != 0;
        public bool ShouldSerializeMintNetReceipts() => MintReceipts != 0 || MintMaterialCost != 0;
        public bool ShouldSerializePublicCommissionExpenditure() => PublicCommissionExpenditure != 0;
        public bool ShouldSerializeSurrenderedQuantities() => SurrenderedQuantities.Count != 0;
        public long OperatingSurplusAfterWages => checked(RealizedGrossMargin - WagesPaid);
        public bool ShouldSerializeWagesPaid() => WagesPaid != 0;
        public bool ShouldSerializeOperatingSurplusAfterWages() => WagesPaid != 0;
        public IReadOnlyDictionary<string, long> GrantedQuantities { get; }
        public IReadOnlyDictionary<string, long> PurchasedQuantities { get; }
        public IReadOnlyDictionary<string, long> SoldQuantities { get; }
        public IReadOnlyDictionary<string, long> ProducedQuantities { get; }
        public IReadOnlyDictionary<string, long> ConsumedQuantities { get; }
        public IReadOnlyDictionary<string, long> HeldInventoryQuantities { get; }
        internal CompanyFinancialSnapshot(string ownerId, long initialCash, long purchases, long revenue,
            long costOfGoodsSold, long construction, long heldCost, IDictionary<string, long> granted,
            IDictionary<string, long> purchased, IDictionary<string, long> sold, IDictionary<string, long> produced,
            IDictionary<string, long> consumed, IDictionary<string, long> held, long wagesPaid = 0,
            long mintReceipts = 0, long mintMaterialCost = 0, long publicCommissionExpenditure = 0,
            IDictionary<string, long>? surrendered = null)
        {
            OwnerId = ownerId; InitialCash = initialCash; InventoryPurchases = purchases; SalesRevenue = revenue;
            CostOfGoodsSold = costOfGoodsSold; ConstructionExpenditure = construction; HeldInventoryCost = heldCost;
            WagesPaid = wagesPaid;
            MintReceipts = mintReceipts; MintMaterialCost = mintMaterialCost;
            PublicCommissionExpenditure = publicCommissionExpenditure;
            SurrenderedQuantities = AccountingCollections.Dictionary(surrendered ?? new Dictionary<string, long>());
            GrantedQuantities = AccountingCollections.Dictionary(granted);
            PurchasedQuantities = AccountingCollections.Dictionary(purchased);
            SoldQuantities = AccountingCollections.Dictionary(sold);
            ProducedQuantities = AccountingCollections.Dictionary(produced);
            ConsumedQuantities = AccountingCollections.Dictionary(consumed);
            HeldInventoryQuantities = AccountingCollections.Dictionary(held);
            if ((BigInteger)purchases != (BigInteger)costOfGoodsSold + heldCost + mintMaterialCost)
                throw new InvalidOperationException("Inventory expenditure does not reconcile with released and held basis.");
        }
    }

    public sealed class AccountingSnapshot
    {
        public IReadOnlyDictionary<string, CompanyFinancialSnapshot> Companies { get; }
        public IReadOnlyDictionary<string, InventoryCostSnapshot> Stores { get; }
        internal long NextLotId { get; }
        internal AccountingSnapshot(IEnumerable<CompanyFinancialSnapshot> companies,
            IEnumerable<InventoryCostSnapshot> stores, long nextLotId)
        {
            Companies = AccountingCollections.Dictionary(companies.ToDictionary(c => c.OwnerId, StringComparer.Ordinal));
            Stores = AccountingCollections.Dictionary(stores.ToDictionary(s => s.StoreId, StringComparer.Ordinal));
            NextLotId = nextLotId;
        }
    }

    /// <summary>A quantity's actual basis and original grant, purchase, or transformation slices.</summary>
    public sealed class EconomicGoodsMovement
    {
        public string GoodId { get; }
        public int Quantity { get; }
        public long CostBasis { get; }
        public IReadOnlyList<InventoryCostLot> Lots { get; }
        internal EconomicGoodsMovement(string goodId, IEnumerable<InventoryCostLot> lots)
        {
            GoodId = goodId; Lots = Array.AsReadOnly(lots.ToArray());
            Quantity = Lots.Aggregate(0, (total, lot) => checked(total + lot.Quantity));
            CostBasis = Lots.Aggregate(0L, (total, lot) => checked(total + lot.CostBasis));
        }
    }

    /// <summary>
    /// One committed ownership/cost event. CashTotal is unsigned; CompanyCashDelta gives its direction.
    /// Produced/Consumed describe gross owned-stock movements, never a valuation at current quotes.
    /// </summary>
    public sealed class EconomicEvent
    {
        public long Day { get; }
        public long Revision { get; }
        public EconomicEventKind Kind { get; }
        public string CompanyId { get; }
        public string? SourceOwnerId { get; }
        public string? DestinationOwnerId { get; }
        public string? SourceStoreId { get; }
        public string? DestinationStoreId { get; }
        public string? PortId { get; }
        public string? GoodId { get; }
        public long Quantity { get; }
        public long CashTotal { get; }
        public long CompanyCashDelta { get; }
        public long AllocatedCost { get; }
        public string? RecipeId { get; }
        public string? FacilityId { get; }
        public string Provenance { get; }
        public IReadOnlyList<EconomicGoodsMovement> Consumed { get; }
        public IReadOnlyList<EconomicGoodsMovement> Produced { get; }
        internal EconomicEvent(long day, long revision, EconomicEventKind kind, string companyId,
            string? sourceOwner, string? destinationOwner, string? sourceStore, string? destinationStore,
            string? port, string? good, long quantity, long cash, long cashDelta, long allocatedCost,
            string? recipe, string? facility, string provenance,
            IEnumerable<EconomicGoodsMovement> consumed, IEnumerable<EconomicGoodsMovement> produced)
        {
            Day = day; Revision = revision; Kind = kind; CompanyId = companyId;
            SourceOwnerId = sourceOwner; DestinationOwnerId = destinationOwner;
            SourceStoreId = sourceStore; DestinationStoreId = destinationStore; PortId = port; GoodId = good;
            Quantity = quantity; CashTotal = cash; CompanyCashDelta = cashDelta; AllocatedCost = allocatedCost;
            RecipeId = recipe; FacilityId = facility; Provenance = provenance;
            Consumed = Array.AsReadOnly(consumed.ToArray()); Produced = Array.AsReadOnly(produced.ToArray());
        }
        internal EconomicEvent AtCommit(long day, long revision) => new EconomicEvent(day, revision, Kind,
            CompanyId, SourceOwnerId, DestinationOwnerId, SourceStoreId, DestinationStoreId, PortId,
            GoodId, Quantity, CashTotal, CompanyCashDelta, AllocatedCost, RecipeId, FacilityId, Provenance,
            Consumed, Produced);
    }

    internal static class AccountingCollections
    {
        internal static IReadOnlyDictionary<string, T> Dictionary<T>(IDictionary<string, T> values) =>
            new ReadOnlyDictionary<string, T>(new SortedDictionary<string, T>(values, StringComparer.Ordinal));
    }

    /// <summary>Private working state discarded alongside the rest of an exceptional regional operation.</summary>
    internal sealed class WorkingAccounting
    {
        private readonly SortedDictionary<string, Company> companies = new SortedDictionary<string, Company>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, Store> stores = new SortedDictionary<string, Store>(StringComparer.Ordinal);
        private readonly List<EconomicEvent> events = new List<EconomicEvent>();
        private long nextLotId = 1;
        internal WorkingAccounting() { }
        internal WorkingAccounting(AccountingSnapshot before)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            nextLotId = before.NextLotId;
            foreach (var company in before.Companies.Values) companies.Add(company.OwnerId, new Company(company));
            foreach (var store in before.Stores.Values) stores.Add(store.StoreId, new Store(store));
        }

        internal void RegisterCompany(string ownerId, long initialCash)
        {
            RuntimeIdentifier.Validate(ownerId, nameof(ownerId));
            if (initialCash < 0) throw new ArgumentOutOfRangeException(nameof(initialCash));
            companies.Add(ownerId, new Company(ownerId, initialCash));
        }
        internal void RegisterStore(string storeId, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(storeId)) throw new ArgumentException("Store ID is required.", nameof(storeId));
            if (!companies.ContainsKey(ownerId)) throw new KeyNotFoundException("Unknown accounting company.");
            stores.Add(storeId, new Store(storeId, ownerId));
        }
        internal void Grant(string storeId, string goodId, int quantity, string provenance = "initial_inventory")
        {
            ValidateGoods(goodId, quantity, 0);
            var store = stores[storeId];
            var lot = NewLot(InventoryCostOrigin.StarterGrant, provenance, quantity, 0, 0, quantity);
            store.Append(goodId, new[] { lot });
            Add(companies[store.OwnerId].Granted, goodId, quantity);
            events.Add(Event(EconomicEventKind.StarterGrant, store.OwnerId, null, store.OwnerId, null,
                storeId, null, goodId, quantity, 0, 0, 0, null, null, provenance,
                Array.Empty<EconomicGoodsMovement>(), new[] { new EconomicGoodsMovement(goodId, new[] { lot }) }));
        }
        internal void Purchase(string storeId, string portId, string goodId, int quantity, long total)
        {
            ValidateGoods(goodId, quantity, total);
            RuntimeIdentifier.Validate(portId, nameof(portId));
            var store = stores[storeId];
            var company = companies[store.OwnerId];
            var purchases = checked(company.Purchases + total);
            var lot = NewLot(InventoryCostOrigin.Purchased, portId, quantity, total, 0, quantity);
            store.Append(goodId, new[] { lot });
            company.Purchases = purchases;
            Add(company.Purchased, goodId, quantity);
            events.Add(Event(EconomicEventKind.Purchase, store.OwnerId, "market:" + portId, store.OwnerId,
                "market:" + portId, storeId, portId, goodId, quantity, total, -total, total, null, null, portId,
                Array.Empty<EconomicGoodsMovement>(), new[] { new EconomicGoodsMovement(goodId, new[] { lot }) }));
        }
        internal void Sell(string storeId, string portId, string goodId, int quantity, long total)
            => SellToAccount(storeId, portId, goodId, quantity, total, "market:" + portId);
        internal void SellToAccount(string storeId, string portId, string goodId, int quantity, long total, string account)
        {
            ValidateGoods(goodId, quantity, total);
            RuntimeIdentifier.Validate(portId, nameof(portId));
            var store = stores[storeId];
            var company = companies[store.OwnerId];
            var removed = new EconomicGoodsMovement(goodId, store.Take(goodId, quantity));
            company.Revenue = checked(company.Revenue + total);
            company.CostOfGoodsSold = checked(company.CostOfGoodsSold + removed.CostBasis);
            Add(company.Sold, goodId, quantity);
            var kind = account == "external_authority" ? EconomicEventKind.ExternalSale :
                account == "royal_treasury" ? EconomicEventKind.TreasurySale : EconomicEventKind.Sale;
            events.Add(Event(kind, store.OwnerId, store.OwnerId, account,
                storeId, account, portId, goodId, quantity, total, total, removed.CostBasis,
                null, null, portId, new[] { removed }, Array.Empty<EconomicGoodsMovement>()));
        }
        internal void Mint(string storeId, string portId, int quantity, long receipts)
        {
            ValidateGoods("silver", quantity, receipts);
            var store = stores[storeId];
            var company = companies[store.OwnerId];
            var removed = new EconomicGoodsMovement("silver", store.Take("silver", quantity));
            company.MintReceipts = checked(company.MintReceipts + receipts);
            company.MintMaterialCost = checked(company.MintMaterialCost + removed.CostBasis);
            Add(company.Surrendered, "silver", quantity);
            events.Add(Event(EconomicEventKind.MintExchange, store.OwnerId, store.OwnerId, "royal_reserve",
                storeId, "royal_reserve", portId, "silver", quantity, receipts, receipts, removed.CostBasis,
                null, null, "authorized_currency_issuance", new[] { removed }, Array.Empty<EconomicGoodsMovement>()));
        }
        internal void PublicCommission(string ownerId, string portId, long cost)
        {
            if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost));
            companies[ownerId].PublicCommission = checked(companies[ownerId].PublicCommission + cost);
            events.Add(Event(EconomicEventKind.PublicCommission, ownerId, ownerId, "royal_treasury",
                null, null, portId, null, 0, cost, -cost, 0, null, null, "public_mint_commission",
                Array.Empty<EconomicGoodsMovement>(), Array.Empty<EconomicGoodsMovement>()));
        }
        internal void Transfer(string sourceStoreId, string destinationStoreId, string portId, string goodId, int quantity)
        {
            ValidateGoods(goodId, quantity, 0);
            RuntimeIdentifier.Validate(portId, nameof(portId));
            var source = stores[sourceStoreId];
            var destination = stores[destinationStoreId];
            if (sourceStoreId == destinationStoreId) throw new ArgumentException("Transfer stores must differ.");
            if (source.OwnerId != destination.OwnerId) throw new InvalidOperationException("Transfers preserve company ownership.");
            var moved = new EconomicGoodsMovement(goodId, source.Take(goodId, quantity));
            destination.Append(goodId, moved.Lots);
            events.Add(Event(EconomicEventKind.Transfer, source.OwnerId, source.OwnerId, destination.OwnerId,
                sourceStoreId, destinationStoreId, portId, goodId, quantity, 0, 0, moved.CostBasis,
                null, null, "physical_transfer", new[] { moved }, new[] { moved }));
        }
        internal void Produce(string storeId, string portId, RecipeDefinition recipe, int quantityScale = 1)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (quantityScale < 1) throw new ArgumentOutOfRangeException(nameof(quantityScale));
            RuntimeIdentifier.Validate(portId, nameof(portId));
            var store = stores[storeId];
            var company = companies[store.OwnerId];
            var consumed = recipe.Inputs.OrderBy(i => i.GoodId, StringComparer.Ordinal)
                .Select(i => new EconomicGoodsMovement(i.GoodId, store.Take(i.GoodId, checked(i.Quantity * quantityScale)))).ToArray();
            var totalCost = consumed.Aggregate(0L, (sum, value) => checked(sum + value.CostBasis));
            var totalOutput = recipe.Outputs.Aggregate(0L, (sum, output) => checked(sum + (long)output.Quantity * quantityScale));
            var lotId = NextLotId();
            var produced = new List<EconomicGoodsMovement>();
            var offset = 0L;
            foreach (var output in recipe.Outputs.OrderBy(o => o.GoodId, StringComparer.Ordinal))
            {
                var amount = checked(output.Quantity * quantityScale);
                var lot = new InventoryCostLot(lotId, InventoryCostOrigin.Manufactured, recipe.Id,
                    totalOutput, totalCost, offset, amount);
                store.Append(output.GoodId, new[] { lot });
                produced.Add(new EconomicGoodsMovement(output.GoodId, new[] { lot }));
                offset = checked(offset + amount);
                Add(company.Produced, output.GoodId, amount);
            }
            foreach (var input in recipe.Inputs) Add(company.Consumed, input.GoodId, checked(input.Quantity * quantityScale));
            events.Add(Event(EconomicEventKind.Production, store.OwnerId, store.OwnerId, store.OwnerId,
                storeId, storeId, portId, null, totalOutput, 0, 0, totalCost, recipe.Id,
                storeId.StartsWith("facility:", StringComparison.Ordinal) ? storeId.Substring(9) : null,
                recipe.Id, consumed, produced));
        }
        internal void Construct(string ownerId, string facilityId, string portId, long cost)
        {
            RuntimeIdentifier.Validate(facilityId, nameof(facilityId));
            RuntimeIdentifier.Validate(portId, nameof(portId));
            if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost));
            var company = companies[ownerId];
            company.Construction = checked(company.Construction + cost);
            events.Add(Event(EconomicEventKind.Construction, ownerId, ownerId, "market:" + portId,
                null, "facility:" + facilityId, portId, null, 0, cost, -cost, 0, null, facilityId,
                "construction", Array.Empty<EconomicGoodsMovement>(), Array.Empty<EconomicGoodsMovement>()));
        }
        internal void CompleteConstruction(string ownerId, string facilityId, string portId)
        {
            if (stores["facility:" + facilityId].OwnerId != ownerId)
                throw new InvalidOperationException("Construction completion needs its owned facility store.");
            events.Add(Event(EconomicEventKind.ConstructionCompleted, ownerId, ownerId, ownerId,
                "facility:" + facilityId, "facility:" + facilityId, portId, null, 0, 0, 0, 0, null, facilityId,
                "construction_completed", Array.Empty<EconomicGoodsMovement>(), Array.Empty<EconomicGoodsMovement>()));
        }
        internal void PayWage(string ownerId, string facilityId, string portId, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            companies[ownerId].Wages = checked(companies[ownerId].Wages + amount);
            events.Add(Event(EconomicEventKind.Wage, ownerId, ownerId, "households:" + portId,
                "facility:" + facilityId, null, portId, null, 0, amount, -amount, 0, null, facilityId,
                "production_wage_expense", Array.Empty<EconomicGoodsMovement>(), Array.Empty<EconomicGoodsMovement>()));
        }
        internal AccountingSnapshot Snapshot()
        {
            var inventory = stores.Values.Select(s => s.Snapshot()).ToArray();
            return new AccountingSnapshot(companies.Values.Select(c => c.Snapshot(inventory.Where(s => s.OwnerId == c.Id))),
                inventory, nextLotId);
        }
        internal IReadOnlyList<EconomicEvent> Events(long day, long revision)
        {
            if (day < 0 || revision < 0) throw new ArgumentOutOfRangeException(nameof(day));
            return Array.AsReadOnly(events.Select(e => e.AtCommit(day, revision)).ToArray());
        }
        private long NextLotId()
        {
            var id = nextLotId;
            nextLotId = checked(nextLotId + 1);
            return id;
        }
        private InventoryCostLot NewLot(InventoryCostOrigin origin, string provenance, long originalQuantity,
            long cost, long offset, int quantity) =>
            new InventoryCostLot(NextLotId(), origin, provenance, originalQuantity, cost, offset, quantity);
        private static void ValidateGoods(string goodId, int quantity, long total)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (total < 0) throw new ArgumentOutOfRangeException(nameof(total));
        }
        private static void Add(IDictionary<string, long> totals, string id, long quantity) =>
            totals[id] = checked((totals.TryGetValue(id, out var previous) ? previous : 0) + quantity);
        private static EconomicEvent Event(EconomicEventKind kind, string owner, string? sourceOwner,
            string? destinationOwner, string? sourceStore, string? destinationStore, string? port,
            string? good, long quantity, long cash, long delta, long cost, string? recipe, string? facility,
            string provenance, IEnumerable<EconomicGoodsMovement> consumed, IEnumerable<EconomicGoodsMovement> produced) =>
            new EconomicEvent(0, 0, kind, owner, sourceOwner, destinationOwner, sourceStore, destinationStore,
                port, good, quantity, cash, delta, cost, recipe, facility, provenance, consumed, produced);

        private sealed class Store
        {
            internal string Id { get; }
            internal string OwnerId { get; }
            private readonly SortedDictionary<string, List<InventoryCostLot>> goods =
                new SortedDictionary<string, List<InventoryCostLot>>(StringComparer.Ordinal);
            internal Store(string id, string ownerId) { Id = id; OwnerId = ownerId; }
            internal Store(InventoryCostSnapshot before) : this(before.StoreId, before.OwnerId)
            {
                foreach (var good in before.Goods.Values) goods.Add(good.GoodId, good.Lots.ToList());
            }
            internal void Append(string goodId, IEnumerable<InventoryCostLot> additions)
            {
                if (!goods.TryGetValue(goodId, out var lots)) goods.Add(goodId, lots = new List<InventoryCostLot>());
                var incoming = additions.ToArray();
                _ = checked(lots.Aggregate(0, (total, lot) => checked(total + lot.Quantity)) +
                    incoming.Aggregate(0, (total, lot) => checked(total + lot.Quantity)));
                lots.AddRange(incoming);
            }
            internal IReadOnlyList<InventoryCostLot> Take(string goodId, int quantity)
            {
                if (!goods.TryGetValue(goodId, out var lots) ||
                    lots.Aggregate(0L, (sum, lot) => sum + lot.Quantity) < quantity)
                    throw new InvalidOperationException("Accounting cannot remove more goods than the physical inventory owns.");
                var selected = new List<InventoryCostLot>();
                var remaining = quantity;
                var wholeLots = 0;
                foreach (var lot in lots)
                {
                    var amount = Math.Min(lot.Quantity, remaining);
                    selected.Add(lot.Slice(0, amount));
                    remaining -= amount;
                    if (amount == lot.Quantity) wholeLots++;
                    else
                    {
                        lots[wholeLots] = lot.Slice(amount, lot.Quantity - amount);
                        break;
                    }
                    if (remaining == 0) break;
                }
                lots.RemoveRange(0, wholeLots);
                if (lots.Count == 0) goods.Remove(goodId);
                return selected;
            }
            internal InventoryCostSnapshot Snapshot() =>
                new InventoryCostSnapshot(Id, OwnerId, goods.Select(g => new GoodCostSnapshot(g.Key, g.Value)));
        }

        private sealed class Company
        {
            internal string Id { get; }
            internal long InitialCash { get; }
            internal long Purchases, Revenue, CostOfGoodsSold, Construction, Wages, MintReceipts, MintMaterialCost, PublicCommission;
            internal readonly Dictionary<string, long> Surrendered = new Dictionary<string, long>(StringComparer.Ordinal);
            internal readonly Dictionary<string, long> Granted = new Dictionary<string, long>(StringComparer.Ordinal);
            internal readonly Dictionary<string, long> Purchased = new Dictionary<string, long>(StringComparer.Ordinal);
            internal readonly Dictionary<string, long> Sold = new Dictionary<string, long>(StringComparer.Ordinal);
            internal readonly Dictionary<string, long> Produced = new Dictionary<string, long>(StringComparer.Ordinal);
            internal readonly Dictionary<string, long> Consumed = new Dictionary<string, long>(StringComparer.Ordinal);
            internal Company(string id, long initialCash) { Id = id; InitialCash = initialCash; }
            internal Company(CompanyFinancialSnapshot before) : this(before.OwnerId, before.InitialCash)
            {
                Purchases = before.InventoryPurchases; Revenue = before.SalesRevenue;
                CostOfGoodsSold = before.CostOfGoodsSold; Construction = before.ConstructionExpenditure;
                Wages = before.WagesPaid;
                MintReceipts = before.MintReceipts; MintMaterialCost = before.MintMaterialCost;
                PublicCommission = before.PublicCommissionExpenditure;
                foreach (var value in before.SurrenderedQuantities) Surrendered.Add(value.Key, value.Value);
                foreach (var value in before.GrantedQuantities) Granted.Add(value.Key, value.Value);
                foreach (var value in before.PurchasedQuantities) Purchased.Add(value.Key, value.Value);
                foreach (var value in before.SoldQuantities) Sold.Add(value.Key, value.Value);
                foreach (var value in before.ProducedQuantities) Produced.Add(value.Key, value.Value);
                foreach (var value in before.ConsumedQuantities) Consumed.Add(value.Key, value.Value);
            }
            internal CompanyFinancialSnapshot Snapshot(IEnumerable<InventoryCostSnapshot> ownedStores)
            {
                var held = new Dictionary<string, long>(StringComparer.Ordinal);
                var basis = 0L;
                foreach (var store in ownedStores)
                {
                    basis = checked(basis + store.TotalCostBasis);
                    foreach (var good in store.Goods.Values) Add(held, good.GoodId, good.Quantity);
                }
                return new CompanyFinancialSnapshot(Id, InitialCash, Purchases, Revenue, CostOfGoodsSold,
                    Construction, basis, Granted, Purchased, Sold, Produced, Consumed, held, Wages,
                    MintReceipts, MintMaterialCost, PublicCommission, Surrendered);
            }
        }
    }
}
