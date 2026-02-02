using System;
using System.Collections.Generic;
using System.Linq;

public static class BacktestTest4
{
    // 1️⃣ Rendement total
    public static double CalculateReturn(List<double> portfolioValues)
    {
        double initialValue = portfolioValues.First();
        double finalValue = portfolioValues.Last();

        return (finalValue - initialValue) / initialValue;
    }

    // 2️⃣ Drawdown maximal
    public static double CalculateMaxDrawdown(List<double> portfolioValues)
    {
        double peak = portfolioValues[0];
        double maxDrawdown = 0.0;

        foreach (double value in portfolioValues)
        {
            if (value > peak)
                peak = value;

            double drawdown = (peak - value) / peak;

            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;
        }

        return maxDrawdown;
    }

    // 3️⃣ Sharpe Ratio
    public static double CalculateSharpeRatio(List<double> portfolioValues)
    {
        List<double> returns = new List<double>();

        for (int i = 1; i < portfolioValues.Count; i++)
        {
            double dailyReturn =
                (portfolioValues[i] - portfolioValues[i - 1]) / portfolioValues[i - 1];
            returns.Add(dailyReturn);
        }

        double averageReturn = returns.Average();
        double stdDev = Math.Sqrt(returns.Average(r => Math.Pow(r - averageReturn, 2)));

        if (stdDev == 0)
            return 0;

        return averageReturn / stdDev;
    }
}
