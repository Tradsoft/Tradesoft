using System;

namespace TradeSoft.Core.Trading;

public sealed class InstrumentSpec
{
    public InstrumentSpec(string symbol, int priceScalePow10)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (priceScalePow10 < 0 || priceScalePow10 > 9) throw new ArgumentOutOfRangeException(nameof(priceScalePow10));

        Symbol = symbol;
        PriceScalePow10 = priceScalePow10;
    }

    public string Symbol { get; }
    public int PriceScalePow10 { get; }

    public decimal RoundPrice(decimal px)
    {
        decimal scale = 1m;
        for (int i = 0; i < PriceScalePow10; i++) scale *= 10m;
        return decimal.Round(px * scale, 0, MidpointRounding.AwayFromZero) / scale;
    }

    public static InstrumentSpec EurUsd() => new("EURUSD", 5);
}
