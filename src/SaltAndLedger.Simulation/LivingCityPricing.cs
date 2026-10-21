using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// One experimental household-coverage adapter. It changes immutable stock targets, never
    /// marginal mathematics. The regional owner freezes the result until the next daily boundary.
    /// </summary>
    public static class LivingCityPricing
    {
        public const int CoverageHorizonDays = 7;
        public const int MaximumTargetStock = 64;

        public static IReadOnlyDictionary<string, MarketQuote> Derive(RegionalPortConfiguration port, CitySnapshot city)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (port.Settlement == null || port.Settlement.PricingMode == LivingPricingMode.StockOnly)
                return port.Market.Quotes;

            var targets = new Dictionary<string, int>(StringComparer.Ordinal);
            var budget = city.HouseholdCash;
            foreach (var need in HouseholdNeeds(port))
            {
                if (!targets.ContainsKey(need.GoodId)) targets.Add(need.GoodId, 0);
                if (need.Kind == ConsumptionKind.Luxury && !city.LuxuryEnabled) continue;
                var opportunities = ((long)CoverageHorizonDays + need.IntervalDays - 1) / need.IntervalDays;
                var desired = (int)BigInteger.Min(MaximumTargetStock,
                    (BigInteger)DesiredQuantity(need, city.Population) * opportunities);
                var quote = port.Market.Quotes[need.GoodId];
                // The declared hypothetical shelf has at least the original target or the desired
                // coverage amount. This isolates the hypothesis from today's missing physical stock.
                // No forecast income is counted, and every good shares the same finite budget.
                var shelf = Math.Max(desired, quote.Pricing?.TargetStock ?? desired);
                var quantity = TradeQuantityCalculator.Maximum(quote, TradeDirection.Buy,
                    shelf, 0, 0, budget, int.MaxValue, desired);
                if (quantity > 0)
                    budget = checked(budget - quote.Price(new TradeCommand(TradeDirection.Buy, need.GoodId, quantity), shelf).TotalAmount);
                targets[need.GoodId] = Math.Min(MaximumTargetStock, checked(targets[need.GoodId] + quantity));
            }

            var result = new SortedDictionary<string, MarketQuote>(StringComparer.Ordinal);
            foreach (var entry in port.Market.Quotes)
            {
                var pricing = entry.Value.Pricing;
                if (pricing == null || !targets.TryGetValue(entry.Key, out var target))
                    result.Add(entry.Key, entry.Value);
                else
                    result.Add(entry.Key, new MarketQuote(entry.Key, new InventoryPricing(pricing.Model,
                        pricing.ReferencePrice, Math.Max(1, target), pricing.Sensitivity,
                        pricing.MinPrice, pricing.MaxPrice, pricing.Spread)));
            }
            return new ReadOnlyDictionary<string, MarketQuote>(result);
        }

        /// <summary>Nominal scheduled household need, with a ceiling and checked public quantity.</summary>
        public static int DesiredQuantity(ConsumptionConfiguration need, int population)
        {
            if (need == null) throw new ArgumentNullException(nameof(need));
            if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
            if (need.Kind == ConsumptionKind.Legacy || need.Kind == ConsumptionKind.Municipal)
                return need.Quantity;
            return checked((int)(((BigInteger)need.Quantity * population + need.PopulationPerUnit - 1) / need.PopulationPerUnit));
        }

        /// <summary>
        /// Floor of physical stock divided by mean scheduled household use. This is an explanatory
        /// quantity, not a prediction of purchases, wages, deliveries or the next due opportunity.
        /// Null means there is no currently eligible household need for this good.
        /// </summary>
        public static int? HouseholdCoverageDays(RegionalPortConfiguration port, CitySnapshot city, string goodId, int stock)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (city == null) throw new ArgumentNullException(nameof(city));
            if (stock < 0) throw new ArgumentOutOfRangeException(nameof(stock));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            var numerator = BigInteger.Zero;
            var denominator = BigInteger.One;
            foreach (var need in HouseholdNeeds(port).Where(need => need.GoodId == goodId &&
                (need.Kind != ConsumptionKind.Luxury || city.LuxuryEnabled)))
            {
                numerator = numerator * need.IntervalDays + (BigInteger)DesiredQuantity(need, city.Population) * denominator;
                denominator *= need.IntervalDays;
                var divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
                numerator /= divisor;
                denominator /= divisor;
            }
            if (numerator.IsZero) return null;
            return (int)BigInteger.Min(int.MaxValue, (BigInteger)stock * denominator / numerator);
        }

        private static IOrderedEnumerable<ConsumptionConfiguration> HouseholdNeeds(RegionalPortConfiguration port) =>
            port.Consumption.Where(need => need.Kind == ConsumptionKind.Essential || need.Kind == ConsumptionKind.Luxury)
                .OrderBy(need => need.Kind == ConsumptionKind.Essential ? 0 : 1)
                .ThenBy(need => need.Id, StringComparer.Ordinal);
    }
}
