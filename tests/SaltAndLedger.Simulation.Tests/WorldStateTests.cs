using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class WorldStateTests
{
    private static ContentCatalog Canonical() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    private static BuildingInstance Mill(string id, int priority = 0, bool enabled = true) =>
        new BuildingInstance(id, "sawmill", "saw_planks", priority, enabled);

    private static WorldState World(int logs = 6, int planks = 0,
        IEnumerable<BuildingInstance>? buildings = null, long initialTick = 0) =>
        new WorldState(Canonical(), "test_port", buildings ?? new[] { Mill("mill_a"), Mill("mill_b") },
            new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks }, initialTick);

    [Fact]
    public void IdenticalInitialStateAndOrderedCommandsProduceIdenticalStructuredResults()
    {
        var first = World();
        var second = World();
        var commands = new[]
        {
            Array.Empty<SetProductionEnabledCommand>(),
            new[] { new SetProductionEnabledCommand("mill_a", false) },
            new[] { new SetProductionEnabledCommand("mill_a", true), new SetProductionEnabledCommand("mill_b", false) },
            new[] { new SetProductionEnabledCommand("mill_b", false), new SetProductionEnabledCommand("mill_b", true) },
            new[] { new SetProductionEnabledCommand("mill_a", false), new SetProductionEnabledCommand("mill_b", false) },
            new[] { new SetProductionEnabledCommand("mill_b", true) }
        };

        for (var tick = 0; tick < commands.Length; tick++)
        {
            var firstResult = first.Step(commands[tick]);
            var secondResult = second.Step(commands[tick]);
            Assert.Equal(tick, firstResult.Tick);
            Assert.Equal(tick + 1, firstResult.After.Tick);
            EqualResults(firstResult, secondResult);
            EqualSnapshots(first.Snapshot(), second.Snapshot());
        }

        Assert.Equal(6, first.Tick);
        Assert.Equal(0, first.Snapshot().Inventory["logs"]);
        Assert.Equal(3, first.Snapshot().Inventory["planks"]);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    [InlineData("sv-SE")]
    public void EquivalentInputsUsePriorityThenOrdinalIdAcrossCultures(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var buildings = new[]
            {
                Mill("last", int.MaxValue), Mill("milla"), Mill("first", int.MinValue), Mill("mill_a")
            };
            var first = World(logs: 4, buildings: buildings);
            var reversedContent = ContentLoader.Load(ContentLoaderTests.Change(root =>
                root["goods"] = new JArray(((JArray)root["goods"]!).Reverse())));
            var second = new WorldState(reversedContent, "test_port", buildings.Reverse(),
                new Dictionary<string, int> { ["planks"] = 0, ["logs"] = 4 });

            EqualSnapshots(first.Snapshot(), second.Snapshot());
            Assert.Equal(new[] { "logs", "planks" }, first.Snapshot().Inventory.Keys);
            Assert.Equal(new[] { "first", "mill_a", "milla", "last" },
                first.Snapshot().Buildings.Select(building => building.Id));
            for (var tick = 0; tick < 3; tick++)
            {
                var result = first.Step();
                EqualResults(result, second.Step());
                Assert.Equal(new[] { "first", "mill_a", "milla", "last" },
                    result.ProductionAttempts.Select(attempt => attempt.BuildingInstanceId));
                if (tick == 0)
                    Assert.Equal(new[] { ProductionOutcome.Produced, ProductionOutcome.Produced,
                        ProductionOutcome.ResourceShortage, ProductionOutcome.ResourceShortage },
                        result.ProductionAttempts.Select(attempt => attempt.Outcome));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TwoInstancesShareStockAndReportDeterministicContention()
    {
        var world = World(logs: 2, buildings: new[] { Mill("mill_b"), Mill("mill_a") });

        var result = world.Step();

        Assert.Equal(0, result.Tick);
        Assert.Equal(1, world.Tick);
        Assert.Equal("test_port", world.LocationId);
        Assert.Equal("test_port", result.After.LocationId);
        var produced = result.ProductionAttempts[0];
        Assert.Equal("mill_a", produced.BuildingInstanceId);
        Assert.Equal("sawmill", produced.BuildingId);
        Assert.Equal("saw_planks", produced.RecipeId);
        Assert.Equal(ProductionOutcome.Produced, produced.Outcome);
        Assert.Collection(produced.InventoryChanges,
            change => EqualChange(change, "logs", 2, 0, -2),
            change => EqualChange(change, "planks", 0, 1, 1));
        var shortage = result.ProductionAttempts[1];
        Assert.Equal("mill_b", shortage.BuildingInstanceId);
        Assert.Equal(ProductionOutcome.ResourceShortage, shortage.Outcome);
        Assert.Empty(shortage.InventoryChanges);
        Assert.Equal(0, result.After.Inventory["logs"]);
        Assert.Equal(1, result.After.Inventory["planks"]);
        EqualSnapshots(result.After, world.Snapshot());
    }

    [Fact]
    public void EachEnabledBuildingAttemptsOnlyOneBatchPerTick()
    {
        var world = World(logs: 100);

        var result = world.Step();

        Assert.Equal(2, result.ProductionAttempts.Count);
        Assert.All(result.ProductionAttempts, attempt => Assert.Equal(ProductionOutcome.Produced, attempt.Outcome));
        Assert.Equal(96, result.After.Inventory["logs"]);
        Assert.Equal(2, result.After.Inventory["planks"]);
        Assert.Equal(1, world.Tick);
    }

    [Fact]
    public void ExhaustedProductionKeepsAdvancingWithoutNegativeStock()
    {
        var world = World(logs: 5);
        world.Step();
        for (var tick = 1; tick <= 10; tick++)
        {
            var result = world.Step();
            Assert.Equal(tick, result.Tick);
            Assert.Equal(tick + 1, result.After.Tick);
            Assert.Equal(2, result.ProductionAttempts.Count);
            Assert.All(result.ProductionAttempts, attempt =>
            {
                Assert.Equal(ProductionOutcome.ResourceShortage, attempt.Outcome);
                Assert.Empty(attempt.InventoryChanges);
            });
            Assert.Equal(1, result.After.Inventory["logs"]);
            Assert.Equal(2, result.After.Inventory["planks"]);
            Assert.All(result.After.Inventory.Values, quantity => Assert.True(quantity >= 0));
        }
    }

    [Fact]
    public void ControlsAffectTheCurrentTickAndPersistUntilChanged()
    {
        var world = World(logs: 8, buildings: new[] { Mill("mill_a", enabled: false), Mill("mill_b") });
        var initiallyDisabled = world.Step();
        Assert.Equal("mill_b", Assert.Single(initiallyDisabled.ProductionAttempts).BuildingInstanceId);

        var switched = world.Step(new[]
        {
            new SetProductionEnabledCommand("mill_a", true),
            new SetProductionEnabledCommand("mill_b", false)
        });
        Assert.Equal("mill_a", Assert.Single(switched.ProductionAttempts).BuildingInstanceId);
        Assert.True(switched.After.Buildings[0].ProductionEnabled);
        Assert.False(switched.After.Buildings[1].ProductionEnabled);

        var persisted = world.Step();
        Assert.Equal("mill_a", Assert.Single(persisted.ProductionAttempts).BuildingInstanceId);
        Assert.Equal(2, persisted.After.Inventory["logs"]);
        Assert.Equal(3, persisted.After.Inventory["planks"]);
        Assert.False(initiallyDisabled.After.Buildings[0].ProductionEnabled);
        Assert.True(initiallyDisabled.After.Buildings[1].ProductionEnabled);
    }

    [Fact]
    public void LastCommandForAnInstanceWinsInDeclaredOrder()
    {
        var world = World(logs: 2);
        var result = world.Step(new[]
        {
            new SetProductionEnabledCommand("mill_a", true),
            new SetProductionEnabledCommand("mill_b", false),
            new SetProductionEnabledCommand("mill_a", false),
            new SetProductionEnabledCommand("mill_b", true)
        });

        Assert.Equal("mill_b", Assert.Single(result.ProductionAttempts).BuildingInstanceId);
        Assert.False(result.After.Buildings[0].ProductionEnabled);
        Assert.True(result.After.Buildings[1].ProductionEnabled);
        Assert.Equal(0, result.After.Inventory["logs"]);
        Assert.Equal(1, result.After.Inventory["planks"]);
    }

    [Fact]
    public void AllDisabledAndEmptyWorldsStillAdvanceOneTick()
    {
        var disabled = World(buildings: new[] { Mill("mill_a", enabled: false) });
        var empty = World(buildings: Array.Empty<BuildingInstance>());
        foreach (var world in new[] { disabled, empty })
        {
            var result = world.Step();
            Assert.Empty(result.ProductionAttempts);
            Assert.Equal(1, world.Tick);
            Assert.Equal(6, result.After.Inventory["logs"]);
            Assert.Equal(0, result.After.Inventory["planks"]);
        }
    }

    [Fact]
    public void WorldUsesCanonicalRecipeAmountsInsteadOfDemoConstants()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
        {
            root["recipes"]![0]!["inputs"]![0]!["quantity"] = 3;
            root["recipes"]![0]!["outputs"]![0]!["quantity"] = 2;
        }));
        var world = new WorldState(content, "test_port", new[] { Mill("mill_a"), Mill("mill_b") },
            new Dictionary<string, int> { ["logs"] = 6 });

        var result = world.Step();

        Assert.All(result.ProductionAttempts, attempt => Assert.Equal(ProductionOutcome.Produced, attempt.Outcome));
        Assert.Equal(0, result.After.Inventory["logs"]);
        Assert.Equal(4, result.After.Inventory["planks"]);
        Assert.Collection(result.ProductionAttempts[1].InventoryChanges,
            change => EqualChange(change, "logs", 3, 0, -3),
            change => EqualChange(change, "planks", 2, 4, 2));
    }

    [Fact]
    public void SuccessfulBatchWithNoNetStockChangeIsStillReportedAsProduced()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
        {
            root["recipes"]![0]!["outputs"]![0]!["goodId"] = "logs";
            root["recipes"]![0]!["outputs"]![0]!["quantity"] = 2;
        }));
        var world = new WorldState(content, "test_port", new[] { Mill("mill_a") },
            new Dictionary<string, int> { ["logs"] = int.MaxValue });

        var result = world.Step();

        Assert.Equal(ProductionOutcome.Produced, Assert.Single(result.ProductionAttempts).Outcome);
        Assert.Empty(result.ProductionAttempts[0].InventoryChanges);
        Assert.Equal(int.MaxValue, result.After.Inventory["logs"]);
        Assert.Equal(1, world.Tick);
    }

    [Fact]
    public void InvalidLaterCommandRejectsTheWholeCommandBatch()
    {
        var world = World();
        var before = world.Snapshot();

        Assert.Throws<KeyNotFoundException>(() => world.Step(new[]
        {
            new SetProductionEnabledCommand("mill_a", false),
            new SetProductionEnabledCommand("missing", true)
        }));

        EqualSnapshots(before, world.Snapshot());
        Assert.All(world.Step().ProductionAttempts, attempt => Assert.Equal(ProductionOutcome.Produced, attempt.Outcome));
    }

    [Fact]
    public void NullCommandElementRejectsEarlierConfigurationChanges()
    {
        var world = World();
        var before = world.Snapshot();

        Assert.ThrowsAny<ArgumentException>(() => world.Step(new[]
        {
            new SetProductionEnabledCommand("mill_a", false), null!
        }));

        EqualSnapshots(before, world.Snapshot());
        Assert.Equal(2, world.Step().ProductionAttempts.Count);
    }

    [Fact]
    public void CommandIteratorFailureLeavesTheWholeTickUnchangedAndAllowsRetry()
    {
        var world = World();
        var before = world.Snapshot();
        IEnumerable<SetProductionEnabledCommand> FailingCommands()
        {
            yield return new SetProductionEnabledCommand("mill_a", false);
            throw new InvalidOperationException("Command source failed.");
        }

        Assert.Throws<InvalidOperationException>(() => world.Step(FailingCommands()));

        EqualSnapshots(before, world.Snapshot());
        Assert.Equal(2, world.Step().ProductionAttempts.Count);
    }

    [Fact]
    public void ReentrantStepFromCommandIteratorIsRejectedWithoutCommittingEitherStep()
    {
        var world = World();
        var before = world.Snapshot();
        IEnumerable<SetProductionEnabledCommand> ReentrantCommands()
        {
            yield return new SetProductionEnabledCommand("mill_a", false);
            world.Step();
        }

        Assert.Throws<InvalidOperationException>(() => world.Step(ReentrantCommands()));

        EqualSnapshots(before, world.Snapshot());
        Assert.Equal(2, world.Step().ProductionAttempts.Count);
    }

    [Fact]
    public void LaterProductionOverflowRollsBackEarlierSuccessAndConfigurationChanges()
    {
        var world = World(logs: 4, planks: int.MaxValue - 1,
            buildings: new[] { Mill("mill_a", enabled: false), Mill("mill_b") });
        var before = world.Snapshot();

        Assert.Throws<OverflowException>(() => world.Step(new[]
        {
            new SetProductionEnabledCommand("mill_a", true)
        }));

        EqualSnapshots(before, world.Snapshot());
        var retry = world.Step();
        Assert.Equal("mill_b", Assert.Single(retry.ProductionAttempts).BuildingInstanceId);
        Assert.Equal(2, retry.After.Inventory["logs"]);
        Assert.Equal(int.MaxValue, retry.After.Inventory["planks"]);
        Assert.Equal(0, retry.Tick);
        Assert.False(retry.After.Buildings[0].ProductionEnabled);
    }

    [Fact]
    public void TickAdvancesToLongMaximumThenOverflowPreservesAllState()
    {
        var world = World(logs: 4, buildings: new[] { Mill("mill_a", enabled: false) },
            initialTick: long.MaxValue - 1);
        var finalStep = world.Step();
        Assert.Equal(long.MaxValue - 1, finalStep.Tick);
        Assert.Equal(long.MaxValue, finalStep.After.Tick);
        var beforeOverflow = world.Snapshot();

        Assert.Throws<OverflowException>(() => world.Step(new[]
        {
            new SetProductionEnabledCommand("mill_a", true)
        }));

        EqualSnapshots(beforeOverflow, world.Snapshot());
        Assert.Equal(long.MaxValue, world.Tick);
        Assert.Throws<OverflowException>(() => World(initialTick: long.MaxValue).Step());
    }

    [Fact]
    public void InputsSnapshotsAndStepResultsCannotMutateLiveOrHistoricalState()
    {
        var initialStock = new Dictionary<string, int> { ["logs"] = 6 };
        var initialBuildings = new[] { Mill("mill_a"), Mill("mill_b") };
        var world = new WorldState(Canonical(), "test_port", initialBuildings, initialStock);
        var initial = world.Snapshot();
        initialStock["logs"] = 100;
        initialBuildings[0] = Mill("replacement", enabled: false);
        Assert.Equal(6, initial.Inventory["logs"]);
        Assert.Equal("mill_a", initial.Buildings[0].Id);

        var inventoryView = Assert.IsAssignableFrom<IDictionary<string, int>>(initial.Inventory);
        Assert.Throws<NotSupportedException>(() => inventoryView["logs"] = 100);
        var buildingsView = Assert.IsAssignableFrom<IList<BuildingInstance>>(initial.Buildings);
        Assert.Throws<NotSupportedException>(() => buildingsView[0] = Mill("replacement"));

        var first = world.Step();
        var attemptsView = Assert.IsAssignableFrom<IList<ProductionAttempt>>(first.ProductionAttempts);
        Assert.Throws<NotSupportedException>(() => attemptsView.Clear());
        var changesView = Assert.IsAssignableFrom<IList<InventoryChange>>(first.ProductionAttempts[0].InventoryChanges);
        Assert.Throws<NotSupportedException>(() => changesView.Clear());
        var resultInventory = Assert.IsAssignableFrom<IDictionary<string, int>>(first.After.Inventory);
        Assert.Throws<NotSupportedException>(() => resultInventory.Clear());

        world.Step(new[] { new SetProductionEnabledCommand("mill_a", false) });

        Assert.Equal(0, initial.Tick);
        Assert.Equal(6, initial.Inventory["logs"]);
        Assert.Equal(1, first.After.Tick);
        Assert.Equal(2, first.After.Inventory["logs"]);
        Assert.True(first.After.Buildings[0].ProductionEnabled);
        EqualChange(first.ProductionAttempts[0].InventoryChanges[0], "logs", 6, 4, -2);
        Assert.Equal(2, world.Tick);
        Assert.Equal(0, world.Snapshot().Inventory["logs"]);
        Assert.False(world.Snapshot().Buildings[0].ProductionEnabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Mill")]
    [InlineData("1mill")]
    [InlineData("mill-a")]
    [InlineData("mill\n")]
    [InlineData("m\u0131ll")]
    [InlineData("mill\0")]
    public void RuntimeIdentifiersRequireTheFullAsciiStableIdFormat(string invalid)
    {
        Assert.ThrowsAny<ArgumentException>(() => Mill(invalid));
        Assert.ThrowsAny<ArgumentException>(() => new BuildingInstance("mill_a", invalid, "saw_planks"));
        Assert.ThrowsAny<ArgumentException>(() => new BuildingInstance("mill_a", "sawmill", invalid));
        Assert.ThrowsAny<ArgumentException>(() => new SetProductionEnabledCommand(invalid, true));
        Assert.ThrowsAny<ArgumentException>(() => new WorldState(Canonical(), invalid, Array.Empty<BuildingInstance>()));
    }

    [Fact]
    public void NullReferencesAndNegativeClockAreRejectedAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() => Mill(null!));
        Assert.Throws<ArgumentNullException>(() => new BuildingInstance("mill_a", null!, "saw_planks"));
        Assert.Throws<ArgumentNullException>(() => new BuildingInstance("mill_a", "sawmill", null!));
        Assert.Throws<ArgumentNullException>(() => new SetProductionEnabledCommand(null!, true));
        Assert.Throws<ArgumentNullException>(() => new WorldState(null!, "test_port", Array.Empty<BuildingInstance>()));
        Assert.Throws<ArgumentNullException>(() => new WorldState(Canonical(), null!, Array.Empty<BuildingInstance>()));
        Assert.Throws<ArgumentNullException>(() => new WorldState(Canonical(), "test_port", null!));
        Assert.ThrowsAny<ArgumentException>(() => World(buildings: new BuildingInstance[] { null! }));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(initialTick: -1));
    }

    [Theory]
    [InlineData("missing", "saw_planks")]
    [InlineData("sawmill", "missing")]
    [InlineData("logs", "saw_planks")]
    [InlineData("sawmill", "planks")]
    public void UnknownOrWrongKindDefinitionReferencesAreRejectedBeforeStepping(string buildingId, string recipeId)
    {
        Assert.Throws<KeyNotFoundException>(() => World(buildings: new[]
        {
            new BuildingInstance("mill_a", buildingId, recipeId)
        }));
    }

    [Fact]
    public void DuplicateInstanceIdsAreRejectedEvenWhenConfigurationDiffers()
    {
        Assert.ThrowsAny<ArgumentException>(() => World(buildings: new[]
        {
            Mill("mill_a"), Mill("mill_a", priority: 1, enabled: false)
        }));
    }

    [Fact]
    public void IncompatibleBuildingAndRecipeAreRejectedEvenWhenDisabled()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
            ((JArray)root["buildings"]!).Add(new JObject
            {
                ["id"] = "other_building", ["name"] = "Other building", ["description"] = "Test association."
            })));
        Assert.ThrowsAny<ArgumentException>(() => new WorldState(content, "test_port", new[]
        {
            new BuildingInstance("other_instance", "other_building", "saw_planks", productionEnabled: false)
        }));
    }

    [Fact]
    public void InitialInventoryDefaultsToZeroAndRejectsInvalidBalances()
    {
        var empty = new WorldState(Canonical(), "test_port", new[] { Mill("mill_a") });
        Assert.Equal(new[] { "logs", "planks" }, empty.Snapshot().Inventory.Keys);
        Assert.All(empty.Snapshot().Inventory.Values, quantity => Assert.Equal(0, quantity));
        Assert.Equal(ProductionOutcome.ResourceShortage, Assert.Single(empty.Step().ProductionAttempts).Outcome);
        Assert.Throws<ArgumentOutOfRangeException>(() => World(logs: -1));
        Assert.Throws<KeyNotFoundException>(() => new WorldState(Canonical(), "test_port", Array.Empty<BuildingInstance>(),
            new Dictionary<string, int> { ["missing"] = 1 }));
    }

    private static void EqualChange(InventoryChange actual, string goodId, int before, int after, int delta)
    {
        Assert.Equal(goodId, actual.GoodId);
        Assert.Equal(before, actual.Before);
        Assert.Equal(after, actual.After);
        Assert.Equal(delta, actual.Delta);
    }

    private static void EqualSnapshots(WorldSnapshot expected, WorldSnapshot actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        Assert.Equal(expected.LocationId, actual.LocationId);
        Assert.Equal(expected.Inventory.ToArray(), actual.Inventory.ToArray());
        Assert.Equal(expected.Buildings.Select(building =>
                (building.Id, building.BuildingId, building.RecipeId, building.Priority, building.ProductionEnabled)),
            actual.Buildings.Select(building =>
                (building.Id, building.BuildingId, building.RecipeId, building.Priority, building.ProductionEnabled)));
    }

    private static void EqualResults(StepResult expected, StepResult actual)
    {
        Assert.Equal(expected.Tick, actual.Tick);
        EqualSnapshots(expected.After, actual.After);
        Assert.Equal(expected.ProductionAttempts.Count, actual.ProductionAttempts.Count);
        for (var index = 0; index < expected.ProductionAttempts.Count; index++)
        {
            var expectedAttempt = expected.ProductionAttempts[index];
            var actualAttempt = actual.ProductionAttempts[index];
            Assert.Equal(expectedAttempt.BuildingInstanceId, actualAttempt.BuildingInstanceId);
            Assert.Equal(expectedAttempt.BuildingId, actualAttempt.BuildingId);
            Assert.Equal(expectedAttempt.RecipeId, actualAttempt.RecipeId);
            Assert.Equal(expectedAttempt.Outcome, actualAttempt.Outcome);
            Assert.Equal(expectedAttempt.InventoryChanges.Select(change => (change.GoodId, change.Before, change.After, change.Delta)),
                actualAttempt.InventoryChanges.Select(change => (change.GoodId, change.Before, change.After, change.Delta)));
        }
    }
}
