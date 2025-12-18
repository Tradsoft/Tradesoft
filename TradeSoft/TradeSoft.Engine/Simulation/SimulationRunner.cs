using Disruptor;
using Disruptor.Dsl;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation.Handlers;

namespace TradeSoft.Engine.Simulation;

public sealed class SimulationRunner
{
    private readonly DisruptorConfig _config;
    private readonly IStrategy? _strategy;
    private readonly IRiskManager? _riskManager;
    private readonly IMatchingEngine? _matchingEngine;
    private readonly IBroker? _broker;

    private Disruptor<MarketEvent>? _disruptor;
    private RingBuffer<MarketEvent>? _ringBuffer;
    private CancellationTokenSource? _cancellationSource;

    public SimulationRunner(
        DisruptorConfig config,
        IStrategy? strategy = null,
        IRiskManager? riskManager = null,
        IBroker? broker = null,
        IMatchingEngine? matchingEngine = null)
    {
        _config = config;
        _strategy = strategy;
        _riskManager = riskManager;
        _broker = broker;
        _matchingEngine = matchingEngine;
    }

    public bool IsRunning => _disruptor is not null;

    public void Start()
    {
        if (_disruptor is not null)
        {
            return;
        }

        ValidateConfig();
        _cancellationSource = new CancellationTokenSource();

        _disruptor = new Disruptor<MarketEvent>(
            () => new MarketEvent(),
            _config.RingBufferSize,
            TaskScheduler.Default,
            _config.ProducerType,
            _config.WaitStrategy);

        _disruptor
        .HandleEventsWith(new StrategyEventHandler(_strategy))
        .Then(new RiskManagerEventHandler(_riskManager))
        .Then(new MatchingEventHandler(_matchingEngine))
        .Then(new BrokerEventHandler(_broker))
        .Then(new CleanupEventHandler());


        _ringBuffer = _disruptor.Start();
    }

    public void Stop()
    {
        if (_disruptor is null)
        {
            return;
        }

        _cancellationSource?.Cancel();
        _disruptor.Shutdown();
        _disruptor = null;
        _ringBuffer = null;
        _cancellationSource?.Dispose();
        _cancellationSource = null;
    }

    public void Cancel() => Stop();

    public void PublishTick(TickData tick)
    {
        Publish(evt => evt.SetTick(tick));
    }

    public void PublishOrder(OrderCommand orderCommand)
    {
        Publish(evt => evt.AttachOrder(orderCommand));
    }

    public void PublishExecutionReport(ExecutionReport executionReport)
    {
        Publish(evt => evt.AttachExecutionReport(executionReport));
    }

    private void Publish(Action<MarketEvent> composer)
    {
        if (_ringBuffer is null)
        {
            throw new InvalidOperationException("SimulationRunner must be started before publishing events.");
        }

        var sequence = _ringBuffer.Next();
        try
        {
            var evt = _ringBuffer[sequence];
            composer(evt);
        }
        finally
        {
            _ringBuffer.Publish(sequence);
        }
    }

    private void ValidateConfig()
    {
        if (!IsPowerOfTwo(_config.RingBufferSize))
        {
            throw new ArgumentException("Ring buffer size must be a power of two.", nameof(_config.RingBufferSize));
        }
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}