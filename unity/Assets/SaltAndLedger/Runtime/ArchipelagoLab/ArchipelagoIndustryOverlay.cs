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
                "\nOne batch every " + facility.Blueprint.IntervalDays + " days · next opportunity day " + facility.NextProductionDay +
                (facility.Blueprint.OutputStockLimit.HasValue ? " · output ceiling " + facility.Blueprint.OutputStockLimit.Value : ""));
            SetText("facilityStatsLabel", "Age " + (snapshot.Day - facility.CreatedDay) + " days · " + facility.BatchesProduced + " batches\n" +
                (facility.IsStarterGrant ? "Starter facility grant" : "Construction paid " + Number(facility.ConstructionCost)) +
                " · purchases " + Number(facility.ProcurementCost) +
                " · actual sales " + Number(facility.SalesRevenue) + "\nConsumed: " + QuantityTotals(facility.ConsumedQuantities) +
                "\nProduced: " + QuantityTotals(facility.ProducedQuantities) +
                "\nHeld material basis " + Number(snapshot.Accounting.Stores["facility:" + facility.Id].TotalCostBasis) +
                ". Company FIFO accounting and realized gross margin are in the Ledger.");
            SetText("facilityStateLabel", (facility.Enabled ? ArchipelagoLabSession.FacilityStatus(facility.LastOutcome) : "Operation disabled") +
                "\nProcurement: " + WaitText(facility.ProcurementWaitReason) + " · sales: " + WaitText(facility.SalesWaitReason) +
                "\nInputs on hand: " + string.Join(", ", recipe.Inputs.Select(input => facility.Inventory[input.GoodId] + "/" + input.Quantity +
                    " " + Session.Content.GetGood(input.GoodId).Name)));
            if (loadedPolicyFacilityId != selectedFacilityId) LoadIndustryPolicy(facility);
            Element<Button>("applyIndustryPolicy").SetEnabled(!Session.IsChallengeEnded);
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
            var recipe = Session.Content.GetRecipe(blueprint.RecipeId);
            SetText("constructionLabel", "Investment " + Number(blueprint.ConstructionCost) + " · private capacity " + blueprint.WarehouseCapacity +
                "\n" + (recipe.Inputs.Count == 0 ? "Extraction" : Quantities(recipe.Inputs)) + " → " + Quantities(recipe.Outputs) +
                " / " + blueprint.IntervalDays + " days" + (owned ? " · already owned here" :
                    Session.Player.Cash < blueprint.ConstructionCost ? " · insufficient company cash" : ""));
            Element<Button>("constructButton").SetEnabled(!owned && Session.Player.Cash >= blueprint.ConstructionCost && !Session.IsChallengeEnded);
        }

        private void LoadIndustryPolicy(PrivateFacilitySnapshot facility)
        {
            var policy = facility.Policy;
            // The shared scenario supplies an editable starting proposal. Only Apply commits it.
            var unapplied = policy.Inputs.Count == 0 && policy.Outputs.Count == 0;
            if (unapplied) policy = EnterpriseScenario.DefaultIndustryPolicy(facility.Blueprint.Id);
            SetText("industryPolicyDraftLabel", unapplied
                ? "Suggested draft only. Apply to start automatic input buying and output sales."
                : "Applied operating settings. Editing requires Apply; no day passes.");
            Element<Toggle>("facilityEnabled").SetValueWithoutNotify(facility.Enabled);
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

        private void IndustryDraftChanged() => SetText("industryPolicyDraftLabel", "Draft changed. Apply to use these operating settings.");

        private void ApplyIndustryPolicy()
        {
            Run(() =>
            {
                if (selectedFacilityId == null) throw new InvalidOperationException("Select a private facility.");
                var facility = Session.Snapshot.PrivateFacilities[selectedFacilityId];
                var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
                var inputs = recipe.Inputs.Select(input => new InputStockRule(input.GoodId,
                    ReadInt("inputTarget_" + input.GoodId, "Input target", true), ReadLong("inputPrice_" + input.GoodId, "Buy limit", false))).ToArray();
                var outputs = recipe.Outputs.Select(output => new OutputSaleRule(output.GoodId,
                    ReadInt("outputTarget_" + output.GoodId, "Output reserve", true), ReadLong("outputPrice_" + output.GoodId, "Sell floor", false))).ToArray();
                Session.ConfigureFacility(selectedFacilityId, new IndustryPolicy(Element<Toggle>("procurementEnabled").value,
                    Element<Toggle>("salesEnabled").value, ReadLong("industryCashReserve", "Cash reserve", true), inputs, outputs),
                    Element<Toggle>("facilityEnabled").value);
                SetText("industryPolicyDraftLabel", "Operating policy applied. Transactions run on whole days.");
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
