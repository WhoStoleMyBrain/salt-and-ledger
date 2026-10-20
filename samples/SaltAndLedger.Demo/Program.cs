using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SaltAndLedger.Simulation;

var worldMode = args.Length >= 2 && args[1] == "--world";
var marketMode = args.Length == 2 && args[1] == "--market";
var shippingMode = args.Length == 2 && args[1] == "--shipping";
var automationMode = args.Length == 2 && args[1] == "--automation";
var pricingMode = args.Length >= 2 && args[1] == "--pricing";
var checkPricing = pricingMode && args.Length >= 3 && args[2] == "--check";
var archipelagoMode = args.Length >= 2 && args[1] == "--archipelago";
var checkArchipelago = archipelagoMode && args.Length >= 3 && args[2] == "--check";
var enterpriseMode = args.Length >= 2 && args[1] == "--enterprise";
var checkEnterprise = enterpriseMode && args.Length >= 3 && args[2] == "--check";
if (args.Length != 1 && !(worldMode && args.Length <= 3) && !marketMode && !shippingMode && !automationMode &&
    !(pricingMode && args.Length <= (checkPricing ? 4 : 3)) &&
    !(archipelagoMode && args.Length <= (checkArchipelago ? 4 : 3)) &&
    !(enterpriseMode && args.Length <= (checkEnterprise ? 4 : 3)))
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/SaltAndLedger.Demo -- data/content.json [--world [initialLogs] | --market | --pricing [--check] [output.json] | --shipping | --automation | --archipelago [--check] [output.json] | --enterprise [--check] [output.json]]");
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
    if (enterpriseMode)
    {
        var outputIndex = checkEnterprise ? 3 : 2;
        return EnterpriseExperiments.Run(content, args.Length > outputIndex ? args[outputIndex] : null, checkEnterprise);
    }
    if (worldMode || marketMode || shippingMode || automationMode || pricingMode || archipelagoMode)
        content = LegacyContent.Timber(content);
    if (worldMode) return WorldDemo.Run(content, initialLogs);
    if (marketMode) return MarketDemo.Run(content);
    if (shippingMode) return ShippingDemo.Run(content);
    if (automationMode) return AutomationDemo.Run(content);
    if (pricingMode)
    {
        var outputIndex = checkPricing ? 3 : 2;
        return PricingExperiments.Run(content, args.Length > outputIndex ? args[outputIndex] : null, checkPricing);
    }
    if (archipelagoMode)
    {
        var outputIndex = checkArchipelago ? 3 : 2;
        return ArchipelagoExperiments.Run(content, args.Length > outputIndex ? args[outputIndex] : null, checkArchipelago);
    }

    var inventory = new Inventory(content, new Dictionary<string, int> { ["logs"] = 6 });
    Console.WriteLine($"Loaded schema {content.SchemaVersion}: {content.Goods.Count} goods, {content.Buildings.Count} buildings, {content.Recipes.Count} recipes.");
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
