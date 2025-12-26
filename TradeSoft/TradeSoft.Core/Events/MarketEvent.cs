using System;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Core.Events;

/// <summary>
/// Événement recyclé dans le ring buffer.
/// Conteneur mutable : Tick + (optionnel) Order + (0..N) ExecutionReports.
/// IMPORTANT : le routing doit se faire via la présence des champs (Tick/Order/Reports),
/// pas via Type.
/// </summary>
public sealed class MarketEvent
{
    public MarketEventType Type { get; private set; } = MarketEventType.None;

    public TickData? Tick { get; private set; }
    public OrderCommand? Order { get; private set; }

    // -------------------------
    // Multi-report (v4+)
    // -------------------------
    public const int MaxReports = 8;
    private readonly ExecutionReport?[] _reports = new ExecutionReport?[MaxReports];
    private int _reportCount;

    public int ReportCount => _reportCount;
    public bool HasReports => _reportCount > 0;
    public bool HasCapacityForReport => _reportCount < MaxReports;

    public ExecutionReport? GetReport(int index)
        => (uint)index < (uint)_reportCount ? _reports[index] : null;

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

    public void AddReport(ExecutionReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));

        if (_reportCount >= MaxReports)
            throw new InvalidOperationException(
                $"MarketEvent report buffer overflow (MaxReports={MaxReports}). " +
                $"Increase MaxReports or drain reports more often.");

        _reports[_reportCount++] = report;
    }

    // Compat legacy
    public void AttachExecutionReport(ExecutionReport executionReport) => AddReport(executionReport);

    public void Reject(string? message = null)
    {
        IsRejected = true;

        if (Order is null)
            return;

        AddReport(new ExecutionReport(
            commandId: Order.CommandId,
            status: ExecutionStatus.Rejected,
            filledQuantity: 0m,
            averagePrice: null,
            message: message ?? "Rejected by risk manager",
            symbol: Order.Symbol,
            side: Order.Side));
    }

    public void Reset()
    {
        Type = MarketEventType.None;
        Tick = null;
        Order = null;
        IsRejected = false;

        for (int i = 0; i < _reportCount; i++)
            _reports[i] = null;

        _reportCount = 0;
    }
}

public enum MarketEventType
{
    None = 0,
    Tick = 1,
    OrderCommand = 2,
    ExecutionReport = 3
}
