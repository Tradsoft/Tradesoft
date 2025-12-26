using System;
using System.Collections.Generic;

namespace TradeSoft.Core.Backtesting;

public readonly record struct BucketReturn(long Timestamp, decimal Return);

public static class ReturnSeries
{
    public static List<BucketReturn> ComputeSimpleReturns(IReadOnlyList<EquitySnapshot> bucketed)
    {
        var res = new List<BucketReturn>(Math.Max(0, bucketed.Count - 1));
        if (bucketed.Count < 2) return res;

        for (int i = 1; i < bucketed.Count; i++)
        {
            var prev = bucketed[i - 1].NetLiquidationValue;
            var cur = bucketed[i].NetLiquidationValue;

            if (prev <= 0m) continue; 

            var r = (cur / prev) - 1m;
            res.Add(new BucketReturn(bucketed[i].Timestamp, r));
        }

        return res;
    }
}

