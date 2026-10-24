using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldSnapshot
    {
        private ExpandingEconomySnapshot RequireEconomy() => Economy ?? throw new InvalidOperationException("This action requires Expanding Economy v3.");
        private RegionalShipSnapshot DevelopmentShip(string shipId)
        {
            var ship = Ships[shipId];
            if (!ship.IsPlayer) throw new InvalidOperationException("Royal services are chartered to the player company only.");
            if (ship.DockedPortId != RequireEconomy().Configuration.MintPortId)
                throw new InvalidOperationException("Deliver goods physically to the royal harbor.");
            return ship;
        }
        internal void ValidateWholeCargo(int quantity)
        {
            if (quantity <= 0 || quantity % QuantityScale != 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Ship orders require positive whole cargo units.");
        }
        private void DevelopmentOwner(string ownerId)
        {
            if (!Companies.ContainsKey(ownerId) || !Ships.Values.Any(s => s.OwnerId == ownerId && s.IsPlayer))
                throw new InvalidOperationException("Only the player company may commission royal development.");
        }
        public DevelopmentAttempt PreviewMintCommission(string ownerId)
        {
            var economy = RequireEconomy(); var config = economy.Configuration; DevelopmentOwner(ownerId);
            var outcome = economy.MintStatus != MintStatus.Uncommissioned ? DevelopmentOutcome.Exhausted :
                Companies[ownerId].Cash < config.MintCommissionCost ? DevelopmentOutcome.InsufficientFunds : DevelopmentOutcome.Started;
            var completion = checked(Day + config.MintConstructionDays);
            if (outcome == DevelopmentOutcome.Started) _ = checked(economy.TreasuryCash + config.MintCommissionCost);
            return new DevelopmentAttempt(DevelopmentKind.MintCommission, outcome, ownerId, config.MintPortId,
                "royal_mint", null, 0, config.MintCommissionCost, config.MintCommissionCost, 0, completion,
                outcome == DevelopmentOutcome.Exhausted ? "The unique public Mint is already commissioned." :
                outcome == DevelopmentOutcome.InsufficientFunds ? "Company cash cannot fund the commission." :
                "Payment funds the public treasury. The Mint is public property, excluded from Company Value.");
        }
        public DevelopmentAttempt PreviewShipCommission(string ownerId, string portId)
        {
            var economy = RequireEconomy(); var config = economy.Configuration; DevelopmentOwner(ownerId);
            if (portId != config.ShipwrightPortId) throw new InvalidOperationException("This port has no authorized shipwright.");
            var port = Ports[portId];
            var count = Ships.Values.Count(s => s.OwnerId == ownerId) + economy.ShipOrders.Count(s => s.OwnerId == ownerId);
            var outcome = count >= config.MaximumPlayerShips ? DevelopmentOutcome.FleetLimit :
                Companies[ownerId].Cash < config.ShipCost ? DevelopmentOutcome.InsufficientFunds :
                port.Inventory["planks"] < config.ShipPlanks ? DevelopmentOutcome.InsufficientStock : DevelopmentOutcome.Started;
            var completion = checked(Day + config.ShipConstructionDays);
            if (outcome == DevelopmentOutcome.Started)
            {
                _ = checked(port.MarketCash + config.ShipCost - config.ShipLaborPayment);
                _ = checked((port.City ?? throw new InvalidOperationException("Shipbuilding requires a local workforce.")).HouseholdCash + config.ShipLaborPayment);
            }
            return new DevelopmentAttempt(DevelopmentKind.ShipCommission, outcome, ownerId, portId,
                "player_ship_" + economy.NextShipNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "planks", config.ShipPlanks, config.ShipCost, 0, config.ShipLaborPayment, completion,
                outcome == DevelopmentOutcome.FleetLimit ? "The three-ship limit includes ships under construction." :
                outcome == DevelopmentOutcome.InsufficientFunds ? "Insufficient company cash for shipbuilding." :
                outcome == DevelopmentOutcome.InsufficientStock ? "The shipwright needs actual local market Planks." :
                "Payment and hull materials are charged once; the empty ship arrives after four days.");
        }
        public DevelopmentAttempt PreviewMintExchange(string shipId, int quantity = 10000)
        {
            ValidateWholeCargo(quantity); var ship = DevelopmentShip(shipId);
            var economy = RequireEconomy(); var config = economy.Configuration;
            if (quantity != config.SilverPerBatch) throw new ArgumentOutOfRangeException(nameof(quantity), "One authorized Silver batch per exchange.");
            var outcome = economy.MintStatus != MintStatus.Active || Day < economy.NextMintDay ? DevelopmentOutcome.NotReady :
                economy.MintBatches >= config.MaximumMintBatches ? DevelopmentOutcome.Exhausted :
                ship.Cargo["silver"] < quantity ? DevelopmentOutcome.InsufficientStock : DevelopmentOutcome.Completed;
            if (outcome == DevelopmentOutcome.Completed)
            {
                _ = checked(ship.Cash + config.SupplierSharePerBatch);
                _ = checked(economy.TreasuryCash + config.TreasurySharePerBatch);
                _ = checked(economy.MintedCurrency + config.CurrencyPerBatch);
                _ = checked(Day + config.MintIntervalDays);
            }
            return new DevelopmentAttempt(DevelopmentKind.MintExchange, outcome, ship.OwnerId, config.MintPortId,
                "royal_mint", "silver", quantity, config.SupplierSharePerBatch, config.TreasurySharePerBatch, 0, null,
                outcome == DevelopmentOutcome.NotReady ? "Mint construction or its batch interval has not completed." :
                outcome == DevelopmentOutcome.Exhausted ? "The finite royal issuance authorization is exhausted." :
                outcome == DevelopmentOutcome.InsufficientStock ? "Deliver one whole Silver aboard this ship." :
                "Silver enters the public reserve once; new coins go to the supplier and treasury separately.");
        }
        public DevelopmentAttempt PreviewExternalContract(string shipId, string contractId, int quantity)
        {
            ValidateWholeCargo(quantity); var ship = DevelopmentShip(shipId); var economy = RequireEconomy();
            var contract = economy.Contracts.Single(c => c.Configuration.Id == contractId); var config = contract.Configuration;
            if (ship.DockedPortId != config.PortId) throw new InvalidOperationException("Contract delivery requires its actual harbor.");
            var payment = checked(quantity * config.UnitPrice);
            var outcome = Day > config.DeadlineDay ? DevelopmentOutcome.Expired : Day < config.OpensDay ? DevelopmentOutcome.NotReady :
                quantity > contract.RemainingQuantity ? DevelopmentOutcome.Exhausted :
                ship.Cargo[config.GoodId] < quantity ? DevelopmentOutcome.InsufficientStock :
                economy.ExternalCash < payment ? DevelopmentOutcome.InsufficientFunds : DevelopmentOutcome.Completed;
            if (outcome == DevelopmentOutcome.Completed) _ = checked(ship.Cash + payment);
            return new DevelopmentAttempt(DevelopmentKind.ExternalExport, outcome, ship.OwnerId, config.PortId,
                contractId, config.GoodId, quantity, payment, 0, 0, null,
                outcome == DevelopmentOutcome.Completed ? "Actual cargo is exported to the finite external authority; this is transferred money, not issuance." :
                "Contract blocked: " + outcome + ". The external account never refills automatically.");
        }
        public DevelopmentAttempt PreviewTreasuryProcurement(string shipId, int quantity)
        {
            ValidateWholeCargo(quantity); var ship = DevelopmentShip(shipId); var economy = RequireEconomy(); var config = economy.Configuration;
            var payment = checked(quantity * config.TreasuryPlankPrice);
            var outcome = quantity > config.TreasuryPlankRequirement - economy.TreasuryPlanks ? DevelopmentOutcome.Exhausted :
                ship.Cargo["planks"] < quantity ? DevelopmentOutcome.InsufficientStock :
                economy.TreasuryCash < payment ? DevelopmentOutcome.InsufficientFunds : DevelopmentOutcome.Completed;
            if (outcome == DevelopmentOutcome.Completed) _ = checked(ship.Cash + payment);
            return new DevelopmentAttempt(DevelopmentKind.TreasuryProcurement, outcome, ship.OwnerId, config.MintPortId,
                "public_works", "planks", quantity, payment, payment, 0, null,
                outcome == DevelopmentOutcome.Completed ? "Treasury pays for delivered Planks consumed in bounded public works." : "Public procurement blocked: " + outcome + ".");
        }
    }

    public sealed partial class RegionalWorldState
    {
        public RegionalResult CommissionMint(string ownerId, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); work.CommissionMint(ownerId); });
        public RegionalResult CommissionShip(string ownerId, string portId, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); work.CommissionShip(ownerId, portId); });
        public RegionalResult MintSilver(string shipId, int quantity = 10000, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); EnsureManual(work.Ships[shipId]); work.MintSilver(shipId, quantity); });
        public RegionalResult FulfillExternalContract(string shipId, string contractId, int quantity, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); EnsureManual(work.Ships[shipId]); work.Export(shipId, contractId, quantity); });
        public RegionalResult SupplyPublicWorks(string shipId, int quantity, RegionalWorldSnapshot? expectedSnapshot = null) =>
            Mutate(work => { EnsureCurrent(expectedSnapshot); EnsureManual(work.Ships[shipId]); work.PublicWorks(shipId, quantity); });

        private void ValidateExpandingConfiguration(ExpandingEconomyConfiguration config)
        {
            if (!state.Ports.ContainsKey(config.MintPortId) || !state.Ports.ContainsKey(config.SilverPortId) ||
                state.Ports.Values.Any(p => p.City?.Configuration.QuantityScale != 10000))
                throw new ArgumentException("V3 requires scaled living ports and valid public institutions.");
            foreach (var good in content.Goods)
                if (!config.GoodsReferencePrices.ContainsKey(good.Id)) throw new ArgumentException("Missing reference price.");
            content.GetGood("silver"); content.GetBuilding("royal_mint");
            foreach (var contract in config.Contracts) { content.GetGood(contract.GoodId); _ = state.Ports[contract.PortId]; }
            foreach (var ship in state.Ships.Values)
                if (ship.Cargo.Values.Any(q => q % 10000 != 0) || ship.CargoCapacity % 10000 != 0)
                    throw new ArgumentException("Ships require whole-unit cargo and capacity.");
        }

        private sealed class WorkingEconomy
        {
            internal readonly ExpandingEconomyConfiguration Configuration;
            internal long Treasury, External, Minted, Inflow, NextMint, Silver, SupplierIssuance, TreasuryIssuance, TreasurySpent;
            internal MintStatus Status;
            internal string? Commissioner;
            internal long? Ordered, Completion;
            internal int Batches, TreasuryPlanks, NextShipNumber;
            internal readonly List<ShipConstructionOrder> Orders;
            internal readonly List<ExternalContractSnapshot> Contracts;
            internal readonly Dictionary<string, long> InitialValues;
            internal WorkingEconomy(ExpandingEconomySnapshot state)
            {
                Configuration = state.Configuration; Treasury = state.TreasuryCash; External = state.ExternalCash;
                Minted = state.MintedCurrency; Inflow = state.ExternalNetInflow; NextMint = state.NextMintDay; Silver = state.SilverReserve;
                SupplierIssuance = state.SupplierIssuance; TreasuryIssuance = state.TreasuryIssuance; TreasurySpent = state.TreasuryExpenditure;
                Status = state.MintStatus; Commissioner = state.MintCommissionerId; Ordered = state.MintOrderedDay; Completion = state.MintCompletionDay;
                Batches = state.MintBatches; TreasuryPlanks = state.TreasuryPlanks; NextShipNumber = state.NextShipNumber;
                Orders = state.ShipOrders.ToList(); Contracts = state.Contracts.ToList();
                InitialValues = state.InitialCompanyValues.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            }
            internal ExpandingEconomySnapshot Snapshot() => new ExpandingEconomySnapshot(Configuration, Treasury, External,
                Minted, Inflow, Status, Commissioner, Ordered, Completion, NextMint, Batches, Silver, SupplierIssuance,
                TreasuryIssuance, TreasuryPlanks, TreasurySpent, NextShipNumber, Orders, Contracts, InitialValues);
        }

        private sealed partial class WorkingWorld
        {
            internal WorkingEconomy? Economy { get; }
            internal List<DevelopmentAttempt> Development { get; } = new List<DevelopmentAttempt>();
            internal void CommissionMint(string ownerId)
            {
                var attempt = View().PreviewMintCommission(ownerId); Development.Add(attempt);
                if (attempt.Outcome != DevelopmentOutcome.Started) return;
                var economy = Economy!;
                Companies[ownerId].Cash = checked(Companies[ownerId].Cash - attempt.Payment);
                economy.Treasury = checked(economy.Treasury + attempt.Payment);
                economy.Status = MintStatus.UnderConstruction; economy.Ordered = Day; economy.Completion = attempt.CompletionDay;
                economy.Commissioner = ownerId;
                Accounting.PublicCommission(ownerId, attempt.PortId, attempt.Payment);
                Changed = true;
            }
            internal void CommissionShip(string ownerId, string portId)
            {
                var attempt = View().PreviewShipCommission(ownerId, portId); Development.Add(attempt);
                if (attempt.Outcome != DevelopmentOutcome.Started) return;
                var economy = Economy!; var port = Ports[portId];
                if (Ships.ContainsKey(attempt.AssetId!) || economy.Orders.Any(o => o.ShipId == attempt.AssetId))
                    throw new InvalidOperationException("Commissioned ship ID already exists.");
                Companies[ownerId].Cash = checked(Companies[ownerId].Cash - attempt.Payment);
                port.Cash = checked(port.Cash + attempt.Payment - attempt.HouseholdAmount);
                port.City!.ReceiveWage(attempt.HouseholdAmount);
                port.Inventory.Commit(new Dictionary<string, int> { ["planks"] = checked(port.Inventory.GetQuantity("planks") - attempt.Quantity) });
                CashTransfers.Add(new CityCashTransfer(CityCashTransferKind.Wage, ownerId, "households:" + portId, portId,
                    attempt.HouseholdAmount, attempt.AssetId));
                economy.Orders.Add(new ShipConstructionOrder(attempt.AssetId!, ownerId, portId, Day, attempt.CompletionDay!.Value, attempt.Payment));
                economy.NextShipNumber = checked(economy.NextShipNumber + 1);
                Accounting.Construct(ownerId, attempt.AssetId!, portId, attempt.Payment);
                Changed = true;
            }
            internal void MintSilver(string shipId, int quantity)
            {
                var attempt = View().PreviewMintExchange(shipId, quantity); Development.Add(attempt);
                if (attempt.Outcome != DevelopmentOutcome.Completed) return;
                var economy = Economy!; var config = economy.Configuration; var ship = Ships[shipId];
                RemoveDevelopmentCargo(ship, "silver", quantity);
                Companies[attempt.OwnerId].Cash = checked(Companies[attempt.OwnerId].Cash + attempt.Payment);
                economy.Treasury = checked(economy.Treasury + attempt.TreasuryAmount);
                economy.Minted = checked(economy.Minted + config.CurrencyPerBatch);
                economy.SupplierIssuance = checked(economy.SupplierIssuance + attempt.Payment);
                economy.TreasuryIssuance = checked(economy.TreasuryIssuance + attempt.TreasuryAmount);
                economy.Silver = checked(economy.Silver + quantity); economy.Batches = checked(economy.Batches + 1);
                economy.NextMint = checked(Day + config.MintIntervalDays);
                Accounting.Mint("ship:" + shipId, attempt.PortId, quantity, attempt.Payment);
                Changed = true;
            }
            internal void Export(string shipId, string contractId, int quantity)
            {
                var attempt = View().PreviewExternalContract(shipId, contractId, quantity); Development.Add(attempt);
                if (attempt.Outcome != DevelopmentOutcome.Completed) return;
                var economy = Economy!; var index = economy.Contracts.FindIndex(c => c.Configuration.Id == contractId);
                var contract = economy.Contracts[index]; RemoveDevelopmentCargo(Ships[shipId], attempt.GoodId!, quantity);
                economy.External = checked(economy.External - attempt.Payment); economy.Inflow = checked(economy.Inflow + attempt.Payment);
                Companies[attempt.OwnerId].Cash = checked(Companies[attempt.OwnerId].Cash + attempt.Payment);
                economy.Contracts[index] = new ExternalContractSnapshot(contract.Configuration,
                    checked(contract.DeliveredQuantity + quantity), checked(contract.AmountPaid + attempt.Payment));
                Accounting.SellToAccount("ship:" + shipId, attempt.PortId, attempt.GoodId!, quantity, attempt.Payment, "external_authority");
                Changed = true;
            }
            internal void PublicWorks(string shipId, int quantity)
            {
                var attempt = View().PreviewTreasuryProcurement(shipId, quantity); Development.Add(attempt);
                if (attempt.Outcome != DevelopmentOutcome.Completed) return;
                var economy = Economy!; RemoveDevelopmentCargo(Ships[shipId], "planks", quantity);
                economy.Treasury = checked(economy.Treasury - attempt.Payment);
                economy.TreasurySpent = checked(economy.TreasurySpent + attempt.Payment);
                economy.TreasuryPlanks = checked(economy.TreasuryPlanks + quantity);
                Companies[attempt.OwnerId].Cash = checked(Companies[attempt.OwnerId].Cash + attempt.Payment);
                Accounting.SellToAccount("ship:" + shipId, attempt.PortId, "planks", quantity, attempt.Payment, "royal_treasury");
                Changed = true;
            }
            private void RemoveDevelopmentCargo(WorkingShip ship, string goodId, int quantity)
            {
                ship.Cargo.Commit(new Dictionary<string, int> { [goodId] = checked(ship.Cargo.GetQuantity(goodId) - quantity) });
                ship.CargoUnits = checked(ship.CargoUnits - quantity);
            }
            internal void CompleteDevelopment(long nextDay)
            {
                if (Economy == null) return;
                var economy = Economy; var config = economy.Configuration;
                if (economy.Status == MintStatus.UnderConstruction && nextDay >= economy.Completion!.Value)
                {
                    economy.Status = MintStatus.Active; economy.NextMint = nextDay;
                    Development.Add(new DevelopmentAttempt(DevelopmentKind.MintActivation, DevelopmentOutcome.Completed,
                        economy.Commissioner!, config.MintPortId, "royal_mint", null, 0, 0, 0, 0, nextDay,
                        "Public Mint activated; issuance still requires actual Silver delivery."));
                }
                foreach (var order in economy.Orders.OrderBy(o => o.ShipId, StringComparer.Ordinal).ToArray())
                {
                    if (order.CompletionDay > nextDay) continue;
                    var shipConfig = CreateCommissionedShipConfiguration(config, order.OwnerId, order.ShipId);
                    var snapshot = new RegionalShipSnapshot(shipConfig,
                        new ShipSnapshot(order.ShipId, config.ShipCapacity, new Inventory(content).Snapshot(), order.PortId, null, null, 0),
                        Companies[order.OwnerId].Cash, null, false, 0);
                    Ships.Add(order.ShipId, new WorkingShip(content, snapshot, before.QuantityScale));
                    if (order.OwnerId == Challenge?.Configuration.OwnerId)
                        Commission?.Visit(order.ShipId, order.PortId, nextDay);
                    Accounting.RegisterStore("ship:" + order.ShipId, order.OwnerId);
                    economy.Orders.Remove(order);
                    Development.Add(new DevelopmentAttempt(DevelopmentKind.ShipCompletion, DevelopmentOutcome.Completed,
                        order.OwnerId, order.PortId, order.ShipId, null, 0, 0, 0, 0, nextDay,
                        "Empty owned ship delivered at the shipwright; no second payment."));
                }
            }
        }
    }
}
