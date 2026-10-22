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
var industrialMode = args.Length >= 2 && args[1] == "--industrial-logistics";
var checkIndustrial = industrialMode && args.Length >= 3 && args[2] == "--check";
var runsMode = args.Length >= 2 && args[1] == "--runs";
var checkRuns = runsMode && args.Length >= 3 && args[2] == "--check";
var seededMode = args.Length >= 2 && args[1] == "--seeded-runs";
var seededIndex = 2;
var deepSeeded = seededMode && args.Length > seededIndex && args[seededIndex] == "--deep";
if (deepSeeded) seededIndex++;
var checkSeeded = seededMode && args.Length > seededIndex && args[seededIndex] == "--check";
if (checkSeeded) seededIndex++;
var validSeeded = seededMode && args.Length <= seededIndex + 1 &&
    (args.Length <= seededIndex || !args[seededIndex].StartsWith("--", StringComparison.Ordinal));
var livingMode = args.Length >= 2 && args[1] == "--living-cities";
var livingIndex = 2;
var deepLiving = livingMode && args.Length > livingIndex && args[livingIndex] == "--deep";
if (deepLiving) livingIndex++;
var checkLiving = livingMode && args.Length > livingIndex && args[livingIndex] == "--check";
if (checkLiving) livingIndex++;
var validLiving = livingMode && args.Length <= livingIndex + 1 &&
    (args.Length <= livingIndex || !args[livingIndex].StartsWith("--", StringComparison.Ordinal));
var expandingMode = args.Length >= 2 && args[1] == "--expanding-economy";
var expandingIndex = 2;
var reinvestmentExpanding = expandingMode && args.Length > expandingIndex && args[expandingIndex] == "--reinvestment";
if (reinvestmentExpanding) expandingIndex++;
var deepExpanding = expandingMode && args.Length > expandingIndex && args[expandingIndex] == "--deep";
if (deepExpanding) expandingIndex++;
var checkExpanding = expandingMode && args.Length > expandingIndex && args[expandingIndex] == "--check";
if (checkExpanding) expandingIndex++;
var validExpanding = expandingMode && args.Length <= expandingIndex + 1 &&
    (args.Length <= expandingIndex || !args[expandingIndex].StartsWith("--", StringComparison.Ordinal));
if (args.Length != 1 && !(worldMode && args.Length <= 3) && !marketMode && !shippingMode && !automationMode &&
    !(pricingMode && args.Length <= (checkPricing ? 4 : 3)) &&
    !(archipelagoMode && args.Length <= (checkArchipelago ? 4 : 3)) &&
    !(enterpriseMode && args.Length <= (checkEnterprise ? 4 : 3)) &&
    !(industrialMode && args.Length <= (checkIndustrial ? 4 : 3)) &&
    !(runsMode && args.Length <= (checkRuns ? 4 : 3)) && !validSeeded && !validLiving && !validExpanding)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/SaltAndLedger.Demo -- data/content.json [--world [initialLogs] | --market | --pricing [--check] [output.json] | --shipping | --automation | --archipelago [--check] [output.json] | --enterprise [--check] [output.json] | --industrial-logistics [--check] [output.json] | --runs [--check] [output.json] | --seeded-runs [--deep] [--check] [output.json] | --living-cities [--deep] [--check] [output.json] | --expanding-economy [--reinvestment] [--deep] [--check] [output.json]]");
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
    if (expandingMode)
        return ExpandingEconomyExperiments.Run(content, args.Length > expandingIndex ? args[expandingIndex] : null,
            checkExpanding, deepExpanding, reinvestmentExpanding);
    if (livingMode)
        return LivingCityExperiments.Run(content, args.Length > livingIndex ? args[livingIndex] : null, checkLiving, deepLiving);
    if (seededMode)
        return SeededRunExperiments.Run(content, args.Length > seededIndex ? args[seededIndex] : null, checkSeeded, deepSeeded);
    if (industrialMode)
    {
        var outputIndex = checkIndustrial ? 3 : 2;
        return IndustrialLogisticsExperiments.Run(content, args.Length > outputIndex ? args[outputIndex] : null, checkIndustrial);
    }
    if (runsMode)
    {
        var outputIndex = checkRuns ? 3 : 2;
        return RunExperiments.Run(content, args.Length > outputIndex ? args[outputIndex] : null, checkRuns);
    }
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
