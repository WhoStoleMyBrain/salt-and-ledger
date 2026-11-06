using System;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private string selectedFacilityId;
        private string selectedPublicFacilityId;
        private string loadedPolicyFacilityId;
        private string industryPortId;
        private bool industryWarehouseSelected;
        private bool industryFactorySelected;
        private ProductionPriority industryDraftPriority = ProductionPriority.Normal;
        private RegionalWorldSnapshot productionDraftSnapshot;

        private void BindIndustry()
        {
            Click("openIndustryButton", () => Run(() =>
            {
                Session.Pause();
                industryFactorySelected = false;
                SelectIndustryPane(false);
                ShowModal("industryBackdrop", "closeIndustryButton");
            }));
            Click("closeIndustryButton", CloseModals);
            Click("industryFactoriesTab", () => { industryFactorySelected = false; SelectIndustryPane(false); });
            Click("industryWarehouseTab", () => SelectIndustryPane(true));
            Click("industryBackToOverview", () => { industryFactorySelected = false; SelectIndustryPane(false); RefreshIndustry(); });
            Click("factoryWarehouseButton", OpenFactoryWarehouse);
            Click("factoryShortagePurchase", OpenFactoryWarehouse);
            Click("factoryAutomationButton", () => SelectIndustryPane(true));
            Click("industryShippingButton", () => OpenRoutePlanner(Session.Player.Id));
            Click("applyIndustryPolicy", ApplyIndustryPolicy);
            Click("cancelIndustryPolicy", () =>
            {
                if (selectedFacilityId != null && Session.Snapshot.PrivateFacilities.TryGetValue(selectedFacilityId, out var facility))
                    LoadIndustryPolicy(facility);
                RefreshIndustryDraftPreview();
            });
            Element<Toggle>("facilityEnabled").RegisterValueChangedCallback(_ => IndustryDraftChanged());
            Element<TextField>("facilityOperatingRate").RegisterValueChangedCallback(_ => IndustryDraftChanged());
            Element<SliderInt>("facilityRateSlider").RegisterValueChangedCallback(evt =>
            {
                Element<TextField>("facilityOperatingRate").SetValueWithoutNotify(Number(evt.newValue));
                IndustryDraftChanged();
            });
            foreach (var rate in new[] { 0, 25, 50, 75, 100 })
            {
                var value = rate;
                Click("facilityRate" + rate, () => Element<TextField>("facilityOperatingRate").value = Number(value));
            }
            foreach (var priority in new[] { ProductionPriority.Low, ProductionPriority.Normal, ProductionPriority.High })
            {
                var value = priority;
                Click("facilityPriority" + priority, () => { industryDraftPriority = value; IndustryDraftChanged(); });
            }
            BindWarehouseAutomation();
        }

        private void SelectIndustryPane(bool warehouse)
        {
            if (!warehouse) DismissWarehouseReserve();
            industryWarehouseSelected = warehouse;
            Element<VisualElement>("industryFactoriesPane").EnableInClassList("hidden", warehouse);
            Element<VisualElement>("industryWarehousePane").EnableInClassList("hidden", !warehouse);
            Element<Button>("industryFactoriesTab").EnableInClassList("selected-tab", !warehouse);
            Element<Button>("industryWarehouseTab").EnableInClassList("selected-tab", warehouse);
            if (!warehouse) ClearApprovedContext();
            if (IndustryIsOpen) RefreshIndustry();
        }

        private void ClearIndustryDrafts()
        {
            selectedFacilityId = null;
            selectedPublicFacilityId = null;
            loadedPolicyFacilityId = null;
            loadedWarehousePortId = null;
            industryPortId = null;
            industryFactorySelected = false;
            warehouseDraftErrors.Clear();
            warehouseReserveError = null;
            Element<ScrollView>("industryPolicyScroll").scrollOffset = UnityEngine.Vector2.zero;
            SelectWarehouseDirection(false);
            SelectIndustryPane(false);
        }

        private void OpenApprovedFactory(string facilityId)
        {
            if (selectedFacilityId != facilityId) loadedPolicyFacilityId = null;
            selectedFacilityId = facilityId;
            selectedPublicFacilityId = null;
            industryFactorySelected = true;
            SelectIndustryPane(false);
            Session.Pause();
            ShowModal("industryBackdrop", "closeIndustryButton");
            RefreshIndustry();
        }

        private void OpenPublicIndustry(string facilityId)
        {
            selectedFacilityId = null;
            selectedPublicFacilityId = facilityId;
            industryFactorySelected = true;
            RefreshIndustry();
        }

        private void RefreshIndustry()
        {
            if (!IndustryIsOpen) return;
            Root.Q<MaritimeIllustration>("industryIllustration").AddToClassList("hidden");
            var portId = Session.SelectedPortId;
            if (industryPortId != null && industryPortId != portId)
            {
                industryFactorySelected = false;
                selectedFacilityId = null;
                selectedPublicFacilityId = null;
                loadedPolicyFacilityId = null;
            }
            industryPortId = portId;
            SetText("industryBreadcrumb", PortName(portId).ToUpperInvariant() + (industryWarehouseSelected ? " / COMPANY WAREHOUSE" : " / INDUSTRIES"));
            SetText("industryTitle", industryWarehouseSelected ? "Automatic local trades" : PortName(portId) + " industries");
            Element<VisualElement>("industryWorkspaceTabs").EnableInClassList("hidden", industryWarehouseSelected || industryFactorySelected);
            Element<VisualElement>("warehouseDirectionTabs").EnableInClassList("hidden", !industryWarehouseSelected);
            Element<Button>("industryBackToOverview").EnableInClassList("hidden", !industryWarehouseSelected && !industryFactorySelected);
            Element<Label>("facilityOwnership").EnableInClassList("hidden", industryWarehouseSelected || !industryFactorySelected);
            Element<VisualElement>("industryOverview").EnableInClassList("hidden", industryFactorySelected);
            Element<ScrollView>("industryPolicyScroll").EnableInClassList("hidden", !industryFactorySelected);
            RefreshIndustryOverview();
            if (industryFactorySelected && !industryWarehouseSelected) RefreshApprovedFactory();
            RefreshWarehouseAutomation();
            RefreshModalStatus();
        }

        private void RefreshIndustryOverview()
        {
            var snapshot = Session.Snapshot;
            var port = snapshot.Ports[Session.SelectedPortId];
            var own = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId && f.PortId == port.Id)
                .OrderBy(f => f.Blueprint.BuildingId, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal).ToArray();
            SetText("industryCounts", (snapshot.PrivateFacilities.Values.Count(f => f.PortId == port.Id) + port.Facilities.Count) + " sites · " + own.Length + " owned");
            var rows = Element<VisualElement>("industryOperatingRows"); rows.Clear();
            foreach (var facility in own)
            {
                var signal = EconomicSignals.PrivateProduction(snapshot, Session.Content, facility.Id);
                AddOperatingIndustry(rows, signal, () => OpenApprovedFactory(facility.Id));
            }
            foreach (var facility in port.Facilities.OrderBy(f => f.Configuration.Instance.BuildingId, StringComparer.Ordinal)
                .ThenBy(f => f.Configuration.Instance.Id, StringComparer.Ordinal))
            {
                var signal = EconomicSignals.PublicProduction(snapshot, Session.Content, port.Id, facility.Configuration.Instance.Id);
                AddOperatingIndustry(rows, signal, () => OpenPublicIndustry(signal.FacilityId));
            }
            foreach (var facility in snapshot.PrivateFacilities.Values.Where(f => f.OwnerId != Session.Player.OwnerId && f.PortId == port.Id)
                .OrderBy(f => f.OwnerId, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal))
                Text(rows, null, snapshot.Companies[facility.OwnerId].Name + " · " + Session.Content.GetBuilding(facility.Blueprint.BuildingId).Name +
                    " · " + facility.ConstructionStatus + " · " + facility.BatchesProduced + " batches · inspection only", "industry-v3-caption");
            if (rows.childCount == 0) Text(rows, null, "No industries here yet.", "industry-v3-caption");
            var buildRows = Element<VisualElement>("industryBuildRows"); buildRows.Clear();
            foreach (var blueprint in snapshot.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(port.Id))
                .OrderBy(b => b.BuildingId, StringComparer.Ordinal))
            {
                var card = Panel(buildRows, "industryBuild_" + blueprint.Id, "industry-v3-card");
                var heading = Panel(card, null, "industry-v3-row");
                var icon = new MaritimeGlyph(IndustryGlyph(blueprint.BuildingId)); icon.AddToClassList("industry-v3-factory-icon"); heading.Add(icon);
                Text(heading, null, Session.Content.GetBuilding(blueprint.BuildingId).Name, "industry-v3-metric industry-v3-factory-copy");
                AddCompactRecipe(card, Session.Content.GetRecipe(blueprint.RecipeId));
                var schedule = snapshot.PreviewConstructionSchedule(Session.Player.OwnerId, blueprint.Id);
                Text(card, "construction_" + blueprint.Id, Money(blueprint.ConstructionCost) + " coins · +" + Q(blueprint.WarehouseCapacity) + " storage", "industry-v3-build-cost");
                Text(card, null, (schedule.DelayDays == 0 ? "Operational now" : "Ready D" + schedule.CompletionDay) +
                    " · first batch D" + schedule.FirstProductionDay, "industry-v3-caption").tooltip =
                    "Pays construction once. Production waits through completion day and then a full recipe interval. First batch still requires inputs, wages and space.";
                var button = Button(card, "construct_" + blueprint.Id, Session.Player.Cash >= blueprint.ConstructionCost ? "Build " +
                    Session.Content.GetBuilding(blueprint.BuildingId).Name : "Needs " + Money(blueprint.ConstructionCost) + " coins", () => Run(() =>
                    {
                        var result = Session.Construct(blueprint.Id);
                        var added = result.After.PrivateFacilities.Keys.Except(result.Before.PrivateFacilities.Keys).FirstOrDefault();
                        if (added != null) OpenApprovedFactory(added);
                    }), "industry-v3-primary");
                button.SetEnabled(!Session.IsChallengeEnded && Session.Player.Cash >= blueprint.ConstructionCost);
            }
            if (buildRows.childCount == 0) Text(buildRows, null, "No eligible construction in this town.", "industry-v3-caption");
        }

        private void AddOperatingIndustry(VisualElement rows, ProductionOpportunitySignal signal, Action inspect)
        {
            var card = Panel(rows, "industryFacility_" + signal.FacilityId, "industry-v3-card industry-v3-factory-row");
            var glyph = new MaritimeGlyph(IndustryGlyph(signal.BuildingId)); glyph.AddToClassList("industry-v3-factory-icon"); card.Add(glyph);
            var copy = Panel(card, null, "industry-v3-factory-copy");
            Text(copy, null, Session.Content.GetBuilding(signal.BuildingId).Name + (signal.IsPrivate ? " · OWNED" : " · TOWN"), "industry-v3-metric");
            AddCompactRecipe(copy, Session.Content.GetRecipe(signal.RecipeId));
            Text(copy, null, FactoryPotential(signal) + " / 12d", "industry-v3-caption").tooltip = "Enabled scheduled potential; requires fresh inputs, payroll and storage at each attempt.";
            Text(copy, null, signal.UnderConstruction ? "Under construction" : signal.ReadyFromCurrentState ? "Ready" : ProductionRemedy(signal.CurrentReadiness), "industry-v3-caption");
            Button(card, "manageFactory_" + signal.FacilityId, signal.IsPrivate ? "Manage →" : "Inspect →", inspect, "industry-v3-secondary").tooltip = signal.FacilityId;
        }

        private void AddCompactRecipe(VisualElement parent, RecipeDefinition recipe)
        {
            var row = Panel(parent, null, "industry-v3-build-recipe");
            if (recipe.Inputs.Count == 0) Text(row, null, "Extract", "industry-v3-caption");
            foreach (var input in recipe.Inputs) AddCompactGood(row, input.GoodId, input.Quantity.ToString());
            Text(row, null, "→", "industry-v3-caption");
            foreach (var output in recipe.Outputs) AddCompactGood(row, output.GoodId, output.Quantity.ToString());
        }

        private static string IndustryGlyph(string buildingId) => ApprovedMaritimeAssets.Industry(buildingId) != null ? buildingId :
            buildingId == "silver_mine" ? "silver" : "industry";

        private void AddCompactGood(VisualElement parent, string goodId, string quantity)
        {
            var glyph = new MaritimeGlyph(goodId); glyph.AddToClassList("sl-glyph-small"); parent.Add(glyph);
            Text(parent, null, quantity + " " + Session.Content.GetGood(goodId).Name, "industry-v3-caption");
        }

        private string FactoryPotential(ProductionOpportunitySignal signal) => string.Join(" · ", signal.Outputs.Select(output =>
        {
            var flow = EconomicSignals.CommodityFlow(Session.Snapshot, Session.Content, signal.PortId, output.GoodId, signal.OwnerId);
            var own = flow.Production.First(item => item.Opportunity.FacilityId == signal.FacilityId);
            return SignedQuantity(own.IsActive ? own.OutputQuantity : 0) + " " + Session.Content.GetGood(output.GoodId).Name;
        }));

        private void RefreshApprovedFactory()
        {
            var snapshot = Session.Snapshot;
            PrivateFacilitySnapshot facility = null;
            if (selectedFacilityId != null) snapshot.PrivateFacilities.TryGetValue(selectedFacilityId, out facility);
            if (facility == null && selectedPublicFacilityId == null) { industryFactorySelected = false; return; }
            var signal = facility != null ? EconomicSignals.PrivateProduction(snapshot, Session.Content, facility.Id) :
                EconomicSignals.PublicProduction(snapshot, Session.Content, Session.SelectedPortId, selectedPublicFacilityId);
            SetText("industryTitle", Session.Content.GetBuilding(signal.BuildingId).Name);
            Root.Q<MaritimeIllustration>("industryIllustration").EnableInClassList("hidden", signal.BuildingId != "bakery");
            SetText("facilityOwnership", facility == null ? "TOWN · READ ONLY" : "OWNED");
            SetText("facilityRecipeLabel", "NEXT BATCH / " + (signal.Inputs.Count == 0 ? "EXTRACTION" : string.Join(" + ", signal.Inputs.Select(i => Q(i.Quantity) + " " + Session.Content.GetGood(i.GoodId).Name))) +
                " → " + string.Join(" + ", signal.Outputs.Select(o => Q(o.Quantity) + " " + Session.Content.GetGood(o.GoodId).Name)));
            Element<VisualElement>("factorySettings").EnableInClassList("hidden", facility == null);
            Element<VisualElement>("factoryRateControls").EnableInClassList("hidden", facility == null);
            Element<Button>("factoryWarehouseButton").EnableInClassList("hidden", facility == null);
            Element<Button>("factoryAutomationButton").EnableInClassList("hidden", facility == null);
            Element<Button>("factoryShortagePurchase").EnableInClassList("hidden", facility == null || signal.Inputs.All(i => i.Shortfall == 0));
            SetText("factoryStockTitle", facility == null ? "TOWN MARKET INPUTS" : "COMPANY WAREHOUSE");
            SetText("facilityStateLabel", signal.UnderConstruction ? "Under construction · " + facility.ConstructionDaysRemaining + " days remaining" :
                signal.ReadyFromCurrentState ? "Ready with current inputs, cash and space" : ArchipelagoLabSession.FacilityStatus(signal.CurrentReadiness));
            Element<Label>("facilityStateLabel").tooltip = "Current state, not a forecast. " + ProductionRemedy(signal.CurrentReadiness) + "\nLast attempt: " + ArchipelagoLabSession.FacilityStatus(signal.LastOutcome);
            BuildFactoryRecipe(signal);
            SetText("factoryPayroll", "Wages " + Money(signal.WagePerBatch) + " / completed batch");
            SetText("factoryFunds", (facility == null ? "Town cash " : "Cash ") + Money(signal.EmployerCash) +
                (signal.EmployerCash >= signal.WagePerBatch ? " · funded" : " · payroll short"));
            SetText("facilityFlowLabel", FactoryPotential(signal) + " / 12d");
            Element<Label>("facilityFlowLabel").tooltip = "Conditional scheduled output at the applied operating rate. Requires inputs, wages and space at every attempt; no stock is reserved.";
            SetText("facilityScheduleLabel", facility != null && !facility.IsOperational ? "First eligible D" + facility.FirstProductionDay :
                signal.NextRateAdmittedDay.HasValue ? "Next eligible attempt D" + signal.NextRateAdmittedDay.Value : "Production paused");
            SetText("factoryStorageSpace", signal.StorageCapacity.HasValue ? "Free shared storage " + Q(signal.StorageCapacity.Value - signal.StoredUnits) : "Output joins the town market");
            SetText("facilityInputsSummary", "Stock / batch: " + string.Join(" · ", signal.Inputs.Select(i => Session.Content.GetGood(i.GoodId).Name + " " + Q(i.CurrentStock) + "/" + Q(i.Quantity))));
            var stocks = Element<VisualElement>("factoryWarehouseStock"); stocks.Clear();
            foreach (var item in signal.Inputs.Concat(signal.Outputs).GroupBy(g => g.GoodId).Select(group => group.First()))
            {
                var row = Panel(stocks, null, "industry-v3-stock-row");
                var icon = new MaritimeGlyph(item.GoodId); icon.AddToClassList("sl-glyph-small"); row.Add(icon);
                Text(row, null, Session.Content.GetGood(item.GoodId).Name, "industry-v3-caption");
                Text(row, null, Q(item.CurrentStock), "industry-v3-stock-value");
            }
            if (facility != null)
            {
                SetText("facilityInputStatusLabel", InputAvailability(facility));
                SetText("facilityScheduleDetail", FacilitySchedule(facility));
                SetText("facilityStatsLabel", "Age " + (snapshot.Day - facility.CreatedDay) + " days · " + facility.BatchesProduced + " batches\nCapital paid " +
                    Money(facility.IsStarterGrant ? 0 : facility.ConstructionCost) + (facility.IsStarterGrant ? " · starter grant" : " · paid once") +
                    " · paid wages " + Money(facility.WagesPaid) + "\nConsumed: " + QuantityTotals(facility.ConsumedQuantities) +
                    "\nProduced: " + QuantityTotals(facility.ProducedQuantities) + "\nShared town stock basis " +
                    Money(snapshot.Accounting.Stores[snapshot.GetFacilityStorage(facility.Id).Id].TotalCostBasis) +
                    ". Shared inputs can serve other local facilities. Warehouse purchases and sales appear on the Warehouse tab. Wages remain separate operating expenses.");
                SetText("industryShippingLabel", FacilityShipping(facility));
                SetText("industryAppliedPolicyLabel", "Applied: " + (facility.Enabled ? "Enabled" : "Disabled") + " · " + facility.OperatingRate + "% · " + facility.Priority);
                if (loadedPolicyFacilityId != facility.Id) LoadIndustryPolicy(facility);
                productionDraftSnapshot = snapshot;
                RefreshIndustryDraftPreview();
            }
            else
            {
                SetText("facilityInputStatusLabel", "Public production takes inputs from, and returns outputs to, the town market. The town pays its wages.");
                SetText("facilityScheduleDetail", "Next scheduled opportunity D" + signal.ScheduledDueDay + " · last outcome " + ArchipelagoLabSession.FacilityStatus(signal.LastOutcome));
                SetText("facilityStatsLabel", "Public facility " + signal.FacilityId + ". It is not a company asset.");
                SetText("industryShippingLabel", "Trade with the town market to supply this public facility.");
            }
            Element<Button>("industryShippingButton").SetEnabled(facility != null);
        }

        private void BuildFactoryRecipe(ProductionOpportunitySignal signal)
        {
            var row = Element<VisualElement>("industryRecipeSymbols"); row.Clear();
            if (signal.Inputs.Count == 0) Text(row, null, "Natural extraction", "industry-v3-caption");
            foreach (var input in signal.Inputs)
            {
                var tile = Panel(row, "factoryInput_" + input.GoodId, "industry-v3-recipe-tile");
                tile.EnableInClassList("industry-v3-blocked", input.Shortfall > 0);
                tile.Add(new MaritimeGlyph(input.GoodId));
                var copy = Panel(tile, null, "industry-v3-grow");
                Text(copy, null, Session.Content.GetGood(input.GoodId).Name.ToUpperInvariant(), "industry-v3-caption");
                Text(copy, null, Q(input.CurrentStock) + " / " + Q(input.Quantity), "industry-v3-metric");
                Text(copy, null, input.Shortfall > 0 ? "NOT ENOUGH" : "READY", "industry-v3-caption");
            }
            Text(row, null, "+", "industry-v3-recipe-arrow");
            var rateTile = Panel(row, null, "industry-v3-recipe-tile"); rateTile.Add(new MaritimeGlyph(IndustryGlyph(signal.BuildingId)));
            var rateCopy = Panel(rateTile, null, "industry-v3-grow");
            Text(rateCopy, null, Session.Content.GetBuilding(signal.BuildingId).Name.ToUpperInvariant(), "industry-v3-caption");
            Text(rateCopy, "factoryRecipeRate", signal.OperatingRate + "%", "industry-v3-metric");
            Text(rateCopy, "factoryRecipeRateState", "FULL BATCHES", "industry-v3-caption");
            Text(row, null, "→", "industry-v3-recipe-arrow");
            foreach (var output in signal.Outputs)
            {
                var tile = Panel(row, "factoryOutput_" + output.GoodId, "industry-v3-recipe-tile");
                tile.Add(new MaritimeGlyph(output.GoodId));
                var copy = Panel(tile, null, "industry-v3-grow");
                Text(copy, null, Session.Content.GetGood(output.GoodId).Name.ToUpperInvariant(), "industry-v3-caption");
                Text(copy, null, "+" + Q(output.Quantity), "industry-v3-metric");
                Text(copy, null, "PER FULL BATCH", "industry-v3-caption");
            }
        }

        private void OpenFactoryWarehouse()
        {
            if (selectedFacilityId == null) return;
            var signal = EconomicSignals.PrivateProduction(Session.Snapshot, Session.Content, selectedFacilityId);
            var input = signal.Inputs.FirstOrDefault(item => item.Shortfall > 0) ?? signal.Inputs.FirstOrDefault();
            var goodId = input?.GoodId ?? signal.Outputs[0].GoodId;
            OpenWarehousePurchase(goodId, input != null && input.Shortfall > 0 ? (decimal)input.Shortfall / Session.Snapshot.QuantityScale : (decimal?)null);
        }

        private void LoadIndustryPolicy(PrivateFacilitySnapshot facility)
        {
            Element<Toggle>("facilityEnabled").SetValueWithoutNotify(facility.Enabled);
            Element<TextField>("facilityOperatingRate").SetValueWithoutNotify(Number(facility.OperatingRate));
            Element<SliderInt>("facilityRateSlider").SetValueWithoutNotify(facility.OperatingRate);
            industryDraftPriority = facility.Priority;
            loadedPolicyFacilityId = facility.Id;
            SetText("industryPolicyDraftLabel", "");
        }

        private void IndustryDraftChanged()
        {
            SetText("industryPolicyDraftLabel", "Unsaved factory changes");
            RefreshIndustryDraftPreview();
        }

        private int ReadOperatingRate()
        {
            var rate = ReadInt("facilityOperatingRate", "Operating rate", true);
            if (rate > 100) throw new ArgumentException("Operating rate must be between 0 and 100%.");
            return rate;
        }

        private ProductionPriority ReadProductionPriority() => industryDraftPriority;

        private void RefreshIndustryDraftPreview()
        {
            if (selectedFacilityId == null) return;
            var valid = false;
            try
            {
                var rate = ReadOperatingRate();
                Element<SliderInt>("facilityRateSlider").SetValueWithoutNotify(rate);
                var enabled = Element<Toggle>("facilityEnabled").value;
                SetText("factoryRecipeRate", rate + "%");
                var changed = Session.Snapshot.PrivateFacilities.TryGetValue(selectedFacilityId, out var current) &&
                    (rate != current.OperatingRate || enabled != current.Enabled || industryDraftPriority != current.Priority);
                SetText("factoryRecipeRateState", changed ? "DRAFT · NOT APPLIED" : "FULL BATCHES");
                SetText("industryPolicyPreviewLabel", !enabled || rate == 0 ? "Production paused; warehouse rules remain active." :
                    "Full inputs and wages per batch. Apply to use " + rate + "% / " + industryDraftPriority + ".");
                valid = true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException)
            { SetText("industryPolicyDraftLabel", exception.Message); }
            foreach (var priority in new[] { ProductionPriority.Low, ProductionPriority.Normal, ProductionPriority.High })
                Element<Button>("facilityPriority" + priority).EnableInClassList("selected-tab", industryDraftPriority == priority);
            Element<Button>("applyIndustryPolicy").SetEnabled(valid && !Session.IsChallengeEnded);
        }

        private void ApplyIndustryPolicy() => Run(() =>
        {
            Session.ConfigureProduction(selectedFacilityId, Element<Toggle>("facilityEnabled").value,
                ReadOperatingRate(), ReadProductionPriority(), productionDraftSnapshot);
            SetText("industryPolicyDraftLabel", "Settings applied");
        });
    }
}
