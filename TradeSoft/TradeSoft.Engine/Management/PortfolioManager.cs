using System;
using System.Collections.Generic;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Engine.Management
{
    public class PortfolioManager : Handler
    {
        private readonly Portfolio _portfolio;

        // Pour ne pas saturer la RAM, on n'enregistre l'équité que de temps en temps
        // (ex: toutes les heures ou à chaque trade)
        public readonly List<(DateTime Time, double Equity)> EquityCurve = new();

        public readonly List<Trade> TradeHistory = new List<Trade>();

        public Portfolio CurrentPortfolio => _portfolio;

        public PortfolioManager(double initialCash)
        {
            _portfolio = new Portfolio(initialCash);
        }

        protected override void OnProcess(TickContext context)
        {
            // 1. Traiter les exécutions (Trades) venant du Broker
            foreach (var trade in context.Trades)
            {
                _portfolio.OnTrade(trade);
                TradeHistory.Add(trade);
            }

            // 2. Mettre à jour la valeur du portefeuille avec le prix actuel du tick
            // (Mark-to-Market)
            _portfolio.MarkToMarket(context.Tick.Median);

            // 3. Enregistrement de la courbe (Sampling)
            // On enregistre si un trade a eu lieu OU si c'est la fin de la journée (simplifié ici à chaque trade pour l'exemple)
            if (context.Trades.Count > 0)
            {
                EquityCurve.Add((new DateTime(context.Tick.Timestamp), _portfolio.Equity));
            }
        }
    }
}