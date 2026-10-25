using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SaltAndLedger.Simulation
{
    /// <summary>Read-only, bounded diagnostics over a committed snapshot. The host owns export paths.</summary>
    public static class RunDiagnosticReport
    {
        public const int FormatVersion = 1;
        public const int MaximumRecentEvents = 100;
        public const int MaximumEventCharacters = 1000;
        public const int MaximumReportCharacters = 4 * 1024 * 1024;

        public static string StateFingerprint(RegionalWorldSnapshot snapshot) =>
            WorldSaveCodec.Fingerprint(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));

        public static string Create(ContentCatalog content, RegionalWorldSnapshot snapshot,
            IEnumerable<string>? recentEvents = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Economy != null) content = content.ForExpandingEconomy();
            var events = (recentEvents ?? Array.Empty<string>()).Take(MaximumRecentEvents + 1).ToArray();
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                Culture = CultureInfo.InvariantCulture,
                Converters = { new StringEnumConverter() }
            };
            var text = JsonConvert.SerializeObject(new
            {
                FormatVersion,
                Purpose = "Diagnostic snapshot, not an import format. Use the companion raw save to restore this run.",
                FullStateFingerprint = StateFingerprint(snapshot),
                Units = new
                {
                    GoodsSubunitsPerUnit = snapshot.QuantityScale,
                    CurrencySubunitsPerCoin = snapshot.MoneyScale,
                    Note = "All quantities and accounts below are exact raw integers. Marginal prices describe the next inventory edge; exact order amounts use authoritative bulk previews. Company Value is a reference valuation, not liquidation proceeds or profit."
                },
                Identity = snapshot.RunIdentity,
                snapshot.Day,
                snapshot.Revision,
                Status = snapshot.Challenge?.Status.ToString() ?? "Sandbox",
                Objectives = new
                {
                    Challenge = snapshot.Challenge,
                    Commission = snapshot.RunIdentity?.Commission == null ? null : GrandCommission.Progress(snapshot),
                    Expanding = snapshot.Challenge != null && ExpandingChallenges.IsExpanding(snapshot.Challenge.Configuration.Kind)
                        ? ExpandingChallenges.Progress(snapshot) : null
                },
                Accounts = new
                {
                    RegionalCash = snapshot.RegionalCash.ToString(CultureInfo.InvariantCulture),
                    AllAccountCash = snapshot.TotalCash.ToString(CultureInfo.InvariantCulture),
                    Note = "Company accounts include NPCs once. Ship cash is a compatibility projection and is deliberately excluded from account totals.",
                    Companies = snapshot.Companies.Values.Select(c => new
                    {
                        c.Id, c.Cash,
                        IsPlayer = snapshot.Ships.Values.Any(s => s.OwnerId == c.Id && s.IsPlayer),
                        ReferenceValue = snapshot.Economy == null ? null : snapshot.CompanyValue(c.Id),
                        Financials = snapshot.Accounting.Companies[c.Id]
                    }).ToArray(),
                    Towns = snapshot.Ports.Values.Select(p => new { p.Id, p.Name, p.MarketCash, HouseholdCash = p.City?.HouseholdCash }).ToArray(),
                    PublicTreasury = snapshot.Economy?.TreasuryCash,
                    ExternalAuthority = snapshot.Economy?.ExternalCash,
                    MintedCurrency = snapshot.Economy?.MintedCurrency,
                    ExternalNetInflow = snapshot.Economy?.ExternalNetInflow
                },
                Towns = snapshot.Ports.Values.Select(p => new
                {
                    p.Id, p.Name, p.Role, p.Inventory, p.City,
                    Consumption = p.Consumption,
                    Prices = p.Quotes.OrderBy(q => q.Key, StringComparer.Ordinal).Select(q => new
                    {
                        GoodId = q.Key,
                        CurrentMarginal = snapshot.GetMarketQuote(p.Id, q.Key),
                        Model = q.Value.Pricing,
                        TargetStock = q.Value.Pricing?.TargetStock,
                        Detail = "Bid is paid by the town to a seller; ask is paid by a buyer to the town. Exact bulk execution uses the shared trade preview, not marginal price times quantity."
                    }).ToArray(),
                    PublicFacilities = p.Facilities.Select(f => new
                    {
                        f.Configuration, f.Enabled, f.NextDueDay, f.LastOutcome,
                        EmployerCash = p.MarketCash,
                        Inputs = RecipeInputs(content, f.Configuration.Instance.RecipeId, p.Inventory, snapshot.QuantityScale),
                        Outputs = RecipeOutputs(content, f.Configuration.Instance.RecipeId, p.Inventory, snapshot.QuantityScale)
                    }).ToArray()
                }).ToArray(),
                CompanyStockpiles = snapshot.CompanyStockpiles.Values,
                LegacyFacilityStores = snapshot.UsesPooledCompanyStorage ? Array.Empty<object>() : snapshot.PrivateFacilities.Values.Select(f =>
                    (object)new { StoreId = "facility:" + f.Id, f.OwnerId, f.PortId, f.Capacity, f.StoredUnits, f.Inventory }).ToArray(),
                FifoStores = snapshot.Accounting.Stores.Values.Select(s => new
                {
                    s.StoreId, s.OwnerId, s.TotalUnits, s.TotalCostBasis,
                    Goods = s.Goods.Values.Select(g => new { g.GoodId, g.Quantity, g.CostBasis, OpenLotCount = g.Lots.Count }).ToArray()
                }).ToArray(),
                FifoNote = "Each physical company store appears once. Totals summarize open FIFO lots; exact provenance and intervals are retained in the companion raw save. Material basis excludes wages.",
                Ships = snapshot.Ships.Values.Select(s => new
                {
                    s.Id, s.Name, s.OwnerId, s.IsPlayer, s.CargoCapacity, s.CargoUnits, s.Cargo,
                    s.DockedPortId, s.OriginPortId, s.DestinationPortId, s.RemainingTravelDays, s.VoyageDurationDays,
                    s.AutomationEnabled, s.Policy, s.IndustrialPolicy, s.IndustrialStage,
                    AutomationDecision = s.AutomationEnabled ? RouteStatus(snapshot, s.Id) : null
                }).ToArray(),
                Facilities = snapshot.PrivateFacilities.Values.Select(f =>
                {
                    var store = snapshot.GetFacilityStorage(f.Id);
                    return new
                    {
                        f.Id, f.OwnerId, f.PortId, f.Blueprint, StorageId = store.Id,
                        f.ConstructionStatus, f.ConstructionCompletionDay, f.FirstProductionDay,
                        f.Enabled, f.NextProductionDay, f.LastOutcome,
                        PreviewOutcome = snapshot.PreviewFacilityProduction(f.Id, content),
                        EffectiveOutputStockLimit = snapshot.FacilityOutputStockLimit(f.Id),
                        f.BatchesProduced, f.WagesPaid, EmployerCash = snapshot.Companies[f.OwnerId].Cash,
                        PayrollAvailableNow = snapshot.Companies[f.OwnerId].Cash >= f.Blueprint.WagePerBatch,
                        Inputs = RecipeInputs(content, f.Blueprint.RecipeId, store.Inventory, snapshot.QuantityScale),
                        Outputs = RecipeOutputs(content, f.Blueprint.RecipeId, store.Inventory, snapshot.QuantityScale),
                        StorageFree = store.Capacity - store.StoredUnits,
                        f.ConsumedQuantities, f.ProducedQuantities, f.ProcurementCost, f.SalesRevenue,
                        f.Policy, f.ProcurementWaitReason, f.SalesWaitReason,
                        StatusNote = "LastOutcome is the committed production outcome. Input quantities and payroll availability describe this snapshot, not a reservation or a promise about the next daily phase."
                    };
                }).ToArray(),
                PublicDevelopment = snapshot.Economy,
                SeaRoutes = snapshot.Routes,
                RecentHistory = new
                {
                    IsPartial = true,
                    Note = "Bounded host activity only; not a complete historical ledger. No events are invented for omitted history or a newly loaded session.",
                    MaximumRecentEvents,
                    Truncated = events.Length > MaximumRecentEvents || events.Any(e => e != null && e.Length > MaximumEventCharacters),
                    Events = events.Take(MaximumRecentEvents).Select(e => e == null ? "" : e.Length <= MaximumEventCharacters ? e : e.Substring(0, MaximumEventCharacters) + " [truncated]").ToArray()
                }
            }, settings);
            if (text.Length > MaximumReportCharacters)
                throw new WorldSaveException("The diagnostic report exceeds the supported size.");
            return text;
        }

        private static object[] RecipeInputs(ContentCatalog content, string recipeId,
            IReadOnlyDictionary<string, int> inventory, int scale) => content.GetRecipe(recipeId).Inputs.Select(input =>
            (object)new { input.GoodId, RequiredForBatch = checked(input.Quantity * scale), Available = inventory[input.GoodId],
                EnoughNow = inventory[input.GoodId] >= checked(input.Quantity * scale) }).ToArray();

        private static object RouteStatus(RegionalWorldSnapshot snapshot, string shipId)
        {
            var decision = RegionalAutomation.Evaluate(snapshot, shipId);
            return new { decision.Action, decision.Stage, decision.Reason, decision.SelectedQuantity, decision.Preview };
        }

        private static object[] RecipeOutputs(ContentCatalog content, string recipeId,
            IReadOnlyDictionary<string, int> inventory, int scale) => content.GetRecipe(recipeId).Outputs.Select(output =>
            (object)new { output.GoodId, PerBatch = checked(output.Quantity * scale), CurrentlyStored = inventory[output.GoodId] }).ToArray();
    }
}
