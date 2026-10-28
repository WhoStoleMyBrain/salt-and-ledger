using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class WarehouseCommodityRule
    {
        public string GoodId { get; }
        public bool BuyEnabled { get; }
        public int BuyTarget { get; }
        public long MaximumBuyPrice { get; }
        public bool SellEnabled { get; }
        public int SellReserve { get; }
        public long MinimumSellPrice { get; }

        public WarehouseCommodityRule(string goodId, bool buyEnabled, int buyTarget, long maximumBuyPrice,
            bool sellEnabled, int sellReserve, long minimumSellPrice)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (buyTarget < 0 || sellReserve < 0) throw new ArgumentOutOfRangeException(nameof(buyTarget));
            if (maximumBuyPrice < 0 || minimumSellPrice < 0) throw new ArgumentOutOfRangeException(nameof(maximumBuyPrice));
            if (buyEnabled && sellEnabled && buyTarget > sellReserve)
                throw new ArgumentException("Buy target cannot exceed sell reserve when both directions are enabled.");
            GoodId = goodId; BuyEnabled = buyEnabled; BuyTarget = buyTarget; MaximumBuyPrice = maximumBuyPrice;
            SellEnabled = sellEnabled; SellReserve = sellReserve; MinimumSellPrice = minimumSellPrice;
        }
    }

    public sealed class WarehousePolicy
    {
        public long CashReserve { get; }
        public IReadOnlyList<WarehouseCommodityRule> Rules { get; }
        public WarehousePolicy(long cashReserve = 0, IEnumerable<WarehouseCommodityRule>? rules = null)
        {
            if (cashReserve < 0) throw new ArgumentOutOfRangeException(nameof(cashReserve));
            var items = (rules ?? Array.Empty<WarehouseCommodityRule>()).ToArray();
            if (items.Any(r => r == null)) throw new ArgumentException("Warehouse rules cannot contain null.");
            if (items.Select(r => r.GoodId).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("Warehouse rules must name unique goods.");
            CashReserve = cashReserve;
            Rules = Array.AsReadOnly(items.OrderBy(r => r.GoodId, StringComparer.Ordinal).ToArray());
        }

        internal void Validate(IReadOnlyDictionary<string, int> inventory, int capacity)
        {
            if (Rules.Any(r => !inventory.ContainsKey(r.GoodId))) throw new ArgumentException("Unknown warehouse good.");
            if (Rules.Any(r => r.BuyTarget > capacity || r.SellReserve > capacity))
                throw new ArgumentException("Warehouse targets and reserves cannot exceed constructed capacity.");
        }

        internal bool Equivalent(WarehousePolicy other) => CashReserve == other.CashReserve &&
            Rules.Count == other.Rules.Count && Rules.Zip(other.Rules, (a, b) => a.GoodId == b.GoodId &&
                a.BuyEnabled == b.BuyEnabled && a.BuyTarget == b.BuyTarget && a.MaximumBuyPrice == b.MaximumBuyPrice &&
                a.SellEnabled == b.SellEnabled && a.SellReserve == b.SellReserve && a.MinimumSellPrice == b.MinimumSellPrice).All(x => x);
    }

    /// <summary>A current-state policy proposal, not a forecast of intervening actors or production.</summary>
    public sealed class WarehouseTradePreview
    {
        public string GoodId { get; }
        public TradeDirection Direction { get; }
        public int RequestedQuantity { get; }
        public int Quantity { get; }
        public IndustryWaitReason Reason { get; }
        public TradeAttempt? Trade { get; }
        public long TotalAmount => Trade?.TotalAmount ?? 0;
        public long MarginalPrice => Trade?.Price?.LastMarginalPrice ?? 0;
        internal WarehouseTradePreview(string goodId, TradeDirection direction, int requested, int quantity,
            IndustryWaitReason reason, TradeAttempt? trade)
        { GoodId = goodId; Direction = direction; RequestedQuantity = requested; Quantity = quantity; Reason = reason; Trade = trade; }
    }

    internal static class WarehouseTradeRules
    {
        internal static WarehouseTradePreview Preview(WarehousePolicy policy, WarehouseCommodityRule? rule,
            string goodId, TradeDirection direction, MarketQuote quote, int marketStock, int privateStock,
            long marketCash, long companyCash, int freeCapacity, Func<TradeCommand, TradeAttempt> preview)
        {
            if (!Enum.IsDefined(typeof(TradeDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            var buy = direction == TradeDirection.Buy;
            if (rule == null || !(buy ? rule.BuyEnabled : rule.SellEnabled))
                return new WarehouseTradePreview(goodId, direction, 0, 0, IndustryWaitReason.Disabled, null);
            var wanted = Math.Max(0, buy ? rule.BuyTarget - privateStock : privateStock - rule.SellReserve);
            if (wanted == 0) return new WarehouseTradePreview(goodId, direction, 0, 0,
                buy ? IndustryWaitReason.TargetSatisfied : IndustryWaitReason.ReserveRetained, null);
            var limit = buy ? rule.MaximumBuyPrice : rule.MinimumSellPrice;
            var quantity = TradeQuantityCalculator.Maximum(quote, direction, marketStock, privateStock,
                marketCash, companyCash, freeCapacity, wanted, limit, buy ? policy.CashReserve : 0);
            var reason = IndustryWaitReason.None;
            if (quantity < wanted)
            {
                var next = checked(quantity + 1);
                if (buy && quantity == marketStock) reason = IndustryWaitReason.InsufficientMarketInventory;
                else if (buy && (quantity == freeCapacity || quantity == int.MaxValue - privateStock)) reason = IndustryWaitReason.WarehouseFull;
                else if (!buy && quantity == int.MaxValue - marketStock) reason = IndustryWaitReason.MarketInventoryFull;
                else if (limit > 0 && !TradeQuantityCalculator.MeetsPrice(quote, direction, marketStock, next, limit))
                    reason = buy ? IndustryWaitReason.BuyPriceLimit : IndustryWaitReason.SellPriceLimit;
                else if (!quote.FitsBudget(direction, marketStock, next, long.MaxValue - (buy ? marketCash : companyCash)))
                    reason = IndustryWaitReason.CashAccountFull;
                else if (!buy) reason = IndustryWaitReason.InsufficientMarketFunds;
                else reason = quote.FitsBudget(direction, marketStock, next, companyCash)
                    ? IndustryWaitReason.CashReserve : IndustryWaitReason.InsufficientCompanyFunds;
            }
            return new WarehouseTradePreview(goodId, direction, wanted, quantity, reason,
                quantity > 0 ? preview(new TradeCommand(direction, goodId, quantity)) : null);
        }
    }

    public sealed partial class RegionalWorldSnapshot
    {
        public WarehouseTradePreview PreviewWarehouseAutomation(string ownerId, string portId,
            TradeDirection direction, string goodId, WarehousePolicy? policy = null)
        {
            var storage = CompanyStockpiles[CompanyStockpileSnapshot.StoreId(ownerId, portId)];
            var selected = policy ?? storage.Policy;
            selected.Validate(storage.Inventory, storage.Capacity);
            var port = Ports[portId];
            var quote = port.Quotes[goodId];
            TradeAttempt Preview(TradeCommand command)
            {
                var gross = TradeCalculator.Preview(quote, command, port.Inventory[goodId], storage.Inventory[goodId],
                    port.MarketCash, Companies[ownerId].Cash, storage.Capacity - storage.StoredUnits);
                return Circulation?.Preview(ownerId, portId, port.City!.HouseholdCash, gross) ?? gross;
            }
            return WarehouseTradeRules.Preview(selected, selected.Rules.FirstOrDefault(r => r.GoodId == goodId),
                goodId, direction, quote, port.Inventory[goodId], storage.Inventory[goodId], port.MarketCash,
                Companies[ownerId].Cash, storage.Capacity - storage.StoredUnits, Preview);
        }
    }
}
