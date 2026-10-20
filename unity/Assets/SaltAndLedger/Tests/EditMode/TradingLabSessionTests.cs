using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class TradingLabSessionTests
    {
        private TradingLabSession Create(TradingLabScenario scenario = TradingLabScenario.Standard)
        {
            var asset = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(asset, Is.Not.Null, "Run python tools/prepare_unity.py before opening Unity.");
            var canonical = ContentLoader.Load(asset.text);
            var session = new TradingLabSession(canonical, scenario);
            Assert.That(session.Content.Goods.Select(good => good.Id), Is.EquivalentTo(new[] { "logs", "planks" }));
            Assert.That(session.Content.GetGood("logs"), Is.SameAs(canonical.GetGood("logs")));
            Assert.That(session.Content.GetRecipe("saw_planks"), Is.SameAs(canonical.GetRecipe("saw_planks")));
            return session;
        }

        [Test]
        public void InitialStateAndRemoteInspectionUseCanonicalDataWithoutChangingSnapshot()
        {
            var session = Create();
            var before = session.Snapshot;
            Assert.That(before.Tick, Is.Zero);
            Assert.That(before.TraderCash, Is.EqualTo(100L));
            Assert.That(before.Ship.DockedPortId, Is.EqualTo(TradingLabScenarios.North));
            Assert.That(before.Ship.CargoUnits, Is.Zero);
            Assert.That(before.Ship.CargoCapacity, Is.EqualTo(6));
            Assert.That(before.Ports[TradingLabScenarios.North].Inventory["logs"], Is.EqualTo(18));
            Assert.That(before.Ports[TradingLabScenarios.South].Inventory["logs"], Is.EqualTo(2));
            Assert.That(session.Content.GetGood("logs").Name, Is.Not.Empty);
            Assert.That(session.AutomationEnabled, Is.False);
            var preview = session.PreviewTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 6);
            Assert.That(preview.Price.FirstMarginalPrice, Is.EqualTo(8L));
            Assert.That(preview.Price.LastMarginalPrice, Is.EqualTo(10L));
            Assert.That(preview.Price.TotalAmount, Is.EqualTo(54L));
            Assert.That(preview.Price.TotalAmount, Is.Not.EqualTo(6 * preview.Price.FirstMarginalPrice));
            session.PreviewTrade(TradingLabScenarios.South, TradeDirection.Buy, "logs", 1);
            before.GetMarketQuote(TradingLabScenarios.South, "planks");
            Assert.That(session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.Disabled));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.RealizedGrossProfit, Is.Null);
            Assert.Throws<InvalidOperationException>(() =>
                session.ExecuteTrade(TradingLabScenarios.South, TradeDirection.Buy, "logs", 1));
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        [TestCase(TradingLabScenario.Standard, 219L, 990L, 891L, 12, 2)]
        [TestCase(TradingLabScenario.Restrictive, 169L, 952L, 979L, 16, 0)]
        public void PresetsCompleteTheMeasuredSixTickEmptyCargoInterval(TradingLabScenario scenario,
            long traderCash, long northCash, long southCash, int northLogs, int southPlanks)
        {
            var session = Create(scenario);
            session.SetAutomationEnabled(true);
            var run = session.AdvanceAutomation(6);
            Assert.That(run.StepsExecuted, Is.EqualTo(6));
            Assert.That(run.StopReason, Is.EqualTo(AutomationStopReason.TickLimit));
            Assert.That(session.Snapshot.Tick, Is.EqualTo(6L));
            Assert.That(session.Snapshot.TraderCash, Is.EqualTo(traderCash));
            Assert.That(session.RealizedGrossProfit, Is.EqualTo(traderCash - 100));
            Assert.That(session.Snapshot.Ship.DockedPortId, Is.EqualTo(TradingLabScenarios.North));
            Assert.That(session.Snapshot.Ship.CargoUnits, Is.Zero);
            Assert.That(session.Snapshot.Ports[TradingLabScenarios.North].MarketCash, Is.EqualTo(northCash));
            Assert.That(session.Snapshot.Ports[TradingLabScenarios.South].MarketCash, Is.EqualTo(southCash));
            Assert.That(session.Snapshot.Ports[TradingLabScenarios.North].Inventory,
                Is.EquivalentTo(new Dictionary<string, int> { ["logs"] = northLogs, ["planks"] = 2 }));
            Assert.That(session.Snapshot.Ports[TradingLabScenarios.South].Inventory,
                Is.EquivalentTo(new Dictionary<string, int> { ["logs"] = 0, ["planks"] = southPlanks }));
            Assert.That(session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.BuyPriceLimit));
            Assert.That(session.Activity.Any(item => item.Kind == "Travel" && item.Message.Contains("Arrived")), Is.True);
        }

        [Test]
        public void ManualPurchaseDepartureArrivalAndSalesMatchTheMeasuredExpedition()
        {
            var session = Create();
            session.ExecuteTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 6, departAfter: true);
            Assert.That(session.Snapshot.Tick, Is.EqualTo(1L));
            Assert.That(session.Snapshot.Ship.DockedPortId, Is.Null);
            Assert.That(session.Snapshot.Ship.RemainingTravelTicks, Is.EqualTo(1));
            Assert.That(session.Snapshot.Ship.Cargo["logs"], Is.EqualTo(6));
            Assert.That(session.Snapshot.TraderCash, Is.EqualTo(46L));
            Assert.That(session.RealizedGrossProfit, Is.Null);
            var sailing = session.Snapshot;
            Assert.Throws<InvalidOperationException>(() =>
                session.ExecuteTrade(TradingLabScenarios.South, TradeDirection.Sell, "logs", 6));
            Assert.That(session.Snapshot, Is.SameAs(sailing));
            session.NextTick();
            Assert.That(session.Snapshot.Ship.DockedPortId, Is.EqualTo(TradingLabScenarios.South));
            Assert.That(session.Snapshot.Ship.RemainingTravelTicks, Is.Zero);
            session.ExecuteTrade(TradingLabScenarios.South, TradeDirection.Sell, "logs", 6);
            session.ExecuteTrade(TradingLabScenarios.South, TradeDirection.Buy, "planks", 2, departAfter: true);
            session.NextTick();
            session.ExecuteTrade(TradingLabScenarios.North, TradeDirection.Sell, "planks", 2);
            Assert.That(session.Snapshot.Tick, Is.EqualTo(6L));
            Assert.That(session.Snapshot.TraderCash, Is.EqualTo(219L));
            Assert.That(session.Snapshot.Ship.CargoUnits, Is.Zero);
            Assert.That(session.RealizedGrossProfit, Is.EqualTo(119L));
        }

        [Test]
        public void GuardedBuyAndDepartCancelsBeforeTickWhenPurchaseCannotExecute()
        {
            var session = Create();
            var before = session.Snapshot;
            Assert.That(session.PreviewTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 7).Outcome,
                Is.EqualTo(TradeOutcome.InsufficientCargoCapacity));
            var error = Assert.Throws<InvalidOperationException>(() =>
                session.ExecuteTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 7, departAfter: true));
            StringAssert.Contains("capacity", error.Message);
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.PreviewTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 0));
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void OrdinaryRejectedTradeStillUsesExistingCompletedTickSemantics()
        {
            var session = Create();
            var result = session.ExecuteTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 7);
            Assert.That(result.TradeAttempts[0].Trade.Outcome, Is.EqualTo(TradeOutcome.InsufficientCargoCapacity));
            Assert.That(session.Snapshot.Tick, Is.EqualTo(1L));
            Assert.That(session.Snapshot.TraderCash, Is.EqualTo(100L));
            Assert.That(session.Snapshot.Ship.CargoUnits, Is.Zero);
            Assert.That(session.Snapshot.Ship.DockedPortId, Is.EqualTo(TradingLabScenarios.North));
            Assert.That(session.Snapshot.Ports[TradingLabScenarios.South].Inventory["planks"], Is.EqualTo(1));
            Assert.That(session.Activity.Any(item => item.Kind == "Rejected" && item.Message.Contains("capacity")), Is.True);
        }

        [Test]
        public void ApplyingLessRestrictivePolicyRecoversWithoutChangingWorldUntilNextTick()
        {
            var session = Create();
            var before = session.Snapshot;
            session.ApplyPolicy(Policy(maxBuy: 7));
            session.SetAutomationEnabled(true);
            var blocked = session.CurrentDecision;
            Assert.That(blocked.Reason, Is.EqualTo(AutomationReason.BuyPriceLimit));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.ApplyPolicy(Policy(maxBuy: 10));
            var ready = session.CurrentDecision;
            Assert.That(ready, Is.Not.SameAs(blocked));
            Assert.That(ready.Before, Is.SameAs(before));
            Assert.That(ready.Action, Is.EqualTo(AutomationAction.BuyAndDepart));
            Assert.That(ready.SelectedQuantity, Is.EqualTo(6));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.NextTick();
            Assert.That(session.Snapshot.Tick, Is.EqualTo(1L));
            Assert.That(session.Snapshot.Ship.Cargo["logs"], Is.EqualTo(6));
        }

        [Test]
        public void EnablementPauseAndManualInterventionConsumeNoImplicitTicks()
        {
            var session = Create();
            var before = session.Snapshot;
            session.SetAutomationEnabled(true);
            Assert.Throws<InvalidOperationException>(() =>
                session.ExecuteTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 2));
            Assert.Throws<InvalidOperationException>(() => session.Sail(TradingLabScenarios.South));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.SetAutomationEnabled(false);
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.AdvanceAutomation(6).StopReason, Is.EqualTo(AutomationStopReason.Disabled));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.ExecuteTrade(TradingLabScenarios.North, TradeDirection.Buy, "logs", 2);
            var purchased = session.Snapshot;
            session.ApplyPolicy(new TradeRoutePolicy(TradingLabScenarios.North, TradingLabScenarios.South,
                new RouteLeg("logs", 2, 2, 8, 19), session.Policy.ReturnLeg));
            session.SetAutomationEnabled(true);
            Assert.That(session.Snapshot, Is.SameAs(purchased));
            Assert.That(session.CurrentDecision.Action, Is.EqualTo(AutomationAction.Depart));
            session.NextTick();
            Assert.That(session.Snapshot.Tick, Is.EqualTo(2L));
            Assert.That(session.Snapshot.Ship.Cargo["logs"], Is.EqualTo(2));
            Assert.That(session.Snapshot.Ship.DestinationPortId, Is.EqualTo(TradingLabScenarios.South));
        }

        [Test]
        public void NoDestinationLiquidityRetainsCargoAndCannotBeFixedByLoweringSaleLimit()
        {
            var session = Create(TradingLabScenario.NoDestinationCash);
            session.SetAutomationEnabled(true);
            var run = session.AdvanceAutomation(6);
            Assert.That(run.StopReason, Is.EqualTo(AutomationStopReason.NoProgress));
            Assert.That(run.StepsExecuted, Is.EqualTo(5));
            Assert.That(session.Snapshot.Tick, Is.EqualTo(5L));
            Assert.That(session.Snapshot.Ship.DockedPortId, Is.EqualTo(TradingLabScenarios.South));
            Assert.That(session.Snapshot.Ship.Cargo["logs"], Is.EqualTo(6));
            Assert.That(session.Snapshot.TraderCash, Is.EqualTo(46L));
            Assert.That(session.CashChange, Is.EqualTo(-54L));
            Assert.That(session.RealizedGrossProfit, Is.Null);
            Assert.That(session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.InsufficientMarketFunds));
            StringAssert.Contains("cannot pay", TradingLabText.Reason(session.CurrentDecision.Reason));
            var before = session.Snapshot;
            session.ApplyPolicy(Policy(minSell: 1));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.InsufficientMarketFunds));
            session.NextTick();
            Assert.That(session.Snapshot.Ports[TradingLabScenarios.South].MarketCash, Is.Zero);
            Assert.That(session.Snapshot.Ship.Cargo["logs"], Is.EqualTo(6));
            Assert.That(session.Snapshot.TraderCash, Is.EqualTo(46L));
        }

        [Test]
        public void ResetRestoresFixturePolicyAndHostStateButDoesNotMutateOldSnapshot()
        {
            var session = Create();
            session.SetAutomationEnabled(true);
            session.AdvanceAutomation(6);
            var completed = session.Snapshot;
            session.Reset(TradingLabScenario.Restrictive);
            Assert.That(session.Snapshot.Tick, Is.Zero);
            Assert.That(session.Snapshot.TraderCash, Is.EqualTo(100L));
            Assert.That(session.Snapshot.Ship.CargoUnits, Is.Zero);
            Assert.That(session.AutomationEnabled, Is.False);
            Assert.That(session.Policy.Outbound.MaxBuyPrice, Is.EqualTo(8L));
            Assert.That(session.RealizedGrossProfit, Is.Null);
            Assert.That(session.Activity.Count, Is.EqualTo(1));
            Assert.That(completed.Tick, Is.EqualTo(6L));
            Assert.That(completed.TraderCash, Is.EqualTo(219L));
            var reset = session.Snapshot;
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Reset((TradingLabScenario)99));
            Assert.That(session.Snapshot, Is.SameAs(reset));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(21)]
        [TestCase(int.MaxValue)]
        public void InvalidRunBoundsNeverConsumeTime(int ticks)
        {
            var session = Create();
            var before = session.Snapshot;
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceTicks(ticks));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceAutomation(ticks));
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void ActivityIsBoundedAndIdleProductionMessagesAreNotRepeated()
        {
            var session = Create();
            session.AdvanceTicks(20);
            Assert.That(session.Snapshot.Tick, Is.EqualTo(20L));
            Assert.That(session.Activity.Count(item => item.Kind == "Production" && item.Message.Contains("insufficient inputs")), Is.EqualTo(1));
            for (var i = 0; i < 60; i++) session.SetAutomationEnabled(i % 2 == 0);
            Assert.That(session.Activity.Count, Is.EqualTo(TradingLabSession.MaximumActivity));
            Assert.That(session.Snapshot.Tick, Is.EqualTo(20L));
        }

        [Test]
        public void NextTickIsExactlyOneEvenWhenPolicyPausesButRunnerDoesNotStepAPause()
        {
            var session = Create();
            session.ApplyPolicy(new TradeRoutePolicy(TradingLabScenarios.North, TradingLabScenarios.South,
                new RouteLeg("logs", 7, 7, 10, 19), session.Policy.ReturnLeg));
            session.SetAutomationEnabled(true);
            Assert.That(session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.MinimumExceedsCapacity));
            Assert.That(session.AdvanceAutomation(6).StepsExecuted, Is.Zero);
            Assert.That(session.Snapshot.Tick, Is.Zero);
            session.NextTick();
            Assert.That(session.Snapshot.Tick, Is.EqualTo(1L));
            Assert.That(session.Snapshot.Ship.CargoUnits, Is.Zero);
            Assert.That(session.Snapshot.Ports[TradingLabScenarios.South].Inventory["planks"], Is.EqualTo(1));
        }

        [Test]
        public void InvalidPolicyReferencePreservesBothPolicyAndWorld()
        {
            var session = Create();
            var before = session.Snapshot;
            var policy = session.Policy;
            var invalid = new TradeRoutePolicy(TradingLabScenarios.North, "missing_port",
                policy.Outbound, policy.ReturnLeg);
            Assert.Throws<KeyNotFoundException>(() => session.ApplyPolicy(invalid));
            Assert.That(session.Policy, Is.SameAs(policy));
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        private static TradeRoutePolicy Policy(long maxBuy = 10, long minSell = 19)
        {
            return new TradeRoutePolicy(TradingLabScenarios.North, TradingLabScenarios.South,
                new RouteLeg("logs", 6, 2, maxBuy, minSell), new RouteLeg("planks", 2, 2, 18, 31));
        }
    }
}
