using System;
using Microsoft.Extensions.Logging;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.Pipeline;
using TradeSoft.Infrastructure.IO;

namespace TradeSoft.Engine
{
    public class BacktestRunner
    {
        private readonly MultiFileTickProvider _provider;
        private readonly Handler _pipelineHead;
        private readonly ILogger _logger;

        public BacktestRunner(MultiFileTickProvider provider, Handler pipelineHead, ILogger logger)
        {
            _provider = provider;
            _pipelineHead = pipelineHead;
            _logger = logger;
        }

        public void Run(string symbol, DateTime start, DateTime end)
        {
            _logger.LogInformation("Démarrage du backtest...");

            // 1. Pré-allocation du contexte (Zéro Allocation durant le Run)
            TickContext context = new TickContext();

            long totalTicks = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // 2. Boucle sur les fichiers
            foreach (var loader in _provider.GetLoaders(symbol, start, end))
            {
                using (loader)
                {
                    // Récupération du Span brut (très rapide)
                    var ticks = loader.GetTicksAsSpan(loader.Count);
                    totalTicks += ticks.Length;

                    // 3. LA BOUCLE HOT PATH (Critique pour la perf)
                    for (int i = 0; i < ticks.Length; i++)
                    {
                        // A. Reset du contexte avec le nouveau tick
                        // 'ref readonly' évite la copie si le compilateur le décide, mais TickData est petit (25o)
                        context.Reset(ticks[i]);

                        // B. Injection dans le pipeline
                        _pipelineHead.Execute(context);
                    }
                }
            }

            watch.Stop();
            _logger.LogInformation("Backtest terminé.");
            _logger.LogInformation("Performance : {Ticks:N0} ticks traités en {Time} ms ({Rate:N0} ticks/sec)",
                totalTicks, watch.ElapsedMilliseconds, totalTicks / (watch.Elapsed.TotalSeconds + 0.001));
        }
    }
}