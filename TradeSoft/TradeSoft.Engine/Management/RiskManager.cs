using System;
using System.Linq;
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
            DateTime tickTime = new DateTime(context.Tick.Timestamp);
            double currentEquity = _portfolioManager.CurrentPortfolio.Equity;

            // 1. RESET JOURNALIER
            if (tickTime.Date > _currentDate)
            {
                _currentDate = tickTime.Date;
                _startOfDayEquity = currentEquity;
                _dailyLossLimitReached = false;
            }

            // 2. CHECK DRAWDOWN
            if (IsMaxDrawdownReached())
            {
                if (context.NewOrders.Count > 0) LogRejection("Max Drawdown Reached");
                context.NewOrders.Clear();
                return;
            }

            // 3. CHECK DAILY LOSS
            double dailyLossLimit = _startOfDayEquity * (1.0 - (_config.MaxDailyLossPercent / 100.0));
            if (currentEquity <= dailyLossLimit) _dailyLossLimitReached = true;

            if (_dailyLossLimitReached)
            {
                if (context.NewOrders.Count > 0) LogRejection("Daily Loss Limit Reached");
                context.NewOrders.Clear();
                return;
            }

            // 4. TRAITEMENT DES ORDRES
            int currentPositions = _portfolioManager.OpenPositionCount;

            for (int i = context.NewOrders.Count - 1; i >= 0; i--)
            {
                var order = context.NewOrders[i];
                if (IsExitOrder(order)) continue;

                // A. Limite positions simultanées
                if (currentPositions >= _config.MaxConcurrentPositions)
                {
                    LogRejection($"Max Positions Reached ({currentPositions}/{_config.MaxConcurrentPositions})");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                // B. Validation Ratio R:R (utilise le prix du tick actuel)
                if (!CheckRiskRewardRatio(order, context.Tick))
                {
                    LogRejection("Bad Risk/Reward Ratio");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                // C. Sizing (utilise le prix du tick actuel)
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

            // CORRECTION : On utilise le prix du tick (Bid/Ask) car plus frais que Portfolio
            double entryPrice = (order.Type == OrderType.Limit) ? order.Price : (order.Side == Side.Buy ? tick.Ask : tick.Bid);

            double risk = Math.Abs(entryPrice - order.StopLoss.Value);
            double reward = Math.Abs(order.TakeProfit.Value - entryPrice);

            if (risk <= 0) return false;
            return (reward / risk) >= _config.MinRiskRewardRatio;
        }

        private bool ValidateAndResizeOrder(Order order, TickData tick)
        {
            double equity = _portfolioManager.CurrentPortfolio.Equity;
            double riskAmount = equity * (_config.MaxRiskPerTradePercent / 100.0);

            // Prix actuel de référence
            double currentPrice = (order.Side == Side.Buy) ? tick.Ask : tick.Bid;

            // Sécurité anti-crash si prix aberrant ou 0
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
                // Fallback sans SL
                double maxQty = (equity * 1.0) / currentPrice;
                if (order.Quantity > maxQty) order.Quantity = maxQty;
            }

            // Vérification finale quantité positive
            return order.Quantity > 0;
        }

        private bool IsMaxDrawdownReached()
        {
            double peak = _portfolioManager.PeakEquity;
            return peak > 0 && ((peak - _portfolioManager.CurrentPortfolio.Equity) / peak * 100 >= _config.MaxDrawdownPercent);
        }

        private bool IsExitOrder(Order order) => false;

        private void LogRejection(string reason)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[RISK] Order Rejected: {reason}");
            Console.ResetColor();
        }
    }
}