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

            // 1. Normalisation des dates : fenêtre [start, end)
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

            // Calcul de la durée totale pour la barre de progression (en Ticks .NET)
            long totalDurationTicks = endTicks - startTicks;

            // 2. Pré-allocation du contexte (Zéro Allocation durant le Run)
            TickContext context = new TickContext();

            long totalTicksProcessed = 0;
            long skippedBeforeStart = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // 3. Boucle sur les fichiers
            foreach (var loader in _provider.GetLoaders(symbol, start, end))
            {
                // VÉRIFICATION ANNULATION (Entre chaque fichier)
                if (ct.IsCancellationRequested) break;

                using (loader)
                {
                    // Récupération du Span brut (très rapide)
                    var ticks = loader.GetTicksAsSpan(loader.Count);
                    if (ticks.Length == 0) continue;

                    // Variable pour stocker le temps actuel afin de calculer la progression à la fin du bloc
                    long lastCurrentTimestamp = 0;

                    // 4. LA BOUCLE HOT PATH (Critique pour la perf)
                    for (int i = 0; i < ticks.Length; i++)
                    {
                        // VÉRIFICATION ANNULATION (Toutes les 1000 itérations pour ne pas tuer la perf)
                        if (i % 1000 == 0 && ct.IsCancellationRequested)
                        {
                            _logger.LogInformation("Backtest annulé par l'utilisateur.");
                            return;
                        }

                        var tick = ticks[i];
                        lastCurrentTimestamp = tick.Timestamp;

                        // Respect strict de la plage [start, end)
                        if (tick.Timestamp < startTicks)
                        {
                            skippedBeforeStart++;
                            continue;
                        }
                        if (tick.Timestamp >= endTicks)
                        {
                            // Les ticks sont triés, on peut arrêter le fichier ici
                            break;
                        }

                        totalTicksProcessed++;

                        // A. Reset du contexte avec le nouveau tick
                        context.Reset(tick);

                        // B. Injection dans le pipeline
                        _pipelineHead.Execute(context);
                    }

                    // 5. MISE A JOUR PROGRESSION
                    // On le fait une fois par fichier chargé pour éviter de ralentir la boucle for
                    if (progress != null && totalDurationTicks > 0 && lastCurrentTimestamp > 0)
                    {
                        // Calcul : (Temps Actuel - Temps Début) / Durée Totale
                        float percent = (float)(lastCurrentTimestamp - startTicks) / totalDurationTicks;

                        // Sécurisation entre 0.0 et 1.0 (Math.Clamp dispo en .NET 6+, sinon Math.Min/Max)
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

            // Éviter la division par zéro dans les logs
            double elapsedSeconds = watch.Elapsed.TotalSeconds > 0 ? watch.Elapsed.TotalSeconds : 0.001;

            _logger.LogInformation("Performance : {Ticks:N0} ticks traités en {Time} ms ({Rate:N0} ticks/sec)",
                totalTicksProcessed,
                watch.ElapsedMilliseconds,
                totalTicksProcessed / elapsedSeconds);
        }
    }
}