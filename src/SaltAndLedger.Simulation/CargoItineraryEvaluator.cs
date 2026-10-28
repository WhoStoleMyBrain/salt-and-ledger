using System;
using System.Globalization;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Pure bounded proposals. Remote draft previews never grant execution permission.</summary>
    public static class CargoItineraryEvaluator
    {
        public static void ValidatePolicy(RegionalWorldSnapshot snapshot, string shipId, CargoItineraryPolicy policy)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var ship = snapshot.Ships[shipId];
            if (snapshot.RunIdentity?.ItineraryVersion != policy.Version || !snapshot.UsesPooledCompanyStorage ||
                snapshot.QuantityScale != EconomicUnits.QuantityScale || !ship.IsPlayer)
                throw new InvalidOperationException("Cargo itineraries require a player ship in a new v3 pooled itinerary run.");
            for (var i = 0; i < policy.Stops.Count; i++)
            {
                var stop = policy.Stops[i];
                var port = snapshot.Ports[stop.PortId];
                snapshot.GetRoute(stop.PortId, policy.Stops[(i + 1) % policy.Stops.Count].PortId);
                foreach (var command in stop.Commands)
                {
                    _ = ship.Cargo[command.GoodId];
                    if (IsMarket(command.Kind)) _ = port.Quotes[command.GoodId];
                }
            }
        }

        public static CargoItineraryDecision Evaluate(RegionalWorldSnapshot snapshot, string shipId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var ship = snapshot.Ships[shipId];
            var policy = ship.Itinerary;
            var progress = ship.ItineraryProgress;
            if (policy == null || progress == null)
                return Status(snapshot, shipId, 0, 0, CargoItineraryDecisionKind.Paused,
                    CargoItineraryBlockReason.Disabled, "No cargo itinerary is applied.");
            ValidatePolicy(snapshot, shipId, policy);
            var stop = policy.Stops[progress.StopIndex];
            if (!ship.AutomationEnabled)
                return Status(snapshot, shipId, progress.StopIndex, progress.ActionIndex, CargoItineraryDecisionKind.Paused,
                    CargoItineraryBlockReason.Disabled, "Manual control is enabled; itinerary progress and cargo are retained.");
            if (ship.DockedPortId == null)
            {
                var previous = policy.Stops[(progress.StopIndex + policy.Stops.Count - 1) % policy.Stops.Count].PortId;
                var matches = ship.DestinationPortId == stop.PortId && ship.OriginPortId == previous && progress.ActionIndex == 0;
                return Status(snapshot, shipId, progress.StopIndex, progress.ActionIndex,
                    matches ? CargoItineraryDecisionKind.ContinueSailing : CargoItineraryDecisionKind.Paused,
                    matches ? CargoItineraryBlockReason.Sailing : CargoItineraryBlockReason.WrongPort,
                    matches ? "Sailing to " + snapshot.Ports[stop.PortId].Name + "; next instruction starts after actual arrival." :
                        "This manual voyage is outside the itinerary's next stop. Pause and return to that stop.");
            }
            if (ship.DockedPortId != stop.PortId)
                return Status(snapshot, shipId, progress.StopIndex, progress.ActionIndex, CargoItineraryDecisionKind.Paused,
                    CargoItineraryBlockReason.WrongPort, "Dock at " + snapshot.Ports[stop.PortId].Name + " to resume this exact instruction.");
            if (progress.OperationsDay == snapshot.Day && progress.OperationsUsed >= CargoItineraryPolicy.MaximumDailyOperations)
                return Status(snapshot, shipId, progress.StopIndex, progress.ActionIndex, CargoItineraryDecisionKind.Wait,
                    CargoItineraryBlockReason.DailyLimit, "Eight itinerary operations used today; advance a day to resume the next instruction.");
            if (progress.ActionIndex == stop.Commands.Count)
                return new CargoItineraryDecision(snapshot, shipId, CargoItineraryDecisionKind.Depart, CargoItineraryBlockReason.None,
                    "This stop's orders are finished; sail to " + snapshot.Ports[policy.Stops[(progress.StopIndex + 1) % policy.Stops.Count].PortId].Name + ".",
                    progress.StopIndex, progress.ActionIndex,
                    departure: new SailCommand(policy.Stops[(progress.StopIndex + 1) % policy.Stops.Count].PortId));
            return PreviewCommand(snapshot, shipId, policy, progress.StopIndex, progress.ActionIndex);
        }

        public static CargoItineraryDecision PreviewCommand(RegionalWorldSnapshot snapshot, string shipId,
            CargoItineraryPolicy policy, int stopIndex, int actionIndex)
        {
            ValidatePolicy(snapshot, shipId, policy);
            if (stopIndex < 0 || stopIndex >= policy.Stops.Count || actionIndex < 0 || actionIndex >= policy.Stops[stopIndex].Commands.Count)
                throw new ArgumentOutOfRangeException(nameof(actionIndex));
            var command = policy.Stops[stopIndex].Commands[actionIndex];
            var ship = snapshot.Ships[shipId];
            var port = snapshot.Ports[policy.Stops[stopIndex].PortId];
            var loading = command.Kind == CargoItineraryCommandKind.LoadFromCompanyTownStock;
            var buying = command.Kind == CargoItineraryCommandKind.BuyFromMarket;
            var acquiring = buying || loading;
            var cargo = ship.Cargo[command.GoodId];
            var free = ship.CargoCapacity - ship.CargoUnits;
            var minimum = command.MinimumQuantity;
            CargoItineraryDecision Block(CargoItineraryBlockReason reason, string text) =>
                new CargoItineraryDecision(snapshot, shipId,
                    command.ShortagePolicy == CargoItineraryShortagePolicy.Skip ? CargoItineraryDecisionKind.Skip : CargoItineraryDecisionKind.Wait,
                    reason, (command.ShortagePolicy == CargoItineraryShortagePolicy.Skip ? "Skip this visit: " : "Waiting at " + port.Name + ": ") + text,
                    stopIndex, actionIndex, command);
            if (acquiring && free < minimum)
                return Block(CargoItineraryBlockReason.CargoFull, "Hold has " + Quantity(free) + " free; free space for " + Quantity(minimum) + " " + command.GoodId + ".");
            if (!acquiring && cargo < minimum)
                return Block(CargoItineraryBlockReason.SourceStock, "Ship carries " + Quantity(cargo) + " " + command.GoodId + "; need at least " + Quantity(minimum) + ".");
            if (!IsMarket(command.Kind))
            {
                if (!snapshot.CompanyStockpiles.TryGetValue(CompanyStockpileSnapshot.StoreId(ship.OwnerId, port.Id), out var store))
                    return Block(CargoItineraryBlockReason.MissingCompanyStock, "Build owned industry here to create company town stock.");
                var source = loading ? Math.Max(0, store.Inventory[command.GoodId] - command.WarehouseReserve) : cargo;
                var space = loading ? free : store.Capacity - store.StoredUnits;
                var quantity = Whole(Math.Min(command.MaximumQuantity, Math.Min(source, space)));
                if (quantity < minimum)
                {
                    if (source < minimum)
                        return Block(command.WarehouseReserve > 0 ? CargoItineraryBlockReason.WarehouseReserve : CargoItineraryBlockReason.SourceStock,
                            "Need " + Quantity(minimum) + " " + command.GoodId + " in company stock after its " + Quantity(command.WarehouseReserve) + " reserve; " + Quantity(source) + " available.");
                    return Block(CargoItineraryBlockReason.WarehouseFull, "Company stock has " + Quantity(space) + " space; sell or use stock before unloading.");
                }
                // A stable owned facility resolves the same town pool through the ordinary transfer path.
                var facility = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == ship.OwnerId && f.PortId == port.Id)
                    .OrderBy(f => f.Id, StringComparer.Ordinal).First();
                return new CargoItineraryDecision(snapshot, shipId, CargoItineraryDecisionKind.Execute, CargoItineraryBlockReason.None,
                    (loading ? "Load " : "Unload ") + Quantity(quantity) + " " + command.GoodId + "; no cash changes. Then advance to the next instruction.",
                    stopIndex, actionIndex, command, transfer: new IndustrialTransferCommand(facility.Id,
                        loading ? TransferDirection.WarehouseToShip : TransferDirection.ShipToWarehouse, command.GoodId, quantity));
            }
            var stock = port.Inventory[command.GoodId];
            var direction = buying ? TradeDirection.Buy : TradeDirection.Sell;
            var quote = port.Quotes[command.GoodId];
            if (buying && stock < minimum)
                return Block(CargoItineraryBlockReason.SourceStock, "Market has " + Quantity(stock) + " " + command.GoodId + "; need at least " + Quantity(minimum) + ".");
            if (!buying && int.MaxValue - stock < minimum)
                return Block(CargoItineraryBlockReason.MarketCapacity, "The market has no room for this minimum cargo order.");
            var budget = buying ? Math.Min(Math.Max(0, ship.Cash - command.CashReserve), command.MaximumExpenditure) : 0;
            var selected = Whole(TradeQuantityCalculator.Maximum(quote, direction, stock, cargo, port.MarketCash, ship.Cash,
                free, command.MaximumQuantity, command.MarginalPriceLimit, buying ? ship.Cash - budget : 0));
            if (selected < minimum)
            {
                if (command.MarginalPriceLimit != 0 && !TradeQuantityCalculator.MeetsPrice(quote, direction, stock, minimum, command.MarginalPriceLimit))
                    return Block(CargoItineraryBlockReason.PriceLimit, "The worst marginal " + (buying ? "ask exceeds the ceiling" : "bid is below the floor") + "; change the draft limit or wait for quotes to change.");
                if (buying && !quote.FitsBudget(direction, stock, minimum, ship.Cash))
                    return Block(CargoItineraryBlockReason.CompanyFunds, "Company cash " + Money(ship.Cash) + " cannot fund " + Quantity(minimum) + " " + command.GoodId + ".");
                if (buying && !quote.FitsBudget(direction, stock, minimum, Math.Max(0, ship.Cash - command.CashReserve)))
                    return Block(CargoItineraryBlockReason.CashReserve, "Preserving " + Money(command.CashReserve) + " company coins prevents this minimum purchase.");
                if (buying && !quote.FitsBudget(direction, stock, minimum, command.MaximumExpenditure))
                    return Block(CargoItineraryBlockReason.ExpenditureLimit, "The per-order spending cap prevents this minimum purchase.");
                if (!buying && !quote.FitsBudget(direction, stock, minimum, port.MarketCash))
                    return Block(CargoItineraryBlockReason.MarketFunds, "Market has " + Money(port.MarketCash) + " coins; can afford " + Quantity(selected) + " " + command.GoodId + " under this order. Retain cargo or skip.");
                return Block(CargoItineraryBlockReason.AccountCapacity, "The receiving cash account has insufficient numeric headroom.");
            }
            var trade = new PortTradeCommand(port.Id, direction, command.GoodId, selected);
            var preview = snapshot.PreviewTrade(shipId, port.Id, trade.Trade);
            return new CargoItineraryDecision(snapshot, shipId, CargoItineraryDecisionKind.Execute, CargoItineraryBlockReason.None,
                (buying ? "Buy " : "Sell ") + Quantity(selected) + " " + command.GoodId + " for exactly " + Money(preview.ExecutedAmount) + " coins at current quotes; then advance.",
                stopIndex, actionIndex, command, trade: trade, preview: preview);
        }

        private static bool IsMarket(CargoItineraryCommandKind kind) => kind == CargoItineraryCommandKind.BuyFromMarket || kind == CargoItineraryCommandKind.SellToMarket;
        private static int Whole(int quantity) => quantity - quantity % EconomicUnits.QuantityScale;
        private static string Quantity(int value) => ((decimal)value / EconomicUnits.QuantityScale).ToString("0.####", CultureInfo.InvariantCulture);
        private static string Money(long value) => ((decimal)value / EconomicUnits.MoneyScale).ToString("0.####", CultureInfo.InvariantCulture);
        private static CargoItineraryDecision Status(RegionalWorldSnapshot snapshot, string shipId, int stop, int action,
            CargoItineraryDecisionKind kind, CargoItineraryBlockReason reason, string text) =>
            new CargoItineraryDecision(snapshot, shipId, kind, reason, text, stop, action);
    }
}
