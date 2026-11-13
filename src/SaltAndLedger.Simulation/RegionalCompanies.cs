using System;
using System.Globalization;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldState
    {
        private sealed partial class WorkingWorld
        {
            // One investment per review, at most eight owned sites/ships, in ordinal company order.
            // All commands operate on this same staged owner, so a later daily exception rolls back the plan too.
            internal void PlanCompanies()
            {
                if (Economy == null || before.RunIdentity == null) return;
                var settings = before.RunIdentity.GenerationConfiguration.Npc;
                foreach (var company in Companies.Values.Where(c => c.Npc != null))
                {
                    var plan = company.Npc!;
                    AssignCompanyFleet(company.Id, plan);
                    if (Day < plan.NextReviewDay) continue;
                    var reserve = EconomicUnits.Coins(settings.CashReserve);
                    var sites = PrivateFacilities.Values.Where(f => f.OwnerId == company.Id).ToArray();
                    var ships = Ships.Values.Count(s => s.Configuration.OwnerId == company.Id) + Economy.Orders.Count(o => o.OwnerId == company.Id);
                    var blueprint = plan.BlueprintId == null ? null : before.IndustryBlueprints[plan.BlueprintId];
                    var purpose = "Holding capital";
                    // Industrial plans establish output first. Merchants first fund shipping; later industry supports their commodity.
                    var needsIndustry = blueprint != null && sites.Length < settings.MaximumIndustries &&
                        (plan.Profile != NpcCompanyProfile.Merchant || ships >= Math.Min(2, settings.MaximumShips)) &&
                        (plan.Profile != NpcCompanyProfile.Diversified || sites.Length == 0 || ships >= Math.Min(2, settings.MaximumShips));
                    if (needsIndustry && company.Cash >= checked(blueprint!.ConstructionCost + reserve + blueprint.WagePerBatch * 4) &&
                        (sites.Length == 0 || sites.Sum(f => f.BatchesProduced) >= 8 * sites.Length))
                    {
                        var id = company.Id + "_site_" + (sites.Length + 1).ToString(CultureInfo.InvariantCulture);
                        Construct(company.Id, plan.HomePortId, blueprint.Id, id);
                        ConfigureCompanyWarehouse(company.Id, plan, blueprint, reserve);
                        purpose = "Constructed " + blueprint.BuildingId + " at " + plan.HomePortId;
                    }
                    else if (ships < settings.MaximumShips && company.Cash >= checked(Economy.Configuration.ShipCost + reserve) &&
                        (plan.Profile == NpcCompanyProfile.Merchant || sites.Any(f => f.BatchesProduced >= 2)))
                    {
                        var attempt = View().PreviewShipCommission(company.Id, Economy.Configuration.ShipwrightPortId);
                        if (attempt.Outcome == DevelopmentOutcome.Started)
                        {
                            CommissionShip(company.Id, attempt.PortId);
                            purpose = "Ordered shipping capacity at " + attempt.PortId;
                        }
                        else purpose = "Ship investment blocked: " + attempt.Outcome;
                    }
                    company.Npc = company.Npc!.With(nextReviewDay: checked(Day + settings.InvestmentIntervalDays), lastInvestment: purpose);
                    AssignCompanyFleet(company.Id, company.Npc);
                    Changed = true;
                }
            }

            private void ConfigureCompanyWarehouse(string owner, NpcCompanyState plan, IndustryBlueprint blueprint, long reserve)
            {
                var stock = CompanyStockpiles[CompanyStockpileSnapshot.StoreId(owner, plan.HomePortId)];
                var recipe = content.GetRecipe(blueprint.RecipeId);
                var rules = recipe.Inputs.Select(i => new WarehouseCommodityRule(i.GoodId, true,
                    Math.Min(stock.Capacity / Math.Max(1, recipe.Inputs.Count), checked(i.Quantity * before.QuantityScale * 3)),
                    0, false, 0, 0)).Concat(recipe.Outputs.Select(o => new WarehouseCommodityRule(o.GoodId,
                        false, 0, 0, true, before.QuantityScale * 2, 0)));
                stock.Policy = new WarehousePolicy(reserve, rules);
                stock.Policy.Validate(stock.Inventory.Snapshot(), stock.Capacity);
            }

            private void AssignCompanyFleet(string owner, NpcCompanyState plan)
            {
                var site = PrivateFacilities.Values.FirstOrDefault(f => f.OwnerId == owner && f.PortId == plan.HomePortId);
                foreach (var ship in Ships.Values.Where(s => s.Configuration.OwnerId == owner))
                {
                    if (ship.Policy != null || ship.IndustrialPolicy != null || ship.Itinerary != null || ship.Dock == null) continue;
                    // Commissioned vessels sail from the real shipyard to the chosen business harbor before assignment.
                    if (ship.Dock != plan.HomePortId) { Depart(ship.Configuration.Ship.Id, plan.HomePortId); continue; }
                    var capacity = ship.Configuration.Ship.CargoCapacity;
                    var reserve = EconomicUnits.Coins(before.RunIdentity!.GenerationConfiguration.Npc.CashReserve);
                    if (site == null)
                    {
                        if (plan.Profile != NpcCompanyProfile.Merchant && plan.BlueprintId != null) continue;
                        ship.Policy = new TradeRoutePolicy(plan.HomePortId, plan.DestinationPortId,
                            new RouteLeg(plan.GoodId, capacity, before.QuantityScale, long.MaxValue, 1), cashReserve: reserve, allowEmptyReturn: true);
                    }
                    else
                    {
                        var input = content.GetRecipe(site.Blueprint.RecipeId).Inputs.OrderBy(i => i.GoodId, StringComparer.Ordinal).FirstOrDefault();
                        ship.IndustrialPolicy = new IndustrialRoutePolicy(plan.HomePortId, plan.DestinationPortId,
                            new IndustrialRouteLeg(plan.GoodId, capacity, before.QuantityScale, IndustrialEndpointKind.Warehouse,
                                sourceFacilityId: site.Id), input == null ? null : new IndustrialRouteLeg(input.GoodId, capacity,
                                    before.QuantityScale, destinationKind: IndustrialEndpointKind.Warehouse, destinationFacilityId: site.Id),
                            cashReserve: reserve);
                        ship.IndustrialStage = IndustrialRouteStage.AcquireOutbound;
                    }
                    ship.AutomationEnabled = true; Changed = true;
                }
            }
        }
    }

    internal static partial class RegionalSaveRestore
    {
        private static NpcCompanyState? RestoreNpc(RegionalWorldSnapshot initial, RegionalSaveData data, RegionalSaveCompany saved)
        {
            var original = initial.Companies[saved.Id].Npc;
            Require((original == null) == (saved.Npc == null), "Company controller changed.");
            if (original == null) return null;
            var plan = saved.Npc!;
            var settings = initial.RunIdentity!.GenerationConfiguration.Npc;
            Require(plan.Name == original.Name && plan.Profile == original.Profile && plan.HomePortId == original.HomePortId &&
                plan.DestinationPortId == original.DestinationPortId && plan.GoodId == original.GoodId && plan.BlueprintId == original.BlueprintId,
                "Saved company business plan changed.");
            Require(plan.NextReviewDay == (data.Day == 0 ? 0 : checked(((data.Day - 1) / settings.InvestmentIntervalDays + 1) * settings.InvestmentIntervalDays)) &&
                plan.ShipsOrdered <= settings.MaximumShips - settings.InitialShips &&
                data.Facilities.Count(f => f.OwnerId == saved.Id) <= settings.MaximumIndustries,
                "Company review or investment bounds are invalid.");
            Require(saved.MintReceipts == 0 && saved.PublicCommissionExpenditure == 0 && saved.RewardReceipts == 0 && saved.ProjectExpenditure == 0,
                "NPC company used player-only privileges.");
            return plan;
        }
    }
}
