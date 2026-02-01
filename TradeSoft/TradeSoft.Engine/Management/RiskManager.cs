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

            // 1. Vérification des coupe-circuits globaux
            if (CheckCircuitBreakers(context))
            {
                return; // Stop tout si un coupe-circuit est actif
            }

            // 2. Traitement des nouveaux ordres
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
            // --- A. Max Drawdown ---
            if (IsMaxDrawdownReached(out double currentDD))
            {
                // On logue seulement s'il y avait des ordres à passer
                if (context.NewOrders.Count > 0)
                    LogRejection($"Max Drawdown Atteint: {currentDD:F2}% (Limite: {_config.MaxDrawdownPercent}%)");

                context.NewOrders.Clear(); // On supprime tout
                return true;
            }

            // --- B. Perte Journalière ---
            double currentEquity = _portfolioManager.CurrentPortfolio.Equity;
            double dailyLossLimit = _startOfDayEquity * (1.0 - (_config.MaxDailyLossPercent / 100.0));

            if (currentEquity <= dailyLossLimit)
            {
                _dailyLossLimitReached = true;
            }

            if (_dailyLossLimitReached)
            {
                if (context.NewOrders.Count > 0)
                    LogRejection($"Limite Perte Journalière. Equity: {currentEquity:F2}, Seuil: {dailyLossLimit:F2}");

                context.NewOrders.Clear();
                return true;
            }

            return false;
        }

        private void ProcessNewOrders(TickContext context)
        {
            int currentPositions = _portfolioManager.OpenPositionCount;

            // Boucle inversée pour suppression sûre
            for (int i = context.NewOrders.Count - 1; i >= 0; i--)
            {
                var order = context.NewOrders[i];

                // Les ordres de sortie (SL/TP) ne sont pas filtrés par le risque d'ouverture
                if (IsExitOrder(order)) continue;

                // --- C. Max Positions Simultanées ---
                if (currentPositions >= _config.MaxConcurrentPositions)
                {
                    LogRejection($"Max Positions Atteint ({currentPositions}/{_config.MaxConcurrentPositions}) pour {order.Symbol}");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                // --- D. Ratio Risque/Rendement ---
                if (!CheckRiskRewardRatio(order, context.Tick, out string rrReason))
                {
                    LogRejection($"Refus Risk/Reward: {rrReason}");
                    context.NewOrders.RemoveAt(i);
                    continue;
                }

                // --- E. Dimensionnement (Sizing) ---
                if (!ValidateAndResizeOrder(order, context.Tick, out string sizeReason))
                {
                    LogRejection($"Erreur Taille: {sizeReason}");
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
            // Pas de TP/SL = Pas de calcul de ratio possible (on laisse passer ou on bloque selon stratégie)
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

            // 1. Calcul taille selon Risque (SL)
            double quantityByRisk = double.MaxValue;

            if (order.StopLoss.HasValue)
            {
                // Ex: Je veux risquer 1% de mon capital
                double riskPerTradeCash = equity * (_config.MaxRiskPerTradePercent / 100.0);

                double entryPrice = (order.Type == OrderType.Limit) ? order.Price : currentPrice;
                double slDistance = Math.Abs(entryPrice - order.StopLoss.Value);
                if (slDistance < 0.0001) slDistance = 0.0001;

                quantityByRisk = riskPerTradeCash / slDistance;
            }
            else
            {
                // Fallback si pas de SL (valeurs par défaut)
                // Si votre Config n'a pas ces champs, mettez des valeurs fixes ici :
                double defaultRisk = 0.01; // 1%
                double defaultLev = 1.0;

                // Si dispo dans config : _config.DefaultRiskNoSL
                quantityByRisk = (equity * defaultRisk * defaultLev) / currentPrice;
            }

            // 2. Calcul taille selon Marge (Levier)
            double maxBuyingPower = equity * _config.Leverage;
            double quantityByMargin = maxBuyingPower / currentPrice;

            // On prend le plus restrictif
            double finalQuantity = Math.Min(quantityByRisk, quantityByMargin);

            // 3. Vérification MinLotSize
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

        // Surcharge simple
        private bool IsMaxDrawdownReached() => IsMaxDrawdownReached(out _);

        private bool IsExitOrder(Order order)
        {
            return order.Comment != null && (order.Comment.Contains("SL_") || order.Comment.Contains("TP_"));
        }

        private void LogRejection(string reason)
        {
            // ✅ Debug.WriteLine pour voir dans Visual Studio
            System.Diagnostics.Debug.WriteLine($"❌ [REJET RISK] {reason}");
        }
    }
}