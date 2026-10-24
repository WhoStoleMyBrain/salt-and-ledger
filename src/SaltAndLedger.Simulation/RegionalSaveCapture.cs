using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldState
    {
        internal ContentCatalog SaveContent => content;

        internal RegionalSaveData CaptureSave()
        {
            if (mutating) throw new InvalidOperationException("Cannot save during a regional operation.");
            var identity = state.RunIdentity ?? throw new InvalidOperationException("A save needs a run identity.");
            return new RegionalSaveData
            {
                Identity = new RegionalSaveIdentity { ChallengeId = identity.ChallengeId, CharterId = identity.CharterId,
                    WorldMode = identity.WorldMode, Seed = identity.Seed, GeneratorVersion = identity.GeneratorVersion,
                    Difficulty = identity.Difficulty, PricingMode = identity.LivingPricingMode,
                    ContentVersion = identity.ContentVersion, ValuationVersion = identity.ValuationVersion,
                    Commission = identity.Commission == null ? null : new RegionalSaveCommissionPlan
                    {
                        Seed = identity.Commission.Seed, Version = identity.Commission.Version,
                        Signature = identity.Commission.Signature,
                        Goals = identity.Commission.Goals.Select(g => new RegionalSaveCommissionGoal
                        { Id = g.Id, TemplateId = g.TemplateId, CanonicalParameters = g.CanonicalParameters }).ToArray()
                    } },
                Day = state.Day, Revision = state.Revision,
                Ports = state.Ports.Values.Select(port => new RegionalSavePort
                {
                    Id = port.Id, Cash = port.MarketCash, Inventory = Copy(port.Inventory),
                    Quotes = port.Quotes.Values.Select(q => new RegionalSaveQuote { GoodId = q.GoodId,
                        Bid = q.IsFixed ? q.Bid : 0, Ask = q.IsFixed ? q.Ask : 0, Pricing = q.Pricing }).ToArray(),
                    Facilities = port.Facilities.Select(f => new RegionalSaveSchedule { Id = f.Configuration.Instance.Id,
                        Enabled = f.Enabled, NextDueDay = f.NextDueDay, LastOutcome = f.LastOutcome }).ToArray(),
                    Consumption = port.Consumption.Select(c => new RegionalSaveConsumption
                    {
                        Id = c.Configuration.Id, NextDueDay = c.NextDueDay, LastUnmet = c.LastUnmetQuantity,
                        TotalUnmet = c.TotalUnmetQuantity, LastDesired = c.LastDesiredQuantity,
                        LastConsumed = c.LastConsumedQuantity, LastPayment = c.LastPayment, LastStock = c.LastUnmetStock,
                        LastAffordability = c.LastUnmetAffordability, TotalDesired = c.TotalDesiredQuantity,
                        TotalConsumed = c.TotalConsumedQuantity, TotalPayment = c.TotalPayment,
                        TotalStock = c.TotalUnmetStock, TotalAffordability = c.TotalUnmetAffordability,
                        DemandRemainder = c.DemandRemainder, BaselineDemandRemainder = c.BaselineDemandRemainder,
                        LastBaselineDesired = c.LastBaselineDesiredQuantity
                    }).ToArray(),
                    City = port.City == null ? null : CaptureCity(port.City)
                }).ToArray(),
                Ships = state.Ships.Values.Select(s => new RegionalSaveShip
                {
                    Id = s.Id, Cargo = Copy(s.Cargo), Dock = s.DockedPortId, Origin = s.OriginPortId,
                    Destination = s.DestinationPortId, RemainingDays = s.RemainingTravelDays,
                    VoyageDays = s.VoyageDurationDays, Policy = s.Policy, IndustrialPolicy = s.IndustrialPolicy,
                    IndustrialStage = s.IndustrialStage, AutomationEnabled = s.AutomationEnabled
                }).ToArray(),
                Companies = state.Companies.Values.Select(company =>
                {
                    var f = state.Accounting.Companies[company.Id];
                    return new RegionalSaveCompany
                    {
                        Id = company.Id, Cash = company.Cash, InitialCash = f.InitialCash,
                        Purchases = f.InventoryPurchases, Revenue = f.SalesRevenue, CostOfGoodsSold = f.CostOfGoodsSold,
                        Construction = f.ConstructionExpenditure, Wages = f.WagesPaid,
                        MintReceipts = f.MintReceipts, MintMaterialCost = f.MintMaterialCost,
                        PublicCommissionExpenditure = f.PublicCommissionExpenditure,
                        SurrenderedQuantities = Copy(f.SurrenderedQuantities),
                        Granted = Copy(f.GrantedQuantities), Purchased = Copy(f.PurchasedQuantities), Sold = Copy(f.SoldQuantities),
                        Produced = Copy(f.ProducedQuantities), Consumed = Copy(f.ConsumedQuantities)
                    };
                }).ToArray(),
                Facilities = state.PrivateFacilities.Values.Select(f => new RegionalSaveFacility
                {
                    Id = f.Id, OwnerId = f.OwnerId, PortId = f.PortId, BlueprintId = f.Blueprint.Id, Policy = f.Policy,
                    Enabled = f.Enabled, CreatedDay = f.CreatedDay, CompletionDay = f.ConstructionCompletionDay,
                    ConstructionStatus = f.ConstructionStatus, NextProductionDay = f.NextProductionDay,
                    LastOutcome = f.LastOutcome, Inventory = Copy(f.Inventory), Batches = f.BatchesProduced,
                    Procurement = f.ProcurementCost, Revenue = f.SalesRevenue, Wages = f.WagesPaid,
                    Produced = Copy(f.ProducedQuantities), Consumed = Copy(f.ConsumedQuantities),
                    ProcurementWait = f.ProcurementWaitReason, SalesWait = f.SalesWaitReason,
                    StarterGrant = f.IsStarterGrant, Provenance = f.Provenance
                }).ToArray(),
                Stores = state.Accounting.Stores.Values.Select(s => new RegionalSaveStore
                {
                    Id = s.StoreId, OwnerId = s.OwnerId, Goods = s.Goods.Values.Select(g => new RegionalSaveGoodLots
                    {
                        GoodId = g.GoodId, Lots = g.Lots.Select(l => new RegionalSaveLot
                        {
                            Id = l.LotId, Origin = l.Origin, Provenance = l.Provenance,
                            OriginalQuantity = l.OriginalQuantity, OriginalCost = l.OriginalCost,
                            Start = l.Start, Quantity = l.Quantity
                        }).ToArray()
                    }).ToArray()
                }).ToArray(),
                NextLotId = state.Accounting.NextLotId,
                Economy = state.Economy == null ? null : CaptureEconomy(state.Economy),
                CommissionHistory = state.CommissionHistory == null ? null : new RegionalSaveCommissionHistory
                {
                    Ships = state.CommissionHistory.Ships.Select(s => new RegionalSaveCommissionShip
                    { ShipId = s.ShipId, AcquisitionOrder = s.AcquisitionOrder, Visits = Copy(s.Visits) }).ToArray(),
                    Markets = state.CommissionHistory.Markets.Select(m => new RegionalSaveCommissionMarket
                    { PortId = m.PortId, GoodId = m.GoodId, PurchasedQuantity = m.PurchasedQuantity,
                        SoldQuantity = m.SoldQuantity }).ToArray()
                },
                Challenge = state.Challenge == null ? null : new RegionalSaveChallenge
                {
                    NetDelivered = state.Challenge.NetDeliveredQuantity, Status = state.Challenge.Status,
                    CompletedDay = state.Challenge.CompletedDay, RegionalConsumed = state.Challenge.RegionalConsumedQuantity,
                    Produced = state.Challenge.ProducedQuantity, Margin = state.Challenge.RealizedGrossMargin
                }
            };
        }

        private static Dictionary<string, T> Copy<T>(IReadOnlyDictionary<string, T> values) =>
            values.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

        private static RegionalSaveCity CaptureCity(CitySnapshot c) => new RegionalSaveCity
        {
            Population = c.Population, PopulationDelta = c.PopulationDelta, Cash = c.HouseholdCash,
            Prosperity = c.Prosperity, Reason = c.ProsperityReason, Luxury = c.LuxuryEnabled,
            PurchasingPower = c.PurchasingPowerPercent, Wages = c.TotalWagesReceived, LastWages = c.LastWagesReceived,
            Retail = c.TotalRetailSpend, LastRetail = c.LastRetailSpend, LastDemographicDay = c.LastDemographicDay,
            Healthy = c.HealthyObservations, Deprived = c.DeprivedObservations,
            LuxuryReady = c.LuxuryReadyObservations, LuxuryPoor = c.LuxuryPoorObservations,
            Food = c.FoodWindow.Select(n => new RegionalSaveObservation { Day = n.Day, Desired = n.DesiredQuantity,
                Consumed = n.ConsumedQuantity, Baseline = n.BaselineDesiredQuantity, Stock = n.UnmetStock,
                Affordability = n.UnmetAffordability, Payment = n.Payment }).ToArray()
        };

        private static RegionalSaveEconomy CaptureEconomy(ExpandingEconomySnapshot e) => new RegionalSaveEconomy
        {
            TreasuryCash = e.TreasuryCash, ExternalCash = e.ExternalCash, MintedCurrency = e.MintedCurrency,
            ExternalNetInflow = e.ExternalNetInflow, MintStatus = e.MintStatus, MintCommissionerId = e.MintCommissionerId,
            MintOrderedDay = e.MintOrderedDay, MintCompletionDay = e.MintCompletionDay, NextMintDay = e.NextMintDay,
            MintBatches = e.MintBatches, SilverReserve = e.SilverReserve, SupplierIssuance = e.SupplierIssuance,
            TreasuryIssuance = e.TreasuryIssuance, TreasuryPlanks = e.TreasuryPlanks,
            TreasuryExpenditure = e.TreasuryExpenditure, NextShipNumber = e.NextShipNumber,
            ShipOrders = e.ShipOrders.Select(o => new RegionalSaveShipOrder { ShipId = o.ShipId,
                OwnerId = o.OwnerId, PortId = o.PortId, OrderedDay = o.OrderedDay,
                CompletionDay = o.CompletionDay, PaidCapital = o.PaidCapital }).ToArray(),
            Contracts = e.Contracts.Select(c => new RegionalSaveContract { Id = c.Configuration.Id,
                Delivered = c.DeliveredQuantity, Paid = c.AmountPaid }).ToArray()
        };
    }
}
