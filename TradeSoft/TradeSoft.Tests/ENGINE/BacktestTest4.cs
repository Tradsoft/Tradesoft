using System;
using System.Collections.Generic;
using TradeSoft.Core.Domain;
using TradeSoft.Engine.Analysis;
using Xunit;

namespace TradeSoft.Tests.ENGINE
{
    public class BacktestTest4
    {
        [Fact]
        public void Calculate_WithWinningAndLosingTrades_ShouldComputeAccurateStatistics()
        {
            var portfolio = new Portfolio(10_000);
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);

            portfolio.OnTrade(new Trade { OrderId = 1, Symbol = "EURUSD", Side = Side.Buy, Price = 1.1000, Quantity = 1000, Commission = 2, Timestamp = t0.Ticks });
            portfolio.OnTrade(new Trade { OrderId = 2, Symbol = "EURUSD", Side = Side.Sell, Price = 1.1200, Quantity = 1000, Commission = 2, Timestamp = t0.AddHours(1).Ticks });

            portfolio.OnTrade(new Trade { OrderId = 3, Symbol = "EURUSD", Side = Side.Buy, Price = 1.1200, Quantity = 1000, Commission = 2, Timestamp = t0.AddHours(2).Ticks });
            portfolio.OnTrade(new Trade { OrderId = 4, Symbol = "EURUSD", Side = Side.Sell, Price = 1.1100, Quantity = 1000, Commission = 2, Timestamp = t0.AddHours(3).Ticks });

            var equityCurve = new List<(DateTime, double)>
            {
                (t0, 10_000),
                (t0.AddHours(1), 10_016),
                (t0.AddHours(3), 10_002)
            };

            var result = StatisticsCalculator.Calculate(portfolio, equityCurve);

            Assert.Equal(10_000, result.InitialCash);
            Assert.Equal(10_002, result.FinalEquity, 5);
            Assert.Equal(2, result.NetProfit, 5);
            Assert.Equal(0.02, result.ReturnPercent, 5);
            Assert.Equal(2, result.TotalTrades);
            Assert.Equal(50.0, result.WinRate, 5);
            Assert.Equal(2.0, result.ProfitFactor, 5);
            Assert.Equal(2, result.ClosedTrades.Count);
            Assert.Equal(3, result.EquityCurve.Count);
            Assert.True(result.MaxDrawdownPercent > 0);
        }

        [Fact]
        public void Calculate_WithZeroTrades_ShouldPreserveEquityCurveAndDrawdown()
        {
            var portfolio = new Portfolio(10_000);
            var t0 = new DateTime(2024, 1, 1, 10, 0, 0);
            var equityCurve = new List<(DateTime, double)>
            {
                (t0, 10_000),
                (t0.AddMinutes(5), 9_500)
            };

            var result = StatisticsCalculator.Calculate(portfolio, equityCurve);

            Assert.Equal(0, result.TotalTrades);
            Assert.Equal(2, result.EquityCurve.Count);
            Assert.Equal(5.0, result.MaxDrawdownPercent, 5);
        }
    }
}
