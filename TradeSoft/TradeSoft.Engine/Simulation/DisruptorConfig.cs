using Disruptor;
using Disruptor.Dsl;

namespace TradeSoft.Engine.Simulation;

public sealed class DisruptorConfig
{
    public int RingBufferSize { get; init; } = 1024;
    public ProducerType ProducerType { get; init; } = ProducerType.Single;
    public IWaitStrategy WaitStrategy { get; init; } = new BlockingWaitStrategy();

    public static DisruptorConfig Default => new();
}