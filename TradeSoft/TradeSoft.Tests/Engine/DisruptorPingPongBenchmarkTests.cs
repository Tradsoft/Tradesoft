using System;
using FluentAssertions;
using TradeSoft.Engine.Simulation;

namespace TradeSoft.Tests.Engine;

public class DisruptorPingPongBenchmarkTests
{
    [Fact]
    public void PingPongBenchmarkProcessesAllEventsWithMinimalAllocations()
    {
        var benchmark = new DisruptorPingPongBenchmark();

        var result = benchmark.Run(eventCount: 100_000, ringBufferSize: 1024, timeout: TimeSpan.FromSeconds(10));

        result.ProcessedEvents.Should().Be(100_000);
        result.ThroughputEventsPerSecond.Should().BeGreaterThan(0);
        result.AllocatedBytesOnHotPath.Should().BeLessThan(512);
    }
}