using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private readonly List<string> blueprintIds = new List<string>();
        private readonly List<string> facilityIds = new List<string>();
        private string selectedFacilityId;
        private string loadedPolicyFacilityId;
        private RegionalWorldSnapshot warehouseSnapshot;

        private void BindIndustry()
        {
            Click("openIndustryButton", () => Run(() =>
            {
                Session.Pause();
                selectedFacilityId = null;
                loadedPolicyFacilityId = null;
                ShowModal("industryBackdrop", "closeIndustryButton");
            }));
            Click("closeIndustryButton", CloseModals);
            SetChoices("storageGoodDropdown", goodIds.Select(id => Session.Content.GetGood(id).Name).ToList());
            Element<DropdownField>("blueprintDropdown").RegisterValueChangedCallback(_ => RefreshConstruction());
            Element<DropdownField>("facilityDropdown").RegisterValueChangedCallback(_ =>
            {
                var index = Element<DropdownField>("facilityDropdown").index;
                selectedFacilityId = index >= 0 && index < facilityIds.Count ? facilityIds[index] : null;
                loadedPolicyFacilityId = null;
                RefreshIndustry();
            });
            Element<DropdownField>("storageGoodDropdown").RegisterValueChangedCallback(_ => RefreshStorage());
            Element<TextField>("storageQuantity").RegisterValueChangedCallback(_ => RefreshStorage());
            Click("constructButton", () => Run(() =>
            {
                var index = Element<DropdownField>("blueprintDropdown").index;
                if (index < 0 || index >= blueprintIds.Count) throw new InvalidOperationException("Choose an eligible investment.");
                var id = blueprintIds[index];
                Session.Construct(id);
                selectedFacilityId = Session.Player.OwnerId + "_" + Session.SelectedPortId + "_" + id;
                loadedPolicyFacilityId = null;
            }));
            Click("applyIndustryPolicy", ApplyIndustryPolicy);
            Click("recommendIndustryPresetButton", () =>
            {
                if (selectedFacilityId == null) return;
                PrepareIndustryDraft(Session.Snapshot.PrivateFacilities[selectedFacilityId], RecommendedIndustryPolicy(
                    Session.Snapshot.PrivateFacilities[selectedFacilityId].Blueprint.Id), true,
                    "Recommended draft only. Review these limits, then Apply to authorize future purchases. Output stays private for shipping.");
            });
            Click("industryShippingButton", () =>
            {
                var facilityId = selectedFacilityId;
                CloseModals();
                OpenLogistics();
                var route = Session.Player.IndustrialPolicy;
                if (facilityId != null && logisticsFacilities.Contains(facilityId) &&
                    (route == null || !LegUsesFacility(route.Outbound, facilityId) && !LegUsesFacility(route.ReturnLeg, facilityId)))
                {
                    SetIndex("logisticsFacility", logisticsFacilities.IndexOf(facilityId));
                    LoadLogisticsPreset();
                }
            });
            foreach (var name in new[] { "facilityEnabled", "procurementEnabled", "salesEnabled" })
                Element<Toggle>(name).RegisterValueChangedCallback(_ => IndustryDraftChanged());
            Element<TextField>("industryCashReserve").RegisterValueChangedCallback(_ => IndustryDraftChanged());
            Click("shipToWarehouse", () => TransferStorage(TransferDirection.ShipToWarehouse));
            Click("warehouseToShip", () => TransferStorage(TransferDirection.WarehouseToShip));
            Click("warehouseBuy", () => TradeStorage(TradeDirection.Buy));
            Click("warehouseSell", () => TradeStorage(TradeDirection.Sell));
        }

        private void RefreshIndustry()
        {
            if (!IndustryIsOpen) return;
            var snapshot = Session.Snapshot;
            var portId = Session.SelectedPortId;
            SetText("industryTitle", PortName(portId) + " · company cash " + Number(Session.Player.Cash));
            var eligible = snapshot.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(portId))
                .OrderBy(b => b.Id, StringComparer.Ordinal).Select(b => b.Id).ToArray();
            if (!blueprintIds.SequenceEqual(eligible))
            {
                blueprintIds.Clear(); blueprintIds.AddRange(eligible);
                SetChoices("blueprintDropdown", blueprintIds.Count == 0 ? new List<string> { "No eligible facilities" } :
                    blueprintIds.Select(id => Session.Content.GetBuilding(snapshot.IndustryBlueprints[id].BuildingId).Name).ToList());
            }
            RefreshConstruction();
            var owned = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId && f.PortId == portId)
                .OrderBy(f => f.Id, StringComparer.Ordinal).ToArray();
            facilityIds.Clear(); facilityIds.AddRange(owned.Select(f => f.Id));
            if (!facilityIds.Contains(selectedFacilityId)) selectedFacilityId = facilityIds.FirstOrDefault();
            var field = Element<DropdownField>("facilityDropdown");
            field.choices = owned.Length == 0 ? new List<string> { "No private facilities here" } :
                owned.Select(f => Session.Content.GetBuilding(f.Blueprint.BuildingId).Name).ToList();
            field.SetValueWithoutNotify(field.choices[Math.Max(0, facilityIds.IndexOf(selectedFacilityId))]);
            field.SetEnabled(owned.Length > 0);
            Element<VisualElement>("facilityBody").EnableInClassList("hidden", owned.Length == 0);
            Element<Label>("noFacilityLabel").EnableInClassList("hidden", owned.Length > 0);
            if (selectedFacilityId == null) return;
            var facility = snapshot.PrivateFacilities[selectedFacilityId];
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            SetText("facilityRecipeLabel", Session.Content.GetBuilding(facility.Blueprint.BuildingId).Name + " · private production\n" +
                (recipe.Inputs.Count == 0 ? "Natural extraction" : Quantities(recipe.Inputs)) + " → " + Quantities(recipe.Outputs) +
                "\nOne batch every " + facility.Blueprint.IntervalDays + " days" +
                (facility.Blueprint.OutputStockLimit.HasValue ? " · output ceiling " + facility.Blueprint.OutputStockLimit.Value : ""));
            SetText("facilityStatsLabel", "Age " + (snapshot.Day - facility.CreatedDay) + " days · " + facility.BatchesProduced + " batches\n" +
                "Capital paid " + Number(facility.IsStarterGrant ? 0 : facility.ConstructionCost) +
                (facility.IsStarterGrant ? " · starter facility grant" : " · construction paid once at order") +
                "\nLocal inventory purchases " + Number(facility.ProcurementCost) +
                " · actual local sale revenue " + Number(facility.SalesRevenue) + "\nConsumed: " + QuantityTotals(facility.ConsumedQuantities) +
                "\nProduced: " + QuantityTotals(facility.ProducedQuantities) +
                "\nHeld material basis " + Number(snapshot.Accounting.Stores["facility:" + facility.Id].TotalCostBasis) +
                ". Transferred inputs retain their original paid basis. These purchase and revenue totals are not profit; company realized material margin is in the Ledger.");
            SetText("facilityStateLabel", IndustryStatus(facility));
            SetText("facilityScheduleLabel", FacilitySchedule(facility));
            SetText("facilityInputStatusLabel", InputAvailability(facility));
            SetText("industryAppliedPolicyLabel", "APPLIED NOW\n" + (facility.Enabled ? "Production enabled" : "Production disabled") +
                " · input buying " + (facility.Policy.ProcurementEnabled ? "on" : "off") +
                " · local output sales " + (facility.Policy.SalesEnabled ? "on" : "off") +
                "\nLast automatic input phase: " + WaitText(facility.ProcurementWaitReason) +
                "; last output phase: " + WaitText(facility.SalesWaitReason));
            SetText("industryShippingLabel", FacilityShipping(facility));
            if (loadedPolicyFacilityId != selectedFacilityId) LoadIndustryPolicy(facility);
            Element<Button>("applyIndustryPolicy").SetEnabled(!Session.IsChallengeEnded);
            Element<Button>("recommendIndustryPresetButton").SetEnabled(!Session.IsChallengeEnded);
            RefreshStorage();
            RefreshModalStatus();
        }

        private void RefreshConstruction()
        {
            var index = Element<DropdownField>("blueprintDropdown").index;
            if (index < 0 || index >= blueprintIds.Count)
            {
                SetText("constructionLabel", "No private construction is offered at this town.");
                Element<Button>("constructButton").SetEnabled(false);
                return;
            }
            var blueprint = Session.Snapshot.IndustryBlueprints[blueprintIds[index]];
            var owned = Session.Snapshot.PrivateFacilities.Values.Any(f => f.OwnerId == Session.Player.OwnerId &&
                f.PortId == Session.SelectedPortId && f.Blueprint.Id == blueprint.Id);
            ConstructionSchedule schedule;
            try { schedule = Session.Snapshot.PreviewConstructionSchedule(Session.Player.OwnerId, blueprint.Id); }
            catch (OverflowException)
            {
                SetText("constructionLabel", "The construction schedule exceeds the supported day range.");
                Element<Button>("constructButton").SetEnabled(false);
                return;
            }
            var delayed = schedule.DelayDays > 0;
            SetText("constructionLabel", owned ? "Already owned here · storage " + blueprint.WarehouseCapacity +
                "\nInspect its actual status and schedule below." :
                "Pay " + Number(blueprint.ConstructionCost) + " now · storage " + blueprint.WarehouseCapacity +
                "\n" + (delayed ? "Ready day " + schedule.CompletionDay : "Operational immediately") +
                " · first batch eligible day " + schedule.FirstProductionDay +
                (Session.Player.Cash < blueprint.ConstructionCost ? " · insufficient company cash" : ""));
            SetText("constructionTimingLabel", (delayed
                ? "Ascension 1: ordering spends no day. The site completes after " + schedule.DelayDays + " Advance Day operations; automatic purchases, production and sales stay off through that completion day. "
                : "Ascension 0: ordering creates an operational facility without advancing the day. ") +
                "Its first batch is eligible one full " + blueprint.IntervalDays + "-day production interval after completion, if enabled and supplied. " +
                "The full cost goes to this town once at order; the site starts empty. Manual storage and local trades remain available under ordinary ownership, capacity and docking rules. " +
                "Inherited charter factories are already operational and keep their original schedule under either ascension.");
            Element<Button>("constructButton").SetEnabled(!owned && Session.Player.Cash >= blueprint.ConstructionCost && !Session.IsChallengeEnded);
        }

        private string IndustryStatus(PrivateFacilitySnapshot facility)
        {
            if (!facility.IsOperational) return "UNDER CONSTRUCTION · " + facility.ConstructionDaysRemaining + " days remaining";
            if (!facility.Enabled) return "DISABLED · Apply an enabled operating policy to resume.";
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            var missing = recipe.Inputs.Where(input => facility.Inventory[input.GoodId] < input.Quantity).ToArray();
            if (missing.Length > 0)
                return "INPUT-STARVED · " + string.Join(", ", missing.Select(input =>
                    Number(input.Quantity - facility.Inventory[input.GoodId]) + " " + Session.Content.GetGood(input.GoodId).Name + " missing")) +
                    (facility.ProcurementWaitReason == IndustryWaitReason.BuyPriceLimit ? "\nPRICE-BLOCKED at the last automatic input phase." : "");
            if (facility.LastOutcome == ScheduledProductionOutcome.WarehouseCapacity)
                return "WAREHOUSE-FULL at the last production opportunity · transfer or sell stock, then retry on a day advance.";
            if (facility.LastOutcome == ScheduledProductionOutcome.OutputLimit)
                return "OUTPUT LIMIT reached at the last production opportunity · move or sell output to make room.";
            return "OPERATIONAL · " + ArchipelagoLabSession.FacilityStatus(facility.LastOutcome) +
                (facility.Policy.SalesEnabled && facility.SalesWaitReason != IndustryWaitReason.None &&
                    facility.SalesWaitReason != IndustryWaitReason.ReserveRetained
                    ? "\nLocal sale waiting: " + WaitText(facility.SalesWaitReason) : "");
        }

        private string FacilitySchedule(PrivateFacilitySnapshot facility)
        {
            var schedule = !facility.IsOperational
                ? "Ordered day " + facility.OrderedDay + " · completion day " + facility.ConstructionCompletionDay +
                    "\nFirst eligible batch: day " + facility.FirstProductionDay + ". Automatic buying, production and selling stay off through completion day."
                : (facility.IsStarterGrant ? "Already-built starter grant; no ascension construction delay.\n" : "Operational since day " + facility.ConstructionCompletionDay + ".\n") +
                    (facility.NextProductionDay <= Session.Snapshot.Day ? "Due since day " + facility.NextProductionDay + "; next day advance retries." :
                        "Next eligible batch: day " + facility.NextProductionDay + ".");
            return schedule + "\nA schedule is an opportunity, not a guarantee: operation, inputs and storage must permit the batch.";
        }

        private string InputAvailability(PrivateFacilitySnapshot facility)
        {
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            if (recipe.Inputs.Count == 0) return "This extraction recipe needs no traded inputs.";
            return "INPUTS FOR ONE BATCH\n" + string.Join("\n", recipe.Inputs.Select(input =>
            {
                var missing = Math.Max(0, input.Quantity - facility.Inventory[input.GoodId]);
                var market = Session.Snapshot.Ports[facility.PortId];
                var quote = Session.Snapshot.GetMarketQuote(facility.PortId, input.GoodId);
                var text = Session.Content.GetGood(input.GoodId).Name + ": private " + facility.Inventory[input.GoodId] + "/" + input.Quantity +
                    " · missing " + missing + " · town stock " + market.Inventory[input.GoodId] +
                    " · next buy " + (quote.Ask.HasValue ? Number(quote.Ask.Value) : "unavailable");
                if (missing == 0) return text;
                try
                {
                    var preview = Session.Snapshot.PreviewWarehouseTrade(facility.OwnerId, facility.Id,
                        new TradeCommand(TradeDirection.Buy, input.GoodId, missing));
                    return text + "\nManual missing-input order: " + (preview.Outcome == TradeOutcome.Traded
                        ? "exact " + Number(preview.TotalAmount) + " for " + missing + "." : WarehouseTradeOutcome(preview.Outcome) + ".");
                }
                catch (OverflowException) { return text + "\nMissing-input order exceeds financial bounds."; }
            })) + "\nEach quote is independent and read-only. Automatic buying also respects its applied limits and cash reserve.";
        }

        private string FacilityShipping(PrivateFacilitySnapshot facility)
        {
            var ship = Session.Player;
            var route = ship.IndustrialPolicy;
            var connected = route != null && (LegUsesFacility(route.Outbound, facility.Id) || LegUsesFacility(route.ReturnLeg, facility.Id));
            if (!connected) return "NO PHYSICAL SHIPPING LINK\n" + ship.Name + " does not currently use this warehouse. Open Industrial shipping, choose this source, review a draft and Apply; enablement is separate.";
            return "PHYSICAL SHIPPING · " + ship.Name + "\n" + (ship.AutomationEnabled ? "Enabled" : "Disabled") +
                " · stage " + ship.IndustrialStage + " · cargo " + ship.CargoUnits + "/" + ship.CargoCapacity +
                "\n" + Session.CurrentDecision.Reason + "\n" + PortName(route.OriginPortId) + " ↔ " + PortName(route.DestinationPortId);
        }

        private static bool LegUsesFacility(IndustrialRouteLeg leg, string id) =>
            leg != null && (leg.SourceFacilityId == id || leg.DestinationFacilityId == id);

        private static IndustryPolicy RecommendedIndustryPolicy(string blueprintId)
        {
            var preset = EnterpriseScenario.DefaultIndustryPolicy(blueprintId);
            return new IndustryPolicy(preset.ProcurementEnabled, false, preset.CashReserve, preset.Inputs, preset.Outputs);
        }

        private void LoadIndustryPolicy(PrivateFacilitySnapshot facility)
        {
            var policy = facility.Policy;
            // The shared scenario supplies an editable starting proposal. Only Apply commits it.
            var unapplied = policy.Inputs.Count == 0 && policy.Outputs.Count == 0;
            if (unapplied) policy = RecommendedIndustryPolicy(facility.Blueprint.Id);
            PrepareIndustryDraft(facility, policy, facility.Enabled, unapplied
                ? "Suggested draft only. Review these limits, then Apply to authorize input buying. Output stays private for shipping."
                : "Showing applied rules. Editing or choosing a preset is passive until Apply.");
        }

        private void PrepareIndustryDraft(PrivateFacilitySnapshot facility, IndustryPolicy policy, bool enabled, string message)
        {
            SetText("industryPolicyDraftLabel", message);
            Element<Toggle>("facilityEnabled").SetValueWithoutNotify(enabled);
            Element<Toggle>("procurementEnabled").SetValueWithoutNotify(policy.ProcurementEnabled);
            Element<Toggle>("salesEnabled").SetValueWithoutNotify(policy.SalesEnabled);
            Element<TextField>("industryCashReserve").SetValueWithoutNotify(Number(policy.CashReserve));
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            var inputs = Element<VisualElement>("inputPolicyRows"); inputs.Clear();
            foreach (var input in recipe.Inputs)
            {
                var rule = policy.Inputs.FirstOrDefault(r => r.GoodId == input.GoodId);
                inputs.Add(PolicyRule(input.GoodId, "input", "Target stock", rule?.TargetQuantity ?? input.Quantity,
                    "Buy ≤ per unit", rule?.MaxBuyPrice ?? 1));
            }
            var outputs = Element<VisualElement>("outputPolicyRows"); outputs.Clear();
            foreach (var output in recipe.Outputs)
            {
                var rule = policy.Outputs.FirstOrDefault(r => r.GoodId == output.GoodId);
                outputs.Add(PolicyRule(output.GoodId, "output", "Retain stock", rule?.ReserveQuantity ?? 0,
                    "Sell ≥ per unit", rule?.MinSellPrice ?? 1));
            }
            loadedPolicyFacilityId = facility.Id;
            RefreshIndustryDraftPreview();
        }

        private VisualElement PolicyRule(string goodId, string prefix, string quantityLabel, int quantity, string priceLabel, long price)
        {
            var container = new VisualElement(); container.AddToClassList("policy-rule");
            var label = new Label(Session.Content.GetGood(goodId).Name); label.AddToClassList("policy-rule-name"); container.Add(label);
            var quantityField = new TextField(quantityLabel) { name = prefix + "Target_" + goodId, value = Number(quantity) };
            var priceField = new TextField(priceLabel) { name = prefix + "Price_" + goodId, value = Number(price) };
            quantityField.RegisterValueChangedCallback(_ => IndustryDraftChanged());
            priceField.RegisterValueChangedCallback(_ => IndustryDraftChanged());
            container.Add(quantityField);
            container.Add(priceField);
            return container;
        }

        private void IndustryDraftChanged()
        {
            SetText("industryPolicyDraftLabel", "Draft changed. Review the preview, then Apply to authorize these operating settings.");
            RefreshIndustryDraftPreview();
        }

        private IndustryPolicy ReadIndustryDraft()
        {
            if (selectedFacilityId == null) throw new InvalidOperationException("Select a private facility.");
            var facility = Session.Snapshot.PrivateFacilities[selectedFacilityId];
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            var inputs = recipe.Inputs.Select(input => new InputStockRule(input.GoodId,
                ReadInt("inputTarget_" + input.GoodId, "Input target", true), ReadLong("inputPrice_" + input.GoodId, "Buy limit", false))).ToArray();
            var outputs = recipe.Outputs.Select(output => new OutputSaleRule(output.GoodId,
                ReadInt("outputTarget_" + output.GoodId, "Output reserve", true), ReadLong("outputPrice_" + output.GoodId, "Sell floor", false))).ToArray();
            return new IndustryPolicy(Element<Toggle>("procurementEnabled").value, Element<Toggle>("salesEnabled").value,
                ReadLong("industryCashReserve", "Cash reserve", true), inputs, outputs);
        }

        private void RefreshIndustryDraftPreview()
        {
            if (selectedFacilityId == null) return;
            try
            {
                var draft = ReadIndustryDraft();
                var lines = new List<string>
                {
                    "REVIEW BEFORE APPLYING",
                    Element<Toggle>("facilityEnabled").value ? "Production enabled; batches still require inputs and space." : "Production disabled.",
                    "Retain company cash " + Number(draft.CashReserve) + "."
                };
                if (draft.ProcurementEnabled)
                    lines.AddRange(draft.Inputs.Select(rule => "Buy " + Session.Content.GetGood(rule.GoodId).Name + " toward " + rule.TargetQuantity +
                        " private units; worst marginal unit at most " + Number(rule.MaxBuyPrice) + "."));
                else lines.Add("Automatic input purchases OFF.");
                if (draft.SalesEnabled)
                    lines.AddRange(draft.Outputs.Select(rule => "Sell " + Session.Content.GetGood(rule.GoodId).Name + " above " + rule.ReserveQuantity +
                        " retained units; worst marginal unit at least " + Number(rule.MinSellPrice) + "."));
                else lines.Add("Automatic local sales OFF. Output stays in private storage until a manual trade or physical route moves it.");
                lines.Add("No order or day is spent by this preview. Apply sets future daily permissions; it does not buy inputs now.");
                SetText("industryPolicyPreviewLabel", string.Join("\n", lines));
            }
            catch (ArgumentException exception) { SetText("industryPolicyPreviewLabel", "Incomplete draft: " + exception.Message); }
            catch (OverflowException) { SetText("industryPolicyPreviewLabel", "Incomplete draft: a numeric value exceeds its allowed range."); }
        }

        private void ApplyIndustryPolicy()
        {
            Run(() =>
            {
                Session.ConfigureFacility(selectedFacilityId, ReadIndustryDraft(), Element<Toggle>("facilityEnabled").value);
                SetText("industryPolicyDraftLabel", "Operating policy applied explicitly. Future whole days may spend cash within these rules; no purchase occurs on Apply.");
            });
        }

        private int ReadInt(string name, string label, bool allowZero)
        {
            var value = ReadLong(name, label, allowZero);
            if (value > int.MaxValue) throw new ArgumentException(label + " must fit a whole inventory quantity.");
            return (int)value;
        }

        private void RefreshStorage()
        {
            if (!IndustryIsOpen || selectedFacilityId == null || !Session.Snapshot.PrivateFacilities.ContainsKey(selectedFacilityId)) return;
            warehouseSnapshot = Session.Snapshot;
            var facility = warehouseSnapshot.PrivateFacilities[selectedFacilityId];
            var player = Session.Player;
            SetText("storageCapacityLabel", "Your ship " + player.CargoUnits + "/" + player.CargoCapacity +
                "   ·   Private warehouse " + facility.StoredUnits + "/" + facility.Capacity + "   ·   Public market separate");
            var rows = Element<VisualElement>("storageRows"); rows.Clear();
            foreach (var goodId in goodIds)
            {
                var row = new VisualElement(); row.AddToClassList("storage-row");
                var name = new Label(Session.Content.GetGood(goodId).Name); name.AddToClassList("trade-good-name"); row.Add(name);
                foreach (var quantity in new[] { player.Cargo[goodId], facility.Inventory[goodId], warehouseSnapshot.Ports[facility.PortId].Inventory[goodId] })
                {
                    var cell = new Label(Number(quantity)); cell.AddToClassList("storage-cell"); row.Add(cell);
                }
                rows.Add(row);
            }
            var good = goodIds[Math.Max(0, Element<DropdownField>("storageGoodDropdown").index)];
            var valid = int.TryParse(Element<TextField>("storageQuantity").value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var amount) && amount > 0;
            var local = player.DockedPortId == facility.PortId && !player.AutomationEnabled && !Session.IsChallengeEnded;
            SetText("transferAvailabilityLabel", Session.IsChallengeEnded ? "The charter has ended. Start a new scenario to move or trade goods." :
                local ? "Docked locally: move your own cargo without buying or selling." :
                player.AutomationEnabled ? "Disable the ship route before moving cargo manually." :
                "Ship transfers require docking at " + PortName(facility.PortId) + ". Local warehouse trading is separate.");
            Element<Button>("shipToWarehouse").SetEnabled(local && valid && player.Cargo[good] >= amount && facility.Capacity - facility.StoredUnits >= amount);
            Element<Button>("warehouseToShip").SetEnabled(local && valid && facility.Inventory[good] >= amount && player.CargoCapacity - player.CargoUnits >= amount);
            foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
            {
                var prefix = direction == TradeDirection.Buy ? "warehouseBuy" : "warehouseSell";
                TradeAttempt preview = null;
                string text;
                try
                {
                    preview = valid ? warehouseSnapshot.PreviewWarehouseTrade(player.OwnerId, facility.Id, new TradeCommand(direction, good, amount)) : null;
                    text = preview?.Price == null ? (preview == null ? "Enter a positive quantity." : WarehouseTradeOutcome(preview.Outcome)) :
                        "Exact " + Number(preview.TotalAmount) + " · average " + AverageExecutionPrice(preview) +
                        "\n" + (preview.Outcome == TradeOutcome.Traded ? "Local transfer of ownership" : WarehouseTradeOutcome(preview.Outcome));
                }
                catch (OverflowException) { text = "Quantity exceeds financial bounds."; }
                SetText(prefix + "Preview", text);
                Element<Button>(prefix).SetEnabled(!Session.IsChallengeEnded && preview?.Outcome == TradeOutcome.Traded);
            }
        }

        private static string WarehouseTradeOutcome(TradeOutcome outcome) =>
            outcome == TradeOutcome.InsufficientTraderInventory ? "Private storage does not contain enough of this good" :
            TradingLabText.Outcome(outcome);

        private void TransferStorage(TransferDirection direction) => Run(() => Session.Transfer(selectedFacilityId, direction,
            goodIds[Element<DropdownField>("storageGoodDropdown").index], ReadInt("storageQuantity", "Transfer quantity", false)));

        private void TradeStorage(TradeDirection direction)
        {
            Run(() => Session.WarehouseTrade(selectedFacilityId, direction, goodIds[Element<DropdownField>("storageGoodDropdown").index],
                ReadInt("storageQuantity", "Trade quantity", false), warehouseSnapshot));
            RefreshStorage();
        }

        private string QuantityTotals(IReadOnlyDictionary<string, long> values)
        {
            var positive = values.Where(pair => pair.Value > 0).ToArray();
            return positive.Length == 0 ? "none" : string.Join(", ", positive.Select(pair =>
                Number(pair.Value) + " " + Session.Content.GetGood(pair.Key).Name));
        }

        private static string WaitText(IndustryWaitReason reason) => System.Text.RegularExpressions.Regex.Replace(reason.ToString(), "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
    }
}
