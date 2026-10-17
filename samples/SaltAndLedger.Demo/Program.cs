using System;
using System.Collections.Generic;
using System.IO;
using SaltAndLedger.Simulation;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/SaltAndLedger.Demo -- data/content.json");
    return 1;
}

try
{
    var content = ContentLoader.Load(File.ReadAllText(args[0]));
    var inventory = new Inventory(content, new Dictionary<string, int> { ["logs"] = 6 });
    Console.WriteLine($"Loaded schema {content.SchemaVersion}: {content.Goods.Count} goods, {content.Buildings.Count} building, {content.Recipes.Count} recipe.");
    Console.WriteLine($"Initial: logs={inventory.GetQuantity("logs")}, planks={inventory.GetQuantity("planks")}");
    for (var batch = 1; batch <= 4; batch++)
    {
        var produced = Production.TryRunBatch(inventory, "sawmill", "saw_planks");
        Console.WriteLine($"Batch {batch}: {(produced ? "produced" : "insufficient inputs")}; logs={inventory.GetQuantity("logs")}, planks={inventory.GetQuantity("planks")}");
    }
    return 0;
}
catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
    exception is ContentValidationException || exception is KeyNotFoundException || exception is OverflowException ||
    exception is ArgumentException)
{
    Console.Error.WriteLine("Demo failed: " + exception.Message);
    return 1;
}
