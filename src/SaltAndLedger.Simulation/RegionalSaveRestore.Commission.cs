using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    internal static partial class RegionalSaveRestore
    {
        internal static GrandCommissionPlan? RestoreCommissionPlan(RegionalSaveData data)
        {
            var saved = data.Identity.Commission;
            if (data.SchemaVersion == WorldSaveCodec.SchemaVersion)
            {
                Require(saved == null && data.CommissionHistory == null,
                    "Schema 1 cannot contain drafted commission state.");
                return null;
            }
            if (data.SchemaVersion == WorldSaveCodec.PooledStorageSchemaVersion && saved == null)
            {
                Require(data.CommissionHistory == null, "Commission history requires a resolved commission plan.");
                return null;
            }
            Require(saved != null && data.CommissionHistory != null,
                "A drafted save requires a complete commission plan and history.");
            Require(data.Identity.WorldMode == RunWorldMode.ExpandingEconomy &&
                data.Identity.Seed == saved!.Seed, "Commission belongs to a different world.");
            Count(saved!.Goals, 4, 4);
            var plan = GrandCommissionDraft.Generate(saved.Seed, saved.Version)
                .Resolve(saved.Goals.Select(g => g.TemplateId));
            Require(saved.Signature == plan.Signature, "Commission signature does not match the known draft.");
            for (var index = 0; index < plan.Goals.Count; index++)
            {
                var goal = plan.Goals[index];
                var value = saved.Goals[index];
                Require(value.Id == goal.Id && value.TemplateId == goal.TemplateId &&
                    value.CanonicalParameters == goal.CanonicalParameters,
                    "Commission resolved parameters do not match the known draft.");
            }
            return plan;
        }

        private static CommissionHistory? RestoreCommissionHistory(ContentCatalog content,
            RegionalWorldSnapshot initial, RegionalSaveData data, RegionalShipSnapshot[] ships,
            ExpandingEconomySnapshot? economy)
        {
            var history = data.CommissionHistory;
            Require((initial.CommissionHistory == null) == (history == null), "Commission history presence changed.");
            if (history == null) return null;
            Require(economy != null && initial.RunIdentity?.Commission != null,
                "Commission history requires an expanding run and its plan.");
            Count(history.Ships, 1, 3);
            Count(history.Markets, 0, 28);
            var owner = initial.Challenge!.Configuration.OwnerId;
            var owned = ships.Where(s => s.IsPlayer && s.OwnerId == owner).ToArray();
            SameIds(history.Ships.Select(s => s.ShipId), owned.Select(s => s.Id), "commission ships");
            Require(history.Ships.Select(s => s.AcquisitionOrder).OrderBy(n => n)
                .SequenceEqual(Enumerable.Range(0, owned.Length)), "Invalid ship acquisition order.");
            var initialShip = initial.Ships.Values.Single(s => s.IsPlayer && s.OwnerId == owner);
            long previousAcquisitionDay = -1;
            var restoredShips = new List<CommissionShipVisits>();
            foreach (var saved in history.Ships.OrderBy(s => s.AcquisitionOrder))
            {
                var ship = owned.Single(s => s.Id == saved.ShipId);
                Require(saved.Visits != null && saved.Visits.Count >= 1 && saved.Visits.Count <= initial.Ports.Count &&
                    saved.Visits.Keys.All(initial.Ports.ContainsKey), "Invalid bounded ship visits.");
                var visits = saved.Visits!.OrderBy(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).ToArray();
                var original = saved.ShipId == initialShip.Id;
                var firstPort = original ? initialShip.DockedPortId! : economy!.Configuration.ShipwrightPortId;
                var minimumAcquisitionDay = original ? 0 : economy!.Configuration.ShipConstructionDays;
                Require(saved.Visits!.ContainsKey(firstPort) && visits[0].Key == firstPort &&
                    visits[0].Value >= minimumAcquisitionDay && visits[0].Value >= previousAcquisitionDay &&
                    (original ? saved.AcquisitionOrder == 0 && visits[0].Value == 0 :
                        saved.ShipId == "player_ship_" + (saved.AcquisitionOrder + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    "Ship initial dock or acquisition history is inconsistent.");
                previousAcquisitionDay = visits[0].Value;
                for (var index = 0; index < visits.Length; index++)
                {
                    var visit = visits[index];
                    Require(visit.Value >= 0 && visit.Value <= data.Day, "Ship visit is outside the saved timeline.");
                    if (index == 0) continue;
                    var previous = visits[index - 1];
                    Require(visit.Value - previous.Value >= MinimumTravelDays(initial, ship, previous.Key, visit.Key),
                        "Ship visits cannot occur before a real voyage can arrive.");
                }
                var currentPort = ship.DockedPortId ?? ship.OriginPortId!;
                var departureDay = ship.DockedPortId != null ? data.Day :
                    checked(data.Day - (ship.VoyageDurationDays - ship.RemainingTravelDays));
                Require(saved.Visits.ContainsKey(currentPort) && saved.Visits[currentPort] <= departureDay &&
                    departureDay - visits[visits.Length - 1].Value >=
                        MinimumTravelDays(initial, ship, visits[visits.Length - 1].Key, currentPort),
                    "Ship location disagrees with its actual visit history.");
                restoredShips.Add(new CommissionShipVisits(saved.ShipId, saved.AcquisitionOrder, saved.Visits));
            }
            Unique(history.Markets.Select(m => m.PortId + ":" + m.GoodId), "commission market balances");
            var balances = history.Markets.Select(m =>
            {
                Require(initial.Ports.ContainsKey(m.PortId), "Unknown commission target market.");
                content.GetGood(m.GoodId);
                Nonnegative(m.PurchasedQuantity, m.SoldQuantity);
                Require(m.PurchasedQuantity > 0 || m.SoldQuantity > 0, "Empty commission market balance.");
                return new CommissionMarketBalance(m.PortId, m.GoodId, m.PurchasedQuantity, m.SoldQuantity);
            }).ToArray();
            var financial = data.Companies.Single(c => c.Id == owner);
            var privateSites = data.Facilities.Where(f => f.OwnerId == owner).ToArray();
            foreach (var facility in privateSites)
            {
                var interval = initial.IndustryBlueprints[facility.BlueprintId].IntervalDays;
                Require(facility.Batches <= Math.Max(0, data.Day - facility.CompletionDay) / interval,
                    "Commission private production precedes its funded batch schedule.");
            }
            Require(privateSites.Aggregate(BigInteger.Zero, (sum, f) => sum + f.Wages) == financial.Wages,
                "Commission payroll does not reconcile with actual private batches.");
            foreach (var good in content.Goods)
            {
                Require(privateSites.Aggregate(BigInteger.Zero, (sum, f) => sum + Get(f.Produced, good.Id)) == Get(financial.Produced, good.Id) &&
                    privateSites.Aggregate(BigInteger.Zero, (sum, f) => sum + Get(f.Consumed, good.Id)) == Get(financial.Consumed, good.Id),
                    "Commission private batch history does not reconcile with company physical flows.");
                var purchases = balances.Where(m => m.GoodId == good.Id)
                    .Aggregate(BigInteger.Zero, (sum, m) => sum + m.PurchasedQuantity);
                var sales = balances.Where(m => m.GoodId == good.Id)
                    .Aggregate(BigInteger.Zero, (sum, m) => sum + m.SoldQuantity);
                var publicSales = economy!.Contracts.Where(c => c.Configuration.GoodId == good.Id)
                    .Aggregate(BigInteger.Zero, (sum, c) => sum + c.DeliveredQuantity);
                if (good.Id == "planks") publicSales += economy.TreasuryPlanks;
                Require(purchases == Get(financial.Purchased, good.Id) &&
                    sales + publicSales == Get(financial.Sold, good.Id),
                    "Commission market history does not reconcile with company purchases and sales.");
            }
            return new CommissionHistory(restoredShips, balances);
        }

        // Four nodes only: use shortest travel time, rather than assuming authored direct distances
        // obey triangle inequalities. First visits are ordered observations, not a complete voyage log.
        private static long MinimumTravelDays(RegionalWorldSnapshot world, RegionalShipSnapshot ship,
            string origin, string destination)
        {
            var ports = world.Ports.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var distances = new long[ports.Length, ports.Length];
            for (var from = 0; from < ports.Length; from++)
                for (var to = 0; to < ports.Length; to++)
                    distances[from, to] = from == to ? 0 : world.GetRoute(ports[from], ports[to]).DurationDays(ship.Configuration.Speed);
            for (var via = 0; via < ports.Length; via++)
                for (var from = 0; from < ports.Length; from++)
                    for (var to = 0; to < ports.Length; to++)
                        distances[from, to] = Math.Min(distances[from, to], distances[from, via] + distances[via, to]);
            return distances[Array.IndexOf(ports, origin), Array.IndexOf(ports, destination)];
        }
    }
}
