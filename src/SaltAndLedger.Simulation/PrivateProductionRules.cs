using System;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    internal static class PrivateProductionRules
    {
        internal static ScheduledProductionOutcome Preview(RecipeDefinition recipe, IndustryBlueprint blueprint,
            Func<string, int> quantity, int quantityScale, int storedUnits, long employerCash, bool enabled,
            FacilityConstructionStatus status, long dueDay, long attemptDay, int? capacity = null,
            bool pooled = false, int? pooledOutputLimit = null)
        {
            if (status == FacilityConstructionStatus.UnderConstruction) return ScheduledProductionOutcome.UnderConstruction;
            if (attemptDay < dueDay) return ScheduledProductionOutcome.NotDue;
            if (!enabled) return ScheduledProductionOutcome.Disabled;
            if (recipe.Inputs.Any(i => quantity(i.GoodId) < checked(i.Quantity * quantityScale))) return ScheduledProductionOutcome.InputShortage;
            var outputLimit = pooled ? pooledOutputLimit : blueprint.OutputStockLimit;
            if (outputLimit.HasValue && recipe.Outputs.Any(output => (long)quantity(output.GoodId) -
                recipe.Inputs.Where(i => i.GoodId == output.GoodId).Sum(i => (long)checked(i.Quantity * quantityScale)) +
                checked(output.Quantity * quantityScale) > outputLimit.Value)) return ScheduledProductionOutcome.OutputLimit;
            if ((long)storedUnits - recipe.Inputs.Sum(i => (long)checked(i.Quantity * quantityScale)) +
                recipe.Outputs.Sum(o => (long)checked(o.Quantity * quantityScale)) > (capacity ?? blueprint.WarehouseCapacity))
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
            var storage = GetFacilityStorage(facilityId);
            return PrivateProductionRules.Preview(content.GetRecipe(facility.Blueprint.RecipeId), facility.Blueprint,
                id => storage.Inventory[id], QuantityScale, storage.StoredUnits, Companies[facility.OwnerId].Cash,
                facility.Enabled, facility.ConstructionStatus, facility.NextProductionDay, checked(Day + 1),
                storage.Capacity, UsesPooledCompanyStorage, FacilityOutputStockLimit(facilityId));
        }
    }
}
