using System;
using System.Collections.Generic;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Backtesting;

/// <summary>
/// Recorder des fills (ExecutionReport) => liste de TradeFill.
/// Compatible multi-reports + partial fills.
/// </summary>
public sealed class TradeLedgerRecorder : IBroker
{
    private readonly string? _symbolFilter;
    private readonly List<TradeFill> _fills = new();

    public TradeLedgerRecorder(string? symbolFilter = null)
    {
        _symbolFilter = string.IsNullOrWhiteSpace(symbolFilter) ? null : symbolFilter;
    }

    public IReadOnlyList<TradeFill> Fills => _fills;

    public void OnTick(in TickData tick) { }

    public void OnExecutionReport(ExecutionReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));

        if (report.Status != ExecutionStatus.Filled &&
            report.Status != ExecutionStatus.PartiallyFilled)
            return;

        if (string.IsNullOrWhiteSpace(report.Symbol))
            throw new InvalidOperationException("ExecutionReport.Symbol is required.");

        if (_symbolFilter is not null &&
            !string.Equals(report.Symbol, _symbolFilter, StringComparison.OrdinalIgnoreCase))
            return;

        if (report.Side is null)
            throw new InvalidOperationException("ExecutionReport.Side is required.");

        if (report.AveragePrice is null)
            throw new InvalidOperationException("Filled/Partial report must include AveragePrice.");

        if (report.FilledQuantity <= 0m)
            return;

        var side = report.Side.Value;
        var qty = report.FilledQuantity;
        var px = report.AveragePrice.Value;

        _fills.Add(new TradeFill(
            CommandId: report.CommandId,
            Symbol: report.Symbol!,
            Side: side,
            Status: report.Status,
            Quantity: qty,
            SignedQuantity: side == OrderSide.Buy ? +qty : -qty,
            Price: px,
            Notional: qty * px,
            Timestamp: report.Timestamp
        ));
    }
}
