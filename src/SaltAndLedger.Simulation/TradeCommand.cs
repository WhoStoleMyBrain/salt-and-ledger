using System;

namespace SaltAndLedger.Simulation
{
    public enum TradeDirection
    {
        Buy,
        Sell
    }

    /// <summary>A one-step, all-or-nothing request, from the trader's perspective.</summary>
    public sealed class TradeCommand
    {
        public TradeDirection Direction { get; }
        public string GoodId { get; }
        public int Quantity { get; }

        public TradeCommand(TradeDirection direction, string goodId, int quantity)
        {
            if (direction != TradeDirection.Buy && direction != TradeDirection.Sell)
                throw new ArgumentOutOfRangeException(nameof(direction));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
            Direction = direction;
            GoodId = goodId;
            Quantity = quantity;
        }
    }
}
