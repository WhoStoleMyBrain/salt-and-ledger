using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    // One exact purchase proposal shared by snapshot inspection and the staged consumption phase.
    internal static class LivingRetail
    {
        internal static BigInteger FoodBudget(RegionalPortConfiguration configuration, int population,
            IReadOnlyDictionary<string, MarketQuote> quotes, IReadOnlyDictionary<string, int> inventory,
            int remainder = 0)
        {
            var rule = configuration.Consumption.Single(c => c.Kind == ConsumptionKind.Essential);
            var desired = LivingCityPricing.DesiredQuantity(rule, population, remainder);
            if (desired == 0) return BigInteger.Zero;
            var quote = quotes[rule.GoodId];
            var stock = Math.Max(desired, inventory[rule.GoodId]);
            return quote.Pricing != null ? quote.Pricing.CalculateTotal(TradeDirection.Buy, stock, desired) :
                (BigInteger)desired * quote.Ask;
        }
        internal static RegionalConsumption Preview(RegionalPortConfiguration configuration, CitySnapshot city,
            ConsumptionConfiguration rule, IReadOnlyDictionary<string, MarketQuote> quotes,
            IReadOnlyDictionary<string, int> inventory, long marketCash, int remainder = 0, int foodRemainder = 0)
        {
            var desired = rule.Kind == ConsumptionKind.Luxury && !city.LuxuryEnabled ? 0 :
                LivingCityPricing.DesiredQuantity(rule, city.Population, remainder);
            var available = Math.Min(desired, inventory[rule.GoodId]);
            if (rule.Kind == ConsumptionKind.Municipal)
                return new RegionalConsumption(configuration.Id, rule, available, desired, unmetStock: desired - available);
            var budget = city.HouseholdCash;
            if (rule.Kind == ConsumptionKind.Luxury)
                budget = (long)BigInteger.Max(0, (BigInteger)budget - FoodBudget(configuration, city.Population,
                    quotes, inventory, foodRemainder));
            var quantity = desired == 0 ? 0 : TradeQuantityCalculator.Maximum(quotes[rule.GoodId], TradeDirection.Buy,
                inventory[rule.GoodId], 0, 0, budget, int.MaxValue, desired);
            // Receiver overflow is exceptional, not mislabeled household poverty. Complete day rolls back.
            var payment = quantity == 0 ? 0 : TradeCalculator.Preview(quotes[rule.GoodId],
                new TradeCommand(TradeDirection.Buy, rule.GoodId, quantity), inventory[rule.GoodId], 0,
                marketCash, city.HouseholdCash, int.MaxValue).ExecutedAmount;
            return new RegionalConsumption(configuration.Id, rule, quantity, desired, payment,
                desired - available, available - quantity);
        }
    }
}
