using System;
using System.Collections.Generic;
using TradeSoft.Core.Events;
using TradeSoft.Core.MarketData;
using TradeSoft.Core.Trading;
using TradeSoft.Engine.Simulation.Models;

namespace TradeSoft.Engine.Simulation;

public sealed class MatchingEngine : IMatchingEngine
{
    private readonly InstrumentSpec _spec;
    private readonly ILatencyModel _latency;
    private readonly ISlippageModel _slippage;

    // Orders en vol (latence) -> triés par dueTimestamp
    private readonly PriorityQueue<OrderState, long> _inFlight = new();
    // Orders actifs (arrivés au marché)
    private readonly Queue<OrderState> _active = new();

    private TickData? _lastTick;

    public MatchingEngine(InstrumentSpec spec, ILatencyModel latency, ISlippageModel slippage)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        _latency = latency ?? throw new ArgumentNullException(nameof(latency));
        _slippage = slippage ?? throw new ArgumentNullException(nameof(slippage));
    }

    public void Process(MarketEvent e)
    {
        if (e is null) throw new ArgumentNullException(nameof(e));
        if (e.IsRejected) return;

        // 1) Tick context
        if (e.Tick is TickData tick)
            _lastTick = tick;

        // 2) Tick => activer + drainer (multi-reports)
        if (e.Tick is TickData tick2)
        {
            ActivateDueOrders(tick2.Timestamp);
            DrainManyOnTick(e, tick2);
        }

        // 3) Enqueue ordre APRES le drain (anti-lookahead : ordre du tick t ne s'exécute pas sur le même tick)
        if (e.Order is not null && e.ReportCount == 0 && !e.IsRejected)
        {
            var ctx = e.Tick ?? _lastTick;
            if (ctx is not null)
                EnqueueOrder(e.Order, ctx.Value);
        }
    }

    private void EnqueueOrder(OrderCommand cmd, in TickData ctxTick)
    {
        var due = ctxTick.Timestamp + _latency.GetLatencyTicks(in cmd, in ctxTick);

        var st = new OrderState(
            CommandId: cmd.CommandId,
            Symbol: cmd.Symbol,
            Side: cmd.Side,
            Type: cmd.Type,
            Tif: cmd.TimeInForce,
            RemainingQty: cmd.Quantity,
            LimitPrice: cmd.LimitPrice,
            DueTimestamp: due);

        _inFlight.Enqueue(st, due);
    }

    private void ActivateDueOrders(long nowTs)
    {
        while (_inFlight.TryPeek(out var next, out var due) && due <= nowTs)
        {
            _inFlight.Dequeue();
            _active.Enqueue(next);
        }
    }

    /// <summary>
    /// v4.1: drain plusieurs ordres sur un même tick-event tant que :
    /// - il reste du budget volume
    /// - il reste des ordres actifs
    /// - MarketEvent a encore de la place pour des reports
    ///
    /// Anti-piège : on scanne au max 1 passe de la queue active sur ce tick (sinon boucle infinie si tout est non marketable).
    /// </summary>
    private void DrainManyOnTick(MarketEvent e, in TickData tick)
    {
        if (_active.Count == 0) return;

        var budget = _slippage.GetMaxFillQuantity(in tick);
        if (budget <= 0m) return;

        // Une passe max sur les ordres actifs présents au début du tick
        var scanLimit = _active.Count;

        for (int scanned = 0; scanned < scanLimit; scanned++)
        {
            if (budget <= 0m) break;
            if (!e.HasCapacityForReport) break; // MarketEvent.MaxReports atteint

            var order = _active.Dequeue();

            // LIMIT non marketable => on requeue et on continue (ne pas bloquer les autres ordres)
            if (order.Type == OrderType.Limit && order.LimitPrice is decimal lim)
            {
                var basePxLim = order.Side == OrderSide.Buy ? (decimal)tick.Ask : (decimal)tick.Bid;
                var marketable =
                    (order.Side == OrderSide.Buy && lim >= basePxLim) ||
                    (order.Side == OrderSide.Sell && lim <= basePxLim);

                if (!marketable)
                {
                    _active.Enqueue(order);
                    continue;
                }
            }

            var fillQty = Math.Min(order.RemainingQty, budget);
            if (fillQty <= 0m)
            {
                _active.Enqueue(order);
                continue;
            }

            var basePrice = order.Side == OrderSide.Buy ? (decimal)tick.Ask : (decimal)tick.Bid;

            var slipped = _slippage.Apply(
                symbol: order.Symbol,
                side: order.Side,
                requestedQty: order.RemainingQty,
                fillQty: fillQty,
                tick: in tick,
                basePrice: basePrice);

            var px = _spec.RoundPrice(slipped);

            var newRemaining = order.RemainingQty - fillQty;
            var status = newRemaining == 0m ? ExecutionStatus.Filled : ExecutionStatus.PartiallyFilled;

            e.AddReport(new ExecutionReport(
                commandId: order.CommandId,
                symbol: order.Symbol,
                side: order.Side,
                status: status,
                filledQuantity: fillQty,
                averagePrice: px,
                message: status == ExecutionStatus.Filled ? "V4 fill" : "V4 partial fill"));

            budget -= fillQty;

            if (newRemaining > 0m)
                _active.Enqueue(order with { RemainingQty = newRemaining });
        }
    }

    private readonly record struct OrderState(
        Guid CommandId,
        string Symbol,
        OrderSide Side,
        OrderType Type,
        OrderTimeInForce Tif,
        decimal RemainingQty,
        decimal? LimitPrice,
        long DueTimestamp);
}
