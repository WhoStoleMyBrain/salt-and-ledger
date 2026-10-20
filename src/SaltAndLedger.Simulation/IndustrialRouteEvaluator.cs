using System;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Pure, bounded selection over real markets and owned stores; no inventories or clocks are owned here.</summary>
    public static class IndustrialRouteEvaluator
    {
        public static IndustrialRouteDecision Evaluate(RegionalWorldSnapshot snapshot, string shipId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var ship = snapshot.Ships[shipId];
            var stage = ship.IndustrialStage;
            if (!ship.AutomationEnabled || ship.IndustrialPolicy == null)
                return Decision(snapshot, ship, IndustrialRouteAction.Pause, "Industrial automation is paused.");
            var policy = ship.IndustrialPolicy;
            ValidatePolicy(snapshot, shipId, policy);
            var outbound = stage == IndustrialRouteStage.AcquireOutbound || stage == IndustrialRouteStage.DeliverOutbound;
            var acquiring = stage == IndustrialRouteStage.AcquireOutbound || stage == IndustrialRouteStage.AcquireReturn;
            var source = outbound ? policy.OriginPortId : policy.DestinationPortId;
            var destination = outbound ? policy.DestinationPortId : policy.OriginPortId;
            if (ship.DockedPortId == null)
            {
                if (acquiring || ship.OriginPortId != source || ship.DestinationPortId != destination)
                    return Decision(snapshot, ship, IndustrialRouteAction.Pause,
                        "The voyage does not match this route stage; finish the voyage and reconfigure with an empty hold.");
                return Decision(snapshot, ship, IndustrialRouteAction.ContinueSailing,
                    "Sailing to the configured delivery port; cargo and stage are retained.");
            }
            if (ship.DockedPortId != (acquiring ? source : destination))
                return Decision(snapshot, ship, IndustrialRouteAction.Pause,
                    "The ship is not docked at the port required by this stage; pause and resolve its location.");
            var leg = outbound ? policy.Outbound : policy.ReturnLeg;
            if (ship.Cargo.Any(p => p.Value > 0 && (leg == null || p.Key != leg.GoodId)))
                return Decision(snapshot, ship, IndustrialRouteAction.Pause,
                    "Cargo is incompatible with the active route leg; pause and handle it manually. Nothing is discarded.");
            if (leg == null)
            {
                if (acquiring) return EmptyReturn(snapshot, ship, policy, "The route has no return shipment.");
                return Decision(snapshot, ship, IndustrialRouteAction.AdvanceStage,
                    "The empty return is complete.", IndustrialRouteStage.AcquireOutbound);
            }
            return acquiring ? Acquire(snapshot, ship, policy, leg, source, destination, outbound)
                : Deliver(snapshot, ship, leg, destination, outbound);
        }

        public static void ValidatePolicy(RegionalWorldSnapshot snapshot, string shipId, IndustrialRoutePolicy policy)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            var ship = snapshot.Ships[shipId];
            snapshot.GetRoute(policy.OriginPortId, policy.DestinationPortId);
            ValidateLeg(snapshot, ship, policy.Outbound, policy.OriginPortId, policy.DestinationPortId);
            if (policy.ReturnLeg != null)
                ValidateLeg(snapshot, ship, policy.ReturnLeg, policy.DestinationPortId, policy.OriginPortId);
        }

        private static void ValidateLeg(RegionalWorldSnapshot snapshot, RegionalShipSnapshot ship,
            IndustrialRouteLeg leg, string source, string destination)
        {
            // Cargo contains every canonical good. A private transfer needs no public market quote.
            _ = ship.Cargo[leg.GoodId];
            if (leg.SourceKind == IndustrialEndpointKind.Market) _ = snapshot.Ports[source].Quotes[leg.GoodId];
            if (leg.DestinationKind == IndustrialEndpointKind.Market) _ = snapshot.Ports[destination].Quotes[leg.GoodId];
            if (leg.SourceKind == IndustrialEndpointKind.Warehouse)
                ValidateFacility(snapshot, ship, leg.SourceFacilityId!, source);
            if (leg.DestinationKind == IndustrialEndpointKind.Warehouse)
                ValidateFacility(snapshot, ship, leg.DestinationFacilityId!, destination);
        }

        private static void ValidateFacility(RegionalWorldSnapshot snapshot, RegionalShipSnapshot ship,
            string facilityId, string portId)
        {
            var facility = snapshot.PrivateFacilities[facilityId];
            if (facility.OwnerId != ship.OwnerId)
                throw new InvalidOperationException("Industrial route warehouses must belong to the ship's company.");
            if (facility.PortId != portId)
                throw new InvalidOperationException("An industrial route warehouse must be at its configured endpoint port.");
        }

        private static IndustrialRouteDecision Acquire(RegionalWorldSnapshot snapshot, RegionalShipSnapshot ship,
            IndustrialRoutePolicy policy, IndustrialRouteLeg leg, string source, string destination, bool outbound)
        {
            var cargo = ship.Cargo[leg.GoodId];
            var next = outbound ? IndustrialRouteStage.DeliverOutbound : IndustrialRouteStage.DeliverReturn;
            if (leg.MinimumShipmentQuantity > ship.CargoCapacity)
                return Decision(snapshot, ship, IndustrialRouteAction.Pause, "The minimum shipment exceeds ship capacity.");
            if (cargo >= leg.MinimumShipmentQuantity)
                return Decision(snapshot, ship, IndustrialRouteAction.Depart, "Carried cargo meets the departure minimum.",
                    next, departure: new SailCommand(destination));
            var market = snapshot.Ports[source];
            var sourceStock = leg.SourceKind == IndustrialEndpointKind.Warehouse
                ? snapshot.PrivateFacilities[leg.SourceFacilityId!].Inventory[leg.GoodId] : market.Inventory[leg.GoodId];
            if (sourceStock < leg.MinimumSourceStock)
                return AcquisitionWait(snapshot, ship, policy, outbound,
                    "Waiting for source stock to reach " + leg.MinimumSourceStock + " units before loading.");
            var maximum = Math.Min(leg.MaximumShipmentQuantity - cargo, ship.CargoCapacity - ship.CargoUnits);
            int quantity;
            if (leg.SourceKind == IndustrialEndpointKind.Warehouse)
            {
                quantity = Math.Min(maximum, Math.Max(0, sourceStock - leg.WarehouseReserve));
                if ((long)cargo + quantity < leg.MinimumShipmentQuantity)
                    return AcquisitionWait(snapshot, ship, policy, outbound,
                        "Available private stock after its " + leg.WarehouseReserve + "-unit reserve cannot fill the minimum shipment.");
                return Decision(snapshot, ship, IndustrialRouteAction.LoadAndDepart,
                    "Load owned stock while retaining its reserve, then depart.", next,
                    transfer: new IndustrialTransferCommand(leg.SourceFacilityId!, TransferDirection.WarehouseToShip,
                        leg.GoodId, quantity), departure: new SailCommand(destination));
            }
            var affordableBudget = Math.Min(Math.Max(0, ship.Cash - policy.CashReserve), leg.MaximumPurchaseExpenditure);
            var effectiveReserve = ship.Cash - affordableBudget;
            quantity = TradeQuantityCalculator.Maximum(market.Quotes[leg.GoodId], TradeDirection.Buy,
                sourceStock, cargo, market.MarketCash, ship.Cash, ship.CargoCapacity - ship.CargoUnits,
                maximum, leg.MaximumBuyPrice, effectiveReserve);
            if ((long)cargo + quantity < leg.MinimumShipmentQuantity)
                return AcquisitionWait(snapshot, ship, policy, outbound,
                    PurchaseWaitReason(market, ship, policy, leg, leg.MinimumShipmentQuantity - cargo));
            var command = new TradeCommand(TradeDirection.Buy, leg.GoodId, quantity);
            var preview = snapshot.PreviewTrade(ship.Id, source, command);
            return Decision(snapshot, ship, IndustrialRouteAction.BuyAndDepart,
                "Buy a qualifying shipment using exact marginal prices, then depart.", next,
                trade: new PortTradeCommand(source, command.Direction, command.GoodId, command.Quantity),
                departure: new SailCommand(destination), preview: preview);
        }

        private static IndustrialRouteDecision Deliver(RegionalWorldSnapshot snapshot, RegionalShipSnapshot ship,
            IndustrialRouteLeg leg, string destination, bool outbound)
        {
            var cargo = ship.Cargo[leg.GoodId];
            var next = outbound ? IndustrialRouteStage.AcquireReturn : IndustrialRouteStage.AcquireOutbound;
            if (cargo == 0)
                return Decision(snapshot, ship, IndustrialRouteAction.AdvanceStage, "Delivery is complete; proceed to the next leg.", next);
            if (leg.DestinationKind == IndustrialEndpointKind.Warehouse)
            {
                var facility = snapshot.PrivateFacilities[leg.DestinationFacilityId!];
                var quantity = Math.Min(cargo, facility.Capacity - facility.StoredUnits);
                if (quantity == 0)
                    return Decision(snapshot, ship, IndustrialRouteAction.Wait,
                        "The destination warehouse is full; unsent cargo remains aboard.");
                return Decision(snapshot, ship, IndustrialRouteAction.Unload,
                    quantity == cargo ? "Unload owned cargo into its destination warehouse." :
                        "Unload only available warehouse capacity; remaining cargo stays aboard.",
                    quantity == cargo ? next : ship.IndustrialStage,
                    transfer: new IndustrialTransferCommand(facility.Id, TransferDirection.ShipToWarehouse,
                        leg.GoodId, quantity));
            }
            var market = snapshot.Ports[destination];
            var sellQuantity = TradeQuantityCalculator.Maximum(market.Quotes[leg.GoodId], TradeDirection.Sell,
                market.Inventory[leg.GoodId], cargo, market.MarketCash, ship.Cash,
                ship.CargoCapacity - ship.CargoUnits, cargo, leg.MinimumSellPrice);
            if (sellQuantity == 0)
                return Decision(snapshot, ship, IndustrialRouteAction.Wait, SaleWaitReason(market, ship, leg));
            var command = new TradeCommand(TradeDirection.Sell, leg.GoodId, sellQuantity);
            var preview = snapshot.PreviewTrade(ship.Id, destination, command);
            return Decision(snapshot, ship, IndustrialRouteAction.Sell,
                sellQuantity == cargo ? "Sell the complete shipment at qualifying exact market bids." :
                    "Sell the eligible portion; blocked cargo remains aboard before any return loading.",
                sellQuantity == cargo ? next : ship.IndustrialStage,
                trade: new PortTradeCommand(destination, command.Direction, command.GoodId, command.Quantity), preview: preview);
        }

        private static string PurchaseWaitReason(RegionalPortSnapshot port, RegionalShipSnapshot ship,
            IndustrialRoutePolicy policy, IndustrialRouteLeg leg, int minimum)
        {
            var stock = port.Inventory[leg.GoodId];
            var quote = port.Quotes[leg.GoodId];
            if (stock < minimum) return "Source market stock cannot fill the minimum shipment.";
            if (!TradeQuantityCalculator.MeetsPrice(quote, TradeDirection.Buy, stock, minimum, leg.MaximumBuyPrice))
                return "The worst marginal purchase price exceeds the buying limit before the minimum shipment.";
            if (!quote.FitsBudget(TradeDirection.Buy, stock, minimum, ship.Cash))
                return "Company cash cannot pay for the minimum shipment.";
            if (!quote.FitsBudget(TradeDirection.Buy, stock, minimum, Math.Max(0, ship.Cash - policy.CashReserve)))
                return "The company cash reserve prevents the minimum shipment.";
            if (!quote.FitsBudget(TradeDirection.Buy, stock, minimum, leg.MaximumPurchaseExpenditure))
                return "The purchase expenditure cap prevents the minimum shipment.";
            return "The receiving market cash account has insufficient numeric headroom for the minimum shipment.";
        }

        private static string SaleWaitReason(RegionalPortSnapshot port, RegionalShipSnapshot ship, IndustrialRouteLeg leg)
        {
            var stock = port.Inventory[leg.GoodId];
            var quote = port.Quotes[leg.GoodId];
            if (stock == int.MaxValue) return "The receiving market has no inventory space; cargo is retained.";
            if (!TradeQuantityCalculator.MeetsPrice(quote, TradeDirection.Sell, stock, 1, leg.MinimumSellPrice))
                return "Current marginal bids are below the selling floor; cargo is retained.";
            if (!quote.FitsBudget(TradeDirection.Sell, stock, 1, port.MarketCash))
                return "The destination market cannot pay for another unit; cargo is retained.";
            return "The company cash account has insufficient numeric headroom; cargo is retained.";
        }

        private static IndustrialRouteDecision AcquisitionWait(RegionalWorldSnapshot snapshot, RegionalShipSnapshot ship,
            IndustrialRoutePolicy policy, bool outbound, string reason)
        {
            if (!outbound && policy.AllowEmptyReturn && ship.CargoUnits == 0)
                return EmptyReturn(snapshot, ship, policy, reason);
            return Decision(snapshot, ship, IndustrialRouteAction.Wait, reason);
        }

        private static IndustrialRouteDecision EmptyReturn(RegionalWorldSnapshot snapshot, RegionalShipSnapshot ship,
            IndustrialRoutePolicy policy, string reason) =>
            Decision(snapshot, ship, IndustrialRouteAction.Depart, reason + " The policy permits an empty return.",
                IndustrialRouteStage.DeliverReturn, departure: new SailCommand(policy.OriginPortId));

        private static IndustrialRouteDecision Decision(RegionalWorldSnapshot snapshot, RegionalShipSnapshot ship,
            IndustrialRouteAction action, string reason, IndustrialRouteStage? next = null,
            PortTradeCommand? trade = null, IndustrialTransferCommand? transfer = null,
            SailCommand? departure = null, TradeAttempt? preview = null) =>
            new IndustrialRouteDecision(snapshot, ship.Id, ship.IndustrialStage, next ?? ship.IndustrialStage,
                action, reason, trade, transfer, departure, preview);
    }
}
