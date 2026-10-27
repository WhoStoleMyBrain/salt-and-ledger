using System;

namespace SaltAndLedger.Simulation
{
    public enum ChallengeStatus
    {
        Active,
        Succeeded,
        Failed,
        Abandoned
    }

    public enum ChallengeKind { GrandCommission = 7 }

    /// <summary>A configured objective evaluated on the regional world's own day.</summary>
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
        public ChallengeKind Kind { get; }
        public long RequiredMargin { get; }
        public ChallengeConfiguration(string id, string title, string ownerId, string portId,
            string goodId, long requiredQuantity, long deadlineDay, long minimumCash,
            ChallengeKind kind = ChallengeKind.GrandCommission)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            RuntimeIdentifier.Validate(ownerId, nameof(ownerId));
            RuntimeIdentifier.Validate(portId, nameof(portId));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A challenge needs a title.", nameof(title));
            if (requiredQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(requiredQuantity));
            if (deadlineDay <= 0) throw new ArgumentOutOfRangeException(nameof(deadlineDay));
            if (minimumCash < 0) throw new ArgumentOutOfRangeException(nameof(minimumCash));
            if (!Enum.IsDefined(typeof(ChallengeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            Id = id;
            Title = title;
            OwnerId = ownerId;
            PortId = portId;
            GoodId = goodId;
            RequiredQuantity = requiredQuantity;
            DeadlineDay = deadlineDay;
            MinimumCash = minimumCash;
            Kind = kind;
            RequiredMargin = 0;
        }
    }

    public sealed class ChallengeSnapshot
    {
        public ChallengeConfiguration Configuration { get; }
        public long NetDeliveredQuantity { get; }
        public long DeliveredQuantity => Math.Max(0, NetDeliveredQuantity);
        public ChallengeStatus Status { get; }
        public long? CompletedDay { get; }
        public long RegionalConsumedQuantity { get; }
        public long ProducedQuantity { get; }
        public long RealizedGrossMargin { get; }
        public long ObjectiveQuantity => ProducedQuantity;

        internal ChallengeSnapshot(ChallengeConfiguration configuration, long netDeliveredQuantity,
            ChallengeStatus status, long? completedDay, long regionalConsumedQuantity = 0,
            long producedQuantity = 0, long realizedGrossMargin = 0)
        {
            Configuration = configuration;
            NetDeliveredQuantity = netDeliveredQuantity;
            Status = status;
            CompletedDay = completedDay;
            RegionalConsumedQuantity = regionalConsumedQuantity;
            ProducedQuantity = producedQuantity;
            RealizedGrossMargin = realizedGrossMargin;
        }
    }

    // Called only on privately staged state, using real completed market transactions.
    internal static class ChallengeRules
    {
        internal static ChallengeSnapshot Create(ChallengeConfiguration configuration) =>
            new ChallengeSnapshot(configuration, 0, ChallengeStatus.Active, null);

        internal static ChallengeSnapshot Abandon(ChallengeSnapshot snapshot, long day) =>
            new ChallengeSnapshot(snapshot.Configuration, snapshot.NetDeliveredQuantity, ChallengeStatus.Abandoned, day,
                snapshot.RegionalConsumedQuantity, snapshot.ProducedQuantity, snapshot.RealizedGrossMargin);

        internal static ChallengeSnapshot Evaluate(ChallengeSnapshot snapshot, long day, long companyCash,
            CompanyFinancialSnapshot? financial = null, CitySnapshot? city = null, RegionalWorldSnapshot? world = null)
        {
            if (snapshot.Status != ChallengeStatus.Active) return snapshot;
            var configuration = snapshot.Configuration;
            var commission = GrandCommission.Progress(world ?? throw new InvalidOperationException("A commission needs its authoritative world."));
            var won = commission.Qualified && companyCash >= configuration.MinimumCash && day <= configuration.DeadlineDay;
            var status = won ? ChallengeStatus.Succeeded : day >= configuration.DeadlineDay ? ChallengeStatus.Failed : ChallengeStatus.Active;
            if (snapshot.ProducedQuantity == commission.Percent && snapshot.Status == status &&
                snapshot.RealizedGrossMargin == financial!.RealizedGrossMargin) return snapshot;
            return new ChallengeSnapshot(configuration, 0, status, status == ChallengeStatus.Active ? (long?)null : day,
                0, commission.Percent, financial!.RealizedGrossMargin);
        }
    }
}
