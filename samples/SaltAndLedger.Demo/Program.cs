using System;
using System.IO;
using System.Linq;
using SaltAndLedger.Simulation;

const string usage = "Usage: dotnet run --project samples/SaltAndLedger.Demo -- data/content.json [--grand-commission [--check] | --economic-circulation [--check] [output.json] | --expanding-economy [--reinvestment] [--deep] [--check] [output.json]]";
if (args.Length == 0) { Console.Error.WriteLine(usage); return 1; }
try
{
    var content = ContentLoader.Load(File.ReadAllText(args[0]));
    var mode = args.Length == 1 ? "--grand-commission" : args[1];
    var options = args.Skip(2).ToList();
    var reinvestment = options.Remove("--reinvestment");
    var deep = options.Remove("--deep");
    var check = options.Remove("--check");
    if (options.Count > 1 || options.Any(v => v.StartsWith("--", StringComparison.Ordinal)) ||
        (mode != "--expanding-economy" && (reinvestment || deep)))
        throw new ArgumentException(usage);
    switch (mode)
    {
        case "--grand-commission" when options.Count == 0:
            return GrandCommissionWitness.Run(content);
        case "--economic-circulation":
            return EconomicCirculationExperiments.Run(content, options.SingleOrDefault(), check);
        case "--expanding-economy":
            return ExpandingEconomyExperiments.Run(content, options.SingleOrDefault(), check, deep, reinvestment);
        default: throw new ArgumentException(usage);
    }
}
catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
    exception is ContentValidationException || exception is OverflowException ||
    exception is ArgumentException || exception is InvalidOperationException)
{
    Console.Error.WriteLine("Verification failed: " + exception.Message);
    return 1;
}
