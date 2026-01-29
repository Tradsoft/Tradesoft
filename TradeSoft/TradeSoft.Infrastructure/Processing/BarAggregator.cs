using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Infrastructure.Processing
{
    public class BarAggregator : Handler
    {
        private Bar? _currentBar;
        private DateTime _nextPeriodClose;
        private readonly TimeSpan _timeframe;

        // Variables de filtrage
        private double _lastValidPrice = 0;
        private DateTime _lastTickTime = DateTime.MinValue;

        public BarAggregator(TimeSpan timeframe)
        {
            _timeframe = timeframe;
        }

        protected override void OnProcess(TickContext context)
        {
            DateTime tickTime = new DateTime(context.Tick.Timestamp);
            double price = context.Tick.MidPrice;

            // --- FILTRE ANTI-BRUIT & REPRISE ---

            if (price <= 0.0001) return;

            // Détection si on a sauté plus d'1 heure (ex: Week-end ou trou de 8 ans)
            bool massiveGap = (tickTime - _lastTickTime).TotalHours > 1;

            // Si ce n'est PAS un gap temporel, on vérifie la cohérence du prix
            if (!massiveGap && _lastValidPrice > 0)
            {
                double variation = Math.Abs(price - _lastValidPrice) / _lastValidPrice;
                // Si le prix bouge de plus de 10% instantanément sans raison temporelle -> Erreur
                if (variation > 0.10)
                {
                    return;
                }
            }

            // On met à jour les références
            _lastValidPrice = price;
            _lastTickTime = tickTime;
            // ------------------------------------

            if (_currentBar == null)
            {
                StartNewBar(tickTime, price, context.Tick.Volume);
                return;
            }

            if (tickTime >= _nextPeriodClose)
            {
                // Clôture de la bougie précédente
                context.GeneratedBar = _currentBar;

                // Démarrage de la nouvelle
                // S'il y a eu un trou de 8 ans, tickTime sera en 2023.
                // StartNewBar va créer la nouvelle bougie en 2023 directement.
                StartNewBar(tickTime, price, context.Tick.Volume);
            }
            else
            {
                // Mise à jour de la bougie en cours
                if (price > _currentBar.High) _currentBar.High = price;
                if (price < _currentBar.Low) _currentBar.Low = price;

                _currentBar.Close = price;
                _currentBar.Volume += context.Tick.Volume;
            }
        }

        private void StartNewBar(DateTime tickTime, double price, int volume)
        {
            long ticks = tickTime.Ticks;
            long periodTicks = _timeframe.Ticks;
            long roundedTicks = (ticks / periodTicks) * periodTicks;
            var barTime = new DateTime(roundedTicks);

            _currentBar = new Bar
            {
                Timestamp = barTime,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = volume
            };

            _nextPeriodClose = barTime.Add(_timeframe);
        }
    }
}