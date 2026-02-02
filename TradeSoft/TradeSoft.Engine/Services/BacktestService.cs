using System;
using System.Collections.Generic;
using System.Linq; // Indispensable pour OrderByDescending et Select
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradeSoft.Core.Context;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Pipeline;
using TradeSoft.Core.Services;
using TradeSoft.Engine.Analysis;
using TradeSoft.Engine.Execution;
using TradeSoft.Engine.Management;
using TradeSoft.Engine.Strategies;
using TradeSoft.Infrastructure.IO;
using TradeSoft.Infrastructure.Processing;

namespace TradeSoft.Engine.Services
{
    public class BarCollector : Handler
    {
        public List<Bar> Bars { get; } = new List<Bar>();
        protected override void OnProcess(TickContext ctx) { if (ctx.GeneratedBar != null) Bars.Add(new Bar { Open = ctx.GeneratedBar.Open, High = ctx.GeneratedBar.High, Low = ctx.GeneratedBar.Low, Close = ctx.GeneratedBar.Close, Timestamp = ctx.GeneratedBar.Timestamp }); }
    }
    public class TradeCollector : Handler
    {
        public List<Trade> AllTrades { get; } = new List<Trade>();
        protected override void OnProcess(TickContext ctx) { if (ctx.Trades.Count > 0) AllTrades.AddRange(ctx.Trades); }
    }
    public class OrderLogger : Handler
    {
        private readonly List<LogEntryDTO> _logs;
        public OrderLogger(List<LogEntryDTO> logs) { _logs = logs; }
        protected override void OnProcess(TickContext ctx) { if (ctx.NewOrders.Count > 0) foreach (var o in ctx.NewOrders) _logs.Add(new LogEntryDTO(new DateTime(ctx.Tick.Timestamp), "INTENTION", $"{o.Side} {o.Type} {o.Quantity:F2}")); }
    }
    public class ExecutionLogger : Handler
    {
        private readonly List<LogEntryDTO> _logs;
        public ExecutionLogger(List<LogEntryDTO> logs) { _logs = logs; }
        protected override void OnProcess(TickContext ctx) { if (ctx.Trades.Count > 0) foreach (var t in ctx.Trades) _logs.Add(new LogEntryDTO(new DateTime(t.Timestamp), "EXECUTION", $"{t.Side} @ {t.Price:F5}")); }
    }

    public class BacktestService : IBacktestService
    {
        private readonly MultiFileTickProvider _provider;
        private readonly ILoggerFactory _loggerFactory;

        public BacktestService(MultiFileTickProvider provider, ILoggerFactory loggerFactory)
        {
            _provider = provider;
            _loggerFactory = loggerFactory;
        }

        public async Task<BacktestResult> RunBacktestAsync(
            BacktestConfig config,
            IProgress<float>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var capturedLogs = new List<LogEntryDTO>();

                var portfolioManager = new PortfolioManager(config.InitialCash);
                var broker = new SimulatedBroker(config, capturedLogs);
                var riskManager = new RiskManager(config, portfolioManager, capturedLogs);

                var aggregator = new BarAggregator(config.Timeframe);

                Handler strategy = CreateStrategy(config);

                var barCollector = new BarCollector();
                var tradeCollector = new TradeCollector();
                var orderLogger = new OrderLogger(capturedLogs);
                var tradeLogger = new ExecutionLogger(capturedLogs);

                aggregator.SetNext(barCollector);
                barCollector.SetNext(strategy);
                strategy.SetNext(orderLogger);
                orderLogger.SetNext(riskManager);
                riskManager.SetNext(broker);
                broker.SetNext(tradeCollector);
                tradeCollector.SetNext(tradeLogger);
                tradeLogger.SetNext(portfolioManager);

                var runnerLogger = _loggerFactory.CreateLogger<BacktestRunner>();
                var runner = new BacktestRunner(_provider, aggregator, runnerLogger);

                runner.Run(config.Symbol, config.StartDate, config.EndDate, progress, cancellationToken);

                var result = BuildResult(portfolioManager);

                result.Bars = barCollector.Bars;
                result.Trades = tradeCollector.AllTrades.Select(t => new TradeResultItem
                {
                    Id = 0, 
                    Time = new DateTime(t.Timestamp),
                    Side = t.Side.ToString(),
                    Price = t.Price,
                    Quantity = t.Quantity,
                    PnL = 0
                }).ToList();

                result.Logs = capturedLogs.OrderByDescending(l => l.Time).ToList();

                if (result.EquityCurve.Count == 0)
                    result.FinalEquity = config.InitialCash;

                return result;

            }, cancellationToken);
        }

        private Handler CreateStrategy(BacktestConfig config)
        {
            double GetD(string key, double def) => config.StrategyParameters.ContainsKey(key) ? Convert.ToDouble(config.StrategyParameters[key]) : def;
            int GetI(string key, int def) => config.StrategyParameters.ContainsKey(key) ? Convert.ToInt32(config.StrategyParameters[key]) : def;

            switch (config.Strategy)
            {
                case StrategyType.RSI:
                    return new RSI(
                        GetI("Period", 14),
                        GetD("Oversold", 30),
                        GetD("Overbought", 70),
                        GetD("Lot", 1.0),
                        GetD("SL Pips", 50),
                        GetD("TP Pips", 100),
                        config.TickSize 
                    );

                case StrategyType.MAC:
                    return new MovingAverageCrossover(
                        GetI("Short Period", 20),
                        GetI("Long Period", 50),
                        GetD("Lot", 1.0)
                    );

                case StrategyType.MeanReversion:
                    return new MeanReversion(
                        GetI("Period", 20),
                        GetD("K", 2.0),
                        GetD("Lot", 1.0)
                    );

                case StrategyType.Breakout:
                    return new BreakoutSupportResistance(
                        GetI("Lookback", 20),
                        GetD("Lot", 1.0)
                    );

                case StrategyType.Random:
                default:
                    return new RandomStrategy();
            }
        }

        private BacktestResult BuildResult(PortfolioManager pm)
        {
            return StatisticsCalculator.Calculate(pm.CurrentPortfolio, pm.EquityCurve);
        }
    }
}