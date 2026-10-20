using System;

namespace SaltAndLedger.Simulation
{
    public enum ChallengeStatus
    {
        Active,
        Succeeded,
        Failed
    }

    /// <summary>A fixed net-supply objective evaluated on the regional world's own day.</summary>
    public sealed class ChallengeConfiguration
    {
        public string Id { get; }
        public string Title { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public string GoodId { get; }
        public long RequiredQuantity { get; }
        public long DeadlineDay { get; }
        public long MinimumCash { get; }

        public ChallengeConfiguration(string id, string title, string ownerId, string portId,
            string goodId, long requiredQuantity, long deadlineDay, long minimumCash)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            RuntimeIdentifier.Validate(ownerId, nameof(ownerId));
            RuntimeIdentifier.Validate(portId, nameof(portId));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A challenge needs a title.", nameof(title));
            if (requiredQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(requiredQuantity));
            if (deadlineDay <= 0) throw new ArgumentOutOfRangeException(nameof(deadlineDay));
            if (minimumCash < 0) throw new ArgumentOutOfRangeException(nameof(minimumCash));
            Id = id;
            Title = title;
            OwnerId = ownerId;
            PortId = portId;
            GoodId = goodId;
            RequiredQuantity = requiredQuantity;
            DeadlineDay = deadlineDay;
            MinimumCash = minimumCash;
        }
    }

    public sealed class ChallengeSnapshot
    {
        public ChallengeConfiguration Configuration { get; }
        public long NetDeliveredQuantity { get; }
        public long DeliveredQuantity => Math.Max(0, NetDeliveredQuantity);
        public ChallengeStatus Status { get; }
        public long? CompletedDay { get; }

        internal ChallengeSnapshot(ChallengeConfiguration configuration, long netDeliveredQuantity,
            ChallengeStatus status, long? completedDay)
        {
            Configuration = configuration;
            NetDeliveredQuantity = netDeliveredQuantity;
            Status = status;
            CompletedDay = completedDay;
        }
    }

    // Called only on privately staged state, using real completed market transactions.
    internal static class ChallengeRules
    {
        internal static ChallengeSnapshot Create(ChallengeConfiguration configuration) =>
            new ChallengeSnapshot(configuration, 0, ChallengeStatus.Active, null);

        internal static ChallengeSnapshot ApplyTrade(ChallengeSnapshot snapshot, string ownerId,
            string portId, TradeAttempt trade)
        {
            var configuration = snapshot.Configuration;
            if (snapshot.Status != ChallengeStatus.Active || trade.Outcome != TradeOutcome.Traded ||
                ownerId != configuration.OwnerId || portId != configuration.PortId ||
                trade.GoodId != configuration.GoodId) return snapshot;
            // Signed debt matters: buying locally and selling back must never create delivery credit.
            var quantity = checked(snapshot.NetDeliveredQuantity +
                (trade.Direction == TradeDirection.Sell ? (long)trade.ExecutedQuantity : -trade.ExecutedQuantity));
            return new ChallengeSnapshot(configuration, quantity, ChallengeStatus.Active, null);
        }

        internal static ChallengeSnapshot Evaluate(ChallengeSnapshot snapshot, long day, long companyCash)
        {
            if (snapshot.Status != ChallengeStatus.Active) return snapshot;
            var configuration = snapshot.Configuration;
            var succeeded = day <= configuration.DeadlineDay &&
                snapshot.NetDeliveredQuantity >= configuration.RequiredQuantity &&
                companyCash >= configuration.MinimumCash;
            if (succeeded || day >= configuration.DeadlineDay)
                return new ChallengeSnapshot(configuration, snapshot.NetDeliveredQuantity,
                    succeeded ? ChallengeStatus.Succeeded : ChallengeStatus.Failed, day);
            return snapshot;
        }
    }
}
