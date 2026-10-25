using System;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldState
    {
        private void AuditExpandingCommit(RegionalResult result)
        {
            var after = result.After;
            var economy = after.Economy;
            if (economy == null) return;
            var config = economy.Configuration;
            AuditCirculationCommit(result);
            if (after.RegionalCash != initialCombinedCash - config.InitialExternalCash + economy.MintedCurrency + economy.ExternalNetInflow ||
                (BigInteger)economy.ExternalCash + economy.ExternalNetInflow != config.InitialExternalCash ||
                (BigInteger)economy.MintedCurrency != (BigInteger)economy.MintBatches * config.CurrencyPerBatch ||
                (BigInteger)economy.SupplierIssuance != (BigInteger)economy.MintBatches * config.SupplierSharePerBatch ||
                (BigInteger)economy.TreasuryIssuance != (BigInteger)economy.MintBatches * config.TreasurySharePerBatch ||
                (BigInteger)economy.SilverReserve != (BigInteger)economy.MintBatches * config.SilverPerBatch ||
                economy.MintBatches < 0 || economy.MintBatches > config.MaximumMintBatches)
                throw new InvalidOperationException("Regional monetary boundary or authorized Silver issuance does not reconcile.");
            var commissions = after.Accounting.Companies.Values.Aggregate(BigInteger.Zero,
                (sum, company) => sum + company.PublicCommissionExpenditure);
            if ((BigInteger)economy.TreasuryCash != config.InitialTreasuryCash + commissions + economy.TreasuryIssuance - economy.TreasuryExpenditure ||
                (BigInteger)economy.TreasuryExpenditure != (BigInteger)economy.TreasuryPlanks * config.TreasuryPlankPrice ||
                economy.TreasuryPlanks > config.TreasuryPlankRequirement || economy.TreasuryCash < 0 || economy.ExternalCash < 0 ||
                economy.Contracts.Aggregate(BigInteger.Zero, (sum, c) => sum + c.AmountPaid) != economy.ExternalNetInflow ||
                economy.Contracts.Any(c => c.DeliveredQuantity < 0 || c.RemainingQuantity < 0 ||
                    (BigInteger)c.AmountPaid != (BigInteger)c.DeliveredQuantity * c.Configuration.UnitPrice))
                throw new InvalidOperationException("Public and external payments must reconcile with funded physical deliveries.");
            if (after.Accounting.Companies.Values.Aggregate(BigInteger.Zero, (sum, f) => sum + f.MintReceipts) != economy.SupplierIssuance)
                throw new InvalidOperationException("Every supplier issuance requires an owned accounting receipt.");
            foreach (var owner in after.Companies.Keys) _ = after.CompanyValue(owner).Total;
            foreach (var good in content.Goods)
            {
                var initial = Physical(result.Before, good.Id);
                var current = Physical(after, good.Id);
                var production = result.Production.SelectMany(p => p.Changes).Concat(result.PrivateProduction.SelectMany(p => p.Changes))
                    .Where(c => c.GoodId == good.Id).Aggregate(BigInteger.Zero, (sum, c) => sum + c.Delta);
                var consumed = result.Consumption.Where(c => c.GoodId == good.Id).Aggregate(BigInteger.Zero, (sum, c) => sum + c.ConsumedQuantity);
                var development = result.Development.Where(d => d.GoodId == good.Id &&
                    ((d.Outcome == DevelopmentOutcome.Completed && (d.Kind == DevelopmentKind.ExternalExport || d.Kind == DevelopmentKind.TreasuryProcurement)) ||
                     (d.Outcome == DevelopmentOutcome.Started && d.Kind == DevelopmentKind.ShipCommission)))
                    .Aggregate(BigInteger.Zero, (sum, d) => sum + d.Quantity);
                if (current != initial + production - consumed - development)
                    throw new InvalidOperationException("Physical flow does not reconcile for " + good.Id + ".");
                foreach (var owner in after.Accounting.Companies.Values)
                {
                    long Amount(System.Collections.Generic.IReadOnlyDictionary<string, long> values) => values.TryGetValue(good.Id, out var value) ? value : 0;
                    if ((BigInteger)Amount(owner.GrantedQuantities) + Amount(owner.PurchasedQuantities) + Amount(owner.ProducedQuantities) -
                        Amount(owner.ConsumedQuantities) - Amount(owner.SoldQuantities) - Amount(owner.SurrenderedQuantities) != Amount(owner.HeldInventoryQuantities))
                        throw new InvalidOperationException("Owned physical/FIFO totals do not reconcile for " + owner.OwnerId + "/" + good.Id);
                }
            }
            foreach (var store in after.Accounting.Stores.Values)
            {
                var inventory = store.StoreId.StartsWith("ship:", StringComparison.Ordinal)
                    ? after.Ships[store.StoreId.Substring(5)].Cargo : store.StoreId.StartsWith("company:", StringComparison.Ordinal)
                        ? after.CompanyStockpiles[store.StoreId].Inventory : after.PrivateFacilities[store.StoreId.Substring(9)].Inventory;
                foreach (var good in inventory)
                    if (store.GetQuantity(good.Key) != good.Value)
                        throw new InvalidOperationException("Physical store and FIFO slices disagree.");
            }
        }

        private static BigInteger Physical(RegionalWorldSnapshot world, string goodId) =>
            world.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.Inventory[goodId]) +
            world.Ships.Values.Aggregate(BigInteger.Zero, (sum, s) => sum + s.Cargo[goodId]) +
            world.PrivateFacilities.Values.Aggregate(BigInteger.Zero, (sum, f) => sum + f.Inventory[goodId]) +
            world.CompanyStockpiles.Values.Aggregate(BigInteger.Zero, (sum, s) => sum + s.Inventory[goodId]) +
            (goodId == "silver" ? world.Economy?.SilverReserve ?? 0 : 0);

        private static void AuditCirculationCommit(RegionalResult result)
        {
            var after = result.After;
            var circulation = after.Circulation;
            if (circulation == null)
            {
                if (after.Accounting.Companies.Values.Any(f => f.FeesPaid != 0))
                    throw new InvalidOperationException("Historical worlds cannot acquire circulation expenses.");
                return;
            }
            var before = result.Before.Circulation ?? throw new InvalidOperationException("Circulation cannot appear during a run.");
            if (circulation.Commercial.Count != after.Companies.Count * after.Ports.Count || circulation.Towns.Count != after.Ports.Count)
                throw new InvalidOperationException("Circulation must retain one row per company/town and one municipal schedule per town.");
            var trades = result.Trades.Select(t => new { Owner = t.OwnerId, Port = t.PortId, Attempt = t.Trade })
                .Concat(result.WarehouseTrades.Select(t => new { Owner = t.OwnerId, Port = t.PortId, Attempt = t.Trade })).ToArray();
            foreach (var owner in after.Companies.Keys)
            {
                var fees = circulation.Commercial.Values.Where(c => c.OwnerId == owner)
                    .Aggregate(BigInteger.Zero, (sum, c) => sum + c.FeesPaid);
                if (fees != after.Accounting.Companies[owner].FeesPaid)
                    throw new InvalidOperationException("Commercial fees and company expenses do not reconcile.");
            }
            foreach (var row in circulation.Commercial.Values)
            {
                var prior = before.Commercial[CommercialCirculationSnapshot.Key(row.OwnerId, row.PortId)];
                var local = trades.Where(t => t.Owner == row.OwnerId && t.Port == row.PortId &&
                    t.Attempt.Direction == TradeDirection.Sell && t.Attempt.Outcome == TradeOutcome.Traded).ToArray();
                var gross = local.Aggregate(BigInteger.Zero, (sum, t) => sum + t.Attempt.ExecutedAmount);
                if ((BigInteger)row.GrossLocalSales - prior.GrossLocalSales != (circulation.Configuration.FeesEnabled ? gross : 0) ||
                    (BigInteger)row.FeesPaid - prior.FeesPaid != local.Sum(t => t.Attempt.ServiceFee) ||
                    (BigInteger)row.MarketFees - prior.MarketFees != local.Sum(t => t.Attempt.MarketServiceFee) ||
                    (BigInteger)row.HouseholdFees - prior.HouseholdFees != local.Sum(t => t.Attempt.HouseholdServiceFee))
                    throw new InvalidOperationException("Commercial fee prefixes disagree with committed local trades.");
            }
            foreach (var port in after.Ports.Values)
            {
                var city = port.City!;
                var town = circulation.Towns[port.Id];
                var prior = before.Towns[port.Id];
                var transfers = result.CashTransfers.Where(t => t.PortId == port.Id).ToArray();
                BigInteger Sum(CityCashTransferKind kind) => transfers.Where(t => t.Kind == kind)
                    .Aggregate(BigInteger.Zero, (sum, t) => sum + t.Amount);
                if ((BigInteger)town.TotalLevy - prior.TotalLevy != Sum(CityCashTransferKind.MunicipalLevy) ||
                    (BigInteger)town.TotalRelief - prior.TotalRelief != Sum(CityCashTransferKind.FoodRelief) ||
                    (BigInteger)circulation.MarketFeesReceived(port.Id) - before.MarketFeesReceived(port.Id) != Sum(CityCashTransferKind.ServiceFeeMarket) ||
                    (BigInteger)circulation.HouseholdFeesReceived(port.Id) - before.HouseholdFeesReceived(port.Id) != Sum(CityCashTransferKind.ServiceFeeHouseholds) ||
                    (BigInteger)city.HouseholdCash != (BigInteger)city.Configuration.InitialHouseholdCash + city.TotalWagesReceived -
                    city.TotalRetailSpend + circulation.HouseholdFeesReceived(port.Id) + town.TotalRelief - town.TotalLevy)
                    throw new InvalidOperationException("Town fiscal/service payments do not reconcile with real accounts.");
                if (town.LastLevy > CirculationConfiguration.MaximumFiscalTransfer || town.LastRelief > CirculationConfiguration.MaximumFiscalTransfer ||
                    (town.LastLevy != 0 && town.LastRelief != 0) || town.NextReviewDay <= after.Day || town.DuePayroll > town.PayrollReserve)
                    throw new InvalidOperationException("Municipal transfer bounds or schedule are invalid.");
            }
        }
    }
}
