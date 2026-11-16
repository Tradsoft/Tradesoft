using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Engine.Management;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class RiskManagerTests
    {
        private static BacktestConfig DefaultConfig() => new()
        {
            Symbol = "EURUSD",
            InitialCash = 10_000,
            Leverage = 30.0,
            MaxRiskPerTradePercent = 1.0,
            MaxDrawdownPercent = 10.0,
            MaxDailyLossPercent = 5.0,
            MaxConcurrentPositions = 1,
            MinRiskRewardRatio = 2.0,
            MinLotSize = 1.0,
            LotSize = 100_000,
            DefaultRiskNoSL = 0.05,
            DefaultLeverageNoSL = 2.0
        };

        private static TickContext MakeContext(DateTime time, double bid, double ask, Order order)
        {
            var ctx = new TickContext();
            ctx.Reset(new TickData(time.Ticks, bid, ask, 100, TickFlags.Trade));
            ctx.NewOrders.Add(order);
            return ctx;
        }

        [Fact]
        public void RiskManager_ShouldUseDefaultRiskNoSLAndDefaultLeverageNoSL_WhenOrderHasNoStopLoss()
        {
            var config = DefaultConfig();
            var pm = new PortfolioManager(config.InitialCash);
            var logs = new List<LogEntryDTO>();
            var rm = new RiskManager(config, pm, logs);

            var order = new Order
            {
                Id = 1,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Type = OrderType.Market,
                Price = 1.0,
                Quantity = 1
            };

            var ctx = MakeContext(new DateTime(2024, 1, 1, 10, 0, 0), 1.0, 1.0, order);
            rm.Execute(ctx);

            var approved = Assert.Single(ctx.NewOrders);
            Assert.Equal(1000.0, approved.Quantity, 5);
        }

        [Fact]
        public void RiskManager_ShouldRejectInvalidStopLossSide_AndAllowValidRiskReward()
        {
            var config = DefaultConfig();
            var pm = new PortfolioManager(config.InitialCash);
            var logs = new List<LogEntryDTO>();
            var rm = new RiskManager(config, pm, logs);

            var badSlOrder = new Order
            {
                Id = 1,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Type = OrderType.Market,
                Price = 1.1000,
                StopLoss = 1.1050, // invalide pour Buy
                TakeProfit = 1.1200,
                Quantity = 10
            };

            var ctxBad = MakeContext(new DateTime(2024, 1, 1, 10, 0, 0), 1.0999, 1.1000, badSlOrder);
            rm.Execute(ctxBad);
            Assert.Empty(ctxBad.NewOrders);
            Assert.Contains(logs, l => l.Type == "REJET RISK");

            var lowRrOrder = new Order
            {
                Id = 2,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Type = OrderType.Market,
                Price = 1.1000,
                StopLoss = 1.0900,   // risk = 0.01
                TakeProfit = 1.1100, // reward = 0.01 -> RR = 1.0 < 2.0
                Quantity = 10
            };

            var ctxLowRr = MakeContext(new DateTime(2024, 1, 1, 10, 1, 0), 1.0999, 1.1000, lowRrOrder);
            rm.Execute(ctxLowRr);
            Assert.Empty(ctxLowRr.NewOrders);

            var goodOrder = new Order
            {
                Id = 3,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Type = OrderType.Market,
                Price = 1.1000,
                StopLoss = 1.0950,   // risk = 0.005
                TakeProfit = 1.1100, // reward = 0.01 -> RR = 2.0
                Quantity = 10
            };

            var ctxGood = MakeContext(new DateTime(2024, 1, 1, 10, 2, 0), 1.0999, 1.1000, goodOrder);
            rm.Execute(ctxGood);
            Assert.Single(ctxGood.NewOrders);
        }

        [Fact]
        public void RiskManager_ShouldAllowOppositeOrderToCloseOrReverse_EvenAtMaxConcurrentPositions()
        {
            var config = DefaultConfig();
            config.MaxConcurrentPositions = 1;
            var pm = new PortfolioManager(config.InitialCash);
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            var openCtx = new TickContext();
            openCtx.Reset(new TickData(t0.Ticks, 1.1000, 1.1001, 100));
            openCtx.Trades.Add(new Trade
            {
                OrderId = 1,
                Timestamp = t0.Ticks,
                Symbol = "EURUSD",
                Side = Side.Buy,
                Price = 1.1001,
                Quantity = 100,
                Commission = 0
            });
            pm.ProcessTick(openCtx);

            var logs = new List<LogEntryDTO>();
            var rm = new RiskManager(config, pm, logs);

            // Nouvel ordre sur un autre symbole -> bloqué par MaxConcurrentPositions
            var otherSymbolOrder = new Order
            {
                Id = 2,
                Symbol = "GBPUSD",
                Side = Side.Buy,
                Type = OrderType.Market,
                Price = 1.2500,
                Quantity = 10
            };
            var ctxOther = MakeContext(t0.AddMinutes(1), 1.2499, 1.2500, otherSymbolOrder);
            rm.Execute(ctxOther);
            Assert.Empty(ctxOther.NewOrders);

            // Ordre de sens opposé sur le même symbole -> autorisé pour fermer/inverser
            var reverseOrder = new Order
            {
                Id = 3,
                Symbol = "EURUSD",
                Side = Side.Sell,
                Type = OrderType.Market,
                Price = 1.1050,
                Quantity = 100
            };
            var ctxReverse = MakeContext(t0.AddMinutes(2), 1.1050, 1.1051, reverseOrder);
            rm.Execute(ctxReverse);
            Assert.Single(ctxReverse.NewOrders);
        }

        [Fact]
        public void RiskManager_ShouldTripCircuitBreaker_WhenMaxDrawdownOrDailyLossReached()
        {
            var config = DefaultConfig();
            var pm = new PortfolioManager(10_000);
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);
            var logs = new List<LogEntryDTO>();
            var rm = new RiskManager(config, pm, logs);

            // Premier tick du jour : initialise _startOfDayEquity à 10_000
            var initCtx = new TickContext();
            initCtx.Reset(new TickData(t0.Ticks, 1.1000, 1.1000, 100));
            rm.Execute(initCtx);

            // Simule une perte journalière > 5% (600$) dans la même journée
            var lossCtx = new TickContext();
            lossCtx.Reset(new TickData(t0.AddSeconds(30).Ticks, 1.1000, 1.1000, 100));
            lossCtx.Trades.Add(new Trade { OrderId = 1, Timestamp = t0.Ticks, Symbol = "EURUSD", Side = Side.Buy, Price = 1.1000, Quantity = 1000, Commission = 0 });
            lossCtx.Trades.Add(new Trade { OrderId = 2, Timestamp = t0.AddSeconds(30).Ticks, Symbol = "EURUSD", Side = Side.Sell, Price = 0.5000, Quantity = 1000, Commission = 0 });
            pm.ProcessTick(lossCtx);

            var order = new Order { Id = 3, Symbol = "EURUSD", Side = Side.Buy, Type = OrderType.Market, Price = 1.0, Quantity = 10 };
            var ctx = MakeContext(t0.AddMinutes(1), 1.0, 1.0, order);
            rm.Execute(ctx);

            Assert.Empty(ctx.NewOrders);
            Assert.Contains(logs, l => l.Type == "REJET RISK");

            // Jour suivant avec un drawdown global >= 10% (perte additionnelle de 500$ -> equity = 8_900)
            var nextDay = t0.AddDays(1);
            var ddCtx = new TickContext();
            ddCtx.Reset(new TickData(nextDay.Ticks, 1.0, 1.0, 100));
            ddCtx.Trades.Add(new Trade { OrderId = 4, Timestamp = nextDay.Ticks, Symbol = "EURUSD", Side = Side.Buy, Price = 1.0, Quantity = 1000, Commission = 0 });
            ddCtx.Trades.Add(new Trade { OrderId = 5, Timestamp = nextDay.Ticks, Symbol = "EURUSD", Side = Side.Sell, Price = 0.5, Quantity = 1000, Commission = 0 });
            pm.ProcessTick(ddCtx);

            var orderNextDay = new Order { Id = 6, Symbol = "EURUSD", Side = Side.Buy, Type = OrderType.Market, Price = 1.0, Quantity = 10 };
            var ctxNextDay = MakeContext(nextDay.AddMinutes(1), 1.0, 1.0, orderNextDay);
            rm.Execute(ctxNextDay);

            Assert.Empty(ctxNextDay.NewOrders);
        }
    }
}
