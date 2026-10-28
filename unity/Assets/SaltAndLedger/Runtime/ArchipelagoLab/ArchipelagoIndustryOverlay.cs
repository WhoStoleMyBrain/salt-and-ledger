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
        private RegionalWorldSnapshot productionDraftSnapshot;

        private void BindIndustry()
        {
            Click("openIndustryButton", () => Run(() =>
            {
                Session.Pause();
                SelectIndustryPane(false);
                Element<ScrollView>("industryPolicyScroll").scrollOffset = UnityEngine.Vector2.zero;
                ShowModal("industryBackdrop", "closeIndustryButton");
            }));
            Click("closeIndustryButton", CloseModals);
            Click("industryFactoriesTab", () => SelectIndustryPane(false));
            Click("industryWarehouseTab", () => SelectIndustryPane(true));
            Click("factoryWarehouseButton", () => SelectIndustryPane(true));
            SetChoices("facilityPriority", new List<string> { "Low", "Normal", "High" });
            SetChoices("storageGoodDropdown", goodIds.Select(id => Session.Content.GetGood(id).Name).ToList());
            Element<DropdownField>("blueprintDropdown").RegisterValueChangedCallback(_ => RefreshConstruction());
            Element<DropdownField>("facilityDropdown").RegisterValueChangedCallback(_ =>
            {
                var index = Element<DropdownField>("facilityDropdown").index;
                selectedFacilityId = index >= 0 && index < facilityIds.Count ? facilityIds[index] : null;
                loadedPolicyFacilityId = null;
                Element<ScrollView>("industryPolicyScroll").scrollOffset = UnityEngine.Vector2.zero;
                RefreshIndustry();
            });
            Element<DropdownField>("storageGoodDropdown").RegisterValueChangedCallback(_ => RefreshStorage());
            Element<TextField>("storageQuantity").RegisterValueChangedCallback(_ => RefreshStorage());
            Click("constructButton", () => Run(() =>
            {
                var index = Element<DropdownField>("blueprintDropdown").index;
                if (index < 0 || index >= blueprintIds.Count) throw new InvalidOperationException("Choose an eligible investment.");
                var id = blueprintIds[index];
                var before = new HashSet<string>(Session.Snapshot.PrivateFacilities.Keys);
                Session.Construct(id);
                selectedFacilityId = Session.Snapshot.PrivateFacilities.Keys.FirstOrDefault(key => !before.Contains(key)) ?? selectedFacilityId;
                loadedPolicyFacilityId = null;
                Element<ScrollView>("industryPolicyScroll").scrollOffset = UnityEngine.Vector2.zero;
            }));
            Click("applyIndustryPolicy", ApplyIndustryPolicy);
            Click("industryShippingButton", () => OpenRoutePlanner(Session.Player.Id));
            Element<Toggle>("facilityEnabled").RegisterValueChangedCallback(_ => IndustryDraftChanged());
            Element<TextField>("facilityOperatingRate").RegisterValueChangedCallback(_ => IndustryDraftChanged());
            Element<DropdownField>("facilityPriority").RegisterValueChangedCallback(_ => IndustryDraftChanged());
            foreach (var rate in new[] { 25, 50, 75, 100 })
            {
                var value = rate;
                Click("facilityRate" + rate, () => Element<TextField>("facilityOperatingRate").value = Number(value));
            }
            BindWarehouseAutomation();
            Click("shipToWarehouse", () => TransferStorage(TransferDirection.ShipToWarehouse));
            Click("warehouseToShip", () => TransferStorage(TransferDirection.WarehouseToShip));
            Click("warehouseBuy", () => TradeStorage(TradeDirection.Buy));
            Click("warehouseSell", () => TradeStorage(TradeDirection.Sell));
        }

        private void SelectIndustryPane(bool warehouse)
        {
            Element<VisualElement>("industryFactoriesPane").EnableInClassList("hidden", warehouse);
            Element<VisualElement>("industryWarehousePane").EnableInClassList("hidden", !warehouse);
            Element<Button>("industryFactoriesTab").EnableInClassList("selected-tab", !warehouse);
            Element<Button>("industryWarehouseTab").EnableInClassList("selected-tab", warehouse);
            if (IndustryIsOpen) RefreshIndustry();
        }

        private void ClearIndustryDrafts()
        {
            selectedFacilityId = null;
            loadedPolicyFacilityId = null;
            loadedWarehousePortId = null;
            illustratedRecipe = null;
            Element<ScrollView>("industryPolicyScroll").scrollOffset = UnityEngine.Vector2.zero;
            SelectWarehouseDirection(false);
            SelectIndustryPane(false);
        }

        private void RefreshIndustry()
        {
            if (!IndustryIsOpen) return;
            var snapshot = Session.Snapshot;
            var portId = Session.SelectedPortId;
            SetText("industryTitle", PortName(portId) + " · company cash " + Money(Session.Player.Cash));
            var eligible = snapshot.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(portId))
                .OrderBy(b => b.Id, StringComparer.Ordinal).Select(b => b.Id).ToArray();
            if (!blueprintIds.SequenceEqual(eligible))
            {
                blueprintIds.Clear(); blueprintIds.AddRange(eligible);
                SetChoices("blueprintDropdown", blueprintIds.Count == 0 ? new List<string> { "No eligible facilities" } :
                    blueprintIds.Select(id => Session.Content.GetBuilding(snapshot.IndustryBlueprints[id].BuildingId).Name).ToList());
            }
            SetText("industryEligibilityLabel", "Eligible here: " + (eligible.Length == 0 ? "none" : string.Join(", ", eligible.Select(id =>
                Session.Content.GetBuilding(snapshot.IndustryBlueprints[id].BuildingId).Name))));
            Element<Label>("industryEligibilityLabel").tooltip = "Only these industries can be built here. All goods can still be traded; inspect Economy for public producers and current inputs.";
            RefreshConstruction();
            var owned = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId && f.PortId == portId)
                .OrderBy(f => f.Id, StringComparer.Ordinal).ToArray();
            facilityIds.Clear(); facilityIds.AddRange(owned.Select(f => f.Id));
            if (!facilityIds.Contains(selectedFacilityId)) selectedFacilityId = facilityIds.FirstOrDefault();
            var field = Element<DropdownField>("facilityDropdown");
            field.choices = owned.Length == 0 ? new List<string> { "No private facilities here" } :
                owned.Select(f => Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " [" + f.Id + "]").ToList();
            field.SetValueWithoutNotify(field.choices[Math.Max(0, facilityIds.IndexOf(selectedFacilityId))]);
            field.SetEnabled(owned.Length > 0);
            Element<VisualElement>("facilityBody").EnableInClassList("hidden", owned.Length == 0);
            Element<Label>("noFacilityLabel").EnableInClassList("hidden", owned.Length > 0);
            RefreshWarehouseAutomation();
            if (selectedFacilityId == null) { RefreshModalStatus(); return; }
            var facility = snapshot.PrivateFacilities[selectedFacilityId];
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            RefreshIndustryRecipeSymbols(facility);
            SetText("facilityRecipeLabel", Session.Content.GetBuilding(facility.Blueprint.BuildingId).Name + " · private production\n" +
                (recipe.Inputs.Count == 0 ? "Natural extraction" : Quantities(recipe.Inputs)) + " → " + Quantities(recipe.Outputs) +
                "\nOne batch every " + facility.Blueprint.IntervalDays + " days" +
                (snapshot.FacilityOutputStockLimit(facility.Id).HasValue ? " · installed recipe output ceiling " + Q(snapshot.FacilityOutputStockLimit(facility.Id).Value) : ""));
            SetText("facilityStatsLabel", "Age " + (snapshot.Day - facility.CreatedDay) + " days · " + facility.BatchesProduced + " batches\n" +
                "Capital paid " + Money(facility.IsStarterGrant ? 0 : facility.ConstructionCost) +
                (facility.IsStarterGrant ? " · starter facility grant" : " · construction paid once at order") +
                (facility.Blueprint.WagePerBatch > 0 ? " · paid wages " + Money(facility.WagesPaid) + " (separate operating expense)" : "") +
                "\nConsumed: " + QuantityTotals(facility.ConsumedQuantities) +
                "\nProduced: " + QuantityTotals(facility.ProducedQuantities) +
                "\n" + (snapshot.UsesPooledCompanyStorage ? "Shared town stock basis (once across all facilities) " : "Held material basis ") + Money(snapshot.Accounting.Stores[snapshot.GetFacilityStorage(facility.Id).Id].TotalCostBasis) +
                ". Transferred inputs retain their original paid basis. Warehouse purchases and sales appear on the Warehouse tab. " +
                "Shared inputs can serve other local facilities. " +
                "Company material margin and wages are separate in the Ledger.");
            var opportunity = EconomicSignals.PrivateProduction(snapshot, Session.Content, facility.Id);
            SetText("facilityStateLabel", !facility.IsOperational ? "Under construction · " + facility.ConstructionDaysRemaining + " days remaining" :
                "Now: " + (opportunity.ReadyFromCurrentState ? "Ready with current inputs, cash and space" :
                    ArchipelagoLabSession.FacilityStatus(opportunity.CurrentReadiness)));
            Element<Label>("facilityStateLabel").tooltip = IndustryStatus(facility) +
                "\nCurrent stock and funds only; no reservation against intervening actors.";
            SetText("facilityInputsSummary", opportunity.Inputs.Count == 0 ? "Extraction · no material inputs" :
                "Stock / batch: " + string.Join(" · ", opportunity.Inputs.Select(input => Session.Content.GetGood(input.GoodId).Name +
                    " " + Q(input.CurrentStock) + "/" + Q(input.Quantity))));
            SetText("facilityScheduleLabel", (!facility.IsOperational ? "First batch D" + facility.FirstProductionDay :
                opportunity.NextRateAdmittedDay.HasValue ? "Next eligible D" + opportunity.NextRateAdmittedDay.Value : "Production paused") +
                " · full wage " + Money(facility.Blueprint.WagePerBatch) + " / batch");
            Element<Label>("facilityScheduleLabel").tooltip = FacilitySchedule(facility);
            SetText("facilityScheduleDetail", FacilitySchedule(facility));
            SetText("facilityFlowLabel", "Next 12d potential: " + string.Join(" · ", recipe.Outputs.Select(output =>
            {
                var flow = EconomicSignals.CommodityFlow(snapshot, Session.Content, portId, output.GoodId, facility.OwnerId);
                var own = flow.Production.First(item => item.Opportunity.FacilityId == facility.Id);
                return SignedQuantity(own.IsActive ? own.OutputQuantity : 0) + " " + Session.Content.GetGood(output.GoodId).Name;
            })) + " · requires inputs / wages / space");
            SetText("facilityInputStatusLabel", InputAvailability(facility));
            SetText("industryAppliedPolicyLabel", "APPLIED · " + (facility.Enabled ? "Enabled" : "Disabled") +
                " · " + facility.OperatingRate + "% · " + facility.Priority + " priority");
            SetText("industryShippingLabel", FacilityShipping(facility));
            if (loadedPolicyFacilityId != selectedFacilityId) LoadIndustryPolicy(facility);
            productionDraftSnapshot = snapshot;
            RefreshIndustryDraftPreview();
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
            var owned = Session.Snapshot.Economy == null && Session.Snapshot.PrivateFacilities.Values.Any(f => f.OwnerId == Session.Player.OwnerId &&
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
            SetText("constructionLabel", owned ? "Already owned here · storage " + Q(blueprint.WarehouseCapacity) +
                "\nInspect its actual status and schedule below." :
                "Pay " + Money(blueprint.ConstructionCost) + " now · storage " + Q(blueprint.WarehouseCapacity) +
                "\n" + (delayed ? "Ready day " + schedule.CompletionDay : "Operational immediately") +
                " · first batch eligible day " + schedule.FirstProductionDay +
                (Session.Player.Cash < blueprint.ConstructionCost ? " · insufficient company cash" : ""));
            SetText("constructionTimingLabel", (delayed
                ? "Construction: " + schedule.DelayDays + " day advances. Factory production stays off through completion day.\n"
                : "Operational immediately; ordering spends no day.\n") +
                "First opportunity: " + blueprint.IntervalDays + " days after completion, if enabled and supplied.\n" +
                "Pay the town once; warehouse starts empty. Manual trades/transfers remain available under normal rules. Starter factories keep their schedule.");
            if (blueprint.WagePerBatch > 0)
                SetText("constructionLabel", Element<Label>("constructionLabel").text + " · wage " + Money(blueprint.WagePerBatch) + " / completed batch");
            Element<Button>("constructButton").SetEnabled(!owned && Session.Player.Cash >= blueprint.ConstructionCost && !Session.IsChallengeEnded);
        }

        private string IndustryStatus(PrivateFacilitySnapshot facility)
        {
            if (!facility.IsOperational) return "UNDER CONSTRUCTION · " + facility.ConstructionDaysRemaining + " days remaining";
            if (!facility.Enabled) return "DISABLED · Enable production, then Apply.";
            if (facility.OperatingRate == 0) return "PAUSED · 0% operating rate. Warehouse automation remains independent.";
            var predicted = Session.Snapshot.PreviewFacilityProduction(facility.Id, Session.Content);
            return "CURRENT STOCK/FUNDS: " + (predicted == ScheduledProductionOutcome.Produced ? "Ready with current stocks/funds" : ProductionRemedy(predicted)) +
                "\nLast attempt: " + ArchipelagoLabSession.FacilityStatus(facility.LastOutcome);
        }

        private string FacilitySchedule(PrivateFacilitySnapshot facility)
        {
            var schedule = !facility.IsOperational
                ? "Ordered day " + facility.OrderedDay + " · completion day " + facility.ConstructionCompletionDay +
                    "\nFirst eligible batch: day " + facility.FirstProductionDay + ". Factory production waits through completion."
                : (facility.IsStarterGrant ? "Already-built starter grant; no ascension construction delay.\n" : "Operational since day " + facility.ConstructionCompletionDay + ".\n") +
                    (facility.NextProductionDay <= Session.Snapshot.Day ? "Due since day " + facility.NextProductionDay + "; next day advance retries." :
                        "Next eligible batch: day " + facility.NextProductionDay + ".");
            var opportunity = EconomicSignals.PrivateProduction(Session.Snapshot, Session.Content, facility.Id);
            return schedule + "\nRate-admitted opportunity: " + (opportunity.NextRateAdmittedDay.HasValue ? "day " + opportunity.NextRateAdmittedDay.Value : "paused") +
                " · " + facility.OperatingRate + "% · complete batches only" +
                (facility.Blueprint.WagePerBatch == 0 ? "" : " · payroll " + Money(facility.Blueprint.WagePerBatch) + " / batch");
        }

        private string InputAvailability(PrivateFacilitySnapshot facility)
        {
            var recipe = Session.Content.GetRecipe(facility.Blueprint.RecipeId);
            var storage = Session.Snapshot.GetFacilityStorage(facility.Id);
            if (recipe.Inputs.Count == 0) return "Extraction · no traded inputs";
            return "INPUTS FOR ONE BATCH\n" + string.Join("\n", recipe.Inputs.Select(input =>
            {
                var required = checked(input.Quantity * Session.Snapshot.QuantityScale);
                var missing = Math.Max(0, required - storage.Inventory[input.GoodId]);
                var market = Session.Snapshot.Ports[facility.PortId];
                var quote = Session.Snapshot.GetMarketQuote(facility.PortId, input.GoodId);
                var text = Session.Content.GetGood(input.GoodId).Name + ": warehouse " + Q(storage.Inventory[input.GoodId]) + "/" + Q(required) +
                    " · missing " + Q(missing) + " · town stock " + Q(market.Inventory[input.GoodId]) +
                    " · next buy " + (quote.Ask.HasValue ? Number(quote.Ask.Value) : "unavailable");
                if (missing == 0) return text;
                try
                {
                    var preview = Session.Snapshot.PreviewWarehouseTrade(facility.OwnerId, facility.Id,
                        new TradeCommand(TradeDirection.Buy, input.GoodId, missing));
                    var automatic = Session.Snapshot.PreviewWarehouseAutomation(facility.OwnerId, facility.PortId, TradeDirection.Buy, input.GoodId);
                    return text + "\nWarehouse buy rule: " + WarehousePreviewText(automatic) +
                        "\nManual missing-input order: " + (preview.Outcome == TradeOutcome.Traded
                        ? "exact " + Money(preview.TotalAmount) + " for " + Q(missing) + "." : WarehouseTradeOutcome(preview.Outcome) + ".");
                }
                catch (OverflowException) { return text + "\nMissing-input order exceeds financial bounds."; }
            })) + "\nIndependent quotes. Automatic buying also respects applied limits and the cash reserve.";
        }

        private string FacilityShipping(PrivateFacilitySnapshot facility)
        {
            var ship = Session.Player;
            var stop = ship.Itinerary?.Stops.FirstOrDefault(item => item.PortId == facility.PortId);
            var movesCompanyStock = stop != null && stop.Commands.Any(command => command.Kind == CargoItineraryCommandKind.LoadFromCompanyTownStock ||
                command.Kind == CargoItineraryCommandKind.UnloadToCompanyTownStock);
            return !movesCompanyStock ? "NO CONFIGURED TOWN-STOCK SHIPPING\n" + ship.Name +
                " · no warehouse Load/Unload here. Configure in Route planner." :
                "TOWN-STOCK ITINERARY · " + ship.Name + "\n" + (ship.AutomationEnabled ? "Enabled" : "Manual control") +
                " · " + Session.CurrentDecision.Reason + "\n" + string.Join(" → ", ship.Itinerary.Stops.Select(item => PortName(item.PortId)));
        }

        private void LoadIndustryPolicy(PrivateFacilitySnapshot facility)
        {
            Element<Toggle>("facilityEnabled").SetValueWithoutNotify(facility.Enabled);
            Element<TextField>("facilityOperatingRate").SetValueWithoutNotify(Number(facility.OperatingRate));
            Element<DropdownField>("facilityPriority").SetValueWithoutNotify(facility.Priority.ToString());
            loadedPolicyFacilityId = facility.Id;
            SetText("industryPolicyDraftLabel", "");
        }

        private void IndustryDraftChanged()
        {
            SetText("industryPolicyDraftLabel", "Unapplied changes");
            RefreshIndustryDraftPreview();
        }

        private int ReadOperatingRate()
        {
            var rate = ReadInt("facilityOperatingRate", "Operating rate", true);
            if (rate > 100) throw new ArgumentException("Operating rate must be between 0 and 100%.");
            return rate;
        }

        private ProductionPriority ReadProductionPriority() =>
            (ProductionPriority)Enum.Parse(typeof(ProductionPriority), Element<DropdownField>("facilityPriority").value);

        private void RefreshIndustryDraftPreview()
        {
            if (selectedFacilityId == null) return;
            var valid = false;
            try
            {
                var rate = ReadOperatingRate();
                var enabled = Element<Toggle>("facilityEnabled").value;
                SetText("industryPolicyPreviewLabel", !enabled || rate == 0 ? "Production paused. Warehouse rules remain active." :
                    rate + "% of scheduled opportunities; " + ReadProductionPriority() + " priority. Full inputs and wages per completed batch.");
                valid = true;
            }
            catch (ArgumentException exception)
            {
                SetText("industryPolicyPreviewLabel", exception.Message);
                SetText("industryPolicyDraftLabel", exception.Message);
            }
            catch (OverflowException)
            {
                SetText("industryPolicyPreviewLabel", "Operating rate exceeds its allowed range.");
                SetText("industryPolicyDraftLabel", "Operating rate exceeds its allowed range.");
            }
            Element<Button>("applyIndustryPolicy").SetEnabled(valid && !Session.IsChallengeEnded);
        }

        private void ApplyIndustryPolicy()
        {
            Run(() =>
            {
                Session.ConfigureProduction(selectedFacilityId, Element<Toggle>("facilityEnabled").value,
                    ReadOperatingRate(), ReadProductionPriority(), productionDraftSnapshot);
                SetText("industryPolicyDraftLabel", "Settings applied");
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
            var storage = warehouseSnapshot.GetFacilityStorage(facility.Id);
            var player = Session.Player;
            SetText("storageCapacityLabel", "Ship cargo · " + player.Name + " " + Q(player.CargoUnits) + "/" + Q(player.CargoCapacity) +
                "\nCompany warehouse " + Q(storage.StoredUnits) + "/" + Q(storage.Capacity));
            Element<Label>("storageCapacityLabel").tooltip = "The local company warehouse is shared by your facilities. Town market stock and ship cargo are separate physical stores.";
            var rows = Element<VisualElement>("storageRows"); rows.Clear();
            foreach (var goodId in goodIds)
            {
                var row = new VisualElement(); row.AddToClassList("storage-row");
                var name = new Label(Session.Content.GetGood(goodId).Name); name.AddToClassList("trade-good-name"); row.Add(name);
                foreach (var quantity in new[] { player.Cargo[goodId], storage.Inventory[goodId], warehouseSnapshot.Ports[facility.PortId].Inventory[goodId] })
                {
                    var cell = new Label(Q(quantity)); cell.AddToClassList("storage-cell"); row.Add(cell);
                }
                rows.Add(row);
            }
            var good = goodIds[Math.Max(0, Element<DropdownField>("storageGoodDropdown").index)];
            SetText("storageSelectedStock", Session.Content.GetGood(good).Name + " · ship " + Q(player.Cargo[good]) +
                " · warehouse " + Q(storage.Inventory[good]) + " · market " + Q(warehouseSnapshot.Ports[facility.PortId].Inventory[good]));
            var valid = TryIndustryQuantity("storageQuantity", out var amount);
            var whole = valid && amount % warehouseSnapshot.QuantityScale == 0;
            var local = player.DockedPortId == facility.PortId && !player.AutomationEnabled && !Session.IsChallengeEnded;
            SetText("transferAvailabilityLabel", Session.IsChallengeEnded ? "Run ended · inspection only" :
                local ? "Docked · whole-unit cargo transfers · fractional warehouse trades" :
                player.AutomationEnabled ? "Choose manual control to move cargo." :
                "Load/unload: dock at " + PortName(facility.PortId) + ". Warehouse trading is available remotely.");
            Element<Button>("shipToWarehouse").SetEnabled(local && whole && player.Cargo[good] >= amount && storage.Capacity - storage.StoredUnits >= amount);
            Element<Button>("warehouseToShip").SetEnabled(local && whole && storage.Inventory[good] >= amount && player.CargoCapacity - player.CargoUnits >= amount);
            foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
            {
                var prefix = direction == TradeDirection.Buy ? "warehouseBuy" : "warehouseSell";
                TradeAttempt preview = null;
                string text;
                try
                {
                    preview = valid ? warehouseSnapshot.PreviewWarehouseTrade(player.OwnerId, facility.Id, new TradeCommand(direction, good, amount)) : null;
                    text = preview?.Price == null ? (preview == null ? "Enter a positive quantity." : WarehouseTradeOutcome(preview.Outcome)) :
                        (direction == TradeDirection.Buy ? "Pay " : "Gross ") + Money(preview.TotalAmount) + " · average " + AverageExecutionPrice(preview) +
                        ServiceFeeText(preview) +
                        "\n" + (preview.Outcome == TradeOutcome.Traded ? "Market ↔ Company warehouse" : WarehouseTradeOutcome(preview.Outcome));
                }
                catch (OverflowException) { text = "Quantity exceeds financial bounds."; }
                SetText(prefix + "Preview", text);
                Element<Button>(prefix).SetEnabled(!Session.IsChallengeEnded && preview?.Outcome == TradeOutcome.Traded);
            }
        }

        private static string WarehouseTradeOutcome(TradeOutcome outcome) =>
            outcome == TradeOutcome.InsufficientTraderInventory ? "Company warehouse stock short" :
            EconomicText.Outcome(outcome);

        private void TransferStorage(TransferDirection direction) => Run(() => Session.Transfer(selectedFacilityId, direction,
            goodIds[Element<DropdownField>("storageGoodDropdown").index], ReadIndustryQuantity("storageQuantity", "Transfer quantity", false), warehouseSnapshot));

        private void TradeStorage(TradeDirection direction)
        {
            Run(() => Session.WarehouseTrade(selectedFacilityId, direction, goodIds[Element<DropdownField>("storageGoodDropdown").index],
                ReadIndustryQuantity("storageQuantity", "Trade quantity", false), warehouseSnapshot));
            RefreshStorage();
        }

        private string QuantityTotals(IReadOnlyDictionary<string, long> values)
        {
            var positive = values.Where(pair => pair.Value > 0).ToArray();
            return positive.Length == 0 ? "none" : string.Join(", ", positive.Select(pair =>
                Q(pair.Value) + " " + Session.Content.GetGood(pair.Key).Name));
        }

        private int ReadIndustryQuantity(string name, string label, bool allowZero)
        {
            var value = ReadScaledAmount(name, label, allowZero, Session.Snapshot.QuantityScale);
            if (value > int.MaxValue) throw new ArgumentException(label + " exceeds the supported stock bound.");
            return (int)value;
        }

        private long ReadIndustryMoney(string name, string label, bool allowZero) =>
            ReadScaledAmount(name, label, allowZero, Session.Snapshot.MoneyScale);

        private long ReadScaledAmount(string name, string label, bool allowZero, int scale)
        {
            if (!decimal.TryParse(Element<TextField>(name).value,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ||
                value < 0 || (!allowZero && value == 0))
                throw new ArgumentException(label + " needs a " + (allowZero ? "nonnegative" : "positive") + " amount.");
            var scaled = checked(value * scale);
            if (scaled != decimal.Truncate(scaled) || scaled > long.MaxValue)
                throw new ArgumentException(label + " exceeds the exact quantity/currency precision or bound.");
            return checked((long)scaled);
        }

        private bool TryIndustryQuantity(string name, out int amount)
        {
            try { amount = ReadIndustryQuantity(name, "Quantity", false); return true; }
            catch (ArgumentException) { amount = 0; return false; }
            catch (OverflowException) { amount = 0; return false; }
        }

        private int ReadWholeCargo(string name, string label, bool allowZero) =>
            checked(ReadInt(name, label, allowZero) * Session.Snapshot.QuantityScale);

        private static string WaitText(IndustryWaitReason reason)
        {
            switch (reason)
            {
                case IndustryWaitReason.Disabled: return "off — apply an enabled policy to authorize orders";
                case IndustryWaitReason.UnderConstruction: return "construction — wait until after completion";
                case IndustryWaitReason.InsufficientMarketInventory: return "market stock short — deliver inputs or lower the target";
                case IndustryWaitReason.WarehouseFull: return "storage full — sell/load goods or add capacity";
                case IndustryWaitReason.BuyPriceLimit: return "ask exceeds limit — review the buy ceiling or wait";
                case IndustryWaitReason.SellPriceLimit: return "bid below limit — review the sale floor or ship elsewhere";
                case IndustryWaitReason.CashReserve: return "cash reserved — earn funds or review the reserve";
                case IndustryWaitReason.InsufficientCompanyFunds: return "company cash short — reduce purchases or earn funds";
                case IndustryWaitReason.InsufficientMarketFunds: return "market cash short — use another funded customer";
                case IndustryWaitReason.TargetSatisfied: return "buy target met";
                case IndustryWaitReason.ReserveRetained: return "retained stock protected";
                case IndustryWaitReason.MarketInventoryFull: return "market full — ship to another town";
                case IndustryWaitReason.CashAccountFull: return "cash account at supported bound";
                default: return "ready";
            }
        }
    }
}
