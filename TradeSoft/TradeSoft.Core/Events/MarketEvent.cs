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


    public bool IsRejected { get; set; }
    public void SetTick(in TickData tick)
    {
        Reset();
        Type = MarketEventType.Tick;
        Tick = tick;
    }

    public void AttachOrder(OrderCommand orderCommand)
    {
        Order = orderCommand;
        Type = MarketEventType.OrderCommand;
    }

    public void AttachExecutionReport(ExecutionReport executionReport)
    {
        ExecutionReport = executionReport;
        Type = MarketEventType.ExecutionReport;
    }

    public void Reject(string? message = null)
    {
        IsRejected = true;

        if(Order is null)
            return;

        AttachExecutionReport(new ExecutionReport(
            commandId: Order.CommandId,
            status: ExecutionStatus.Rejected,
            filledQuantity: 0m,
            averagePrice: null,
            message: message ?? "Rejected by risk manager"));
    }

    public void Reset()
    {
        Type = MarketEventType.None;
        Tick = null;
        Order = null;
        ExecutionReport = null;
        IsRejected = false;
    }
}

public enum MarketEventType
{
    None = 0,
    Tick = 1,
    OrderCommand = 2,
    ExecutionReport = 3
}