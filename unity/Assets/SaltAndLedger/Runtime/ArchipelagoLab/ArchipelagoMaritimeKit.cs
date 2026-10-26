using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private string illustratedRecipe;

        private void ApplyMaritimeKit()
        {
            Root.AddToClassList("sl-theme");
            var harborArt = new MaritimeIllustration("harbor") { name = "harborIllustration", tooltip = "Original harbor vignette; not a count of local buildings." };
            harborArt.AddToClassList("sl-illustration-small");
            Element<VisualElement>("tradeDialog").Q(className: "modal-heading").Insert(0, harborArt);
            var industryArt = new MaritimeIllustration("bakery") { name = "industryIllustration", tooltip = "Original workshop vignette; the recipe below describes the selected real facility." };
            industryArt.AddToClassList("sl-illustration-small");
            Element<VisualElement>("industryDialog").Q(className: "modal-heading").Insert(0, industryArt);
            var fleetArt = new MaritimeIllustration("ship") { name = "fleetIllustration" };
            fleetArt.AddToClassList("sl-illustration-banner");
            Element<ScrollView>("fleetCommandScroll").Insert(0, fleetArt);
            var recipeLabel = Element<Label>("facilityRecipeLabel");
            var recipe = new VisualElement { name = "industryRecipeSymbols" }; recipe.AddToClassList("sl-recipe");
            var policy = Element<ScrollView>("industryPolicyScroll");
            policy.Insert(policy.IndexOf(Element<Label>("facilityStateLabel")) + 1, recipe);
            foreach (var name in new[] { "objectiveCards", "itineraryScroll", "cityEconomyScroll", "commissionMapInspector" })
                Root.Q(name)?.AddToClassList("sl-paper");
            foreach (var pair in new[] { new[] { "modeTrade", "market" }, new[] { "modeIndustry", "industry" },
                new[] { "modeFleet", "ship" }, new[] { "modeEconomy", "cash" }, new[] { "modeObjectives", "info" } })
            {
                var button = Element<Button>(pair[0]); button.AddToClassList("sl-inline");
                var title = button.text; button.text = "";
                var glyph = new MaritimeGlyph(pair[1]); glyph.AddToClassList("sl-glyph-small"); button.Add(glyph);
                var label = new Label(title) { pickingMode = PickingMode.Ignore }; label.AddToClassList("command-nav-label"); button.Add(label);
            }
        }

        private void RefreshIndustryRecipeSymbols(PrivateFacilitySnapshot facility)
        {
            var container = Root.Q("industryRecipeSymbols");
            if (container == null || illustratedRecipe == facility.Blueprint.RecipeId) return;
            illustratedRecipe = facility.Blueprint.RecipeId;
            container.Clear();
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            if (recipe.Inputs.Count == 0) Text(container, null, "Extraction", "sl-group-label");
            foreach (var input in recipe.Inputs) RecipeSymbol(container, input.GoodId, input.Quantity.ToString());
            Text(container, null, "→", "sl-recipe-arrow");
            foreach (var output in recipe.Outputs) RecipeSymbol(container, output.GoodId, output.Quantity.ToString());
        }

        private void RecipeSymbol(VisualElement parent, string goodId, string quantity)
        {
            var tile = Panel(parent, null, "sl-recipe-good");
            tile.Add(new MaritimeGlyph(goodId));
            Text(tile, null, quantity + " " + Session.Content.GetGood(goodId).Name, "");
        }
    }
}
