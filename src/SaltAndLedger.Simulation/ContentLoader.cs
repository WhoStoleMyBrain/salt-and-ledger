using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class ContentValidationException : Exception
    {
        public ContentValidationException(string message) : base(message) { }
        public ContentValidationException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public static class ContentLoader
    {
        private static readonly Regex Identifier = new Regex(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

        // Json.NET also accepts JavaScript extensions. Only permit JSON lexical tokens,
        // then let Json.NET validate structure and reject duplicate object properties.
        private static readonly Regex JsonToken = new Regex(
            "\\G(?:[ \\t\\r\\n]+|\"(?:[^\"\\\\\\x00-\\x1f]|\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4}))*\"|" +
            "-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?(?=[ \\t\\r\\n,}\\]]|\\z)|" +
            "true|false|null|[{}\\[\\]:,])", RegexOptions.CultureInvariant);

        public static ContentCatalog Load(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            JObject root;
            try
            {
                ValidateJsonSyntax(json);
                using var reader = new JsonTextReader(new StringReader(json))
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = 32
                };
                root = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (reader.Read()) throw Invalid("$", "must contain exactly one JSON object");
            }
            catch (JsonException exception)
            {
                throw new ContentValidationException("Invalid content JSON: " + exception.Message, exception);
            }

            if (root.Property("expandingEconomy", StringComparison.Ordinal) == null)
                RequireProperties(root, "$", "schemaVersion", "goods", "buildings", "recipes");
            else
                RequireProperties(root, "$", "schemaVersion", "goods", "buildings", "recipes", "expandingEconomy");
            if (RequireInteger(root["schemaVersion"], "$.schemaVersion") != 1)
                throw Invalid("$.schemaVersion", "must be 1");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var baseCatalog = ReadDefinitions(root, ids);
            if (root["expandingEconomy"] == null) return baseCatalog;
            var extension = RequireObject(root["expandingEconomy"]!);
            RequireProperties(extension, extension.Path, "version", "goods", "buildings", "recipes");
            if (RequireInteger(extension["version"], extension.Path + ".version") != 3)
                throw Invalid(extension.Path + ".version", "must be 3");
            var expanded = ReadDefinitions(extension, ids, baseCatalog);
            return new ContentCatalog(baseCatalog.Goods, baseCatalog.Buildings, baseCatalog.Recipes, expanded);
        }

        private static ContentCatalog ReadDefinitions(JObject root, HashSet<string> ids, ContentCatalog? baseline = null)
        {
            var goods = new List<GoodDefinition>();
            if (baseline != null) goods.AddRange(baseline.Goods);
            foreach (var token in RequireArray(root["goods"], "$.goods"))
            {
                var item = RequireObject(token);
                RequireProperties(item, item.Path, "id", "name", "description");
                goods.Add(new GoodDefinition(DefinitionId(item, ids), Text(item, "name"), Text(item, "description")));
            }

            var buildings = new List<BuildingDefinition>();
            if (baseline != null) buildings.AddRange(baseline.Buildings);
            foreach (var token in RequireArray(root["buildings"], "$.buildings"))
            {
                var item = RequireObject(token);
                RequireProperties(item, item.Path, "id", "name", "description");
                buildings.Add(new BuildingDefinition(DefinitionId(item, ids), Text(item, "name"), Text(item, "description")));
            }

            var recipes = new List<RecipeDefinition>();
            if (baseline != null) recipes.AddRange(baseline.Recipes);
            foreach (var token in RequireArray(root["recipes"], "$.recipes"))
            {
                var item = RequireObject(token);
                if (item.Property("kind", StringComparison.Ordinal) == null)
                    RequireProperties(item, item.Path, "id", "name", "description", "buildingId", "inputs", "outputs");
                else
                    RequireProperties(item, item.Path, "id", "name", "description", "buildingId", "kind", "inputs", "outputs");
                var kind = RecipeType(item);
                var inputs = Quantities(item, "inputs", allowEmpty: kind == RecipeKind.Extraction);
                if (kind == RecipeKind.Extraction && inputs.Count != 0)
                    throw Invalid(item.Path + ".inputs", "extraction must have no tradeable inputs");
                recipes.Add(new RecipeDefinition(DefinitionId(item, ids), Text(item, "name"), Text(item, "description"),
                    Id(item, "buildingId"), kind, inputs, Quantities(item, "outputs")));
            }

            var goodIds = new HashSet<string>(goods.Select(good => good.Id), StringComparer.Ordinal);
            var buildingIds = new HashSet<string>(buildings.Select(building => building.Id), StringComparer.Ordinal);
            foreach (var recipe in recipes)
            {
                if (!buildingIds.Contains(recipe.BuildingId))
                    throw Invalid(recipe.Id, "references unknown building '" + recipe.BuildingId + "'");
                foreach (var quantity in recipe.Inputs.Concat(recipe.Outputs))
                    if (!goodIds.Contains(quantity.GoodId))
                        throw Invalid(recipe.Id, "references unknown good '" + quantity.GoodId + "'");
            }
            return new ContentCatalog(goods, buildings, recipes, isExpandingEconomy: baseline != null);
        }

        private static void ValidateJsonSyntax(string json)
        {
            var offset = 0;
            var previous = "";
            while (offset < json.Length)
            {
                var token = JsonToken.Match(json, offset);
                if (!token.Success) throw Invalid("$", "invalid JSON token at character " + offset);
                var value = token.Value;
                if (value[0] != ' ' && value[0] != '\t' && value[0] != '\r' && value[0] != '\n')
                {
                    if (previous == "," && (value == "}" || value == "]"))
                        throw Invalid("$", "JSON must not contain trailing commas");
                    previous = value;
                }
                offset += token.Length;
            }
        }

        private static JObject RequireObject(JToken token) => token as JObject
            ?? throw Invalid(token.Path, "must be an object");

        private static JArray RequireArray(JToken? token, string path, bool allowEmpty = false)
        {
            if (!(token is JArray array) || (!allowEmpty && array.Count == 0))
                throw Invalid(path, allowEmpty ? "must be an array" : "must be a nonempty array");
            return array;
        }

        private static RecipeKind RecipeType(JObject item)
        {
            if (item.Property("kind", StringComparison.Ordinal) == null) return RecipeKind.Transformation;
            var kind = Text(item, "kind");
            if (kind == "transformation") return RecipeKind.Transformation;
            if (kind == "extraction") return RecipeKind.Extraction;
            throw Invalid(item.Path + ".kind", "must be transformation or extraction");
        }

        private static void RequireProperties(JObject item, string path, params string[] names)
        {
            foreach (var property in item.Properties())
                if (!names.Contains(property.Name, StringComparer.Ordinal))
                    throw Invalid(path, "unknown property '" + property.Name + "'");
            foreach (var name in names)
                if (item.Property(name, StringComparison.Ordinal) == null)
                    throw Invalid(path, "missing property '" + name + "'");
        }

        private static string Text(JObject item, string name)
        {
            var token = item[name];
            if (token?.Type != JTokenType.String)
                throw Invalid(item.Path + "." + name, "must be a nonblank string");
            var value = token!.Value<string>()!;
            // Python's Unicode whitespace also includes the four information separators.
            if (!value.Any(character => !char.IsWhiteSpace(character) &&
                (character < '\u001c' || character > '\u001f')))
                throw Invalid(item.Path + "." + name, "must be a nonblank string");
            return value;
        }

        private static string Id(JObject item, string name)
        {
            var value = Text(item, name);
            if (!Identifier.IsMatch(value)) throw Invalid(item.Path + "." + name, "must match ^[a-z][a-z0-9_]*$");
            return value;
        }

        private static string DefinitionId(JObject item, HashSet<string> ids)
        {
            var id = Id(item, "id");
            if (!ids.Add(id)) throw Invalid(item.Path, "duplicate definition id '" + id + "'");
            return id;
        }

        private static int RequireInteger(JToken? token, string path)
        {
            if (token?.Type != JTokenType.Integer || !int.TryParse(token.ToString(), out var value) || value < 1)
                throw Invalid(path, "must be an integer between 1 and 2147483647");
            return value;
        }

        private static IReadOnlyList<RecipeQuantity> Quantities(JObject recipe, string side, bool allowEmpty = false)
        {
            var result = new List<RecipeQuantity>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in RequireArray(recipe[side], recipe.Path + "." + side, allowEmpty))
            {
                var item = RequireObject(token);
                RequireProperties(item, item.Path, "goodId", "quantity");
                var goodId = Id(item, "goodId");
                if (!ids.Add(goodId)) throw Invalid(item.Path, "duplicate good '" + goodId + "' on recipe " + side);
                result.Add(new RecipeQuantity(goodId, RequireInteger(item["quantity"], item.Path + ".quantity")));
            }
            return result;
        }

        private static ContentValidationException Invalid(string path, string message) =>
            new ContentValidationException(path + ": " + message + ".");
    }
}
