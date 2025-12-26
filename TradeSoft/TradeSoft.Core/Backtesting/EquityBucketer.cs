using System;
using System.Collections.Generic;

namespace TradeSoft.Core.Backtesting;

public static class EquityBucketer
{
    public static List<EquitySnapshot> Bucketize(
        IReadOnlyList<EquitySnapshot> curve,
        BucketSpec bucket)
    {
        if (curve is null) throw new ArgumentNullException(nameof(curve));
        if (!bucket.IsValid) throw new ArgumentOutOfRangeException(nameof(bucket), "Bucket size must be > 0.");
        if (curve.Count == 0) return new List<EquitySnapshot>(0);

        var outCurve = new List<EquitySnapshot>(capacity: Math.Max(16, curve.Count / 4));

        long currentBucketId = GetBucketId(curve[0].Timestamp, bucket.Size);
        EquitySnapshot candidate = curve[0];

        if (bucket.Mode == BucketingMode.First)
        {
            outCurve.Add(candidate);
        }

        for (int i = 1; i < curve.Count; i++)
        {
            var s = curve[i];
            var bid = GetBucketId(s.Timestamp, bucket.Size);

            if (bid == currentBucketId)
            {
                if (bucket.Mode == BucketingMode.Last)
                    candidate = s; 

                continue;
            }

            if (bucket.Mode == BucketingMode.Last)
                outCurve.Add(candidate);

            currentBucketId = bid;
            candidate = s;

            if (bucket.Mode == BucketingMode.First)
                outCurve.Add(candidate);
        }

        if (bucket.Mode == BucketingMode.Last)
            outCurve.Add(candidate);

        return outCurve;
    }

    private static long GetBucketId(long timestamp, long bucketSize)
        => timestamp / bucketSize;
}
