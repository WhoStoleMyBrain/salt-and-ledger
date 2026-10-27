using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace SaltAndLedger.Simulation
{
    public sealed class WorldSaveException : Exception
    {
        public WorldSaveException(string message) : base(message) { }
        public WorldSaveException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Portable state codec. The host alone chooses slots, paths and atomic file replacement.</summary>
    public static class WorldSaveCodec
    {
        public const int SchemaVersion = 5;
        public const int MaximumJsonCharacters = 8 * 1024 * 1024;
        internal const int MaximumFacilities = 512;
        internal const int MaximumOpenLots = 16384;
        private static readonly Regex JsonToken = new Regex(
            "\\G(?:[ \\t\\r\\n]+|\"(?:[^\"\\\\\\x00-\\x1f]|\\\\(?:[\"\\\\/bfnrt]|u[0-9a-fA-F]{4}))*\"|" +
            "-?(?:0|[1-9][0-9]*)(?=[ \\t\\r\\n,}\\]]|\\z)|true|false|null|[{}\\[\\]:,])",
            RegexOptions.CultureInvariant);

        private static JsonSerializer Serializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            MaxDepth = 32
            , ContractResolver = new SaveContractResolver()
        });

        public static string Save(RegionalWorldState world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            try
            {
                var identity = world.Snapshot().RunIdentity ??
                    throw new WorldSaveException("Only named RunScenario worlds support durable saves; custom diagnostic fixtures do not have a reproducible identity.");
                if (world.Snapshot().Circulation != null && world.Snapshot().Circulation!.Configuration.Mode != CirculationMode.Combined)
                    throw new WorldSaveException("Diagnostic circulation treatments do not support durable named saves.");
                RequireSupportedIdentity(identity);
                var initial = RunScenario.Create(world.SaveContent, identity);
                var data = world.CaptureSave();
                data.SchemaVersion = SchemaVersion;
                data.ContentFingerprint = Fingerprint(ContentShape(world.SaveContent));
                data.ConfigurationFingerprint = Fingerprint(initial.Snapshot());
                data.StateFingerprint = Fingerprint(data);
                var text = JObject.FromObject(data, Serializer()).ToString(Formatting.None);
                if (text.Length > MaximumJsonCharacters) throw new WorldSaveException("The run exceeds the bounded save size.");
                // Save and load share the same bounds. A host never writes a save that this codec cannot read.
                ValidateTree(Parse(text));
                RegionalSaveRestore.ValidateCounts(data);
                return text;
            }
            catch (WorldSaveException) { throw; }
            catch (Exception exception) when (IsValidationError(exception))
            { throw new WorldSaveException("The run cannot be saved: " + exception.Message, exception); }
        }

        public static RegionalWorldState Load(ContentCatalog content, string json)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (json == null) throw new ArgumentNullException(nameof(json));
            try
            {
                var root = Parse(json);
                ValidateTree(root);
                var data = root.ToObject<RegionalSaveData>(Serializer()) ?? throw new WorldSaveException("Save is empty.");
                if (data.SchemaVersion != SchemaVersion)
                    throw new WorldSaveException("Unsupported save format. Only current v3 schema 5 saves can be loaded.");
                var checksum = data.StateFingerprint;
                data.StateFingerprint = "";
                if (checksum != Fingerprint(data)) throw new WorldSaveException("Save state checksum is invalid; the file may be damaged.");
                data.StateFingerprint = checksum;
                RegionalSaveRestore.ValidateCounts(data);
                var id = data.Identity ?? throw new WorldSaveException("Save identity is missing.");
                var commission = RegionalSaveRestore.RestoreCommissionPlan(data);
                var identity = new RunIdentity(id.ChallengeId, id.CharterId, id.WorldMode, id.Seed,
                    id.Difficulty, id.GeneratorVersion, id.PricingMode, id.ContentVersion, id.ValuationVersion, commission,
                    id.CompanyStorageRules, id.ItineraryVersion, id.EconomicRulesVersion);
                RequireSupportedIdentity(identity);
                var world = RunScenario.Create(content, identity);
                if (data.ContentFingerprint != Fingerprint(ContentShape(world.SaveContent)))
                    throw new WorldSaveException("Save content does not match the current canonical catalog.");
                if (data.ConfigurationFingerprint != Fingerprint(world.Snapshot()))
                    throw new WorldSaveException("Save rules, valuation or initial configuration do not match this version.");
                world.RestoreSave(data);
                return world;
            }
            catch (WorldSaveException) { throw; }
            catch (Exception exception) when (IsValidationError(exception))
            { throw new WorldSaveException("Invalid save: " + exception.Message, exception); }
        }

        private static void RequireSupportedIdentity(RunIdentity identity)
        {
            if (identity.WorldMode != RunWorldMode.ExpandingEconomy || identity.EconomicRulesVersion != 1 ||
                identity.CompanyStorageRules != CompanyStorageRules.Pooled || identity.ItineraryVersion != 1)
                throw new WorldSaveException("Only current v3 circulation, shared storage and itinerary runs support saves.");
        }

        internal static string Fingerprint(object value)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                Canonical(JToken.FromObject(value, Serializer())).ToString(Formatting.None)))).Replace("-", "").ToLowerInvariant();
        }

        private static object ContentShape(ContentCatalog content) => new
        {
            Goods = content.Goods.OrderBy(g => g.Id, StringComparer.Ordinal),
            Buildings = content.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal),
            Recipes = content.Recipes.OrderBy(r => r.Id, StringComparer.Ordinal)
        };

        private static JToken Canonical(JToken token)
        {
            if (token is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new JProperty(p.Name, Canonical(p.Value))));
            if (token is JArray array) return new JArray(array.Select(Canonical));
            return token.DeepClone();
        }

        private static JObject Parse(string json)
        {
            if (json.Length == 0 || json.Length > MaximumJsonCharacters) throw new WorldSaveException("Invalid save size.");
            var offset = 0;
            var previous = "";
            while (offset < json.Length)
            {
                var token = JsonToken.Match(json, offset);
                if (!token.Success) throw new WorldSaveException("Save contains an invalid JSON token.");
                var text = token.Value;
                if (!char.IsWhiteSpace(text[0]))
                {
                    if (previous == "," && (text == "}" || text == "]"))
                        throw new WorldSaveException("Save contains a trailing comma.");
                    previous = text;
                }
                offset += token.Length;
            }
            using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 32, DateParseHandling = DateParseHandling.None };
            var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new WorldSaveException("Save contains more than one JSON value.");
            return root;
        }

        private static void ValidateTree(JObject root)
        {
            var nodes = 0;
            foreach (var token in root.DescendantsAndSelf())
            {
                if (++nodes > 500000) throw new WorldSaveException("Save has too many fields.");
                if (token.Type == JTokenType.String && ((string)token!).Length > 4096)
                    throw new WorldSaveException("Save text exceeds its field limit.");
                if (token is JProperty property && (property.Name.StartsWith("$", StringComparison.Ordinal) || property.Name.Length > 128))
                    throw new WorldSaveException("Save contains unsupported metadata or a long property name.");
                if (token.Type != JTokenType.Integer && token.Type != JTokenType.String && token.Type != JTokenType.Boolean &&
                    token.Type != JTokenType.Null && token.Type != JTokenType.Object && token.Type != JTokenType.Array &&
                    token.Type != JTokenType.Property) throw new WorldSaveException("Save quantities must be exact JSON integers.");
            }
        }

        private static bool IsValidationError(Exception exception) => exception is ArgumentException ||
            exception is InvalidOperationException || exception is OverflowException || exception is KeyNotFoundException ||
            exception is JsonException || exception is NullReferenceException || exception is FormatException;

        private sealed class SaveContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
            {
                var property = base.CreateProperty(member, serialization);
                if (member.DeclaringType == typeof(MarketQuote) && (member.Name == "Bid" || member.Name == "Ask"))
                    property.ShouldSerialize = value => ((MarketQuote)value).IsFixed;
                return property;
            }
        }
    }
}
