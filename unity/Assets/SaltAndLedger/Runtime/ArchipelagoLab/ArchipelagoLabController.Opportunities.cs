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
        private int opportunityTab;
        private string opportunityPortId;
        private RegionalWorldSnapshot opportunitiesPresented;
        private string opportunityShipPresented;
        private readonly List<string> opportunityTownChoices = new List<string>();
        private readonly List<string> opportunityShipChoices = new List<string>();

        private void BuildOpportunities()
        {
            var screen = CreateScreen(ArchipelagoScreen.Opportunities, "Contracts & Projects",
                "Choose commitments, deliver real cargo and invest in lasting infrastructure. Economic time is paused.");
            Text(screen, "opportunityHeadline", "", "run-objective");
            var filters = Panel(screen, null, "row run-actions opportunity-fields");
            var town = new DropdownField("Town") { name = "opportunityTownPicker" };
            filters.Add(town);
            town.RegisterValueChangedCallback(_ =>
            {
                opportunityPortId = town.index <= 0 ? null : opportunityTownChoices[town.index - 1];
                opportunitiesPresented = null; RefreshOpportunities();
            });
            var ship = new DropdownField("Delivery ship") { name = "opportunityShipPicker" };
            filters.Add(ship);
            ship.RegisterValueChangedCallback(_ =>
            {
                if (ship.index < 0 || ship.index >= opportunityShipChoices.Count) return;
                Run(() => Session.SelectShip(opportunityShipChoices[ship.index]));
            });
            var tabs = Panel(screen, null, "row run-actions");
            var names = new[] { "Available contracts", "Active commitments", "Projects", "History" };
            for (var index = 0; index < names.Length; index++)
            {
                var tab = index;
                Button(tabs, "opportunityTab" + tab, names[tab], () =>
                {
                    opportunityTab = tab; opportunitiesPresented = null; RefreshOpportunities();
                });
            }
            var scroll = new ScrollView { name = "opportunityScroll" };
            scroll.AddToClassList("run-content-scroll"); screen.Add(scroll);
            Panel(scroll, "opportunityCards", "");
            var actions = Panel(screen, null, "row run-actions");
            Button(actions, "opportunitiesBackButton", "Back", () => { ShowScreen(secondaryParent); Refresh(); });
            Button(actions, "opportunitiesRouteButton", "Route planner", () => OpenRoutePlanner(Session.Player.Id));
            Button(actions, "opportunitiesCompanyButton", "Company services", () => OpenSecondary(ArchipelagoScreen.Development));
            Button(actions, "opportunitiesSaveButton", "Save / Load", OpenSaveSlots);
        }

        public void OpenOpportunities(string portId = null)
        {
            opportunityPortId = portId;
            opportunityTab = 0;
            opportunitiesPresented = null;
            OpenSecondary(ArchipelagoScreen.Opportunities);
        }

        private void RefreshOpportunities()
        {
            if (Screen != ArchipelagoScreen.Opportunities || Root.Q("opportunityCards") == null) return;
            var world = Session.Snapshot;
            if (ReferenceEquals(opportunitiesPresented, world) && opportunityShipPresented == Session.Player.Id) return;
            opportunitiesPresented = world; opportunityShipPresented = Session.Player.Id;
            var board = world.Opportunities;
            var cards = Element<VisualElement>("opportunityCards"); cards.Clear();
            if (board == null)
            {
                SetText("opportunityHeadline", "No economic opportunities in this world.");
                return;
            }
            opportunityTownChoices.Clear(); opportunityTownChoices.AddRange(world.Ports.Keys.OrderBy(id => id, StringComparer.Ordinal));
            if (opportunityPortId != null && !world.Ports.ContainsKey(opportunityPortId)) opportunityPortId = null;
            var towns = Element<DropdownField>("opportunityTownPicker");
            towns.choices = new[] { "All towns" }.Concat(opportunityTownChoices.Select(PortName)).ToList();
            towns.SetValueWithoutNotify(opportunityPortId == null ? "All towns" : PortName(opportunityPortId));
            opportunityShipChoices.Clear(); opportunityShipChoices.AddRange(Session.OwnedShips.Select(ship => ship.Id));
            var ships = Element<DropdownField>("opportunityShipPicker");
            ships.choices = opportunityShipChoices.Select(id => world.Ships[id].Name + " · " + id).ToList();
            ships.SetValueWithoutNotify(Session.Player.Name + " · " + Session.Player.Id);
            var active = board.Entries.Count(entry => !entry.IsProject && entry.Status == OpportunityStatus.Active);
            SetText("opportunityHeadline", "Day " + world.Day + " · active contracts " + active + "/" + board.Configuration.ActiveContractLimit +
                " · company cash " + Money(Session.Player.Cash) + " · uncommitted authority funds " + Money(world.Economy.ExternalCash) + "\n" + Session.Player.Name + ": " +
                (Session.Player.DockedPortId == null ? "at sea, " + Session.Player.RemainingTravelDays + " days to " + PortName(Session.Player.DestinationPortId) :
                    "docked at " + PortName(Session.Player.DockedPortId)) + " · cargo " + Q(Session.Player.CargoUnits) + "/" + Q(Session.Player.CargoCapacity));
            for (var tab = 0; tab < 4; tab++) Element<Button>("opportunityTab" + tab).EnableInClassList("primary", tab == opportunityTab);
            var entries = board.Entries.Where(entry => opportunityPortId == null || entry.PortId == opportunityPortId)
                .Where(entry => opportunityTab == 0 ? !entry.IsProject && entry.Status == OpportunityStatus.Available :
                    opportunityTab == 1 ? entry.Status == OpportunityStatus.Active :
                    opportunityTab == 2 ? entry.IsProject && !entry.IsTerminal : entry.IsTerminal)
                .OrderBy(entry => entry.IsProject ? long.MaxValue : entry.DeadlineDay)
                .ThenBy(entry => entry.PortId, StringComparer.Ordinal).ThenBy(entry => entry.Id, StringComparer.Ordinal).ToArray();
            foreach (var entry in entries) BuildOpportunityCard(cards, world, entry);
            if (entries.Length == 0) Text(cards, null, "No matching opportunities. Choose another tab or All towns.", "run-copy");
        }

        private void BuildOpportunityCard(VisualElement parent, RegionalWorldSnapshot world, OpportunitySnapshot entry)
        {
            var card = Panel(parent, "opportunity_" + entry.Id, "run-card opportunity-card");
            Text(card, null, OpportunityTitle(entry) + " · " + PortName(entry.PortId), "sl-section-title");
            Text(card, null, OpportunityProgress(entry), "run-copy");
            Text(card, null, OpportunityRewardText(world, entry), "run-copy");
            if (entry.CompetitionGroupId != null || entry.ExclusiveGroupId != null)
            {
                var rivals = world.Opportunities.Entries.Where(other => other.Id != entry.Id && !other.IsTerminal &&
                    (entry.ExclusiveGroupId != null && other.ExclusiveGroupId == entry.ExclusiveGroupId ||
                     entry.CompetitionGroupId != null && other.CompetitionGroupId == entry.CompetitionGroupId)).ToArray();
                Text(card, null, (entry.ExclusiveGroupId != null ? "EXCLUSIVE TENDER · accepting this offer withdraws its alternative." :
                    "SHARED RESOURCES · these offers compete for cargo and shipping time; both may be fulfilled.") +
                    (rivals.Length == 0 ? "" : "\n" + string.Join("\n", rivals.Select(other =>
                        OpportunityTitle(other) + " at " + PortName(other.PortId) + ": " + Q(other.RequiredQuantity) + " " +
                        Session.Content.GetGood(other.GoodId).Name + " · cash up to " + Money(other.FundingRequired) + " · due D" + OpportunityDeliveryDeadline(world, other)))),
                    entry.ExclusiveGroupId != null ? "sl-problem run-copy" : "run-copy");
            }
            var details = new Foldout { text = "Terms, stock & travel", value = entry.Status == OpportunityStatus.Active };
            card.Add(details);
            Text(details, null, OpportunityTerms(entry), "run-copy");
            Text(details, null, OpportunityStockAndTravel(world, entry), "run-copy");
            var actions = Panel(card, null, "row run-actions");
            Button(actions, "opportunityHarbor_" + entry.Id, "Inspect harbor", () => OpenTrade(entry.PortId));
            if (entry.Status == OpportunityStatus.Available)
            {
                var accept = Button(actions, "opportunityAccept_" + entry.Id, entry.IsProject ? "Start project" : "Accept contract", () => Run(() =>
                    Session.AcceptOpportunity(entry.Id, world)), "primary");
                accept.SetEnabled(!Session.IsChallengeEnded);
                var decline = Button(actions, "opportunityDecline_" + entry.Id, "Decline", () => Run(() => Session.DeclineOpportunity(entry.Id, world)));
                decline.SetEnabled(!Session.IsChallengeEnded);
            }
            if (entry.Status == OpportunityStatus.Active && entry.OwnerId == Session.Player.OwnerId)
            {
                if (entry.IsProject) BuildProjectContribution(card, world, entry);
                else BuildContractDelivery(card, world, entry);
            }
            if (entry.IsProject && entry.Status == OpportunityStatus.Succeeded) ShowCompletedProject(card, world, entry);
        }

        private string OpportunityTitle(OpportunitySnapshot entry)
        {
            switch (entry.Kind)
            {
                case OpportunityKind.BulkProcurement: return "Bulk procurement · " + Session.Content.GetGood(entry.GoodId).Name;
                case OpportunityKind.RecurringSupply: return "Recurring supply · " + Session.Content.GetGood(entry.GoodId).Name;
                case OpportunityKind.IndustrialSupply: return "Industrial supply · " + Session.Content.GetGood(entry.GoodId).Name;
                case OpportunityKind.EmergencyRelief: return "Emergency economic relief";
                case OpportunityKind.PublicIndustryCommission:
                    return "Public industry · " + (entry.BlueprintId != null && Session.Snapshot.IndustryBlueprints.TryGetValue(entry.BlueprintId, out var blueprint)
                        ? Session.Content.GetBuilding(blueprint.BuildingId).Name : "commission");
                default: return "Trading Depot";
            }
        }

        private string OpportunityProgress(OpportunitySnapshot entry)
        {
            var text = entry.Status + " · " + Q(entry.DeliveredQuantity) + "/" + Q(entry.RequiredQuantity) + " " + Session.Content.GetGood(entry.GoodId).Name;
            if (entry.IsProject) return text + " · invested " + Money(entry.ContributedCash) + "/" + Money(entry.RequiredCash) + " coins";
            var prospective = entry.Status == OpportunityStatus.Available &&
                !Session.Snapshot.Economy.Configuration.Contracts.Any(contract => contract.Id == entry.Id);
            text += prospective ? " · " + (entry.DeadlineDay - entry.CreatedDay) + " days from acceptance" : " · due day " + entry.DeadlineDay;
            if (entry.Status == OpportunityStatus.Available) text += " · accept by D" + entry.ExpiresDay;
            if (entry.Kind == OpportunityKind.RecurringSupply)
                text += "\nWindow " + Math.Min(entry.WindowIndex + 1, entry.WindowCount) + "/" + entry.WindowCount +
                    " · this window " + Q(entry.WindowDelivered) + "/" + Q(entry.RequiredQuantity / entry.WindowCount) +
                    (prospective ? " · first window lasts " + entry.WindowDays + " days" : " · window closes D" + entry.CurrentWindowDeadlineDay);
            if (entry.EvidenceRequired > 0) text += "\nVerified " + (entry.Kind == OpportunityKind.IndustrialSupply ? "funded batches " : "paid food opportunities ") +
                entry.EvidenceCount + "/" + entry.EvidenceRequired;
            if (entry.IsTerminal && entry.CompletedDay.HasValue) text += " · resolved D" + entry.CompletedDay.Value;
            return text;
        }

        private string OpportunityRewardText(RegionalWorldSnapshot world, OpportunitySnapshot entry)
        {
            if (entry.IsProject) return entry.Kind == OpportunityKind.TradingDepot ?
                "Permanent company storage: " + Q(entry.StorageCapacity) + " capacity; local trading, warehouse automation and docked load/unload." :
                "Permanent public factory: ordinary recipe inputs, town-funded wages and output sold through the local market.";
            var text = "External authority payment: " + Money(checked((long)entry.RequiredQuantity * entry.UnitPrice)) +
                " for deliveries + " + Money(entry.CashReward) + " completion bonus · received " + Money(entry.PaidCash);
            if (entry.RewardKind == OpportunityRewardKind.CommercialPrivilege)
                text += "\nLocal sale handling fee reduced to 1% for " + entry.PrivilegeDays + " days" +
                    (entry.Status == OpportunityStatus.Succeeded ? " · " + (world.Day <= entry.PrivilegeUntilDay ? "active through D" : "expired D") + entry.PrivilegeUntilDay : " after success") + ".";
            if (entry.RewardKind == OpportunityRewardKind.InfrastructureAccess)
                text += "\nPublic construction access at this town may unlock eligible industry projects. Structural production restrictions still apply.";
            if (entry.Status == OpportunityStatus.Active) text += "\nReserved external funds remaining: " + Money(entry.ReservedCash) + ".";
            return text;
        }

        private string OpportunityTerms(OpportunitySnapshot entry)
        {
            if (entry.IsProject) return "Contribute the specified materials and money in any number of payments. Contributions are committed investments. " +
                "Materials come from your docked ship or this town's company stock; the project completes only when both requirements are met.";
            var text = "Use the contract's Deliver action or a ship itinerary contract order. Ordinary market sales do not count. " +
                "Cargo physically leaves the selected ship; the external authority pays the agreed amount once.";
            text += Session.Snapshot.Economy.Configuration.Contracts.Any(contract => contract.Id == entry.Id)
                ? " Royal export cargo leaves the region."
                : " Delivered goods enter the issuing town's public market.";
            if (entry.Kind == OpportunityKind.RecurringSupply) text += "\nDeliver " + Q(entry.RequiredQuantity / entry.WindowCount) + " " +
                Session.Content.GetGood(entry.GoodId).Name + " in each of " + entry.WindowCount + " windows of " + entry.WindowDays +
                " days. Missing a window fails the agreement and forfeits the completion bonus; already earned delivery payments remain yours.";
            if (entry.Kind == OpportunityKind.IndustrialSupply) text += "\nThe named public factory must complete " + entry.EvidenceRequired +
                " actual funded batches using contribution credit. Arrival alone is insufficient. Facility: " + entry.FacilityId + ".";
            if (entry.Kind == OpportunityKind.EmergencyRelief) text += "\nHouseholds must afford and consume sufficient food over " + entry.EvidenceRequired +
                " opportunities, backed by your delivered food contribution. Unrelated town recovery alone does not complete this commitment.";
            return text;
        }

        private string OpportunityStockAndTravel(RegionalWorldSnapshot world, OpportunitySnapshot entry)
        {
            var ship = Session.Player;
            var pool = world.CompanyStockpiles.Values.FirstOrDefault(stock => stock.OwnerId == ship.OwnerId && stock.PortId == entry.PortId);
            var text = Session.Content.GetGood(entry.GoodId).Name + ": selected cargo " + Q(ship.Cargo[entry.GoodId]) +
                " · company stock " + (pool == null ? "no local warehouse" : Q(pool.Inventory[entry.GoodId])) +
                " · town market " + Q(world.Ports[entry.PortId].Inventory[entry.GoodId]);
            if (ship.DockedPortId == entry.PortId) return text + "\nSelected ship is docked here" + (ship.AutomationEnabled ? "; pause its route for manual delivery." : "; manual cargo delivery is available.");
            var origin = ship.DockedPortId ?? ship.DestinationPortId;
            try
            {
                var travel = checked((ship.DockedPortId == null ? ship.RemainingTravelDays : 0) +
                    (origin == entry.PortId ? 0 : world.GetPath(origin, entry.PortId, ship.Configuration.Speed).DurationDays));
                text += "\nEarliest direct arrival D" + checked(world.Day + travel) + " · " + travel + " travel days" +
                    (!entry.IsProject && world.Day + travel > OpportunityDeliveryDeadline(world, entry) ? " · later than the contract deadline." : ".");
                text += " This excludes loading, sourcing, production and other stops.";
            }
            catch (InvalidOperationException) { text += "\nNo connected voyage from this ship's location."; }
            catch (ArgumentException) { text += "\nNo connected voyage from this ship's location."; }
            return text;
        }

        private static long OpportunityDeliveryDeadline(RegionalWorldSnapshot world, OpportunitySnapshot entry) =>
            entry.Status == OpportunityStatus.Available && !entry.IsProject && !world.Economy.Configuration.Contracts.Any(contract => contract.Id == entry.Id)
                ? checked(world.Day + entry.DeadlineDay - entry.CreatedDay) : entry.DeadlineDay;

        private void BuildContractDelivery(VisualElement parent, RegionalWorldSnapshot world, OpportunitySnapshot entry)
        {
            if (entry.RemainingQuantity == 0)
            {
                Text(parent, null, "All contracted cargo is delivered. Waiting for " +
                    (entry.Kind == OpportunityKind.IndustrialSupply ? "actual funded factory batches" : "sufficient paid household food consumption") +
                    "; progress updates during ordinary day advances.", "run-copy");
                return;
            }
            if (entry.Kind == OpportunityKind.RecurringSupply && entry.WindowDelivered == entry.RequiredQuantity / entry.WindowCount)
            {
                Text(parent, null, "This delivery window is supplied. The next window opens on day " +
                    checked(entry.CurrentWindowDeadlineDay + 1) + ". Later windows cannot be supplied early.", "run-copy");
                return;
            }
            var row = Panel(parent, null, "row run-actions opportunity-fields");
            var quantity = new TextField("Whole units") { name = "contractQuantity_" + entry.Id, value = "1" }; row.Add(quantity);
            var previewLabel = Text(parent, null, "", "run-copy");
            var deliver = Button(row, "contractDeliver_" + entry.Id, "Deliver cargo", () => Run(() =>
                Session.DeliverContract(entry.Id, ReadWholeCargo(quantity.name, "Delivery", false), world)), "primary");
            Button(row, "contractMaximum_" + entry.Id, "Maximum now", () =>
            {
                quantity.value = Q(world.MaximumContractDelivery(Session.Player.Id, entry.Id));
            });
            if (Session.Player.AutomationEnabled)
                Button(row, "contractPauseRoute_" + entry.Id, "Pause route for manual delivery", () => Run(() => Session.SetAutomationEnabled(false)));
            void RefreshDelivery()
            {
                deliver.SetEnabled(false);
                try
                {
                    var amount = ReadWholeCargo(quantity.name, "Delivery", false);
                    var preview = world.PreviewContractDelivery(Session.Player.Id, entry.Id, amount);
                    previewLabel.text = preview.Reason + (preview.Success ? " · exact payment " + Money(preview.Payment) : "");
                    deliver.SetEnabled(preview.Success && !Session.Player.AutomationEnabled && !Session.IsChallengeEnded);
                }
                catch (ArgumentException error) { previewLabel.text = error.Message; }
                catch (InvalidOperationException error) { previewLabel.text = error.Message; }
                catch (OverflowException) { previewLabel.text = "Quantity exceeds the supported bound."; }
            }
            quantity.RegisterValueChangedCallback(_ => RefreshDelivery());
            RefreshDelivery();
        }

        private void BuildProjectContribution(VisualElement parent, RegionalWorldSnapshot world, OpportunitySnapshot entry)
        {
            var row = Panel(parent, null, "row run-actions opportunity-fields");
            var source = new DropdownField("Materials from", new List<string> { "Selected ship cargo", "Local company warehouse" }, 0);
            row.Add(source);
            var quantity = new TextField("Material units") { name = "projectQuantity_" + entry.Id, value = "0" }; row.Add(quantity);
            var cash = new TextField("Coins to invest") { name = "projectCash_" + entry.Id, value = "0" }; row.Add(cash);
            var previewLabel = Text(parent, null, "", "run-copy");
            var contribute = Button(row, "projectContribute_" + entry.Id, "Contribute", () => Run(() =>
                Session.ContributeProject(entry.Id, source.index == 0 ? Session.Player.Id : null,
                    ReadIndustryQuantity(quantity.name, "Materials", true), ReadIndustryMoney(cash.name, "Investment", true), world)), "primary");
            Button(row, "projectMaximum_" + entry.Id, "Fill available contribution", () =>
            {
                var pool = world.CompanyStockpiles.Values.FirstOrDefault(stock => stock.OwnerId == Session.Player.OwnerId && stock.PortId == entry.PortId);
                var stock = source.index == 0 ? Session.Player.DockedPortId == entry.PortId ? Session.Player.Cargo[entry.GoodId] : 0 :
                    pool == null ? 0 : pool.Inventory[entry.GoodId];
                var material = Math.Min(stock, entry.RemainingQuantity);
                if (source.index == 0) material -= material % world.QuantityScale;
                quantity.value = Q(material);
                cash.value = Money(Math.Min(Session.Player.Cash, entry.RequiredCash - entry.ContributedCash));
            });
            void RefreshContribution()
            {
                contribute.SetEnabled(false);
                try
                {
                    var preview = world.PreviewProjectContribution(Session.Player.OwnerId, entry.Id, source.index == 0 ? Session.Player.Id : null,
                        ReadIndustryQuantity(quantity.name, "Materials", true), ReadIndustryMoney(cash.name, "Investment", true));
                    previewLabel.text = preview.Reason + (preview.Successful ? " · " + Q(preview.Quantity) + " " +
                        Session.Content.GetGood(entry.GoodId).Name + " + " + Money(preview.Cash) + " coins" : "");
                    contribute.SetEnabled(preview.Successful && !Session.IsChallengeEnded);
                }
                catch (ArgumentException error) { previewLabel.text = error.Message; }
                catch (InvalidOperationException error) { previewLabel.text = error.Message; }
                catch (OverflowException) { previewLabel.text = "Contribution exceeds the supported bound."; }
            }
            source.RegisterValueChangedCallback(_ => RefreshContribution());
            quantity.RegisterValueChangedCallback(_ => RefreshContribution());
            cash.RegisterValueChangedCallback(_ => RefreshContribution());
            RefreshContribution();
        }

        private void ShowCompletedProject(VisualElement card, RegionalWorldSnapshot world, OpportunitySnapshot entry)
        {
            if (entry.Kind == OpportunityKind.TradingDepot)
            {
                var pool = world.CompanyStockpiles.Values.FirstOrDefault(stock => stock.OwnerId == entry.OwnerId && stock.PortId == entry.PortId);
                if (pool != null) Text(card, null, "Current warehouse: " + Q(pool.StoredUnits) + "/" + Q(pool.Capacity) +
                    " units. Open Harbor to trade, load/unload or configure warehouse automation.", "run-copy");
            }
            else
            {
                var facility = world.Ports[entry.PortId].Facilities.FirstOrDefault(item => item.Configuration.Instance.Id == entry.FacilityId);
                if (facility != null) Text(card, null, "Public factory exists · next production attempt D" + facility.NextDueDay +
                    " · last outcome " + ArchipelagoLabSession.FacilityStatus(facility.LastOutcome) + ". Inspect town Economy for inputs and wages.", "run-copy");
            }
        }
    }
}
