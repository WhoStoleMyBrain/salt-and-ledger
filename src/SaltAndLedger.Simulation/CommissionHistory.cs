using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Bounded evidence of real company docking and local market operations.</summary>
    public sealed class CommissionHistory
    {
        public IReadOnlyList<CommissionShipVisits> Ships { get; }
        public IReadOnlyList<CommissionMarketBalance> Markets { get; }
        internal CommissionHistory(IEnumerable<CommissionShipVisits> ships, IEnumerable<CommissionMarketBalance> markets)
        {
            Ships = Array.AsReadOnly(ships.OrderBy(s => s.AcquisitionOrder).ToArray());
            Markets = Array.AsReadOnly(markets.OrderBy(m => m.PortId, StringComparer.Ordinal)
                .ThenBy(m => m.GoodId, StringComparer.Ordinal).ToArray());
            if (Ships.Count > 3 || Markets.Count > 28) throw new InvalidOperationException("Commission history exceeds its fixed world bounds.");
        }

        internal static CommissionHistory Initial(IEnumerable<RegionalShipSnapshot> ships, string ownerId, long day) =>
            new CommissionHistory(ships.Where(s => s.OwnerId == ownerId).OrderBy(s => s.Id, StringComparer.Ordinal)
                .Select((s, index) => new CommissionShipVisits(s.Id, index,
                    new Dictionary<string, long> { [s.DockedPortId!] = day })), Array.Empty<CommissionMarketBalance>());
    }

    public sealed class CommissionShipVisits
    {
        public string ShipId { get; }
        public int AcquisitionOrder { get; }
        public IReadOnlyDictionary<string, long> Visits { get; }
        internal CommissionShipVisits(string shipId, int acquisitionOrder, IReadOnlyDictionary<string, long> visits)
        {
            ShipId = shipId; AcquisitionOrder = acquisitionOrder;
            Visits = new ReadOnlyDictionary<string, long>(new SortedDictionary<string, long>(
                visits.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal));
            if (Visits.Count > 4) throw new InvalidOperationException("A commission has four towns.");
        }
    }

    public sealed class CommissionMarketBalance
    {
        public string PortId { get; }
        public string GoodId { get; }
        public long PurchasedQuantity { get; }
        public long SoldQuantity { get; }
        public long NetQuantity => checked(SoldQuantity - PurchasedQuantity);
        internal CommissionMarketBalance(string portId, string goodId, long purchasedQuantity, long soldQuantity)
        { PortId = portId; GoodId = goodId; PurchasedQuantity = purchasedQuantity; SoldQuantity = soldQuantity; }
    }

    internal sealed class WorkingCommissionHistory
    {
        private readonly List<CommissionShipVisits> ships;
        private readonly Dictionary<string, CommissionMarketBalance> markets;
        internal WorkingCommissionHistory(CommissionHistory snapshot)
        {
            ships = snapshot.Ships.ToList();
            markets = snapshot.Markets.ToDictionary(m => m.PortId + ":" + m.GoodId, StringComparer.Ordinal);
        }
        internal void Visit(string shipId, string portId, long day)
        {
            var index = ships.FindIndex(s => s.ShipId == shipId);
            if (index < 0)
            {
                if (ships.Count == 3) throw new InvalidOperationException("Commission fleet exceeds three owned ships.");
                ships.Add(new CommissionShipVisits(shipId, ships.Count, new Dictionary<string, long> { [portId] = day }));
                return;
            }
            if (ships[index].Visits.ContainsKey(portId)) return;
            var visits = ships[index].Visits.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            visits.Add(portId, day);
            ships[index] = new CommissionShipVisits(shipId, ships[index].AcquisitionOrder, visits);
        }
        internal void Trade(string portId, TradeAttempt trade)
        {
            if (trade.Outcome != TradeOutcome.Traded) return;
            var key = portId + ":" + trade.GoodId;
            markets.TryGetValue(key, out var previous);
            var bought = previous?.PurchasedQuantity ?? 0;
            var sold = previous?.SoldQuantity ?? 0;
            if (trade.Direction == TradeDirection.Buy) bought = checked(bought + trade.ExecutedQuantity);
            else sold = checked(sold + trade.ExecutedQuantity);
            markets[key] = new CommissionMarketBalance(portId, trade.GoodId, bought, sold);
        }
        internal CommissionHistory Snapshot() => new CommissionHistory(ships, markets.Values);
    }
}
