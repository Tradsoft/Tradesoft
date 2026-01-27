using System;
using System.Collections.Generic;
using System.Linq;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Engine.Management; 

namespace TradeSoft.Engine.Analysis
{
    public static class StatisticsCalculator
    {
        public static BacktestResult Calculate(Portfolio portfolio, List<(DateTime, double)> equityCurve)
        {
            var result = new BacktestResult();
            var history = portfolio.History;

            result.InitialCash = portfolio.InitialCash;
            result.FinalEquity = portfolio.Equity;
            result.NetProfit = result.FinalEquity - result.InitialCash;
            result.ReturnPercent = result.InitialCash > 0 ? (result.NetProfit / result.InitialCash) * 100 : 0;
            result.TotalTrades = history.Count;

            if (result.TotalTrades == 0) 
                return result;

            int wins = history.Count(t => t.PnL > 0);
            result.WinRate = (double)wins / result.TotalTrades * 100;

            double grossProfit = history.Where(t => t.PnL > 0).Sum(t => t.PnL);
            double grossLoss = Math.Abs(history.Where(t => t.PnL < 0).Sum(t => t.PnL));

            result.ProfitFactor = grossLoss == 0 ? (grossProfit > 0 ? 999 : 0) : grossProfit / grossLoss;

            result.MaxDrawdownPercent = CalculateMaxDrawdown(equityCurve);

            result.Trades = history.Select(t => new TradeResultItem
            {
                Id = 0,
                Time = t.ExitTime,
                Side = t.Direction.ToString(),
                Price = t.EntryPrice,    
                Quantity = t.Quantity,
                PnL = t.PnL
            }).ToList();

            result.EquityCurve = equityCurve.Select(x => new EquityPoint(x.Item1, x.Item2)).ToList();

            return result;
        }

        private static double CalculateMaxDrawdown(List<(DateTime, double)> curve)
        {
            if (curve == null || curve.Count == 0) 
                return 0;

            double maxDrawdown = 0;
            double peak = curve[0].Item2;

            foreach (var point in curve)
            {
                double currentEquity = point.Item2;

                if (currentEquity > peak)
                {
                    peak = currentEquity;
                }

                double drawdown = (peak - currentEquity) / peak * 100;

                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }

            return maxDrawdown;
        }
    }
}