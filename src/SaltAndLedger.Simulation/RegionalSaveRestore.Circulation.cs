using System;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    internal static partial class RegionalSaveRestore
    {
        private static CirculationSnapshot? RestoreCirculation(RegionalWorldSnapshot initial, RegionalSaveData data)
        {
            var saved = data.Circulation;
            Require((saved == null) == (initial.Circulation == null), "Economic circulation state presence changed.");
            if (saved == null)
            {
                Require(data.Companies.All(c => c.FeesPaid == 0), "Historical runs cannot contain service expenses.");
                return null;
            }
            Require(data.Identity.EconomicRulesVersion == CirculationConfiguration.CurrentVersion &&
                saved.Version == data.Identity.EconomicRulesVersion && saved.Mode == CirculationMode.Combined,
                "Only the named combined circulation rule supports saved runs.");
            var config = new CirculationConfiguration(saved.Version, saved.Mode);
            SameIds(saved.Commercial.Select(c => CommercialCirculationSnapshot.Key(c.OwnerId, c.PortId)),
                initial.Circulation!.Commercial.Keys, "company/town service accounts");
            SameIds(saved.Towns.Select(t => t.PortId), initial.Ports.Keys, "municipal accounts");
            var commercial = saved.Commercial.Select(c =>
            {
                Nonnegative(c.GrossLocalSales, c.FeesPaid, c.MarketFees, c.HouseholdFees);
                var row = new CommercialCirculationSnapshot(c.OwnerId, c.PortId, c.GrossLocalSales);
                Require(c.FeesPaid == row.FeesPaid && c.MarketFees == row.MarketFees && c.HouseholdFees == row.HouseholdFees,
                    "Service totals disagree with exact cumulative gross proceeds.");
                return row;
            }).ToArray();
            foreach (var company in data.Companies)
            {
                var rows = commercial.Where(c => c.OwnerId == company.Id).ToArray();
                Require(rows.Aggregate(BigInteger.Zero, (sum, c) => sum + c.FeesPaid) == company.FeesPaid,
                    "Company service expenses differ from local recipients.");
                var publicRevenue = company.Id == EnterpriseScenario.PlayerOwnerId ?
                    data.Economy!.Contracts.Aggregate(BigInteger.Zero, (sum, c) => sum + c.Paid) +
                        data.Economy.TreasuryExpenditure : BigInteger.Zero;
                Require(rows.Aggregate(BigInteger.Zero, (sum, c) => sum + c.GrossLocalSales) + publicRevenue == company.Revenue,
                    "Local service proceeds differ from company gross sales and public transactions.");
            }
            var interval = CirculationConfiguration.ReviewIntervalDays;
            var reviewCount = data.Day / interval;
            var lastReview = checked(reviewCount * interval);
            var towns = saved.Towns.Select(t =>
            {
                Nonnegative(t.TotalLevy, t.TotalRelief, t.LastLevy, t.LastRelief,
                    t.PayrollReserve, t.DuePayroll, t.FoodReserve);
                Defined(t.LastReason);
                Require(t.LastReviewDay == lastReview && t.NextReviewDay == checked(lastReview + interval),
                    "Municipal review dates do not match the exact four-day schedule.");
                Require(t.LastLevy <= t.TotalLevy && t.LastRelief <= t.TotalRelief &&
                    t.LastLevy <= CirculationConfiguration.MaximumFiscalTransfer &&
                    t.LastRelief <= CirculationConfiguration.MaximumFiscalTransfer &&
                    (BigInteger)t.TotalLevy + t.TotalRelief <= (BigInteger)reviewCount * CirculationConfiguration.MaximumFiscalTransfer,
                    "Municipal transfer counters exceed their actual review budget.");
                Require(t.LastLevy == 0 || t.LastRelief == 0, "A review cannot levy and relieve simultaneously.");
                Require((t.LastLevy > 0) == (t.LastReason == MunicipalReviewReason.PayrollLevy) &&
                    (t.LastRelief > 0) == (t.LastReason == MunicipalReviewReason.FoodRelief),
                    "Municipal last transfer and reason disagree.");
                Require(t.DuePayroll <= t.PayrollReserve && t.LastLevy <= t.DuePayroll && t.LastRelief <= t.FoodReserve,
                    "Municipal transfers exceed their recorded productive payroll or food need.");
                var maximumPayroll = initial.Ports[t.PortId].Facilities.Aggregate(BigInteger.Zero,
                    (sum, f) => sum + f.Configuration.WagePerBatch);
                Require(t.PayrollReserve <= maximumPayroll, "Municipal payroll reserve exceeds installed public wages.");
                if (reviewCount == 0)
                    Require(t.LastReason == MunicipalReviewReason.AwaitingReview && t.PayrollReserve == 0 &&
                        t.DuePayroll == 0 && t.FoodReserve == 0, "An unreviewed municipality cannot have reviewed reserves.");
                else Require(t.LastReason != MunicipalReviewReason.AwaitingReview && t.LastReason != MunicipalReviewReason.Disabled,
                    "A combined-rule municipality must retain its completed review outcome.");
                return new MunicipalCirculationSnapshot(t.PortId, t.NextReviewDay, t.LastReviewDay,
                    t.TotalLevy, t.TotalRelief, t.LastLevy, t.LastRelief, t.PayrollReserve,
                    t.DuePayroll, t.FoodReserve, t.LastReason);
            }).ToArray();
            return new CirculationSnapshot(config, commercial, towns);
        }
    }
}
