using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Management
{
    public class RiskManager : Handler
    {
        private readonly BacktestConfig _config;
        private readonly PortfolioManager _portfolioManager;

        private DateTime _currentDate = DateTime.MinValue;
        private double _startOfDayEquity;
        private bool _dailyLossLimitReached = false;

        public RiskManager(BacktestConfig config, PortfolioManager portfolioManager)
        {
            _config = config;
            _portfolioManager = portfolioManager;
            _startOfDayEquity = portfolioManager.CurrentPortfolio.InitialCash;
        }

        protected override void OnProcess(TickContext context)
        {
            UpdateDailyState(context.Tick.Timestamp);

            if (CheckCircuitBreakers(context))
            {
                return; 
            }

            ProcessNewOrders(context);
        }

        private void UpdateDailyState(long tickTimestamp)
        {
            DateTime tickTime = new DateTime(tickTimestamp);

            if (tickTime.Date > _currentDate)
            {
                _currentDate = tickTime.Date;
                _startOfDayEquity = _portfolioManager.CurrentPortfolio.Equity;
                _dailyLossLimitReached = false;
            }
        }

        private bool CheckCircuitBreakers(TickContext context)
        {
            if (IsMaxDrawdownReached())
            {
                if (context.NewOrders.Count > 0) LogRejection("Max Drawdown Reached");
                context.NewOrders.Clear();
                return true;
            }

            double currentEquity = _portfolioManager.CurrentPortfolio.Equity;
            double dailyLossLimit = _startOfDayEquity * (1.0 - (_config.MaxDailyLossPercent / 100.0));

            if (currentEquity <= dailyLossLimit)
            {
                _dailyLossLimitReached = true;
            }

            if (_dailyLossLimitReached)
            {
                if (context.NewOrders.Count > 0) LogRejection("Daily Loss Limit Reached");
                context.NewOrders.Clear();
                return true;
            }

            return false;
        }

        private void ProcessNewOrders(TickContext context)
        {
            int currentPositions = _portfolioManager.OpenPositionCount;

            for (int i = context.NewOrders.Count - 1; i >= 0; i--)
            {
                var order = context.NewOrders[i];

                if (IsExitOrder(order)) continue;

                if (currentPositions >= _config.MaxConcurrentPositions)
                {
                    LogRejection($"Max Positions Reached ({currentPositions}/{_config.MaxConcurrentPositions})");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                if (!CheckRiskRewardRatio(order, context.Tick))
                {
                    LogRejection("Bad Risk/Reward Ratio");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                if (!ValidateAndResizeOrder(order, context.Tick))
                {
                    LogRejection("Sizing/Margin Failed (Price 0 or Insufficient Funds)");
                    context.NewOrders.RemoveAt(i);
                }
                else
                {
                    currentPositions++;
                }
            }
        }

        private bool CheckRiskRewardRatio(Order order, TickData tick)
        {
            if (!order.StopLoss.HasValue || !order.TakeProfit.HasValue) return true;

            double entryPrice = (order.Type == OrderType.Limit)
                ? order.Price
                : (order.Side == Side.Buy ? tick.Ask : tick.Bid);

            double risk = Math.Abs(entryPrice - order.StopLoss.Value);
            double reward = Math.Abs(order.TakeProfit.Value - entryPrice);

            if (risk <= 0) return false;
            return (reward / risk) >= _config.MinRiskRewardRatio;
        }

        private bool ValidateAndResizeOrder(Order order, TickData tick)
        {
            double equity = _portfolioManager.CurrentPortfolio.Equity;
            double riskAmount = equity * (_config.MaxRiskPerTradePercent / 100.0);
            double currentPrice = (order.Side == Side.Buy) ? tick.Ask : tick.Bid;

            if (currentPrice <= 0.00001) return false;

            if (order.StopLoss.HasValue)
            {
                double entryPrice = (order.Type == OrderType.Limit) ? order.Price : currentPrice;
                double slDistance = Math.Abs(entryPrice - order.StopLoss.Value);

                if (slDistance <= 0) return false;
                order.Quantity = riskAmount / slDistance;
            }
            else
            {
                double maxQty = (equity * 1.0) / currentPrice;
                if (order.Quantity > maxQty) order.Quantity = maxQty;
            }

            return order.Quantity > 0;
        }

        private bool IsMaxDrawdownReached()
        {
            double peak = _portfolioManager.PeakEquity;
            return peak > 0 && ((peak - _portfolioManager.CurrentPortfolio.Equity) / peak * 100 >= _config.MaxDrawdownPercent);
        }

        private bool IsExitOrder(Order order)
        {
            return false;
        }

        private void LogRejection(string reason)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[RISK] Order Rejected: {reason}");
            Console.ResetColor();
        }
    }
}