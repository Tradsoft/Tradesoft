using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Engine.Execution;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class SimulatedBrokerTests
    {
        private static BacktestConfig DefaultConfig() => new()
        {
            Symbol = "EURUSD",
            CommissionType = CommissionType.Fixed,
            CommissionPerLot = 5.0,
            LotSize = 100_000,
            SlippagePips = 1.0,
            TickSize = 0.0001
        };

        [Fact]
        public void Broker_ShouldExecuteMarketOrderAfterLatency_WithSlippageAndCommission()
        {
            var broker = new SimulatedBroker(DefaultConfig());
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            var ctx1 = new TickContext();
            ctx1.Reset(new TickData(t0.Ticks, 1.1000, 1.1002, 100));
            ctx1.NewOrders.Add(new Order
            {
                Symbol = "EURUSD",
                Side = Side.Buy,
                Type = OrderType.Market,
                Quantity = 50_000
            });
            broker.Execute(ctx1);
            Assert.Empty(ctx1.Trades); // Latence réseau 20ms non écoulée

            var ctx2 = new TickContext();
            ctx2.Reset(new TickData(t0.AddMilliseconds(25).Ticks, 1.1005, 1.1007, 100));
            broker.Execute(ctx2);

            var trade = Assert.Single(ctx2.Trades);
            Assert.Equal(Side.Buy, trade.Side);
            Assert.Equal(1.1008, trade.Price, 5); // 1.1007 + 1 pip slippage
            Assert.Equal(2.5, trade.Commission, 5); // 5.0 * (50_000 / 100_000)
        }

        [Fact]
        public void Broker_ShouldExecuteStopOrder_WhenStopLevelIsReached()
        {
            var config = DefaultConfig();
            config.SlippagePips = 0;
            var broker = new SimulatedBroker(config);
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            var ctx1 = new TickContext();
            ctx1.Reset(new TickData(t0.Ticks, 1.1000, 1.1002, 100));
            ctx1.NewOrders.Add(new Order
            {
                Id = 1,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Type = OrderType.Stop,
                Price = 1.1050,
                Quantity = 100_000
            });
            broker.Execute(ctx1);

            var ctx2 = new TickContext();
            ctx2.Reset(new TickData(t0.AddMilliseconds(30).Ticks, 1.1030, 1.1032, 100));
            broker.Execute(ctx2);
            Assert.Empty(ctx2.Trades);

            var ctx3 = new TickContext();
            ctx3.Reset(new TickData(t0.AddMilliseconds(50).Ticks, 1.1049, 1.1051, 100));
            broker.Execute(ctx3);

            var trade = Assert.Single(ctx3.Trades);
            Assert.Equal(1.1051, trade.Price, 5);
        }

        [Fact]
        public void Broker_ShouldTriggerTakeProfitAndCancelStopLoss_AndCleanUpOnOppositeEntry()
        {
            var config = DefaultConfig();
            config.SlippagePips = 0;
            var broker = new SimulatedBroker(config);
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            // 1. Ouvre un Buy avec SL=1.0900 et TP=1.1100
            var ctx1 = new TickContext();
            ctx1.Reset(new TickData(t0.Ticks, 1.1000, 1.1001, 100));
            ctx1.NewOrders.Add(new Order
            {
                Id = 10,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Type = OrderType.Market,
                Quantity = 100_000,
                StopLoss = 1.0900,
                TakeProfit = 1.1100
            });
            broker.Execute(ctx1);

            var ctxFill = new TickContext();
            ctxFill.Reset(new TickData(t0.AddMilliseconds(25).Ticks, 1.1000, 1.1001, 100));
            broker.Execute(ctxFill);
            Assert.Single(ctxFill.Trades);

            // 2. Envoie un ordre Sell opposé (retournement) -> doit nettoyer les anciens SL/TP du Buy
            var ctxRev = new TickContext();
            ctxRev.Reset(new TickData(t0.AddMilliseconds(30).Ticks, 1.1000, 1.1001, 100));
            ctxRev.NewOrders.Add(new Order
            {
                Id = 11,
                Symbol = "EURUSD",
                Side = Side.Sell,
                Type = OrderType.Market,
                Quantity = 100_000,
                StopLoss = 1.1100,
                TakeProfit = 1.0900
            });
            broker.Execute(ctxRev);

            var ctxRevFill = new TickContext();
            ctxRevFill.Reset(new TickData(t0.AddMilliseconds(55).Ticks, 1.1000, 1.1001, 100));
            broker.Execute(ctxRevFill);
            Assert.Single(ctxRevFill.Trades);

            // 3. Le prix baisse à 1.0890 -> doit déclencher uniquement le TP du Sell (1.0900), pas l'ancien SL du Buy !
            var ctxTp = new TickContext();
            ctxTp.Reset(new TickData(t0.AddMilliseconds(80).Ticks, 1.0889, 1.0890, 100));
            broker.Execute(ctxTp);

            var exitTrade = Assert.Single(ctxTp.Trades);
            Assert.Equal(Side.Buy, exitTrade.Side);

            // 4. Si le prix remonte ensuite à 1.1150, le SL du Sell (qui était partenaire OCO du TP) ne doit plus se déclencher
            var ctxAfter = new TickContext();
            ctxAfter.Reset(new TickData(t0.AddMilliseconds(100).Ticks, 1.1150, 1.1151, 100));
            broker.Execute(ctxAfter);
            Assert.Empty(ctxAfter.Trades);
        }
    }
}
