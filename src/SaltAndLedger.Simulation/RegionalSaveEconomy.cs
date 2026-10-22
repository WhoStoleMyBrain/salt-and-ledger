using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldState
    {
        internal static RegionalShipConfiguration CreateCommissionedShipConfiguration(
            ExpandingEconomyConfiguration config, string ownerId, string shipId) =>
            new RegionalShipConfiguration(new ShipConfiguration(shipId, config.ShipCapacity, config.ShipwrightPortId),
                ownerId, "Company ship " + shipId.Substring(shipId.LastIndexOf('_') + 1),
                0, config.ShipSpeed, isPlayer: true);
    }

    internal static partial class RegionalSaveRestore
    {
        private static ExpandingEconomySnapshot? RestoreEconomy(RegionalWorldSnapshot initial, RegionalSaveData data)
        {
            var e = data.Economy;
            Require((e == null) == (initial.Economy == null), "Public monetary sector presence changed.");
            if (e == null || initial.Economy == null) return null;
            var config = initial.Economy.Configuration;
            Nonnegative(e.TreasuryCash, e.ExternalCash, e.MintedCurrency, e.ExternalNetInflow, e.NextMintDay,
                e.MintBatches, e.SilverReserve, e.SupplierIssuance, e.TreasuryIssuance, e.TreasuryPlanks, e.TreasuryExpenditure);
            Defined(e.MintStatus);
            Require(e.MintBatches <= config.MaximumMintBatches &&
                (BigInteger)e.MintedCurrency == (BigInteger)e.MintBatches * config.CurrencyPerBatch &&
                (BigInteger)e.SupplierIssuance == (BigInteger)e.MintBatches * config.SupplierSharePerBatch &&
                (BigInteger)e.TreasuryIssuance == (BigInteger)e.MintBatches * config.TreasurySharePerBatch &&
                e.SilverReserve == (long)e.MintBatches * config.SilverPerBatch, "Silver reserve and issuance do not reconcile.");
            if (e.MintStatus == MintStatus.Uncommissioned)
                Require(e.MintCommissionerId == null && e.MintOrderedDay == null && e.MintCompletionDay == null &&
                    e.NextMintDay == 0 && e.MintBatches == 0, "Uncommissioned Mint has an active schedule or issuance.");
            else
            {
                Require(e.MintCommissionerId != null && initial.Ships.Values.Any(s => s.IsPlayer && s.OwnerId == e.MintCommissionerId),
                    "Invalid Mint commissioner.");
                Require(e.MintOrderedDay >= 0 && e.MintOrderedDay <= data.Day &&
                    e.MintCompletionDay == checked(e.MintOrderedDay + config.MintConstructionDays), "Invalid Mint commission dates.");
                Require((e.MintStatus == MintStatus.Active) == (e.MintCompletionDay <= data.Day), "Invalid Mint lifecycle.");
                if (e.MintStatus == MintStatus.UnderConstruction)
                    Require(e.MintBatches == 0 && e.NextMintDay == 0, "Pending Mint already issued currency.");
                else Require(e.NextMintDay >= e.MintCompletionDay && e.NextMintDay <= checked(data.Day + config.MintIntervalDays),
                    "Invalid next Mint date.");
            }
            Require(e.TreasuryPlanks <= config.TreasuryPlankRequirement && e.TreasuryPlanks % initial.QuantityScale == 0 &&
                (BigInteger)e.TreasuryExpenditure == (BigInteger)e.TreasuryPlanks * config.TreasuryPlankPrice,
                "Treasury procurement does not reconcile.");
            var commission = e.MintStatus == MintStatus.Uncommissioned ? 0 : config.MintCommissionCost;
            Require((BigInteger)e.TreasuryCash == (BigInteger)config.InitialTreasuryCash + commission +
                e.TreasuryIssuance - e.TreasuryExpenditure, "Treasury cash does not reconcile.");
            Count(e.Contracts, 0, 16);
            SameIds(e.Contracts.Select(c => c.Id), config.Contracts.Select(c => c.Id), "external contracts");
            var contracts = e.Contracts.Select(c =>
            {
                var definition = config.Contracts.Single(d => d.Id == c.Id);
                Require(c.Delivered >= 0 && c.Delivered <= definition.Quantity && c.Delivered % initial.QuantityScale == 0 &&
                    (BigInteger)c.Paid == (BigInteger)c.Delivered * definition.UnitPrice, "External delivery/payment is invalid.");
                Require(c.Delivered == 0 || data.Day >= definition.OpensDay, "Contract was fulfilled before it opened.");
                return new ExternalContractSnapshot(definition, c.Delivered, c.Paid);
            }).ToArray();
            Require(contracts.Aggregate(BigInteger.Zero, (sum, c) => sum + c.AmountPaid) == e.ExternalNetInflow &&
                (BigInteger)e.ExternalCash + e.ExternalNetInflow == config.InitialExternalCash, "External authority funding does not reconcile.");
            Count(e.ShipOrders, 0, config.MaximumPlayerShips - 1);
            Unique(e.ShipOrders.Select(o => o.ShipId), "ship orders");
            Require(e.NextShipNumber >= 2 && e.NextShipNumber <= config.MaximumPlayerShips + 1, "Invalid ship sequence.");
            var orders = e.ShipOrders.Select(o =>
            {
                Require(initial.Ships.Values.Any(s => s.IsPlayer && s.OwnerId == o.OwnerId) &&
                    o.PortId == config.ShipwrightPortId && o.OrderedDay >= 0 && o.OrderedDay <= data.Day &&
                    o.CompletionDay == checked(o.OrderedDay + config.ShipConstructionDays) && o.CompletionDay > data.Day &&
                    o.PaidCapital == config.ShipCost, "Invalid ship commission.");
                return new ShipConstructionOrder(o.ShipId, o.OwnerId, o.PortId, o.OrderedDay, o.CompletionDay, o.PaidCapital);
            }).ToArray();
            return new ExpandingEconomySnapshot(config, e.TreasuryCash, e.ExternalCash, e.MintedCurrency,
                e.ExternalNetInflow, e.MintStatus, e.MintCommissionerId, e.MintOrderedDay, e.MintCompletionDay,
                e.NextMintDay, e.MintBatches, e.SilverReserve, e.SupplierIssuance, e.TreasuryIssuance,
                e.TreasuryPlanks, e.TreasuryExpenditure, e.NextShipNumber, orders, contracts,
                initial.Economy.InitialCompanyValues.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        }

        private static IReadOnlyDictionary<string, RegionalShipConfiguration> RestoreShipConfigurations(
            RegionalWorldSnapshot initial, RegionalSaveData data, ExpandingEconomySnapshot? economy)
        {
            Unique(data.Ships.Select(s => s.Id), "ships");
            var configs = initial.Ships.Values.ToDictionary(s => s.Id, s => s.Configuration, StringComparer.Ordinal);
            if (economy == null)
            {
                SameIds(data.Ships.Select(s => s.Id), configs.Keys, "ships");
                return configs;
            }
            var owner = initial.Ships.Values.Single(s => s.IsPlayer).OwnerId;
            var completed = new List<string>();
            for (var number = 2; number < economy.NextShipNumber; number++)
            {
                var id = "player_ship_" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var pending = economy.ShipOrders.Any(o => o.ShipId == id);
                var delivered = data.Ships.Any(s => s.Id == id);
                Require(pending != delivered, "A paid ship must be either pending or delivered, once.");
                if (!pending)
                {
                    completed.Add(id);
                    configs.Add(id, RegionalWorldState.CreateCommissionedShipConfiguration(economy.Configuration, owner, id));
                }
            }
            Require(economy.ShipOrders.All(o => Enumerable.Range(2, economy.NextShipNumber - 2).Any(n =>
                o.ShipId == "player_ship_" + n.ToString(System.Globalization.CultureInfo.InvariantCulture))), "Unknown pending ship ID.");
            SameIds(data.Ships.Select(s => s.Id), configs.Keys, "ships");
            Require(configs.Values.Count(s => s.IsPlayer) + economy.ShipOrders.Count <= economy.Configuration.MaximumPlayerShips,
                "Saved fleet exceeds the player ship cap.");
            var finance = data.Companies.Single(c => c.Id == owner);
            var facilityCapital = data.Facilities.Where(f => f.OwnerId == owner && !f.StarterGrant)
                .Aggregate(BigInteger.Zero, (sum, f) => sum + initial.IndustryBlueprints[f.BlueprintId].ConstructionCost);
            Require((BigInteger)finance.Construction == facilityCapital +
                (BigInteger)(economy.NextShipNumber - 2) * economy.Configuration.ShipCost, "Paid private capital does not reconcile with sites and ships.");
            Require(finance.PublicCommissionExpenditure == (economy.MintStatus == MintStatus.Uncommissioned ? 0 : economy.Configuration.MintCommissionCost) &&
                finance.MintReceipts == economy.SupplierIssuance &&
                Get(finance.SurrenderedQuantities, "silver") == economy.SilverReserve, "Company monetary accounting does not reconcile with public institutions.");
            return configs;
        }
    }
}
