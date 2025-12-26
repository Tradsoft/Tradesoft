using System;
using TradeSoft.Core.Backtesting;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;

namespace TradeSoft.Engine.Simulation;

public sealed class SimulatedBroker : IBroker, IBrokerState
{
    private readonly InstrumentSpec _spec;
    private readonly BrokerFees _fees;

    private decimal _cash;
    private decimal _position;  
    private decimal _avgPrice;   
    private decimal _realizedPnl;

    private decimal? _lastBid;
    private decimal? _lastAsk;

    public SimulatedBroker(InstrumentSpec spec, decimal initialCash = 0m, BrokerFees? fees = null)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        _cash = initialCash;
        _fees = fees ?? BrokerFees.None;
    }

    public decimal Cash => _cash;
    public decimal Position => _position;
    public decimal AveragePrice => _avgPrice;
    public decimal RealizedPnl => _realizedPnl;

    public decimal? Bid => _lastBid;
    public decimal? Ask => _lastAsk;

    public decimal? MarkPrice
    {
        get
        {
            if (_lastBid is null || _lastAsk is null) return null;

            if (_position == 0m) return (_lastBid.Value + _lastAsk.Value) / 2m;

            return _position > 0m ? _lastBid.Value : _lastAsk.Value;
        }
    }

    public decimal UnrealizedPnl
    {
        get
        {
            if (_position == 0m) return 0m;
            if (_lastBid is null || _lastAsk is null) return 0m;

            var mark = _position > 0m ? _lastBid.Value : _lastAsk.Value;

            return _position > 0m
                ? (mark - _avgPrice) * _position
                : (_avgPrice - mark) * Math.Abs(_position);
        }
    }

    public decimal NetLiquidationValue
    {
        get
        {
            if (_position == 0m) return _cash;
            if (_lastBid is null || _lastAsk is null) return _cash;

            var mark = _position > 0m ? _lastBid.Value : _lastAsk.Value;
            return _cash + (_position * mark);
        }
    }

    public void OnTick(in TickData tick)
    {
        _lastBid = _spec.RoundPrice((decimal)tick.Bid);
        _lastAsk = _spec.RoundPrice((decimal)tick.Ask);
    }

    public void OnExecutionReport(ExecutionReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));

        if (report.Status != ExecutionStatus.Filled && report.Status != ExecutionStatus.PartiallyFilled)
            return;

        if (string.IsNullOrWhiteSpace(report.Symbol))
            throw new InvalidOperationException("ExecutionReport.Symbol is required for broker accounting.");

        if (report.Side is null)
            throw new InvalidOperationException("ExecutionReport.Side is required for broker accounting.");

        if (!string.Equals(report.Symbol, _spec.Symbol, StringComparison.OrdinalIgnoreCase))
            return;

        if (report.AveragePrice is null)
            throw new InvalidOperationException("Filled/Partial report must include AveragePrice.");

        var qty = report.FilledQuantity;
        if (qty <= 0m) return;

        var px = _spec.RoundPrice(report.AveragePrice.Value);
        var signedQty = report.Side.Value == OrderSide.Buy ? +qty : -qty;

        ApplyFill(signedQty, px);

        var fee = _fees.ComputeFee(qty, px);
        _cash -= fee;
    }

    private void ApplyFill(decimal signedQty, decimal fillPrice)
    {
        _cash -= signedQty * fillPrice;

        if (_position == 0m)
        {
            _position = signedQty;
            _avgPrice = fillPrice;
            return;
        }

        if (Math.Sign(_position) == Math.Sign(signedQty))
        {
            var newPos = _position + signedQty;
            var weighted = (_avgPrice * Math.Abs(_position)) + (fillPrice * Math.Abs(signedQty));
            _avgPrice = weighted / Math.Abs(newPos);
            _position = newPos;
            return;
        }

        var closeQty = Math.Min(Math.Abs(_position), Math.Abs(signedQty));
        var remaining = Math.Abs(signedQty) - closeQty;

        var pnlPerUnit = _position > 0m ? (fillPrice - _avgPrice) : (_avgPrice - fillPrice);
        _realizedPnl += pnlPerUnit * closeQty;

        var newPosAfter = _position + signedQty;

        if (newPosAfter == 0m)
        {
            _position = 0m;
            _avgPrice = 0m;
            return;
        }

        if (remaining > 0m)
        {
            _position = newPosAfter;
            _avgPrice = fillPrice;
            return;
        }

        _position = newPosAfter;
    }
}
