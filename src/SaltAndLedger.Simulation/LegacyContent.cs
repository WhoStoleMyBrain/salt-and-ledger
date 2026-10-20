using System;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Historical two-good fixture, referencing the canonical definitions without copying them.</summary>
    public static class LegacyContent
    {
        public static ContentCatalog Timber(ContentCatalog content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            return new ContentCatalog(
                new[] { content.GetGood("logs"), content.GetGood("planks") },
                new[] { content.GetBuilding("sawmill") }.Concat(content.Buildings.Where(b => b.Id == "lumber_camp")),
                new[] { content.GetRecipe("saw_planks") }.Concat(content.Recipes.Where(r => r.Id == "harvest_logs")));
        }
    }
}
