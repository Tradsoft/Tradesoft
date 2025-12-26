using System;
using System.Collections.Generic;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation;

public sealed class CompositeBroker : IBroker
{
    private readonly IBroker[] _brokers;

    public CompositeBroker(params IBroker[] brokers)
    {
        _brokers = brokers ?? throw new ArgumentNullException(nameof(brokers));
    }

    public void OnTick(in TickData tick)
    {
        for (int i = 0; i < _brokers.Length; i++)
            _brokers[i].OnTick(in tick);
    }

    public void OnExecutionReport(ExecutionReport report)
    {
        for (int i = 0; i < _brokers.Length; i++)
            _brokers[i].OnExecutionReport(report);
    }
}
