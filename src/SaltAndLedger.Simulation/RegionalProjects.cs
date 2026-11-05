using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public enum ProjectContributionOutcome { Contributed, NotActive, InsufficientFunds, InsufficientStock, ExcessContribution, WrongPort, MissingWarehouse }

    public sealed class ProjectContributionAttempt
    {
        public string ProjectId { get; }
        public ProjectContributionOutcome Outcome { get; }
        public int Quantity { get; }
        public long Cash { get; }
        public string Reason { get; }
        public bool Successful => Outcome == ProjectContributionOutcome.Contributed;
        internal ProjectContributionAttempt(string id, ProjectContributionOutcome outcome, int quantity, long cash, string reason)
        { ProjectId = id; Outcome = outcome; Quantity = quantity; Cash = cash; Reason = reason; }
    }

    /// <summary>Canonical construction terms shared by generation, execution and strict save validation.</summary>
    public static class DevelopmentProjects
    {
        public const int DepotCapacity = 40 * EconomicUnits.QuantityScale;
        public const long DepotCost = 120 * EconomicUnits.MoneyScale;
        public const int DepotMaterialQuantity = 4 * EconomicUnits.QuantityScale;
        public const int IndustryMaterialQuantity = 6 * EconomicUnits.QuantityScale;
        public const int MaximumOpenProjects = 4;

        public static ScheduledFacilityConfiguration PublicFacility(OpportunitySnapshot project,
            IndustryBlueprint blueprint, long completedDay)
        {
            if (project.Kind != OpportunityKind.PublicIndustryCommission || project.BlueprintId != blueprint.Id ||
                project.FacilityId == null) throw new ArgumentException("A public project needs its canonical blueprint and facility identity.");
            return new ScheduledFacilityConfiguration(new BuildingInstance(project.FacilityId,
                blueprint.BuildingId, blueprint.RecipeId), blueprint.IntervalDays, blueprint.OutputStockLimit,
                checked(completedDay + blueprint.IntervalDays), blueprint.WagePerBatch);
        }

        public static long DepotInvestedValue(OpportunitySnapshot project, ExpandingEconomyConfiguration economy) =>
            checked(project.ContributedCash + (long)project.DeliveredQuantity * economy.GoodsReferencePrices[project.GoodId]);
    }

    public sealed partial class RegionalWorldSnapshot
    {
        public ProjectContributionAttempt PreviewProjectContribution(string ownerId, string projectId,
            string? shipId, int quantity, long cash)
        {
            if (quantity < 0 || cash < 0 || quantity == 0 && cash == 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Contribute a positive material quantity or money amount.");
            if (!Ships.Values.Any(s => s.OwnerId == ownerId && s.IsPlayer))
                throw new InvalidOperationException("Only the player company can fund development projects.");
            var project = (Opportunities ?? throw new InvalidOperationException("This world has no opportunity board.")).Entries.Single(e => e.Id == projectId);
            if (!project.IsProject) throw new InvalidOperationException("This opportunity is a delivery contract, not a construction project.");
            ProjectContributionAttempt Result(ProjectContributionOutcome outcome, string reason) =>
                new ProjectContributionAttempt(projectId, outcome, quantity, cash, reason);
            if (project.Status != OpportunityStatus.Active || project.OwnerId != ownerId)
                return Result(ProjectContributionOutcome.NotActive, "Accept this project before contributing to it.");
            if (quantity > project.RemainingQuantity || cash > project.RequiredCash - project.ContributedCash)
                return Result(ProjectContributionOutcome.ExcessContribution, "Contribute no more than the remaining materials and money.");
            if (Companies[ownerId].Cash < cash)
                return Result(ProjectContributionOutcome.InsufficientFunds, "Company cash cannot fund this contribution.");
            if (quantity > 0)
            {
                if (shipId != null)
                {
                    ValidateWholeCargo(quantity);
                    var ship = Ships[shipId];
                    if (ship.OwnerId != ownerId || !ship.IsPlayer)
                        throw new InvalidOperationException("The selected ship must belong to the player company.");
                    if (ship.AutomationEnabled) throw new InvalidOperationException("Pause automation before a manual project contribution.");
                    if (ship.DockedPortId != project.PortId)
                        return Result(ProjectContributionOutcome.WrongPort, "Deliver construction materials aboard a ship docked at the project town.");
                    if (ship.Cargo[project.GoodId] < quantity)
                        return Result(ProjectContributionOutcome.InsufficientStock, "The selected ship lacks the construction materials.");
                }
                else
                {
                    if (!CompanyStockpiles.TryGetValue(CompanyStockpileSnapshot.StoreId(ownerId, project.PortId), out var store))
                        return Result(ProjectContributionOutcome.MissingWarehouse, "Use a docked ship until company storage exists in this town.");
                    if (store.Inventory[project.GoodId] < quantity)
                        return Result(ProjectContributionOutcome.InsufficientStock, "Local company stock lacks the construction materials.");
                }
            }
            _ = checked(Ports[project.PortId].MarketCash + cash);
            return Result(ProjectContributionOutcome.Contributed,
                "Money pays the town and materials are consumed in construction. Contributions are permanent; completion creates only the funded infrastructure.");
        }
    }

    public sealed partial class RegionalWorldState
    {
        public RegionalResult ContributeProject(string ownerId, string projectId, string? shipId,
            int quantity, long cash, RegionalWorldSnapshot? expectedSnapshot = null) => Mutate(work =>
        {
            EnsureCurrent(expectedSnapshot);
            work.ContributeProject(ownerId, projectId, shipId, quantity, cash);
        });

        private sealed partial class WorkingWorld
        {
            private void GenerateProjects()
            {
                if (Economy == null || Opportunities == null || !before.UsesPooledCompanyStorage) return;
                var view = View();
                var material = new[] { "planks", "logs" }.FirstOrDefault(good =>
                    before.RunIdentity!.GenerationConfiguration.EnabledGoods.Contains(good) &&
                    CanSupplyOpportunityGood(good, DevelopmentProjects.IndustryMaterialQuantity) &&
                    (Ports.Values.Any(p => p.Inventory.GetQuantity(good) >= DevelopmentProjects.IndustryMaterialQuantity) ||
                     before.IndustryBlueprints.Values.Any(b => content.GetRecipe(b.RecipeId).Outputs.Any(o => o.GoodId == good)) ||
                     Ports.Values.Any(p => p.Facilities.Any(f => content.GetRecipe(f.Configuration.Instance.RecipeId).Outputs.Any(o => o.GoodId == good)))));
                if (material == null) return;
                var open = Opportunities.Entries.Count(e => e.IsProject && !e.IsTerminal);
                var owner = Ships.Values.Where(s => s.Configuration.IsPlayer).OrderBy(s => s.Configuration.Ship.Id, StringComparer.Ordinal).First();
                var origin = owner.Dock ?? owner.Destination ?? owner.Configuration.Ship.InitialPortId;
                var ports = Ports.Keys.OrderBy(port => port == origin ? 0 : view.GetPath(origin, port, owner.Configuration.Speed).DurationDays)
                    .ThenBy(port => port, StringComparer.Ordinal).ToArray();
                // Alternate the two investment types, retaining at most four open projects.
                for (var round = 0; round < 2 && open < DevelopmentProjects.MaximumOpenProjects; round++)
                {
                    var depotPort = ports.FirstOrDefault(port => !Opportunities.Entries.Any(e =>
                        e.Kind == OpportunityKind.TradingDepot && e.PortId == port));
                    if (depotPort != null)
                    {
                        ReplaceOpportunity(new OpportunitySnapshot
                        {
                            Id = "project_depot_" + depotPort, Kind = OpportunityKind.TradingDepot,
                            Status = OpportunityStatus.Available, PortId = depotPort, GoodId = material,
                            RequiredQuantity = DevelopmentProjects.DepotMaterialQuantity, RequiredCash = DevelopmentProjects.DepotCost,
                            CreatedDay = Day, ExpiresDay = long.MaxValue, DeadlineDay = long.MaxValue,
                            StorageCapacity = DevelopmentProjects.DepotCapacity, RewardKind = OpportunityRewardKind.InfrastructureAccess
                        });
                        open++;
                    }
                    if (open >= DevelopmentProjects.MaximumOpenProjects) break;
                    foreach (var port in ports.OrderByDescending(id => Opportunities.HasInfrastructureAccess(owner.Configuration.OwnerId, id)))
                    {
                        var blueprint = before.IndustryBlueprints.Values.OrderBy(b => b.EligiblePortIds.Contains(port) ? 1 : 0)
                            .ThenBy(b => b.ConstructionCost).ThenBy(b => b.Id, StringComparer.Ordinal)
                            .FirstOrDefault(b => ProjectIndustryCompatible(port, b) &&
                                !Ports[port].Facilities.Any(f => f.Configuration.Instance.RecipeId == b.RecipeId) &&
                                !Opportunities.Entries.Any(e => e.Kind == OpportunityKind.PublicIndustryCommission && e.PortId == port && e.BlueprintId == b.Id));
                        if (blueprint == null) continue;
                        var id = "project_industry_" + port + "_" + blueprint.Id;
                        ReplaceOpportunity(new OpportunitySnapshot
                        {
                            Id = id, Kind = OpportunityKind.PublicIndustryCommission, Status = OpportunityStatus.Available,
                            PortId = port, GoodId = material, RequiredQuantity = DevelopmentProjects.IndustryMaterialQuantity,
                            RequiredCash = blueprint.ConstructionCost, CreatedDay = Day, ExpiresDay = long.MaxValue, DeadlineDay = long.MaxValue,
                            BlueprintId = blueprint.Id, FacilityId = "commissioned_" + id, RewardKind = OpportunityRewardKind.InfrastructureAccess
                        });
                        open++;
                        break;
                    }
                }
            }

            private bool ProjectIndustryCompatible(string portId, IndustryBlueprint blueprint)
            {
                if (!blueprint.EligiblePortIds.Contains(portId) && !Ships.Values.Any(s => s.Configuration.IsPlayer &&
                    Opportunities!.HasInfrastructureAccess(s.Configuration.OwnerId, portId))) return false;
                var capabilities = Ports[portId].Facilities.Select(f => f.Configuration.Instance.RecipeId)
                    .Concat(before.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(portId)).Select(b => b.RecipeId))
                    .Concat(PrivateFacilities.Values.Where(f => f.PortId == portId).Select(f => f.Blueprint.RecipeId))
                    .Concat(Opportunities!.Entries.Where(e => e.Kind == OpportunityKind.PublicIndustryCommission &&
                        e.PortId == portId && !e.IsTerminal).Select(e => before.IndustryBlueprints[e.BlueprintId!].RecipeId))
                    .Concat(new[] { blueprint.RecipeId }).ToArray();
                return ProductionSpecialization.Conflict(content, capabilities) == null &&
                    (Economy == null || ProductionSpecialization.InstitutionConflict(content, capabilities, portId, Economy.Configuration) == null);
            }

            internal void ContributeProject(string ownerId, string projectId, string? shipId, int quantity, long cash)
            {
                var attempt = View().PreviewProjectContribution(ownerId, projectId, shipId, quantity, cash);
                if (!attempt.Successful) return;
                var project = Opportunities!.Entries.Single(e => e.Id == projectId).Copy();
                var port = Ports[project.PortId];
                // Structural permission is rechecked at each contribution, so a reward can never bypass it.
                if (project.Kind == OpportunityKind.PublicIndustryCommission &&
                    !ProjectIndustryCompatible(project.PortId, before.IndustryBlueprints[project.BlueprintId!]))
                    throw new InvalidOperationException("This public industry violates the town's structural production specialization.");
                string? storeId = null;
                if (quantity > 0)
                {
                    if (shipId != null)
                    {
                        var ship = Ships[shipId];
                        storeId = "ship:" + shipId;
                        ship.Cargo.Commit(new Dictionary<string, int> { [project.GoodId] = checked(ship.Cargo.GetQuantity(project.GoodId) - quantity) });
                        ship.CargoUnits = checked(ship.CargoUnits - quantity);
                    }
                    else
                    {
                        storeId = CompanyStockpileSnapshot.StoreId(ownerId, project.PortId);
                        var store = CompanyStockpiles[storeId];
                        store.Inventory.Commit(new Dictionary<string, int> { [project.GoodId] = checked(store.Inventory.GetQuantity(project.GoodId) - quantity) });
                        store.StoredUnits = checked(store.StoredUnits - quantity);
                    }
                }
                Companies[ownerId].Cash = checked(Companies[ownerId].Cash - cash);
                port.Cash = checked(port.Cash + cash);
                Accounting.ProjectContribution(ownerId, storeId, project.PortId, projectId, project.GoodId, quantity, cash);
                project.DeliveredQuantity = checked(project.DeliveredQuantity + quantity);
                project.ContributedCash = checked(project.ContributedCash + cash);
                if (project.DeliveredQuantity == project.RequiredQuantity && project.ContributedCash == project.RequiredCash)
                {
                    if (project.Kind == OpportunityKind.TradingDepot)
                    {
                        var id = CompanyStockpileSnapshot.StoreId(ownerId, project.PortId);
                        if (!CompanyStockpiles.TryGetValue(id, out var store))
                        {
                            store = new WorkingStockpile(content, id, ownerId, project.PortId, 0,
                                new Inventory(content, quantityScale: before.QuantityScale).Snapshot(), before.QuantityScale);
                            CompanyStockpiles.Add(id, store);
                            Accounting.RegisterStore(id, ownerId);
                        }
                        store.Capacity = checked(store.Capacity + project.StorageCapacity);
                    }
                    else
                    {
                        var blueprint = before.IndustryBlueprints[project.BlueprintId!];
                        if (Ports.Values.Any(p => p.Facilities.Any(f => f.Configuration.Instance.Id == project.FacilityId)) ||
                            PrivateFacilities.ContainsKey(project.FacilityId!)) throw new InvalidOperationException("The commissioned facility identity already exists.");
                        var configuration = DevelopmentProjects.PublicFacility(project, blueprint, Day);
                        port.Facilities = port.Facilities.Concat(new[] { new ScheduledFacilitySnapshot(configuration, true,
                            configuration.FirstDueDay, ScheduledProductionOutcome.NotDue) }).OrderBy(f => f.Configuration.Instance.Priority)
                            .ThenBy(f => f.Configuration.Instance.Id, StringComparer.Ordinal).ToArray();
                    }
                    project.Status = OpportunityStatus.Succeeded;
                    project.CompletedDay = Day;
                }
                ReplaceOpportunity(project);
                Changed = true;
            }

            internal void TransferStockpile(string ownerId, string shipId, string storeId,
                TransferDirection direction, string goodId, int quantity)
            {
                before.ValidateWholeCargo(quantity);
                if (!Enum.IsDefined(typeof(TransferDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
                content.GetGood(goodId);
                var store = CompanyStockpiles[storeId];
                var ship = Ships[shipId];
                if (store.OwnerId != ownerId || ship.Configuration.OwnerId != ownerId)
                    throw new InvalidOperationException("Ship and warehouse must belong to the same company.");
                if (ship.Dock != store.PortId) throw new InvalidOperationException("Dock at the company warehouse before transferring cargo.");
                var unloading = direction == TransferDirection.ShipToWarehouse;
                var source = unloading ? ship.Cargo : store.Inventory;
                var destination = unloading ? store.Inventory : ship.Cargo;
                var free = unloading ? store.Capacity - store.StoredUnits : ship.Configuration.Ship.CargoCapacity - ship.CargoUnits;
                var outcome = RegionalWorldSnapshot.TransferOutcomeFor(source.GetQuantity(goodId), free, quantity);
                if (outcome == TransferOutcome.Transferred)
                {
                    var nextSource = checked(source.GetQuantity(goodId) - quantity);
                    var nextDestination = checked(destination.GetQuantity(goodId) + quantity);
                    source.Commit(new Dictionary<string, int> { [goodId] = nextSource });
                    destination.Commit(new Dictionary<string, int> { [goodId] = nextDestination });
                    store.StoredUnits = checked(store.StoredUnits + (unloading ? quantity : -quantity));
                    ship.CargoUnits = checked(ship.CargoUnits + (unloading ? -quantity : quantity));
                    Accounting.Transfer(unloading ? "ship:" + shipId : storeId, unloading ? storeId : "ship:" + shipId,
                        store.PortId, goodId, quantity);
                    Changed = true;
                }
                Transfers.Add(new RegionalTransfer(ownerId, shipId, storeId, direction, goodId, quantity, outcome));
            }
        }
    }
}
