using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ContentLoaderTests
{
    internal static string CanonicalJson() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content.json"));

    internal static string Change(Action<JObject> mutate)
    {
        var root = JObject.Parse(CanonicalJson());
        mutate(root);
        return root.ToString();
    }

    [Fact]
    public void LoadsCanonicalDefinitionsAndResolvesReferences()
    {
        var catalog = ContentLoader.Load(CanonicalJson());
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(new[] { "logs", "planks", "grain", "flour", "bread", "furniture" }, catalog.Goods.Select(good => good.Id));
        Assert.Equal(new[] { "sawmill", "lumber_camp", "farm", "mill", "bakery", "furniture_workshop" }, catalog.Buildings.Select(building => building.Id));
        Assert.Equal(new[] { "saw_planks", "harvest_logs", "harvest_grain", "mill_flour", "bake_bread", "make_furniture" }, catalog.Recipes.Select(item => item.Id));
        Assert.Equal("Sawmill", catalog.GetBuilding("sawmill").Name);
        var recipe = catalog.GetRecipe("saw_planks");
        Assert.Same(catalog.GetRecipe("saw_planks"), recipe);
        Assert.Equal(RecipeKind.Transformation, recipe.Kind);
        Assert.Equal(catalog.GetBuilding("sawmill").Id, recipe.BuildingId);
        Assert.Equal("logs", Assert.Single(recipe.Inputs).GoodId);
        Assert.Equal(2, recipe.Inputs[0].Quantity);
        Assert.Equal("planks", Assert.Single(recipe.Outputs).GoodId);
        Assert.Equal(1, recipe.Outputs[0].Quantity);
        foreach (var good in recipe.Inputs.Concat(recipe.Outputs))
            Assert.Equal(good.GoodId, catalog.GetGood(good.GoodId).Id);

        var extraction = catalog.GetRecipe("harvest_logs");
        Assert.Equal("lumber_camp", extraction.BuildingId);
        Assert.Equal(RecipeKind.Extraction, extraction.Kind);
        Assert.Empty(extraction.Inputs);
        Assert.Equal("logs", Assert.Single(extraction.Outputs).GoodId);
        Assert.Equal(2, extraction.Outputs[0].Quantity);
    }

    [Fact]
    public void OriginalVersionOneDocumentRetainsImplicitTransformationKind()
    {
        var originalJson = Change(root =>
        {
            root["goods"] = new JArray(root["goods"]!.Take(2));
            root["buildings"] = new JArray(root["buildings"]!.Take(1));
            root["recipes"] = new JArray(root["recipes"]!.Take(1));
        });
        var catalog = ContentLoader.Load(originalJson);
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Single(catalog.Buildings);
        Assert.Equal(RecipeKind.Transformation, Assert.Single(catalog.Recipes).Kind);
    }

    [Fact]
    public void ExplicitTransformationMatchesOmittedKind()
    {
        var catalog = ContentLoader.Load(Change(root => root["recipes"]![0]!["kind"] = "transformation"));
        Assert.Equal(RecipeKind.Transformation, catalog.GetRecipe("saw_planks").Kind);
        Assert.Equal(2, catalog.GetRecipe("saw_planks").Inputs[0].Quantity);
    }

    [Fact]
    public void DefinitionsCannotBeMutatedThroughCollectionCasts()
    {
        var catalog = ContentLoader.Load(CanonicalJson());
        var goods = Assert.IsAssignableFrom<ICollection<GoodDefinition>>(catalog.Goods);
        Assert.Throws<NotSupportedException>(() => goods.Clear());
        var inputs = Assert.IsAssignableFrom<IList<RecipeQuantity>>(catalog.Recipes[0].Inputs);
        Assert.Throws<NotSupportedException>(() => inputs.RemoveAt(0));
        Assert.Equal(2, catalog.GetRecipe("saw_planks").Inputs[0].Quantity);
    }

    [Fact]
    public void LoadsMaximumQuantityWithoutCultureDependentParsing()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var content = ContentLoader.Load(Change(root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = int.MaxValue));
            Assert.Equal(int.MaxValue, content.Recipes[0].Inputs[0].Quantity);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    public static IEnumerable<object[]> InvalidDocuments()
    {
        var mutations = new Dictionary<string, Action<JObject>>
        {
            ["unknown root field"] = root => root["productionSpeed"] = 1,
            ["missing schema version"] = root => root.Property("schemaVersion")!.Remove(),
            ["unsupported schema version"] = root => root["schemaVersion"] = 2,
            ["floating schema version"] = root => root["schemaVersion"] = 1.0,
            ["boolean schema version"] = root => root["schemaVersion"] = true,
            ["null schema version"] = root => root["schemaVersion"] = null,
            ["missing goods"] = root => root.Property("goods")!.Remove(),
            ["empty goods"] = root => root["goods"] = new JArray(),
            ["empty buildings"] = root => root["buildings"] = new JArray(),
            ["empty recipes"] = root => root["recipes"] = new JArray(),
            ["null goods"] = root => root["goods"] = null,
            ["object goods"] = root => root["goods"] = new JObject(),
            ["null good"] = root => root["goods"]![0] = null,
            ["scalar good"] = root => root["goods"]![0] = 5,
            ["unknown good field"] = root => root["goods"]![0]!["price"] = 10,
            ["unknown building field"] = root => root["buildings"]![0]!["workers"] = 2,
            ["unknown recipe field"] = root => root["recipes"]![0]!["duration"] = 1,
            ["missing name"] = root => ((JObject)root["goods"]![0]!).Property("name")!.Remove(),
            ["blank name"] = root => root["goods"]![0]!["name"] = " \t\r\n",
            ["Unicode blank name"] = root => root["goods"]![0]!["name"] = "\u001c\u001d\u001e\u001f\u0085\u00a0\u2028\u2029",
            ["null name"] = root => root["goods"]![0]!["name"] = null,
            ["numeric name"] = root => root["goods"]![0]!["name"] = 3,
            ["missing description"] = root => ((JObject)root["goods"]![0]!).Property("description")!.Remove(),
            ["blank description"] = root => root["goods"]![0]!["description"] = " ",
            ["uppercase id"] = root => root["goods"]![0]!["id"] = "Logs",
            ["id with newline"] = root => root["goods"]![0]!["id"] = "logs\n",
            ["id starting digit"] = root => root["goods"]![0]!["id"] = "1logs",
            ["id with hyphen"] = root => root["goods"]![0]!["id"] = "raw-logs",
            ["blank id"] = root => root["goods"]![0]!["id"] = "",
            ["duplicate good id"] = root => root["goods"]![1]!["id"] = "logs",
            ["duplicate cross-kind id"] = root => root["buildings"]![0]!["id"] = "logs",
            ["duplicate recipe id"] = root => root["recipes"]![0]!["id"] = "planks",
            ["unknown building reference"] = root => root["recipes"]![0]!["buildingId"] = "missing",
            ["good as building reference"] = root => root["recipes"]![0]!["buildingId"] = "logs",
            ["invalid building reference"] = root => root["recipes"]![0]!["buildingId"] = "saw-mill",
            ["missing building reference"] = root => ((JObject)root["recipes"]![0]!).Property("buildingId")!.Remove(),
            ["unknown input reference"] = root => root["recipes"]![0]!["inputs"]![0]!["goodId"] = "missing",
            ["unknown output reference"] = root => root["recipes"]![0]!["outputs"]![0]!["goodId"] = "missing",
            ["building as good reference"] = root => root["recipes"]![0]!["inputs"]![0]!["goodId"] = "sawmill",
            ["empty inputs"] = root => root["recipes"]![0]!["inputs"] = new JArray(),
            ["explicit transformation without inputs"] = root =>
            {
                root["recipes"]![0]!["kind"] = "transformation";
                root["recipes"]![0]!["inputs"] = new JArray();
            },
            ["extraction with inputs"] = root => root["recipes"]![0]!["kind"] = "extraction",
            ["extraction without explicit kind"] = root => ((JObject)root["recipes"]![1]!).Property("kind")!.Remove(),
            ["extraction without outputs"] = root => root["recipes"]![1]!["outputs"] = new JArray(),
            ["null recipe kind"] = root => root["recipes"]![0]!["kind"] = null,
            ["numeric recipe kind"] = root => root["recipes"]![0]!["kind"] = 1,
            ["boolean recipe kind"] = root => root["recipes"]![0]!["kind"] = true,
            ["unknown recipe kind"] = root => root["recipes"]![0]!["kind"] = "renewable",
            ["case-sensitive recipe kind"] = root => root["recipes"]![0]!["kind"] = "Extraction",
            ["extraction null inputs"] = root => root["recipes"]![1]!["inputs"] = null,
            ["extraction missing inputs"] = root => ((JObject)root["recipes"]![1]!).Property("inputs")!.Remove(),
            ["empty outputs"] = root => root["recipes"]![0]!["outputs"] = new JArray(),
            ["null inputs"] = root => root["recipes"]![0]!["inputs"] = null,
            ["null ingredient"] = root => root["recipes"]![0]!["inputs"]![0] = null,
            ["unknown ingredient field"] = root => root["recipes"]![0]!["inputs"]![0]!["chance"] = 1,
            ["missing quantity"] = root => ((JObject)root["recipes"]![0]!["inputs"]![0]!).Property("quantity")!.Remove(),
            ["null quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = null,
            ["zero quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = 0,
            ["negative quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = -1,
            ["boolean quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = true,
            ["string quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = "2",
            ["fractional quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = 1.5,
            ["integral float quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = 2.0,
            ["overflowing quantity"] = root => root["recipes"]![0]!["inputs"]![0]!["quantity"] = 2147483648L,
            ["duplicate input"] = root => ((JArray)root["recipes"]![0]!["inputs"]!).Add(root["recipes"]![0]!["inputs"]![0]!.DeepClone()),
            ["duplicate output"] = root => ((JArray)root["recipes"]![0]!["outputs"]!).Add(root["recipes"]![0]!["outputs"]![0]!.DeepClone())
        };
        foreach (var item in mutations) yield return new object[] { item.Key, Change(item.Value) };

        yield return new object[] { "empty JSON", "" };
        yield return new object[] { "null root", "null" };
        yield return new object[] { "array root", "[]" };
        yield return new object[] { "multiple roots", CanonicalJson() + "{}" };
        yield return new object[] { "duplicate root property", CanonicalJson().Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1") };
        yield return new object[] { "duplicate nested property", Change(root => { }).Replace("\"quantity\": 2", "\"quantity\": 2, \"quantity\": 2") };
        yield return new object[] { "comment", "/*comment*/" + CanonicalJson() };
        yield return new object[] { "single quotes", CanonicalJson().Replace('"', '\'') };
        yield return new object[] { "unquoted key", CanonicalJson().Replace("\"schemaVersion\"", "schemaVersion") };
        yield return new object[] { "trailing comma", CanonicalJson().TrimEnd().TrimEnd('}') + ",}" };
        yield return new object[] { "NaN", Change(root => { }).Replace("\"quantity\": 2", "\"quantity\": NaN") };
        yield return new object[] { "infinity", Change(root => { }).Replace("\"quantity\": 2", "\"quantity\": Infinity") };
        yield return new object[] { "hexadecimal", Change(root => { }).Replace("\"quantity\": 2", "\"quantity\": 0x2") };
        yield return new object[] { "leading zero", Change(root => { }).Replace("\"quantity\": 2", "\"quantity\": 02") };
        yield return new object[] { "exponent quantity", Change(root => { }).Replace("\"quantity\": 2", "\"quantity\": 2e0") };
        yield return new object[] { "huge integer", Change(root => { }).Replace("\"quantity\": 2", "\"quantity\": 9999999999999999999999999999999999999999") };
    }

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void RejectsInvalidContent(string reason, string json)
    {
        var error = Assert.Throws<ContentValidationException>(() => ContentLoader.Load(json));
        Assert.False(string.IsNullOrWhiteSpace(error.Message), reason);
    }
}
