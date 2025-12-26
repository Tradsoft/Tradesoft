using System.Collections.Generic;
using FluentAssertions;
using TradeSoft.Core.Backtesting;
using Xunit;

namespace TradeSoft.Tests.Backtesting;

public sealed class EquityBucketingTests
{
    [Fact]
    public void Bucketize_Last_ShouldPickLastSnapshotPerBucket()
    {
        var curve = new List<EquitySnapshot>
        {
            S(ts: 0,  nlv: 100m),
            S(ts: 1,  nlv: 101m),
            S(ts: 9,  nlv: 109m),
            S(ts: 10, nlv: 110m),
            S(ts: 11, nlv: 111m),
        };

        var bucket = new BucketSpec(Size: 10, Mode: BucketingMode.Last);

        var bucketed = EquityBucketer.Bucketize(curve, bucket);

        bucketed.Should().HaveCount(2);
        bucketed[0].Timestamp.Should().Be(9);
        bucketed[1].Timestamp.Should().Be(11);
    }

    [Fact]
    public void Bucketize_First_ShouldPickFirstSnapshotPerBucket()
    {
        var curve = new List<EquitySnapshot>
        {
            S(ts: 0,  nlv: 100m),
            S(ts: 1,  nlv: 101m),
            S(ts: 9,  nlv: 109m),
            S(ts: 10, nlv: 110m),
            S(ts: 11, nlv: 111m),
        };

        var bucket = new BucketSpec(Size: 10, Mode: BucketingMode.First);

        var bucketed = EquityBucketer.Bucketize(curve, bucket);

        bucketed.Should().HaveCount(2);
        bucketed[0].Timestamp.Should().Be(0);
        bucketed[1].Timestamp.Should().Be(10);
    }

    [Fact]
    public void ComputeSimpleReturns_ShouldReturnOneReturnPerBucketStep()
    {
        var bucketed = new List<EquitySnapshot>
        {
            S(ts: 9,  nlv: 100m),
            S(ts: 19, nlv: 110m),
            S(ts: 29, nlv: 99m),
        };

        var rets = ReturnSeries.ComputeSimpleReturns(bucketed);

        rets.Should().HaveCount(2);

        rets[0].Timestamp.Should().Be(19);
        rets[0].Return.Should().BeApproximately(0.10m, 0.0000001m);

        rets[1].Timestamp.Should().Be(29);
        rets[1].Return.Should().BeApproximately((99m / 110m) - 1m, 0.0000001m);
    }

    private static EquitySnapshot S(long ts, decimal nlv)
        => new EquitySnapshot(
            Timestamp: ts,
            Symbol: "EURUSD",

            Bid: 0m,
            Ask: 0m,
            MarkPrice: 0m,

            Cash: 0m,
            Position: 0m,
            AveragePrice: 0m,

            NetLiquidationValue: nlv,
            RealizedPnl: 0m,
            UnrealizedPnl: 0m
        );
}
