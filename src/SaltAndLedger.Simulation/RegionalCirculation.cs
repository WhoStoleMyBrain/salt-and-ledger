using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public sealed partial class RegionalWorldState
    {
        private sealed partial class WorkingWorld
        {
            internal CirculationSnapshot? Circulation { get; private set; }

            private TradeAttempt PreviewLocalTrade(string ownerId, WorkingPort port, TradeAttempt gross) =>
                Circulation?.Preview(ownerId, port.Configuration.Id, port.City!.Cash, gross) ?? gross;

            private void ApplyServiceFee(string ownerId, string storeId, WorkingPort port, TradeAttempt attempt)
            {
                if (Circulation == null || !Circulation.Configuration.FeesEnabled ||
                    attempt.Outcome != TradeOutcome.Traded || attempt.Direction != TradeDirection.Sell) return;
                var portId = port.Configuration.Id;
                var key = CommercialCirculationSnapshot.Key(ownerId, portId);
                var previous = Circulation.Commercial[key];
                var next = new CommercialCirculationSnapshot(ownerId, portId,
                    checked(previous.GrossLocalSales + attempt.ExecutedAmount));
                var rows = Circulation.Commercial.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                rows[key] = next;
                Circulation = new CirculationSnapshot(Circulation.Configuration, rows.Values, Circulation.Towns.Values);
                port.City!.Cash = checked(port.City.Cash + attempt.HouseholdServiceFee);
                RecordFee(attempt.MarketServiceFee, "market:" + portId, CityCashTransferKind.ServiceFeeMarket);
                RecordFee(attempt.HouseholdServiceFee, "households:" + portId, CityCashTransferKind.ServiceFeeHouseholds);

                void RecordFee(long amount, string destination, CityCashTransferKind kind)
                {
                    if (amount == 0) return;
                    Accounting.PayServiceFee(ownerId, storeId, portId, attempt.GoodId, amount, destination);
                    CashTransfers.Add(new CityCashTransfer(kind, "company:" + ownerId, destination,
                        portId, amount, good: attempt.GoodId));
                }
            }

            internal void ReviewMunicipalBudgets(long nextDay)
            {
                if (Circulation == null) return;
                var towns = Circulation.Towns.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                foreach (var port in Ports.Values)
                {
                    var portId = port.Configuration.Id;
                    var prior = towns[portId];
                    if (nextDay < prior.NextReviewDay) continue;
                    var followingReview = checked(nextDay + CirculationConfiguration.ReviewIntervalDays);
                    var duePayroll = ReadyPayroll(port, nextDay);
                    var payrollReserve = Math.Max(duePayroll, ReadyPayroll(port, checked(followingReview - 1)));
                    var city = port.City!;
                    var food = port.Consumption.Single(c => c.Configuration.Kind == ConsumptionKind.Essential);
                    var foodReserve = LivingRetail.FoodBudget(port.Configuration, city.Population,
                        port.Quotes, port.Inventory.Snapshot(), food.DemandRemainder);
                    var desired = food.Configuration.PreviewDemand(city.Population, food.DemandRemainder).Quantity;
                    var stock = port.Inventory.GetQuantity(food.Configuration.GoodId);
                    var available = Math.Min(desired, stock);
                    var quote = port.Quotes[food.Configuration.GoodId];
                    var availableCost = available == 0 ? BigInteger.Zero : quote.Pricing != null
                        ? quote.Pricing.CalculateTotal(TradeDirection.Buy, stock, available)
                        : (BigInteger)quote.Ask * available;
                    long levy = 0, relief = 0;
                    var reason = MunicipalReviewReason.AccountsFunded;
                    if (!Circulation.Configuration.MunicipalEnabled)
                        reason = MunicipalReviewReason.Disabled;
                    else if (duePayroll > port.Cash && (BigInteger)city.Cash > 2 * foodReserve)
                    {
                        var surplus = (BigInteger)city.Cash - 2 * foodReserve;
                        levy = BoundedFiscalPayment((BigInteger)duePayroll - port.Cash, surplus);
                        reason = levy > 0 ? MunicipalReviewReason.PayrollLevy : MunicipalReviewReason.HouseholdFoodReserveProtected;
                        if (levy > 0)
                        {
                            city.Cash = checked(city.Cash - levy);
                            port.Cash = checked(port.Cash + levy);
                            CashTransfers.Add(new CityCashTransfer(CityCashTransferKind.MunicipalLevy,
                                "households:" + portId, "market:" + portId, portId, levy));
                        }
                    }
                    else if (availableCost > city.Cash)
                    {
                        var surplus = BigInteger.Max(0, (BigInteger)port.Cash - payrollReserve);
                        relief = BoundedFiscalPayment(availableCost - city.Cash, surplus);
                        reason = relief > 0 ? MunicipalReviewReason.FoodRelief : MunicipalReviewReason.MunicipalPayrollReserveProtected;
                        if (relief > 0)
                        {
                            port.Cash = checked(port.Cash - relief);
                            city.Cash = checked(city.Cash + relief);
                            CashTransfers.Add(new CityCashTransfer(CityCashTransferKind.FoodRelief,
                                "market:" + portId, "households:" + portId, portId, relief,
                                good: food.Configuration.GoodId));
                        }
                    }
                    else if (duePayroll > port.Cash)
                        reason = MunicipalReviewReason.HouseholdFoodReserveProtected;
                    else if (available == 0)
                        reason = MunicipalReviewReason.NoFoodStock;
                    else if (duePayroll == 0)
                        reason = MunicipalReviewReason.NoProductivePayroll;
                    towns[portId] = new MunicipalCirculationSnapshot(portId, followingReview, nextDay,
                        checked(prior.TotalLevy + levy), checked(prior.TotalRelief + relief), levy, relief,
                        payrollReserve, duePayroll, (long)BigInteger.Min(long.MaxValue, foodReserve), reason);
                }
                Circulation = new CirculationSnapshot(Circulation.Configuration, Circulation.Commercial.Values, towns.Values);
            }

            private static long BoundedFiscalPayment(BigInteger shortfall, BigInteger surplus) =>
                (long)BigInteger.Min(CirculationConfiguration.MaximumFiscalTransfer,
                    BigInteger.Min(shortfall, surplus / CirculationConfiguration.SurplusDenominator));

            private long ReadyPayroll(WorkingPort port, long horizon)
            {
                var stock = new Inventory(content, port.Inventory.Snapshot(), port.Inventory.QuantityScale);
                long wages = 0;
                foreach (var facility in port.Facilities)
                {
                    if (PreviewPublicBatch(stock, facility, horizon, long.MaxValue) != ScheduledProductionOutcome.Produced) continue;
                    var instance = facility.Configuration.Instance;
                    if (!Simulation.Production.TryRunBatch(stock, instance.BuildingId, instance.RecipeId))
                        throw new InvalidOperationException("Ready public batch and shadow production disagree.");
                    wages = checked(wages + facility.Configuration.WagePerBatch);
                }
                return wages;
            }

            private ScheduledProductionOutcome PreviewPublicBatch(Inventory inventory, ScheduledFacilitySnapshot facility,
                long attemptDay, long employerCash)
            {
                if (attemptDay < facility.NextDueDay) return ScheduledProductionOutcome.NotDue;
                if (!facility.Enabled) return ScheduledProductionOutcome.Disabled;
                var config = facility.Configuration;
                var recipe = content.GetRecipe(config.Instance.RecipeId);
                var scale = inventory.QuantityScale;
                if (recipe.Inputs.Any(i => inventory.GetQuantity(i.GoodId) < checked(i.Quantity * scale)))
                    return ScheduledProductionOutcome.InputShortage;
                if (config.OutputStockLimit.HasValue && recipe.Outputs.Any(output =>
                    (long)inventory.GetQuantity(output.GoodId) -
                    recipe.Inputs.Where(i => i.GoodId == output.GoodId).Sum(i => (long)checked(i.Quantity * scale)) +
                    checked(output.Quantity * scale) > config.OutputStockLimit.Value))
                    return ScheduledProductionOutcome.OutputLimit;
                return employerCash < config.WagePerBatch ? ScheduledProductionOutcome.PayrollShortage : ScheduledProductionOutcome.Produced;
            }
        }
    }
}
