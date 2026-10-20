using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public enum ArchipelagoScreen { Gameplay, MainMenu, Setup, Pause, Results, Encyclopedia, Settings, Ledger, Logistics }

    public static class ArchipelagoApplicationExit
    {
        public static void Execute(bool inEditor, Action stopEditor, Action quitPlayer)
        {
            if (inEditor) stopEditor();
            else quitPlayer();
        }
    }

    public sealed partial class ArchipelagoLabController
    {
        private readonly Dictionary<ArchipelagoScreen, VisualElement> screens = new Dictionary<ArchipelagoScreen, VisualElement>();
        private VisualElement navigationRoot;
        private ArchipelagoScreen secondaryParent;
        private Action confirmedAction;
        private RegionalWorldSnapshot presentedResult;
        private bool resumePlayback;
        public ArchipelagoScreen Screen { get; private set; } = ArchipelagoScreen.MainMenu;
        public bool HasActiveRun { get; private set; }
        public bool ConfirmationIsOpen => confirmedAction != null;
        public bool SmoothMovement { get; private set; } = true;

        private void BindRunExperience()
        {
            navigationRoot = Panel(Root.Q("archipelagoLab"), "runNavigation", "run-navigation");
            BuildMainMenu();
            BuildRunSetup();
            BuildPauseMenu();
            BuildResults();
            BuildSettings();
            BuildEncyclopedia();
            BuildLedgerAndLogistics();
            Text(navigationRoot, "navigationStatus", "", "run-copy error");
            var confirmation = Panel(Root.Q("archipelagoLab"), "confirmationBackdrop", "run-confirmation hidden");
            var card = Panel(confirmation, "confirmationCard", "run-confirmation-card");
            Text(card, "confirmationTitle", "Leave this run?", "run-heading");
            Text(card, "confirmationMessage", "", "run-copy");
            var actions = Panel(card, null, "row");
            Button(actions, "confirmCancelButton", "Keep playing", CancelConfirmation);
            Button(actions, "confirmAcceptButton", "Confirm", () =>
            {
                var action = confirmedAction;
                CancelConfirmation();
                action?.Invoke();
            }, "primary");
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || TradeIsOpen || IndustryIsOpen) return;
                evt.StopImmediatePropagation();
                HandleCancel();
            }, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                if (TradeIsOpen || IndustryIsOpen) return;
                evt.StopImmediatePropagation();
                HandleCancel();
            }, TrickleDown.TrickleDown);
            var identityDetails = new Foldout { name = "runIdentityFoldout", text = "Run identity", value = false };
            Element<ScrollView>("contextScroll").Add(identityDetails);
            Text(identityDetails, "runIdentityLabel", "", "run-copy");
            Button(identityDetails, "copyRunSeedButton", "Copy seed", () => CopySeed(Session.Identity));
            var bar = Element<VisualElement>("topBar");
            Button(bar, "ledgerButton", "Ledger", () => OpenSecondary(ArchipelagoScreen.Ledger));
            Button(bar, "menuButton", "Menu", OpenPauseMenu);
            Button(Element<VisualElement>("contextScroll").contentContainer, "logisticsButton",
                "Industrial shipping", OpenLogistics);
            Button(Element<VisualElement>("contextScroll").contentContainer, "gameHelpButton",
                "Encyclopedia / help", () => OpenSecondary(ArchipelagoScreen.Encyclopedia));
        }

        private VisualElement CreateScreen(ArchipelagoScreen screen, string title, string subtitle = null)
        {
            var panel = Panel(navigationRoot, "screen" + screen, "run-screen hidden");
            screens.Add(screen, panel);
            Text(panel, null, "SALT & LEDGER  /  " + title.ToUpperInvariant(), "run-eyebrow");
            if (screen != ArchipelagoScreen.MainMenu) Text(panel, null, title, "run-heading");
            if (subtitle != null) Text(panel, null, subtitle, "run-copy");
            return panel;
        }

        private void BuildMainMenu()
        {
            var screen = CreateScreen(ArchipelagoScreen.MainMenu, "The archipelago awaits");
            var layout = Panel(screen, null, "run-title-layout");
            var copy = new ScrollView(ScrollViewMode.Vertical) { name = "mainMenuScroll" };
            copy.AddToClassList("run-title-copy");
            layout.Add(copy);
            Text(copy, null, "Salt\n& Ledger", "run-title");
            Text(copy, null, "A COMPANY. A SHIP. AN OPEN HORIZON.", "run-eyebrow");
            Text(copy, null, "Trade across a living archipelago. Build an industry, connect its supply lines, and decide what your company will become.", "run-introduction");
            Button(copy, "newRunButton", "New Run", () => OpenSetup(false), "primary run-menu-button");
            Button(copy, "sandboxMenuButton", "Sandbox", () => OpenSetup(true), "run-menu-button");
            Button(copy, "menuEncyclopediaButton", "Encyclopedia", () => OpenSecondary(ArchipelagoScreen.Encyclopedia), "run-menu-button");
            Button(copy, "menuSettingsButton", "Settings", () => OpenSecondary(ArchipelagoScreen.Settings), "run-menu-button");
            Button(copy, "menuExitButton", "Exit Game", RequestExit, "run-menu-button");
            var art = new HarborIllustration();
            art.AddToClassList("run-title-art");
            layout.Add(art);
            Text(screen, null, "Handcrafted voyages · finite markets · meaningful choices\nRuns live only in this application session. There is no persistent save or recovery.", "run-footnote");
        }

        public void BeginRun(string challengeId, string charterId) => BeginRun(new RunIdentity(challengeId, charterId));

        public void BeginRun(RunIdentity identity)
        {
            CloseModals();
            Session.StartRun(identity);
            LoadSetupIdentity(identity);
            HasActiveRun = true;
            presentedResult = null;
            tradeShipId = Session.Player.Id;
            selectedFacilityId = null;
            status = null;
            statusIsError = false;
            LoadPolicy();
            Element<DropdownField>("speedDropdown").SetValueWithoutNotify("1×");
            Element<TextField>("quantityField").SetValueWithoutNotify("2");
            Element<Foldout>("automationFoldout").SetValueWithoutNotify(false);
            Element<ScrollView>("contextScroll").scrollOffset = Vector2.zero;
            ShowScreen(ArchipelagoScreen.Gameplay);
            Refresh();
            Element<Button>("playButton").Focus();
        }

        private void BuildPauseMenu()
        {
            var screen = CreateScreen(ArchipelagoScreen.Pause, "At anchor", "Economic time is paused. Inspect your accounts, change course, or leave this run.");
            var pauseScroll = new ScrollView(ScrollViewMode.Vertical) { name = "pauseScroll" };
            pauseScroll.AddToClassList("run-pause-scroll");
            screen.Add(pauseScroll);
            var card = Panel(pauseScroll, null, "run-pause-card run-card");
            Text(card, "pauseSummary", "", "run-copy");
            Button(card, "resumeButton", "Resume", ResumeGame, "primary run-menu-button");
            Button(card, "pauseEncyclopediaButton", "Encyclopedia", () => OpenSecondary(ArchipelagoScreen.Encyclopedia), "run-menu-button");
            Button(card, "pauseLedgerButton", "Company ledger", () => OpenSecondary(ArchipelagoScreen.Ledger), "run-menu-button");
            Button(card, "pauseSettingsButton", "Settings", () => OpenSecondary(ArchipelagoScreen.Settings), "run-menu-button");
            Button(card, "restartRunButton", "Retry Same World", RequestRestart, "run-menu-button");
            Button(card, "changeSetupButton", "Change Setup", RequestChangeSetup, "run-menu-button");
            Button(card, "rerollWorldButton", "New World / Reroll", RequestReroll, "run-menu-button");
            Button(card, "abandonRunButton", "Abandon and inspect results", () => Confirm(
                "Abandon this challenge?", "This ends the current run with an abandonment result. Your assets and financial summary remain visible.",
                () => Run(() => Session.AbandonRun())), "run-menu-button");
            Button(card, "returnMenuButton", "Return to Main Menu", RequestMainMenu, "run-menu-button");
            Button(card, "pauseExitButton", "Exit Game", RequestExit, "run-menu-button");
        }

        public void OpenPauseMenu()
        {
            if (!HasActiveRun || Session.IsChallengeEnded) return;
            resumePlayback = Session.IsPlaying;
            Session.Pause();
            CloseModals();
            SetText("pauseSummary", RunScenario.GetCharter(Session.CharterId).Name + " · Day " + Session.Snapshot.Day +
                " · Cash " + Number(Session.Player.Cash) + "\n" +
                (Session.Snapshot.Challenge?.Configuration.Title ?? "Unlimited sandbox") + "\n" + WorldIdentityText(Session.Identity));
            Element<Button>("abandonRunButton").EnableInClassList("hidden", Session.Snapshot.Challenge == null);
            ShowScreen(ArchipelagoScreen.Pause);
            Element<Button>("resumeButton").Focus();
            Refresh();
        }

        private void ResumeGame()
        {
            ShowScreen(ArchipelagoScreen.Gameplay);
            if (resumePlayback && !Session.IsChallengeEnded) Session.Play();
            Refresh();
            Element<Button>("menuButton").Focus();
        }

        private void RequestRestart() => Confirm("Retry Same World?",
            "Discard progress and restore this exact world, seed, ascension, challenge and starting charter. Runs are not saved.",
            () => BeginRun(Session.Identity));

        private void RequestMainMenu()
        {
            if (HasActiveRun && !Session.IsChallengeEnded)
                Confirm("Return to Main Menu?", "The active run will be discarded. This application does not save or recover unfinished runs.",
                    ReturnToMainMenu);
            else ReturnToMainMenu();
        }
        private void ReturnToMainMenu()
        {
            Session.Pause();
            if (HasActiveRun && Session.Snapshot.Challenge != null && !Session.IsChallengeEnded) Session.AbandonRun();
            HasActiveRun = false;
            CloseModals();
            ShowScreen(ArchipelagoScreen.MainMenu);
            Element<Button>("newRunButton").Focus();
        }

        private void RequestExit()
        {
            if (HasActiveRun && !Session.IsChallengeEnded)
                Confirm("Exit Salt & Ledger?", "Your active run will be lost. There is no persistent save.", ExitGame);
            else ExitGame();
        }
        private void ExitGame()
        {
            Session.Pause();
            Debug.Log("Salt & Ledger exit requested.");
            ArchipelagoApplicationExit.Execute(Application.isEditor, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }, () => Application.Quit());
        }

        private void Confirm(string title, string message, Action action)
        {
            Session.Pause();
            Refresh();
            confirmedAction = action;
            SetText("confirmationTitle", title);
            SetText("confirmationMessage", message);
            Element<VisualElement>("confirmationBackdrop").RemoveFromClassList("hidden");
            navigationRoot.SetEnabled(false);
            Element<Button>("confirmCancelButton").Focus();
        }
        private void CancelConfirmation()
        {
            confirmedAction = null;
            Element<VisualElement>("confirmationBackdrop").AddToClassList("hidden");
            navigationRoot.SetEnabled(true);
            Refresh();
        }

        private void BuildResults()
        {
            var screen = CreateScreen(ArchipelagoScreen.Results, "Voyage concluded");
            Text(screen, "resultOutcome", "", "run-result-title");
            Text(screen, "resultIdentity", "", "run-copy");
            var scroll = new ScrollView { name = "resultsScroll" };
            scroll.AddToClassList("run-content-scroll");
            screen.Add(scroll);
            Text(scroll, "resultObjective", "", "run-objective");
            Text(scroll, "resultScore", "", "run-copy");
            Text(scroll, "resultFinances", "", "run-copy");
            Text(scroll, "resultAssets", "", "run-copy");
            Text(scroll, "resultOperations", "", "run-copy");
            Text(scroll, null, "Realized gross margin deducts the paid material basis released by sales. Wages, transport operating costs, depreciation and asset resale values are not modeled. Grants have zero paid basis, not zero economic value. Compare scores only within the same challenge, charter, ascension and exact world identity. Different seeds are not equivalent leaderboard placements.", "run-footnote");
            var actions = Panel(screen, null, "run-actions row");
            Button(actions, "retryRunButton", "Retry Same World", () => BeginRun(Session.Identity), "primary");
            Button(actions, "resultsNewRunButton", "Change Setup", ChangeSetup);
            Button(actions, "resultsRerollButton", "New World / Reroll", RerollWorld);
            Button(actions, "resultsMenuButton", "Main Menu", ReturnToMainMenu);
            Button(actions, "resultsHelpButton", "Encyclopedia", () => OpenSecondary(ArchipelagoScreen.Encyclopedia));
        }

        private void PresentResults()
        {
            Session.Pause();
            CloseModals();
            presentedResult = Session.Snapshot;
            var result = new RunResults(presentedResult, Session.CharterId);
            SetText("resultOutcome", result.Challenge.Status == ChallengeStatus.Succeeded ? "VICTORY" :
                result.Challenge.Status == ChallengeStatus.Abandoned ? "RUN ABANDONED" : "DEFEAT");
            SetText("resultIdentity", result.Challenge.Configuration.Title + "\n" + result.Charter.Name +
                " · Day " + presentedResult.Day + " of " + result.Challenge.Configuration.DeadlineDay +
                "\n" + WorldIdentityText(result.Identity));
            SetText("resultObjective", ObjectiveText());
            SetText("resultScore", "Score " + result.TotalScore + "\nObjective " + result.ObjectiveScore +
                " / 500 · Victory " + result.VictoryScore + " / 1,000 · Time remaining " + result.TimeScore +
                " / 600 · Cash " + result.SolvencyScore + " / 100\n" +
                "Comparison group: " + result.ComparisonGroup.Replace("/", " / ") +
                "\nObjective: 500 × capped completion fraction. Victory: 1,000. Winning early: 10 per remaining day, capped at 60 days. Cash: 1 per 10, capped at 100.");
            SetText("resultFinances", FinancialText());
            SetText("resultAssets", AssetText());
            SetText("resultOperations", OperationsText());
            ShowScreen(ArchipelagoScreen.Results);
            Element<Button>("retryRunButton").Focus();
        }

        private void BuildSettings()
        {
            var screen = CreateScreen(ArchipelagoScreen.Settings, "Presentation settings",
                "Text size applies throughout the game and is remembered on this device. Other presentation settings last for this application session. Neither changes economic rules.");
            var card = Panel(screen, null, "run-card");
            var smooth = new Toggle("Smooth ship movement between committed days") { name = "smoothMovementToggle", value = true };
            card.Add(smooth);
            smooth.RegisterValueChangedCallback(change => { SmoothMovement = change.newValue; Refresh(); });
            var scale = new DropdownField("Game-wide text size", new List<string> { "Compact", "Default", "Large", "Extra Large" },
                (int)TextScale.Current) { name = "textScaleDropdown" };
            card.Add(scale);
            scale.RegisterValueChangedCallback(_ => TextScale.Apply((ArchipelagoUiScale)scale.index));
            Text(card, null, "Economic speed remains under your control on the map. Pause and menus stop the economic clock.", "run-copy");
            Button(screen, "settingsBackButton", "Back", () => ShowScreen(secondaryParent));
        }

        private void OpenSecondary(ArchipelagoScreen screen)
        {
            secondaryParent = Screen;
            Session.Pause();
            ShowScreen(screen);
            if (screen == ArchipelagoScreen.Encyclopedia) RefreshEncyclopedia();
            if (screen == ArchipelagoScreen.Ledger) RefreshLedger();
        }

        public void HandleCancel()
        {
            if (ConfirmationIsOpen) { CancelConfirmation(); return; }
            if (TradeIsOpen || IndustryIsOpen) { CloseModals(); return; }
            if (Screen == ArchipelagoScreen.Encyclopedia) { EncyclopediaBack(); return; }
            if (Screen == ArchipelagoScreen.Settings || Screen == ArchipelagoScreen.Ledger || Screen == ArchipelagoScreen.Logistics)
            { ShowScreen(secondaryParent); return; }
            if (Screen == ArchipelagoScreen.Setup) { ShowScreen(ArchipelagoScreen.MainMenu); return; }
            if (Screen == ArchipelagoScreen.Pause) { ResumeGame(); return; }
            if (Screen == ArchipelagoScreen.Gameplay) OpenPauseMenu();
        }

        private void ShowScreen(ArchipelagoScreen screen)
        {
            Screen = screen;
            if (screen != ArchipelagoScreen.Gameplay) Session?.Pause();
            navigationRoot.EnableInClassList("hidden", screen == ArchipelagoScreen.Gameplay);
            foreach (var pair in screens) pair.Value.EnableInClassList("hidden", pair.Key != screen);
            Element<VisualElement>("topBar").SetEnabled(screen == ArchipelagoScreen.Gameplay);
            Element<VisualElement>("mainLayout").SetEnabled(screen == ArchipelagoScreen.Gameplay);
            if (screen == ArchipelagoScreen.Gameplay) Root.Q<Button>("menuButton")?.Focus();
            else if (screens.TryGetValue(screen, out var active)) active.Q<Button>()?.Focus();
        }

        private void RefreshRunExperience()
        {
            if (navigationRoot == null) return;
            if (HasActiveRun && Session.IsChallengeEnded && !ReferenceEquals(presentedResult, Session.Snapshot)) PresentResults();
            SetText("runIdentityLabel", WorldIdentityText(Session.Identity) + "\n" +
                (Session.ChallengeId == null ? "Unlimited sandbox" : RunScenario.GetChallenge(Session.ChallengeId).Name) +
                "\n" + RunScenario.GetCharter(Session.CharterId).Name);
            Element<Button>("copyRunSeedButton").SetEnabled(Session.Identity?.Seed != null);
            if (Screen == ArchipelagoScreen.Ledger) RefreshLedger();
            if (Screen == ArchipelagoScreen.Logistics) RefreshLogisticsStatus();
        }

        private string ObjectiveText()
        {
            var challenge = Session.Snapshot.Challenge;
            if (challenge == null) return "Sandbox · no deadline. Build your enterprise at your own pace.";
            var config = challenge.Configuration;
            var measure = config.Kind == ChallengeKind.RegionalRelief ? "Bread consumed regionally" :
                config.Kind == ChallengeKind.EnterpriseAmbition ? "Bread produced privately" : "Net Bread supplied to Eastport";
            return config.Title + " · " + challenge.Status + "\n" + measure + ": " + challenge.ObjectiveQuantity +
                " / " + config.RequiredQuantity +
                (config.Kind == ChallengeKind.EnterpriseAmbition ? " · Realized margin " + challenge.RealizedGrossMargin +
                    " / " + config.RequiredMargin : "") +
                "\n" + Math.Max(0, config.DeadlineDay - Session.Snapshot.Day) + " days remaining · Cash " +
                Session.Player.Cash + " / " + config.MinimumCash + " minimum";
        }

        private static VisualElement Panel(VisualElement parent, string name, string classes)
        {
            var element = new VisualElement { name = name };
            foreach (var item in classes.Split(' ')) if (item.Length > 0) element.AddToClassList(item);
            parent.Add(element);
            return element;
        }
        private static Label Text(VisualElement parent, string name, string value, string classes)
        {
            var label = new Label(value) { name = name };
            foreach (var item in classes.Split(' ')) if (item.Length > 0) label.AddToClassList(item);
            parent.Add(label);
            return label;
        }
        private static Button Button(VisualElement parent, string name, string value, Action action, string classes = "")
        {
            var button = new Button(action) { name = name, text = value };
            foreach (var item in classes.Split(' ')) if (item.Length > 0) button.AddToClassList(item);
            parent.Add(button);
            return button;
        }
    }
}
