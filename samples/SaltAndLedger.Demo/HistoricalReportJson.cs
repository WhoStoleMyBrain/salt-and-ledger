using System;
using System.Text.Json.Serialization.Metadata;
using SaltAndLedger.Simulation;

/// <summary>Keep the established report shapes when optional circulation data is absent.</summary>
internal static class HistoricalReportJson
{
    internal static IJsonTypeInfoResolver Resolver()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(type =>
        {
            foreach (var property in type.Properties)
            {
                if (type.Type == typeof(CompanyFinancialSnapshot) &&
                    (property.Name == "feesPaid" || property.Name == "operatingSurplusAfterExpenses"))
                    property.ShouldSerialize = (owner, _) => ((CompanyFinancialSnapshot)owner).FeesPaid != 0;
                else if (type.Type == typeof(TradeAttempt) &&
                    (property.Name == "serviceFee" || property.Name == "marketServiceFee" ||
                     property.Name == "householdServiceFee" || property.Name == "netProceeds"))
                    property.ShouldSerialize = (owner, _) => ((TradeAttempt)owner).ServiceFee != 0;
                else if (type.Type == typeof(RunIdentity) && property.Name == "economicRulesVersion")
                    property.ShouldSerialize = (owner, _) => ((RunIdentity)owner).EconomicRulesVersion != 0;
                else if (type.Type == typeof(ExpandingEconomyGeneration) && property.Name == "economicRulesVersion")
                    property.ShouldSerialize = (owner, _) => ((ExpandingEconomyGeneration)owner).EconomicRulesVersion != 0;
                else if (type.Type == typeof(RegionalWorldSnapshot) && property.Name == "circulation")
                    property.ShouldSerialize = (_, value) => value != null;
            }
        });
        return resolver;
    }
}
