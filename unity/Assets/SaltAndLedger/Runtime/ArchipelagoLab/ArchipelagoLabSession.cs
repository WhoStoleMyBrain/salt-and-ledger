using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    public sealed class ArchipelagoActivity
    {
        public long Day { get; }
        public string PortId { get; }
        public string ShipId { get; }
        public string Message { get; }

        internal ArchipelagoActivity(long day, string portId, string shipId, string message)
        {
            Day = day;
            PortId = portId;
            ShipId = shipId;
            Message = message;
        }
    }

    /// <summary>
    /// Main-thread host choices and bounded observation. Elapsed presentation time only
    /// requests whole days; all economic changes are made by RegionalWorldState.
    /// </summary>
    public sealed class ArchipelagoLabSession
    {
        public const int MaximumActivity = 60;
        public const int MaximumDaysPerFrame = 4;
        public const int MaximumJournalEntries = 200;
        public const double SecondsPerDay = 1.5;

        private RegionalWorldState world;
        private readonly ContentCatalog baseContent;
        private readonly List<ArchipelagoActivity> activity = new List<ArchipelagoActivity>();
        private readonly ReadOnlyCollection<ArchipelagoActivity> activityView;
        private readonly Dictionary<string, string> lastReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        private double accumulatedSeconds;
        private long initialPlayerCash;
        private bool hasPlayerTrade;
        private readonly bool legacyScenario;
        private bool configuredRun;
        private readonly List<EconomicEvent> journal = new List<EconomicEvent>();
        private readonly SortedDictionary<string, long> shipDeliveries = new SortedDictionary<string, long>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, long> ShipDeliveries => new ReadOnlyDictionary<string, long>(shipDeliveries);
        public string CharterId { get; private set; } = RunScenario.MerchantCharter;
        public string ChallengeId { get; private set; }
        public RunIdentity Identity { get; private set; }
        public bool HasConfiguredRun => configuredRun;
        public long PlayerInputs { get; private set; }
        public long AutomatedActions { get; private set; }
        public IReadOnlyList<EconomicEvent> Journal => journal.AsReadOnly();
        public bool ChallengeMode { get; private set; }
        public bool IsChallengeEnded => Snapshot.Challenge != null && Snapshot.Challenge.Status != ChallengeStatus.Active;

        public ContentCatalog Content { get; private set; }
        public ContentCatalog CanonicalContent => baseContent;
        public RegionalWorldSnapshot Snapshot => world.Snapshot();
        public string ActivePlayerShipId { get; private set; } = ArchipelagoScenario.PlayerShipId;
        public RegionalShipSnapshot Player => Snapshot.Ships[ActivePlayerShipId];
        public IReadOnlyList<RegionalShipSnapshot> OwnedShips => Array.AsReadOnly(Snapshot.Ships.Values
            .Where(s => s.IsPlayer && s.OwnerId == Player.OwnerId).OrderBy(s => s.Id, StringComparer.Ordinal).ToArray());
        public long? LastSavedRevision { get; private set; }
        public bool HasUnsavedChanges => configuredRun && LastSavedRevision != Snapshot.Revision;
        public TradeRoutePolicy Policy => Player.Policy ?? RecommendedMerchantPolicy();
        public RegionalDecision CurrentDecision => world.EvaluateAutomation(Player.Id);
        public IReadOnlyList<ArchipelagoActivity> Activity => activityView;
        public string SelectedPortId { get; private set; }
        public string SelectedShipId { get; private set; }
        public string PendingDestinationId { get; private set; }
        public bool NavigationArmed { get; private set; }
        public bool IsPlaying { get; private set; }
        public int PlaybackSpeed { get; private set; } = 1;
        public double PlaybackInterval => SecondsPerDay / PlaybackSpeed;
        public string LastStatus { get; private set; }
        public long CashChange => checked(Player.Cash - initialPlayerCash);
        // Compatibility for the original shipping-only lab. V3 cash includes capital and public
        // monetary receipts; its UI uses attributed material margin and operating surplus instead.
        public long? ClosedCashProfit => Snapshot.Economy == null && hasPlayerTrade &&
            OwnedShips.All(ship => ship.CargoUnits == 0) && Snapshot.PrivateFacilities.Count == 0 ? CashChange : (long?)null;

        public ArchipelagoLabSession(ContentCatalog content, bool challenge = false, bool legacyScenario = false)
        {
            baseContent = content ?? throw new ArgumentNullException(nameof(content));
            Content = baseContent;
            this.legacyScenario = legacyScenario;
            ChallengeMode = challenge;
            activityView = activity.AsReadOnly();
            Reset();
        }

        public void Reset()
        {
            var replacement = configuredRun ? RunScenario.Create(baseContent, Identity) :
                legacyScenario ? ArchipelagoScenario.Create(baseContent) : EnterpriseScenario.Create(baseContent, ChallengeMode);
            InstallWorld(replacement, false);
        }

        private void InstallWorld(RegionalWorldState replacement, bool loaded)
        {
            world = replacement;
            Content = replacement.Snapshot().RunIdentity?.WorldMode == RunWorldMode.ExpandingEconomy
                ? baseContent.ForExpandingEconomy() : baseContent;
            ActivePlayerShipId = ArchipelagoScenario.PlayerShipId;
            initialPlayerCash = Snapshot.Accounting.Companies[Player.OwnerId].InitialCash;
            hasPlayerTrade = Snapshot.Accounting.Companies[Player.OwnerId].SalesRevenue > 0 ||
                Snapshot.Accounting.Companies[Player.OwnerId].InventoryPurchases > 0;
            LastSavedRevision = loaded ? Snapshot.Revision : (long?)null;
            SelectedPortId = Player.DockedPortId ?? ArchipelagoScenario.North;
            SelectedShipId = null;
            PendingDestinationId = null;
            NavigationArmed = false;
            IsPlaying = false;
            PlaybackSpeed = 1;
            accumulatedSeconds = 0;
            activity.Clear();
            journal.Clear();
            shipDeliveries.Clear();
            PlayerInputs = 0;
            AutomatedActions = 0;
            lastReasons.Clear();
            LastStatus = loaded ? "Saved run restored exactly on day " + Snapshot.Day + ". Playback is paused." :
                "New archipelago. Paused on day 0. Select a port to inspect, or your ship to plan a voyage.";
            Add(null, null, LastStatus);
        }

        public void StartScenario(bool challenge)
        {
            configuredRun = false;
            Identity = null;
            ChallengeMode = challenge;
            Reset();
        }

        public void StartRun(string challengeId, string charterId) =>
            StartRun(new RunIdentity(challengeId, charterId));

        public void StartRun(RunIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            var replacement = RunScenario.Create(baseContent, identity);
            Identity = identity;
            CharterId = identity.CharterId;
            ChallengeId = identity.ChallengeId;
            ChallengeMode = identity.ChallengeId != null;
            configuredRun = true;
            InstallWorld(replacement, false);
        }

        public string SaveText() => WorldSaveCodec.Save(world);

        public void MarkSaved()
        {
            if (!configuredRun) throw new InvalidOperationException("Start a configured run before saving.");
            LastSavedRevision = Snapshot.Revision;
            LastStatus = "Saved locally on day " + Snapshot.Day + ". This run can be continued from its saved revision.";
        }

        public void LoadSaveText(string json)
        {
            // Parsing and all semantic checks finish before even host selection or playback changes.
            var replacement = WorldSaveCodec.Load(baseContent, json);
            var identity = replacement.Snapshot().RunIdentity;
            Identity = identity;
            CharterId = identity.CharterId;
            ChallengeId = identity.ChallengeId;
            ChallengeMode = identity.ChallengeId != null;
            configuredRun = true;
            InstallWorld(replacement, true);
        }

        public RegionalResult AbandonRun()
        {
            Pause();
            var result = world.AbandonChallenge();
            Observe(result);
            LastStatus = "Run abandoned. The committed assets and accounts remain available in results.";
            return result;
        }

        public void ApplyIndustrialPolicy(IndustrialRoutePolicy policy, bool enabled)
        {
            var result = world.ConfigureIndustrialAutomation(Player.Id, policy, enabled);
            Observe(result);
            LastStatus = "Industrial shipping applied. Goods move only through your docked ship and actual market trades.";
        }

        public void InspectTradePort(string portId)
        {
            var port = Snapshot.Ports[portId];
            SelectedPortId = port.Id;
            PendingDestinationId = null;
            Pause();
        }

        public void SailTo(string portId)
        {
            SelectShip(Player.Id);
            SelectPort(portId);
            Sail();
        }

        public void SelectPort(string portId)
        {
            var port = Snapshot.Ports[portId];
            SelectedPortId = port.Id;
            SelectedShipId = null;
            PendingDestinationId = null;
            if (NavigationArmed && Player.DockedPortId != null && Player.DockedPortId != portId &&
                HasRoute(Player.DockedPortId, portId))
                PendingDestinationId = portId;
            LastStatus = "Inspecting " + port.Name + ". Selection spends no day.";
        }

        public void SelectShip(string shipId)
        {
            var ship = Snapshot.Ships[shipId];
            SelectedShipId = ship.Id;
            if (ship.IsPlayer && ship.OwnerId == Snapshot.Ships[ArchipelagoScenario.PlayerShipId].OwnerId)
                ActivePlayerShipId = ship.Id;
            NavigationArmed = ship.IsPlayer;
            PendingDestinationId = null;
            if (ship.DockedPortId != null) SelectedPortId = ship.DockedPortId;
            LastStatus = ship.IsPlayer
                ? "Your ship selected. Right-click a town to trade; left-click a destination to plan a voyage."
                : "Inspecting " + ship.Name + ". NPC ships use their own accounts and policies.";
        }

        public bool HasRoute(string from, string to) => Snapshot.Routes.Any(route =>
            route.OriginPortId == from && route.DestinationPortId == to ||
            route.OriginPortId == to && route.DestinationPortId == from);

        public void Play()
        {
            if (IsChallengeEnded) throw new InvalidOperationException("The charter has ended. Start a new charter or sandbox.");
            IsPlaying = true;
            accumulatedSeconds = 0;
            LastStatus = "Playback running. Each whole day resolves merchants, production, consumption and voyages.";
        }

        public void Pause()
        {
            IsPlaying = false;
            accumulatedSeconds = 0;
            LastStatus = "Playback paused. Dockside trades and configuration spend no day.";
        }

        public void SetPlaybackSpeed(int speed)
        {
            if (speed != 1 && speed != 3 && speed != 10)
                throw new ArgumentOutOfRangeException(nameof(speed), "Choose 1, 3 or 10.");
            PlaybackSpeed = speed;
            accumulatedSeconds = 0;
        }

        public int AdvancePlayback(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (!IsPlaying) return 0;
            // A stalled renderer never leaves an unlimited backlog of economic commands.
            accumulatedSeconds = Math.Min(accumulatedSeconds + elapsedSeconds, PlaybackInterval * MaximumDaysPerFrame);
            var days = 0;
            while (IsPlaying && accumulatedSeconds >= PlaybackInterval && days < MaximumDaysPerFrame)
            {
                accumulatedSeconds -= PlaybackInterval;
                AdvanceDay();
                days++;
            }
            return days;
        }

        public RegionalResult NextDay()
        {
            Pause();
            return AdvanceDay();
        }

        private RegionalResult AdvanceDay()
        {
            var result = world.AdvanceDay();
            Observe(result);
            LastStatus = IsChallengeEnded ? "Charter " + Snapshot.Challenge.Status + " on day " + Snapshot.Day + ". Start a new scenario to continue." :
                "Day " + Number(Snapshot.Day) + " committed. All ports and ships share this day.";
            if (IsChallengeEnded) { IsPlaying = false; accumulatedSeconds = 0; }
            return result;
        }

        public TradeAttempt PreviewTrade(TradeDirection direction, string goodId, int quantity) =>
            Snapshot.PreviewTrade(Player.Id, SelectedPortId, new TradeCommand(direction, goodId, quantity));

        public RegionalResult Trade(TradeDirection direction, string goodId, int quantity, RegionalWorldSnapshot expectedSnapshot = null)
        {
            Pause();
            var result = world.ExecuteTrade(Player.Id, SelectedPortId, new TradeCommand(direction, goodId, quantity), expectedSnapshot);
            Observe(result);
            var trade = result.Trades[0].Trade;
            LastStatus = trade.Outcome == TradeOutcome.Traded
                ? (direction == TradeDirection.Buy ? "Bought " : "Sold ") + Quantity(trade.ExecutedQuantity) + " " +
                  Content.GetGood(goodId).Name + " for " + Coins(trade.ExecutedAmount) +
                  ". Still day " + Number(Snapshot.Day) + "."
                : "Trade rejected: " + TradingLabText.Outcome(trade.Outcome) + ". No economic state or day changed.";
            return result;
        }

        public RegionalResult Sail()
        {
            Pause();
            if (!NavigationArmed || PendingDestinationId == null)
                throw new InvalidOperationException("Select your ship and a different connected port first.");
            var destination = PendingDestinationId;
            var result = world.Depart(Player.Id, destination);
            Observe(result);
            PendingDestinationId = null;
            LastStatus = "Departed for " + Snapshot.Ports[destination].Name + ". Advance days to travel; departure spent no day.";
            return result;
        }

        public void SetAutomationEnabled(bool enabled)
        {
            Observe(world.SetAutomationEnabled(Player.Id, enabled));
            LastStatus = enabled
                ? "Player route enabled. It acts at the next day boundary or with Run dockside actions."
                : "Player route disabled. Manual orders are available; existing voyages continue when days advance.";
        }

        public void ApplyPolicy(TradeRoutePolicy policy)
        {
            Observe(world.ConfigureAutomation(Player.Id, policy, Player.AutomationEnabled));
            LastStatus = "Route applied. No day, goods or money changed. The next evaluation uses the new policy.";
        }

        public RegionalResult RunDockside()
        {
            Pause();
            var limit = Player.Itinerary == null ? 4 : 8;
            var result = world.RunAutomation(Player.Id, limit);
            Observe(result);
            LastStatus = "Up to " + limit + " dockside decisions resolved without advancing a day. " + CurrentDecision.Reason;
            return result;
        }

        public void ApplyItinerary(CargoItineraryPolicy policy, RegionalWorldSnapshot expectedSnapshot = null)
        {
            Pause();
            Observe(world.ConfigureItinerary(Player.Id, policy, false, expectedSnapshot));
            LastStatus = "Itinerary applied with manual control. Cargo, cash and day are unchanged. Enable route actions when ready.";
        }

        public RegionalResult Construct(string blueprintId)
        {
            Pause();
            var facilityId = Player.OwnerId + "_" + SelectedPortId + "_" + blueprintId;
            if (Snapshot.Economy != null)
            {
                var prefix = facilityId;
                var suffix = 1;
                while (Snapshot.PrivateFacilities.ContainsKey(facilityId))
                    facilityId = prefix + "_" + (++suffix).ToString(CultureInfo.InvariantCulture);
            }
            var result = world.ConstructFacility(Player.OwnerId, SelectedPortId, blueprintId, facilityId);
            Observe(result);
            LastStatus = result.After.PrivateFacilities.TryGetValue(facilityId, out var site)
                ? (site.IsOperational ? "Facility operational. " : "Construction ordered; completes on day " + site.ConstructionCompletionDay + ". ") +
                    "The town received the full investment. First eligible production: day " + site.FirstProductionDay + ", if inputs and capacity allow."
                : "Construction needs more company cash. No money or day changed.";
            return result;
        }

        public void ConfigureFacility(string facilityId, IndustryPolicy policy, bool enabled)
        {
            Observe(world.ConfigureFacility(Player.OwnerId, facilityId, policy));
            Observe(world.SetFacilityEnabled(Player.OwnerId, facilityId, enabled));
            LastStatus = "Industry policy applied. Local purchases and sales run on whole days; inputs and outputs remain private.";
        }

        public RegionalResult Transfer(string facilityId, TransferDirection direction, string goodId, int quantity,
            RegionalWorldSnapshot expectedSnapshot = null)
        {
            Pause();
            var result = world.TransferGoods(Player.OwnerId, Player.Id, facilityId, direction, goodId, quantity, expectedSnapshot);
            Observe(result);
            LastStatus = ReferenceEquals(result.Before, result.After)
                ? "Transfer rejected: check source stock and destination capacity. Nothing changed."
                : "Transferred " + Quantity(quantity) + " " + Content.GetGood(goodId).Name + ". Ownership, cash and day unchanged.";
            return result;
        }

        public RegionalResult WarehouseTrade(string facilityId, TradeDirection direction, string goodId, int quantity,
            RegionalWorldSnapshot expectedSnapshot)
        {
            Pause();
            var result = world.ExecuteWarehouseTrade(Player.OwnerId, facilityId,
                new TradeCommand(direction, goodId, quantity), expectedSnapshot);
            Observe(result);
            LastStatus = ReferenceEquals(result.Before, result.After) ? "Warehouse trade rejected. No economic state changed."
                : "Private stock traded with the local town. Company cash and ownership changed; no day passed.";
            return result;
        }

        public RegionalDecision DecisionFor(string shipId) => world.EvaluateAutomation(shipId);

        public TradeRoutePolicy RecommendedMerchantPolicy() => Identity?.WorldMode == RunWorldMode.ExpandingEconomy
            ? ExpandingEconomyScenario.MerchantPolicy(ExpandingEconomyGeneration.Generate(Identity.Seed.Value,
                Identity.GeneratorVersion, Identity.LivingPricingMode))
            : Identity?.WorldMode == RunWorldMode.LivingCities
                ? LivingCityScenario.MerchantPolicy(LivingCityGeneration.Generate(Identity.Seed.Value, Identity.GeneratorVersion, Identity.LivingPricingMode))
                : EnterpriseScenario.MerchantPolicy();

        public RegionalResult CommissionMint() => Development(() => world.CommissionMint(Player.OwnerId));
        public RegionalResult CommissionShip() => Development(() => world.CommissionShip(Player.OwnerId, SelectedPortId));
        public RegionalResult MintSilver() => Development(() => world.MintSilver(Player.Id, Snapshot.Economy.Configuration.SilverPerBatch));
        public RegionalResult FulfillContract(string contractId, int quantity) =>
            Development(() => world.FulfillExternalContract(Player.Id, contractId, quantity));
        public RegionalResult SupplyPublicWorks(int quantity) => Development(() => world.SupplyPublicWorks(Player.Id, quantity));

        private RegionalResult Development(Func<RegionalResult> action)
        {
            Pause();
            var result = action();
            Observe(result);
            LastStatus = result.Development.Count == 0 ? "No development action committed." : result.Development.Last().Reason;
            return result;
        }

        public string Quantity(long subunits) => ((decimal)subunits / Snapshot.QuantityScale).ToString("0.####", CultureInfo.InvariantCulture);
        public string Coins(long subunits) => ((decimal)subunits / Snapshot.MoneyScale).ToString("0.####", CultureInfo.InvariantCulture);
        public int WholeOrderQuantity(int units) => checked(units * Snapshot.QuantityScale);

        private void Observe(RegionalResult result)
        {
            if (!ReferenceEquals(result.Before, result.After) && result.After.Day == result.Before.Day &&
                result.Decisions.Count == 0) PlayerInputs++;
            AutomatedActions += result.Decisions.Count(d => result.After.Ships[d.ShipId].IsPlayer &&
                result.After.Ships[d.ShipId].OwnerId == Player.OwnerId &&
                (d.Detail?.Trade != null || d.Detail?.Departure != null ||
                 d.IndustrialDetail?.Transfer != null || d.IndustrialDetail?.Trade != null ||
                 d.IndustrialDetail?.Departure != null || d.ItineraryDetail?.Transfer != null ||
                 d.ItineraryDetail?.Trade != null || d.ItineraryDetail?.Departure != null));
            foreach (var entry in result.EconomicEvents.Where(e => e.CompanyId == Player.OwnerId))
                journal.Insert(0, entry);
            if (journal.Count > MaximumJournalEntries) journal.RemoveRange(MaximumJournalEntries, journal.Count - MaximumJournalEntries);
            if (IsChallengeEnded) { IsPlaying = false; accumulatedSeconds = 0; }
            foreach (var facility in result.After.PrivateFacilities.Values)
            {
                if (!result.Before.PrivateFacilities.TryGetValue(facility.Id, out var prior))
                    Add(facility.PortId, null, (facility.IsOperational ? "Constructed private " : "Started construction of private ") +
                        Content.GetBuilding(facility.Blueprint.BuildingId).Name + "; first eligible batch day " + facility.FirstProductionDay + ".");
                else if (!prior.IsOperational && facility.IsOperational)
                    Add(facility.PortId, null, "Construction completed: " + Content.GetBuilding(facility.Blueprint.BuildingId).Name +
                        ". First eligible batch day " + facility.FirstProductionDay + "; inputs and capacity still required.");
                else if (facility.BatchesProduced > prior.BatchesProduced)
                    Add(facility.PortId, null, "Your " + Content.GetBuilding(facility.Blueprint.BuildingId).Name + " completed a batch into private storage.");
            }
            if (result.Movements.Any(movement => movement.ShipId == Player.Id &&
                (movement.Kind == ShipMovementKind.Departure || movement.Kind == ShipMovementKind.Arrival)))
                PendingDestinationId = null;
            foreach (var item in result.Trades)
            {
                var trade = item.Trade;
                var ship = result.After.Ships[item.ShipId];
                if (trade.Outcome != TradeOutcome.Traded) continue;
                if (ship.IsPlayer) hasPlayerTrade = true;
                if (ship.IsPlayer && trade.Direction == TradeDirection.Sell)
                    shipDeliveries[trade.GoodId] = checked((shipDeliveries.TryGetValue(trade.GoodId, out var delivered) ? delivered : 0) + trade.ExecutedQuantity);
                Add(item.PortId, item.ShipId, ship.Name + " " +
                    (trade.Direction == TradeDirection.Buy ? "bought " : "delivered ") + Quantity(trade.ExecutedQuantity) +
                    " " + Content.GetGood(trade.GoodId).Name + " at " + result.After.Ports[item.PortId].Name +
                    " for " + Coins(trade.ExecutedAmount) + ".");
            }
            foreach (var production in result.Production)
            {
                var key = "facility/" + production.PortId + "/" + production.FacilityId;
                var reason = production.Outcome.ToString();
                if (production.Outcome == ScheduledProductionOutcome.Produced)
                    Add(production.PortId, null, result.After.Ports[production.PortId].Name + ": " +
                        Content.GetRecipe(production.RecipeId).Name + " (" + string.Join(", ", production.Changes.Select(
                            change => (change.Delta > 0 ? "+" : "") + Quantity(change.Delta) + " " + Content.GetGood(change.GoodId).Name)) + ").");
                else if (production.Outcome != ScheduledProductionOutcome.NotDue &&
                    (!lastReasons.TryGetValue(key, out var previous) || previous != reason))
                    Add(production.PortId, null, result.After.Ports[production.PortId].Name + ": " +
                        Content.GetRecipe(production.RecipeId).Name + " — " + FacilityStatus(production.Outcome) + ".");
                lastReasons[key] = reason;
            }
            foreach (var consumption in result.Consumption)
            {
                var port = result.After.Ports[consumption.PortId];
                if (port.City == null)
                {
                    Add(port.Id, null, port.Name +
                        (consumption.UnmetQuantity > 0 ? ": unmet demand " + consumption.UnmetQuantity : ": consumed " + consumption.ConsumedQuantity) +
                        " " + Content.GetGood(consumption.GoodId).Name + ".");
                    continue;
                }
                var need = port.Consumption.First(rule => rule.Configuration.Id == consumption.RuleId);
                var key = "city-need/" + port.Id + "/" + need.Configuration.Id;
                var reason = need.LastConsumedQuantity + "/" + need.LastDesiredQuantity + "/" + need.LastUnmetStock + "/" + need.LastUnmetAffordability;
                if (need.LastConsumedQuantity > 0 || !lastReasons.TryGetValue(key, out var previous) || previous != reason)
                    Add(port.Id, null, port.Name + ": " + Quantity(need.LastConsumedQuantity) + "/" + Quantity(need.LastDesiredQuantity) + " " +
                        Content.GetGood(consumption.GoodId).Name +
                        (need.Configuration.Kind == ConsumptionKind.Municipal ? " municipal own-use" : " consumed; households paid " + Coins(need.LastPayment)) +
                        "; unmet stock " + Quantity(need.LastUnmetStock) + ", funds " + Quantity(need.LastUnmetAffordability) + ".");
                lastReasons[key] = reason;
            }
            foreach (var port in result.After.Ports.Values.Where(p => p.City != null))
            {
                var prior = result.Before.Ports[port.Id].City;
                if (prior == null) continue;
                if (port.City.Population != prior.Population)
                    Add(port.Id, null, port.Name + ": population " + prior.Population + " → " + port.City.Population + ". " + port.City.ProsperityReason);
                if (port.City.LuxuryEnabled != prior.LuxuryEnabled)
                    Add(port.Id, null, port.Name + ": durable Furniture demand " + (port.City.LuxuryEnabled ? "eligible after sustained prosperity." : "suspended after sustained hardship."));
            }
            foreach (var movement in result.Movements)
            {
                if (movement.Kind != ShipMovementKind.Departure && movement.Kind != ShipMovementKind.Arrival) continue;
                Add(movement.DestinationPortId, movement.ShipId, result.After.Ships[movement.ShipId].Name +
                    (movement.Kind == ShipMovementKind.Arrival ? " arrived at " : " departed for ") +
                    result.After.Ports[movement.DestinationPortId].Name + ".");
            }
            foreach (var decision in result.Decisions)
            {
                var key = "ship/" + decision.ShipId;
                var reason = decision.Action + ": " + decision.Reason;
                if (!lastReasons.TryGetValue(key, out var previous) || previous != reason)
                    Add(null, decision.ShipId, result.After.Ships[decision.ShipId].Name + ": " + reason);
                lastReasons[key] = reason;
            }
            foreach (var development in result.Development)
                Add(development.PortId, development.AssetId, development.Kind + ": " + development.Reason +
                    (development.Payment == 0 ? "" : " Amount " + Coins(development.Payment) + "."));
        }

        private void Add(string portId, string shipId, string message)
        {
            activity.Insert(0, new ArchipelagoActivity(Snapshot.Day, portId, shipId, message));
            if (activity.Count > MaximumActivity) activity.RemoveAt(activity.Count - 1);
        }

        public static string FacilityStatus(ScheduledProductionOutcome outcome)
        {
            switch (outcome)
            {
                case ScheduledProductionOutcome.NotDue: return "scheduled; not due yet";
                case ScheduledProductionOutcome.Disabled: return "production disabled";
                case ScheduledProductionOutcome.Produced: return "last batch completed";
                case ScheduledProductionOutcome.InputShortage: return "waiting for inputs";
                case ScheduledProductionOutcome.OutputLimit: return "waiting for output stock space";
                case ScheduledProductionOutcome.WarehouseCapacity: return "waiting for private warehouse capacity";
                case ScheduledProductionOutcome.PayrollShortage: return "waiting for employer payroll funds; no unpaid batch";
                default: return outcome.ToString();
            }
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
