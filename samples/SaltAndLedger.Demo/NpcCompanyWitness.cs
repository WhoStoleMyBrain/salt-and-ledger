using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;

internal static class NpcCompanyWitness
{
    internal static int Run(ContentCatalog content, string? output, bool check)
    {
        var rows = new List<object>();
        foreach (var poor in new[] { false, true })
        {
            var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
            if (poor) json["npc"]!["startingCash"] = new JObject { ["min"] = 0, ["max"] = 0 };
            var config = WorldGenerationConfiguration.Parse(json.ToString());
            var identity = new RunIdentity(null, RunScenario.MerchantCharter, seed: 42,
                difficulty: RunDifficulty.SlowFoundations, generationConfiguration: config);
            var world = RunScenario.Create(content, identity);
            var initial = world.Snapshot();
            var replay = RunScenario.Create(content, identity);
            var production = 0L; var transfers = 0; var shipsOrdered = 0; var inputBatches = 0L;
            var shippedSales = 0L; var warehouseSales = 0L;
            for (var day = 0; day < 720; day++)
            {
                var result = world.AdvanceDay();
                if (day < 40 || day >= 710)
                {
                    replay.AdvanceDay();
                    if (RunDiagnosticReport.StateFingerprint(world.Snapshot()) != RunDiagnosticReport.StateFingerprint(replay.Snapshot()))
                        throw new InvalidOperationException("NPC continuation diverged.");
                }
                production += result.PrivateProduction.Count(p => p.Outcome == ScheduledProductionOutcome.Produced);
                inputBatches += result.PrivateProduction.Count(p => p.Outcome == ScheduledProductionOutcome.Produced &&
                    content.ForExpandingEconomy().GetRecipe(p.RecipeId).Inputs.Count > 0);
                transfers += result.Transfers.Count(t => t.Outcome == TransferOutcome.Transferred);
                shipsOrdered += result.Development.Count(d => d.Kind == DevelopmentKind.ShipCommission && d.Outcome == DevelopmentOutcome.Started);
                shippedSales += result.Trades.Where(t => t.Trade.Direction == TradeDirection.Sell).Sum(t => (long)t.Trade.ExecutedQuantity);
                warehouseSales += result.WarehouseTrades.Where(t => t.Trade.Direction == TradeDirection.Sell).Sum(t => (long)t.Trade.ExecutedQuantity);
                if (day == 0 || day == 1 || day == 3 || day == 39 || day == 359 || day == 709 || day == 719)
                {
                    var saved = WorldSaveCodec.Save(world);
                    replay = WorldSaveCodec.Load(content, saved);
                    if (saved != WorldSaveCodec.Save(replay)) throw new InvalidOperationException("NPC save round-trip changed state.");
                }
            }
            var state = world.Snapshot();
            if (state.TotalCash != initial.TotalCash || state.Economy!.MintedCurrency != 0) throw new InvalidOperationException("NPC money conservation failed.");
            if (poor ? production != 0 || shipsOrdered != 0 || state.PrivateFacilities.Count != 0 :
                production == 0 || inputBatches == 0 || shipsOrdered == 0 || transfers == 0 || shippedSales == 0 || warehouseSales == 0)
                throw new InvalidOperationException("NPC witness lacks required real activity or unaffordable control expanded.");
            rows.Add(new
            {
                Seed = 42, Horizon = 720, Unaffordable = poor, Towns = state.Ports.Count,
                ProductionBatches = production, InputBatches = inputBatches, Transfers = transfers, ShipsOrdered = shipsOrdered,
                ShippedSales = shippedSales, WarehouseSales = warehouseSales, MoneyConserved = true, SaveContinuationIdentical = true,
                Companies = state.Companies.Values.Where(c => c.Npc != null).Select(c => new
                {
                    c.Id, c.Name, c.Npc!.Profile, Specialization = c.Npc.GoodId,
                    Ships = state.Ships.Values.Count(s => s.OwnerId == c.Id),
                    Factories = state.PrivateFacilities.Values.Count(f => f.OwnerId == c.Id),
                    Warehouses = state.CompanyStockpiles.Values.Count(s => s.OwnerId == c.Id),
                    c.Cash, CapitalPaid = state.Accounting.Companies[c.Id].ConstructionExpenditure,
                    WagesPaid = state.Accounting.Companies[c.Id].WagesPaid, FeesPaid = state.Accounting.Companies[c.Id].FeesPaid
                }).ToArray(),
                Markets = state.Ports.Values.Select(p => new { p.Id, p.Inventory, Prices = p.Quotes.Keys.Select(g => state.GetMarketQuote(p.Id, g)).ToArray() }).ToArray(),
                FinalFingerprint = RunDiagnosticReport.StateFingerprint(state)
            });
        }
        var text = JsonConvert.SerializeObject(new { Format = 1, Cases = rows }, Formatting.Indented) + "\n";
        output = output ?? "docs/experiments/npc-companies-results.json";
        if (check)
        {
            if (!File.Exists(output) || File.ReadAllText(output) != text) throw new InvalidOperationException("NPC report is stale: " + output);
            Console.WriteLine("NPC 720-day witness and unaffordable control verified.");
        }
        else { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!); File.WriteAllText(output, text); Console.WriteLine(output); }
        return 0;
    }
}
