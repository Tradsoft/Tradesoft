using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Pipeline;
using TradeSoft.Engine.Strategies;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

// Alias pour éviter les conflits
using Bar = TradeSoft.Core.Domain.Bar;

namespace TradeSoft.UI.Services
{
    public class BacktestService
    {
        public async Task<BacktestResult> RunBacktestAsync(BacktestConfig config, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<MultiFileTickProvider>();

                string? dataPath = FindDataPath();
                if (dataPath == null) throw new FileNotFoundException("Data introuvable.");

                var provider = new MultiFileTickProvider(dataPath, logger);
                var aggregator = new BarAggregator(config.Timeframe);
                var context = new TickContext();

                Handler strategy = CreateStrategy(config.Strategy, config.StrategyParameters);

                var bars = new List<Bar>();

                foreach (var tick in provider.GetTicks(config.Symbol, config.StartDate, config.EndDate))
                {
                    if (token.IsCancellationRequested) break;

                    context.Reset(tick);
                    aggregator.Execute(context);
                    strategy.Execute(context);

                    if (context.GeneratedBar != null)
                    {
                        var b = context.GeneratedBar;
                        bars.Add(new Bar
                        {
                            Open = b.Open,
                            High = b.High,
                            Low = b.Low,
                            Close = b.Close,
                            Timestamp = b.Timestamp
                        });
                    }
                }

                return new BacktestResult
                {
                    Bars = bars,
                    FinalEquity = config.InitialCash
                };
            }, token);
        }

        private Handler CreateStrategy(StrategyType type, Dictionary<string, object> parameters)
        {
            double GetD(string key, double def) => parameters.ContainsKey(key) ? Convert.ToDouble(parameters[key]) : def;
            int GetI(string key, int def) => parameters.ContainsKey(key) ? Convert.ToInt32(parameters[key]) : def;

            return type switch
            {
                StrategyType.RSI => new RSI(
                    period: GetI("Period", 14),
                    oversold: GetD("Oversold", 30),
                    overbought: GetD("Overbought", 70),
                    lot: GetD("Lot", 1.0),
                    slPips: GetD("SL Pips", 50),
                    tpPips: GetD("TP Pips", 100)),

                StrategyType.MAC => new MovingAverageCrossover(
                    shortPeriod: GetI("Short Period", 20),
                    longPeriod: GetI("Long Period", 50),
                    lot: GetD("Lot", 1.0)),

                StrategyType.MeanReversion => new MeanReversion(
                    period: GetI("Period", 20),
                    k: GetD("K", 2.0),
                    lot: GetD("Lot", 1.0)),

                StrategyType.Breakout => new BreakoutSupportResistance(
                    lookback: GetI("Lookback", 20),
                    lot: GetD("Lot", 1.0)),

                _ => new StrategyRouter()
            };
        }

        private string? FindDataPath()
        {
            // Vérifiez bien ces chemins sur votre PC
            string[] paths = new[] {
                @"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\EURUSD\EURUSD",
                @"C:\Users\louis\source\repos\Tradesoft\Data\EURUSD",
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD"))
            };
            return paths.FirstOrDefault(Directory.Exists);
        }
    }
}