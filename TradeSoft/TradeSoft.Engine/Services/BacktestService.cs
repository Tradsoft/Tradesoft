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
        protected override void OnProcess(TickContext ctx)
        {
            if (ctx.GeneratedBar != null)
            {
                Bars.Add(new Bar
                {
                    Open = ctx.GeneratedBar.Open,
                    High = ctx.GeneratedBar.High,
                    Low = ctx.GeneratedBar.Low,
                    Close = ctx.GeneratedBar.Close,
                    Volume = ctx.GeneratedBar.Volume,
                    Timestamp = ctx.GeneratedBar.Timestamp
                });
            }
        }
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
        protected override void OnProcess(TickContext ctx)
        {
            if (ctx.NewOrders.Count > 0)
            {
                var time = new DateTime(ctx.Tick.Timestamp);
                foreach (var o in ctx.NewOrders)
                    _logs.Add(new LogEntryDTO(time, "INTENTION", $"{o.Side} {o.Type} | Qty: {o.Quantity:F2}"));
            }
        }
    }
    public class ExecutionLogger : Handler
    {
        private readonly List<LogEntryDTO> _logs;
        public ExecutionLogger(List<LogEntryDTO> logs) { _logs = logs; }
        protected override void OnProcess(TickContext ctx)
        {
            if (ctx.Trades.Count > 0)
            {
                foreach (var t in ctx.Trades)
                    _logs.Add(new LogEntryDTO(new DateTime(t.Timestamp), "EXECUTION", $"{t.Side} | Prix: {t.Price:F5} | Qty: {t.Quantity:F2}"));
            }
        }
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
            ValidateConfig(config);

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
                    Id = t.OrderId,
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

        private static void ValidateConfig(BacktestConfig config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.Symbol))
                throw new ArgumentException("Symbol is required.", nameof(config.Symbol));

            if (config.EndDate < config.StartDate)
                throw new ArgumentException("EndDate must be greater than or equal to StartDate.", nameof(config.EndDate));

            if (config.LotSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.LotSize), "LotSize must be > 0.");

            if (config.MinLotSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.MinLotSize), "MinLotSize must be > 0.");

            if (config.MinLotSize > config.LotSize)
                throw new ArgumentException("MinLotSize must be <= LotSize.", nameof(config.MinLotSize));
        }

        private Handler CreateStrategy(BacktestConfig config)
        {
            double GetD(double def, params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (config.StrategyParameters.TryGetValue(key, out var val) && val != null)
                        return Convert.ToDouble(val);
                }
                return def;
            }

            int GetI(int def, params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (config.StrategyParameters.TryGetValue(key, out var val) && val != null)
                        return Convert.ToInt32(val);
                }
                return def;
            }

            switch (config.Strategy)
            {
                case StrategyType.RSI:
                    return new RSI(
                        GetI(14, "Period"),
                        GetD(30, "Oversold"),
                        GetD(70, "Overbought"),
                        GetD(1.0, "Lot"),
                        GetD(50, "SL Pips", "SLPips"),
                        GetD(100, "TP Pips", "TPPips"),
                        GetD(config.TickSize, "Tick Size", "TickSize"),
                        config.Symbol
                    );

                case StrategyType.MAC:
                    return new MovingAverageCrossover(
                        GetI(20, "Short Period", "ShortPeriod"),
                        GetI(50, "Long Period", "LongPeriod"),
                        GetD(1.0, "Lot"),
                        config.Symbol
                    );

                case StrategyType.MeanReversion:
                    return new MeanReversion(
                        GetI(20, "Period"),
                        GetD(2.0, "K"),
                        GetD(1.0, "Lot"),
                        config.Symbol
                    );

                case StrategyType.Breakout:
                    return new BreakoutSupportResistance(
                        GetI(20, "Lookback"),
                        GetD(1.0, "Lot"),
                        config.Symbol
                    );

                case StrategyType.Random:
                default:
                    return new RandomStrategy(config.Symbol);
            }
        }

        private BacktestResult BuildResult(PortfolioManager pm)
        {
            return StatisticsCalculator.Calculate(pm.CurrentPortfolio, pm.EquityCurve);
        }
    }
}