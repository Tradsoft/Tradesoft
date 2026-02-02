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

        private readonly List<LogEntryDTO> _logs;

        private DateTime _currentDate = DateTime.MinValue;
        private double _startOfDayEquity;
        private bool _dailyLossLimitReached = false;

        public RiskManager(BacktestConfig config, PortfolioManager portfolioManager, List<LogEntryDTO> logs)
        {
            _config = config;
            _portfolioManager = portfolioManager;
            _logs = logs;
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
            if (IsMaxDrawdownReached(out double currentDD))
            {
                if (context.NewOrders.Count > 0)
                    LogRejection(new DateTime(context.Tick.Timestamp), $"Max Drawdown Atteint: {currentDD:F2}% (Limite: {_config.MaxDrawdownPercent}%)");

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
                if (context.NewOrders.Count > 0)
                    LogRejection(new DateTime(context.Tick.Timestamp), $"Limite Perte Journalière. Equity: {currentEquity:F2}, Seuil: {dailyLossLimit:F2}");

                context.NewOrders.Clear();
                return true;
            }

            return false;
        }

        private void LogRejection(DateTime time, string reason)
        {
            string msg = reason;
            System.Diagnostics.Debug.WriteLine($"❌ [REJET RISK] {msg}");

            _logs.Add(new LogEntryDTO(time, "REJET RISK", msg));
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
                    LogRejection(new DateTime(context.Tick.Timestamp), $"Max Positions Atteint ({currentPositions}/{_config.MaxConcurrentPositions}) pour {order.Symbol}");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                if (!CheckRiskRewardRatio(order, context.Tick, out string rrReason))
                {
                    LogRejection(new DateTime(context.Tick.Timestamp), $"Refus Risk/Reward: {rrReason}");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                if (!ValidateAndResizeOrder(order, context.Tick, out string sizeReason))
                {
                    LogRejection(new DateTime(context.Tick.Timestamp), $"Erreur Taille: {sizeReason}");
                    context.NewOrders.RemoveAt(i);
                }
                else
                {
                    currentPositions++;
                }
            }
        }

        private bool CheckRiskRewardRatio(Order order, TickData tick, out string reason)
        {
            reason = string.Empty;
            if (!order.StopLoss.HasValue || !order.TakeProfit.HasValue) return true;

            double entryPrice = (order.Type == OrderType.Limit)
                ? order.Price
                : (order.Side == Side.Buy ? tick.Ask : tick.Bid);

            double risk = Math.Abs(entryPrice - order.StopLoss.Value);
            double reward = Math.Abs(order.TakeProfit.Value - entryPrice);

            if (risk <= 0.00001)
            {
                reason = "Distance SL nulle";
                return false;
            }

            double ratio = reward / risk;
            if (ratio < _config.MinRiskRewardRatio)
            {
                reason = $"Ratio {ratio:F2} < Min {_config.MinRiskRewardRatio} (Risk:{risk:F4}, Reward:{reward:F4})";
                return false;
            }

            return true;
        }

        private bool ValidateAndResizeOrder(Order order, TickData tick, out string reason)
        {
            reason = string.Empty;
            double equity = _portfolioManager.CurrentPortfolio.Equity;
            double currentPrice = (order.Side == Side.Buy) ? tick.Ask : tick.Bid;

            if (currentPrice <= 0.00001)
            {
                reason = "Prix invalide (0)";
                return false;
            }

            double quantityByRisk = double.MaxValue;

            if (order.StopLoss.HasValue)
            {
                double riskPerTradeCash = equity * (_config.MaxRiskPerTradePercent / 100.0);

                double entryPrice = (order.Type == OrderType.Limit) ? order.Price : currentPrice;
                double slDistance = Math.Abs(entryPrice - order.StopLoss.Value);
                if (slDistance < 0.0001) slDistance = 0.0001;

                quantityByRisk = riskPerTradeCash / slDistance;
            }
            else
            {
                double defaultRisk = 0.01;
                double defaultLev = 1.0;

                quantityByRisk = (equity * defaultRisk * defaultLev) / currentPrice;
            }

            double maxBuyingPower = equity * _config.Leverage;
            double quantityByMargin = maxBuyingPower / currentPrice;

            double finalQuantity = Math.Min(quantityByRisk, quantityByMargin);

            if (finalQuantity < _config.MinLotSize)
            {
                reason = $"Qté {finalQuantity:F2} < Min {_config.MinLotSize}. (Limité par: {(quantityByRisk < quantityByMargin ? "Risque" : "Marge")})";
                return false;
            }

            order.Quantity = finalQuantity;
            return true;
        }

        private bool IsMaxDrawdownReached(out double currentDD)
        {
            double peak = _portfolioManager.PeakEquity;
            currentDD = 0;
            if (peak <= 0) return false;

            currentDD = (peak - _portfolioManager.CurrentPortfolio.Equity) / peak * 100;
            return currentDD >= _config.MaxDrawdownPercent;
        }

        private bool IsMaxDrawdownReached() => IsMaxDrawdownReached(out _);

        private bool IsExitOrder(Order order)
        {
            return order.Comment != null && (order.Comment.Contains("SL_") || order.Comment.Contains("TP_"));
        }
    }
}