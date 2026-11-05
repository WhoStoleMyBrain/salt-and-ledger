using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldSnapshot
    {
        public OpportunityBoardSnapshot? Opportunities { get; }
        public int MaximumContractDelivery(string shipId, string opportunityId)
        {
            var entry = Opportunities?.Entries.SingleOrDefault(e => e.Id == opportunityId);
            if (entry == null || entry.IsProject || entry.Status != OpportunityStatus.Active) return 0;
            var ship = Ships[shipId];
            if (!ship.IsPlayer || ship.OwnerId != entry.OwnerId || ship.DockedPortId != entry.PortId || Day > entry.DeadlineDay) return 0;
            var available = entry.Kind == OpportunityKind.RecurringSupply
                ? entry.RequiredQuantity / entry.WindowCount - entry.WindowDelivered : entry.RemainingQuantity;
            var quantity = Math.Min(available, ship.Cargo[entry.GoodId]);
            if (!IsRoyalOpportunity(entry.Id)) quantity = Math.Min(quantity, int.MaxValue - Ports[entry.PortId].Inventory[entry.GoodId]);
            return quantity - quantity % QuantityScale;
        }
        public ContractDeliveryAttempt PreviewContractDelivery(string shipId, string opportunityId, int quantity)
        {
            ValidateWholeCargo(quantity);
            var entry = Opportunities?.Entries.SingleOrDefault(e => e.Id == opportunityId);
            var ship = Ships[shipId];
            var outcome = entry == null || entry.IsProject || entry.Status != OpportunityStatus.Active || entry.OwnerId != ship.OwnerId || !ship.IsPlayer
                ? ContractDeliveryOutcome.NotActive : ship.DockedPortId != entry.PortId ? ContractDeliveryOutcome.WrongPort :
                Day > entry.DeadlineDay ? ContractDeliveryOutcome.Expired : ship.Cargo[entry.GoodId] < quantity ? ContractDeliveryOutcome.InsufficientCargo :
                quantity > MaximumContractDelivery(shipId, opportunityId) ? ContractDeliveryOutcome.ExcessQuantity : ContractDeliveryOutcome.Delivered;
            var payment = outcome == ContractDeliveryOutcome.Delivered ? checked(quantity * entry!.UnitPrice) : 0;
            if (payment > 0) _ = checked(ship.Cash + payment);
            return new ContractDeliveryAttempt(opportunityId, outcome, quantity, payment,
                outcome == ContractDeliveryOutcome.Delivered ? "Deliver actual cargo to the named recipient; the finite external authority pays the contracted price once." :
                    "Contract delivery blocked: " + outcome + ". Recurring windows cannot be supplied early.");
        }
        internal bool IsRoyalOpportunity(string id) => Economy?.Configuration.Contracts.Any(c => c.Id == id) == true;
    }

    public sealed partial class RegionalWorldState
    {
        public RegionalResult AcceptOpportunity(string ownerId, string opportunityId, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); work.AcceptOpportunity(ownerId, opportunityId); });
        public RegionalResult DeclineOpportunity(string ownerId, string opportunityId, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); work.DeclineOpportunity(ownerId, opportunityId); });
        public RegionalResult DeliverContract(string shipId, string opportunityId, int quantity, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); EnsureManual(work.Ships[shipId]); work.DeliverOpportunity(shipId, opportunityId, quantity); });

        private sealed partial class WorkingWorld
        {
            internal OpportunityBoardSnapshot? Opportunities { get; set; }
            internal void ReplaceOpportunity(OpportunitySnapshot entry)
            {
                var board = Opportunities ?? throw new InvalidOperationException("This world has no opportunity board.");
                Opportunities = new OpportunityBoardSnapshot(board.Configuration, board.Entries.Where(e => e.Id != entry.Id).Concat(new[] { entry }),
                    board.NextSequence, board.NextGenerationDay, board.CumulativePayments, board.CumulativeDeliveryPayments,
                    board.CumulativeRewards, board.ExportedGoods.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
                Changed = true;
            }
            private void RequireOpportunityOwner(string ownerId)
            {
                if (!Ships.Values.Any(s => s.Configuration.IsPlayer && s.Configuration.OwnerId == ownerId))
                    throw new InvalidOperationException("Opportunities belong to the player company.");
            }
            private bool CanSupplyOpportunityGood(string goodId, BigInteger quantity)
            {
                var recipes = Ports.Values.SelectMany(p => p.Facilities.Select(f => f.Configuration.Instance.RecipeId))
                    .Concat(before.IndustryBlueprints.Values.Select(b => b.RecipeId)).Distinct(StringComparer.Ordinal)
                    .Select(content.GetRecipe).ToArray();
                var visiting = new HashSet<string>(StringComparer.Ordinal);
                bool Obtainable(string good, BigInteger required)
                {
                    var stock = Ports.Values.Aggregate(BigInteger.Zero, (sum, port) => sum + port.Inventory.GetQuantity(good)) +
                        Ships.Values.Aggregate(BigInteger.Zero, (sum, ship) => sum + ship.Cargo.GetQuantity(good)) +
                        CompanyStockpiles.Values.Aggregate(BigInteger.Zero, (sum, store) => sum + store.Inventory.GetQuantity(good)) +
                        PrivateFacilities.Values.Aggregate(BigInteger.Zero, (sum, facility) => sum + facility.Inventory.GetQuantity(good));
                    if (stock >= required) return true;
                    if (!visiting.Add(good)) return false;
                    foreach (var recipe in recipes)
                    {
                        var output = recipe.Outputs.FirstOrDefault(o => o.GoodId == good);
                        if (output == null) continue;
                        var outputQuantity = (BigInteger)output.Quantity * before.QuantityScale;
                        var batches = (required - stock + outputQuantity - 1) / outputQuantity;
                        if (recipe.Inputs.All(input => Obtainable(input.GoodId,
                            batches * input.Quantity * before.QuantityScale)))
                        {
                            visiting.Remove(good);
                            return true;
                        }
                    }
                    visiting.Remove(good);
                    return false;
                }
                return Obtainable(goodId, quantity);
            }
            private bool CanSupplyOpportunityRecipe(string recipeId, string goodId, int quantity)
            {
                var recipe = content.GetRecipe(recipeId);
                var output = recipe.Outputs.First(o => o.GoodId == goodId);
                var outputQuantity = (BigInteger)output.Quantity * before.QuantityScale;
                var batches = (quantity + outputQuantity - 1) / outputQuantity;
                return recipe.Inputs.All(input => CanSupplyOpportunityGood(input.GoodId,
                    batches * input.Quantity * before.QuantityScale));
            }
            internal void InitializeOpportunities()
            {
                if (Economy == null || before.RunIdentity == null) return;
                Opportunities = new OpportunityBoardSnapshot(before.RunIdentity.GenerationConfiguration.Opportunities, Array.Empty<OpportunitySnapshot>());
                foreach (var royal in Economy.Configuration.Contracts)
                {
                    var obtainable = CanSupplyOpportunityGood(royal.GoodId, royal.Quantity);
                    ReplaceOpportunity(new OpportunitySnapshot { Id = royal.Id, Kind = OpportunityKind.BulkProcurement, PortId = royal.PortId,
                        GoodId = royal.GoodId, RequiredQuantity = royal.Quantity, UnitPrice = royal.UnitPrice,
                        CreatedDay = royal.OpensDay, ExpiresDay = royal.DeadlineDay, DeadlineDay = royal.DeadlineDay,
                        Status = obtainable ? OpportunityStatus.Available : OpportunityStatus.Withdrawn,
                        CompletedDay = obtainable ? (long?)null : Day });
                }
                GenerateOpportunities();
            }
            internal void AcceptOpportunity(string ownerId, string id)
            {
                RequireOpportunityOwner(ownerId);
                var board = Opportunities ?? throw new InvalidOperationException("This world has no opportunity board.");
                var current = board.Entries.Single(e => e.Id == id);
                if (current.Status != OpportunityStatus.Available || Day < current.CreatedDay || (!current.IsProject && Day > current.ExpiresDay)) return;
                if (!current.IsProject && board.Entries.Count(e => !e.IsProject && e.Status == OpportunityStatus.Active) >= board.Configuration.ActiveContractLimit) return;
                if (!current.IsProject && Economy!.External < current.FundingRequired) return;
                var entry = current.Copy(); entry.Status = OpportunityStatus.Active; entry.OwnerId = ownerId;
                if (!entry.IsProject)
                {
                    entry.ReservedCash = entry.FundingRequired;
                    Economy!.External = checked(Economy.External - entry.ReservedCash);
                    if (!before.IsRoyalOpportunity(entry.Id)) entry.DeadlineDay = checked(Day + current.DeadlineDay - current.CreatedDay);
                }
                ReplaceOpportunity(entry);
                if (entry.ExclusiveGroupId != null)
                    foreach (var alternative in Opportunities!.Entries.Where(e => e.Id != id && e.Status == OpportunityStatus.Available && e.ExclusiveGroupId == entry.ExclusiveGroupId).ToArray())
                    { var withdrawn = alternative.Copy(); withdrawn.Status = OpportunityStatus.Withdrawn; withdrawn.CompletedDay = Day; ReplaceOpportunity(withdrawn); }
            }
            internal void DeclineOpportunity(string ownerId, string id)
            {
                RequireOpportunityOwner(ownerId);
                var current = Opportunities!.Entries.Single(e => e.Id == id);
                if (current.Status != OpportunityStatus.Available) return;
                var entry = current.Copy(); entry.Status = OpportunityStatus.Declined; entry.CompletedDay = Day; ReplaceOpportunity(entry);
            }
            internal ContractDeliveryAttempt DeliverOpportunity(string shipId, string id, int quantity)
            {
                var attempt = View().PreviewContractDelivery(shipId, id, quantity);
                if (!attempt.Success) return attempt;
                var entry = Opportunities!.Entries.Single(e => e.Id == id).Copy();
                var ship = Ships[shipId];
                RemoveDevelopmentCargo(ship, entry.GoodId, quantity);
                if (!before.IsRoyalOpportunity(id))
                {
                    var port = Ports[entry.PortId];
                    port.Inventory.Commit(new Dictionary<string, int> { [entry.GoodId] = checked(port.Inventory.GetQuantity(entry.GoodId) + quantity) });
                }
                entry.DeliveredQuantity = checked(entry.DeliveredQuantity + quantity);
                if (entry.Kind == OpportunityKind.RecurringSupply) entry.WindowDelivered = checked(entry.WindowDelivered + quantity);
                if (entry.Kind == OpportunityKind.IndustrialSupply || entry.Kind == OpportunityKind.EmergencyRelief)
                    entry.ContributionCredit = checked(entry.ContributionCredit + quantity);
                PayOpportunity(entry, attempt.Payment, false, before.IsRoyalOpportunity(id) ? quantity : 0);
                Accounting.SellToAccount("ship:" + shipId, entry.PortId, entry.GoodId, quantity, attempt.Payment, "external_authority");
                if (before.IsRoyalOpportunity(id))
                {
                    var index = Economy!.Contracts.FindIndex(c => c.Configuration.Id == id);
                    var old = Economy.Contracts[index];
                    Economy.Contracts[index] = new ExternalContractSnapshot(old.Configuration, entry.DeliveredQuantity, entry.PaidCash);
                }
                ReplaceOpportunity(entry);
                if (entry.DeliveredQuantity == entry.RequiredQuantity && entry.EvidenceCount >= entry.EvidenceRequired)
                    CompleteOpportunity(entry, Day);
                return attempt;
            }
            private void PayOpportunity(OpportunitySnapshot entry, long amount, bool reward, int exported = 0)
            {
                if (amount < 0 || amount > entry.ReservedCash) throw new InvalidOperationException("Contract escrow is insufficient.");
                var board = Opportunities!;
                Companies[entry.OwnerId!].Cash = checked(Companies[entry.OwnerId!].Cash + amount);
                Economy!.Inflow = checked(Economy.Inflow + amount);
                entry.ReservedCash -= amount; entry.PaidCash = checked(entry.PaidCash + amount);
                var goods = board.ExportedGoods.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                if (exported > 0) { goods.TryGetValue(entry.GoodId, out var prior); goods[entry.GoodId] = checked(prior + exported); }
                Opportunities = new OpportunityBoardSnapshot(board.Configuration, board.Entries, board.NextSequence, board.NextGenerationDay,
                    checked(board.CumulativePayments + amount), checked(board.CumulativeDeliveryPayments + (reward ? 0 : amount)),
                    checked(board.CumulativeRewards + (reward ? amount : 0)), goods);
                if (reward && amount > 0) Accounting.OpportunityReward(entry.OwnerId!, entry.PortId, entry.Id, amount);
            }
            private void CompleteOpportunity(OpportunitySnapshot entry, long day)
            {
                PayOpportunity(entry, entry.CashReward, true);
                entry.Status = OpportunityStatus.Succeeded; entry.CompletedDay = day;
                if (entry.RewardKind == OpportunityRewardKind.CommercialPrivilege) entry.PrivilegeUntilDay = checked(day + entry.PrivilegeDays);
                ReplaceOpportunity(entry);
            }
            private void EndOpportunity(OpportunitySnapshot entry, OpportunityStatus status, long day)
            {
                Economy!.External = checked(Economy.External + entry.ReservedCash);
                entry.ReservedCash = 0; entry.Status = status; entry.CompletedDay = day; ReplaceOpportunity(entry);
            }
            internal void AdvanceOpportunities(long nextDay)
            {
                if (Opportunities == null) return;
                foreach (var original in Opportunities.Entries.Where(e => !e.IsProject && !e.IsTerminal).ToArray())
                {
                    var entry = original.Copy();
                    if (entry.Status == OpportunityStatus.Available)
                    { if (nextDay > entry.ExpiresDay) EndOpportunity(entry, OpportunityStatus.Expired, nextDay); continue; }
                    if (nextDay > entry.DeadlineDay) { EndOpportunity(entry, OpportunityStatus.Failed, nextDay); continue; }
                    if (entry.Kind == OpportunityKind.IndustrialSupply && entry.EvidenceCount < entry.EvidenceRequired)
                    {
                        var facility = Ports[entry.PortId].Facilities.Single(f => f.Configuration.Instance.Id == entry.FacilityId);
                        var input = content.GetRecipe(facility.Configuration.Instance.RecipeId).Inputs.Single(i => i.GoodId == entry.GoodId);
                        var required = checked(input.Quantity * before.QuantityScale);
                        if (entry.ContributionCredit >= required && Production.Any(p => p.PortId == entry.PortId && p.FacilityId == entry.FacilityId && p.Outcome == ScheduledProductionOutcome.Produced))
                        { entry.ContributionCredit -= required; entry.EvidenceCount++; }
                    }
                    if (entry.Kind == OpportunityKind.EmergencyRelief && entry.EvidenceCount < entry.EvidenceRequired)
                    {
                        var consumption = Consumption.FirstOrDefault(c => c.PortId == entry.PortId && c.GoodId == entry.GoodId && c.Kind == ConsumptionKind.Essential);
                        if (consumption != null && consumption.RequestedQuantity > 0 && consumption.Payment > 0 &&
                            (long)consumption.ConsumedQuantity * 5 >= (long)consumption.RequestedQuantity * 4 &&
                            entry.ContributionCredit >= consumption.ConsumedQuantity)
                        { entry.ContributionCredit -= consumption.ConsumedQuantity; entry.EvidenceCount++; }
                    }
                    if (entry.DeliveredQuantity == entry.RequiredQuantity && entry.EvidenceCount >= entry.EvidenceRequired)
                    { CompleteOpportunity(entry, nextDay); continue; }
                    if (entry.Kind == OpportunityKind.RecurringSupply)
                    {
                        var due = entry.CurrentWindowDeadlineDay;
                        if (nextDay > due)
                        {
                            if (entry.WindowDelivered != entry.RequiredQuantity / entry.WindowCount)
                            { EndOpportunity(entry, OpportunityStatus.Failed, nextDay); continue; }
                            entry.WindowIndex++; entry.WindowDelivered = 0;
                        }
                    }
                    if (entry.EvidenceCount != original.EvidenceCount || entry.WindowIndex != original.WindowIndex) ReplaceOpportunity(entry);
                }
                PruneOpportunityHistory(nextDay);
                if (nextDay >= Opportunities.NextGenerationDay) GenerateOpportunities();
            }
            private void PruneOpportunityHistory(long day)
            {
                var board = Opportunities!;
                var ordinaryHistory = board.Entries.Where(e => !e.IsProject && e.IsTerminal && !before.IsRoyalOpportunity(e.Id) &&
                    !(e.Status == OpportunityStatus.Succeeded && e.RewardKind == OpportunityRewardKind.CommercialPrivilege && e.PrivilegeUntilDay >= day) &&
                    !(e.Status == OpportunityStatus.Succeeded && e.RewardKind == OpportunityRewardKind.InfrastructureAccess &&
                        !board.Entries.Any(other => !other.IsProject && other.Id != e.Id && other.PortId == e.PortId && other.Status == OpportunityStatus.Succeeded &&
                            other.RewardKind == OpportunityRewardKind.InfrastructureAccess && (other.CompletedDay < e.CompletedDay ||
                                other.CompletedDay == e.CompletedDay && string.CompareOrdinal(other.Id, e.Id) < 0))))
                    .OrderByDescending(e => e.CompletedDay).ThenByDescending(e => e.Id, StringComparer.Ordinal).Skip(board.Configuration.HistoryLimit).Select(e => e.Id).ToHashSet();
                if (ordinaryHistory.Count == 0) return;
                Opportunities = new OpportunityBoardSnapshot(board.Configuration, board.Entries.Where(e => !ordinaryHistory.Contains(e.Id)), board.NextSequence,
                    board.NextGenerationDay, board.CumulativePayments, board.CumulativeDeliveryPayments, board.CumulativeRewards,
                    board.ExportedGoods.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
            }

            internal void GenerateOpportunities()
            {
                if (Opportunities == null || Economy == null) return;
                GenerateProjects();
                var board = Opportunities!;
                var sequence = board.NextSequence;
                var available = board.Entries.Count(e => !e.IsProject && !before.IsRoyalOpportunity(e.Id) && e.Status == OpportunityStatus.Available);
                var player = Ships.Values.First(s => s.Configuration.IsPlayer);
                var view = View();
                var ports = Ports.Values.ToArray();
                var offset = (int)(((long)(before.RunIdentity?.Seed ?? 0) + Day / board.Configuration.GenerationIntervalDays) % ports.Length + ports.Length) % ports.Length;
                ports = ports.Skip(offset).Concat(ports.Take(offset)).ToArray();
                int SourceDays(string goodId, string target, int quantity)
                {
                    var lead = int.MaxValue;
                    foreach (var source in ports.Where(p => p.Configuration.Id != target))
                    {
                        var sourceId = source.Configuration.Id;
                        var origin = player.Dock ?? player.Destination ?? ports[0].Configuration.Id;
                        var travel = checked((origin == sourceId ? 0 : view.GetPath(origin, sourceId, player.Configuration.Speed).DurationDays) +
                            view.GetPath(sourceId, target, player.Configuration.Speed).DurationDays + player.Remaining);
                        if (source.Inventory.GetQuantity(goodId) >= quantity) lead = Math.Min(lead, travel);
                        foreach (var facility in source.Facilities)
                        {
                            var output = content.GetRecipe(facility.Configuration.Instance.RecipeId).Outputs.FirstOrDefault(o => o.GoodId == goodId);
                            if (output != null && CanSupplyOpportunityRecipe(facility.Configuration.Instance.RecipeId, goodId, quantity))
                                lead = Math.Min(lead, checked(travel + (int)(((long)quantity + output.Quantity * before.QuantityScale - 1) /
                                (output.Quantity * before.QuantityScale)) * facility.Configuration.IntervalDays));
                        }
                        foreach (var blueprint in before.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(sourceId)))
                        {
                            var output = content.GetRecipe(blueprint.RecipeId).Outputs.FirstOrDefault(o => o.GoodId == goodId);
                            if (output != null && CanSupplyOpportunityRecipe(blueprint.RecipeId, goodId, quantity))
                                lead = Math.Min(lead, checked(travel + 2 + (int)(((long)quantity + output.Quantity * before.QuantityScale - 1) /
                                (output.Quantity * before.QuantityScale)) * blueprint.IntervalDays));
                        }
                    }
                    return lead;
                }
                bool HasSource(string goodId, string target) => SourceDays(goodId, target, 3 * before.QuantityScale) != int.MaxValue;
                int Travel(string port, string goodId, int quantity) => SourceDays(goodId, port, quantity);
                void Add(OpportunitySnapshot entry)
                {
                    if (available >= board.Configuration.MaximumAvailableContracts || entry.FundingRequired > Economy.External ||
                        Opportunities!.Entries.Any(e => !e.IsProject && !e.IsTerminal && e.Kind == entry.Kind && e.PortId == entry.PortId && e.GoodId == entry.GoodId)) return;
                    entry.Id = "opportunity_" + sequence.ToString("D10", CultureInfo.InvariantCulture); sequence++;
                    entry.CreatedDay = Day; entry.ExpiresDay = checked(Day + board.Configuration.OfferLifetimeDays);
                    entry.RewardKind = (OpportunityRewardKind)((sequence - 1) % 3); entry.PrivilegeDays = 24;
                    ReplaceOpportunity(entry); available++;
                }
                // Two recurring agreements consume the same cargo stream in overlapping windows.
                // On alternating rounds they are explicitly mutually exclusive tenders.
                var good = content.Goods.Select(g => g.Id).Where(g => before.RunIdentity!.GenerationConfiguration.EnabledGoods.Contains(g)).Where(g => g != "silver" && CanSupplyOpportunityGood(g, 9 * before.QuantityScale)).FirstOrDefault(g => ports.Count(p => HasSource(g, p.Configuration.Id) &&
                    p.Configuration.Consumption.Any(c => c.GoodId == g)) >= 2) ??
                    content.Goods.Select(g => g.Id).Where(g => before.RunIdentity!.GenerationConfiguration.EnabledGoods.Contains(g)).FirstOrDefault(g => g != "silver" && CanSupplyOpportunityGood(g, 9 * before.QuantityScale) && ports.Count(p => HasSource(g, p.Configuration.Id)) >= 2);
                if (good != null && available <= board.Configuration.MaximumAvailableContracts - 2)
                {
                    var group = "cargo_tender_" + sequence.ToString(CultureInfo.InvariantCulture);
                    var destinations = ports.Where(p => HasSource(good, p.Configuration.Id) && !Opportunities!.Entries.Any(e =>
                        e.Kind == OpportunityKind.RecurringSupply && !e.IsTerminal && e.PortId == p.Configuration.Id && e.GoodId == good)).Take(2).ToArray();
                    var interval = Math.Max(12, destinations.Select(p => Travel(p.Configuration.Id, good, 3 * before.QuantityScale)).DefaultIfEmpty(0).Max() * 2 + 8);
                    foreach (var destination in destinations.Length == 2 ? destinations : Array.Empty<WorkingPort>())
                        Add(new OpportunitySnapshot { Kind = OpportunityKind.RecurringSupply, PortId = destination.Configuration.Id, GoodId = good,
                            RequiredQuantity = 9 * before.QuantityScale, UnitPrice = checked(Economy.Configuration.GoodsReferencePrices[good] + 3),
                            CashReward = 15 * EconomicUnits.MoneyScale, WindowCount = 3, WindowDays = interval,
                            DeadlineDay = checked(Day + interval * 3), CompetitionGroupId = group,
                            ExclusiveGroupId = (Day / board.Configuration.GenerationIntervalDays) % 2 == 0 ? group : null });
                }
                // Give real relief a slot before industrial requests can fill the board.
                var reliefAdded = 0;
                foreach (var port in ports)
                {
                    var food = port.Consumption.FirstOrDefault(c => c.Configuration.Kind == ConsumptionKind.Essential);
                    if (food == null || port.City == null || !HasSource(food.Configuration.GoodId, port.Configuration.Id)) continue;
                    var demand = food.Configuration.PreviewDemand(port.City.Population, food.DemandRemainder).Quantity;
                    if (port.Inventory.GetQuantity(food.Configuration.GoodId) >= demand && food.LastUnmetQuantity == 0) continue;
                    var wanted = ((long)Math.Max(demand, before.QuantityScale) * 3 + before.QuantityScale - 1) / before.QuantityScale * before.QuantityScale;
                    if (wanted > int.MaxValue) continue;
                    var quantity = (int)wanted;
                    var lead = Travel(port.Configuration.Id, food.Configuration.GoodId, quantity); if (lead == int.MaxValue) continue;
                    var count = available;
                    Add(new OpportunitySnapshot { Kind = OpportunityKind.EmergencyRelief, PortId = port.Configuration.Id, GoodId = food.Configuration.GoodId,
                        RequiredQuantity = quantity, UnitPrice = Economy.Configuration.GoodsReferencePrices[food.Configuration.GoodId], CashReward = 20 * EconomicUnits.MoneyScale,
                        EvidenceRequired = 2, DeadlineDay = checked(Day + lead * 2 + food.Configuration.IntervalDays * 6 + 12) });
                    if (available > count && ++reliefAdded >= 1) break;
                }
                var industrialAdded = 0;
                foreach (var port in ports)
                {
                    foreach (var facility in port.Facilities)
                    {
                        var recipe = content.GetRecipe(facility.Configuration.Instance.RecipeId);
                        if (recipe.Inputs.Count != 1) continue;
                        var input = recipe.Inputs[0];
                        var quantity = checked(input.Quantity * before.QuantityScale * 3);
                        var lead = Travel(port.Configuration.Id, input.GoodId, quantity); if (lead == int.MaxValue) continue;
                        var count = available;
                        Add(new OpportunitySnapshot { Kind = OpportunityKind.IndustrialSupply, PortId = port.Configuration.Id,
                            FacilityId = facility.Configuration.Instance.Id, GoodId = input.GoodId, RequiredQuantity = quantity,
                            UnitPrice = checked(Economy.Configuration.GoodsReferencePrices[input.GoodId] + 2), CashReward = 12 * EconomicUnits.MoneyScale,
                            EvidenceRequired = 3, DeadlineDay = checked(Day + lead * 2 + facility.Configuration.IntervalDays * 6 + 12) });
                        if (available > count) industrialAdded++;
                        if (industrialAdded >= 3) break;
                    }
                    if (industrialAdded >= 3) break;
                }
                // At most one ordinary bulk order per round; the three authored royal exports use this same lifecycle.
                foreach (var port in ports)
                {
                    var need = port.Configuration.Consumption.FirstOrDefault(c => c.Kind == ConsumptionKind.Municipal || c.Kind == ConsumptionKind.Luxury);
                    if (need == null) continue;
                    var quantity = 5 * before.QuantityScale;
                    var lead = Travel(port.Configuration.Id, need.GoodId, quantity); if (lead == int.MaxValue) continue;
                    var count = available;
                    Add(new OpportunitySnapshot { Kind = OpportunityKind.BulkProcurement, PortId = port.Configuration.Id, GoodId = need.GoodId,
                        RequiredQuantity = quantity, UnitPrice = checked(Economy.Configuration.GoodsReferencePrices[need.GoodId] + 4),
                        DeadlineDay = checked(Day + lead * 2 + 12) });
                    if (available > count) break;
                }
                board = Opportunities!;
                Opportunities = new OpportunityBoardSnapshot(board.Configuration, board.Entries, sequence, checked(Day + board.Configuration.GenerationIntervalDays),
                    board.CumulativePayments, board.CumulativeDeliveryPayments, board.CumulativeRewards,
                    board.ExportedGoods.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
            }
        }
    }
}
