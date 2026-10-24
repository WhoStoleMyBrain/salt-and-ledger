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
                { "Fixed / Classic Archipelago", "Seeded Archipelago · v1 geography", "Living Cities · v2 economy", "Expanding Economy · v3" }, 0) { name = "worldModePicker" };
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
            var pricing = new DropdownField("Living pricing", new System.Collections.Generic.List<string>
                { "Stock-sensitive baseline", "Coverage target hypothesis" }, 0) { name = "livingPricingPicker" };
            world.Add(pricing);
            Text(world, "setupEconomyDescription", "", "run-footnote");
            Text(world, null, "Seed syntax: optional + or − followed by ASCII digits, from −2147483648 to 2147483647; no spaces.", "run-footnote");
            var preview = Panel(worldRow, null, "run-column run-card");
            Text(preview, null, "GEOGRAPHY PREVIEW", "run-eyebrow");
            Text(preview, "setupWorldIdentity", "", "run-objective");
            Text(preview, "setupCoordinates", "", "run-copy");
            Text(preview, "setupRoutes", "", "run-copy");
            Text(preview, null, "Distances are abstract units. Direct sea connections do not avoid coastlines. Travel estimates use the authoritative starting ship speed.", "run-footnote");
            var cities = Panel(scroll, "setupCities", "run-card hidden");
            Text(cities, null, "KNOWN CITY ECONOMIES", "run-eyebrow");
            Text(cities, "setupRecoveryTown", "", "run-objective");
            Panel(cities, "setupCityCards", "setup-city-cards");
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
                if (setupIdentity == null) return;
                if (SetupChallengeId() == GrandCommission.ChallengeId) OpenCommissionMap();
                else BeginRun(setupIdentity);
            }, "primary");
            mode.RegisterValueChangedCallback(_ => RefreshSetup());
            seed.RegisterValueChangedCallback(_ => RefreshSetup());
            difficulty.RegisterValueChangedCallback(_ => RefreshSetup());
            pricing.RegisterValueChangedCallback(_ => RefreshSetup());
            challenges.RegisterValueChangedCallback(_ => RefreshSetup());
            charters.RegisterValueChangedCallback(_ => RefreshSetup());
        }

        private string SetupChallengeId()
        {
            var index = Math.Max(0, Element<DropdownField>("challengePicker").index);
            return index == 0 ? null : RunScenario.GetChallenges(SetupWorldMode())[index - 1].Id;
        }

        private RunWorldMode SetupWorldMode() => (RunWorldMode)Math.Max(0, Element<DropdownField>("worldModePicker").index);

        private void RefreshSetupChallenges()
        {
            var picker = Element<DropdownField>("challengePicker");
            var choices = new[] { "Unlimited sandbox" }.Concat(RunScenario.GetChallenges(SetupWorldMode()).Select(c => c.Name)).ToList();
            if (picker.choices.SequenceEqual(choices)) return;
            var previous = picker.value;
            picker.choices = choices;
            picker.SetValueWithoutNotify(choices.Contains(previous) ? previous : choices[1]);
        }

        private string SetupCharterId() => RunScenario.Charters[Math.Max(0, Element<DropdownField>("charterPicker").index)].Id;

        private void RefreshSetup()
        {
            RefreshSetupChallenges();
            var id = SetupChallengeId();
            var challenge = id == null ? null : RunScenario.GetChallenge(id);
            var charter = RunScenario.GetCharter(SetupCharterId());
            SetText("setupChallengeDescription", challenge?.Description ?? "Finite markets without a deadline or scored objective.");
            SetText("setupObjective", challenge?.Objective ?? "Explore, invest and automate at your own pace.");
            SetText("setupDeadline", challenge == null ? "Unlimited sandbox." :
                "Deadline: day " + challenge.Configuration.DeadlineDay + " · Minimum cash: " +
                (challenge.Configuration.MinimumCash / (SetupWorldMode() == RunWorldMode.ExpandingEconomy ? 10000 : 1)) +
                "\nCompleting the objective wins immediately. Missing it at the deadline ends the run in defeat.");
            var mode = SetupWorldMode();
            var seeded = mode != RunWorldMode.Classic;
            var expanding = mode == RunWorldMode.ExpandingEconomy;
            var living = mode == RunWorldMode.LivingCities || expanding;
            var difficulty = (RunDifficulty)Math.Max(0, Element<DropdownField>("difficultyPicker").index);
            var pricing = (LivingPricingMode)Math.Max(0, Element<DropdownField>("livingPricingPicker").index);
            Element<DropdownField>("livingPricingPicker").EnableInClassList("hidden", !living);
            Element<VisualElement>("setupCities").EnableInClassList("hidden", !living);
            Element<TextField>("seedField").SetEnabled(seeded);
            var validSeed = SeededArchipelago.TryParseSeed(Element<TextField>("seedField").value, out var seed);
            var valid = !seeded || validSeed;
            var commission = id == GrandCommission.ChallengeId;
            if (commission && valid) EnsureCommissionDraft(seed);
            Element<Button>("startRunButton").text = commission ? "Reveal map & draft" : "Begin voyage";
            Element<Button>("startRunButton").SetEnabled(valid);
            Element<Button>("copySeedButton").SetEnabled(seeded && valid);
            SetText("seedValidation", valid ? "" : "Enter a signed 32-bit integer using ASCII digits. No spaces, decimals or exponents.");
            SetText("setupDifficulty", difficulty == RunDifficulty.Normal
                ? "New private facilities operate immediately; their first batch waits one normal production interval."
                : "Pay the full cost at order. New private sites spend two complete days under construction, then wait a full production interval. No automatic purchases, sales or production while building. The Industrial Consortium's granted bakery is already operational and keeps its original schedule.");
            var plan = commission && valid ? SelectedCommission() : null;
            // An unfinished draft uses an ordinary day-zero sandbox preview; it cannot be started as a commission.
            setupIdentity = valid ? new RunIdentity(commission && plan == null ? null : id, charter.Id, mode,
                seeded ? seed : (int?)null, difficulty, livingPricingMode: living ? pricing : LivingPricingMode.StockOnly,
                commission: plan) : null;
            Root.Q<Button>("commissionBeginButton")?.SetEnabled(commission && valid && plan != null);
            if (valid) charter = RunScenario.GetCharter(setupIdentity);
            SetText("setupCharterDescription", charter.Description);
            SetText("setupAssets", charter.Endowment);
            SetText("setupTradeoff", "Opportunity cost\n" + charter.Tradeoff);
            SetText("setupEconomyDescription", expanding ? "Expanding Economy v3: exact fractional paid needs, seven buildable industries, up to three ships, finite outside trade, public Silver-backed Mint and Company Value. No automatic subsidies. Save explicitly to continue locally." : living
                ? "Living Cities v2 varies geography, production roles, eligible private industries, population and initial finite accounts. Households pay for actual purchases; completed production pays wages. Economic viability is not guaranteed victory."
                : "Historical economy: Classic keeps authored geography; seeded v1 changes geography only. Household finances, wages and population are not modeled.");
            SetText("setupWorldIdentity", valid ? WorldIdentityText(setupIdentity) : "Preview unavailable until the seed is valid.");
            Element<VisualElement>("setupCityCards").Clear();
            if (!valid) { SetText("setupCoordinates", ""); SetText("setupRoutes", ""); SetText("setupRecoveryTown", "Enter a valid seed to preview cities."); return; }
            var geography = RunScenario.PreviewGeography(setupIdentity);
            SetText("setupCoordinates", string.Join("\n", geography.Positions.Values.Select(p =>
                PortName(p.PortId) + "  (" + p.X + ", " + p.Y + ")")));
            SetText("setupRoutes", string.Join("\n", geography.Routes.Select(route =>
                PortName(route.OriginPortId) + " ↔ " + PortName(route.DestinationPortId) + ": " +
                route.Distance + " units · " + route.DurationDays(RunScenario.StartingShipSpeed) + " days")));
            if (living) RefreshSetupCities();
        }

        private void RefreshSetupCities()
        {
            var expanding = setupIdentity.WorldMode == RunWorldMode.ExpandingEconomy;
            var v3 = expanding ? ExpandingEconomyGeneration.Generate(setupIdentity.Seed.Value, setupIdentity.GeneratorVersion, setupIdentity.LivingPricingMode) : null;
            var generation = expanding ? v3.SourceRoles : LivingCityGeneration.Generate(setupIdentity.Seed.Value, setupIdentity.GeneratorVersion, setupIdentity.LivingPricingMode);
            var ports = expanding ? v3.Ports : generation.Ports;
            var blueprints = expanding ? v3.Blueprints : generation.Blueprints;
            var content = expanding ? Session.Content.ForExpandingEconomy() : Session.Content;
            var scale = expanding ? 10000m : 1m;
            string Units(long amount) => (amount / scale).ToString("0.####", CultureInfo.InvariantCulture);
            SetText("setupRecoveryTown", "Vulnerable recovery town: " + PortName(generation.RecoveryPortId) +
                ". Regional paid meals count; a delivery alone does not complete the challenge.");
            foreach (var port in ports)
            {
                var card = Panel(Element<VisualElement>("setupCityCards"), "setupCity_" + port.Id, "setup-city-card");
                var allowed = blueprints.Where(b => b.EligiblePortIds.Contains(port.Id)).ToArray();
                var absent = blueprints.Where(b => !b.EligiblePortIds.Contains(port.Id)).ToArray();
                Text(card, null, port.Name + " · " + port.Role, "run-objective");
                Text(card, null, "Population " + port.Settlement.InitialPopulation + " · prosperity " + port.Settlement.InitialProsperity +
                    " / 100\nHousehold cash " + Units(port.Settlement.InitialHouseholdCash) + " · market cash " + Units(port.Market.InitialMarketCash), "run-copy");
                Text(card, "setupCapabilities_" + port.Id, "Public: " + string.Join(", ", port.Facilities.Select(f => content.GetBuilding(f.Instance.BuildingId).Name)) +
                    "\nCan build: " + string.Join(", ", allowed.Select(b => content.GetBuilding(b.BuildingId).Name)) +
                    "\nUnavailable: " + string.Join(", ", absent.Select(b => content.GetBuilding(b.BuildingId).Name)) +
                    (expanding && port.Id == v3.MintPortId ? "\nPublic Mint and shipwright service harbor" : ""), "run-copy");
                Text(card, null, "Needs: " + string.Join("; ", port.Consumption.Select(c =>
                    Units(LivingCityPricing.DesiredQuantity(c, port.Settlement.InitialPopulation)) + " " + content.GetGood(c.GoodId).Name +
                    " / " + NeedKind(c.Kind) + " every " + c.IntervalDays + " days" + (c.Kind == ConsumptionKind.Luxury ? " if eligible" : ""))) +
                    "\nFurniture still needs sustained eligibility and actual spending power.", "run-footnote");
            }
        }

        private void LoadSetupIdentity(RunIdentity identity)
        {
            RestoreCommissionSelection(identity);
            var mode = Element<DropdownField>("worldModePicker");
            mode.SetValueWithoutNotify(mode.choices[(int)identity.WorldMode]);
            RefreshSetupChallenges();
            Element<DropdownField>("challengePicker").SetValueWithoutNotify(identity.ChallengeId == null ?
                "Unlimited sandbox" : RunScenario.GetChallenge(identity.ChallengeId).Name);
            Element<DropdownField>("charterPicker").SetValueWithoutNotify(RunScenario.GetCharter(identity.CharterId).Name);
            var pricing = Element<DropdownField>("livingPricingPicker");
            pricing.SetValueWithoutNotify(pricing.choices[(int)identity.LivingPricingMode]);
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
            if (mode.index == 0) mode.SetValueWithoutNotify(mode.choices[1]);
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
                (identity.WorldMode == RunWorldMode.ExpandingEconomy ? "Expanding Economy · " : identity.WorldMode == RunWorldMode.LivingCities ? "Living Cities · " : "") +
                "Seed " + identity.Seed.Value.ToString(CultureInfo.InvariantCulture) + " · Generator v" + identity.GeneratorVersion +
                (identity.WorldMode == RunWorldMode.LivingCities || identity.WorldMode == RunWorldMode.ExpandingEconomy ? " · " + identity.LivingPricingMode + " pricing" : "")) +
                " · Ascension " + identity.Ascension + (identity.Difficulty == RunDifficulty.SlowFoundations ? ": Slow Foundations" : ": Normal");
        }
    }
}
