using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Core.Events;

/// <summary>
/// Événement recyclé dans le ring buffer.
/// Conteneur mutable : Tick + (optionnel) Order + (optionnel) ExecutionReport.
/// IMPORTANT : le "routing" dans le pipeline doit se faire via la présence des champs,
/// pas via Type, sinon un AttachOrder() peut masquer un Tick.
/// </summary>
public sealed class MarketEvent
{
    // Type est conservé uniquement comme "type source" (debug/inspection).
    // On ne le modifie pas lors des Attach*.
    public MarketEventType Type { get; private set; } = MarketEventType.None;

    public TickData? Tick { get; private set; }
    public OrderCommand? Order { get; private set; }
    public ExecutionReport? ExecutionReport { get; private set; }

    public bool IsRejected { get; private set; }

    public void SetTick(in TickData tick)
    {
        Reset();
        Type = MarketEventType.Tick;
        Tick = tick;
    }

    public void AttachOrder(OrderCommand orderCommand)
    {
        Order = orderCommand;
    }

    public void AttachExecutionReport(ExecutionReport executionReport)
    {
        ExecutionReport = executionReport;
    }

    public void Reject(string? message = null)
    {
        IsRejected = true;

        if (Order is null)
            return;

        ExecutionReport = new ExecutionReport(
            commandId: Order.CommandId,
            status: ExecutionStatus.Rejected,
            filledQuantity: 0m,
            averagePrice: null,
            message: message ?? "Rejected by risk manager");
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
