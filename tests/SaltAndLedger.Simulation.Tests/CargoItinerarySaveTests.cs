using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class CargoItinerarySaveTests
{
    private const string Ship = WorldIds.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    private static RegionalWorldState World(bool commission = false, int itineraryVersion = 1,
        string charter = RunScenario.IndustrialCharter)
    {
        var draft = GrandCommissionDraft.Generate(42);
        return RunScenario.Create(Content(), new RunIdentity(commission ? GrandCommission.ChallengeId : null,
            charter, RunWorldMode.ExpandingEconomy, 42,
            RunDifficulty.SlowFoundations, livingPricingMode: LivingPricingMode.Coverage,
            commission: commission ? draft.Resolve(draft.Rounds.Select(r => r.Offers[0].TemplateId)) : null,
            companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: itineraryVersion));
    }

    private static CargoItineraryPolicy Policy(RegionalWorldState world, bool wait = true)
    {
        var snapshot = world.Snapshot();
        var port = snapshot.Ships[Ship].DockedPortId!;
        var other = snapshot.Ports.Keys.Where(p => p != port)
            .OrderByDescending(p => snapshot.GetRoute(port, p).DurationDays(snapshot.Ships[Ship].Configuration.Speed)).First();
        return new CargoItineraryPolicy("save_itinerary", new[]
        {
            new CargoItineraryStop(port, new[]
            {
                new CargoItineraryCommand(CargoItineraryCommandKind.LoadFromCompanyTownStock, "flour", 10000),
                new CargoItineraryCommand(CargoItineraryCommandKind.LoadFromCompanyTownStock, "logs", 10000),
                new CargoItineraryCommand(CargoItineraryCommandKind.LoadFromCompanyTownStock, "bread", 10000,
                    shortagePolicy: wait ? CargoItineraryShortagePolicy.Wait : CargoItineraryShortagePolicy.Skip)
            }),
            new CargoItineraryStop(other, new[]
            {
                new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "flour", 10000),
                new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "logs", 10000),
                new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "bread", 10000)
            })
        });
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VersionedIdentitySelectsCurrentSchemaBeforeAnyPolicyIsAssigned(bool commission)
    {
        var world = World(commission);
        var saved = WorldSaveCodec.Save(world);
        var data = JObject.Parse(saved);
        Assert.Equal(6, (int)data["SchemaVersion"]!);
        Assert.Equal(1, (int)data["Identity"]!["ItineraryVersion"]!);
        Assert.Contains("/itinerary-v1", world.Snapshot().RunIdentity!.ComparisonGroup);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(world.Snapshot().RunIdentity, restored.Snapshot().RunIdentity);
        AdvanceBoth(world, restored);
    }

    [Fact]
    public void SaveAfterTwoCommandsResumesThirdExactlyWithoutMovingTheSameFifoSlicesAgain()
    {
        var world = World();
        ReplaceStarterInputsWithPaidStock(world);
        world.ConfigureItinerary(Ship, Policy(world), true);
        world.RunAutomation(Ship, 2);
        var before = world.Snapshot();
        Assert.Equal(2, before.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(10000, before.Ships[Ship].Cargo["flour"]);
        Assert.Equal(10000, before.Ships[Ship].Cargo["logs"]);
        Assert.Equal(0, before.Ships[Ship].Cargo["bread"]);
        Assert.True(before.Accounting.Stores["ship:" + Ship].TotalCostBasis > 0);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(CargoItineraryDecisionKind.Wait, CargoItineraryEvaluator.Evaluate(restored.Snapshot(), Ship).Kind);
        Assert.Equal(Events(world.RunAutomation(Ship)), Events(restored.RunAutomation(Ship)));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        Assert.Equal(2, restored.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        for (var day = 0; day < 16; day++) AdvanceBoth(world, restored);
        Assert.True(restored.Snapshot().Ships[Ship].ItineraryProgress!.Cycle > 0);
    }

    [Fact]
    public void MixedCargoVoyageContinuesWithIdenticalEventsMoneyFifoAndCursor()
    {
        var world = World();
        ReplaceStarterInputsWithPaidStock(world);
        world.ConfigureItinerary(Ship, Policy(world, wait: false), true);
        world.RunAutomation(Ship);
        Assert.Null(world.Snapshot().Ships[Ship].DockedPortId);
        Assert.Equal(2, world.Snapshot().Ships[Ship].Cargo.Count(p => p.Value > 0));
        world.AdvanceDay();
        Assert.Null(world.Snapshot().Ships[Ship].DockedPortId);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(0, restored.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        for (var day = 0; day < 32; day++)
        {
            AdvanceBoth(world, restored);
            if (day % 5 == 0) restored = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(restored));
        }
    }

    [Fact]
    public void PausedManualRelocationOutsideThePolicyRemainsALegalExactSave()
    {
        var world = World();
        var policy = Policy(world);
        world.ConfigureItinerary(Ship, policy);
        var outside = world.Snapshot().Ports.Keys.First(p => policy.Stops.All(s => s.PortId != p));
        world.Depart(Ship, outside);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        while (world.Snapshot().Ships[Ship].DockedPortId == null) AdvanceBoth(world, restored);
        Assert.Equal(outside, restored.Snapshot().Ships[Ship].DockedPortId);
        var docked = WorldSaveCodec.Save(restored);
        Assert.Equal(docked, WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), docked)));
        Assert.False(restored.Snapshot().Ships[Ship].AutomationEnabled);
        Assert.Equal(0, restored.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
    }

    [Fact]
    public void UnstartedItineraryCanResumeOnAManualReturnVoyageWithoutInventingAnOperation()
    {
        var world = World();
        var policy = Policy(world);
        world.ConfigureItinerary(Ship, policy);
        world.Depart(Ship, policy.Stops[1].PortId);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) world.AdvanceDay();
        world.Depart(Ship, policy.Stops[0].PortId);
        world.SetAutomationEnabled(Ship, true);
        world.AdvanceDay();
        var progress = world.Snapshot().Ships[Ship].ItineraryProgress!;
        Assert.Equal(CargoItineraryDecisionKind.ContinueSailing, progress.LastDecision);
        Assert.Equal(-1, progress.OperationsDay);
        Assert.Equal(0, progress.OperationsUsed);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        for (var day = 0; day < 8; day++) AdvanceBoth(world, restored);
    }

    [Fact]
    public void TerminalItineraryIsFrozenAndRetryRetainsVersionWithoutCopyingTheLiveRoute()
    {
        var world = World(commission: true);
        var initial = WorldSaveCodec.Save(world);
        world.ConfigureItinerary(Ship, Policy(world), true);
        world.RunAutomation(Ship, 2);
        world.AbandonChallenge();
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Throws<InvalidOperationException>(() => restored.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => restored.RunAutomation(Ship));
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(initial, WorldSaveCodec.Save(RunScenario.Create(Content(), restored.Snapshot().RunIdentity!)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SavedDailyBudgetDoesNotPermitAnExtraDepartureAfterReload(bool missingWarehouse)
    {
        var world = World(charter: missingWarehouse ? RunScenario.MerchantCharter : RunScenario.IndustrialCharter);
        var snapshot = world.Snapshot();
        var dock = snapshot.Ships[Ship].DockedPortId!;
        var other = snapshot.Ports.Keys.First(p => p != dock);
        var blockedOrder = new CargoItineraryCommand(missingWarehouse ? CargoItineraryCommandKind.LoadFromCompanyTownStock :
            CargoItineraryCommandKind.SellToMarket, "silver", 10000);
        var policy = new CargoItineraryPolicy("saved_daily_budget", new[]
        {
            new CargoItineraryStop(dock, Enumerable.Repeat(blockedOrder, 8)),
            new CargoItineraryStop(other, new[] { blockedOrder })
        });
        world.ConfigureItinerary(Ship, policy, true);
        Assert.Equal(missingWarehouse ? CargoItineraryBlockReason.MissingCompanyStock : CargoItineraryBlockReason.SourceStock,
            CargoItineraryEvaluator.Evaluate(world.Snapshot(), Ship).BlockReason);
        world.RunAutomation(Ship, 8);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        var progress = restored.Snapshot().Ships[Ship].ItineraryProgress!;
        Assert.Equal(8, progress.ActionIndex);
        Assert.Equal(8, progress.OperationsUsed);
        Assert.Equal(CargoItineraryBlockReason.DailyLimit, CargoItineraryEvaluator.Evaluate(restored.Snapshot(), Ship).BlockReason);
        Assert.Equal(Events(world.RunAutomation(Ship, 8)), Events(restored.RunAutomation(Ship, 8)));
        Assert.Equal(dock, restored.Snapshot().Ships[Ship].DockedPortId);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        AdvanceBoth(world, restored);
        AdvanceBoth(world, restored);
        Assert.Null(restored.Snapshot().Ships[Ship].DockedPortId);
    }

    [Theory]
    [InlineData(CargoItineraryCommandKind.LoadFromCompanyTownStock, RunDifficulty.Normal)]
    [InlineData(CargoItineraryCommandKind.LoadFromCompanyTownStock, RunDifficulty.SlowFoundations)]
    [InlineData(CargoItineraryCommandKind.UnloadToCompanyTownStock, RunDifficulty.Normal)]
    [InlineData(CargoItineraryCommandKind.UnloadToCompanyTownStock, RunDifficulty.SlowFoundations)]
    public void MissingWarehousePlanSurvivesPauseSaveAndConstructionWithoutReassignment(
        CargoItineraryCommandKind kind, RunDifficulty difficulty)
    {
        var world = RunScenario.Create(Content(), new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 99, difficulty,
            companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1));
        var dock = world.Snapshot().Ships[Ship].DockedPortId!;
        var other = world.Snapshot().Ports.Keys.First(p => p != dock);
        var policy = new CargoItineraryPolicy("planned_warehouse", new[]
        {
            new CargoItineraryStop(dock, new[]
            {
                new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "grain", 10000),
                new CargoItineraryCommand(kind, "grain", 10000, shortagePolicy: CargoItineraryShortagePolicy.Wait),
                new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "silver", 10000,
                    shortagePolicy: CargoItineraryShortagePolicy.Wait)
            }),
            new CargoItineraryStop(other, new[]
            {
                new CargoItineraryCommand(CargoItineraryCommandKind.LoadFromCompanyTownStock, "logs", 10000)
            })
        });
        world.ConfigureItinerary(Ship, policy, true);
        world.RunAutomation(Ship);
        var waiting = world.Snapshot();
        Assert.Empty(waiting.CompanyStockpiles);
        Assert.Equal(CargoItineraryBlockReason.MissingCompanyStock, waiting.Ships[Ship].ItineraryProgress!.LastReason);
        Assert.Equal(1, waiting.Ships[Ship].ItineraryProgress!.OperationsUsed);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(Events(world.RunAutomation(Ship)), Events(restored.RunAutomation(Ship)));
        Assert.Equal(saved, WorldSaveCodec.Save(restored));

        world.SetAutomationEnabled(Ship, false);
        restored.SetAutomationEnabled(Ship, false);
        saved = WorldSaveCodec.Save(restored);
        restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(CargoItineraryDecisionKind.Paused, CargoItineraryEvaluator.Evaluate(restored.Snapshot(), Ship).Kind);
        foreach (var instance in new[] { world, restored })
        {
            instance.ConstructFacility(WorldIds.PlayerOwnerId, dock, ExpandingEconomyGeneration.FarmBlueprint, "planned_farm");
            if (kind == CargoItineraryCommandKind.LoadFromCompanyTownStock)
                instance.ExecuteWarehouseTrade(WorldIds.PlayerOwnerId, "planned_farm",
                    new TradeCommand(TradeDirection.Buy, "grain", 10000));
            instance.SetAutomationEnabled(Ship, true);
        }
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        var completed = world.RunAutomation(Ship, 1);
        Assert.Equal(Events(completed), Events(restored.RunAutomation(Ship, 1)));
        Assert.Equal(10000, Assert.Single(completed.Transfers).TransferredQuantity);
        Assert.Same(policy, completed.After.Ships[Ship].Itinerary);
        Assert.True(policy.EquivalentTo(restored.Snapshot().Ships[Ship].Itinerary));
        Assert.Equal(2, restored.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(2, restored.Snapshot().Ships[Ship].ItineraryProgress!.OperationsUsed);
        Assert.Equal(0, restored.Snapshot().Day);
        saved = WorldSaveCodec.Save(world);
        restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(Events(world.RunAutomation(Ship)), Events(restored.RunAutomation(Ship)));
        Assert.Equal(2, restored.Snapshot().Ships[Ship].ItineraryProgress!.OperationsUsed);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        for (var day = 0; day < 6; day++) AdvanceBoth(world, restored);
    }

    [Theory]
    [InlineData("schema_three")]
    [InlineData("missing_version")]
    [InlineData("unknown_version")]
    [InlineData("policy_version")]
    [InlineData("missing_progress")]
    [InlineData("orphan_progress")]
    [InlineData("too_many_stops")]
    [InlineData("empty_commands")]
    [InlineData("too_many_commands")]
    [InlineData("total_commands")]
    [InlineData("duplicate_port")]
    [InlineData("unknown_port")]
    [InlineData("unknown_good")]
    [InlineData("negative_quantity")]
    [InlineData("fractional_quantity")]
    [InlineData("invalid_kind")]
    [InlineData("invalid_shortage")]
    [InlineData("inapplicable_reserve")]
    [InlineData("missing_pool")]
    [InlineData("stop_index")]
    [InlineData("action_index")]
    [InlineData("future_cycle")]
    [InlineData("future_budget")]
    [InlineData("over_budget")]
    [InlineData("dated_empty_budget")]
    [InlineData("unstarted_action")]
    [InlineData("unstarted_cycle")]
    [InlineData("unstarted_skip")]
    [InlineData("skip_without_reason")]
    [InlineData("skip_without_completed_action")]
    [InlineData("skip_wait_policy")]
    [InlineData("skip_state_blocker")]
    [InlineData("unstarted_departure")]
    [InlineData("departure_with_partial_cursor")]
    [InlineData("departure_with_reason")]
    [InlineData("wait_without_reason")]
    [InlineData("wait_skip_policy")]
    [InlineData("wait_after_final_command")]
    [InlineData("wait_state_blocker")]
    [InlineData("daily_limit_with_unspent_budget")]
    [InlineData("sailing_without_reason")]
    [InlineData("pause_with_wrong_reason")]
    [InlineData("pause_without_reason_after_operations")]
    [InlineData("invalid_reason")]
    [InlineData("invalid_decision")]
    [InlineData("quantity_without_execution")]
    [InlineData("mismatched_dock")]
    [InlineData("both_policies")]
    public void ResealedMalformedItineraryIsRejectedWithoutReplacingTheActiveWorld(string corruption)
    {
        var world = World();
        var legacy = JObject.Parse(WorldSaveCodec.Save(world))["Ships"]!.Single(s => (string)s["Id"]! == Ship)["Policy"]!.DeepClone();
        world.ConfigureItinerary(Ship, Policy(world), true);
        var before = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        var data = JObject.Parse(saved);
        var ship = data["Ships"]!.Single(s => (string)s["Id"]! == Ship);
        var policy = ship["Itinerary"]!;
        var stops = (JArray)policy["Stops"]!;
        var commands = (JArray)stops[0]!["Commands"]!;
        var command = commands[0]!;
        var progress = ship["ItineraryProgress"]!;
        switch (corruption)
        {
            case "schema_three": data["SchemaVersion"] = 3; break;
            case "missing_version": ((JObject)data["Identity"]!).Remove("ItineraryVersion"); break;
            case "unknown_version": data["Identity"]!["ItineraryVersion"] = 2; break;
            case "policy_version": policy["Version"] = 2; break;
            case "missing_progress": ((JObject)ship).Remove("ItineraryProgress"); break;
            case "orphan_progress": ((JObject)ship).Remove("Itinerary"); break;
            case "too_many_stops": while (stops.Count <= 4) stops.Add(stops[0]!.DeepClone()); break;
            case "empty_commands": commands.Clear(); break;
            case "too_many_commands": while (commands.Count <= 8) commands.Add(command.DeepClone()); break;
            case "total_commands":
                stops.Clear();
                foreach (var port in before.Ports.Keys)
                    stops.Add(new JObject { ["PortId"] = port, ["Commands"] = new JArray(Enumerable.Range(0, 8).Select(_ => command.DeepClone())) });
                break;
            case "duplicate_port": stops[1]!["PortId"] = stops[0]!["PortId"]!.DeepClone(); break;
            case "unknown_port": stops[1]!["PortId"] = "unknown_port"; break;
            case "unknown_good": command["GoodId"] = "unknown_good"; break;
            case "negative_quantity": command["MaximumQuantity"] = -10000; break;
            case "fractional_quantity": command["MaximumQuantity"] = 10001; break;
            case "invalid_kind": command["Kind"] = 9; break;
            case "invalid_shortage": command["ShortagePolicy"] = 9; break;
            case "inapplicable_reserve": command["CashReserve"] = 1; break;
            case "missing_pool": ((JArray)data["CompanyStockpiles"]!).Clear(); break;
            case "stop_index": progress["StopIndex"] = 3; break;
            case "action_index": progress["ActionIndex"] = 4; break;
            case "future_cycle": progress["Cycle"] = 2; break;
            case "future_budget": progress["OperationsDay"] = 1; break;
            case "over_budget": progress["OperationsUsed"] = 9; break;
            case "dated_empty_budget": progress["OperationsDay"] = 0; break;
            case "unstarted_action": progress["ActionIndex"] = 1; break;
            case "unstarted_cycle": progress["Cycle"] = 1; break;
            case "unstarted_skip": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Skip;
                progress["LastReason"] = (int)CargoItineraryBlockReason.SourceStock; break;
            case "skip_without_reason": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Skip;
                progress["ActionIndex"] = 1; progress["OperationsDay"] = 0; progress["OperationsUsed"] = 1; break;
            case "skip_without_completed_action": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Skip;
                progress["LastReason"] = (int)CargoItineraryBlockReason.SourceStock;
                progress["OperationsDay"] = 0; progress["OperationsUsed"] = 1; break;
            case "skip_wait_policy": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Skip;
                progress["LastReason"] = (int)CargoItineraryBlockReason.SourceStock;
                progress["ActionIndex"] = 1; progress["OperationsDay"] = 0; progress["OperationsUsed"] = 1;
                command["ShortagePolicy"] = (int)CargoItineraryShortagePolicy.Wait; break;
            case "skip_state_blocker": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Skip;
                progress["LastReason"] = (int)CargoItineraryBlockReason.DailyLimit;
                progress["ActionIndex"] = 1; progress["OperationsDay"] = 0; progress["OperationsUsed"] = 8; break;
            case "unstarted_departure": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Depart; break;
            case "departure_with_partial_cursor": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Depart;
                progress["ActionIndex"] = 1; progress["OperationsDay"] = 0; progress["OperationsUsed"] = 1; break;
            case "departure_with_reason": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Depart;
                progress["LastReason"] = (int)CargoItineraryBlockReason.SourceStock;
                progress["OperationsDay"] = 0; progress["OperationsUsed"] = 1; break;
            case "wait_without_reason": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Wait; break;
            case "wait_skip_policy": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Wait;
                progress["LastReason"] = (int)CargoItineraryBlockReason.SourceStock; break;
            case "wait_after_final_command": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Wait;
                progress["LastReason"] = (int)CargoItineraryBlockReason.SourceStock;
                progress["ActionIndex"] = 3; progress["OperationsDay"] = 0; progress["OperationsUsed"] = 3; break;
            case "wait_state_blocker": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Wait;
                progress["LastReason"] = (int)CargoItineraryBlockReason.Sailing;
                command["ShortagePolicy"] = (int)CargoItineraryShortagePolicy.Wait; break;
            case "daily_limit_with_unspent_budget": progress["LastDecision"] = (int)CargoItineraryDecisionKind.Wait;
                progress["LastReason"] = (int)CargoItineraryBlockReason.DailyLimit;
                progress["OperationsDay"] = 0; progress["OperationsUsed"] = 1; break;
            case "sailing_without_reason": progress["LastDecision"] = (int)CargoItineraryDecisionKind.ContinueSailing; break;
            case "pause_with_wrong_reason": progress["LastReason"] = (int)CargoItineraryBlockReason.SourceStock; break;
            case "pause_without_reason_after_operations": progress["OperationsDay"] = 0; progress["OperationsUsed"] = 1; break;
            case "invalid_reason": progress["LastReason"] = 99; break;
            case "invalid_decision": progress["LastDecision"] = 99; break;
            case "quantity_without_execution": progress["LastQuantity"] = 10000; break;
            case "mismatched_dock": progress["StopIndex"] = 1; break;
            case "both_policies": ship["Policy"] = legacy; break;
        }
        Rechecksum(data);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), data.ToString(Formatting.None)));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    [Fact]
    public void EnabledVoyageCannotClaimTheWrongItineraryDestination()
    {
        var world = World();
        world.ConfigureItinerary(Ship, Policy(world, wait: false), true);
        world.RunAutomation(Ship);
        var saved = WorldSaveCodec.Save(world);
        var data = JObject.Parse(saved);
        var ship = data["Ships"]!.Single(s => (string)s["Id"]! == Ship);
        Assert.Null((string?)ship["Dock"]);
        ship["ItineraryProgress"]!["StopIndex"] = 0;
        Rechecksum(data);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), data.ToString(Formatting.None)));
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    private static void AdvanceBoth(RegionalWorldState first, RegionalWorldState second)
    {
        Assert.Equal(Events(first.AdvanceDay()), Events(second.AdvanceDay()));
        Assert.Equal(WorldSaveCodec.Save(first), WorldSaveCodec.Save(second));
    }

    private static void ReplaceStarterInputsWithPaidStock(RegionalWorldState world)
    {
        foreach (var good in new[] { "flour", "logs" })
        {
            Assert.Equal(TradeOutcome.Traded, world.ExecuteWarehouseTrade(WorldIds.PlayerOwnerId,
                RunScenario.StarterBakery, new TradeCommand(TradeDirection.Sell, good, 20000)).WarehouseTrades.Single().Trade.Outcome);
            Assert.Equal(TradeOutcome.Traded, world.ExecuteWarehouseTrade(WorldIds.PlayerOwnerId,
                RunScenario.StarterBakery, new TradeCommand(TradeDirection.Buy, good, 20000)).WarehouseTrades.Single().Trade.Outcome);
        }
    }

    private static string Events(RegionalResult result) => JsonConvert.SerializeObject(new
    {
        result.EconomicEvents, result.CashTransfers, result.CityChanges, result.Trades,
        result.WarehouseTrades, result.Production, result.Consumption, result.Movements,
        result.PrivateProduction, result.Construction, result.Transfers, result.Development
    });

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void Rechecksum(JObject data)
    {
        data["StateFingerprint"] = "";
        static JToken Canonical(JToken token) => token switch
        {
            JObject obj => new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new JProperty(p.Name, Canonical(p.Value)))),
            JArray array => new JArray(array.Select(Canonical)),
            _ => token.DeepClone()
        };
        data["StateFingerprint"] = Hash(Canonical(data).ToString(Formatting.None));
    }
}
