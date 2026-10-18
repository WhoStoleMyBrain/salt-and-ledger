using System;
using System.Globalization;
using System.Linq;
using System.Text;
using SaltAndLedger.Simulation;

internal static class ShippingDemo
{
    private const string North = ShippingScenario.North;
    private const string South = ShippingScenario.South;
    private const int Capacity = ShippingScenario.Capacity;

    internal static int Run(ContentCatalog content)
    {
        Console.WriteLine("Manual shipping: handcrafted ports, linear prices, capacity 6, two-tick travel, no operating costs.");
        foreach (var quantity in new[] { 2, Capacity })
        {
            var transcript = Simulate(content, quantity, false);
            Require(transcript == Simulate(content, quantity, false), "Shipping repeat differed.");
            Require(transcript == Simulate(content, quantity, true), "Reversed port setup differed.");
            Console.Write(transcript);
        }
        Console.WriteLine("Verified repeated and reversed-port transcripts; both decisions finish at tick 6 with empty cargo.");
        Console.WriteLine("Comparison: logs 2 => purchase 16, sale 56, log profit 40; logs 6 => purchase 54, sale 144, log profit 90.");
        Console.WriteLine("Each return carries 2 planks: purchase 35, sale 64, plank profit 29. Total realized profit: 69 versus 119.");
        Console.WriteLine("The larger order earns more here but has lower log profit per unit and leaves a different market state; this is not an optimal-strategy claim.");
        return 0;
    }

    private static string Simulate(ContentCatalog content, int logs, bool reversePorts)
    {
        var world = ShippingScenario.Create(content, reversePorts);
        var output = new StringBuilder();
        output.AppendLine(FormattableString.Invariant($"Decision: buy {logs} logs, return with 2 planks."));
        AppendState(output, world.Snapshot());
        var beforeBuy = world.Snapshot();
        var preview = beforeBuy.PreviewTrade(North, new TradeCommand(TradeDirection.Buy, "logs", logs));
        Require(ReferenceEquals(beforeBuy, world.Snapshot()), "Preview mutated state.");
        AppendTrade(output, North, preview, "Preview");

        // Capacity rejection is ordinary: the following feasible purchase and departure still run.
        var outward = world.Step(trades: new[]
        {
            Trade(North, TradeDirection.Buy, "logs", Capacity + 1),
            Trade(North, TradeDirection.Buy, "logs", logs)
        }, sailCommands: new[] { new SailCommand(South) });
        Require(outward.TradeAttempts[0].Trade.Outcome == TradeOutcome.InsufficientCargoCapacity,
            "Oversized purchase must reject for cargo capacity.");
        var logBuy = outward.TradeAttempts[1].Trade;
        RequireTraded(logBuy, logs);
        Require(logBuy.TotalAmount == preview.TotalAmount, "Preview and execution differ.");
        Require(outward.After.Ship.RemainingTravelTicks == 1 && outward.After.Ship.DockedPortId == null,
            "Departure must consume exactly one travel tick.");
        AppendStep(output, outward);

        // An illegal in-transit command is exceptional and does not consume a tick.
        var inTransit = world.Snapshot();
        var rejected = false;
        try
        {
            world.Step(trades: new[] { Trade(South, TradeDirection.Sell, "logs", logs) });
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected && ReferenceEquals(inTransit, world.Snapshot()), "Transit trade must fail without advancing.");
        output.AppendLine("Tick 1 illegal trade: InvalidOperationException; complete snapshot unchanged, no tick consumed.");
        var arrival = world.Step();
        Require(arrival.After.Ship.DockedPortId == South && arrival.After.Ship.Cargo["logs"] == logs,
            "Travel must arrive with cargo unchanged.");
        AppendStep(output, arrival);

        var delivery = world.Step(trades: new[] { Trade(South, TradeDirection.Sell, "logs", logs) });
        var logSale = delivery.TradeAttempts[0].Trade;
        RequireTraded(logSale, logs);
        Require(delivery.ProductionAttempts.Count == 1 &&
            delivery.ProductionAttempts[0].Production.Outcome == ProductionOutcome.Produced,
            "Delivered logs must feed Southhaven production in the same tick.");
        Require(delivery.After.Ports[South].Inventory["planks"] == 2,
            "The next tick must expose the plank made from the delivery.");
        AppendStep(output, delivery);

        var homeward = world.Step(trades: new[] { Trade(South, TradeDirection.Buy, "planks", 2) },
            sailCommands: new[] { new SailCommand(North) });
        var plankBuy = homeward.TradeAttempts[0].Trade;
        RequireTraded(plankBuy, 2);
        AppendStep(output, homeward);
        AppendStep(output, world.Step());
        var homeSale = world.Step(trades: new[] { Trade(North, TradeDirection.Sell, "planks", 2) });
        var plankSale = homeSale.TradeAttempts[0].Trade;
        RequireTraded(plankSale, 2);
        AppendStep(output, homeSale);

        var purchaseCost = checked(logBuy.ExecutedAmount + plankBuy.ExecutedAmount);
        var saleRevenue = checked(logSale.ExecutedAmount + plankSale.ExecutedAmount);
        var profit = checked(saleRevenue - purchaseCost);
        var final = world.Snapshot();
        var full = logs == Capacity;
        Require(logBuy.TotalAmount == (full ? 54 : 16) && logSale.TotalAmount == (full ? 144 : 56) &&
            plankBuy.TotalAmount == 35 && plankSale.TotalAmount == 64, "Measured trade totals changed.");
        Require(final.Tick == 6 && final.Ship.DockedPortId == North && final.Ship.CargoUnits == 0 &&
            final.TraderCash == (full ? 219 : 169) && profit == (full ? 119 : 69),
            "Final trader state differs from the documented comparison.");
        Require(final.Ports[North].Inventory["logs"] == 18 - logs && final.Ports[North].Inventory["planks"] == 2 &&
            final.Ports[North].MarketCash == (full ? 990 : 952) && final.Ports[South].Inventory["logs"] == 0 &&
            final.Ports[South].Inventory["planks"] == (full ? 2 : 0) &&
            final.Ports[South].MarketCash == (full ? 891 : 979), "Final market state changed.");
        Require(final.TraderCash - 100 == profit, "Empty-cargo realized profit must equal the trader cash change.");
        output.AppendLine(FormattableString.Invariant(
            $"Result: purchased logs={logs}, planks=2; cost={purchaseCost}; sold logs={logs}, planks=2; revenue={saleRevenue}; realized gross profit={profit}; ticks={final.Tick}; cargo={final.Ship.CargoUnits}; trader cash=100->{final.TraderCash}."));
        return output.ToString();
    }

    private static PortTradeCommand Trade(string portId, TradeDirection direction, string goodId, int quantity) =>
        new PortTradeCommand(portId, direction, goodId, quantity);

    private static void RequireTraded(TradeAttempt trade, int quantity) =>
        Require(trade.Outcome == TradeOutcome.Traded && trade.ExecutedQuantity == quantity, "Expected complete trade.");

    private static void AppendStep(StringBuilder output, MaritimeStepResult step)
    {
        output.AppendLine(FormattableString.Invariant($"Tick {step.Tick}: trades, departure, local production, travel"));
        foreach (var attempt in step.TradeAttempts) AppendTrade(output, attempt.PortId, attempt.Trade, "Trade");
        foreach (var attempt in step.ProductionAttempts)
        {
            var changes = string.Join(", ", attempt.Production.InventoryChanges.Select(change =>
                FormattableString.Invariant($"{change.GoodId} {change.Before}->{change.After}")));
            output.AppendLine($"  Production {attempt.PortId}/{attempt.Production.BuildingInstanceId}: {attempt.Production.Outcome}; {changes}");
        }
        foreach (var movement in step.MovementEvents)
            output.AppendLine(FormattableString.Invariant(
                $"  Movement {movement.Kind}: {movement.ShipId} {movement.OriginPortId}->{movement.DestinationPortId}, remaining {movement.RemainingTicksBefore}->{movement.RemainingTicksAfter}"));
        AppendState(output, step.After);
    }

    private static void AppendTrade(StringBuilder output, string portId, TradeAttempt trade, string label)
    {
        var price = trade.Price == null ? "unpriced" : FormattableString.Invariant(
            $"first={trade.Price.FirstMarginalPrice}, last={trade.Price.LastMarginalPrice}, total={trade.Price.TotalAmount}");
        output.AppendLine(FormattableString.Invariant(
            $"  {label} {portId}: {trade.Direction} {trade.Quantity} {trade.GoodId}: {trade.Outcome}; {price}; executed={trade.ExecutedQuantity}/{trade.ExecutedAmount}"));
        output.AppendLine(FormattableString.Invariant(
            $"    stock port {trade.MarketInventoryChange.Before}->{trade.MarketInventoryChange.After}, cargo {trade.TraderInventoryChange.Before}->{trade.TraderInventoryChange.After}; cash port {trade.MarketCashChange.Before}->{trade.MarketCashChange.After}, trader {trade.TraderCashChange.Before}->{trade.TraderCashChange.After}"));
    }

    private static void AppendState(StringBuilder output, MaritimeWorldSnapshot state)
    {
        var ship = state.Ship;
        var position = ship.DockedPortId ?? FormattableString.Invariant(
            $"{ship.OriginPortId}->{ship.DestinationPortId}, remaining={ship.RemainingTravelTicks}");
        output.AppendLine(FormattableString.Invariant(
            $"State {state.Tick}: ship={position}; cargo logs={ship.Cargo["logs"]}, planks={ship.Cargo["planks"]}, used={ship.CargoUnits}/{ship.CargoCapacity}; trader cash={state.TraderCash}"));
        long totalCash = state.TraderCash;
        foreach (var item in state.Ports)
        {
            var port = item.Value;
            totalCash = checked(totalCash + port.MarketCash);
            var logs = state.GetMarketQuote(item.Key, "logs");
            var planks = state.GetMarketQuote(item.Key, "planks");
            output.AppendLine(FormattableString.Invariant(
                $"  {item.Key}: logs={port.Inventory["logs"]}, planks={port.Inventory["planks"]}, cash={port.MarketCash}; bid/ask logs={Number(logs.Bid)}/{Number(logs.Ask)}, planks={Number(planks.Bid)}/{Number(planks.Ask)}"));
        }
        Require(totalCash == 2100, "Cash conservation failed.");
        Require(ship.CargoUnits <= Capacity, "Cargo exceeded capacity.");
    }

    private static string Number(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "none";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
