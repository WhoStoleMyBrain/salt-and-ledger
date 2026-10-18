using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SaltAndLedger.Simulation;

var worldMode = args.Length >= 2 && args[1] == "--world";
var marketMode = args.Length == 2 && args[1] == "--market";
var shippingMode = args.Length == 2 && args[1] == "--shipping";
var pricingMode = args.Length >= 2 && args[1] == "--pricing";
var checkPricing = pricingMode && args.Length >= 3 && args[2] == "--check";
if (args.Length != 1 && !(worldMode && args.Length <= 3) && !marketMode && !shippingMode &&
    !(pricingMode && args.Length <= (checkPricing ? 4 : 3)))
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/SaltAndLedger.Demo -- data/content.json [--world [initialLogs] | --market | --pricing [--check] [output.json] | --shipping]");
    return 1;
}

var initialLogs = 6;
if (worldMode && args.Length == 3 &&
    !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out initialLogs))
{
    Console.Error.WriteLine("initialLogs must be an integer from 0 through 2147483647.");
    return 1;
}

try
{
    var content = ContentLoader.Load(File.ReadAllText(args[0]));
    if (worldMode) return WorldDemo.Run(content, initialLogs);
    if (marketMode) return MarketDemo.Run(content);
    if (shippingMode) return ShippingDemo.Run(content);
    if (pricingMode)
    {
        var outputIndex = checkPricing ? 3 : 2;
        return PricingExperiments.Run(content, args.Length > outputIndex ? args[outputIndex] : null, checkPricing);
    }

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
    exception is ArgumentException || exception is InvalidOperationException)
{
    Console.Error.WriteLine("Demo failed: " + exception.Message);
    return 1;
}
