using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Core.Events;

/// <summary>
/// Événement recyclé dans le ring buffer pour transporter données marché ou trading.
/// </summary>
public sealed class MarketEvent
{
    public MarketEventType Type { get; private set; } = MarketEventType.None;
    public TickData? Tick { get; private set; }
    public OrderCommand? Order { get; private set; }
    public ExecutionReport? ExecutionReport { get; private set; }

    public void SetTick(in TickData tick)
    {
        Reset();
        Type = MarketEventType.Tick;
        Tick = tick;
    }

    public void SetOrder(OrderCommand orderCommand)
    {
        Reset();
        Type = MarketEventType.OrderCommand;
        Order = orderCommand;
    }

    public void SetExecutionReport(ExecutionReport executionReport)
    {
        Reset();
        Type = MarketEventType.ExecutionReport;
        ExecutionReport = executionReport;
    }

    public void Reset()
    {
        Type = MarketEventType.None;
        Tick = null;
        Order = null;
        ExecutionReport = null;
    }
}

public enum MarketEventType
{
    None = 0,
    Tick = 1,
    OrderCommand = 2,
    ExecutionReport = 3
}