using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradeSoft.Core.Context;
using TradeSoft.Core.DTO;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

using Bar = TradeSoft.Core.Domain.Bar;

namespace TradeSoft.UI.Services
{
    public class BacktestService
    {
        public async Task<List<Bar>> GetHistoricalBarsAsync(BacktestConfig config)
        {
            return await Task.Run(() =>
            {
                string? dataPath = MultiFileTickProvider.FindDataDirectory(config.Symbol);
                if (dataPath == null) return new List<Bar>();

                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<MultiFileTickProvider>();

                var provider = new MultiFileTickProvider(dataPath, logger);
                var aggregator = new BarAggregator(config.Timeframe);
                var collector = new TradeSoft.Engine.Services.BarCollector();

                aggregator.SetNext(collector);

                var context = new TickContext();
                long startTicks = config.StartDate.Ticks;
                long endTicks = config.EndDate.Ticks;

                foreach (var loader in provider.GetLoaders(config.Symbol, config.StartDate, config.EndDate))
                {
                    using (loader)
                    {
                        if (loader.Count == 0) continue;
                        var ticks = loader.GetTicksAsSpan(loader.Count);
                        for (int i = 0; i < ticks.Length; i++)
                        {
                            var tick = ticks[i];
                            if (tick.Timestamp < startTicks) continue;
                            if (tick.Timestamp > endTicks) break;

                            context.Reset(tick);
                            aggregator.Execute(context);
                        }
                    }
                }

                return collector.Bars;
            });
        }

        public async Task<BacktestResult> RunBacktestAsync(BacktestConfig config, CancellationToken token)
        {
            string? dataPath = MultiFileTickProvider.FindDataDirectory(config.Symbol);

            if (dataPath == null)
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                System.Windows.MessageBox.Show(
                    $"ERREUR CRITIQUE :\nImpossible de trouver le dossier 'Data'.\n\nJ'ai cherché dans :\n{Path.Combine(baseDir, "Data", config.Symbol)}\net dans les dossiers parents.",
                    "Dossier Data Introuvable");
                throw new FileNotFoundException("Data introuvable. Vérifiez l'installation.");
            }

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<MultiFileTickProvider>();
            var provider = new MultiFileTickProvider(dataPath, logger);
            var engineService = new TradeSoft.Engine.Services.BacktestService(provider, loggerFactory);

            return await engineService.RunBacktestAsync(config, null, token);
        }
    }
}
