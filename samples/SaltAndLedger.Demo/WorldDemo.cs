using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SaltAndLedger.Simulation;

internal static class WorldDemo
{
    public static int Run(ContentCatalog content, int initialLogs)
    {
        var transcript = Simulate(content, initialLogs, reverseBuildings: false);
        var repeated = Simulate(content, initialLogs, reverseBuildings: false);
        var reordered = Simulate(content, initialLogs, reverseBuildings: true);
        if (!string.Equals(transcript, repeated, StringComparison.Ordinal) ||
            !string.Equals(transcript, reordered, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("World simulation produced different replay results.");
        }

        Console.Write(transcript);
        Console.WriteLine("Determinism check passed: repeated run and reversed building input order match.");
        return 0;
    }

    private static string Simulate(ContentCatalog content, int initialLogs, bool reverseBuildings)
    {
        var buildings = new[]
        {
            new BuildingInstance("sawmill_a", "sawmill", "saw_planks"),
            new BuildingInstance("sawmill_b", "sawmill", "saw_planks")
        };
        if (reverseBuildings) Array.Reverse(buildings);
        var world = new WorldState(content, "harbor", buildings,
            new Dictionary<string, int> { ["logs"] = initialLogs });
        var text = new StringBuilder();
        text.AppendLine("World: harbor; two sawmills share one local inventory.");
        AppendState(text, "Initial", world.Snapshot());

        for (var step = 0; step < 5; step++)
        {
            var commands = CommandsFor(step);
            var result = world.Step(commands);
            text.AppendFormat(CultureInfo.InvariantCulture, "Tick {0} -> {1}", result.Tick, result.After.Tick).AppendLine();
            foreach (var command in commands)
            {
                text.AppendLine($"  Control: {command.BuildingInstanceId} {(command.Enabled ? "enabled" : "disabled")}");
            }

            foreach (var attempt in result.ProductionAttempts)
            {
                text.Append($"  {attempt.BuildingInstanceId}: ");
                text.Append(attempt.Outcome == ProductionOutcome.Produced ? "produced" : "resource shortage");
                foreach (var change in attempt.InventoryChanges)
                {
                    text.AppendFormat(CultureInfo.InvariantCulture, "; {0} {1} -> {2} ({3:+0;-0;0})",
                        change.GoodId, change.Before, change.After, change.Delta);
                }
                text.AppendLine();
            }
            if (result.ProductionAttempts.Count == 0) text.AppendLine("  No enabled facilities.");
            AppendState(text, "  After", result.After);
        }

        AppendState(text, "Final", world.Snapshot());
        return text.ToString();
    }

    private static SetProductionEnabledCommand[] CommandsFor(int step)
    {
        switch (step)
        {
            case 0:
                return new[] { new SetProductionEnabledCommand("sawmill_a", false) };
            case 1:
                return new[]
                {
                    new SetProductionEnabledCommand("sawmill_a", true),
                    new SetProductionEnabledCommand("sawmill_b", false)
                };
            case 2:
                return new[] { new SetProductionEnabledCommand("sawmill_b", true) };
            case 4:
                return new[]
                {
                    new SetProductionEnabledCommand("sawmill_a", false),
                    new SetProductionEnabledCommand("sawmill_b", false)
                };
            default:
                return Array.Empty<SetProductionEnabledCommand>();
        }
    }

    private static void AppendState(StringBuilder text, string label, WorldSnapshot snapshot)
    {
        text.AppendFormat(CultureInfo.InvariantCulture, "{0} tick {1}: ", label, snapshot.Tick);
        text.Append(string.Join(", ", snapshot.Inventory.Select(item =>
            item.Key + "=" + item.Value.ToString(CultureInfo.InvariantCulture))));
        text.Append("; ");
        text.AppendLine(string.Join(", ", snapshot.Buildings.Select(building =>
            building.Id + "=" + (building.ProductionEnabled ? "enabled" : "disabled"))));
    }
}
