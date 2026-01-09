using System;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;

namespace TradeSoft.Infrastructure.Processing
{
	public class BarAggregator : Handler
	{
		private Bar? _currentBar;
		private DateTime? _nextPeriod;
		private readonly TimeSpan _period = TimeSpan.FromMinutes(1);

		protected override void OnProcess(TickContext context)
		{
			// On récupère le temps du tick (le timestamp est en Ticks .NET)
			DateTime tickTime = new DateTime(context.Tick.Timestamp);
			double price = context.Tick.Median;

			// 1. Détection du changement de minute
			if (_nextPeriod == null || tickTime >= _nextPeriod)
			{
				// Si on finit une bougie, on peut l'afficher pour le debug
				if (_currentBar != null)
				{
					// DECOMMENTER LES LIGNES CI-DESSOUS POUR VOIR LES LOGS DANS LA CONSOLE
					 //Console.ForegroundColor = ConsoleColor.Cyan;
					 //Console.WriteLine(_currentBar.ToString());
					 //Console.ResetColor();
				}

				// On arrondit au début de la minute (ex: 10:05:42 -> 10:05:00)
				DateTime barStart = new DateTime(tickTime.Year, tickTime.Month, tickTime.Day,
												tickTime.Hour, tickTime.Minute, 0);

				_currentBar = new Bar
				{
					Timestamp = barStart,
					Open = price,
					High = price,
					Low = price,
					Close = price,
					Volume = context.Tick.Volume
				};

				_nextPeriod = barStart.Add(_period);
			}
			else
			{
				// 2. Mise à jour de la bougie en cours (High, Low, Close)
				if (price > _currentBar!.High) _currentBar.High = price;
				if (price < _currentBar.Low) _currentBar.Low = price;
				_currentBar.Close = price;
				_currentBar.Volume += context.Tick.Volume;
			}
		}
	}
}