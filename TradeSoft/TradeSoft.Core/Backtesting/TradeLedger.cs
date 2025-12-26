using System;
using System.Collections.Generic;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Backtesting;

/// <summary>
/// Capture les fills (execution reports) pour sortir un "journal de trades".
/// Compatible multi-reports + partial fills.
/// </summary>
public sealed class TradeLedger : IBroker
{
    private readonly List<TradeFill> _fills = new();

    public IReadOnlyList<TradeFill> Fills => _fills;

    public void OnTick(in TickData tick)
    {
    }

    public void OnExecutionReport(ExecutionReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));

        if (report.Status != ExecutionStatus.Filled &&
            report.Status != ExecutionStatus.PartiallyFilled)
            return;

        if (string.IsNullOrWhiteSpace(report.Symbol))
            throw new InvalidOperationException("ExecutionReport.Symbol is required.");

        if (report.Side is null)
            throw new InvalidOperationException("ExecutionReport.Side is required.");

        if (report.AveragePrice is null)
            throw new InvalidOperationException("Filled/Partial report must include AveragePrice.");

        if (report.FilledQuantity <= 0m)
            return;

        _fills.Add(new TradeFill(
            CommandId: report.CommandId,
            Symbol: report.Symbol!,
            Side: report.Side.Value,
            Status: report.Status,
            Quantity: report.FilledQuantity,
            Price: report.AveragePrice.Value,
            Timestamp: report.Timestamp
        ));
    }
}
