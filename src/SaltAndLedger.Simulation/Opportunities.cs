using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public enum OpportunityKind { BulkProcurement, RecurringSupply, IndustrialSupply, EmergencyRelief, PublicIndustryCommission, TradingDepot }
    public enum OpportunityStatus { Available, Active, Succeeded, Failed, Expired, Declined, Withdrawn }
    public enum OpportunityRewardKind { Cash, CommercialPrivilege, InfrastructureAccess }
    public enum ContractDeliveryOutcome { Delivered, NotActive, WrongPort, Expired, WindowClosed, InsufficientCargo, ExcessQuantity }

    public sealed class OpportunityConfiguration
    {
        public int ActiveContractLimit { get; }
        public int MaximumAvailableContracts { get; }
        public int GenerationIntervalDays { get; }
        public int OfferLifetimeDays { get; }
        public int HistoryLimit { get; }
        public OpportunityConfiguration(int activeContractLimit = 3, int maximumAvailableContracts = 8,
            int generationIntervalDays = 16, int offerLifetimeDays = 24, int historyLimit = 64)
        {
            if (activeContractLimit < 1 || activeContractLimit > 8 || maximumAvailableContracts < 2 || maximumAvailableContracts > 16 ||
                generationIntervalDays < 1 || generationIntervalDays > 365 || offerLifetimeDays < 1 || offerLifetimeDays > 365 ||
                historyLimit < 8 || historyLimit > 128) throw new ArgumentOutOfRangeException(nameof(activeContractLimit));
            ActiveContractLimit = activeContractLimit; MaximumAvailableContracts = maximumAvailableContracts;
            GenerationIntervalDays = generationIntervalDays; OfferLifetimeDays = offerLifetimeDays; HistoryLimit = historyLimit;
        }
    }

    /// <summary>Explicit economic terms and bounded progress. Only staged copies may be changed.</summary>
    public sealed class OpportunitySnapshot
    {
        public string Id { get; internal set; } = "";
        public OpportunityKind Kind { get; internal set; }
        public OpportunityStatus Status { get; internal set; }
        public string PortId { get; internal set; } = "";
        public string? OwnerId { get; internal set; }
        public string GoodId { get; internal set; } = "";
        public int RequiredQuantity { get; internal set; }
        public int DeliveredQuantity { get; internal set; }
        public long UnitPrice { get; internal set; }
        public long CashReward { get; internal set; }
        public long RequiredCash { get; internal set; }
        public long ContributedCash { get; internal set; }
        public long CreatedDay { get; internal set; }
        public long ExpiresDay { get; internal set; }
        public long DeadlineDay { get; internal set; }
        public int WindowDays { get; internal set; }
        public int WindowCount { get; internal set; } = 1;
        public int WindowIndex { get; internal set; }
        public int WindowDelivered { get; internal set; }
        public int EvidenceRequired { get; internal set; }
        public int EvidenceCount { get; internal set; }
        public int ContributionCredit { get; internal set; }
        public long ReservedCash { get; internal set; }
        public long PaidCash { get; internal set; }
        public string? CompetitionGroupId { get; internal set; }
        public string? ExclusiveGroupId { get; internal set; }
        public string? BlueprintId { get; internal set; }
        public string? FacilityId { get; internal set; }
        public int StorageCapacity { get; internal set; }
        public long? CompletedDay { get; internal set; }
        public OpportunityRewardKind RewardKind { get; internal set; }
        public int PrivilegeDays { get; internal set; }
        public long PrivilegeUntilDay { get; internal set; }
        public bool IsProject => Kind == OpportunityKind.PublicIndustryCommission || Kind == OpportunityKind.TradingDepot;
        public bool IsTerminal => Status != OpportunityStatus.Available && Status != OpportunityStatus.Active;
        public long CurrentWindowDeadlineDay => DeadlineDay - (long)WindowDays * (WindowCount - WindowIndex - 1);
        public int RemainingQuantity => RequiredQuantity - DeliveredQuantity;
        public long FundingRequired => checked((long)RequiredQuantity * UnitPrice + CashReward);
        internal OpportunitySnapshot Copy() => (OpportunitySnapshot)MemberwiseClone();
    }

    public sealed class OpportunityBoardSnapshot
    {
        public OpportunityConfiguration Configuration { get; }
        public IReadOnlyList<OpportunitySnapshot> Entries { get; }
        public long NextSequence { get; }
        public long NextGenerationDay { get; }
        public long CumulativePayments { get; }
        public long CumulativeDeliveryPayments { get; }
        public long CumulativeRewards { get; }
        public IReadOnlyDictionary<string, long> ExportedGoods { get; }
        public long ReservedCash => Entries.Aggregate(0L, (sum, entry) => checked(sum + entry.ReservedCash));
        internal OpportunityBoardSnapshot(OpportunityConfiguration configuration, IEnumerable<OpportunitySnapshot> entries,
            long nextSequence = 1, long nextGenerationDay = 0, long cumulativePayments = 0,
            long cumulativeDeliveryPayments = 0, long cumulativeRewards = 0, IDictionary<string, long>? exportedGoods = null)
        {
            Configuration = configuration;
            Entries = Array.AsReadOnly(entries.OrderBy(e => e.Id, StringComparer.Ordinal).Select(e => e.Copy()).ToArray());
            NextSequence = nextSequence; NextGenerationDay = nextGenerationDay; CumulativePayments = cumulativePayments;
            CumulativeDeliveryPayments = cumulativeDeliveryPayments; CumulativeRewards = cumulativeRewards;
            ExportedGoods = new ReadOnlyDictionary<string, long>(new SortedDictionary<string, long>(exportedGoods ?? new Dictionary<string, long>(), StringComparer.Ordinal));
        }
        public bool HasCommercialPrivilege(string ownerId, string portId, long day) => Entries.Any(e => e.OwnerId == ownerId &&
            e.PortId == portId && e.Status == OpportunityStatus.Succeeded && e.RewardKind == OpportunityRewardKind.CommercialPrivilege && day <= e.PrivilegeUntilDay);
        public bool HasInfrastructureAccess(string ownerId, string portId) => Entries.Any(e => !e.IsProject && e.OwnerId == ownerId && e.PortId == portId &&
            e.Status == OpportunityStatus.Succeeded && e.RewardKind == OpportunityRewardKind.InfrastructureAccess);
    }

    public sealed class ContractDeliveryAttempt
    {
        public string OpportunityId { get; }
        public ContractDeliveryOutcome Outcome { get; }
        public int Quantity { get; }
        public long Payment { get; }
        public string Reason { get; }
        public bool Success => Outcome == ContractDeliveryOutcome.Delivered;
        internal ContractDeliveryAttempt(string id, ContractDeliveryOutcome outcome, int quantity, long payment, string reason)
        { OpportunityId = id; Outcome = outcome; Quantity = quantity; Payment = payment; Reason = reason; }
    }
}
