using System;
using System.Threading; 
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

        public void Run(string symbol, DateTime start, DateTime end, IProgress<float>? progress = null, CancellationToken ct = default)
        {
            _logger.LogInformation("Démarrage du backtest...");

            if (end < start)
            {
                (start, end) = (end, start);
            }
            if (end == start)
            {
                _logger.LogWarning("Backtest ignoré: end == start ({Start:O}).", start);
                return;
            }

            long startTicks = start.Ticks;
            long endTicks = end.Ticks;

            long totalDurationTicks = endTicks - startTicks;

            TickContext context = new TickContext();

            long totalTicksProcessed = 0;
            long skippedBeforeStart = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var loader in _provider.GetLoaders(symbol, start, end))
            {
                if (ct.IsCancellationRequested) break;

                using (loader)
                {
                    var ticks = loader.GetTicksAsSpan(loader.Count);
                    if (ticks.Length == 0) continue;

                    long lastCurrentTimestamp = 0;

                    for (int i = 0; i < ticks.Length; i++)
                    {
                        if (i % 1000 == 0 && ct.IsCancellationRequested)
                        {
                            _logger.LogInformation("Backtest annulé par l'utilisateur.");
                            return;
                        }

                        var tick = ticks[i];
                        lastCurrentTimestamp = tick.Timestamp;

                        if (tick.Timestamp < startTicks)
                        {
                            skippedBeforeStart++;
                            continue;
                        }
                        if (tick.Timestamp >= endTicks)
                        {
                            break;
                        }

                        totalTicksProcessed++;

                        context.Reset(tick);

                        _pipelineHead.Execute(context);
                    }

                    if (progress != null && totalDurationTicks > 0 && lastCurrentTimestamp > 0)
                    {
                        float percent = (float)(lastCurrentTimestamp - startTicks) / totalDurationTicks;

                        float safePercent = Math.Min(1.0f, Math.Max(0.0f, percent));

                        progress.Report(safePercent);
                    }
                }
            }

            watch.Stop();
            _logger.LogInformation("Backtest terminé.");

            if (skippedBeforeStart > 0)
            {
                _logger.LogInformation("{Count:N0} ticks ignorés (< start).", skippedBeforeStart);
            }

            double elapsedSeconds = watch.Elapsed.TotalSeconds > 0 ? watch.Elapsed.TotalSeconds : 0.001;

            _logger.LogInformation("Performance : {Ticks:N0} ticks traités en {Time} ms ({Rate:N0} ticks/sec)",
                totalTicksProcessed,
                watch.ElapsedMilliseconds,
                totalTicksProcessed / elapsedSeconds);
        }
    }
}