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
                    ? after.Ships[store.StoreId.Substring(5)].Cargo : after.PrivateFacilities[store.StoreId.Substring(9)].Inventory;
                foreach (var good in inventory)
                    if (store.GetQuantity(good.Key) != good.Value)
                        throw new InvalidOperationException("Physical store and FIFO slices disagree.");
            }
        }

        private static BigInteger Physical(RegionalWorldSnapshot world, string goodId) =>
            world.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.Inventory[goodId]) +
            world.Ships.Values.Aggregate(BigInteger.Zero, (sum, s) => sum + s.Cargo[goodId]) +
            world.PrivateFacilities.Values.Aggregate(BigInteger.Zero, (sum, f) => sum + f.Inventory[goodId]) +
            (goodId == "silver" ? world.Economy?.SilverReserve ?? 0 : 0);
    }
}
