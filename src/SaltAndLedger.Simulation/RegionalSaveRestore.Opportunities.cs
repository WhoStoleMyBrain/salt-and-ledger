using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    internal static partial class RegionalSaveRestore
    {
        private static void ValidateOpportunityCounts(RegionalSaveData data)
        {
            if (data.Opportunities == null) throw new WorldSaveException("Opportunity board is missing.");
            Count(data.Opportunities.Entries, 0, 1024);
            Require(data.Opportunities.ExportedGoods != null && data.Opportunities.ExportedGoods.Count <= 64,
                "Invalid exported goods counters.");
        }

        private static OpportunityBoardSnapshot RestoreOpportunities(ContentCatalog content,
            RegionalWorldSnapshot initial, RegionalSaveData data)
        {
            var saved = data.Opportunities ?? throw new WorldSaveException("Opportunity board is missing.");
            var config = initial.Opportunities!.Configuration;
            Nonnegative(saved.CumulativePayments, saved.CumulativeDeliveryPayments, saved.CumulativeRewards,
                saved.NextGenerationDay);
            Require(saved.NextSequence >= 1 && (BigInteger)saved.NextSequence <= 1 +
                ((BigInteger)data.Day / config.GenerationIntervalDays + 1) * config.MaximumAvailableContracts,
                "Invalid opportunity identity sequence.");
            Require(saved.NextGenerationDay > data.Day && saved.NextGenerationDay <= checked(data.Day + config.GenerationIntervalDays),
                "Opportunity generation date is outside its bounded schedule.");
            Require((BigInteger)saved.CumulativePayments == (BigInteger)saved.CumulativeDeliveryPayments + saved.CumulativeRewards,
                "Opportunity payment counters do not reconcile.");
            Totals(content, saved.ExportedGoods);
            Unique(saved.Entries.Select(e => e.Id), "opportunities");
            var entries = saved.Entries.Select(RestoreOpportunity).ToArray();
            var player = initial.Ships.Values.Single(s => s.IsPlayer).OwnerId;
            Require(initial.Opportunities.Entries.Where(e => e.IsProject || initial.IsRoyalOpportunity(e.Id))
                .All(e => entries.Any(savedEntry => savedEntry.Id == e.Id)), "A permanent initial opportunity record is missing.");
            foreach (var entry in entries)
            {
                RuntimeIdentifier.Validate(entry.Id, nameof(entry.Id));
                Defined(entry.Kind); Defined(entry.Status); Defined(entry.RewardKind);
                Require(initial.Ports.ContainsKey(entry.PortId), "Opportunity refers to an unknown town.");
                content.GetGood(entry.GoodId);
                Require(initial.RunIdentity!.GenerationConfiguration.EnabledGoods.Contains(entry.GoodId),
                    "Opportunity requires a disabled commodity.");
                Require(entry.OwnerId == null || entry.OwnerId == player, "Opportunity has an invalid owner.");
                Nonnegative(entry.RequiredQuantity, entry.DeliveredQuantity, entry.UnitPrice, entry.CashReward,
                    entry.RequiredCash, entry.ContributedCash, entry.CreatedDay, entry.ExpiresDay, entry.DeadlineDay,
                    entry.WindowDays, entry.WindowCount, entry.WindowIndex, entry.WindowDelivered,
                    entry.EvidenceRequired, entry.EvidenceCount, entry.ContributionCredit, entry.ReservedCash,
                    entry.PaidCash, entry.StorageCapacity, entry.PrivilegeDays, entry.PrivilegeUntilDay);
                Require(entry.RequiredQuantity > 0 && entry.DeliveredQuantity <= entry.RequiredQuantity &&
                    entry.ContributedCash <= entry.RequiredCash && entry.WindowCount > 0 &&
                    entry.WindowIndex < entry.WindowCount && entry.WindowDelivered <= entry.RequiredQuantity &&
                    entry.EvidenceCount <= entry.EvidenceRequired && entry.ContributionCredit <= entry.DeliveredQuantity,
                    "Opportunity progress exceeds its declared obligations.");
                Require(entry.CreatedDay <= data.Day && entry.ExpiresDay >= entry.CreatedDay && entry.DeadlineDay >= entry.CreatedDay,
                    "Opportunity terms precede their creation.");
                Require(entry.IsTerminal == entry.CompletedDay.HasValue && (!entry.CompletedDay.HasValue ||
                    entry.CompletedDay >= entry.CreatedDay && entry.CompletedDay <= data.Day), "Invalid opportunity resolution date.");
                Require((entry.Status == OpportunityStatus.Available || entry.Status == OpportunityStatus.Expired ||
                    entry.Status == OpportunityStatus.Declined || entry.Status == OpportunityStatus.Withdrawn) == (entry.OwnerId == null),
                    "Opportunity lifecycle and ownership disagree.");
                Require(entry.Status != OpportunityStatus.Available || data.Day <= entry.ExpiresDay,
                    "Expired opportunity remains available.");
                Require(entry.Status != OpportunityStatus.Active || entry.IsProject || data.Day <= entry.DeadlineDay,
                    "An overdue commitment remains active.");
                if (entry.OwnerId == null)
                    Require(entry.DeliveredQuantity == 0 && entry.ContributedCash == 0 && entry.EvidenceCount == 0 &&
                        entry.ContributionCredit == 0 && entry.PaidCash == 0 && entry.ReservedCash == 0,
                        "Unaccepted opportunity contains fulfillment or funding.");
                if (entry.IsProject) ValidateSavedProject(content, initial, entries, entry);
                else ValidateSavedContract(content, initial, data, entries, entry);
            }
            Require(entries.Count(e => !e.IsProject && e.Status == OpportunityStatus.Active) <= config.ActiveContractLimit,
                "Active contract limit exceeded.");
            Require(entries.Count(e => !e.IsProject && e.Status == OpportunityStatus.Available) <= config.MaximumAvailableContracts +
                initial.Economy!.Configuration.Contracts.Count, "Available contract limit exceeded.");
            foreach (var group in entries.Where(e => e.ExclusiveGroupId != null).GroupBy(e => e.ExclusiveGroupId))
                Require(group.Count(e => e.OwnerId != null) <= 1, "Mutually exclusive tenders were accepted together.");
            Require(entries.Aggregate(BigInteger.Zero, (sum, e) => sum + e.PaidCash) <= saved.CumulativePayments,
                "Retained payments exceed the cumulative opportunity ledger.");
            var finance = data.Companies.Single(c => c.Id == player);
            Require(finance.RewardReceipts == saved.CumulativeRewards &&
                entries.Where(e => e.IsProject).Aggregate(BigInteger.Zero, (sum, e) => sum + e.ContributedCash) == finance.ProjectExpenditure,
                "Company reward or project expenditure differs from opportunity accounting.");
            foreach (var company in data.Companies.Where(c => c.Id != player))
                Require(company.RewardReceipts == 0 && company.ProjectExpenditure == 0 && company.ProjectMaterialCost == 0,
                    "Non-player company contains player opportunity transactions.");
            foreach (var good in content.Goods)
            {
                var projectQuantity = entries.Where(e => e.IsProject && e.GoodId == good.Id)
                    .Aggregate(BigInteger.Zero, (sum, e) => sum + e.DeliveredQuantity);
                var mintQuantity = good.Id == ExpandingEconomyConfiguration.MintInputGoodId ? data.Economy!.SilverReserve : 0;
                Require((BigInteger)Get(finance.SurrenderedQuantities, good.Id) == projectQuantity + mintQuantity,
                    "Surrendered material does not reconcile with projects and the Mint.");
                var exported = data.Economy!.Contracts.Where(c => initial.Economy!.Configuration.Contracts.Single(d => d.Id == c.Id).GoodId == good.Id)
                    .Aggregate(BigInteger.Zero, (sum, c) => sum + c.Delivered);
                Require(exported == Get(saved.ExportedGoods, good.Id), "Exported goods differ from finite royal cargo deliveries.");
            }
            return new OpportunityBoardSnapshot(config, entries, saved.NextSequence, saved.NextGenerationDay,
                saved.CumulativePayments, saved.CumulativeDeliveryPayments, saved.CumulativeRewards, saved.ExportedGoods);
        }

        private static OpportunitySnapshot RestoreOpportunity(RegionalSaveOpportunity e) => new OpportunitySnapshot
        {
                Id = e.Id,
                Kind = (OpportunityKind)e.Kind,
                Status = (OpportunityStatus)e.Status,
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
                RewardKind = (OpportunityRewardKind)e.RewardKind,
                PrivilegeDays = e.PrivilegeDays,
                PrivilegeUntilDay = e.PrivilegeUntilDay,
        };

        private static void ValidateSavedProject(ContentCatalog content, RegionalWorldSnapshot initial, OpportunitySnapshot[] entries, OpportunitySnapshot entry)
        {
            Require(entry.ExpiresDay == long.MaxValue && entry.DeadlineDay == long.MaxValue &&
                entry.UnitPrice == 0 && entry.CashReward == 0 && entry.ReservedCash == 0 && entry.PaidCash == 0 &&
                entry.WindowDays == 0 && entry.WindowCount == 1 && entry.WindowIndex == 0 && entry.WindowDelivered == 0 &&
                entry.EvidenceRequired == 0 && entry.EvidenceCount == 0 && entry.ContributionCredit == 0 &&
                entry.CompetitionGroupId == null && entry.ExclusiveGroupId == null &&
                entry.RewardKind == OpportunityRewardKind.InfrastructureAccess && entry.PrivilegeDays == 0 && entry.PrivilegeUntilDay == 0,
                "Invalid permanent project terms.");
            Require(entry.Status == OpportunityStatus.Available || entry.Status == OpportunityStatus.Active ||
                entry.Status == OpportunityStatus.Succeeded || entry.Status == OpportunityStatus.Declined,
                "Invalid permanent project lifecycle.");
            var fullyFunded = entry.DeliveredQuantity == entry.RequiredQuantity && entry.ContributedCash == entry.RequiredCash;
            Require((entry.Status == OpportunityStatus.Succeeded) == fullyFunded,
                "Project completion does not match actual contributions.");
            Require(entry.GoodId == "planks" || entry.GoodId == "logs", "Project requires an unsupported material.");
            if (entry.Kind == OpportunityKind.TradingDepot)
            {
                Require(entry.Id == "project_depot_" + entry.PortId && entry.BlueprintId == null && entry.FacilityId == null &&
                    entry.RequiredQuantity == DevelopmentProjects.DepotMaterialQuantity &&
                    entry.RequiredCash == DevelopmentProjects.DepotCost && entry.StorageCapacity == DevelopmentProjects.DepotCapacity,
                    "Depot terms do not match canonical construction.");
            }
            else
            {
                Require(entry.BlueprintId != null && initial.IndustryBlueprints.ContainsKey(entry.BlueprintId),
                    "Public project blueprint is unknown.");
                var blueprint = initial.IndustryBlueprints[entry.BlueprintId!];
                Require(entry.Id == "project_industry_" + entry.PortId + "_" + blueprint.Id &&
                    entry.FacilityId == "commissioned_" + entry.Id && entry.StorageCapacity == 0 &&
                    entry.RequiredQuantity == DevelopmentProjects.IndustryMaterialQuantity &&
                    entry.RequiredCash == blueprint.ConstructionCost && (blueprint.EligiblePortIds.Contains(entry.PortId) ||
                    entries.Any(e => !e.IsProject && e.PortId == entry.PortId && e.Status == OpportunityStatus.Succeeded &&
                        e.RewardKind == OpportunityRewardKind.InfrastructureAccess && e.CompletedDay <= entry.CreatedDay)),
                    "Public project terms do not match canonical construction eligibility.");
                var capabilities = initial.Ports[entry.PortId].Facilities.Select(f => f.Configuration.Instance.RecipeId)
                    .Concat(entries.Where(e => e.Kind == OpportunityKind.PublicIndustryCommission && e.PortId == entry.PortId &&
                        entry.Status != OpportunityStatus.Declined && e.Status != OpportunityStatus.Declined)
                        .Select(e => initial.IndustryBlueprints[e.BlueprintId!].RecipeId))
                    .Concat(initial.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(entry.PortId)).Select(b => b.RecipeId))
                    .Concat(new[] { blueprint.RecipeId });
                Require(ProductionSpecialization.Conflict(content, capabilities) == null &&
                    ProductionSpecialization.InstitutionConflict(content, capabilities, entry.PortId, initial.Economy!.Configuration) == null,
                    "Public project violates production specialization.");
            }
        }

        private static void ValidateSavedContract(ContentCatalog content, RegionalWorldSnapshot initial, RegionalSaveData data,
            OpportunitySnapshot[] entries, OpportunitySnapshot entry)
        {
            Require(entry.RequiredQuantity % initial.QuantityScale == 0 && entry.DeliveredQuantity % initial.QuantityScale == 0 &&
                entry.UnitPrice > 0 && entry.RequiredCash == 0 && entry.ContributedCash == 0 && entry.StorageCapacity == 0 &&
                entry.BlueprintId == null, "Invalid cargo contract terms.");
            Require((BigInteger)entry.PaidCash == (BigInteger)entry.DeliveredQuantity * entry.UnitPrice +
                (entry.Status == OpportunityStatus.Succeeded ? entry.CashReward : 0), "Contract payment does not match fulfillment.");
            Require(entry.ReservedCash == (entry.Status == OpportunityStatus.Active ? entry.FundingRequired - entry.PaidCash : 0),
                "Contract escrow does not match its outstanding entitlement.");
            if (entry.Status == OpportunityStatus.Succeeded)
                Require(entry.DeliveredQuantity == entry.RequiredQuantity && entry.EvidenceCount == entry.EvidenceRequired,
                    "Successful contract has incomplete physical or economic obligations.");
            if (entry.Kind == OpportunityKind.IndustrialSupply)
            {
                var facility = initial.Ports[entry.PortId].Facilities.Select(f => f.Configuration)
                    .Concat(ProjectFacilities(initial, new OpportunityBoardSnapshot(initial.Opportunities!.Configuration, entries), entry.PortId))
                    .SingleOrDefault(f => f.Instance.Id == entry.FacilityId);
                Require(facility != null && entry.EvidenceRequired > 0, "Industrial contract has no actual target facility.");
                var inputs = content.GetRecipe(facility!.Instance.RecipeId).Inputs;
                Require(inputs.Count == 1 && inputs[0].GoodId == entry.GoodId &&
                    (long)entry.RequiredQuantity == (long)inputs[0].Quantity * initial.QuantityScale * entry.EvidenceRequired,
                    "Industrial obligation does not match its canonical production inputs.");
            }
            else
            {
                Require(entry.FacilityId == null, "Non-industrial contract contains a production target.");
                if (entry.Kind == OpportunityKind.EmergencyRelief)
                    Require(entry.EvidenceRequired > 0 && initial.Ports[entry.PortId].Consumption.Any(c =>
                        c.Configuration.Kind == ConsumptionKind.Essential && c.Configuration.GoodId == entry.GoodId),
                        "Relief contract does not match actual household demand.");
                else Require(entry.EvidenceRequired == 0 && entry.ContributionCredit == 0,
                    "Simple cargo contract contains unrelated economic evidence.");
            }
            if (entry.RewardKind == OpportunityRewardKind.CommercialPrivilege && entry.Status == OpportunityStatus.Succeeded)
                Require(entry.PrivilegeDays > 0 && entry.PrivilegeUntilDay == checked(entry.CompletedDay!.Value + entry.PrivilegeDays),
                    "Commercial privilege lifetime is invalid.");
            else Require(entry.PrivilegeUntilDay == 0, "Unawarded opportunity contains a commercial privilege.");
            if (entry.Kind == OpportunityKind.RecurringSupply)
                Require(entry.WindowDays > 0 && entry.WindowCount > 1 && entry.RequiredQuantity % entry.WindowCount == 0 &&
                    entry.WindowDelivered <= entry.RequiredQuantity / entry.WindowCount,
                    "Recurring contract has invalid delivery windows.");
            if (entry.Kind == OpportunityKind.RecurringSupply && entry.Status == OpportunityStatus.Active)
                Require(data.Day <= entry.CurrentWindowDeadlineDay, "A missed recurring window remains active.");
            if (entry.Kind == OpportunityKind.RecurringSupply)
                Require((long)entry.DeliveredQuantity == (long)entry.WindowIndex * (entry.RequiredQuantity / entry.WindowCount) + entry.WindowDelivered,
                    "Recurring delivery progress disagrees with its completed windows.");
            else Require(entry.WindowDays == 0 && entry.WindowCount == 1 && entry.WindowIndex == 0 && entry.WindowDelivered == 0,
                "Non-recurring contract contains delivery-window progress.");
            var royal = initial.Economy!.Configuration.Contracts.SingleOrDefault(c => c.Id == entry.Id);
            if (royal == null)
            {
                const string prefix = "opportunity_";
                long sequence = 0;
                Require(entry.Id.StartsWith(prefix, StringComparison.Ordinal) && long.TryParse(entry.Id.Substring(prefix.Length),
                    NumberStyles.None, CultureInfo.InvariantCulture, out sequence) && sequence >= 1 &&
                    sequence < data.Opportunities!.NextSequence && entry.Id == prefix + sequence.ToString("D10", CultureInfo.InvariantCulture),
                    "Generated opportunity identity is outside its sequence.");
                Require(entry.ExpiresDay == checked(entry.CreatedDay + initial.Opportunities!.Configuration.OfferLifetimeDays) &&
                    entry.PrivilegeDays == 24 && (int)entry.RewardKind == sequence % 3,
                    "Generated opportunity lifetime or reward category differs from its canonical terms.");
                var reference = initial.Economy.Configuration.GoodsReferencePrices[entry.GoodId];
                switch (entry.Kind)
                {
                    case OpportunityKind.BulkProcurement:
                        Require(entry.RequiredQuantity == 5 * initial.QuantityScale && entry.UnitPrice == checked(reference + 4) &&
                            entry.CashReward == 0, "Bulk procurement terms changed.");
                        break;
                    case OpportunityKind.RecurringSupply:
                        Require(entry.RequiredQuantity == 9 * initial.QuantityScale && entry.WindowCount == 3 && entry.WindowDays >= 12 &&
                            entry.UnitPrice == checked(reference + 3) && entry.CashReward == 15 * EconomicUnits.MoneyScale &&
                            entry.DeadlineDay - (long)entry.WindowDays * entry.WindowCount >= entry.CreatedDay &&
                            entry.DeadlineDay - (long)entry.WindowDays * entry.WindowCount <= data.Day,
                            "Recurring supply terms changed.");
                        break;
                    case OpportunityKind.IndustrialSupply:
                        Require(entry.EvidenceRequired == 3 && entry.UnitPrice == checked(reference + 2) &&
                            entry.CashReward == 12 * EconomicUnits.MoneyScale, "Industrial supply terms changed.");
                        break;
                    case OpportunityKind.EmergencyRelief:
                        Require(entry.EvidenceRequired == 2 && entry.UnitPrice == reference && entry.RequiredQuantity >= 3 * initial.QuantityScale &&
                            entry.CashReward == 20 * EconomicUnits.MoneyScale, "Emergency relief terms changed.");
                        break;
                }
            }
            if (royal != null)
            {
                Require(entry.Kind == OpportunityKind.BulkProcurement && entry.PortId == royal.PortId && entry.GoodId == royal.GoodId &&
                    entry.RequiredQuantity == royal.Quantity && entry.UnitPrice == royal.UnitPrice && entry.CreatedDay == royal.OpensDay &&
                    entry.DeadlineDay == royal.DeadlineDay && entry.ExpiresDay == royal.DeadlineDay && entry.CashReward == 0 &&
                    entry.RewardKind == OpportunityRewardKind.Cash && entry.PrivilegeDays == 0 && entry.PrivilegeUntilDay == 0 &&
                    entry.CompetitionGroupId == null && entry.ExclusiveGroupId == null,
                    "Royal contract canonical terms changed.");
                var saved = data.Economy!.Contracts.Single(c => c.Id == entry.Id);
                Require(entry.DeliveredQuantity == saved.Delivered && entry.PaidCash == saved.Paid,
                    "Royal contract projection differs from the shared opportunity ledger.");
            }
        }

        private static ScheduledFacilityConfiguration[] ProjectFacilities(RegionalWorldSnapshot initial,
            OpportunityBoardSnapshot? board, string portId) => (board?.Entries ?? Array.Empty<OpportunitySnapshot>())
                .Where(e => e.Kind == OpportunityKind.PublicIndustryCommission && e.Status == OpportunityStatus.Succeeded && e.PortId == portId)
                .Select(e => DevelopmentProjects.PublicFacility(e, initial.IndustryBlueprints[e.BlueprintId!], e.CompletedDay!.Value)).ToArray();
    }
}
