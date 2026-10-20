using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private EncyclopediaCatalog encyclopedia;
        private string encyclopediaError;
        private string encyclopediaSelection;
        private readonly Stack<string> encyclopediaHistory = new Stack<string>();
        private static readonly string[] ReferenceCategories = { "", "goods", "buildings", "recipes", "mechanics" };

        private void BuildEncyclopedia()
        {
            var screen = CreateScreen(ArchipelagoScreen.Encyclopedia, "The company handbook",
                "Search the canonical goods, buildings, recipes and practical rules. This reference works offline.");
            var tools = Panel(screen, null, "row reference-tools");
            var category = new DropdownField { name = "encyclopediaCategory",
                choices = new List<string> { "All entries", "Commodities", "Buildings", "Recipes", "Mechanics" } };
            tools.Add(category);
            category.SetValueWithoutNotify(category.choices[0]);
            var search = new TextField { name = "encyclopediaSearch", label = "Search" };
            tools.Add(search);
            category.RegisterValueChangedCallback(_ => FilterReference());
            search.RegisterValueChangedCallback(_ => FilterReference());
            var body = Panel(screen, null, "reference-columns");
            var list = new ScrollView { name = "encyclopediaEntries" };
            list.AddToClassList("reference-list");
            body.Add(list);
            var detail = new ScrollView { name = "encyclopediaDetail" };
            detail.AddToClassList("reference-detail");
            body.Add(detail);
            Text(screen, "encyclopediaCount", "", "run-footnote");
            var actions = Panel(screen, null, "run-actions row");
            Button(actions, "encyclopediaBackButton", "Back", EncyclopediaBack);
            Button(actions, "encyclopediaCloseButton", "Return to previous screen", () => { encyclopediaHistory.Clear(); ShowScreen(secondaryParent); });
            try
            {
                var asset = Resources.Load<TextAsset>("Encyclopedia");
                if (asset == null) throw new InvalidOperationException("Offline encyclopedia is missing. Run python tools/generate_docs.py and python tools/prepare_unity.py.");
                encyclopedia = EncyclopediaCatalog.Load(asset.text, Session.Content);
            }
            catch (Exception exception) { encyclopediaError = exception.Message; }
        }
        private void RefreshEncyclopedia()
        {
            encyclopediaHistory.Clear();
            FilterReference();
            Element<TextField>("encyclopediaSearch").Focus();
        }
        private void FilterReference()
        {
            var list = Element<ScrollView>("encyclopediaEntries");
            list.Clear();
            if (encyclopedia == null)
            {
                var detail = Element<ScrollView>("encyclopediaDetail");
                detail.Clear();
                Text(detail, null, "Reference unavailable", "run-heading");
                Text(detail, "encyclopediaError", encyclopediaError, "run-copy error");
                SetText("encyclopediaCount", "Missing or stale reference data is not silently substituted.");
                return;
            }
            var category = ReferenceCategories[Math.Max(0, Element<DropdownField>("encyclopediaCategory").index)];
            var matches = encyclopedia.Search(category, Element<TextField>("encyclopediaSearch").value);
            foreach (var entry in matches)
            {
                var captured = entry.Id;
                Button(list, "reference_" + entry.Id, entry.Name + "\n" + CategoryName(entry.Category),
                    () => SelectReference(captured, false), "reference-entry");
            }
            SetText("encyclopediaCount", matches.Count + " entries · generated reference v2 · links use canonical IDs");
            if (matches.Count > 0)
                SelectReference(matches.Any(e => e.Id == encyclopediaSelection) ? encyclopediaSelection : matches[0].Id, false);
            else
            {
                Element<ScrollView>("encyclopediaDetail").Clear();
                Text(Element<ScrollView>("encyclopediaDetail"), null, "No entries match your search.", "run-copy");
            }
        }
        private void SelectReference(string id, bool remember)
        {
            if (remember && encyclopediaSelection != null) encyclopediaHistory.Push(encyclopediaSelection);
            encyclopediaSelection = id;
            var entry = encyclopedia.Entries[id];
            var detail = Element<ScrollView>("encyclopediaDetail");
            detail.Clear();
            Text(detail, null, CategoryName(entry.Category).ToUpperInvariant(), "run-eyebrow");
            Text(detail, "encyclopediaEntryTitle", entry.Name, "run-heading");
            Text(detail, null, entry.Description, "run-objective");
            foreach (var paragraph in entry.Paragraphs) Text(detail, null, paragraph, "run-copy");
            if (HasActiveRun && entry.Category == "buildings")
                foreach (var blueprint in Session.Snapshot.IndustryBlueprints.Values.Where(b => b.BuildingId == id))
                    Text(detail, null, "Current run blueprint: " + blueprint.ConstructionCost + " construction cash · one batch every " +
                        blueprint.IntervalDays + " days · storage " + blueprint.WarehouseCapacity + " units.\nEligible towns: " +
                        string.Join(", ", blueprint.EligiblePortIds.Select(PortName)), "run-copy");
            if (entry.RelatedIds.Count > 0)
            {
                Text(detail, null, "CONNECTED ENTRIES", "run-eyebrow");
                var links = Panel(detail, null, "reference-links");
                foreach (var relatedId in entry.RelatedIds)
                {
                    var captured = relatedId;
                    Button(links, "related_" + relatedId, encyclopedia.Entries[relatedId].Name,
                        () => SelectReference(captured, true));
                }
            }
            foreach (var button in Element<ScrollView>("encyclopediaEntries").Query<Button>().ToList())
                button.EnableInClassList("selected-good", button.name == "reference_" + id);
            detail.scrollOffset = Vector2.zero;
            Element<Button>("encyclopediaBackButton").text = encyclopediaHistory.Count > 0 ? "Back to previous entry" : "Back";
        }
        private void EncyclopediaBack()
        {
            if (encyclopediaHistory.Count > 0) SelectReference(encyclopediaHistory.Pop(), false);
            else ShowScreen(secondaryParent);
        }
        private static string CategoryName(string category) => category == "goods" ? "Commodity" :
            category == "buildings" ? "Building" : category == "recipes" ? "Recipe" : "Mechanic";
    }
}
