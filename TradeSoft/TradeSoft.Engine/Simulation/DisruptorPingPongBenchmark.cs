using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor;
using Disruptor.Dsl;

namespace TradeSoft.Engine.Simulation;

public sealed class DisruptorPingPongBenchmark
{
    public const long DefaultEventCount = 10_000_000;
    private const int DefaultRingBufferSize = 1024 * 64;

    public DisruptorPingPongResult Run(long eventCount = DefaultEventCount, int? ringBufferSize = null, TimeSpan? timeout = null)
    {
        if (eventCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventCount), "Event count must be positive.");

        var ringSize = ringBufferSize ?? DefaultRingBufferSize;
        if (!IsPowerOfTwo(ringSize))
            throw new ArgumentException("Ring buffer size must be a power of two.", nameof(ringBufferSize));

        using var completion = new ManualResetEventSlim(false);
        var handler = new CountingEventHandler(eventCount, completion);

        var disruptor = new Disruptor<PingPongEvent>(
            () => new PingPongEvent(),
            ringSize,
            TaskScheduler.Default,
            ProducerType.Single,
            new BusySpinWaitStrategy());

        disruptor.HandleEventsWith(handler);
        var ringBuffer = disruptor.Start();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();

        for (long i = 0; i < eventCount; i++)
        {
            using var scope = ringBuffer.PublishEvent(); 
            var evt = scope.Event();                     
            evt.Value = i;                              
        } 

        var waitTimeout = timeout ?? TimeSpan.FromSeconds(30);
        if (!completion.Wait(waitTimeout))
            throw new TimeoutException($"Disruptor did not process {eventCount} events within {waitTimeout.TotalSeconds} seconds.");

        stopwatch.Stop();
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        disruptor.Shutdown();

        var processed = handler.ProcessedCount;
        var elapsed = stopwatch.Elapsed;
        var throughput = processed / elapsed.TotalSeconds;

        return new DisruptorPingPongResult(
            processed,
            elapsed,
            throughput,
            Math.Max(0, allocatedAfter - allocatedBefore));
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

    public sealed class PingPongEvent
    {
        public long Value;
    }

    private sealed class CountingEventHandler : IEventHandler<PingPongEvent>
    {
        private readonly long _expectedCount;
        private readonly ManualResetEventSlim _completion;
        private long _processed;

        public CountingEventHandler(long expectedCount, ManualResetEventSlim completion)
        {
            _expectedCount = expectedCount;
            _completion = completion;
        }

        public long ProcessedCount => Interlocked.Read(ref _processed);

        public void OnEvent(PingPongEvent data, long sequence, bool endOfBatch)
        {
            var current = Interlocked.Increment(ref _processed);
            data.Value = 0;

            if (current == _expectedCount)
                _completion.Set();
        }
    }
}

public sealed record DisruptorPingPongResult(
    long ProcessedEvents,
    TimeSpan Elapsed,
    double ThroughputEventsPerSecond,
    long AllocatedBytesOnHotPath)
{
    public string FormatReport() =>
        $"{ProcessedEvents:N0} events processed in {Elapsed.TotalSeconds:F2}s ({ThroughputEventsPerSecond:N0} events/s), allocations on hot path: {AllocatedBytesOnHotPath} bytes.";
}
