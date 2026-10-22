using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    public sealed class EncyclopediaEntry
    {
        public string Id { get; }
        public string Category { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<string> Paragraphs { get; }
        public IReadOnlyList<string> RelatedIds { get; }
        internal EncyclopediaEntry(string category, JObject value)
        {
            Category = category; Id = Required(value, "id"); Name = Required(value, "name");
            Description = Required(value, "description");
            Paragraphs = Strings(value, "paragraphs");
            RelatedIds = Strings(value, "relatedIds");
        }
        private static string Required(JObject entry, string name) =>
            entry[name]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)entry[name])
                ? (string)entry[name] : throw new InvalidOperationException("Encyclopedia entry is missing " + name + ".");
        private static IReadOnlyList<string> Strings(JObject entry, string name)
        {
            if (!(entry[name] is JArray array) || array.Any(v => v.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)v)))
                throw new InvalidOperationException("Invalid encyclopedia " + name + ".");
            return Array.AsReadOnly(array.Values<string>().ToArray());
        }
    }

    /// <summary>Offline generated reference, validated against the staged canonical catalog before display.</summary>
    public sealed class EncyclopediaCatalog
    {
        public const int FormatVersion = 2;
        public IReadOnlyDictionary<string, EncyclopediaEntry> Entries { get; }
        private EncyclopediaCatalog(IDictionary<string, EncyclopediaEntry> entries) =>
            Entries = new ReadOnlyDictionary<string, EncyclopediaEntry>(new SortedDictionary<string, EncyclopediaEntry>(entries, StringComparer.Ordinal));

        public static EncyclopediaCatalog Load(string json, ContentCatalog content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            // Reference includes every explicitly scoped canonical entry; selecting a run still controls its actual inventory catalog.
            if (content.HasExpandingEconomy) content = content.ForExpandingEconomy();
            var document = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (document["formatVersion"]?.Type != JTokenType.Integer || (int)document["formatVersion"] != FormatVersion ||
                document["sourceSchemaVersion"]?.Type != JTokenType.Integer || (int)document["sourceSchemaVersion"] != content.SchemaVersion ||
                document["sourceHelpSchemaVersion"]?.Type != JTokenType.Integer || (int)document["sourceHelpSchemaVersion"] != 1)
                throw new InvalidOperationException("Encyclopedia version is unsupported or stale. Run python tools/generate_docs.py and tools/prepare_unity.py.");
            var entries = new Dictionary<string, EncyclopediaEntry>(StringComparer.Ordinal);
            var raw = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (var category in new[] { "goods", "buildings", "recipes", "mechanics" })
            {
                if (!(document[category] is JArray array) || array.Count == 0)
                    throw new InvalidOperationException("Encyclopedia category is missing: " + category);
                foreach (var token in array)
                {
                    if (!(token is JObject value)) throw new InvalidOperationException("Invalid encyclopedia entry.");
                    var entry = new EncyclopediaEntry(category, value);
                    if (entries.ContainsKey(entry.Id)) throw new InvalidOperationException("Duplicate encyclopedia ID: " + entry.Id);
                    entries.Add(entry.Id, entry); raw.Add(entry.Id, value);
                }
            }
            foreach (var entry in entries.Values)
                if (entry.RelatedIds.Distinct(StringComparer.Ordinal).Count() != entry.RelatedIds.Count ||
                    entry.RelatedIds.Any(id => !entries.ContainsKey(id)))
                    throw new InvalidOperationException("Unresolved encyclopedia relationship: " + entry.Id);
            ValidateCategory(entries, "goods", content.Goods.Select(g => g.Id));
            ValidateCategory(entries, "buildings", content.Buildings.Select(b => b.Id));
            ValidateCategory(entries, "recipes", content.Recipes.Select(r => r.Id));
            foreach (var good in content.Goods) ValidateText(entries[good.Id], good.Name, good.Description);
            foreach (var building in content.Buildings) ValidateText(entries[building.Id], building.Name, building.Description);
            foreach (var recipe in content.Recipes)
            {
                ValidateText(entries[recipe.Id], recipe.Name, recipe.Description);
                var value = raw[recipe.Id];
                if ((string)value["buildingId"] != recipe.BuildingId ||
                    (string)value["kind"] != recipe.Kind.ToString().ToLowerInvariant() ||
                    !QuantitiesMatch(value["inputs"], recipe.Inputs) || !QuantitiesMatch(value["outputs"], recipe.Outputs))
                    throw new InvalidOperationException("Stale encyclopedia recipe: " + recipe.Id);
            }
            return new EncyclopediaCatalog(entries);
        }
        private static void ValidateText(EncyclopediaEntry entry, string name, string description)
        {
            if (entry.Name != name || entry.Description != description)
                throw new InvalidOperationException("Stale encyclopedia definition: " + entry.Id);
        }
        private static void ValidateCategory(IDictionary<string, EncyclopediaEntry> entries, string category, IEnumerable<string> ids)
        {
            if (!entries.Values.Where(e => e.Category == category).Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(ids.OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidOperationException("Encyclopedia definitions differ from canonical " + category + ".");
        }
        private static bool QuantitiesMatch(JToken value, IReadOnlyList<RecipeQuantity> expected)
        {
            if (!(value is JArray quantities) || quantities.Count != expected.Count) return false;
            if (quantities.Any(q => !(q is JObject) || q["goodId"]?.Type != JTokenType.String ||
                q["quantity"]?.Type != JTokenType.Integer)) return false;
            var actual = quantities.Select(q => ((string)q["goodId"], (int?)q["quantity"])).OrderBy(q => q.Item1, StringComparer.Ordinal).ToArray();
            return actual.SequenceEqual(expected.Select(q => (q.GoodId, (int?)q.Quantity)).OrderBy(q => q.Item1, StringComparer.Ordinal));
        }
        public IReadOnlyList<EncyclopediaEntry> Search(string category, string query)
        {
            query = (query ?? "").Trim();
            return Array.AsReadOnly(Entries.Values.Where(e => (string.IsNullOrEmpty(category) || e.Category == category) &&
                (query.Length == 0 || (e.Name + "\n" + e.Description + "\n" + string.Join("\n", e.Paragraphs))
                    .IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(e => e.Name, StringComparer.Ordinal).ToArray());
        }
    }
}
