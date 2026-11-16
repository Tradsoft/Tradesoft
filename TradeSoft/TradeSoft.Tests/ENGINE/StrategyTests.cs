using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Engine.Strategies;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class StrategyTests
    {
        private static TickContext MakeBarContext(DateTime time, double close, double? bid = null, double? ask = null)
        {
            double b = bid ?? close - 0.0001;
            double a = ask ?? close + 0.0001;
            var ctx = new TickContext();
            ctx.Reset(new TickData(time.Ticks, b, a, 100, TickFlags.Trade));
            ctx.GeneratedBar = new Bar
            {
                Timestamp = time,
                Open = close,
                High = close,
                Low = close,
                Close = close,
                Volume = 100
            };
            return ctx;
        }

        [Fact]
        public void RSI_ShouldGenerateBuyAndSellOrders_WithConfiguredSymbolAndTickSize()
        {
            var strategy = new RSI(period: 2, oversold: 30, overbought: 70, lot: 0.5, slPips: 50, tpPips: 100, tickSize: 0.0001, symbol: "GBPUSD");
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            double[] prices = { 1.1000, 1.0900, 1.0800, 1.1000, 1.1200, 1.0900 };
            TickContext? buyCtx = null;
            TickContext? sellCtx = null;

            for (int i = 0; i < prices.Length; i++)
            {
                var ctx = MakeBarContext(t0.AddMinutes(i), prices[i]);
                strategy.Execute(ctx);
                if (ctx.NewOrders.Count > 0)
                {
                    if (ctx.NewOrders[0].Side == Side.Buy) buyCtx = ctx;
                    else sellCtx = ctx;
                }
            }

            Assert.NotNull(buyCtx);
            var buyOrder = Assert.Single(buyCtx!.NewOrders);
            Assert.Equal("GBPUSD", buyOrder.Symbol);
            Assert.Equal(0.5, buyOrder.Quantity);
            Assert.Equal(1.0950, buyOrder.StopLoss!.Value, 5);
            Assert.Equal(1.1100, buyOrder.TakeProfit!.Value, 5);
            Assert.True(buyOrder.Id > 0);

            Assert.NotNull(sellCtx);
            var sellOrder = Assert.Single(sellCtx!.NewOrders);
            Assert.Equal("GBPUSD", sellOrder.Symbol);
            Assert.NotEqual(buyOrder.Id, sellOrder.Id);
        }

        [Fact]
        public void MovingAverageCrossover_ShouldSupportFractionalLotsAndReversals()
        {
            var strategy = new MovingAverageCrossover(shortPeriod: 2, longPeriod: 3, lot: 0.25, symbol: "USDJPY");
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            double[] prices = { 100, 100, 100, 105, 90 };
            TickContext? buyCtx = null;
            TickContext? sellCtx = null;

            for (int i = 0; i < prices.Length; i++)
            {
                var ctx = MakeBarContext(t0.AddMinutes(i), prices[i]);
                strategy.Execute(ctx);
                if (ctx.NewOrders.Count > 0)
                {
                    if (ctx.NewOrders[0].Side == Side.Buy) buyCtx = ctx;
                    else sellCtx = ctx;
                }
            }

            Assert.NotNull(buyCtx);
            var buy = Assert.Single(buyCtx!.NewOrders);
            Assert.Equal("USDJPY", buy.Symbol);
            Assert.Equal(0.25, buy.Quantity, 5);

            Assert.NotNull(sellCtx);
            var sell = Assert.Single(sellCtx!.NewOrders);
            Assert.Equal("USDJPY", sell.Symbol);
            Assert.Equal(0.50, sell.Quantity, 5);
        }

        [Fact]
        public void MovingAverageCrossover_ShouldThrow_WhenLongPeriodIsNotGreaterThanShortPeriod()
        {
            Assert.Throws<ArgumentException>(() => new MovingAverageCrossover(shortPeriod: 20, longPeriod: 20));
        }

        [Fact]
        public void MeanReversion_ShouldGenerateBuyAndSellOrders_WithFractionalLots()
        {
            var strategy = new MeanReversion(period: 3, k: 1.0, lot: 0.5, symbol: "BTCUSD");
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            double[] prices = { 100, 100, 100, 90, 120 };
            TickContext? buyCtx = null;
            TickContext? sellCtx = null;

            for (int i = 0; i < prices.Length; i++)
            {
                var ctx = MakeBarContext(t0.AddMinutes(i), prices[i]);
                strategy.Execute(ctx);
                if (ctx.NewOrders.Count > 0)
                {
                    if (ctx.NewOrders[0].Side == Side.Buy) buyCtx = ctx;
                    else sellCtx = ctx;
                }
            }

            Assert.NotNull(buyCtx);
            var buy = Assert.Single(buyCtx!.NewOrders);
            Assert.Equal("BTCUSD", buy.Symbol);
            Assert.Equal(0.5, buy.Quantity, 5);

            Assert.NotNull(sellCtx);
            var sell = Assert.Single(sellCtx!.NewOrders);
            Assert.Equal("BTCUSD", sell.Symbol);
            Assert.Equal(1.0, sell.Quantity, 5);
        }

        [Fact]
        public void Breakout_ShouldGenerateBuyOnBreakoutAndSellOnBreakdown()
        {
            var strategy = new BreakoutSupportResistance(lookback: 3, lot: 0.2, symbol: "ETHUSD");
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            double[] prices = { 100, 102, 101, 105, 95 };
            TickContext? buyCtx = null;
            TickContext? sellCtx = null;

            for (int i = 0; i < prices.Length; i++)
            {
                var ctx = MakeBarContext(t0.AddMinutes(i), prices[i]);
                strategy.Execute(ctx);
                if (ctx.NewOrders.Count > 0)
                {
                    if (ctx.NewOrders[0].Side == Side.Buy) buyCtx = ctx;
                    else sellCtx = ctx;
                }
            }

            Assert.NotNull(buyCtx);
            var buy = Assert.Single(buyCtx!.NewOrders);
            Assert.Equal("ETHUSD", buy.Symbol);
            Assert.Equal(0.2, buy.Quantity, 5);

            Assert.NotNull(sellCtx);
            var sell = Assert.Single(sellCtx!.NewOrders);
            Assert.Equal("ETHUSD", sell.Symbol);
            Assert.Equal(0.4, sell.Quantity, 5);
        }
    }
}
