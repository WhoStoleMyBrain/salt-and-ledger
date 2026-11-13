using System;
using Newtonsoft.Json;

namespace SaltAndLedger.Simulation
{
    public enum NpcCompanyProfile { Merchant, Industrial, Diversified }

    /// <summary>Chosen once from geography; mutable counters are replaced within the daily transaction.</summary>
    [JsonObject(ItemRequired = Required.AllowNull)]
    public sealed class NpcCompanyState
    {
        public string Name { get; }
        public NpcCompanyProfile Profile { get; }
        public string HomePortId { get; }
        public string DestinationPortId { get; }
        public string GoodId { get; }
        public string? BlueprintId { get; }
        public long NextReviewDay { get; }
        public int ShipsOrdered { get; }
        public string LastInvestment { get; }
        [JsonIgnore] public string EconomicObjective => BlueprintId == null
            ? "Develop the " + GoodId + " trade between " + HomePortId + " and " + DestinationPortId
            : "Supply and distribute " + GoodId + " through owned production and shipping";
        [JsonConstructor]
        public NpcCompanyState(string name, NpcCompanyProfile profile, string homePortId, string destinationPortId,
            string goodId, string? blueprintId, long nextReviewDay = 0, int shipsOrdered = 0, string lastInvestment = "Holding capital")
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 96 || !Enum.IsDefined(typeof(NpcCompanyProfile), profile) ||
                nextReviewDay < 0 || shipsOrdered < 0 || shipsOrdered > 8 || lastInvestment == null || lastInvestment.Length > 160)
                throw new ArgumentException("Invalid bounded NPC company state.");
            RuntimeIdentifier.Validate(homePortId, nameof(homePortId)); RuntimeIdentifier.Validate(destinationPortId, nameof(destinationPortId));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (blueprintId != null) RuntimeIdentifier.Validate(blueprintId, nameof(blueprintId));
            if (homePortId == destinationPortId) throw new ArgumentException("A company needs two distinct business harbors.");
            Name = name; Profile = profile; HomePortId = homePortId; DestinationPortId = destinationPortId;
            GoodId = goodId; BlueprintId = blueprintId; NextReviewDay = nextReviewDay; ShipsOrdered = shipsOrdered;
            LastInvestment = lastInvestment;
        }
        internal NpcCompanyState With(long? nextReviewDay = null, int? shipsOrdered = null, string? lastInvestment = null) =>
            new NpcCompanyState(Name, Profile, HomePortId, DestinationPortId, GoodId, BlueprintId,
                nextReviewDay ?? NextReviewDay, shipsOrdered ?? ShipsOrdered, lastInvestment ?? LastInvestment);
    }
}
