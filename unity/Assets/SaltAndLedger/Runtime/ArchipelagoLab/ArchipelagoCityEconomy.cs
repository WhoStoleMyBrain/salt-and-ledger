using System;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private int contextTab;
        private ArchipelagoScreen cityReturnScreen;

        private void BuildCityEconomy()
        {
            var context = Element<ScrollView>("contextScroll");
            var shell = new VisualElement { name = "contextShell" };
            shell.AddToClassList("context-panel");
            shell.AddToClassList("context-shell");
            context.parent.Insert(context.parent.IndexOf(context), shell);
            shell.Add(Element<Label>("selectionTitle"));
            shell.Add(Element<Label>("selectionSubtitle"));
            var tabs = Panel(shell, "contextTabs", "row context-tabs");
            Button(tabs, "contextOverviewTab", "Overview", () => SelectContextTab(0));
            Button(tabs, "contextMarketTab", "Market", () => SelectContextTab(1));
            Button(tabs, "contextControlsTab", "Controls", () => SelectContextTab(2));
            context.RemoveFromClassList("context-panel");
            context.AddToClassList("context-body");
            shell.Add(context);
            var overview = Panel(context, "contextOverview", "context-overview");
            var city = Panel(overview, "cityPrimary", "");
            Text(city, "cityVitalsLabel", "", "city-vitals");
            Text(city, "cityFundsLabel", "", "city-funds");
            Text(city, "cityFoodLabel", "", "city-need");
            Text(city, "cityReasonLabel", "", "compact");
            Text(city, "cityProductionSummary", "", "compact");
            overview.Add(Element<VisualElement>("shipContext"));
            overview.Add(Element<Label>("dockedLabel"));
            overview.Add(Element<VisualElement>("voyageBar"));
            var controls = Panel(context, "contextControls", "hidden");
            foreach (var name in new[] { "automationFoldout", "activityFoldout", "latestEventLabel", "mapHelpFoldout",
                "runIdentityFoldout", "profitLabel", "logisticsButton", "routePlannerButton", "gameHelpButton" })
                controls.Add(Element<VisualElement>(name));
            var actions = Root.Q(className: "context-actions");
            shell.Add(actions);
            Button(shell, "cityEconomyButton", "City economy & capabilities", () => OpenSecondary(ArchipelagoScreen.CityEconomy));

            var screen = CreateScreen(ArchipelagoScreen.CityEconomy, "City economy",
                "Committed town accounts, paid needs and productive opportunities. Inspecting this screen spends no day.");
            Text(screen, "cityEconomyTitle", "", "run-objective");
            var cityTabs = Panel(screen, null, "row city-tabs");
            Button(cityTabs, "cityOverviewTab", "People & money", () => SelectCityTab(0));
            Button(cityTabs, "cityNeedsTab", "Needs & coverage", () => SelectCityTab(1));
            Button(cityTabs, "cityIndustryTab", "Industry & trade", () => SelectCityTab(2));
            var scroll = new ScrollView { name = "cityEconomyScroll" };
            scroll.AddToClassList("run-content-scroll");
            screen.Add(scroll);
            var people = Panel(scroll, "cityPeoplePage", "");
            Text(people, "cityPopulationDetail", "", "run-objective");
            Text(people, "cityProsperityDetail", "", "run-copy");
            var accounts = Panel(people, null, "run-columns");
            var market = Panel(accounts, null, "run-column run-card");
            Text(market, null, "PUBLIC MARKET", "run-eyebrow");
            Text(market, "cityMarketAccount", "", "run-objective");
            Text(market, null, "Buys your cargo, owns public stock and pays public employers' wages. A high need does not create money to buy your shipment.", "run-copy");
            var households = Panel(accounts, null, "run-column run-card");
            Text(households, null, "HOUSEHOLDS", "run-eyebrow");
            Text(households, "cityHouseholdAccount", "", "run-objective");
            Text(households, null, "Earn completed-batch wages and purchase food or eligible luxuries. These funds cannot directly pay a merchant's sale to the market.", "run-copy");
            Text(people, "cityCashFlows", "", "run-copy");
            Text(people, null, "No daily allowance or rescue payment. Household purchases can fund the market only when households have cash and the goods actually exist. Future wages, quotes and recovery days are not guaranteed.", "run-footnote");
            var needs = Panel(scroll, "cityNeedsPage", "hidden");
            Text(needs, "cityNeedsDetail", "", "run-copy");
            Text(needs, "cityCoverageDetail", "", "run-copy");
            Text(needs, "cityPricingDetail", "", "run-footnote");
            var industry = Panel(scroll, "cityIndustryPage", "hidden");
            industry.Add(Element<Label>("economySummaryLabel"));
            Text(industry, "cityCapabilities", "", "run-copy");
            Text(industry, "cityIndustriesDetail", "", "run-copy");
            Text(industry, "cityPrivateDetail", "", "run-copy");
            Text(industry, "cityTradeConstraints", "", "run-copy");
            industry.Add(Element<Foldout>("economyFoldout"));
            industry.Add(Element<Foldout>("pricingFoldout"));
            var footer = Panel(screen, null, "row run-actions");
            Button(footer, "cityEconomyBackButton", "Back", () => ShowScreen(cityReturnScreen));
            Button(footer, "cityEconomyHelpButton", "City economy help", OpenCityEconomyHelp);
            SelectCityTab(0);
            SelectContextTab(0);
        }

        private void OpenCityEconomyHelp()
        {
            var category = Element<DropdownField>("encyclopediaCategory");
            category.SetValueWithoutNotify(category.choices[0]);
            Element<TextField>("encyclopediaSearch").SetValueWithoutNotify("");
            OpenSecondary(ArchipelagoScreen.Encyclopedia);
            if (encyclopedia != null) SelectReference("help_city_accounts", false);
        }

        private void SelectContextTab(int tab)
        {
            contextTab = tab;
            RefreshContextLayout();
            Element<ScrollView>("contextScroll").scrollOffset = Vector2.zero;
        }

        private void RefreshContextLayout()
        {
            if (Root.Q("contextOverview") == null) return;
            Element<VisualElement>("contextOverview").EnableInClassList("hidden", contextTab != 0);
            Element<VisualElement>("portContext").EnableInClassList("hidden", contextTab != 1);
            Element<VisualElement>("contextControls").EnableInClassList("hidden", contextTab != 2);
            Element<VisualElement>("cityPrimary").EnableInClassList("hidden", Session.SelectedShipId != null);
            for (var index = 0; index < 3; index++)
                Element<Button>(new[] { "contextOverviewTab", "contextMarketTab", "contextControlsTab" }[index])
                    .EnableInClassList("selected-tab", index == contextTab);
            Element<Button>("cityEconomyButton").text = PortName(Session.SelectedPortId) + " · city economy";
        }

        private void SelectCityTab(int tab)
        {
            for (var index = 0; index < 3; index++)
            {
                Element<VisualElement>(new[] { "cityPeoplePage", "cityNeedsPage", "cityIndustryPage" }[index])
                    .EnableInClassList("hidden", tab != index);
                Element<Button>(new[] { "cityOverviewTab", "cityNeedsTab", "cityIndustryTab" }[index])
                    .EnableInClassList("selected-tab", tab == index);
            }
            Element<ScrollView>("cityEconomyScroll").scrollOffset = Vector2.zero;
        }

        private void RefreshCityEconomy()
        {
            if (Root.Q("cityEconomyTitle") == null) return;
            var snapshot = Session.Snapshot;
            var port = snapshot.Ports[Session.SelectedPortId];
            var city = port.City;
            var eligible = snapshot.IndustryBlueprints.Values.Where(b => b.EligiblePortIds.Contains(port.Id))
                .OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
            var missing = snapshot.IndustryBlueprints.Values.Where(b => !b.EligiblePortIds.Contains(port.Id))
                .OrderBy(b => b.Id, StringComparer.Ordinal).ToArray();
            var producers = port.Facilities.Select(f => Session.Content.GetBuilding(f.Configuration.Instance.BuildingId).Name).ToArray();
            var privateSites = snapshot.PrivateFacilities.Values.Where(f => f.PortId == port.Id).ToArray();
            var publicGoods = port.Facilities.SelectMany(f => Session.Content.GetRecipe(f.Configuration.Instance.RecipeId).Outputs)
                .Select(q => q.GoodId).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
            SetText("cityEconomyTitle", port.Name + " · day " + snapshot.Day + " · " + port.Role);
            SetText("cityProductionSummary", "Public: " + (producers.Length == 0 ? "none" : string.Join(", ", producers)));
            SetText("cityCapabilities", "PUBLIC PRODUCTION NOW\n" + (producers.Length == 0 ? "None" : string.Join(", ", producers)) +
                "\nProduces: " + (publicGoods.Length == 0 ? "nothing locally" : string.Join(", ", publicGoods.Select(id => Session.Content.GetGood(id).Name))) +
                "\n\nPRIVATE CONSTRUCTION ELIGIBLE\n" + (eligible.Length == 0 ? "None" : string.Join(", ", eligible.Select(b => Session.Content.GetBuilding(b.BuildingId).Name))) +
                "\n\nNOT ELIGIBLE HERE\n" + (missing.Length == 0 ? "All configured blueprints are available." : string.Join(", ", missing.Select(b => Session.Content.GetBuilding(b.BuildingId).Name))) +
                "\nEligibility is enforced by the simulation. Missing capability never prevents ordinary trade or supplies free inputs.");
            SetText("cityIndustriesDetail", "PUBLIC EMPLOYERS\n" + (port.Facilities.Count == 0 ? "None" : string.Join("\n\n", port.Facilities.Select(f =>
                Session.Content.GetBuilding(f.Configuration.Instance.BuildingId).Name + " · wage " + Money(f.Configuration.WagePerBatch) +
                " per completed batch; last attempt: " + ArchipelagoLabSession.FacilityStatus(f.LastOutcome) +
                "\nNext opportunity day " + f.NextDueDay + ". " + PublicInputStatus(port, f)))));
            SetText("cityPrivateDetail", "PRIVATE EMPLOYERS\n" + (privateSites.Length == 0 ? "None" : string.Join("\n\n", privateSites.Select(f =>
                Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " · owner " + f.OwnerId +
                " · wage " + Money(f.Blueprint.WagePerBatch) + " per completed batch · paid " + Money(f.WagesPaid) +
                "\n" + IndustryStatus(f)))));
            SetText("cityTradeConstraints", "IMPORT DEPENDENCIES\n" + string.Join("\n", goodIds.Where(id => !publicGoods.Contains(id)).Select(id =>
                Session.Content.GetGood(id).Name + ": public stock " + Q(port.Inventory[id]) + "; no existing public producer.")) +
                "\n\nMarket cash " + Money(port.MarketCash) + ". All player and NPC sales need this account to fund their exact bid total. " +
                "Buying locally transfers actual company cash into this market. Shipping requires the real dock, cargo and voyage.");
            if (city == null)
            {
                SetText("cityVitalsLabel", "Historical economy");
                SetText("cityFundsLabel", "Market cash " + Money(port.MarketCash));
                SetText("cityFoodLabel", "Population and household funds are not modeled.");
                SetText("cityReasonLabel", "Classic and seeded v1 preserve their original physical demand and unpaid production.");
                SetText("cityPopulationDetail", "Historical fixture · no population account");
                SetText("cityProsperityDetail", "Prosperity and household welfare are not modeled in this run.");
                SetText("cityMarketAccount", Money(port.MarketCash) + " cash");
                SetText("cityHouseholdAccount", "Not modeled");
                SetText("cityCashFlows", "This legacy mode preserves its original finite company/market money model. Household payments and production wages are disabled.");
                SetText("cityNeedsDetail", Element<Label>("consumptionLabel").text);
                SetText("cityCoverageDetail", "Population-based coverage is not applicable to this historical fixture.");
                SetText("cityPricingDetail", "Historical authored stock-sensitive quotes are unchanged.");
                return;
            }
            var food = port.Consumption.FirstOrDefault(rule => rule.Configuration.Kind == ConsumptionKind.Essential);
            var direction = city.PopulationDelta > 0 ? "growing" : city.PopulationDelta < 0 ? "declining" : "stable";
            SetText("cityVitalsLabel", "Population " + city.Population + " · " + direction + "\nProsperity " + city.Prosperity + " / 100");
            SetText("cityFundsLabel", "Market cash  " + Money(port.MarketCash) + "\nHousehold cash  " + Money(city.HouseholdCash));
            SetText("cityFoodLabel", food == null ? "No essential need configured." :
                "Last food: " + Q(food.LastConsumedQuantity) + "/" + Q(food.LastDesiredQuantity) + " paid\nUnmet: stock " + Q(food.LastUnmetStock) + " · funds " + Q(food.LastUnmetAffordability));
            SetText("cityReasonLabel", city.ProsperityReason);
            SetText("cityPopulationDetail", "Population " + city.Population + " · " + direction +
                "\nStarted with " + city.InitialPopulation + "; latest review change " + Signed(city.PopulationDelta) + " on day " + city.LastDemographicDay + ".");
            SetText("cityProsperityDetail", "Prosperity " + city.Prosperity + " / 100\n" + city.ProsperityReason +
                "\nFood window: " + city.EssentialFulfillmentPercent + "% fulfilled over " + city.FoodWindow.Count + " observations. " +
                (city.FoodWindow.Count == 0 ? "Purchasing power: awaiting first food observation." :
                    "Purchasing power at last food observation: " + city.PurchasingPowerPercent + "% of the food reserve benchmark.") +
                "\nFurniture demand " + (city.LuxuryEnabled ? "eligible" : "suspended") + ". Sustained conditions determine eligibility; one good day is not a population boom.");
            SetText("cityMarketAccount", Money(port.MarketCash) + " cash");
            SetText("cityHouseholdAccount", Money(city.HouseholdCash) + " cash");
            SetText("cityCashFlows", "ACTUAL CASH TRANSFERS\nWages received: " + Money(city.LastWagesReceived) + " latest day · " + Money(city.TotalWagesReceived) +
                " total\nRetail paid to market: " + Money(city.LastRetailSpend) + " latest day · " + Money(city.TotalRetailSpend) +
                " total\nPrivate employers pay from their company account; public employers pay from this market. Each successful retail purchase moves household cash into this market and consumes real goods.");
            SetText("cityNeedsDetail", string.Join("\n\n", port.Consumption.Select(rule =>
                NeedKind(rule.Configuration.Kind) + " · " + Session.Content.GetGood(rule.Configuration.GoodId).Name +
                "\nCurrent scheduled need " + Q(rule.Configuration.PreviewDemand(city.Population, rule.DemandRemainder).Quantity) +
                (rule.Configuration.Kind == ConsumptionKind.Luxury && !city.LuxuryEnabled ? " nominal units; currently suspended" : " units per opportunity") +
                "\nLast due: desired " + Q(rule.LastDesiredQuantity) + " · consumed " + Q(rule.LastConsumedQuantity) + " · paid " + Money(rule.LastPayment) +
                "\nUnmet: missing stock " + Q(rule.LastUnmetStock) + " · unaffordable stocked units " + Q(rule.LastUnmetAffordability) +
                "\nNext due day " + rule.NextDueDay + " · interval " + rule.Configuration.IntervalDays + " days")));
            SetText("cityCoverageDetail", "HOUSEHOLD DAYS OF SUPPLY\n" + string.Join("\n", goodIds.Select(id =>
            {
                var coverage = LivingCityPricing.HouseholdCoverageDays(port.Configuration, city, id, port.Inventory[id]);
                return Session.Content.GetGood(id).Name + ": " + (coverage.HasValue ? coverage.Value + " whole days at current scheduled household need" : "not applicable; no currently active household demand");
            })) + "\nCoverage describes current stock, not what households can afford, a promised delivery or a survival forecast. Industrial inputs and municipal use are shown separately.");
            SetText("cityPricingDetail", "Pricing experiment: " + port.Configuration.Settlement.PricingMode +
                ". Quotes, supply and actual purchasing power are separate. Prosperity is not a global price multiplier.");
        }

        private string PublicInputStatus(RegionalPortSnapshot port, ScheduledFacilitySnapshot facility)
        {
            var recipe = Session.Content.GetRecipe(facility.Configuration.Instance.RecipeId);
            if (recipe.Inputs.Count == 0) return "Natural extraction; no traded inputs.";
            return string.Join("; ", recipe.Inputs.Select(q => Session.Content.GetGood(q.GoodId).Name + " " + Q(port.Inventory[q.GoodId]) + "/" + Q(checked(q.Quantity * Session.Snapshot.QuantityScale)) +
                (port.Inventory[q.GoodId] < checked(q.Quantity * Session.Snapshot.QuantityScale) ? " missing input" : " in stock")));
        }

        private static string NeedKind(ConsumptionKind kind)
        {
            switch (kind)
            {
                case ConsumptionKind.Essential: return "Essential household purchase";
                case ConsumptionKind.Luxury: return "Durable household purchase";
                case ConsumptionKind.Municipal: return "Municipal own-use; no self-payment";
                default: return "Historical physical consumption";
            }
        }
    }
}
