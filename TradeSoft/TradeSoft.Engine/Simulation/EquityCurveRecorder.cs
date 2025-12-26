using System;
using System.Collections.Generic;
using TradeSoft.Core.Backtesting;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Backtesting;

public sealed class EquityCurveRecorder : IBroker
{
    private readonly IBrokerState _state;
    private readonly string _symbol;
    private readonly List<EquitySnapshot> _curve = new();

    public EquityCurveRecorder(IBrokerState state, string symbol)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _symbol = string.IsNullOrWhiteSpace(symbol) ? throw new ArgumentException("Symbol is required.", nameof(symbol)) : symbol;
    }

    public IReadOnlyList<EquitySnapshot> Curve => _curve;

    public void OnTick(in TickData tick)
    {
        if (_state.Bid is null || _state.Ask is null || _state.MarkPrice is null)
            return;

        _curve.Add(new EquitySnapshot(
            Timestamp: tick.Timestamp,
            Symbol: _symbol,

            Bid: _state.Bid.Value,
            Ask: _state.Ask.Value,
            MarkPrice: _state.MarkPrice.Value,

            Cash: _state.Cash,
            Position: _state.Position,
            AveragePrice: _state.AveragePrice,

            NetLiquidationValue: _state.NetLiquidationValue,
            RealizedPnl: _state.RealizedPnl,
            UnrealizedPnl: _state.UnrealizedPnl
        ));
    }

    public void OnExecutionReport(ExecutionReport report)
    {
    }
}
