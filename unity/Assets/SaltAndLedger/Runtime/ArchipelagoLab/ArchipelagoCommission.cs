using System;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private GrandCommissionDraft commissionDraft;
        private readonly string[] commissionSelections = new string[4];
        private int commissionRound;
        private RegionalWorldSnapshot commissionPreview;
        public RegionalWorldSnapshot CommissionPreview => commissionPreview;

        private void BuildCommissionScreens()
        {
            var mapScreen = CreateScreen(ArchipelagoScreen.CommissionMap, "Read the archipelago",
                "Select a town to inspect your starting world.");
            Text(mapScreen, "commissionMapIdentity", "", "run-copy");
            var mapScroll = new ScrollView { name = "commissionMapScroll" };
            mapScroll.AddToClassList("run-content-scroll");
            mapScreen.Add(mapScroll);
            var chart = Panel(mapScroll, null, "commission-chart-layout");
            Panel(chart, "commissionMapHost", "commission-map-host");
            var details = new ScrollView { name = "commissionTownScroll" };
            details.AddToClassList("commission-town-details"); chart.Add(details);
            Text(details, "commissionMapSelection", "Select a town to inspect.", "run-objective");
            Text(details, "commissionMapDetails", "", "run-copy");
            var routes = new Foldout { name = "commissionRoutes", text = "Exact connections and starting travel times" };
            mapScroll.Add(routes);
            Text(routes, "commissionRouteDetails", "", "run-copy");
            Text(mapScroll, null, "Drafting is free. Production still needs materials, wages and storage.", "run-footnote");
            var mapActions = Panel(mapScreen, null, "row run-actions");
            Button(mapActions, "commissionMapBackButton", "Back to setup", () => ShowScreen(ArchipelagoScreen.Setup));
            Button(mapActions, "commissionMapNextButton", "Draft objectives", () =>
            {
                if (SetupChallengeId() == GrandCommission.ChallengeId) ShowCommissionRound(0);
                else if (setupIdentity != null) BeginRun(setupIdentity);
            }, "primary");

            var draft = CreateScreen(ArchipelagoScreen.CommissionDraft, "Draft your commission",
                "Choose one card per round. All four goals are mandatory.");
            Text(draft, "commissionRoundTitle", "", "run-objective");
            Text(draft, "commissionDraftWorld", "", "run-footnote");
            var draftScroll = new ScrollView { name = "commissionDraftScroll" };
            draftScroll.AddToClassList("run-content-scroll");
            draft.Add(draftScroll);
            Panel(draftScroll, "commissionOfferCards", "commission-offers");
            Text(draft, "commissionSelections", "", "commission-selections");
            var draftActions = Panel(draft, null, "row run-actions");
            Button(draftActions, "commissionCancelButton", "Cancel to setup", () => ShowScreen(ArchipelagoScreen.Setup));
            Button(draftActions, "commissionBackButton", "Back", BackCommissionRound);
            Button(draftActions, "commissionNextButton", "Next category", () =>
            {
                if (commissionSelections[commissionRound] == null) return;
                if (commissionRound == 3) ShowCommissionReview();
                else ShowCommissionRound(commissionRound + 1);
            }, "primary");

            var review = CreateScreen(ArchipelagoScreen.CommissionReview, "Review & begin",
                "Meet all four goals together, with the required cash, by the deadline.");
            Text(review, "commissionReviewWorld", "", "run-copy");
            Text(review, "commissionReviewDeadline", "", "run-objective");
            var reviewScroll = new ScrollView { name = "commissionReviewScroll" };
            reviewScroll.AddToClassList("run-content-scroll");
            review.Add(reviewScroll);
            Panel(reviewScroll, "commissionReviewGoals", "commission-review-goals");
            var reviewActions = Panel(review, null, "row run-actions");
            Button(reviewActions, "commissionReviewBackButton", "Back to choices", () => ShowCommissionRound(3));
            Button(reviewActions, "commissionReviewSetupButton", "Change setup", () => ShowScreen(ArchipelagoScreen.Setup));
            Button(reviewActions, "commissionBeginButton", "Begin Grand Commission", () =>
            {
                RefreshSetup();
                if (SetupChallengeId() == GrandCommission.ChallengeId && setupIdentity?.Commission != null)
                    BeginRun(setupIdentity);
            }, "primary");
            Element<Button>("commissionBeginButton").SetEnabled(false);

            var journal = CreateScreen(ArchipelagoScreen.Objectives, "Objectives / Journal",
                "All four required · Cumulative stays earned · Current can fall again");
            Text(journal, "objectivesOverview", "", "run-objective");
            Text(journal, "objectivesDeadline", "", "run-copy");
            var journalScroll = new ScrollView { name = "objectivesScroll" };
            journalScroll.AddToClassList("run-content-scroll");
            journal.Add(journalScroll);
            Panel(journalScroll, "objectiveCards", "commission-journal-cards");
            var journalActions = Panel(journal, null, "row run-actions");
            Button(journalActions, "objectivesBackButton", "Back", CloseObjectives, "primary");
            Button(journalActions, "objectivesCopyIdentityButton", "Copy run identity", () =>
                GUIUtility.systemCopyBuffer = Session.Identity.ComparisonGroup);
        }

        private void EnsureCommissionDraft(int seed)
        {
            if (commissionDraft != null && commissionDraft.Seed == seed) return;
            commissionDraft = GrandCommissionDraft.Generate(seed);
            Array.Clear(commissionSelections, 0, commissionSelections.Length);
            commissionRound = 0;
            Element<Button>("commissionBeginButton")?.SetEnabled(false);
        }

        private GrandCommissionPlan SelectedCommission()
        {
            if (commissionDraft == null || commissionSelections.Any(id => id == null)) return null;
            return commissionDraft.Resolve(commissionSelections);
        }

        private void RestoreCommissionSelection(RunIdentity identity)
        {
            if (identity.Commission == null) return;
            EnsureCommissionDraft(identity.Seed.Value);
            for (var index = 0; index < 4; index++) commissionSelections[index] = identity.Commission.Goals[index].TemplateId;
        }

        private void OpenCommissionMap()
        {
            RefreshSetup();
            if (setupIdentity == null) return;
            var previewIdentity = new RunIdentity(null, setupIdentity.CharterId, setupIdentity.WorldMode, setupIdentity.Seed,
                setupIdentity.Difficulty, livingPricingMode: setupIdentity.LivingPricingMode,
                companyStorageRules: setupIdentity.CompanyStorageRules, itineraryVersion: setupIdentity.ItineraryVersion,
                economicRulesVersion: setupIdentity.EconomicRulesVersion);
            commissionPreview = RunScenario.Create(Session.CanonicalContent, previewIdentity).Snapshot();
            SetText("commissionMapIdentity", WorldIdentityText(setupIdentity) + " · " + RunScenario.GetCharter(setupIdentity).Name);
            var host = Element<VisualElement>("commissionMapHost");
            host.Clear();
            var map = new ArchipelagoMap { name = "commissionPreviewMap" };
            host.Add(map);
            map.PortSelected += ShowCommissionPort;
            map.PortTradeRequested += ShowCommissionPort;
            map.ShipSelected += id =>
            {
                var ship = commissionPreview.Ships[id];
                if (ship.DockedPortId != null) ShowCommissionPort(ship.DockedPortId);
            };
            map.Present(commissionPreview, null, null, false, 1);
            SetText("commissionRouteDetails", string.Join("\n", commissionPreview.Routes.Select(route =>
                commissionPreview.Ports[route.OriginPortId].Name + " ↔ " + commissionPreview.Ports[route.DestinationPortId].Name +
                ": " + route.Distance + " units · " + route.DurationDays(RunScenario.StartingShipSpeed) + " days")));
            ShowCommissionPort(commissionPreview.Ships.Values.First(ship => ship.IsPlayer).DockedPortId);
            ShowScreen(ArchipelagoScreen.CommissionMap);
            Element<Button>("commissionMapNextButton").text = SetupChallengeId() == GrandCommission.ChallengeId ? "Draft objectives" : "Begin voyage";
            Element<ScrollView>("commissionMapScroll").scrollOffset = Vector2.zero;
            Element<Button>("commissionMapNextButton").Focus();
        }

        private void ShowCommissionPort(string portId)
        {
            var port = commissionPreview.Ports[portId];
            SetText("commissionMapSelection", port.Name + " · " + port.Role);
            var content = Session.CanonicalContent.ForExpandingEconomy();
            string Quantity(long value) => ((decimal)value / commissionPreview.QuantityScale).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            string Coins(long value) => ((decimal)value / commissionPreview.MoneyScale).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            SetText("commissionMapDetails", "Market cash " + Coins(port.MarketCash) +
                (port.City == null ? "" : " · households " + Coins(port.City.HouseholdCash) + "\nPopulation " + port.City.Population + " · prosperity " + port.City.Prosperity + "/100") +
                "\n\nSTARTING MARKET STOCK\n" + string.Join(" · ", port.Inventory.Select(item => content.GetGood(item.Key).Name + " " + Quantity(item.Value))) +
                "\n\nPUBLIC PRODUCTION\n" + string.Join(", ", port.Facilities.Select(f => content.GetBuilding(f.Configuration.Instance.BuildingId).Name)) +
                "\n\nYOU CAN BUILD\n" + string.Join(", ", commissionPreview.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(portId)).Select(b => content.GetBuilding(b.BuildingId).Name)) +
                "\n\nLOCAL NEEDS\n" + string.Join("\n", port.Consumption.Select(rule =>
                    Quantity(rule.Configuration.PreviewDemand(port.City?.Population ?? 0, rule.DemandRemainder).Quantity) + " " + content.GetGood(rule.Configuration.GoodId).Name +
                    " / " + rule.Configuration.IntervalDays + " days · " + NeedKind(rule.Configuration.Kind))) +
                (commissionPreview.Economy?.Configuration.MintPortId == portId ? "\n\nRoyal Mint, shipwright and finite external contracts." : ""));
            var map = Element<ArchipelagoMap>("commissionPreviewMap");
            map.Present(commissionPreview, portId, null, false, 1);
            foreach (var town in commissionPreview.Ports.Values)
                map.Q<Button>("port_" + town.Id).tooltip = town.Role + "\nSelect to inspect this town's starting economy. Preview only; no economic commands.";
        }

        private void ShowCommissionRound(int index)
        {
            if (setupIdentity == null || commissionDraft == null) return;
            commissionRound = index;
            var round = commissionDraft.Rounds[index];
            SetText("commissionRoundTitle", "Round " + (index + 1) + " of 4 · " + round.Title);
            SetText("commissionDraftWorld", "Seed " + commissionDraft.Seed + " · Mission draft v" + commissionDraft.Version +
                " · offers stay identical when you go back");
            var cards = Element<VisualElement>("commissionOfferCards");
            cards.Clear();
            foreach (var goal in round.Offers)
            {
                var card = Panel(cards, "commissionOffer_" + goal.TemplateId, "commission-offer run-card");
                Text(card, null, goal.Title, "run-objective");
                Text(card, null, goal.Explanation, "run-copy");
                foreach (var condition in goal.Conditions)
                    Text(card, null, condition.Label + " · " + condition.TargetText, "commission-requirement");
                var id = goal.TemplateId;
                Button(card, "chooseCommission_" + id, "Choose objective", () =>
                {
                    commissionSelections[index] = id;
                    RefreshCommissionSelection();
                }, "commission-choose");
            }
            RefreshCommissionSelection();
            ShowScreen(ArchipelagoScreen.CommissionDraft);
            Element<ScrollView>("commissionDraftScroll").scrollOffset = Vector2.zero;
            cards.Q<Button>()?.Focus();
        }

        private void RefreshCommissionSelection()
        {
            foreach (var goal in commissionDraft.Rounds[commissionRound].Offers)
            {
                var selected = commissionSelections[commissionRound] == goal.TemplateId;
                Element<VisualElement>("commissionOffer_" + goal.TemplateId).EnableInClassList("commission-selected", selected);
                var button = Element<Button>("chooseCommission_" + goal.TemplateId);
                button.text = selected ? "✓ Selected" : "Choose objective";
                button.EnableInClassList("primary", selected);
            }
            SetText("commissionSelections", string.Join(" · ", commissionDraft.Rounds.Select((round, index) =>
                (index + 1) + ": " + (commissionSelections[index] == null ? "Not selected" :
                    round.Offers.First(goal => goal.TemplateId == commissionSelections[index]).Title))));
            var next = Element<Button>("commissionNextButton");
            next.text = commissionRound == 3 ? "Review & begin" : "Next category";
            next.SetEnabled(commissionSelections[commissionRound] != null);
            Element<Button>("commissionBeginButton").SetEnabled(SelectedCommission() != null);
        }

        private void BackCommissionRound()
        {
            if (commissionRound > 0) ShowCommissionRound(commissionRound - 1);
            else ShowScreen(ArchipelagoScreen.CommissionMap);
        }

        private void ShowCommissionReview()
        {
            RefreshSetup();
            var plan = setupIdentity?.Commission;
            Element<Button>("commissionBeginButton").SetEnabled(plan != null);
            if (plan == null) return;
            SetText("commissionReviewWorld", WorldIdentityText(setupIdentity) + "\n" + RunScenario.GetCharter(setupIdentity).Name +
                " · Mission draft v" + plan.Version + " · " + plan.Signature);
            SetText("commissionReviewDeadline", "All 4 objectives + at least " +
                CommissionConditionProgress.Format(GrandCommission.MinimumCash, CommissionUnit.Coins) +
                " company cash · deadline Day " + GrandCommission.DeadlineDay);
            var goals = Element<VisualElement>("commissionReviewGoals"); goals.Clear();
            for (var index = 0; index < plan.Goals.Count; index++)
            {
                var goal = plan.Goals[index];
                var card = Panel(goals, null, "run-card commission-review-card");
                Text(card, null, (index + 1) + ". " + goal.Title, "run-objective");
                Text(card, null, goal.Explanation, "run-copy");
                foreach (var condition in goal.Conditions)
                    Text(card, null, condition.Label + " · " + condition.TargetText, "commission-requirement");
                var editRound = index;
                Button(card, "commissionEdit" + index, "Change this objective", () => ShowCommissionRound(editRound));
            }
            ShowScreen(ArchipelagoScreen.CommissionReview);
            Element<Button>("commissionBeginButton").Focus();
        }

        private void OpenObjectives()
        {
            OpenSecondary(ArchipelagoScreen.Objectives);
            Element<Button>("objectivesBackButton").Focus();
        }

        private void CloseObjectives()
        {
            var parent = secondaryParent;
            ShowScreen(parent);
            Refresh();
            if (parent == ArchipelagoScreen.Gameplay) Element<Button>("objectivesButton").Focus();
        }

        private void RefreshObjectives()
        {
            if (Session.Identity?.Commission == null)
            {
                SetText("objectivesOverview", Session.Snapshot.Challenge?.Configuration.Title ?? "Sandbox");
                SetText("objectivesDeadline", ObjectiveText());
                Element<VisualElement>("objectiveCards").Clear();
                return;
            }
            var snapshot = Session.Snapshot;
            var progress = GrandCommission.Progress(snapshot);
            SetText("objectivesOverview", progress.CurrentlyMetCount + "/4 goals currently met · " +
                progress.PermanentlyFinishedCount + " permanently finished · " + snapshot.Challenge.Status);
            SetText("objectivesDeadline", "Day " + snapshot.Day + " · deadline Day " + GrandCommission.DeadlineDay +
                " · " + Math.Max(0, GrandCommission.DeadlineDay - snapshot.Day) + " days remaining" +
                "\nCompany cash " + Money(Session.Player.Cash) + " / " + Money(GrandCommission.MinimumCash) + " coins required at victory");
            RenderCommissionProgress(Element<VisualElement>("objectiveCards"), progress);
        }

        private void RefreshCommissionResults()
        {
            var parent = Element<VisualElement>("resultCommissionGoals"); parent.Clear();
            if (Session.Identity?.Commission == null) return;
            RenderCommissionProgress(parent, GrandCommission.Progress(Session.Snapshot));
            SetText("resultIdentity", Element<Label>("resultIdentity").text + "\nMission draft v" +
                Session.Identity.Commission.Version + " · " + Session.Identity.Commission.Signature);
        }

        private void RenderCommissionProgress(VisualElement parent, CommissionProgress progress)
        {
            var expanded = parent.Query<Foldout>().ToList().Where(foldout => foldout.value).Select(foldout => foldout.name).ToArray();
            parent.Clear();
            foreach (var goal in progress.Goals)
            {
                var card = Panel(parent, "objective_" + goal.Id, "run-card commission-progress-card");
                Text(card, null, (goal.IsMet ? "✓ " : "○ ") + goal.Title, "run-objective");
                Text(card, null, goal.IsPermanent && goal.IsMet ? "PERMANENTLY FINISHED" :
                    goal.IsMet ? "CURRENTLY MET · can change" : "NOT YET MET", "run-eyebrow");
                var bar = new ProgressBar { name = "objectiveBar_" + goal.Id, lowValue = 0, highValue = 100,
                    value = goal.Percent, title = goal.Percent + "%" };
                bar.AddToClassList("commission-progress"); card.Add(bar);
                var definition = Session.Identity.Commission.Goals.First(item => item.Id == goal.Id);
                foreach (var condition in goal.Conditions)
                {
                    var permanent = condition.Semantic == CommissionSemantic.Cumulative;
                    var label = definition.Conditions.First(item => item.Id == condition.Id).Label;
                    var text = Text(card, "objectiveCondition_" + condition.Id,
                        (condition.IsMet ? "✓ " : "○ ") + label + ": " + condition.CurrentText + " / " + condition.TargetText +
                        " · " + (permanent ? "Cumulative" : "Current"), "commission-requirement commission-condition-summary");
                    text.tooltip = condition.Label + "\n" + condition.Reason;
                    text.EnableInClassList("commission-finished", permanent && condition.IsMet);
                }
                var detailsName = "objectiveDetails_" + goal.Id;
                var details = new Foldout { name = detailsName, text = "Details & blockers", value = expanded.Contains(detailsName) };
                details.AddToClassList("commission-details"); card.Add(details);
                Text(details, null, goal.Explanation, "run-copy");
                foreach (var condition in goal.Conditions)
                    Text(details, null, condition.Label + "\n" +
                        (condition.Semantic == CommissionSemantic.Cumulative ? "Cumulative · " : "Current · ") + condition.Reason,
                        "commission-requirement");
            }
        }
    }
}
