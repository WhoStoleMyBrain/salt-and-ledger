using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// Scheduled capacity and need over the next twelve whole days, not a stock forecast.
    /// Repeated batches assume success; readiness describes only the current committed state.
    /// Transfers, trade, procurement and sales are deliberately outside this passive flow.
    /// </summary>
    public sealed class CommodityFlowSignal
    {
        public int HorizonDays => 12;
        public long FirstDay { get; }
        public long LastDay { get; }
        public string PortId { get; }
        public string GoodId { get; }
        public string? OwnerId { get; }
        public bool HasStore { get; }
        public long CurrentStock { get; }
        public bool Produces => Production.Any(p => p.OutputPerBatch > 0);
        public bool Uses => Production.Any(p => p.InputPerBatch > 0) || Consumption.Count > 0;
        public long InstalledProductionQuantity { get; }
        public long EnabledProductionQuantity { get; }
        public long InputUsageQuantity { get; }
        public long ConsumptionNeedQuantity { get; }
        public long UsageQuantity => checked(InputUsageQuantity + ConsumptionNeedQuantity);
        public long NetQuantity => checked(EnabledProductionQuantity - UsageQuantity);
        public IReadOnlyList<CommodityProductionFlow> Production { get; }
        public IReadOnlyList<CommodityConsumptionFlow> Consumption { get; }

        internal CommodityFlowSignal(long day, string portId, string goodId, string? ownerId,
            bool hasStore, long stock, IEnumerable<CommodityProductionFlow> production,
            IEnumerable<CommodityConsumptionFlow> consumption)
        {
            FirstDay = checked(day + 1); LastDay = checked(day + HorizonDays);
            PortId = portId; GoodId = goodId; OwnerId = ownerId; HasStore = hasStore; CurrentStock = stock;
            Production = Array.AsReadOnly(production.OrderBy(p => p.Opportunity.FacilityId, StringComparer.Ordinal).ToArray());
            Consumption = Array.AsReadOnly(consumption.OrderBy(c => c.RuleId, StringComparer.Ordinal).ToArray());
            InstalledProductionQuantity = Production.Sum(p => p.InstalledOutputQuantity);
            EnabledProductionQuantity = Production.Where(p => p.IsActive).Sum(p => p.OutputQuantity);
            InputUsageQuantity = Production.Where(p => p.IsActive).Sum(p => p.InputQuantity);
            ConsumptionNeedQuantity = Consumption.Sum(c => c.NeedQuantity);
        }
    }

    /// <summary>Installed selected-good capacity, including disabled and pending sites for explanation.</summary>
    public sealed class CommodityProductionFlow
    {
        public ProductionOpportunitySignal Opportunity { get; }
        public int IntervalDays { get; }
        public long Opportunities { get; }
        public long InstalledOpportunities { get; }
        public long InstalledOutputQuantity { get; }
        public int OutputPerBatch { get; }
        public int InputPerBatch { get; }
        public long OutputQuantity { get; }
        public long InputQuantity { get; }
        public bool IsActive => Opportunity.Enabled && !Opportunity.UnderConstruction && Opportunity.OperatingRate > 0;

        internal CommodityProductionFlow(ProductionOpportunitySignal opportunity, string goodId,
            int intervalDays, long lastDay)
        {
            Opportunity = opportunity; IntervalDays = intervalDays;
            InstalledOpportunities = EconomicSignals.ScheduledOpportunities(opportunity.NextEligibleDay, lastDay, intervalDays);
            Opportunities = FactoryProduction.Opportunities(opportunity.NextEligibleDay, lastDay, intervalDays,
                opportunity.OperatingRate, opportunity.ProductionCredit);
            OutputPerBatch = opportunity.Outputs.Where(o => o.GoodId == goodId).Sum(o => o.Quantity);
            InputPerBatch = opportunity.Inputs.Where(i => i.GoodId == goodId).Sum(i => i.Quantity);
            OutputQuantity = checked(Opportunities * OutputPerBatch);
            InstalledOutputQuantity = checked(InstalledOpportunities * OutputPerBatch);
            InputQuantity = checked(Opportunities * InputPerBatch);
        }
    }

    /// <summary>
    /// Demand keeps current population and luxury eligibility fixed and carries a local copy of
    /// the canonical fractional residual. Last observation and next current-state retail proposal
    /// are separate from the horizon's need; none of these fields promise future purchases.
    /// </summary>
    public sealed class CommodityConsumptionFlow
    {
        public string RuleId { get; }
        public string GoodId { get; }
        public ConsumptionKind Kind { get; }
        public int IntervalDays { get; }
        public long ScheduledDueDay { get; }
        public long NextDueDay { get; }
        public long Opportunities { get; }
        public bool IsEligible { get; }
        public int Population { get; }
        public int DemandRemainder { get; }
        public int NextDemandRemainder { get; }
        public long NeedQuantity { get; }
        public int NextDesiredQuantity { get; }
        public int NextStockShortfall { get; }
        public bool PreviewAvailable { get; }
        public bool NumericCapacityBlocked { get; }
        public int? NextAffordableQuantity { get; }
        public int? NextAffordabilityShortfall { get; }
        public long? NextPayment { get; }
        public bool HasObservation { get; }
        public int LastDesiredQuantity { get; }
        public int LastConsumedQuantity { get; }
        public int LastUnmetStock { get; }
        public int LastUnmetAffordability { get; }
        public long LastPayment { get; }

        internal CommodityConsumptionFlow(RegionalWorldSnapshot snapshot, RegionalPortSnapshot port,
            ConsumptionSnapshot need, long lastDay)
        {
            var rule = need.Configuration;
            RuleId = rule.Id; GoodId = rule.GoodId; Kind = rule.Kind; IntervalDays = rule.IntervalDays;
            ScheduledDueDay = need.NextDueDay; NextDueDay = Math.Max(checked(snapshot.Day + 1), need.NextDueDay);
            Opportunities = EconomicSignals.ScheduledOpportunities(NextDueDay, lastDay, IntervalDays);
            IsEligible = Kind != ConsumptionKind.Luxury || port.City?.LuxuryEnabled == true;
            Population = port.City?.Population ?? 0;
            DemandRemainder = need.DemandRemainder;
            var next = IsEligible ? rule.PreviewDemand(Population, DemandRemainder) :
                new ConsumptionDemand(0, DemandRemainder);
            NextDesiredQuantity = next.Quantity; NextDemandRemainder = next.Remainder;
            NextStockShortfall = Math.Max(0, NextDesiredQuantity - port.Inventory[GoodId]);
            var remainder = DemandRemainder;
            for (var index = 0L; index < Opportunities && IsEligible; index++)
            {
                var demand = rule.PreviewDemand(Population, remainder);
                NeedQuantity = checked(NeedQuantity + demand.Quantity);
                remainder = demand.Remainder;
            }
            HasObservation = need.NextDueDay > rule.FirstDueDay;
            LastDesiredQuantity = need.LastDesiredQuantity; LastConsumedQuantity = need.LastConsumedQuantity;
            LastUnmetStock = need.LastUnmetStock; LastUnmetAffordability = need.LastUnmetAffordability;
            LastPayment = need.LastPayment;
            if (port.City == null) return;
            try
            {
                var preview = snapshot.PreviewHouseholdConsumption(port.Id, RuleId);
                PreviewAvailable = true;
                NextAffordableQuantity = preview.ConsumedQuantity;
                NextAffordabilityShortfall = preview.UnmetAffordability;
                NextPayment = preview.Payment;
            }
            catch (OverflowException)
            {
                // As with NextEssentialNeed, a failed monetary proposal is not household poverty.
                NumericCapacityBlocked = true;
            }
        }
    }

    public static partial class EconomicSignals
    {
        /// <summary>
        /// Public market production/needs, or one company's local industrial stock and batches.
        /// Overdue work gets one next-day opportunity and then its normal interval, never catch-up.
        /// </summary>
        public static CommodityFlowSignal CommodityFlow(RegionalWorldSnapshot snapshot, ContentCatalog content,
            string portId, string goodId, string? ownerId = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (content == null) throw new ArgumentNullException(nameof(content));
            var port = snapshot.Ports[portId];
            var stock = (long)port.Inventory[goodId];
            var lastDay = checked(snapshot.Day + 12);
            var production = new List<CommodityProductionFlow>();
            var consumption = new List<CommodityConsumptionFlow>();
            var hasStore = true;
            if (ownerId == null)
            {
                foreach (var facility in port.Facilities)
                {
                    var signal = PublicProduction(snapshot, content, portId, facility.Configuration.Instance.Id);
                    if (signal.Inputs.Any(i => i.GoodId == goodId) || signal.Outputs.Any(o => o.GoodId == goodId))
                        production.Add(new CommodityProductionFlow(signal, goodId, facility.Configuration.IntervalDays, lastDay));
                }
                consumption.AddRange(port.Consumption.Where(c => c.Configuration.GoodId == goodId)
                    .Select(c => new CommodityConsumptionFlow(snapshot, port, c, lastDay)));
            }
            else
            {
                _ = snapshot.Companies[ownerId];
                var facilities = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == ownerId && f.PortId == portId).ToArray();
                var stores = facilities.Select(f => snapshot.GetFacilityStorage(f.Id)).GroupBy(s => s.Id, StringComparer.Ordinal)
                    .Select(g => g.First()).ToArray();
                hasStore = stores.Length > 0;
                stock = stores.Sum(s => (long)s.Inventory[goodId]);
                foreach (var facility in facilities)
                {
                    var signal = PrivateProduction(snapshot, content, facility.Id);
                    if (signal.Inputs.Any(i => i.GoodId == goodId) || signal.Outputs.Any(o => o.GoodId == goodId))
                        production.Add(new CommodityProductionFlow(signal, goodId, facility.Blueprint.IntervalDays, lastDay));
                }
            }
            return new CommodityFlowSignal(snapshot.Day, portId, goodId, ownerId, hasStore, stock, production, consumption);
        }

        internal static long ScheduledOpportunities(long nextDay, long lastDay, int intervalDays) =>
            nextDay > lastDay ? 0 : checked(1 + (lastDay - nextDay) / intervalDays);
    }
}
