using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public enum LivingPricingMode { StockOnly, Coverage }
    public enum ConsumptionKind { Legacy, Essential, Luxury, Municipal }
    public enum CityCashTransferKind { Wage, Retail }

    public sealed class SettlementConfiguration
    {
        public int InitialPopulation { get; }
        public long InitialHouseholdCash { get; }
        public int InitialProsperity { get; }
        public int MinimumPopulation { get; }
        public int MaximumPopulation { get; }
        public int DemographicIntervalDays { get; }
        public LivingPricingMode PricingMode { get; }
        public SettlementConfiguration(int initialPopulation, long initialHouseholdCash,
            int initialProsperity = 50, int minimumPopulation = 10, int maximumPopulation = 120,
            int demographicIntervalDays = 12, LivingPricingMode pricingMode = LivingPricingMode.StockOnly)
        {
            if (minimumPopulation < 1 || maximumPopulation < minimumPopulation ||
                initialPopulation < minimumPopulation || initialPopulation > maximumPopulation)
                throw new ArgumentOutOfRangeException(nameof(initialPopulation));
            if (initialHouseholdCash < 0) throw new ArgumentOutOfRangeException(nameof(initialHouseholdCash));
            if (initialProsperity < 0 || initialProsperity > 100) throw new ArgumentOutOfRangeException(nameof(initialProsperity));
            if (demographicIntervalDays < 1) throw new ArgumentOutOfRangeException(nameof(demographicIntervalDays));
            if (!Enum.IsDefined(typeof(LivingPricingMode), pricingMode)) throw new ArgumentOutOfRangeException(nameof(pricingMode));
            InitialPopulation = initialPopulation; InitialHouseholdCash = initialHouseholdCash;
            InitialProsperity = initialProsperity; MinimumPopulation = minimumPopulation;
            MaximumPopulation = maximumPopulation; DemographicIntervalDays = demographicIntervalDays;
            PricingMode = pricingMode;
        }
    }

    public sealed class NeedObservation
    {
        public long Day { get; }
        public int DesiredQuantity { get; }
        public int ConsumedQuantity { get; }
        public int BaselineDesiredQuantity { get; }
        public int UnmetStock { get; }
        public int UnmetAffordability { get; }
        public long Payment { get; }
        internal NeedObservation(long day, int desired, int consumed, int baseline, int stock, int affordability, long payment)
        {
            Day = day; DesiredQuantity = desired; ConsumedQuantity = consumed;
            BaselineDesiredQuantity = baseline; UnmetStock = stock; UnmetAffordability = affordability; Payment = payment;
        }
    }

    public sealed class CitySnapshot
    {
        public const int FoodWindowCapacity = 6;
        public SettlementConfiguration Configuration { get; }
        public int Population { get; }
        public int InitialPopulation => Configuration.InitialPopulation;
        public int PopulationDelta { get; }
        public long HouseholdCash { get; }
        public int Prosperity { get; }
        public string ProsperityReason { get; }
        public bool LuxuryEnabled { get; }
        public int PurchasingPowerPercent { get; }
        public long TotalWagesReceived { get; }
        public long LastWagesReceived { get; }
        public long TotalRetailSpend { get; }
        public long LastRetailSpend { get; }
        public long LastDemographicDay { get; }
        public int HealthyObservations { get; }
        public int DeprivedObservations { get; }
        public int LuxuryReadyObservations { get; }
        public int LuxuryPoorObservations { get; }
        public IReadOnlyList<NeedObservation> FoodWindow { get; }
        public int EssentialFulfillmentPercent => Fulfillment(false);
        public int RecoveryFulfillmentPercent => Fulfillment(true);
        internal CitySnapshot(SettlementConfiguration configuration, int population, int populationDelta,
            long cash, int prosperity, string reason, bool luxury, int power, long wages, long lastWages,
            long retail, long lastRetail, long demographicDay, int healthy, int deprived, int luxuryReady,
            int luxuryPoor, IEnumerable<NeedObservation> food)
        {
            Configuration = configuration; Population = population; PopulationDelta = populationDelta;
            HouseholdCash = cash; Prosperity = prosperity; ProsperityReason = reason; LuxuryEnabled = luxury;
            PurchasingPowerPercent = power; TotalWagesReceived = wages; LastWagesReceived = lastWages;
            TotalRetailSpend = retail; LastRetailSpend = lastRetail; LastDemographicDay = demographicDay;
            HealthyObservations = healthy; DeprivedObservations = deprived; LuxuryReadyObservations = luxuryReady;
            LuxuryPoorObservations = luxuryPoor; FoodWindow = Array.AsReadOnly(food.ToArray());
        }
        private int Fulfillment(bool baseline)
        {
            var desired = FoodWindow.Sum(n => (long)(baseline ? Math.Max(n.DesiredQuantity, n.BaselineDesiredQuantity) : n.DesiredQuantity));
            return desired == 0 ? 0 : (int)(FoodWindow.Sum(n => (long)n.ConsumedQuantity) * 100 / desired);
        }
        internal static CitySnapshot Initial(SettlementConfiguration configuration, long day) =>
            new CitySnapshot(configuration, configuration.InitialPopulation, 0, configuration.InitialHouseholdCash,
                configuration.InitialProsperity, "Awaiting paid essential-need observations.", false, 0,
                0, 0, 0, 0, day, 0, 0, 0, 0, Array.Empty<NeedObservation>());
    }

    public sealed class CityCashTransfer
    {
        public long Day { get; }
        public long Revision { get; }
        public CityCashTransferKind Kind { get; }
        public string SourceAccountId { get; }
        public string DestinationAccountId { get; }
        public string PortId { get; }
        public string? FacilityId { get; }
        public string? GoodId { get; }
        public long Amount { get; }
        internal CityCashTransfer(CityCashTransferKind kind, string source, string destination, string port,
            long amount, string? facility = null, string? good = null, long day = 0, long revision = 0)
        {
            Kind = kind; SourceAccountId = source; DestinationAccountId = destination; PortId = port;
            Amount = amount; FacilityId = facility; GoodId = good; Day = day; Revision = revision;
        }
        internal CityCashTransfer AtCommit(long day, long revision) =>
            new CityCashTransfer(Kind, SourceAccountId, DestinationAccountId, PortId, Amount, FacilityId, GoodId, day, revision);
    }

    public sealed class CityChange
    {
        public string PortId { get; }
        public long Day { get; }
        public int PopulationBefore { get; }
        public int PopulationAfter { get; }
        public int PopulationDelta => PopulationAfter - PopulationBefore;
        public int ProsperityBefore { get; }
        public int ProsperityAfter { get; }
        public string Reason { get; }
        internal CityChange(string portId, long day, CitySnapshot before, CitySnapshot after)
        {
            PortId = portId; Day = day; PopulationBefore = before.Population; PopulationAfter = after.Population;
            ProsperityBefore = before.Prosperity; ProsperityAfter = after.Prosperity; Reason = after.ProsperityReason;
        }
    }

    // Owned only by a RegionalWorldState working port; never independently commits or advances time.
    internal sealed class WorkingCity
    {
        internal readonly SettlementConfiguration Configuration;
        internal int Population, PopulationDelta, Prosperity, PurchasingPowerPercent;
        internal long Cash, Wages, LastWages, Retail, LastRetail, LastDemographicDay;
        internal int Healthy, Deprived, LuxuryReady, LuxuryPoor;
        internal bool Luxury;
        internal string Reason;
        internal readonly List<NeedObservation> Food;
        internal WorkingCity(CitySnapshot before)
        {
            Configuration = before.Configuration; Population = before.Population; PopulationDelta = before.PopulationDelta;
            Cash = before.HouseholdCash; Prosperity = before.Prosperity; Reason = before.ProsperityReason;
            PurchasingPowerPercent = before.PurchasingPowerPercent; Luxury = before.LuxuryEnabled;
            Wages = before.TotalWagesReceived; LastWages = before.LastWagesReceived;
            Retail = before.TotalRetailSpend; LastRetail = before.LastRetailSpend; LastDemographicDay = before.LastDemographicDay;
            Healthy = before.HealthyObservations; Deprived = before.DeprivedObservations;
            LuxuryReady = before.LuxuryReadyObservations; LuxuryPoor = before.LuxuryPoorObservations;
            Food = before.FoodWindow.ToList();
        }
        internal CitySnapshot Snapshot() => new CitySnapshot(Configuration, Population, PopulationDelta, Cash,
            Prosperity, Reason, Luxury, PurchasingPowerPercent, Wages, LastWages, Retail, LastRetail,
            LastDemographicDay, Healthy, Deprived, LuxuryReady, LuxuryPoor, Food);
        internal void BeginDay() { LastWages = 0; LastRetail = 0; }
        internal void ReceiveWage(long amount)
        {
            Cash = checked(Cash + amount); Wages = checked(Wages + amount); LastWages = checked(LastWages + amount);
        }
        internal void PayRetail(long amount)
        {
            Cash = checked(Cash - amount); Retail = checked(Retail + amount); LastRetail = checked(LastRetail + amount);
        }
        internal void Observe(NeedObservation observation, BigInteger nextFoodCost)
        {
            Food.Add(observation);
            if (Food.Count > CitySnapshot.FoodWindowCapacity) Food.RemoveAt(0);
            PurchasingPowerPercent = nextFoodCost <= 0 ? 100 : (int)BigInteger.Min(100, (BigInteger)Cash * 100 / nextFoodCost);
            var fulfillment = Snapshot().EssentialFulfillmentPercent;
            var target = (3 * fulfillment + PurchasingPowerPercent) / 4;
            var difference = target - Prosperity;
            Prosperity += Math.Sign(difference) * Math.Min(3, (Math.Abs(difference) + 7) / 8);
            var healthy = observation.ConsumedQuantity == observation.DesiredQuantity && PurchasingPowerPercent == 100;
            Healthy = healthy ? Math.Min(6, Healthy + 1) : 0;
            Deprived = observation.ConsumedQuantity * 2L < observation.DesiredQuantity ? Math.Min(6, Deprived + 1) : 0;
            LuxuryReady = Prosperity >= 65 && healthy ? Math.Min(3, LuxuryReady + 1) : 0;
            LuxuryPoor = Prosperity <= 40 || PurchasingPowerPercent < 25 ? Math.Min(3, LuxuryPoor + 1) : 0;
            if (LuxuryReady >= 3) Luxury = true;
            if (LuxuryPoor >= 3) Luxury = false;
            Reason = observation.UnmetStock > 0 && observation.UnmetAffordability > 0 ? "Food stock and purchasing power both constrain meals." :
                observation.UnmetStock > 0 ? "Food missing from the market." :
                observation.UnmetAffordability > 0 ? "Food stocked but household funds insufficient." :
                PurchasingPowerPercent < 100 ? "Meals fulfilled; little household reserve for the next food need." :
                "Paid food needs fulfilled with a reserve for the next opportunity.";
        }
        internal void ReviewPopulation(long day)
        {
            if (day - LastDemographicDay < Configuration.DemographicIntervalDays) return;
            LastDemographicDay = day;
            PopulationDelta = 0;
            if (Food.Count < CitySnapshot.FoodWindowCapacity) return;
            if (Healthy >= 3 && Prosperity >= 65 && Population < Configuration.MaximumPopulation) PopulationDelta = 1;
            else if (Deprived >= 3 && Prosperity <= 35 && Population > Configuration.MinimumPopulation) PopulationDelta = -1;
            Population = checked(Population + PopulationDelta);
        }
    }
}
