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

        private int ReadInt(string name, string label, bool allowZero)
        {
            var value = ReadLong(name, label, allowZero);
            if (value > int.MaxValue) throw new ArgumentException(label + " must fit a whole inventory quantity.");
            return (int)value;
        }

        private static string WarehouseTradeOutcome(TradeOutcome outcome) =>
            outcome == TradeOutcome.InsufficientTraderInventory ? "Company warehouse stock short" : EconomicText.Outcome(outcome);

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
