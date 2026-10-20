using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class EncyclopediaCatalogTests
    {
        private ContentCatalog content;
        private string generatedJson;

        [SetUp]
        public void SetUp()
        {
            var canonical = Resources.Load<TextAsset>("CanonicalContent");
            var encyclopedia = Resources.Load<TextAsset>("Encyclopedia");
            Assert.That(canonical, Is.Not.Null, "Run tools/prepare_unity.py to stage canonical content.");
            Assert.That(encyclopedia, Is.Not.Null, "Run generation and Unity preparation to stage the offline encyclopedia.");
            content = ContentLoader.Load(canonical.text);
            generatedJson = encyclopedia.text;
        }

        [Test]
        public void StagedReferenceResolvesAllCanonicalEntriesAndRelatedLinks()
        {
            var catalog = EncyclopediaCatalog.Load(generatedJson, content);
            Assert.That(catalog.Search("goods", "").Select(e => e.Id),
                Is.EquivalentTo(content.Goods.Select(g => g.Id)));
            Assert.That(catalog.Search("buildings", "").Select(e => e.Id),
                Is.EquivalentTo(content.Buildings.Select(b => b.Id)));
            Assert.That(catalog.Search("recipes", "").Select(e => e.Id),
                Is.EquivalentTo(content.Recipes.Select(r => r.Id)));
            Assert.That(catalog.Search("mechanics", "").Count, Is.GreaterThanOrEqualTo(12));
            foreach (var entry in catalog.Entries.Values)
            {
                Assert.That(entry.Name, Is.Not.Empty);
                Assert.That(entry.Paragraphs, Is.Not.Empty);
                foreach (var related in entry.RelatedIds)
                    Assert.That(catalog.Entries.ContainsKey(related), Is.True, entry.Id + " -> " + related);
            }
            foreach (var recipe in content.Recipes)
            {
                var expected = recipe.Inputs.Concat(recipe.Outputs).Select(q => q.GoodId)
                    .Append(recipe.BuildingId).Distinct().ToArray();
                Assert.That(catalog.Entries[recipe.Id].RelatedIds, Is.EquivalentTo(expected));
                Assert.That(catalog.Entries[recipe.BuildingId].RelatedIds, Does.Contain(recipe.Id));
                foreach (var good in recipe.Inputs.Concat(recipe.Outputs))
                    Assert.That(catalog.Entries[good.GoodId].RelatedIds, Does.Contain(recipe.Id));
            }
        }

        [Test]
        public void SearchCombinesCategoriesCaseInsensitiveTextAndDeterministicOrdering()
        {
            var catalog = EncyclopediaCatalog.Load(generatedJson, content);
            Assert.That(catalog.Search("goods", "  LOGS  ").Select(e => e.Id), Does.Contain("logs"));
            Assert.That(catalog.Search("goods", "logs").All(e => e.Category == "goods"), Is.True);
            Assert.That(catalog.Search("recipes", "Flour").Select(e => e.Id), Does.Contain("bake_bread"),
                "Search must include generated relationship paragraphs as well as titles.");
            Assert.That(catalog.Search("mechanics", "cost").Select(e => e.Id), Does.Contain("help_cost_basis"));
            Assert.That(catalog.Search("", "not_a_real_reference_term"), Is.Empty);
            Assert.That(catalog.Search("absent_category", ""), Is.Empty);
            var all = catalog.Search("", " \t ");
            Assert.That(all.Count, Is.EqualTo(catalog.Entries.Count));
            Assert.That(all.Select(e => e.Name), Is.EqualTo(all.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal)));
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.That(catalog.Search("", "SHIPPING").Select(e => e.Id), Does.Contain("help_shipping"));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("formatVersion", 1)]
        [TestCase("formatVersion", 3)]
        [TestCase("sourceSchemaVersion", 2)]
        [TestCase("sourceHelpSchemaVersion", 2)]
        public void UnsupportedOrStaleMetadataFailsExplicitly(string field, int value)
        {
            var document = JObject.Parse(generatedJson);
            document[field] = value;
            var error = Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
            Assert.That(error.Message, Does.Contain("generate_docs.py"));
        }

        [TestCase("formatVersion")]
        [TestCase("sourceSchemaVersion")]
        [TestCase("sourceHelpSchemaVersion")]
        public void VersionsRequirePresentIntegerTokens(string field)
        {
            var document = JObject.Parse(generatedJson);
            var integer = (int)document[field];
            document[field] = integer.ToString(CultureInfo.InvariantCulture);
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
            document[field] = new JValue((double)integer);
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
            document.Remove(field);
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
        }

        [TestCase("goods")]
        [TestCase("buildings")]
        [TestCase("recipes")]
        [TestCase("mechanics")]
        public void MissingOrEmptyCategoryFailsExplicitly(string category)
        {
            var document = JObject.Parse(generatedJson);
            document.Remove(category);
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
            document[category] = new JArray();
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
        }

        [Test]
        public void MissingCanonicalEntityOrChangedDefinitionRejectsTheReference()
        {
            var missing = JObject.Parse(generatedJson);
            ((JArray)missing["goods"]).First.Remove();
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(missing.ToString(), content));
            foreach (var field in new[] { "name", "description" })
            {
                var changed = JObject.Parse(generatedJson);
                changed["goods"][0][field] = "Old canonical text";
                Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(changed.ToString(), content));
            }
        }

        [Test]
        public void BrokenDuplicateOrCrossCategoryIdentifiersCannotLoad()
        {
            var missing = JObject.Parse(generatedJson);
            missing["mechanics"][0]["relatedIds"] = new JArray("missing_entry");
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(missing.ToString(), content));
            var duplicateLink = JObject.Parse(generatedJson);
            duplicateLink["mechanics"][0]["relatedIds"] = new JArray("logs", "logs");
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(duplicateLink.ToString(), content));
            var duplicateId = JObject.Parse(generatedJson);
            duplicateId["mechanics"][0]["id"] = "logs";
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(duplicateId.ToString(), content));
            Assert.Throws<JsonReaderException>(() => EncyclopediaCatalog.Load("{\"formatVersion\":2,\"formatVersion\":2}", content));
        }

        [TestCase("buildingId", "farm")]
        [TestCase("kind", "extraction")]
        public void ChangedRecipeMetadataFails(string field, string value)
        {
            var document = JObject.Parse(generatedJson);
            var recipe = document["recipes"].Single(r => (string)r["id"] == "bake_bread");
            recipe[field] = value;
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
        }

        [Test]
        public void ChangedRecipeRatiosAndNonIntegerQuantitiesFail()
        {
            var document = JObject.Parse(generatedJson);
            var recipe = document["recipes"].Single(r => (string)r["id"] == "bake_bread");
            recipe["outputs"][0]["quantity"] = 3;
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
            recipe["outputs"][0]["quantity"] = new JValue(2.0);
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
            recipe["outputs"][0]["quantity"] = "2";
            Assert.Throws<InvalidOperationException>(() => EncyclopediaCatalog.Load(document.ToString(), content));
        }
    }
}
