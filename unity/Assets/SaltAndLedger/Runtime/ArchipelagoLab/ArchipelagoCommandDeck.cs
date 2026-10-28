using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public enum CommandMode { Map, Trade, Industry, Fleet, Economy, Objectives }

    /// <summary>Navigation only. Selection and all economic facts remain in the existing session.</summary>
    public sealed class PresentationContext
    {
        public CommandMode Mode { get; internal set; }
        public bool HasSelection { get; internal set; }
        public bool DeckCollapsed { get; internal set; }
    }

    public sealed partial class ArchipelagoLabController
    {
        public PresentationContext Presentation { get; } = new PresentationContext();
        private readonly Dictionary<CommandMode, Button> modeButtons = new Dictionary<CommandMode, Button>();
        private CommandMode? chooserMode;
        private string observedPort, observedShip;
        private bool commandDeckBuilt;

        private void BuildCommandDeck()
        {
            var root = Element<VisualElement>("archipelagoLab");
            root.AddToClassList("sl-command-shell");
            var bar = Element<VisualElement>("topBar");
            bar.AddToClassList("sl-hud");
            Element<Label>("cashLabel").AddToClassList("command-cash");
            Element<Button>("pauseButton").text = "Pause";
            Element<Button>("playButton").text = "Play";
            var nav = Panel(root, "commandModeNav", "sl-mode-nav command-mode-nav");
            root.Insert(root.IndexOf(bar) + 1, nav);
            foreach (CommandMode mode in Enum.GetValues(typeof(CommandMode)))
                modeButtons.Add(mode, Button(nav, "mode" + mode, mode.ToString(), () => SwitchCommandMode(mode), "command-mode"));
            var notices = Button(bar, "noticesButton", "Notices", ToggleCommandNotices);
            notices.tooltip = "Current actionable stock, production and route blockers; no future price prediction.";
            var goal = Element<Button>("objectivesButton");
            bar.Insert(bar.IndexOf(Element<Label>("cashLabel")) + 1, goal);
            goal.AddToClassList("command-goals");

            // Preserve the complete old inspector and route controls in an intentional detail destination.
            var inspector = CreateScreen(ArchipelagoScreen.Inspector, "Operational details");
            var oldShell = Element<VisualElement>("contextShell");
            oldShell.AddToClassList("command-inspector");
            inspector.Add(oldShell);
            Button(inspector, "inspectorBackButton", "Back", () => { ShowScreen(secondaryParent); Refresh(); });
            inspector.Add(Element<Button>("ledgerButton"));
            inspector.Add(Element<Button>("royalServicesButton"));
            inspector.Add(Element<Label>("challengeLabel"));
            Root.Q(className: "scenario-bar").AddToClassList("hidden");

            var main = Element<VisualElement>("mainLayout");
            var deck = Panel(root, "commandDeck", "sl-paper command-deck");
            root.Insert(root.IndexOf(main) + 1, deck);
            var deckTop = Panel(deck, "deckTop", "command-deck-top");
            Text(deckTop, "deckHint", "Select a town or ship", "command-deck-hint");
            Button(deckTop, "deckCollapseButton", "Collapse", () =>
            {
                Presentation.DeckCollapsed = !Presentation.DeckCollapsed;
                RefreshCommandDeck();
            });
            var body = Panel(deck, "deckBody", "command-deck-body");
            var identity = Panel(body, "deckIdentity", "command-deck-identity");
            identity.Add(Element<Label>("selectionTitle"));
            identity.Add(Element<Label>("selectionSubtitle"));
            var facts = Panel(body, "deckFacts", "command-deck-facts");
            for (var i = 0; i < 3; i++) Text(facts, "deckFact" + i, "", "sl-metric command-fact");
            var actions = Panel(body, "deckActions", "command-deck-actions");
            actions.Add(Element<Button>("openTradeButton"));
            Element<Button>("openTradeButton").text = "Harbor";
            actions.Add(Element<Button>("openIndustryButton"));
            Element<Button>("openIndustryButton").text = "Industry";
            actions.Add(Element<Button>("cityEconomyButton"));
            Button(actions, "deckRouteButton", "Route planner", OpenSelectedShipRoute);
            Button(actions, "deckFleetButton", "Fleet", () => SwitchCommandMode(CommandMode.Fleet));
            Button(actions, "deckInspectButton", "Details", () => OpenSecondary(ArchipelagoScreen.Inspector));
            Text(deck, "deckReason", "", "sl-problem command-deck-reason");
            deck.Add(Element<VisualElement>("voyageBar"));
            var footer = Root.Q(className: "map-footer");
            var routes = new Toggle("Routes") { name = "mapRoutesToggle" };
            routes.RegisterValueChangedCallback(change => { Map.ShowAllRoutes = change.newValue; });
            footer.Add(routes);
            Button(footer, "mapDetailsButton", "Details / log", () => OpenSecondary(ArchipelagoScreen.Inspector));
            var noticePanel = Panel(main, "commandNotices", "sl-paper command-notices hidden");
            Text(noticePanel, null, "Current notices", "sl-section-title");
            var noticeRows = new ScrollView { name = "commandNoticeRows" };
            noticeRows.AddToClassList("command-scroll"); noticePanel.Add(noticeRows);
            Button(noticePanel, "noticesCloseButton", "Close notices", ToggleCommandNotices);
            var chooser = Panel(main, "commandChooser", "sl-paper command-chooser hidden");
            Text(chooser, "chooserTitle", "Choose a town", "sl-section-title");
            Panel(chooser, "chooserTowns", "command-chooser-towns");
            Button(chooser, "chooserBackButton", "Back to map", () => SwitchCommandMode(CommandMode.Map));
            BuildCommandWorkspaces(main);
            commandDeckBuilt = true;
            ApplyMaritimeKit();
            ResetPresentation();
        }

        private void ResetPresentation()
        {
            Presentation.Mode = CommandMode.Map;
            Presentation.HasSelection = false;
            Presentation.DeckCollapsed = false;
            chooserMode = null;
            economyPresented = null;
            noticesPresented = null;
            observedPort = Session.SelectedPortId;
            observedShip = Session.SelectedShipId;
            if (commandDeckBuilt)
            {
                Element<VisualElement>("commandNotices").AddToClassList("hidden");
                RefreshCommandMode();
            }
        }

        public void SelectCommandPort(string portId)
        {
            Presentation.HasSelection = true;
            Presentation.DeckCollapsed = false;
            Run(() => Session.SelectPort(portId));
        }

        public void SelectCommandShip(string shipId)
        {
            Presentation.HasSelection = true;
            Presentation.DeckCollapsed = false;
            Run(() => Session.SelectShip(shipId));
        }

        public void SwitchCommandMode(CommandMode mode)
        {
            Session.Pause();
            if (Screen != ArchipelagoScreen.Gameplay) ShowScreen(ArchipelagoScreen.Gameplay);
            CloseModals();
            Element<VisualElement>("commandNotices").AddToClassList("hidden");
            chooserMode = null;
            if (mode == CommandMode.Trade || mode == CommandMode.Industry)
            {
                if (!Presentation.HasSelection)
                {
                    chooserMode = mode;
                    BuildCommandChooser(mode);
                }
                else if (mode == CommandMode.Trade) OpenTrade(Session.SelectedPortId);
                else OpenCommandIndustry();
            }
            else if (mode == CommandMode.Objectives) OpenObjectives();
            else
            {
                Presentation.Mode = mode;
                if (mode == CommandMode.Fleet && (Session.SelectedShipId == null || !Session.Snapshot.Ships[Session.SelectedShipId].IsPlayer))
                    SelectCommandShip(Session.Player.Id);
                if (mode == CommandMode.Economy) Presentation.HasSelection = true;
            }
            Refresh();
        }

        private void OpenCommandIndustry()
        {
            Session.Pause();
            selectedFacilityId = null;
            loadedPolicyFacilityId = null;
            ShowModal("industryBackdrop", "closeIndustryButton");
            RefreshIndustry();
        }

        private void OpenSelectedShipRoute()
        {
            if (Session.SelectedShipId != null && !Session.Snapshot.Ships[Session.SelectedShipId].IsPlayer) return;
            OpenRoutePlanner(Session.SelectedShipId ?? Session.Player.Id);
        }

        private void BuildCommandChooser(CommandMode mode)
        {
            SetText("chooserTitle", mode + " · choose a town");
            var rows = Element<VisualElement>("chooserTowns");
            rows.Clear();
            foreach (var port in Session.Snapshot.Ports.Values.OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                var id = port.Id;
                Button(rows, "chooseCommand_" + id, port.Name + " · " + ShortRole(port.Role), () =>
                {
                    SelectCommandPort(id);
                    SwitchCommandMode(mode);
                }, "sl-card");
            }
        }

        private bool CancelCommandView()
        {
            if (!Element<VisualElement>("commandNotices").ClassListContains("hidden"))
            {
                Element<VisualElement>("commandNotices").AddToClassList("hidden");
                Element<Button>("noticesButton").Focus();
                return true;
            }
            if (chooserMode != null || Presentation.Mode != CommandMode.Map)
            {
                SwitchCommandMode(CommandMode.Map);
                return true;
            }
            return false;
        }

        private void RefreshCommandMode()
        {
            if (!commandDeckBuilt) return;
            var mode = TradeIsOpen ? CommandMode.Trade : IndustryIsOpen ? CommandMode.Industry :
                Screen == ArchipelagoScreen.Objectives ? CommandMode.Objectives : chooserMode ?? Presentation.Mode;
            foreach (var pair in modeButtons) pair.Value.EnableInClassList("sl-selected", pair.Key == mode);
            var fleet = Presentation.Mode == CommandMode.Fleet && chooserMode == null;
            var economy = Presentation.Mode == CommandMode.Economy && chooserMode == null;
            Element<VisualElement>("archipelagoLab").EnableInClassList("command-fleet-mode", fleet);
            Root.Q(className: "map-side").EnableInClassList("hidden", economy || chooserMode != null);
            Element<VisualElement>("fleetCommandList").EnableInClassList("hidden", !fleet);
            Element<VisualElement>("fleetCommandInspector").EnableInClassList("hidden", !fleet);
            Element<VisualElement>("economyWorkspace").EnableInClassList("hidden", !economy);
            Element<VisualElement>("commandDeck").EnableInClassList("hidden", fleet || economy || chooserMode != null);
            Element<VisualElement>("commandChooser").EnableInClassList("hidden", chooserMode == null);
        }

        private void RefreshCommandDeck()
        {
            if (!commandDeckBuilt) return;
            if (observedPort != Session.SelectedPortId || observedShip != Session.SelectedShipId) Presentation.HasSelection = true;
            observedPort = Session.SelectedPortId;
            observedShip = Session.SelectedShipId;
            var snapshot = Session.Snapshot;
            var port = snapshot.Ports[Session.SelectedPortId];
            var ship = Session.SelectedShipId == null ? null : snapshot.Ships[Session.SelectedShipId];
            var expanded = Presentation.HasSelection && !Presentation.DeckCollapsed;
            Element<VisualElement>("deckBody").EnableInClassList("hidden", !expanded);
            Element<Label>("deckReason").EnableInClassList("hidden", !expanded);
            Element<VisualElement>("voyageBar").EnableInClassList("hidden", !expanded || Session.PendingDestinationId == null);
            SetText("deckHint", !Presentation.HasSelection ? "Select a town or ship" :
                ship?.Name ?? port.Name);
            Element<Label>("deckHint").EnableInClassList("hidden", expanded);
            Element<Button>("deckCollapseButton").text = expanded ? "Collapse ▾" : "Expand ▴";
            Element<Button>("deckCollapseButton").SetEnabled(Presentation.HasSelection);
            SetText("cashLabel", "Cash " + CompactCoins(Session.Player.Cash, snapshot.MoneyScale) +
                (snapshot.Economy == null ? "" : " · Ref. value " + CompactCoins(snapshot.CompanyValue(Session.Player.OwnerId).Total, snapshot.MoneyScale)));
            Element<Label>("cashLabel").tooltip = "Spendable company cash: " + Money(Session.Player.Cash) + " coins." +
                (snapshot.Economy == null ? "" : " Reference Company Value: " + Money(snapshot.CompanyValue(Session.Player.OwnerId).Total) +
                " coins; not cash, profit or liquidation proceeds.");
            SetText("dayLabel", "DAY " + snapshot.Day + (snapshot.Challenge == null ? "" : " / " + snapshot.Challenge.Configuration.DeadlineDay));
            var goals = Element<Button>("objectivesButton");
            goals.RemoveFromClassList("hidden");
            goals.text = Session.Identity?.Commission != null ? "Goals " + GrandCommission.Progress(snapshot).CurrentlyMetCount + "/4" :
                snapshot.Challenge == null ? "Sandbox" : "Goals";
            goals.tooltip = ObjectiveText();
            Element<Button>("openTradeButton").SetEnabled(ship == null || ship.DockedPortId != null);
            Element<Button>("openIndustryButton").EnableInClassList("hidden", ship != null);
            Element<Button>("cityEconomyButton").EnableInClassList("hidden", ship != null);
            Element<Button>("deckRouteButton").EnableInClassList("hidden", ship == null || !ship.IsPlayer);
            Element<Button>("deckRouteButton").text = "Route planner";
            Element<Button>("deckFleetButton").EnableInClassList("hidden", ship == null || !ship.IsPlayer);
            Element<Button>("deckInspectButton").EnableInClassList("hidden", ship == null || ship.IsPlayer);
            if (ship == null) RefreshTownDeck(port);
            else RefreshShipDeck(ship);
            RefreshCommandNotices();
            RefreshCommandWorkspaces();
            RefreshCommandMode();
            Element<Label>("statusLabel").EnableInClassList("hidden", !statusIsError);
        }

        private void RefreshTownDeck(RegionalPortSnapshot port)
        {
            SetText("selectionSubtitle", ShortRole(port.Role));
            var need = EconomicSignals.NextEssentialNeed(Session.Snapshot, port.Id);
            SetText("deckFact0", !need.Modeled ? "Households\nNot modeled" : !need.HasNeed ? "Food\nNo scheduled need" :
                "Bread · due D" + need.NextDueDay + "\n" + Q(need.MarketStock) + " / " + Q(need.DesiredQuantity));
            Element<Label>("deckFact0").tooltip = "Current public stock / next need at current population and exact carried demand remainder. Imports, production, other purchases and future prices may change availability.";
            SetText("deckFact1", "Market cash\n" + CompactCoins(port.MarketCash, Session.Snapshot.MoneyScale) + " coins");
            var production = PriorityProduction(port.Id);
            SetText("deckFact2", production == null ? "Production\nNo facility" : ProductionName(production) + "\n" + ReadinessLabel(production));
            SetText("deckReason", TownReason(need, production));
            Element<Label>("deckReason").EnableInClassList("sl-warning", need.StockShortfall > 0 || need.AffordabilityShortfall > 0 || IsProductionProblem(production));
        }

        private void RefreshShipDeck(RegionalShipSnapshot ship)
        {
            SetText("deckFact0", ship.DockedPortId == null ? "Arrival\nD" + checked(Session.Snapshot.Day + ship.RemainingTravelDays) + " · " + PortName(ship.DestinationPortId) :
                "Docked\n" + PortName(ship.DockedPortId));
            SetText("deckFact1", "Ship cargo\n" + Q(ship.CargoUnits) + " / " + Q(ship.CargoCapacity));
            SetText("deckFact2", "Control\n" + (ship.AutomationEnabled ? "Automated" : "Manual"));
            SetText("deckReason", ShipInstruction(ship));
            Element<Label>("deckReason").EnableInClassList("sl-warning", ShipBlocked(ship));
        }

        public static string CompactCoins(long amount, int scale)
        {
            if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            var coins = (decimal)amount / scale;
            if (coins > 0 && coins < 0.1m) return "<0.1";
            return coins.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string ShortRole(string role)
        {
            return ArchipelagoMap.ShortRole(role);
        }
    }
}
