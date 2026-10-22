using System;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    internal static class PrivateProductionRules
    {
        internal static ScheduledProductionOutcome Preview(RecipeDefinition recipe, IndustryBlueprint blueprint,
            Func<string, int> quantity, int quantityScale, int storedUnits, long employerCash, bool enabled,
            FacilityConstructionStatus status, long dueDay, long attemptDay)
        {
            if (status == FacilityConstructionStatus.UnderConstruction) return ScheduledProductionOutcome.UnderConstruction;
            if (attemptDay < dueDay) return ScheduledProductionOutcome.NotDue;
            if (!enabled) return ScheduledProductionOutcome.Disabled;
            if (recipe.Inputs.Any(i => quantity(i.GoodId) < checked(i.Quantity * quantityScale))) return ScheduledProductionOutcome.InputShortage;
            if (blueprint.OutputStockLimit.HasValue && recipe.Outputs.Any(output => (long)quantity(output.GoodId) -
                recipe.Inputs.Where(i => i.GoodId == output.GoodId).Sum(i => (long)checked(i.Quantity * quantityScale)) +
                checked(output.Quantity * quantityScale) > blueprint.OutputStockLimit.Value)) return ScheduledProductionOutcome.OutputLimit;
            if ((long)storedUnits - recipe.Inputs.Sum(i => (long)checked(i.Quantity * quantityScale)) +
                recipe.Outputs.Sum(o => (long)checked(o.Quantity * quantityScale)) > blueprint.WarehouseCapacity)
                return ScheduledProductionOutcome.WarehouseCapacity;
            return employerCash < blueprint.WagePerBatch ? ScheduledProductionOutcome.PayrollShortage : ScheduledProductionOutcome.Produced;
        }
    }
    public sealed partial class RegionalWorldSnapshot
    {
        /// <summary>Eligibility of the next daily attempt using current stock and funds, before future procurement or other actors.</summary>
        public ScheduledProductionOutcome PreviewFacilityProduction(string facilityId, ContentCatalog content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var facility = PrivateFacilities[facilityId];
            return PrivateProductionRules.Preview(content.GetRecipe(facility.Blueprint.RecipeId), facility.Blueprint,
                id => facility.Inventory[id], QuantityScale, facility.StoredUnits, Companies[facility.OwnerId].Cash,
                facility.Enabled, facility.ConstructionStatus, facility.NextProductionDay, checked(Day + 1));
        }
    }
}
