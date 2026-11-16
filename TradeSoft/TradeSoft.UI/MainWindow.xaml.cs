using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ScottPlot;
using ScottPlot.TickGenerators;
using TradeSoft.Core.DTO;
using TradeSoft.UI.ViewModels;
using TradeSoft.Core.Domain;

using Bar = TradeSoft.Core.Domain.Bar;
using Color = ScottPlot.Color;

namespace TradeSoft.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private List<Bar>? _currentBars;
    private BacktestResult? _lastResult;

    private ScottPlot.Plottables.Crosshair? _crosshair;
    private ScottPlot.Plottables.Crosshair? _equityCrosshair;

    private static readonly Color _chartBlue = Color.FromHex("#2962FF");

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = (MainViewModel)DataContext;

        _viewModel.BacktestCompleted += OnBacktestCompleted;
        _viewModel.MarketDataLoaded += OnMarketDataLoaded;

        SetupChartStyle();
        SetupEquityChartStyle();
        SetupCrosshair();

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.AvailableIndicators))
            {
                if (_currentBars != null)
                    Dispatcher.Invoke(() => UpdateMainChart(_currentBars));
            }
        };
    }

    private void OnMarketDataLoaded(object? sender, List<Bar> bars)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateMainChart(bars);
            EquityPlot.Plot.Clear();
            EquityPlot.Refresh();
            TxtCrosshairInfo.Text = "";
            ClearStatistics();
        });
    }

    private void OnBacktestCompleted(object? sender, BacktestResult result)
    {
        if (result.Bars == null || result.Bars.Count == 0) return;

        _lastResult = result;

        Dispatcher.Invoke(() =>
        {
            UpdateMainChart(result.Bars);

            if (result.Trades != null && result.Trades.Count > 0)
            {
                double avgCandleSize = result.Bars.Average(b => b.High - b.Low);
                double offset = avgCandleSize * 2.0;

                var buyX = new List<double>();
                var buyY = new List<double>();
                var sellX = new List<double>();
                var sellY = new List<double>();

                foreach (var trade in result.Trades)
                {
                    var closestBar = result.Bars.Select((b, i) => new { Bar = b, Index = i })
                                                .Where(x => x.Bar.Timestamp <= trade.Time)
                                                .LastOrDefault();

                    if (closestBar != null)
                    {
                        double xIndex = closestBar.Index;
                        string side = trade.Side.ToUpper();

                        if (side == "BUY")
                        {
                            buyX.Add(xIndex);
                            buyY.Add(closestBar.Bar.Low - offset);
                        }
                        else if (side == "SELL")
                        {
                            sellX.Add(xIndex);
                            sellY.Add(closestBar.Bar.High + offset);
                        }
                    }
                }

                if (buyX.Any())
                {
                    var scat = MainPlot.Plot.Add.Scatter(buyX.ToArray(), buyY.ToArray());
                    scat.LineWidth = 0;
                    scat.MarkerStyle.Shape = MarkerShape.FilledTriangleUp;
                    scat.MarkerStyle.Size = 12;
                    scat.Color = Color.FromHex("#00E676");
                    scat.LegendText = "Buy";
                }

                if (sellX.Any())
                {
                    var scat = MainPlot.Plot.Add.Scatter(sellX.ToArray(), sellY.ToArray());
                    scat.LineWidth = 0;
                    scat.MarkerStyle.Shape = MarkerShape.FilledTriangleDown;
                    scat.MarkerStyle.Size = 12;
                    scat.Color = Color.FromHex("#FF1744");
                    scat.LegendText = "Sell";
                }

                MainPlot.Plot.ShowLegend();
                MainPlot.Plot.Legend.Alignment = Alignment.LowerLeft;
                MainPlot.Plot.Legend.BackgroundColor = Color.FromHex("#1E222D").WithAlpha(200);
                MainPlot.Plot.Legend.FontColor = Color.FromHex("#D1D4DC");
                MainPlot.Plot.Legend.OutlineColor = Color.FromHex("#2B2B2B");
            }

            SetupCrosshair();
            MainPlot.Refresh();

            DrawEquityCurve(result);
            DisplayDetailedStats(result);

            if (result.Logs != null)
            {
                _viewModel.LoadLogs(result.Logs);
            }
        });
    }

    private void DisplayDetailedStats(BacktestResult result)
    {
        var greenBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 230, 118));
        var redBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 23, 68));

        ValInitialCash.Text = $"{result.InitialCash:N2} $";
        ValFinalEquity.Text = $"{result.FinalEquity:N2} $";

        ValNetProfit.Text = $"{result.NetProfit:N2} $";
        ValNetProfit.Foreground = result.NetProfit >= 0 ? greenBrush : redBrush;

        ValReturn.Text = $"{result.ReturnPercent:F2} %";
        ValReturn.Foreground = result.ReturnPercent >= 0 ? greenBrush : redBrush;

        ValMaxDD.Text = $"{result.MaxDrawdownPercent:F2} %";
        ValMaxDD.Foreground = redBrush;

        ValTotalTrades.Text = $"{result.TotalTrades}";
        ValWinRate.Text = $"{result.WinRate:F2} %";
        ValProfitFactor.Text = $"{result.ProfitFactor:F2}";
    }

    private void ClearStatistics()
    {
        ValInitialCash.Text = "-";
        ValFinalEquity.Text = "-";
        ValNetProfit.Text = "-";
        ValReturn.Text = "-";
        ValMaxDD.Text = "-";
        ValTotalTrades.Text = "-";
        ValWinRate.Text = "-";
        ValProfitFactor.Text = "-";
    }

    // Modifiez la méthode existante UpdateMainChart
    private void UpdateMainChart(List<Bar> bars)
    {
        _currentBars = bars;
        MainPlot.Plot.Clear();

        // 1. Dessin des Bougies (Code existant)
        var oldLimits = MainPlot.Plot.Axes.GetLimits();
        var candles = bars.Select((b, i) => new OHLC(b.Open, b.High, b.Low, b.Close, DateTime.FromOADate(i), TimeSpan.FromDays(1))).ToList();
        var series = MainPlot.Plot.Add.Candlestick(candles);
        series.RisingColor = Color.FromHex("#2962FF");
        series.FallingColor = Color.FromHex("#F23645");

        // 2. NOUVEAU : Dessin des Indicateurs
        DrawIndicators(bars);

        // 3. Configuration Axes (Code existant)
        DateTime[] dates = bars.Select(b => b.Timestamp).ToArray();
        MainPlot.Plot.Axes.Bottom.TickGenerator = new NumericAutomatic
        {
            LabelFormatter = (value) => {
                int idx = (int)Math.Round(value);
                if (idx >= 0 && idx < dates.Length) return dates[idx].ToString("dd/MM\nHH:mm");
                return "";
            }
        };

        if (_viewModel.ShouldResetZoom) MainPlot.Plot.Axes.AutoScale();
        else MainPlot.Plot.Axes.SetLimits(oldLimits);

        MainPlot.Refresh();
    }

    // --- NOUVELLES MÉTHODES DE DESSIN ---

    private void DrawIndicators(List<Bar> bars)
    {
        foreach (var ind in _viewModel.AvailableIndicators.Where(i => i.IsSelected))
        {
            switch (ind.Type)
            {
                case "SMA": DrawSMA(bars, ind.Period); break;
                case "EMA": DrawEMA(bars, ind.Period); break;
                case "Bands": DrawBollinger(bars, ind.Period, 2.0); break;
                case "Breakout": DrawDonchian(bars, ind.Period); break;
                case "RSI_Color": DrawRSIHighlights(bars, ind.Period); break; 
                case "PSAR": DrawParabolicSAR(bars); break; 
            }
        }
    }

    private void DrawEMA(List<Bar> bars, int period)
    {
        if (bars.Count < period || period <= 0) return;

        double[] ema = new double[bars.Count];
        double[] xs = new double[bars.Count];
        double k = 2.0 / (period + 1);

        double sum = 0;
        for (int i = 0; i < period; i++) sum += bars[i].Close;
        double prevEma = sum / period;

        for (int i = 0; i < bars.Count; i++)
        {
            xs[i] = i;
            if (i < period - 1)
            {
                ema[i] = double.NaN;
            }
            else if (i == period - 1)
            {
                ema[i] = prevEma;
            }
            else
            {
                ema[i] = (bars[i].Close - prevEma) * k + prevEma;
                prevEma = ema[i];
            }
        }

        var line = MainPlot.Plot.Add.ScatterLine(xs, ema);
        line.Color = Color.FromHex("#FF9800");
        line.LineWidth = 2;
    }

    private void DrawSMA(List<Bar> bars, int period)
    {
        double[] sma = new double[bars.Count];
        double[] xs = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            xs[i] = i;
            if (i < period - 1)
            {
                sma[i] = double.NaN; 
            }
            else
            {
                double sum = 0;
                for (int j = 0; j < period; j++) sum += bars[i - j].Close;
                sma[i] = sum / period;
            }
        }

        var line = MainPlot.Plot.Add.ScatterLine(xs, sma);
        line.Color = period > 30 ? Color.FromHex("#FFA726") : Color.FromHex("#29B6F6"); 
        line.LineWidth = 2;
    }

    private void DrawRSIHighlights(List<Bar> bars, int period)
    {
        if (bars.Count < period) return;

        double[] rsi = CalculateRSI(bars, period);

        var overboughtX = new List<double>();
        var overboughtY = new List<double>();
        var oversoldX = new List<double>();
        var oversoldY = new List<double>();

        for (int i = 0; i < bars.Count; i++)
        {
            if (rsi[i] >= 70) 
            {
                overboughtX.Add(i);
                overboughtY.Add(bars[i].High * 1.0005); 
            }
            else if (rsi[i] <= 30) 
            {
                oversoldX.Add(i);
                oversoldY.Add(bars[i].Low * 0.9995);
            }
        }

        if (overboughtX.Count > 0)
        {
            var sc = MainPlot.Plot.Add.Scatter(overboughtX.ToArray(), overboughtY.ToArray());
            sc.MarkerStyle.Shape = MarkerShape.FilledCircle;
            sc.MarkerStyle.Size = 5;
            sc.Color = Color.FromHex("#FF1744"); 
            sc.LineWidth = 0;
        }

        if (oversoldX.Count > 0)
        {
            var sc = MainPlot.Plot.Add.Scatter(oversoldX.ToArray(), oversoldY.ToArray());
            sc.MarkerStyle.Shape = MarkerShape.FilledCircle;
            sc.MarkerStyle.Size = 5;
            sc.Color = Color.FromHex("#00E676");
            sc.LineWidth = 0;
        }
    }

    private double[] CalculateRSI(List<Bar> bars, int period)
    {
        double[] rsi = new double[bars.Count];
        Array.Fill(rsi, double.NaN);
        double avgGain = 0;
        double avgLoss = 0;

        for (int i = 1; i < bars.Count; i++)
        {
            double change = bars[i].Close - bars[i - 1].Close;
            double gain = change > 0 ? change : 0;
            double loss = change < 0 ? -change : 0;

            if (i <= period)
            {
                avgGain += gain;
                avgLoss += loss;
                if (i == period)
                {
                    avgGain /= period;
                    avgLoss /= period;
                }
            }
            else
            {
                avgGain = (avgGain * (period - 1) + gain) / period;
                avgLoss = (avgLoss * (period - 1) + loss) / period;
            }

            if (i >= period)
            {
                if (avgGain == 0.0 && avgLoss == 0.0)
                {
                    rsi[i] = 50.0;
                }
                else if (avgLoss == 0.0)
                {
                    rsi[i] = 100.0;
                }
                else
                {
                    double rs = avgGain / avgLoss;
                    rsi[i] = 100.0 - (100.0 / (1.0 + rs));
                }
            }
        }
        return rsi;
    }

    private void DrawParabolicSAR(List<Bar> bars)
    {
        if (bars.Count < 2) return;

        double af = 0.02;
        double maxAf = 0.2;
        double[] sar = new double[bars.Count];
        double[] xs = new double[bars.Count];

        bool isBullish = true;
        sar[0] = bars[0].Low;
        double ep = bars[0].High; 
        double currentAf = af;

        for (int i = 1; i < bars.Count; i++)
        {
            xs[i] = i;
            double prevSar = sar[i - 1];

            sar[i] = prevSar + currentAf * (ep - prevSar);

            if (isBullish)
            {
                if (bars[i].Low < sar[i]) 
                {
                    isBullish = false;
                    sar[i] = ep;
                    ep = bars[i].Low;
                    currentAf = af;
                }
                else
                {
                    if (bars[i].High > ep)
                    {
                        ep = bars[i].High;
                        currentAf = Math.Min(currentAf + af, maxAf);
                    }
                    sar[i] = Math.Min(sar[i], Math.Min(bars[i - 1].Low, bars[Math.Max(0, i - 2)].Low));
                }
            }
            else
            {
                if (bars[i].High > sar[i]) 
                {
                    isBullish = true;
                    sar[i] = ep;
                    ep = bars[i].High;
                    currentAf = af;
                }
                else
                {
                    if (bars[i].Low < ep)
                    {
                        ep = bars[i].Low;
                        currentAf = Math.Min(currentAf + af, maxAf);
                    }
                    sar[i] = Math.Max(sar[i], Math.Max(bars[i - 1].High, bars[Math.Max(0, i - 2)].High));
                }
            }
        }

        var scatter = MainPlot.Plot.Add.Scatter(xs, sar);
        scatter.MarkerStyle.Shape = MarkerShape.FilledCircle;
        scatter.MarkerStyle.Size = 2; 
        scatter.Color = Color.FromHex("#787B86"); 
        scatter.LineWidth = 0;
    }

    private void DrawBollinger(List<Bar> bars, int period, double k)
    {
        double[] upper = new double[bars.Count];
        double[] lower = new double[bars.Count];
        double[] xs = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            xs[i] = i;
            if (i < period - 1)
            {
                upper[i] = double.NaN; lower[i] = double.NaN;
            }
            else
            {
                double sum = 0;
                for (int j = 0; j < period; j++) sum += bars[i - j].Close;
                double mean = sum / period;

                double sumSq = 0;
                for (int j = 0; j < period; j++)
                {
                    double val = bars[i - j].Close;
                    sumSq += (val - mean) * (val - mean);
                }
                double stdDev = Math.Sqrt(sumSq / period);

                upper[i] = mean + k * stdDev;
                lower[i] = mean - k * stdDev;
            }
        }

        var line1 = MainPlot.Plot.Add.ScatterLine(xs, upper);
        line1.Color = Color.FromHex("#AB47BC"); 
        line1.LinePattern = LinePattern.Dashed;

        var line2 = MainPlot.Plot.Add.ScatterLine(xs, lower);
        line2.Color = Color.FromHex("#AB47BC");
        line2.LinePattern = LinePattern.Dashed;
    }

    private void DrawDonchian(List<Bar> bars, int period)
    {
        double[] highLine = new double[bars.Count];
        double[] lowLine = new double[bars.Count];
        double[] xs = new double[bars.Count];

        for (int i = 0; i < bars.Count; i++)
        {
            xs[i] = i;
            if (i < period)
            {
                highLine[i] = double.NaN; lowLine[i] = double.NaN;
            }
            else
            {
                double max = double.MinValue;
                double min = double.MaxValue;
                for (int j = 1; j <= period; j++)
                {
                    if (bars[i - j].High > max) max = bars[i - j].High;
                    if (bars[i - j].Low < min) min = bars[i - j].Low;
                }
                highLine[i] = max;
                lowLine[i] = min;
            }
        }

        var l1 = MainPlot.Plot.Add.ScatterLine(xs, highLine);
        l1.Color = Color.FromHex("#66BB6A"); 

        var l2 = MainPlot.Plot.Add.ScatterLine(xs, lowLine);
        l2.Color = Color.FromHex("#EF5350");
    }

    private void SetupEquityChartStyle()
    {
        EquityPlot.Plot.FigureBackground.Color = ScottPlot.Colors.White;
        EquityPlot.Plot.DataBackground.Color = ScottPlot.Colors.White;

        EquityPlot.Plot.Grid.MajorLineColor = Color.FromHex("#E1E1E1");
        EquityPlot.Plot.Grid.MinorLineColor = Color.FromHex("#F5F5F5");

        EquityPlot.Plot.Axes.Color(ScottPlot.Colors.Black);

        var bottom = EquityPlot.Plot.Axes.Bottom;
        var left = EquityPlot.Plot.Axes.Left;

        bottom.TickLabelStyle.ForeColor = ScottPlot.Colors.Black;
        bottom.TickLabelStyle.FontSize = 11;

        left.TickLabelStyle.ForeColor = ScottPlot.Colors.Black;
        left.TickLabelStyle.FontSize = 11;

        EquityPlot.UserInputProcessor.Disable();
    }

    private void DrawEquityCurve(BacktestResult result)
    {
        EquityPlot.Plot.Clear();

        var data = result.EquityCurve
                         .Where(p => p.Time.Year > 2000 && p.Value > 0)
                         .OrderBy(p => p.Time)
                         .ToList();

        if (data.Count == 0) return;

        double threshold = result.InitialCash;

        var xs = new List<double>();
        var ys = new List<double>();

        bool isAbove = data[0].Value >= threshold;
        Color currentColor = isAbove ? _chartBlue : Color.FromHex("#FF1744"); 

        xs.Add(data[0].Time.ToOADate());
        ys.Add(data[0].Value);

        for (int i = 1; i < data.Count; i++)
        {
            double x1 = data[i - 1].Time.ToOADate();
            double y1 = data[i - 1].Value;
            double x2 = data[i].Time.ToOADate();
            double y2 = data[i].Value;

            if ((y1 >= threshold && y2 < threshold) || (y1 < threshold && y2 >= threshold))
            {
                double intersectX = x1 + (threshold - y1) * (x2 - x1) / (y2 - y1);
                double intersectY = threshold;

                xs.Add(intersectX);
                ys.Add(intersectY);
                AddSegment(xs, ys, currentColor); 

                xs.Clear(); ys.Clear();
                xs.Add(intersectX);
                ys.Add(intersectY);

                isAbove = y2 >= threshold;
                currentColor = isAbove ? _chartBlue : Color.FromHex("#FF1744");
            }

            xs.Add(x2);
            ys.Add(y2);
        }

        AddSegment(xs, ys, currentColor);

        var hLine = EquityPlot.Plot.Add.HorizontalLine(threshold);
        hLine.Color = Color.FromHex("#000000").WithAlpha(0.5);
        hLine.LinePattern = LinePattern.Dashed;
        hLine.LineWidth = 1;

        EquityPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
        EquityPlot.Plot.Axes.AutoScale();

        double minVal = data.Min(p => p.Value);
        double maxVal = data.Max(p => p.Value);
        double yLow = Math.Min(minVal, threshold) * 0.999;
        double yHigh = Math.Max(maxVal, threshold) * 1.001;

        EquityPlot.Plot.Axes.SetLimitsY(yLow, yHigh);

        SetupEquityCrosshair();
        EquityPlot.Refresh();
    }

    private void AddSegment(List<double> xs, List<double> ys, Color color)
    {
        var line = EquityPlot.Plot.Add.ScatterLine(xs.ToArray(), ys.ToArray());
        line.Color = color;
        line.LineWidth = 2;
        line.MarkerSize = 0; 
    }

    private void SetupEquityCrosshair()
    {
        if (_equityCrosshair != null) EquityPlot.Plot.Remove(_equityCrosshair);

        _equityCrosshair = EquityPlot.Plot.Add.Crosshair(0, 0);
        _equityCrosshair.IsVisible = false;

        var darkGrey = Color.FromHex("#555555");
        _equityCrosshair.LineColor = darkGrey;
        _equityCrosshair.LinePattern = LinePattern.Dashed;

        EquityPlot.MouseMove -= EquityPlot_MouseMove;
        EquityPlot.MouseLeave -= EquityPlot_MouseLeave;

        EquityPlot.MouseMove += EquityPlot_MouseMove;
        EquityPlot.MouseLeave += EquityPlot_MouseLeave;
    }

    private void EquityPlot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_equityCrosshair == null || _lastResult == null || _lastResult.EquityCurve.Count == 0) return;

        Point mousePoint = e.GetPosition(EquityPlot);
        var source = PresentationSource.FromVisual(EquityPlot);
        if (source?.CompositionTarget == null) return;

        double dpiX = source.CompositionTarget.TransformToDevice.M11;
        double dpiY = source.CompositionTarget.TransformToDevice.M22;

        var coords = EquityPlot.Plot.GetCoordinates((float)(mousePoint.X * dpiX), (float)(mousePoint.Y * dpiY));

        _equityCrosshair.X = coords.X;
        _equityCrosshair.Y = coords.Y;
        _equityCrosshair.IsVisible = true;

        DateTime mouseDate = DateTime.FromOADate(coords.X);

        var closestPoint = _lastResult.EquityCurve
            .Where(p => p.Time != DateTime.MinValue) 
            .OrderBy(p => Math.Abs((p.Time - mouseDate).Ticks)) 
            .FirstOrDefault();

        if (closestPoint.Time != DateTime.MinValue)
        {
            double equity = closestPoint.Value;
            double initialCash = _lastResult.InitialCash; 

            TxtCrosshairInfo.Text = $"📅 {closestPoint.Time:dd/MM HH:mm}   💰 {equity:N2} $";

            if (equity >= initialCash)
            {
                TxtCrosshairInfo.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(41, 98, 255));
            }
            else
            {
                TxtCrosshairInfo.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 57, 53));
            }
        }

        EquityPlot.Refresh();
    }

    private void EquityPlot_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_equityCrosshair != null)
        {
            _equityCrosshair.IsVisible = false;
            TxtCrosshairInfo.Text = "";
            EquityPlot.Refresh();
        }
    }

    private void SetupChartStyle()
    {
        MainPlot.Plot.FigureBackground.Color = Color.FromHex("#131722");
        MainPlot.Plot.DataBackground.Color = Color.FromHex("#131722");
        MainPlot.Plot.Grid.MajorLineColor = Color.FromHex("#1E222D");
        MainPlot.Plot.Axes.Color(Color.FromHex("#787B86"));
        MainPlot.Plot.Layout.Frameless();
    }

    private void SetupCrosshair()
    {
        if (_crosshair != null) MainPlot.Plot.Remove(_crosshair);

        _crosshair = MainPlot.Plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;

        var grey = Color.FromHex("#787B86");

        _crosshair.VerticalLine.LineStyle.Color = grey;
        _crosshair.VerticalLine.LineStyle.Pattern = LinePattern.Dashed;
        _crosshair.VerticalLine.LabelStyle.IsVisible = false;

        _crosshair.HorizontalLine.LineStyle.Color = grey;
        _crosshair.HorizontalLine.LineStyle.Pattern = LinePattern.Dashed;
        _crosshair.HorizontalLine.LabelStyle.IsVisible = false;

        MainPlot.MouseMove -= MainPlot_MouseMove;
        MainPlot.MouseMove += MainPlot_MouseMove;
        MainPlot.MouseLeave -= MainPlot_MouseLeave;
        MainPlot.MouseLeave += MainPlot_MouseLeave;
    }

    private void MainPlot_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_crosshair != null)
        {
            _crosshair.IsVisible = false;
            TxtOHLC.Text = "";
            MainPlot.Refresh();
        }
    }

    private void MainPlot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_crosshair == null || _currentBars == null || _currentBars.Count == 0) return;

        Point mousePoint = e.GetPosition(MainPlot);
        var source = PresentationSource.FromVisual(MainPlot);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        var coords = MainPlot.Plot.GetCoordinates((float)(mousePoint.X * dpiX), (float)(mousePoint.Y * dpiY));

        _crosshair.X = coords.X;
        _crosshair.Y = coords.Y;
        _crosshair.IsVisible = true;

        int idx = (int)Math.Round(coords.X);
        if (idx >= 0 && idx < _currentBars.Count)
        {
            var bar = _currentBars[idx];
            UpdateOHLCDisplay(bar);
        }
        else
        {
            TxtOHLC.Text = "";
        }
        MainPlot.Refresh();
    }

    private void UpdateOHLCDisplay(Bar bar)
    {
        double change = bar.Close - bar.Open;
        double percent = (change / bar.Open) * 100;
        string sign = percent >= 0 ? "+" : "";

        string text = $"📅 {bar.Timestamp:dd/MM HH:mm}   " +
                      $"O: {bar.Open:F5}   " +
                      $"H: {bar.High:F5}   " +
                      $"L: {bar.Low:F5}   " +
                      $"C: {bar.Close:F5}   " +
                      $"({sign}{percent:F2}%)";

        TxtOHLC.Text = text;

        if (change >= 0)
            TxtOHLC.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 166, 154));
        else
            TxtOHLC.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 83, 80));
    }

    private void CbxIndicators_DropDownClosed(object sender, System.EventArgs e)
    {
    }
}