using System;

namespace TradeSoft.Core.Trading;

public sealed class BrokerFees
{
    private BrokerFees(decimal commissionPerTrade, decimal bpsOnNotional)
    {
        CommissionPerTrade = commissionPerTrade;
        BpsOnNotional = bpsOnNotional;
    }

    public decimal CommissionPerTrade { get; }
    public decimal BpsOnNotional { get; }

    public decimal ComputeFee(decimal qty, decimal price)
    {
        var notional = Math.Abs(qty) * price;
        return CommissionPerTrade + (notional * BpsOnNotional);
    }

    public static BrokerFees None => new(0m, 0m);

    public static BrokerFees PerTrade(decimal commission) => new(commission, 0m);

    public static BrokerFees Bps(decimal bps)
    {
        var rate = bps / 10000m;
        return new(0m, rate);
    }
}
