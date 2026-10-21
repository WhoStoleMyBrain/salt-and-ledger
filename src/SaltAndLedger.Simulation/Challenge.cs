using System;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public enum ChallengeStatus
    {
        Active,
        Succeeded,
        Failed,
        Abandoned
    }

    public enum ChallengeKind { CoastalSupply, RegionalRelief, EnterpriseAmbition, CityRecovery }

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
        public int RecoveryMinimumProsperity { get; }
        public int RecoveryWindowSize { get; }
        public bool ShouldSerializeRecoveryMinimumProsperity() => Kind == ChallengeKind.CityRecovery;
        public bool ShouldSerializeRecoveryWindowSize() => Kind == ChallengeKind.CityRecovery;

        public ChallengeConfiguration(string id, string title, string ownerId, string portId,
            string goodId, long requiredQuantity, long deadlineDay, long minimumCash,
            ChallengeKind kind = ChallengeKind.CoastalSupply, long requiredMargin = 0,
            int recoveryMinimumProsperity = 55, int recoveryWindowSize = 6)
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
            if (requiredMargin < 0 || (kind == ChallengeKind.EnterpriseAmbition && requiredMargin == 0))
                throw new ArgumentOutOfRangeException(nameof(requiredMargin));
            if (kind == ChallengeKind.CityRecovery && (goodId != "bread" || requiredQuantity > 100 ||
                recoveryMinimumProsperity < 0 || recoveryMinimumProsperity > 100 ||
                recoveryWindowSize < 1 || recoveryWindowSize > CitySnapshot.FoodWindowCapacity))
                throw new ArgumentException("Recovery requires a bounded paid Bread service window and prosperity target.");
            Id = id;
            Title = title;
            OwnerId = ownerId;
            PortId = portId;
            GoodId = goodId;
            RequiredQuantity = requiredQuantity;
            DeadlineDay = deadlineDay;
            MinimumCash = minimumCash;
            Kind = kind;
            RequiredMargin = requiredMargin;
            RecoveryMinimumProsperity = recoveryMinimumProsperity; RecoveryWindowSize = recoveryWindowSize;
        }
    }

    public sealed class CityRecoveryProgress
    {
        public int Observations { get; }
        public int RequiredObservations { get; }
        public int FulfillmentPercent { get; }
        public int ProgressPercent => Math.Min(FulfillmentPercent, Observations * 100 / RequiredObservations);
        public int Population { get; }
        public int InitialPopulation { get; }
        public int Prosperity { get; }
        public int MinimumProsperity { get; }
        public bool Qualified { get; }
        public string Reason { get; }
        internal CityRecoveryProgress(ChallengeConfiguration configuration, CitySnapshot city)
        {
            var window = city.FoodWindow.Skip(Math.Max(0, city.FoodWindow.Count - configuration.RecoveryWindowSize)).ToArray();
            Observations = window.Length; RequiredObservations = configuration.RecoveryWindowSize;
            var required = window.Sum(n => (long)Math.Max(n.DesiredQuantity, n.BaselineDesiredQuantity));
            FulfillmentPercent = required == 0 ? 0 : (int)(window.Sum(n => (long)n.ConsumedQuantity) * 100 / required);
            Population = city.Population; InitialPopulation = city.InitialPopulation;
            Prosperity = city.Prosperity; MinimumProsperity = configuration.RecoveryMinimumProsperity;
            Qualified = Observations == RequiredObservations && FulfillmentPercent >= configuration.RequiredQuantity &&
                Population >= InitialPopulation && Prosperity >= MinimumProsperity;
            Reason = Observations < RequiredObservations ? "More scheduled paid food opportunities are required." :
                Population < InitialPopulation ? "Population is below the initial baseline." :
                FulfillmentPercent < configuration.RequiredQuantity ? "Recent paid food fulfillment is below the target." :
                Prosperity < MinimumProsperity ? "Prosperity has not recovered sufficiently." :
                "Sustained city conditions met; company solvency and deadline still apply.";
        }
        internal bool SameAs(CityRecoveryProgress? other) => other != null &&
            Observations == other.Observations && RequiredObservations == other.RequiredObservations &&
            FulfillmentPercent == other.FulfillmentPercent && Population == other.Population &&
            InitialPopulation == other.InitialPopulation && Prosperity == other.Prosperity &&
            MinimumProsperity == other.MinimumProsperity && Qualified == other.Qualified && Reason == other.Reason;
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
        public CityRecoveryProgress? Recovery { get; }
        public bool ShouldSerializeRecovery() => Recovery != null;
        public long ObjectiveQuantity => Configuration.Kind == ChallengeKind.RegionalRelief ? RegionalConsumedQuantity :
            Configuration.Kind == ChallengeKind.EnterpriseAmbition ? ProducedQuantity :
            Configuration.Kind == ChallengeKind.CityRecovery ? Recovery?.ProgressPercent ?? 0 : DeliveredQuantity;

        internal ChallengeSnapshot(ChallengeConfiguration configuration, long netDeliveredQuantity,
            ChallengeStatus status, long? completedDay, long regionalConsumedQuantity = 0,
            long producedQuantity = 0, long realizedGrossMargin = 0, CityRecoveryProgress? recovery = null)
        {
            Configuration = configuration;
            NetDeliveredQuantity = netDeliveredQuantity;
            Status = status;
            CompletedDay = completedDay;
            RegionalConsumedQuantity = regionalConsumedQuantity;
            ProducedQuantity = producedQuantity;
            RealizedGrossMargin = realizedGrossMargin;
            Recovery = recovery;
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
                configuration.Kind == ChallengeKind.CityRecovery ||
                ownerId != configuration.OwnerId || portId != configuration.PortId ||
                trade.GoodId != configuration.GoodId) return snapshot;
            // Signed debt matters: buying locally and selling back must never create delivery credit.
            var quantity = checked(snapshot.NetDeliveredQuantity +
                (trade.Direction == TradeDirection.Sell ? (long)trade.ExecutedQuantity : -trade.ExecutedQuantity));
            return new ChallengeSnapshot(configuration, quantity, ChallengeStatus.Active, null,
                snapshot.RegionalConsumedQuantity, snapshot.ProducedQuantity, snapshot.RealizedGrossMargin);
        }

        internal static ChallengeSnapshot ApplyConsumption(ChallengeSnapshot snapshot, string goodId, int consumed)
        {
            if (snapshot.Status != ChallengeStatus.Active || goodId != snapshot.Configuration.GoodId || consumed == 0)
                return snapshot;
            if (snapshot.Configuration.Kind == ChallengeKind.CityRecovery) return snapshot;
            return new ChallengeSnapshot(snapshot.Configuration, snapshot.NetDeliveredQuantity, snapshot.Status, null,
                checked(snapshot.RegionalConsumedQuantity + consumed), snapshot.ProducedQuantity, snapshot.RealizedGrossMargin);
        }

        internal static ChallengeSnapshot Abandon(ChallengeSnapshot snapshot, long day) =>
            new ChallengeSnapshot(snapshot.Configuration, snapshot.NetDeliveredQuantity, ChallengeStatus.Abandoned, day,
                snapshot.RegionalConsumedQuantity, snapshot.ProducedQuantity, snapshot.RealizedGrossMargin, snapshot.Recovery);

        internal static ChallengeSnapshot Evaluate(ChallengeSnapshot snapshot, long day, long companyCash,
            CompanyFinancialSnapshot? financial = null, CitySnapshot? city = null)
        {
            if (snapshot.Status != ChallengeStatus.Active) return snapshot;
            var configuration = snapshot.Configuration;
            var produced = financial != null && financial.ProducedQuantities.TryGetValue(configuration.GoodId, out var quantity)
                ? quantity : snapshot.ProducedQuantity;
            var margin = financial?.RealizedGrossMargin ?? snapshot.RealizedGrossMargin;
            if (configuration.Kind == ChallengeKind.CityRecovery)
            {
                var recovery = new CityRecoveryProgress(configuration, city ??
                    throw new InvalidOperationException("A recovery objective needs its living town."));
                var won = day <= configuration.DeadlineDay && recovery.Qualified && companyCash >= configuration.MinimumCash;
                if (won || day >= configuration.DeadlineDay)
                    return new ChallengeSnapshot(configuration, snapshot.NetDeliveredQuantity,
                        won ? ChallengeStatus.Succeeded : ChallengeStatus.Failed, day,
                        snapshot.RegionalConsumedQuantity, produced, margin, recovery);
                if (recovery.SameAs(snapshot.Recovery) && snapshot.ProducedQuantity == produced && snapshot.RealizedGrossMargin == margin)
                    return snapshot;
                return new ChallengeSnapshot(configuration, snapshot.NetDeliveredQuantity, snapshot.Status, null,
                    snapshot.RegionalConsumedQuantity, produced, margin, recovery);
            }
            var progress = configuration.Kind == ChallengeKind.RegionalRelief ? snapshot.RegionalConsumedQuantity :
                configuration.Kind == ChallengeKind.EnterpriseAmbition ? produced : snapshot.NetDeliveredQuantity;
            var succeeded = day <= configuration.DeadlineDay &&
                progress >= configuration.RequiredQuantity &&
                (configuration.Kind != ChallengeKind.EnterpriseAmbition || margin >= configuration.RequiredMargin) &&
                companyCash >= configuration.MinimumCash;
            if (succeeded || day >= configuration.DeadlineDay)
                return new ChallengeSnapshot(configuration, snapshot.NetDeliveredQuantity,
                    succeeded ? ChallengeStatus.Succeeded : ChallengeStatus.Failed, day,
                    snapshot.RegionalConsumedQuantity, produced, margin);
            if (snapshot.ProducedQuantity != produced || snapshot.RealizedGrossMargin != margin)
                return new ChallengeSnapshot(configuration, snapshot.NetDeliveredQuantity, snapshot.Status, null,
                    snapshot.RegionalConsumedQuantity, produced, margin);
            return snapshot;
        }
    }
}
