using System;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private RunIdentity setupIdentity;
        public RunIdentity SetupIdentity => setupIdentity;

        private void BuildRunSetup()
        {
            var screen = CreateScreen(ArchipelagoScreen.Setup, "Chart your course",
                "Preview a world, choose your company and challenge, then begin. Nothing runs until you confirm Begin voyage.");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "setupScroll" };
            scroll.AddToClassList("run-content-scroll");
            screen.Add(scroll);
            var worldRow = Panel(scroll, null, "run-columns");
            var world = Panel(worldRow, null, "run-column run-card");
            Text(world, null, "YOUR WORLD", "run-eyebrow");
            var mode = new DropdownField("World type", new System.Collections.Generic.List<string>
                { "Fixed / Classic Archipelago", "Seeded Archipelago" }, 0) { name = "worldModePicker" };
            world.Add(mode);
            var seed = new TextField("Seed") { name = "seedField", value = "42" };
            world.Add(seed);
            var seedActions = Panel(world, null, "row run-actions");
            Button(seedActions, "randomizeSeedButton", "Randomize seed", RandomizeSetupSeed);
            Button(seedActions, "copySeedButton", "Copy seed", () => CopySeed(setupIdentity));
            Text(world, "seedValidation", "", "run-copy error");
            var difficulty = new DropdownField("Difficulty", new System.Collections.Generic.List<string>
                { "Ascension 0: Normal", "Ascension 1: Slow Foundations" }, 0) { name = "difficultyPicker" };
            world.Add(difficulty);
            Text(world, "setupDifficulty", "", "run-copy");
            Text(world, null, "Exactly four towns and the same authored economy. Only geography varies. Seed syntax: optional + or − followed by ASCII digits, from −2147483648 to 2147483647; no spaces.", "run-footnote");
            var preview = Panel(worldRow, null, "run-column run-card");
            Text(preview, null, "GEOGRAPHY PREVIEW", "run-eyebrow");
            Text(preview, "setupWorldIdentity", "", "run-objective");
            Text(preview, "setupCoordinates", "", "run-copy");
            Text(preview, "setupRoutes", "", "run-copy");
            Text(preview, null, "Distances are abstract units. Direct sea connections do not avoid coastlines. Travel estimates use the authoritative starting ship speed.", "run-footnote");
            var columns = Panel(scroll, null, "run-columns");
            var objective = Panel(columns, null, "run-column run-card");
            Text(objective, null, "YOUR OBJECTIVE", "run-eyebrow");
            var challenges = new DropdownField { name = "challengePicker",
                choices = new[] { "Unlimited sandbox" }.Concat(RunScenario.Challenges.Select(c => c.Name)).ToList() };
            objective.Add(challenges);
            challenges.SetValueWithoutNotify(challenges.choices[1]);
            Text(objective, "setupChallengeDescription", "", "run-copy");
            Text(objective, "setupObjective", "", "run-objective");
            Text(objective, "setupDeadline", "", "run-copy");
            var charter = Panel(columns, null, "run-column run-card");
            Text(charter, null, "YOUR COMPANY", "run-eyebrow");
            var charters = new DropdownField { name = "charterPicker", choices = RunScenario.Charters.Select(c => c.Name).ToList() };
            charter.Add(charters);
            charters.SetValueWithoutNotify(charters.choices[0]);
            Text(charter, "setupCharterDescription", "", "run-copy");
            Text(charter, "setupAssets", "", "run-objective");
            Text(charter, "setupTradeoff", "", "run-copy");
            Text(charter, null, "Starter assets are grants, not purchases. Charters replace each other; their advantages never stack.", "run-footnote");
            var actions = Panel(screen, null, "run-actions row");
            Button(actions, "setupBackButton", "Main Menu", () => ShowScreen(ArchipelagoScreen.MainMenu));
            Button(actions, "setupHelpButton", "Encyclopedia", () => OpenSecondary(ArchipelagoScreen.Encyclopedia));
            Button(actions, "startRunButton", "Begin voyage", () =>
            {
                RefreshSetup();
                if (setupIdentity != null) BeginRun(setupIdentity);
            }, "primary");
            mode.RegisterValueChangedCallback(_ => RefreshSetup());
            seed.RegisterValueChangedCallback(_ => RefreshSetup());
            difficulty.RegisterValueChangedCallback(_ => RefreshSetup());
            challenges.RegisterValueChangedCallback(_ => RefreshSetup());
            charters.RegisterValueChangedCallback(_ => RefreshSetup());
        }

        private string SetupChallengeId()
        {
            var index = Math.Max(0, Element<DropdownField>("challengePicker").index);
            return index == 0 ? null : RunScenario.Challenges[index - 1].Id;
        }

        private string SetupCharterId() => RunScenario.Charters[Math.Max(0, Element<DropdownField>("charterPicker").index)].Id;

        private void RefreshSetup()
        {
            var id = SetupChallengeId();
            var challenge = id == null ? null : RunScenario.GetChallenge(id);
            var charter = RunScenario.GetCharter(SetupCharterId());
            SetText("setupChallengeDescription", challenge?.Description ?? "Finite markets without a deadline or scored objective.");
            SetText("setupObjective", challenge?.Objective ?? "Explore, invest and automate at your own pace.");
            SetText("setupDeadline", challenge == null ? "Unlimited sandbox." :
                "Deadline: day " + challenge.Configuration.DeadlineDay + " · Minimum cash: " + challenge.Configuration.MinimumCash +
                "\nCompleting the objective wins immediately. Missing it at the deadline ends the run in defeat.");
            SetText("setupCharterDescription", charter.Description);
            SetText("setupAssets", charter.Endowment);
            SetText("setupTradeoff", "Opportunity cost\n" + charter.Tradeoff);
            var seeded = Element<DropdownField>("worldModePicker").index == 1;
            var difficulty = (RunDifficulty)Math.Max(0, Element<DropdownField>("difficultyPicker").index);
            Element<TextField>("seedField").SetEnabled(seeded);
            var validSeed = SeededArchipelago.TryParseSeed(Element<TextField>("seedField").value, out var seed);
            var valid = !seeded || validSeed;
            Element<Button>("startRunButton").SetEnabled(valid);
            Element<Button>("copySeedButton").SetEnabled(seeded && valid);
            SetText("seedValidation", valid ? "" : "Enter a signed 32-bit integer using ASCII digits. No spaces, decimals or exponents.");
            SetText("setupDifficulty", difficulty == RunDifficulty.Normal
                ? "New private facilities operate immediately; their first batch waits one normal production interval."
                : "Pay the full cost at order. New private sites spend two complete days under construction, then wait a full production interval. No automatic purchases, sales or production while building. The Industrial Consortium's granted bakery is already operational and keeps its original schedule.");
            setupIdentity = valid ? new RunIdentity(id, charter.Id, seeded ? RunWorldMode.Seeded : RunWorldMode.Classic,
                seeded ? seed : (int?)null, difficulty) : null;
            SetText("setupWorldIdentity", valid ? WorldIdentityText(setupIdentity) : "Preview unavailable until the seed is valid.");
            if (!valid) { SetText("setupCoordinates", ""); SetText("setupRoutes", ""); return; }
            var geography = RunScenario.PreviewGeography(setupIdentity);
            SetText("setupCoordinates", string.Join("\n", geography.Positions.Values.Select(p =>
                PortName(p.PortId) + "  (" + p.X + ", " + p.Y + ")")));
            SetText("setupRoutes", string.Join("\n", geography.Routes.Select(route =>
                PortName(route.OriginPortId) + " ↔ " + PortName(route.DestinationPortId) + ": " +
                route.Distance + " units · " + route.DurationDays(RunScenario.StartingShipSpeed) + " days")));
        }

        private void LoadSetupIdentity(RunIdentity identity)
        {
            Element<DropdownField>("challengePicker").SetValueWithoutNotify(identity.ChallengeId == null ?
                "Unlimited sandbox" : RunScenario.GetChallenge(identity.ChallengeId).Name);
            Element<DropdownField>("charterPicker").SetValueWithoutNotify(RunScenario.GetCharter(identity.CharterId).Name);
            var mode = Element<DropdownField>("worldModePicker");
            mode.SetValueWithoutNotify(mode.choices[(int)identity.WorldMode]);
            if (identity.Seed.HasValue) Element<TextField>("seedField").SetValueWithoutNotify(identity.Seed.Value.ToString(CultureInfo.InvariantCulture));
            var difficulty = Element<DropdownField>("difficultyPicker");
            difficulty.SetValueWithoutNotify(difficulty.choices[identity.Ascension]);
            RefreshSetup();
        }

        private void OpenSetup(bool sandbox)
        {
            LeaveActiveRunForSetup();
            var field = Element<DropdownField>("challengePicker");
            if (sandbox || field.index < 1) field.SetValueWithoutNotify(field.choices[sandbox ? 0 : 1]);
            RefreshSetup();
            ShowScreen(ArchipelagoScreen.Setup);
            Element<ScrollView>("setupScroll").scrollOffset = Vector2.zero;
            Element<DropdownField>("worldModePicker").Focus();
        }

        private void LeaveActiveRunForSetup()
        {
            Session.Pause();
            if (HasActiveRun && Session.Snapshot.Challenge != null && !Session.IsChallengeEnded) Session.AbandonRun();
            HasActiveRun = false;
        }

        private void ChangeSetup()
        {
            if (Session.Identity != null) LoadSetupIdentity(Session.Identity);
            OpenSetup(Session.Identity?.ChallengeId == null);
        }

        private void RequestChangeSetup() => Confirm("Change Setup?",
            "Discard this run and return to its current selections. The seed changes only if you edit it or request a reroll.", ChangeSetup);

        private void RequestReroll() => Confirm("Preview a new world?",
            "Discard this run and choose a new seed. Review the new geography before confirming Begin voyage.", RerollWorld);

        private void RerollWorld()
        {
            ChangeSetup();
            RandomizeSetupSeed();
        }

        private void RandomizeSetupSeed()
        {
            // Host-only entropy. The frozen integer is the sole input to the portable generator.
            var next = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
            if (SeededArchipelago.TryParseSeed(Element<TextField>("seedField").value, out var previous) && next == previous)
                next = previous == int.MaxValue ? int.MinValue : previous + 1;
            var mode = Element<DropdownField>("worldModePicker");
            mode.SetValueWithoutNotify(mode.choices[1]);
            Element<TextField>("seedField").SetValueWithoutNotify(next.ToString(CultureInfo.InvariantCulture));
            RefreshSetup();
        }

        private void CopySeed(RunIdentity identity)
        {
            if (identity?.Seed == null) return;
            GUIUtility.systemCopyBuffer = identity.Seed.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string WorldIdentityText(RunIdentity identity)
        {
            if (identity == null) return "Classic Archipelago · Ascension 0";
            return (identity.WorldMode == RunWorldMode.Classic ? "Fixed / Classic Archipelago" :
                "Seed " + identity.Seed.Value.ToString(CultureInfo.InvariantCulture) + " · Generator v" + identity.GeneratorVersion) +
                " · Ascension " + identity.Ascension + (identity.Difficulty == RunDifficulty.SlowFoundations ? ": Slow Foundations" : ": Normal");
        }
    }
}
