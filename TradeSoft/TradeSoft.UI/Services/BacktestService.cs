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
using TradeSoft.Engine.Management;
using TradeSoft.Engine.Execution;
using TradeSoft.Engine.Analysis;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

// Alias pour éviter les conflits
using Bar = TradeSoft.Core.Domain.Bar;

namespace TradeSoft.UI.Services
{
    // Handler pour capturer les barres générées pendant le backtest
    public class BarCollector : Handler
    {
        public List<Bar> Bars { get; } = new List<Bar>();

        protected override void OnProcess(TickContext context)
        {
            if (context.GeneratedBar != null)
            {
                var b = context.GeneratedBar;
                Bars.Add(new Bar
                {
                    Open = b.Open,
                    High = b.High,
                    Low = b.Low,
                    Close = b.Close,
                    Timestamp = b.Timestamp
                });
            }
        }
    }

    public class BacktestService
    {
        public async Task<BacktestResult> RunBacktestAsync(BacktestConfig config, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<MultiFileTickProvider>();
                var runnerLogger = loggerFactory.CreateLogger<TradeSoft.Engine.BacktestRunner>();

                string? dataPath = FindDataPath();
                if (dataPath == null) throw new FileNotFoundException("Data introuvable.");

                var provider = new MultiFileTickProvider(dataPath, logger);
                var aggregator = new BarAggregator(config.Timeframe);
                
                // Créer un collecteur de barres
                var barCollector = new BarCollector();
                
                // Créer le pipeline complet
                var portfolioManager = new PortfolioManager(config.InitialCash);
                var broker = new SimulatedBroker(config);
                var riskManager = new RiskManager(config, portfolioManager);
                Handler strategy = CreateStrategy(config.Strategy, config.StrategyParameters);

                // Chaîner les handlers: aggregator -> barCollector -> strategy -> riskManager -> broker -> portfolioManager
                aggregator.SetNext(barCollector);
                barCollector.SetNext(strategy);
                strategy.SetNext(riskManager);
                riskManager.SetNext(broker);
                broker.SetNext(portfolioManager);

                // Exécuter le backtest avec le runner
                var runner = new TradeSoft.Engine.BacktestRunner(provider, aggregator, runnerLogger);
                runner.Run(config.Symbol, config.StartDate, config.EndDate, null, token);

                System.Diagnostics.Debug.WriteLine($"Bars collected: {barCollector.Bars.Count}");
                System.Diagnostics.Debug.WriteLine($"Portfolio Manager Equity Curve Count: {portfolioManager.EquityCurve.Count}");
                System.Diagnostics.Debug.WriteLine($"Portfolio Final Equity: {portfolioManager.CurrentPortfolio.Equity}");
                System.Diagnostics.Debug.WriteLine($"Portfolio Trade History: {portfolioManager.TradeHistory.Count}");
                
                // Afficher les premiers points de la courbe d'équité
                if (portfolioManager.EquityCurve.Count > 0)
                {
                    var first = portfolioManager.EquityCurve[0];
                    var last = portfolioManager.EquityCurve[portfolioManager.EquityCurve.Count - 1];
                    System.Diagnostics.Debug.WriteLine($"First equity point: {first.Time} = {first.Equity}");
                    System.Diagnostics.Debug.WriteLine($"Last equity point: {last.Time} = {last.Equity}");
                }

                // Construire le résultat avec statistiques et courbe d'équité
                var result = StatisticsCalculator.Calculate(portfolioManager.CurrentPortfolio, portfolioManager.EquityCurve);
                
                System.Diagnostics.Debug.WriteLine($"Result Equity Curve Count: {result.EquityCurve.Count}");
                System.Diagnostics.Debug.WriteLine($"Result Total Trades: {result.TotalTrades}");
                
                if (result.EquityCurve.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Result EquityCurve is EMPTY!");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"First result equity: {result.EquityCurve[0].Time} = {result.EquityCurve[0].Value}");
                    System.Diagnostics.Debug.WriteLine($"Last result equity: {result.EquityCurve[result.EquityCurve.Count - 1].Time} = {result.EquityCurve[result.EquityCurve.Count - 1].Value}");
                }
                
                // Utiliser les barres collectées pendant le backtest
                result.Bars = barCollector.Bars;
                return result;
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
                @"C:\Users\Pc\Bureau\Projet\Data\EURUSD",
                @"C:\Users\inesm\OneDrive\Documents\M2\Tradesoft\Data\EURUSD",
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD"))
            };
            return paths.FirstOrDefault(Directory.Exists);
        }
    }
}