using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SaltAndLedger.Simulation
{
    // Save schema 1 deliberately names every mutable field. Canonical configuration is rebuilt
    // from identity and verified by fingerprint; it is never accepted as executable save data.
    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveData
    {
        public int SchemaVersion { get; set; }
        public string ContentFingerprint { get; set; } = "";
        public string ConfigurationFingerprint { get; set; } = "";
        public string StateFingerprint { get; set; } = "";
        public RegionalSaveIdentity Identity { get; set; } = null!;
        public long Day { get; set; }
        public long Revision { get; set; }
        public RegionalSavePort[] Ports { get; set; } = null!;
        public RegionalSaveShip[] Ships { get; set; } = null!;
        public RegionalSaveCompany[] Companies { get; set; } = null!;
        public RegionalSaveFacility[] Facilities { get; set; } = null!;
        public RegionalSaveStore[] Stores { get; set; } = null!;
        public long NextLotId { get; set; }
        public RegionalSaveChallenge? Challenge { get; set; }
        public RegionalSaveEconomy? Economy { get; set; }
        [JsonProperty(Required = Required.Default)]
        public RegionalSaveCommissionHistory? CommissionHistory { get; set; }
        public bool ShouldSerializeCommissionHistory() => CommissionHistory != null;
        [JsonProperty(Required = Required.Default)]
        public RegionalSaveCompanyStockpile[]? CompanyStockpiles { get; set; }
        public bool ShouldSerializeCompanyStockpiles() => CompanyStockpiles != null;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveIdentity
    {
        public string? ChallengeId { get; set; }
        public string CharterId { get; set; } = "";
        public RunWorldMode WorldMode { get; set; }
        public int? Seed { get; set; }
        public int GeneratorVersion { get; set; }
        public RunDifficulty Difficulty { get; set; }
        public LivingPricingMode PricingMode { get; set; }
        public int ContentVersion { get; set; }
        public int ValuationVersion { get; set; }
        [JsonProperty(Required = Required.Default)]
        public RegionalSaveCommissionPlan? Commission { get; set; }
        public bool ShouldSerializeCommission() => Commission != null;
        [JsonProperty(Required = Required.Default)]
        public CompanyStorageRules CompanyStorageRules { get; set; } = CompanyStorageRules.Legacy;
        public bool ShouldSerializeCompanyStorageRules() => CompanyStorageRules != CompanyStorageRules.Legacy;
        [JsonProperty(Required = Required.Default)]
        public int ItineraryVersion { get; set; }
        public bool ShouldSerializeItineraryVersion() => ItineraryVersion != 0;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCommissionPlan
    {
        public int Seed { get; set; }
        public int Version { get; set; }
        public string Signature { get; set; } = "";
        public RegionalSaveCommissionGoal[] Goals { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCommissionGoal
    {
        public string Id { get; set; } = "";
        public string TemplateId { get; set; } = "";
        // Versioned, invariant, typed rule/role/port/good/target parameters, never display prose.
        public string CanonicalParameters { get; set; } = "";
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCommissionHistory
    {
        public RegionalSaveCommissionShip[] Ships { get; set; } = null!;
        public RegionalSaveCommissionMarket[] Markets { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCommissionShip
    {
        public string ShipId { get; set; } = "";
        public int AcquisitionOrder { get; set; }
        public Dictionary<string, long> Visits { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCommissionMarket
    {
        public string PortId { get; set; } = "";
        public string GoodId { get; set; } = "";
        public long PurchasedQuantity { get; set; }
        public long SoldQuantity { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSavePort
    {
        public string Id { get; set; } = "";
        public long Cash { get; set; }
        public Dictionary<string, int> Inventory { get; set; } = null!;
        public RegionalSaveQuote[] Quotes { get; set; } = null!;
        public RegionalSaveSchedule[] Facilities { get; set; } = null!;
        public RegionalSaveConsumption[] Consumption { get; set; } = null!;
        public RegionalSaveCity? City { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveQuote
    {
        public string GoodId { get; set; } = "";
        public long Bid { get; set; }
        public long Ask { get; set; }
        public InventoryPricing? Pricing { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveSchedule
    {
        public string Id { get; set; } = "";
        public bool Enabled { get; set; }
        public long NextDueDay { get; set; }
        public ScheduledProductionOutcome LastOutcome { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveConsumption
    {
        public string Id { get; set; } = "";
        public long NextDueDay { get; set; }
        public int LastUnmet { get; set; }
        public long TotalUnmet { get; set; }
        public int LastDesired { get; set; }
        public int LastConsumed { get; set; }
        public long LastPayment { get; set; }
        public int LastStock { get; set; }
        public int LastAffordability { get; set; }
        public long TotalDesired { get; set; }
        public long TotalConsumed { get; set; }
        public long TotalPayment { get; set; }
        public long TotalStock { get; set; }
        public long TotalAffordability { get; set; }
        public int DemandRemainder { get; set; }
        public int BaselineDemandRemainder { get; set; }
        public int LastBaselineDesired { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCity
    {
        public int Population { get; set; }
        public int PopulationDelta { get; set; }
        public long Cash { get; set; }
        public int Prosperity { get; set; }
        public string Reason { get; set; } = "";
        public bool Luxury { get; set; }
        public int PurchasingPower { get; set; }
        public long Wages { get; set; }
        public long LastWages { get; set; }
        public long Retail { get; set; }
        public long LastRetail { get; set; }
        public long LastDemographicDay { get; set; }
        public int Healthy { get; set; }
        public int Deprived { get; set; }
        public int LuxuryReady { get; set; }
        public int LuxuryPoor { get; set; }
        public RegionalSaveObservation[] Food { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveObservation
    {
        public long Day { get; set; }
        public int Desired { get; set; }
        public int Consumed { get; set; }
        public int Baseline { get; set; }
        public int Stock { get; set; }
        public int Affordability { get; set; }
        public long Payment { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveShip
    {
        public string Id { get; set; } = "";
        public Dictionary<string, int> Cargo { get; set; } = null!;
        public string? Dock { get; set; }
        public string? Origin { get; set; }
        public string? Destination { get; set; }
        public int RemainingDays { get; set; }
        public int VoyageDays { get; set; }
        public TradeRoutePolicy? Policy { get; set; }
        public IndustrialRoutePolicy? IndustrialPolicy { get; set; }
        public IndustrialRouteStage IndustrialStage { get; set; }
        public bool AutomationEnabled { get; set; }
        [JsonProperty(Required = Required.Default)]
        public RegionalSaveItinerary? Itinerary { get; set; }
        public bool ShouldSerializeItinerary() => Itinerary != null;
        [JsonProperty(Required = Required.Default)]
        public RegionalSaveItineraryProgress? ItineraryProgress { get; set; }
        public bool ShouldSerializeItineraryProgress() => ItineraryProgress != null;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveItinerary
    {
        public string Id { get; set; } = "";
        public int Version { get; set; }
        public RegionalSaveItineraryStop[] Stops { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveItineraryStop
    {
        public string PortId { get; set; } = "";
        public RegionalSaveItineraryCommand[] Commands { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveItineraryCommand
    {
        public int Kind { get; set; }
        public string GoodId { get; set; } = "";
        public int MaximumQuantity { get; set; }
        public int MinimumQuantity { get; set; }
        public int ShortagePolicy { get; set; }
        public long MarginalPriceLimit { get; set; }
        public long CashReserve { get; set; }
        public long MaximumExpenditure { get; set; }
        public int WarehouseReserve { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveItineraryProgress
    {
        public int StopIndex { get; set; }
        public int ActionIndex { get; set; }
        public long Cycle { get; set; }
        public int LastDecision { get; set; }
        public int LastReason { get; set; }
        public int LastQuantity { get; set; }
        public long OperationsDay { get; set; }
        public int OperationsUsed { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCompany
    {
        public string Id { get; set; } = "";
        public long Cash { get; set; }
        public long InitialCash { get; set; }
        public long Purchases { get; set; }
        public long Revenue { get; set; }
        public long CostOfGoodsSold { get; set; }
        public long Construction { get; set; }
        public long Wages { get; set; }
        public long MintReceipts { get; set; }
        public long MintMaterialCost { get; set; }
        public long PublicCommissionExpenditure { get; set; }
        public Dictionary<string, long> SurrenderedQuantities { get; set; } = null!;
        public Dictionary<string, long> Granted { get; set; } = null!;
        public Dictionary<string, long> Purchased { get; set; } = null!;
        public Dictionary<string, long> Sold { get; set; } = null!;
        public Dictionary<string, long> Produced { get; set; } = null!;
        public Dictionary<string, long> Consumed { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveFacility
    {
        public string Id { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public string PortId { get; set; } = "";
        public string BlueprintId { get; set; } = "";
        public IndustryPolicy Policy { get; set; } = null!;
        public bool Enabled { get; set; }
        public long CreatedDay { get; set; }
        public long CompletionDay { get; set; }
        public FacilityConstructionStatus ConstructionStatus { get; set; }
        public long NextProductionDay { get; set; }
        public ScheduledProductionOutcome LastOutcome { get; set; }
        public Dictionary<string, int> Inventory { get; set; } = null!;
        public long Batches { get; set; }
        public long Procurement { get; set; }
        public long Revenue { get; set; }
        public long Wages { get; set; }
        public Dictionary<string, long> Produced { get; set; } = null!;
        public Dictionary<string, long> Consumed { get; set; } = null!;
        public IndustryWaitReason ProcurementWait { get; set; }
        public IndustryWaitReason SalesWait { get; set; }
        public bool StarterGrant { get; set; }
        public string Provenance { get; set; } = "";
        [JsonProperty(Required = Required.Default)]
        public string? SharedStockpileId { get; set; }
        public bool ShouldSerializeSharedStockpileId() => SharedStockpileId != null;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveCompanyStockpile
    {
        public string Id { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public string PortId { get; set; } = "";
        public int Capacity { get; set; }
        public Dictionary<string, int> Inventory { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveStore
    {
        public string Id { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public RegionalSaveGoodLots[] Goods { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveGoodLots
    {
        public string GoodId { get; set; } = "";
        public RegionalSaveLot[] Lots { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveLot
    {
        public long Id { get; set; }
        public InventoryCostOrigin Origin { get; set; }
        public string Provenance { get; set; } = "";
        public long OriginalQuantity { get; set; }
        public long OriginalCost { get; set; }
        public long Start { get; set; }
        public int Quantity { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveChallenge
    {
        public long NetDelivered { get; set; }
        public ChallengeStatus Status { get; set; }
        public long? CompletedDay { get; set; }
        public long RegionalConsumed { get; set; }
        public long Produced { get; set; }
        public long Margin { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveEconomy
    {
        public long TreasuryCash { get; set; }
        public long ExternalCash { get; set; }
        public long MintedCurrency { get; set; }
        public long ExternalNetInflow { get; set; }
        public MintStatus MintStatus { get; set; }
        public string? MintCommissionerId { get; set; }
        public long? MintOrderedDay { get; set; }
        public long? MintCompletionDay { get; set; }
        public long NextMintDay { get; set; }
        public int MintBatches { get; set; }
        public long SilverReserve { get; set; }
        public long SupplierIssuance { get; set; }
        public long TreasuryIssuance { get; set; }
        public int TreasuryPlanks { get; set; }
        public long TreasuryExpenditure { get; set; }
        public int NextShipNumber { get; set; }
        public RegionalSaveShipOrder[] ShipOrders { get; set; } = null!;
        public RegionalSaveContract[] Contracts { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveShipOrder
    {
        public string ShipId { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public string PortId { get; set; } = "";
        public long OrderedDay { get; set; }
        public long CompletionDay { get; set; }
        public long PaidCapital { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveContract
    {
        public string Id { get; set; } = "";
        public int Delivered { get; set; }
        public long Paid { get; set; }
    }
}
