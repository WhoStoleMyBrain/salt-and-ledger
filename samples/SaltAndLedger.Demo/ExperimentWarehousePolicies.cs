using System;
using System.Linq;
using SaltAndLedger.Simulation;

/// <summary>Explicit research setup: combine town needs once, retaining inputs before surplus sales.</summary>
internal static class ExperimentWarehousePolicies
{
    internal static WarehousePolicy Combine(WarehousePolicy existing, WarehousePolicy addition)
    {
        var rules = existing.Rules.ToDictionary(r => r.GoodId, StringComparer.Ordinal);
        foreach (var next in addition.Rules)
        {
            rules.TryGetValue(next.GoodId, out var old);
            var buy = next.BuyEnabled || old?.BuyEnabled == true;
            var target = next.BuyEnabled ? next.BuyTarget : old?.BuyTarget ?? next.BuyTarget;
            var maximum = next.BuyEnabled ? next.MaximumBuyPrice : old?.MaximumBuyPrice ?? next.MaximumBuyPrice;
            var sell = next.SellEnabled || old?.SellEnabled == true;
            var reserve = next.SellEnabled ? next.SellReserve : old?.SellReserve ?? next.SellReserve;
            var minimum = next.SellEnabled ? next.MinimumSellPrice : old?.MinimumSellPrice ?? next.MinimumSellPrice;
            if (buy && sell) reserve = Math.Max(reserve, target);
            rules[next.GoodId] = new WarehouseCommodityRule(next.GoodId, buy, target, maximum, sell, reserve, minimum);
        }
        return new WarehousePolicy(Math.Max(existing.CashReserve, addition.CashReserve), rules.Values);
    }

    internal static WarehousePolicy Sell(string good, long cashReserve, bool enabled = true) =>
        new WarehousePolicy(cashReserve, new[] { new WarehouseCommodityRule(good, false, 0, 0, enabled, 0, 1) });

    internal static WarehousePolicy RecoveryBakery(long cashReserve) => new WarehousePolicy(cashReserve,
        new[] { new WarehouseCommodityRule("logs", true, EconomicUnits.FromWhole(3), 18, false, 0, 0),
            new WarehouseCommodityRule("bread", false, 0, 0, true, 0, 1) });
}
