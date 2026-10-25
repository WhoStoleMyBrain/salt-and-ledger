using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public enum MintStatus { Uncommissioned, UnderConstruction, Active }
    public enum DevelopmentKind { MintCommission, MintActivation, MintExchange, ExternalExport, TreasuryProcurement, ShipCommission, ShipCompletion }
    public enum DevelopmentOutcome { Completed, Started, InsufficientFunds, InsufficientStock, NotReady, Exhausted, Expired, FleetLimit }

    public sealed class ExternalContractConfiguration
    {
        public string Id { get; }
        public string PortId { get; }
        public string GoodId { get; }
        public int Quantity { get; }
        public long UnitPrice { get; }
        public long OpensDay { get; }
        public long DeadlineDay { get; }
        public ExternalContractConfiguration(string id, string portId, string goodId, int quantity,
            long unitPrice, long opensDay, long deadlineDay)
        {
            RuntimeIdentifier.Validate(id, nameof(id)); RuntimeIdentifier.Validate(portId, nameof(portId));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (quantity <= 0 || quantity % 10000 != 0 || unitPrice <= 0 || opensDay < 0 || deadlineDay < opensDay)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            _ = checked(quantity * unitPrice);
            Id = id; PortId = portId; GoodId = goodId; Quantity = quantity;
            UnitPrice = unitPrice; OpensDay = opensDay; DeadlineDay = deadlineDay;
        }
    }

    /// <summary>Authored v3 public institutions and stable reference values. Amounts are raw subunits.</summary>
    public sealed class ExpandingEconomyConfiguration
    {
        public const int RulesetVersion = 3;
        public const int ContentVersion = 3;
        public const int ValuationVersion = 1;
        public string MintPortId { get; }
        public string SilverPortId { get; }
        public string ShipwrightPortId => MintPortId;
        public long InitialTreasuryCash { get; }
        public long InitialExternalCash { get; }
        public long MintCommissionCost => 4000000;
        public int MintConstructionDays => 4;
        public int MintIntervalDays => 4;
        public int SilverPerBatch => 10000;
        public long CurrencyPerBatch => 800000;
        public long SupplierSharePerBatch => 600000;
        public long TreasurySharePerBatch => CurrencyPerBatch - SupplierSharePerBatch;
        public int MaximumMintBatches => 24;
        public long ShipCost => 5000000;
        public long ShipLaborPayment => 400000;
        public int ShipPlanks => 40000;
        public int ShipConstructionDays => 4;
        public int ShipCapacity => 100000;
        public int ShipSpeed => 100;
        public int MaximumPlayerShips => 3;
        public int TreasuryPlankRequirement => 200000;
        public long TreasuryPlankPrice => 25;
        public IReadOnlyDictionary<string, long> GoodsReferencePrices { get; }
        public IReadOnlyList<ExternalContractConfiguration> Contracts { get; }

        public ExpandingEconomyConfiguration(string mintPortId, string silverPortId,
            IEnumerable<ExternalContractConfiguration> contracts, IReadOnlyDictionary<string, long> goodsReferencePrices,
            long initialExternalCash = 12000000, long initialTreasuryCash = 0)
        {
            RuntimeIdentifier.Validate(mintPortId, nameof(mintPortId)); RuntimeIdentifier.Validate(silverPortId, nameof(silverPortId));
            if (mintPortId == silverPortId) throw new ArgumentException("Silver and the Mint require separate ports.");
            if (initialExternalCash < 0 || initialTreasuryCash < 0) throw new ArgumentOutOfRangeException(nameof(initialExternalCash));
            var values = contracts?.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray() ?? throw new ArgumentNullException(nameof(contracts));
            if (values.Length > 16 || values.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("Contracts must be bounded and uniquely identified.");
            if (goodsReferencePrices == null || goodsReferencePrices.Count == 0 || goodsReferencePrices.Any(p => p.Value <= 0))
                throw new ArgumentException("Positive reference values are required.", nameof(goodsReferencePrices));
            MintPortId = mintPortId; SilverPortId = silverPortId;
            InitialExternalCash = initialExternalCash; InitialTreasuryCash = initialTreasuryCash;
            Contracts = Array.AsReadOnly(values);
            GoodsReferencePrices = AccountingCollections.Dictionary(goodsReferencePrices.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        }

        public static ExpandingEconomyConfiguration CreateDefault(string mintPortId, string silverPortId) =>
            new ExpandingEconomyConfiguration(mintPortId, silverPortId, new[]
            {
                new ExternalContractConfiguration("royal_planks", mintPortId, "planks", 100000, 25, 0, 120),
                new ExternalContractConfiguration("royal_furniture", mintPortId, "furniture", 40000, 100, 0, 180),
                new ExternalContractConfiguration("royal_flour", mintPortId, "flour", 120000, 20, 0, 240)
            }, new Dictionary<string, long>(StringComparer.Ordinal)
            { ["grain"] = 6, ["logs"] = 10, ["flour"] = 15, ["planks"] = 20, ["bread"] = 30, ["furniture"] = 80, ["silver"] = 40 });
    }

    public sealed class ExternalContractSnapshot
    {
        public ExternalContractConfiguration Configuration { get; }
        public int DeliveredQuantity { get; }
        public int RemainingQuantity => Configuration.Quantity - DeliveredQuantity;
        public long AmountPaid { get; }
        internal ExternalContractSnapshot(ExternalContractConfiguration configuration, int delivered, long paid)
        { Configuration = configuration; DeliveredQuantity = delivered; AmountPaid = paid; }
    }

    public sealed class ShipConstructionOrder
    {
        public string ShipId { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public long OrderedDay { get; }
        public long CompletionDay { get; }
        public long PaidCapital { get; }
        internal ShipConstructionOrder(string shipId, string ownerId, string portId, long orderedDay, long completionDay, long paidCapital)
        { ShipId = shipId; OwnerId = ownerId; PortId = portId; OrderedDay = orderedDay; CompletionDay = completionDay; PaidCapital = paidCapital; }
    }

    public sealed class ExpandingEconomySnapshot
    {
        public ExpandingEconomyConfiguration Configuration { get; }
        public long TreasuryCash { get; }
        public long ExternalCash { get; }
        public long MintedCurrency { get; }
        public long ExternalNetInflow { get; }
        public MintStatus MintStatus { get; }
        public string? MintCommissionerId { get; }
        public long? MintOrderedDay { get; }
        public long? MintCompletionDay { get; }
        public long NextMintDay { get; }
        public int MintBatches { get; }
        public long SilverReserve { get; }
        public long SupplierIssuance { get; }
        public long TreasuryIssuance { get; }
        public int TreasuryPlanks { get; }
        public long TreasuryExpenditure { get; }
        public int NextShipNumber { get; }
        public IReadOnlyList<ShipConstructionOrder> ShipOrders { get; }
        public IReadOnlyList<ExternalContractSnapshot> Contracts { get; }
        public IReadOnlyDictionary<string, long> InitialCompanyValues { get; }
        internal ExpandingEconomySnapshot(ExpandingEconomyConfiguration configuration, long treasury, long external,
            long minted, long externalInflow, MintStatus status, string? commissioner, long? ordered, long? completion,
            long nextMint, int batches, long silver, long supplierIssuance, long treasuryIssuance,
            int treasuryPlanks, long treasurySpent, int nextShipNumber, IEnumerable<ShipConstructionOrder> shipOrders,
            IEnumerable<ExternalContractSnapshot> contracts, IDictionary<string, long> initialCompanyValues)
        {
            Configuration = configuration; TreasuryCash = treasury; ExternalCash = external; MintedCurrency = minted;
            ExternalNetInflow = externalInflow; MintStatus = status; MintCommissionerId = commissioner;
            MintOrderedDay = ordered; MintCompletionDay = completion; NextMintDay = nextMint; MintBatches = batches;
            SilverReserve = silver; SupplierIssuance = supplierIssuance; TreasuryIssuance = treasuryIssuance;
            TreasuryPlanks = treasuryPlanks; TreasuryExpenditure = treasurySpent; NextShipNumber = nextShipNumber;
            ShipOrders = Array.AsReadOnly(shipOrders.OrderBy(s => s.ShipId, StringComparer.Ordinal).ToArray());
            Contracts = Array.AsReadOnly(contracts.OrderBy(c => c.Configuration.Id, StringComparer.Ordinal).ToArray());
            InitialCompanyValues = AccountingCollections.Dictionary(initialCompanyValues);
        }
        internal static ExpandingEconomySnapshot Initial(ExpandingEconomyConfiguration configuration) =>
            new ExpandingEconomySnapshot(configuration, configuration.InitialTreasuryCash, configuration.InitialExternalCash,
                0, 0, MintStatus.Uncommissioned, null, null, null, 0, 0, 0, 0, 0, 0, 0, 2,
                Array.Empty<ShipConstructionOrder>(), configuration.Contracts.Select(c => new ExternalContractSnapshot(c, 0, 0)),
                new Dictionary<string, long>());
    }

    public sealed class DevelopmentAttempt
    {
        public DevelopmentKind Kind { get; }
        public DevelopmentOutcome Outcome { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public string? AssetId { get; }
        public string? GoodId { get; }
        public int Quantity { get; }
        public long Payment { get; }
        public long TreasuryAmount { get; }
        public long HouseholdAmount { get; }
        public long? CompletionDay { get; }
        public string Reason { get; }
        internal DevelopmentAttempt(DevelopmentKind kind, DevelopmentOutcome outcome, string owner, string port,
            string? asset, string? good, int quantity, long payment, long treasury, long household, long? completion, string reason)
        { Kind = kind; Outcome = outcome; OwnerId = owner; PortId = port; AssetId = asset; GoodId = good; Quantity = quantity;
            Payment = payment; TreasuryAmount = treasury; HouseholdAmount = household; CompletionDay = completion; Reason = reason; }
    }

    public sealed class CompanyValueSnapshot
    {
        public long Cash { get; }
        public long Goods { get; }
        public long Ships { get; }
        public long Facilities { get; }
        public long PendingCapital { get; }
        public long Total { get; }
        public long InitialValue { get; }
        public long Change => checked(Total - InitialValue);
        internal CompanyValueSnapshot(long cash, long goods, long ships, long facilities, long pending, long initial)
        { Cash = cash; Goods = goods; Ships = ships; Facilities = facilities; PendingCapital = pending;
            Total = checked(cash + goods + ships + facilities + pending); InitialValue = initial; }
    }

    public sealed partial class RegionalWorldSnapshot
    {
        public CompanyValueSnapshot CompanyValue(string ownerId)
        {
            var economy = Economy ?? throw new InvalidOperationException("Reference Company Value belongs to economic v3.");
            BigInteger goods = 0, ships = 0, facilities = 0, pending = 0;
            foreach (var ship in Ships.Values.Where(s => s.OwnerId == ownerId))
            {
                ships += (BigInteger)ship.CargoCapacity * 50;
                foreach (var stock in ship.Cargo) goods += (BigInteger)stock.Value * economy.Configuration.GoodsReferencePrices[stock.Key];
            }
            foreach (var facility in PrivateFacilities.Values.Where(f => f.OwnerId == ownerId))
            {
                foreach (var stock in facility.Inventory) goods += (BigInteger)stock.Value * economy.Configuration.GoodsReferencePrices[stock.Key];
                if (facility.ConstructionStatus == FacilityConstructionStatus.UnderConstruction) pending += facility.Blueprint.ConstructionCost;
                else facilities += facility.Blueprint.ConstructionCost;
            }
            foreach (var store in CompanyStockpiles.Values.Where(s => s.OwnerId == ownerId))
                foreach (var stock in store.Inventory) goods += (BigInteger)stock.Value * economy.Configuration.GoodsReferencePrices[stock.Key];
            foreach (var order in economy.ShipOrders.Where(o => o.OwnerId == ownerId)) pending += order.PaidCapital;
            economy.InitialCompanyValues.TryGetValue(ownerId, out var initial);
            return new CompanyValueSnapshot(Companies[ownerId].Cash, checked((long)goods), checked((long)ships),
                checked((long)facilities), checked((long)pending), initial);
        }
    }
}
