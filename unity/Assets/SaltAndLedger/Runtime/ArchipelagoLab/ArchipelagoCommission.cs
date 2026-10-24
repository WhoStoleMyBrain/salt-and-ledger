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
        private bool objectiveResumePlayback;
        private RegionalWorldSnapshot commissionPreview;
        public RegionalWorldSnapshot CommissionPreview => commissionPreview;

        private void BuildCommissionScreens()
        {
            var mapScreen = CreateScreen(ArchipelagoScreen.CommissionMap, "Read the archipelago",
                "All four towns and sea connections are revealed. These are the exact starting conditions; choosing objectives changes none of them.");
            Text(mapScreen, "commissionMapIdentity", "", "run-copy");
            var mapScroll = new ScrollView { name = "commissionMapScroll" };
            mapScroll.AddToClassList("run-content-scroll");
            mapScreen.Add(mapScroll);
            Panel(mapScroll, "commissionMapHost", "commission-map-host");
            Text(mapScroll, "commissionMapSelection", "Select a town to inspect its capabilities below.", "run-objective");
            Text(mapScroll, "commissionMapDetails", "", "run-copy");
            var routes = new Foldout { name = "commissionRoutes", text = "Exact connections and starting travel times" };
            mapScroll.Add(routes);
            Text(routes, "commissionRouteDetails", "", "run-copy");
            Text(mapScroll, null, "Every town can be inspected before drafting. Industries, inputs and wages stay finite; a legal choice is not a guarantee of victory.", "run-footnote");
            var mapActions = Panel(mapScreen, null, "row run-actions");
            Button(mapActions, "commissionMapBackButton", "Back to setup", () => ShowScreen(ArchipelagoScreen.Setup));
            Button(mapActions, "commissionMapNextButton", "Draft objectives", () => ShowCommissionRound(0), "primary");

            var draft = CreateScreen(ArchipelagoScreen.CommissionDraft, "Draft your commission",
                "Choose one of three objectives in each category. All four selected goals will be mandatory.");
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
                "These four goals are a single commission. They must all hold together with the cash minimum by the deadline.");
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
                "All four goals are mandatory. Permanent achievements stay checked; current conditions can fall again.");
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
            if (setupIdentity == null || SetupChallengeId() != GrandCommission.ChallengeId) return;
            var previewIdentity = new RunIdentity(null, setupIdentity.CharterId, setupIdentity.WorldMode, setupIdentity.Seed,
                setupIdentity.Difficulty, livingPricingMode: setupIdentity.LivingPricingMode);
            commissionPreview = RunScenario.Create(Session.CanonicalContent, previewIdentity).Snapshot();
            SetText("commissionMapIdentity", WorldIdentityText(setupIdentity) + " · " + RunScenario.GetCharter(setupIdentity).Name);
            var host = Element<VisualElement>("commissionMapHost");
            host.Clear();
            var map = new ArchipelagoMap { name = "commissionPreviewMap" };
            host.Add(map);
            map.PortSelected += ShowCommissionPort;
            map.Present(commissionPreview, null, null, false, 1);
            SetText("commissionRouteDetails", string.Join("\n", commissionPreview.Routes.Select(route =>
                commissionPreview.Ports[route.OriginPortId].Name + " ↔ " + commissionPreview.Ports[route.DestinationPortId].Name +
                ": " + route.Distance + " units · " + route.DurationDays(RunScenario.StartingShipSpeed) + " days")));
            ShowCommissionPort(commissionPreview.Ships.Values.First(ship => ship.IsPlayer).DockedPortId);
            ShowScreen(ArchipelagoScreen.CommissionMap);
            Element<ScrollView>("commissionMapScroll").scrollOffset = Vector2.zero;
            Element<Button>("commissionMapNextButton").Focus();
        }

        private void ShowCommissionPort(string portId)
        {
            var port = commissionPreview.Ports[portId];
            SetText("commissionMapSelection", port.Name + " · " + port.Role);
            // Reuse the setup's canonical generation-derived facts, including permitted and absent buildings.
            var card = Element<VisualElement>("setupCity_" + portId);
            SetText("commissionMapDetails", string.Join("\n", card.Query<Label>().ToList().Skip(1).Select(label => label.text)));
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
            if (Session.Identity?.Commission == null) return;
            objectiveResumePlayback = Screen == ArchipelagoScreen.Gameplay && Session.IsPlaying;
            OpenSecondary(ArchipelagoScreen.Objectives);
            Element<Button>("objectivesBackButton").Focus();
        }

        private void CloseObjectives()
        {
            var parent = secondaryParent;
            ShowScreen(parent);
            if (parent == ArchipelagoScreen.Gameplay && objectiveResumePlayback && !Session.IsChallengeEnded) Session.Play();
            objectiveResumePlayback = false;
            Refresh();
            if (parent == ArchipelagoScreen.Gameplay) Element<Button>("objectivesButton").Focus();
        }

        private void RefreshObjectives()
        {
            if (Session.Identity?.Commission == null) return;
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
            parent.Clear();
            foreach (var goal in progress.Goals)
            {
                var card = Panel(parent, "objective_" + goal.Id, "run-card commission-progress-card");
                Text(card, null, (goal.IsMet ? "✓ " : "○ ") + goal.Title, "run-objective");
                Text(card, null, goal.IsPermanent && goal.IsMet ? "PERMANENTLY FINISHED" :
                    goal.IsMet ? "CURRENTLY MET — can change" : "NOT YET MET", "run-eyebrow");
                var bar = new ProgressBar { name = "objectiveBar_" + goal.Id, lowValue = 0, highValue = 100,
                    value = goal.Percent, title = goal.Percent + "%" };
                bar.AddToClassList("commission-progress"); card.Add(bar);
                Text(card, null, goal.Explanation, "run-copy");
                foreach (var condition in goal.Conditions)
                {
                    var permanent = condition.Semantic == CommissionSemantic.Cumulative;
                    var text = Text(card, "objectiveCondition_" + condition.Id,
                        (condition.IsMet ? "✓ " : "○ ") + condition.Label + ": " + condition.CurrentText + " / " + condition.TargetText +
                        "\n" + (permanent ? "Cumulative" : "Current condition") + " · " + condition.Reason, "commission-requirement");
                    text.EnableInClassList("commission-finished", permanent && condition.IsMet);
                }
                Text(card, null, goal.Reason, "run-footnote");
            }
        }
    }
}
