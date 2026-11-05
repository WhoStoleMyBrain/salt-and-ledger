using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace SaltAndLedger.Simulation
{
    public sealed class WorldGenerationException : ArgumentException
    {
        public WorldGenerationException(string message) : base(message) { }
    }

    public sealed class GenerationRange
    {
        public int Min { get; }
        public int Max { get; }
        [JsonIgnore] public int Minimum => Min;
        [JsonIgnore] public int Maximum => Max;
        [JsonConstructor] public GenerationRange(int min, int max) { Min = min; Max = max; }
        internal void Validate(string path, int minimum, int maximum)
        {
            if (Min < minimum || Max < Min || Max > maximum)
                throw new WorldGenerationException(path + " requires " + minimum + " <= min <= max <= " + maximum + ".");
        }
        internal int Draw(ref GenerationSequence sequence) => Min + sequence.Next(Max - Min + 1);
    }

    public sealed class GeographyGenerationSettings
    {
        public int Width { get; }
        public int Height { get; }
        public int Margin { get; }
        public int MinimumTownSpacing { get; }
        public int MinimumLaneDistance { get; }
        public int MaximumLaneDistance { get; }
        public int ExtraLaneCount { get; }
        public int CandidateCount { get; }
        public int PlacementAttempts { get; }
        [JsonConstructor] public GeographyGenerationSettings(int width = 1000, int height = 700, int margin = 60,
            int minimumTownSpacing = 110, int minimumLaneDistance = 110, int maximumLaneDistance = 450,
            int extraLaneCount = 3, int candidateCount = 12, int placementAttempts = 1500)
        {
            Width = width; Height = height; Margin = margin; MinimumTownSpacing = minimumTownSpacing;
            MinimumLaneDistance = minimumLaneDistance; MaximumLaneDistance = maximumLaneDistance;
            ExtraLaneCount = extraLaneCount; CandidateCount = candidateCount; PlacementAttempts = placementAttempts;
        }
    }

    public sealed class IndustryDistribution
    {
        public string IndustryId { get; }
        public GenerationRange PublicTowns { get; }
        public GenerationRange PrivateTowns { get; }
        [JsonConstructor] public IndustryDistribution(string industryId, GenerationRange publicTowns, GenerationRange privateTowns)
        { IndustryId = industryId; PublicTowns = publicTowns; PrivateTowns = privateTowns; }
    }

    public sealed class NpcGenerationSettings
    {
        public int TradersPerTownPermille { get; }
        public int MinimumCount { get; }
        public int MaximumCount { get; }
        public GenerationRange StartingCash { get; }
        public GenerationRange CargoCapacity { get; }
        public int Speed { get; }
        [JsonConstructor] public NpcGenerationSettings(int tradersPerTownPermille = 500, int minimumCount = 2,
            int maximumCount = 24, GenerationRange? startingCash = null, GenerationRange? cargoCapacity = null, int speed = 100)
        {
            TradersPerTownPermille = tradersPerTownPermille; MinimumCount = minimumCount; MaximumCount = maximumCount;
            StartingCash = startingCash ?? new GenerationRange(300, 500);
            CargoCapacity = cargoCapacity ?? new GenerationRange(3, 6); Speed = speed;
        }
    }

    public sealed class InstitutionGenerationSettings
    {
        public int? StartingPortIndex { get; }
        public int? MintPortIndex { get; }
        public int? SilverPortIndex { get; }
        public int? ShipyardPortIndex { get; }
        [JsonConstructor] public InstitutionGenerationSettings(int? startingPortIndex = null, int? mintPortIndex = null,
            int? silverPortIndex = null, int? shipyardPortIndex = null)
        { StartingPortIndex = startingPortIndex; MintPortIndex = mintPortIndex; SilverPortIndex = silverPortIndex; ShipyardPortIndex = shipyardPortIndex; }
    }

    /// <summary>Immutable distribution settings; units are whole goods and coins, not simulation subunits.</summary>
    public sealed class WorldGenerationConfiguration
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(), MissingMemberHandling = MissingMemberHandling.Error,
            Culture = CultureInfo.InvariantCulture, MaxDepth = 16
        };
        private static readonly Lazy<WorldGenerationConfiguration> DefaultValue = new Lazy<WorldGenerationConfiguration>(
            () => Parse(GenerationContent.ReadResource("WorldGenerationDefault.json")));
        public static WorldGenerationConfiguration Default => DefaultValue.Value;
        private string? canonicalJson;
        private string? fingerprint;
        public int SchemaVersion { get; }
        public GenerationRange TownCount { get; }
        public GeographyGenerationSettings Geography { get; }
        public IReadOnlyList<string> EnabledGoods { get; }
        public IReadOnlyList<IndustryDistribution> Industries { get; }
        public bool EnforceProductionSeparation { get; }
        public bool RequireSupplyChains { get; }
        public GenerationRange Population { get; }
        public GenerationRange Prosperity { get; }
        public GenerationRange MarketCash { get; }
        public GenerationRange HouseholdCashPerResident { get; }
        public IReadOnlyDictionary<string, GenerationRange> StartingInventories { get; }
        public NpcGenerationSettings Npc { get; }
        public InstitutionGenerationSettings Institutions { get; }
        public int FleetLimit { get; }
        public int DeadlineDays { get; }
        public int EconomicCandidateCount { get; }
        public OpportunityConfiguration Opportunities { get; }
        [JsonIgnore] public string Fingerprint
        {
            get
            {
                if (fingerprint != null) return fingerprint;
                using var hash = SHA256.Create();
                return fingerprint = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(ToJson())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
        [JsonConstructor] public WorldGenerationConfiguration(int schemaVersion, GenerationRange townCount,
            GeographyGenerationSettings geography, IEnumerable<string> enabledGoods, IEnumerable<IndustryDistribution> industries,
            bool enforceProductionSeparation, bool requireSupplyChains, GenerationRange population, GenerationRange prosperity,
            GenerationRange marketCash, GenerationRange householdCashPerResident,
            IDictionary<string, GenerationRange> startingInventories, NpcGenerationSettings npc,
            InstitutionGenerationSettings institutions, int fleetLimit = 3, int deadlineDays = 720, int economicCandidateCount = 16,
            OpportunityConfiguration? opportunities = null)
        {
            SchemaVersion = schemaVersion; TownCount = townCount; Geography = geography;
            var goodValues = (enabledGoods ?? throw new WorldGenerationException("enabledGoods is required.")).ToArray();
            var industryValues = (industries ?? throw new WorldGenerationException("industries is required.")).ToArray();
            if (goodValues.Any(x => string.IsNullOrWhiteSpace(x))) throw new WorldGenerationException("enabledGoods cannot contain empty or null IDs.");
            if (industryValues.Any(x => x == null || string.IsNullOrWhiteSpace(x.IndustryId))) throw new WorldGenerationException("industries cannot contain null entries or empty industryId values.");
            EnabledGoods = Array.AsReadOnly(goodValues.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            Industries = Array.AsReadOnly(industryValues.OrderBy(x => x.IndustryId, StringComparer.Ordinal).ToArray());
            EnforceProductionSeparation = enforceProductionSeparation; RequireSupplyChains = requireSupplyChains;
            Population = population; Prosperity = prosperity; MarketCash = marketCash; HouseholdCashPerResident = householdCashPerResident;
            StartingInventories = new ReadOnlyDictionary<string, GenerationRange>(new SortedDictionary<string, GenerationRange>(
                startingInventories ?? throw new WorldGenerationException("startingInventories is required."), StringComparer.Ordinal));
            Npc = npc; Institutions = institutions; FleetLimit = fleetLimit; DeadlineDays = deadlineDays; EconomicCandidateCount = economicCandidateCount;
            Opportunities = opportunities ?? new OpportunityConfiguration();
        }
        public string ToJson() => canonicalJson ?? (canonicalJson = JsonConvert.SerializeObject(this, Formatting.None, JsonSettings));
        public static WorldGenerationConfiguration Parse(string json, ContentCatalog? content = null)
        {
            if (json == null || json.Length > 131072) throw new WorldGenerationException("World generation JSON must be present and at most 128 KiB.");
            try
            {
                ContentLoader.ValidateJsonSyntax(json);
                using var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 16, DateParseHandling = DateParseHandling.None };
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new WorldGenerationException("World generation configuration must contain one JSON object.");
                ValidateJsonShape(root);
                // Json.NET passes zero for an omitted value-type constructor argument.
                // Apply the authored optional defaults after strict token validation instead.
                if (root["opportunities"] is JObject opportunities)
                {
                    var defaults = JObject.FromObject(new OpportunityConfiguration(), JsonSerializer.Create(JsonSettings));
                    foreach (var property in defaults.Properties())
                        if (opportunities[property.Name] == null) opportunities.Add(property.Name, property.Value.DeepClone());
                }
                var result = root.ToObject<WorldGenerationConfiguration>(JsonSerializer.Create(JsonSettings))
                    ?? throw new WorldGenerationException("World generation configuration is required.");
                result.Validate(content);
                return result;
            }
            catch (JsonException exception) { throw new WorldGenerationException("Invalid world generation JSON: " + exception.Message); }
            catch (ContentValidationException exception) { throw new WorldGenerationException("Invalid world generation JSON: " + exception.Message); }
            catch (ArgumentOutOfRangeException exception) { throw new WorldGenerationException("Invalid world generation configuration: " + exception.Message); }
        }
        private static void ValidateJsonShape(JObject root)
        {
            void Keys(JObject item, params string[] names)
            {
                foreach (var property in item.Properties())
                    if (!names.Contains(property.Name, StringComparer.Ordinal))
                        throw new WorldGenerationException(property.Path + " is an unknown configuration property (names are case sensitive).");
            }
            Keys(root, "schemaVersion", "townCount", "geography", "enabledGoods", "industries", "enforceProductionSeparation", "requireSupplyChains",
                "population", "prosperity", "marketCash", "householdCashPerResident", "startingInventories", "npc", "institutions", "fleetLimit", "deadlineDays", "economicCandidateCount", "opportunities");
            void Require(JObject item, string name, JTokenType kind)
            {
                var value = item[name];
                if (value == null || value.Type != kind) throw new WorldGenerationException((item.Path.Length == 0 ? "$" : item.Path) + "." + name + " must be a " + kind + ".");
            }
            foreach (var name in new[] { "schemaVersion" }) Require(root, name, JTokenType.Integer);
            foreach (var name in new[] { "enforceProductionSeparation", "requireSupplyChains" }) Require(root, name, JTokenType.Boolean);
            foreach (var name in new[] { "townCount", "geography", "population", "prosperity", "marketCash", "householdCashPerResident", "startingInventories", "npc", "institutions" }) Require(root, name, JTokenType.Object);
            foreach (var name in new[] { "enabledGoods", "industries" }) Require(root, name, JTokenType.Array);
            foreach (var name in new[] { "fleetLimit", "deadlineDays", "economicCandidateCount" })
                if (root[name] != null) Require(root, name, JTokenType.Integer);
            if (root["opportunities"] != null)
            {
                Require(root, "opportunities", JTokenType.Object);
                var opportunities = (JObject)root["opportunities"]!;
                Keys(opportunities, "activeContractLimit", "maximumAvailableContracts", "generationIntervalDays", "offerLifetimeDays", "historyLimit");
                foreach (var property in opportunities.Properties())
                    if (property.Value.Type != JTokenType.Integer)
                        throw new WorldGenerationException(property.Path + " must be an integer.");
            }
            foreach (var token in (JArray)root["enabledGoods"]!)
                if (token.Type != JTokenType.String) throw new WorldGenerationException("enabledGoods entries must be canonical string IDs.");
            void Range(JToken token)
            {
                if (!(token is JObject range)) throw new WorldGenerationException(token.Path + " must contain integer min and max.");
                Keys(range, "min", "max");
                Require(range, "min", JTokenType.Integer); Require(range, "max", JTokenType.Integer);
            }
            foreach (var name in new[] { "townCount", "population", "prosperity", "marketCash", "householdCashPerResident" }) Range(root[name]!);
            foreach (var property in ((JObject)root["startingInventories"]!).Properties()) Range(property.Value);
            foreach (var token in (JArray)root["industries"]!)
            {
                if (!(token is JObject industry)) throw new WorldGenerationException("industries entries must be objects.");
                Keys(industry, "industryId", "publicTowns", "privateTowns");
                Require(industry, "industryId", JTokenType.String);
                Require(industry, "publicTowns", JTokenType.Object); Require(industry, "privateTowns", JTokenType.Object);
                Range(industry["publicTowns"]!); Range(industry["privateTowns"]!);
            }
            Keys((JObject)root["geography"]!, "width", "height", "margin", "minimumTownSpacing", "minimumLaneDistance", "maximumLaneDistance", "extraLaneCount", "candidateCount", "placementAttempts");
            Keys((JObject)root["npc"]!, "tradersPerTownPermille", "minimumCount", "maximumCount", "startingCash", "cargoCapacity", "speed");
            Keys((JObject)root["institutions"]!, "startingPortIndex", "mintPortIndex", "silverPortIndex", "shipyardPortIndex");
            foreach (var property in ((JObject)root["geography"]!).Properties())
                if (property.Value.Type != JTokenType.Integer) throw new WorldGenerationException(property.Path + " must be an integer.");
            foreach (var property in ((JObject)root["npc"]!).Properties())
                if (property.Name == "startingCash" || property.Name == "cargoCapacity") Range(property.Value);
                else if (property.Value.Type != JTokenType.Integer) throw new WorldGenerationException(property.Path + " must be an integer.");
            foreach (var property in ((JObject)root["institutions"]!).Properties())
                if (property.Value.Type != JTokenType.Integer && property.Value.Type != JTokenType.Null)
                    throw new WorldGenerationException(property.Path + " must be an integer town index or null.");
        }
        public void Validate(ContentCatalog? content = null)
        {
            if (SchemaVersion != 1) throw new WorldGenerationException("schemaVersion must be 1.");
            if (TownCount == null || Geography == null || Population == null || Prosperity == null || MarketCash == null ||
                HouseholdCashPerResident == null || Npc == null || Institutions == null)
                throw new WorldGenerationException("townCount, geography, population, prosperity, marketCash, householdCashPerResident, npc and institutions are required.");
            TownCount.Validate("townCount", 2, 64); Population.Validate("population", 10, 5000); Prosperity.Validate("prosperity", 0, 100);
            MarketCash.Validate("marketCash", 0, 1000000); HouseholdCashPerResident.Validate("householdCashPerResident", 0, 10000);
            if (FleetLimit < 1 || FleetLimit > 32 || DeadlineDays < 1 || DeadlineDays > 36500 || EconomicCandidateCount < 1 || EconomicCandidateCount > 64)
                throw new WorldGenerationException("fleetLimit must be 1..32; deadlineDays 1..36500; economicCandidateCount 1..64.");
            if (Opportunities.ActiveContractLimit < 1 || Opportunities.ActiveContractLimit > 8 ||
                Opportunities.MaximumAvailableContracts < 2 || Opportunities.MaximumAvailableContracts > 16 ||
                Opportunities.GenerationIntervalDays < 1 || Opportunities.GenerationIntervalDays > 365 ||
                Opportunities.OfferLifetimeDays < 1 || Opportunities.OfferLifetimeDays > 365 ||
                Opportunities.HistoryLimit < 8 || Opportunities.HistoryLimit > 128)
                throw new WorldGenerationException("opportunities requires activeContractLimit 1..8, maximumAvailableContracts 2..16, generationIntervalDays/offerLifetimeDays 1..365 and historyLimit 8..128.");
            var g = Geography;
            if (g.Width < 100 || g.Width > 10000 || g.Height < 100 || g.Height > 10000 || g.Margin < 0 ||
                2L * g.Margin >= Math.Min(g.Width, g.Height) || g.MinimumTownSpacing < 1 || g.MinimumTownSpacing > 14000 ||
                g.MinimumLaneDistance < 1 || g.MaximumLaneDistance < g.MinimumLaneDistance || g.MaximumLaneDistance > 15000 ||
                g.ExtraLaneCount < 0 || g.ExtraLaneCount > 256 || g.CandidateCount < 1 || g.CandidateCount > 32 ||
                g.PlacementAttempts < 1 || g.PlacementAttempts > 100000)
                throw new WorldGenerationException("geography requires bounded positive dimensions, spacing/distances, 1..32 candidates, and 1..100000 placementAttempts; minimumLaneDistance must not exceed maximumLaneDistance.");
            if (g.MaximumLaneDistance < g.MinimumTownSpacing)
                throw new WorldGenerationException("maximumLaneDistance is smaller than minimumTownSpacing: no sea lane can connect separated towns.");
            var maxEdges = (long)TownCount.Min * (TownCount.Min - 1) / 2;
            if (g.ExtraLaneCount > maxEdges - (TownCount.Min - 1))
                throw new WorldGenerationException("extraLaneCount exceeds available distinct links for the minimum town count.");
            if (Npc.TradersPerTownPermille < 0 || Npc.TradersPerTownPermille > 10000 || Npc.MinimumCount < 0 ||
                Npc.MaximumCount < Npc.MinimumCount || Npc.MaximumCount > 128 || Npc.Speed < 1 || Npc.Speed > 10000)
                throw new WorldGenerationException("npc requires density 0..10000, 0 <= minimumCount <= maximumCount <= 128 and speed 1..10000.");
            Npc.StartingCash.Validate("npc.startingCash", 0, 1000000); Npc.CargoCapacity.Validate("npc.cargoCapacity", 1, 1000);
            foreach (var pair in new[] { ("startingPortIndex", Institutions.StartingPortIndex), ("mintPortIndex", Institutions.MintPortIndex),
                ("silverPortIndex", Institutions.SilverPortIndex), ("shipyardPortIndex", Institutions.ShipyardPortIndex) })
                if (pair.Item2.HasValue && (pair.Item2.Value < 0 || pair.Item2.Value >= TownCount.Min))
                    throw new WorldGenerationException("institutions." + pair.Item1 + " must be a zero-based town index below townCount.min.");
            if (Institutions.MintPortIndex.HasValue && Institutions.MintPortIndex == Institutions.SilverPortIndex)
                throw new WorldGenerationException("Mint and Silver source require separate towns.");
            var catalog = content ?? GenerationContent.Catalog;
            if (catalog.HasExpandingEconomy) catalog = catalog.ForExpandingEconomy();
            if (EnabledGoods.Count == 0 || EnabledGoods.Count > 64 || EnabledGoods.Distinct(StringComparer.Ordinal).Count() != EnabledGoods.Count)
                throw new WorldGenerationException("enabledGoods must contain 1..64 unique canonical good IDs.");
            if (!EnabledGoods.Contains("bread")) throw new WorldGenerationException("enabledGoods must include bread: living towns require essential Bread consumption.");
            foreach (var good in EnabledGoods)
            {
                if (!catalog.Goods.Any(x => x.Id == good)) throw new WorldGenerationException("Unknown enabled good: " + good + ".");
                GenerationEconomics.Good(good);
            }
            if (Industries.Count == 0 || Industries.Count > 64 || Industries.Select(x => x.IndustryId).Distinct(StringComparer.Ordinal).Count() != Industries.Count)
                throw new WorldGenerationException("industries must contain 1..64 unique canonical producing building IDs.");
            foreach (var distribution in Industries)
            {
                if (distribution.PublicTowns == null || distribution.PrivateTowns == null) throw new WorldGenerationException("Industry " + distribution.IndustryId + " needs publicTowns and privateTowns.");
                distribution.PublicTowns.Validate(distribution.IndustryId + ".publicTowns", 0, 64);
                distribution.PrivateTowns.Validate(distribution.IndustryId + ".privateTowns", 0, 64);
                if (Math.Max(distribution.PublicTowns.Min, distribution.PrivateTowns.Min) > TownCount.Min)
                    throw new WorldGenerationException("Industry " + distribution.IndustryId + " minimum availability exceeds townCount.min.");
                if (Math.Max(distribution.PublicTowns.Max, distribution.PrivateTowns.Max) == 0)
                    throw new WorldGenerationException("Enabled industry " + distribution.IndustryId + " must permit at least one public or private town.");
                var recipes = catalog.Recipes.Where(x => x.BuildingId == distribution.IndustryId).ToArray();
                if (recipes.Length == 0) throw new WorldGenerationException("Unknown producing industry: " + distribution.IndustryId + ".");
                GenerationEconomics.Industry(distribution.IndustryId);
                if (EnforceProductionSeparation && recipes.Any(r => r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.ShipMaterialGoodId)) &&
                    Math.Max(distribution.PublicTowns.Min, distribution.PrivateTowns.Min) >= TownCount.Min)
                    throw new WorldGenerationException("Industry " + distribution.IndustryId + " requires every town at townCount.min, leaving no shipyard without local hull-material production.");
                if (EnforceProductionSeparation && ProductionSpecialization.Conflict(catalog, recipes.Select(r => r.Id)) != null)
                    throw new WorldGenerationException("Industry " + distribution.IndustryId + " contains directly dependent recipes and cannot satisfy production separation as a single capability.");
                foreach (var recipe in recipes)
                    foreach (var quantity in recipe.Inputs.Concat(recipe.Outputs))
                        if (!EnabledGoods.Contains(quantity.GoodId)) throw new WorldGenerationException("Industry " + distribution.IndustryId + " requires enabled good " + quantity.GoodId + ".");
            }
            foreach (var stock in StartingInventories)
            {
                if (!EnabledGoods.Contains(stock.Key)) throw new WorldGenerationException("startingInventories references disabled/unknown good " + stock.Key + ".");
                if (stock.Value == null) throw new WorldGenerationException("startingInventories." + stock.Key + " needs a range.");
                stock.Value.Validate("startingInventories." + stock.Key, 0, 10000);
            }
            var enabledRecipes = catalog.Recipes.Where(x => Industries.Any(d => d.IndustryId == x.BuildingId)).ToArray();
            if (RequireSupplyChains)
            {
                var obtainable = new HashSet<string>(StringComparer.Ordinal);
                var pending = enabledRecipes.ToList();
                for (var pass = 0; pass < enabledRecipes.Length; pass++)
                    foreach (var recipe in pending.ToArray())
                        if (recipe.Inputs.All(x => obtainable.Contains(x.GoodId)))
                        { foreach (var output in recipe.Outputs) obtainable.Add(output.GoodId); pending.Remove(recipe); }
                if (pending.Count != 0)
                    throw new WorldGenerationException("Enabled production has no renewable source for inputs: " + string.Join(", ", pending.SelectMany(x => x.Inputs).Where(x => !obtainable.Contains(x.GoodId)).Select(x => x.GoodId).Distinct().OrderBy(x => x, StringComparer.Ordinal)) + ". Enable the missing upstream industries or set requireSupplyChains=false for a deliberately finite challenge.");
                if (EnabledGoods.Contains("bread") && !obtainable.Contains("bread"))
                    throw new WorldGenerationException("Essential Bread has no enabled renewable supply chain.");
            }
            if (EnforceProductionSeparation)
                foreach (var first in Industries)
                    foreach (var second in Industries.Where(x => string.CompareOrdinal(first.IndustryId, x.IndustryId) < 0))
                        if (enabledRecipes.Where(x => x.BuildingId == first.IndustryId).Any(a => enabledRecipes.Where(x => x.BuildingId == second.IndustryId).Any(b => ProductionSpecialization.AreIncompatible(a, b))) &&
                            Math.Max(first.PublicTowns.Min, first.PrivateTowns.Min) + Math.Max(second.PublicTowns.Min, second.PrivateTowns.Min) > TownCount.Min)
                            throw new WorldGenerationException("Incompatible industries " + first.IndustryId + " and " + second.IndustryId + " require more separate towns than townCount.min.");
        }
    }

    internal struct GenerationSequence
    {
        private uint state;
        internal GenerationSequence(int seed, uint salt = 0) { state = unchecked((uint)seed ^ salt); }
        internal int Next(int upperExclusive)
        {
            if (upperExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(upperExclusive));
            unchecked
            {
                state += 0x9E3779B9u; var value = state;
                value = (value ^ (value >> 16)) * 0x85EBCA6Bu; value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                return (int)((value ^ (value >> 16)) % (uint)upperExclusive);
            }
        }
    }

    internal static class GenerationContent
    {
        private static readonly Lazy<ContentCatalog> Content = new Lazy<ContentCatalog>(() => ContentLoader.Load(ReadResource("CanonicalContent.json")).ForExpandingEconomy());
        internal static ContentCatalog Catalog => Content.Value;
        internal static string ReadResource(string name)
        {
            using var stream = typeof(GenerationContent).Assembly.GetManifestResourceStream("SaltAndLedger." + name)
                ?? throw new InvalidOperationException("Missing canonical generation resource: " + name);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
