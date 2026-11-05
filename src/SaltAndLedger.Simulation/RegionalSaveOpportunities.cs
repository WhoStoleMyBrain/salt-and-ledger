using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SaltAndLedger.Simulation
{
    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveOpportunities
    {
        public long NextSequence { get; set; }
        public long NextGenerationDay { get; set; }
        public long CumulativePayments { get; set; }
        public long CumulativeDeliveryPayments { get; set; }
        public long CumulativeRewards { get; set; }
        public Dictionary<string, long> ExportedGoods { get; set; } = null!;
        public RegionalSaveOpportunity[] Entries { get; set; } = null!;
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class RegionalSaveOpportunity
    {
        public string Id { get; set; } = "";
        public int Kind { get; set; }
        public int Status { get; set; }
        public string PortId { get; set; } = "";
        public string? OwnerId { get; set; }
        public string GoodId { get; set; } = "";
        public int RequiredQuantity { get; set; }
        public int DeliveredQuantity { get; set; }
        public long UnitPrice { get; set; }
        public long CashReward { get; set; }
        public long RequiredCash { get; set; }
        public long ContributedCash { get; set; }
        public long CreatedDay { get; set; }
        public long ExpiresDay { get; set; }
        public long DeadlineDay { get; set; }
        public int WindowDays { get; set; }
        public int WindowCount { get; set; }
        public int WindowIndex { get; set; }
        public int WindowDelivered { get; set; }
        public int EvidenceRequired { get; set; }
        public int EvidenceCount { get; set; }
        public int ContributionCredit { get; set; }
        public long ReservedCash { get; set; }
        public long PaidCash { get; set; }
        public string? CompetitionGroupId { get; set; }
        public string? ExclusiveGroupId { get; set; }
        public string? BlueprintId { get; set; }
        public string? FacilityId { get; set; }
        public int StorageCapacity { get; set; }
        public long? CompletedDay { get; set; }
        public int RewardKind { get; set; }
        public int PrivilegeDays { get; set; }
        public long PrivilegeUntilDay { get; set; }
    }
    public sealed partial class RegionalWorldState
    {
        private static RegionalSaveOpportunities CaptureOpportunities(OpportunityBoardSnapshot board) =>
            new RegionalSaveOpportunities
            {
                NextSequence = board.NextSequence, NextGenerationDay = board.NextGenerationDay,
                CumulativePayments = board.CumulativePayments,
                CumulativeDeliveryPayments = board.CumulativeDeliveryPayments,
                CumulativeRewards = board.CumulativeRewards,
                ExportedGoods = Copy(board.ExportedGoods),
                Entries = board.Entries.Select(e => new RegionalSaveOpportunity
                {
                Id = e.Id,
                Kind = (int)e.Kind,
                Status = (int)e.Status,
                PortId = e.PortId,
                OwnerId = e.OwnerId,
                GoodId = e.GoodId,
                RequiredQuantity = e.RequiredQuantity,
                DeliveredQuantity = e.DeliveredQuantity,
                UnitPrice = e.UnitPrice,
                CashReward = e.CashReward,
                RequiredCash = e.RequiredCash,
                ContributedCash = e.ContributedCash,
                CreatedDay = e.CreatedDay,
                ExpiresDay = e.ExpiresDay,
                DeadlineDay = e.DeadlineDay,
                WindowDays = e.WindowDays,
                WindowCount = e.WindowCount,
                WindowIndex = e.WindowIndex,
                WindowDelivered = e.WindowDelivered,
                EvidenceRequired = e.EvidenceRequired,
                EvidenceCount = e.EvidenceCount,
                ContributionCredit = e.ContributionCredit,
                ReservedCash = e.ReservedCash,
                PaidCash = e.PaidCash,
                CompetitionGroupId = e.CompetitionGroupId,
                ExclusiveGroupId = e.ExclusiveGroupId,
                BlueprintId = e.BlueprintId,
                FacilityId = e.FacilityId,
                StorageCapacity = e.StorageCapacity,
                CompletedDay = e.CompletedDay,
                RewardKind = (int)e.RewardKind,
                PrivilegeDays = e.PrivilegeDays,
                PrivilegeUntilDay = e.PrivilegeUntilDay,
                }).ToArray()
            };
    }

}
