using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ScottPlot;
using ScottPlot.TickGenerators;
using TradeSoft.Core.DTO;
using TradeSoft.UI.ViewModels;

using Bar = TradeSoft.Core.Domain.Bar;
using Color = ScottPlot.Color;

namespace TradeSoft.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private List<Bar>? _currentBars;
    private ScottPlot.Plottables.Crosshair? _crosshair;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = (MainViewModel)DataContext;
        _viewModel.BacktestCompleted += OnBacktestCompleted;

        SetupChartStyle();
        SetupEquityChartStyle();
        SetupCrosshair();
    }

    private void SetupChartStyle()
    {
        MainPlot.Plot.FigureBackground.Color = Color.FromHex("#131722");
        MainPlot.Plot.DataBackground.Color = Color.FromHex("#131722");
        MainPlot.Plot.Grid.MajorLineColor = Color.FromHex("#1E222D");
        MainPlot.Plot.Axes.Color(Color.FromHex("#787B86"));
        MainPlot.Plot.Layout.Frameless();
    }

    private void SetupEquityChartStyle()
    {
        EquityPlot.Plot.FigureBackground.Color = Color.FromHex("#131722");
        EquityPlot.Plot.DataBackground.Color = Color.FromHex("#131722");
        EquityPlot.Plot.Grid.MajorLineColor = Color.FromHex("#1E222D");
        EquityPlot.Plot.Axes.Color(Color.FromHex("#787B86"));
        EquityPlot.Plot.Layout.Frameless();
    }

    private void SetupCrosshair()
    {
        _crosshair = MainPlot.Plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;

        _crosshair.LineColor = Color.FromHex("#787B86");
        _crosshair.LinePattern = LinePattern.Dashed;
        _crosshair.LineWidth = 1;

        _crosshair.HorizontalLine.IsVisible = false;
        _crosshair.VerticalLine.IsVisible = false;

        MainPlot.MouseMove += MainPlot_MouseMove;
        MainPlot.MouseEnter += (s, e) => { if (_crosshair != null) _crosshair.IsVisible = true; };
        MainPlot.MouseLeave += (s, e) => { if (_crosshair != null) _crosshair.IsVisible = false; MainPlot.Refresh(); };
    }

    private void MainPlot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_crosshair == null || _currentBars == null || _currentBars.Count == 0) return;

        Point mousePoint = e.GetPosition(MainPlot);

        var source = PresentationSource.FromVisual(MainPlot);
        double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        float pixelX = (float)(mousePoint.X * dpiScaleX);
        float pixelY = (float)(mousePoint.Y * dpiScaleY);

        var coordinates = MainPlot.Plot.GetCoordinates(pixelX, pixelY);

        _crosshair.X = coordinates.X;
        _crosshair.Y = coordinates.Y;
        _crosshair.IsVisible = true;

        int index = (int)Math.Round(coordinates.X);

        if (index >= 0 && index < _currentBars.Count)
        {
            var bar = _currentBars[index];
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

        string text = $"Date: {bar.Timestamp:dd/MM HH:mm}   O: {bar.Open:F5}   H: {bar.High:F5}   L: {bar.Low:F5}   C: {bar.Close:F5}   ";
        TxtOHLC.Text = text + $"({(change >= 0 ? "+" : "")}{percent:F2}%)";
    }

    private void OnBacktestCompleted(object? sender, BacktestResult result)
    {
        if (result.Bars == null || result.Bars.Count == 0)
        {
            MainPlot.Plot.Clear();
            MainPlot.Refresh();
            EquityPlot.Plot.Clear();
            EquityPlot.Refresh();
            _currentBars = null;
            return;
        }

        _currentBars = result.Bars;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var oldLimits = MainPlot.Plot.Axes.GetLimits();
            var scottCandles = result.Bars.Select((b, i) => new OHLC(
                b.Open, b.High, b.Low, b.Close,
                DateTime.FromOADate(i),
                TimeSpan.FromDays(0.8)
            )).ToList();
            DateTime[] realDates = result.Bars.Select(b => b.Timestamp).ToArray();

            MainPlot.Plot.Clear();

            var series = MainPlot.Plot.Add.Candlestick(scottCandles);
            series.RisingColor = Color.FromHex("#2962FF");
            series.FallingColor = Color.FromHex("#F23645");

            // AFFICHAGE DES TRADES (TRIANGLES)
            if (result.Trades != null && result.Trades.Count > 0)
            {
                var buyTrades = new List<(double x, double y)>();
                var sellTrades = new List<(double x, double y)>();

                foreach (var trade in result.Trades)
                {
                    // Trouver l'index de la barre correspondante
                    int barIndex = -1;
                    for (int i = 0; i < result.Bars.Count; i++)
                    {
                        if (result.Bars[i].Timestamp >= trade.Time)
                        {
                            barIndex = i;
                            break;
                        }
                    }

                    if (barIndex >= 0 && barIndex < result.Bars.Count)
                    {
                        var bar = result.Bars[barIndex];
                        if (trade.Side.ToUpper() == "BUY")
                        {
                            // Triangle vert en bas de la bougie
                            buyTrades.Add((barIndex, bar.Low * 0.9995));
                        }
                        else if (trade.Side.ToUpper() == "SELL")
                        {
                            // Triangle rouge en haut de la bougie
                            sellTrades.Add((barIndex, bar.High * 1.0005));
                        }
                    }
                }

                // Afficher les triangles d'achat (vert, pointant vers le haut)
                if (buyTrades.Count > 0)
                {
                    var buyScatter = MainPlot.Plot.Add.Scatter(
                        buyTrades.Select(t => t.x).ToArray(),
                        buyTrades.Select(t => t.y).ToArray()
                    );
                    buyScatter.Color = Color.FromHex("#26A69A"); // Vert
                    buyScatter.MarkerSize = 12;
                    buyScatter.MarkerShape = MarkerShape.FilledTriangleUp;
                    buyScatter.LineWidth = 0;
                }

                // Afficher les triangles de vente (rouge, pointant vers le bas)
                if (sellTrades.Count > 0)
                {
                    var sellScatter = MainPlot.Plot.Add.Scatter(
                        sellTrades.Select(t => t.x).ToArray(),
                        sellTrades.Select(t => t.y).ToArray()
                    );
                    sellScatter.Color = Color.FromHex("#EF5350"); // Rouge
                    sellScatter.MarkerSize = 12;
                    sellScatter.MarkerShape = MarkerShape.FilledTriangleDown;
                    sellScatter.LineWidth = 0;
                }
            }

            _crosshair = MainPlot.Plot.Add.Crosshair(0, 0);

            var lineColor = Color.FromHex("#FFFFFF");

            _crosshair.VerticalLine.Color = lineColor;
            _crosshair.VerticalLine.LineStyle.Width = 1;
            _crosshair.VerticalLine.LineStyle.Pattern = LinePattern.Dashed;
            _crosshair.VerticalLine.LabelStyle.IsVisible = false;

            _crosshair.HorizontalLine.Color = lineColor;
            _crosshair.HorizontalLine.LineStyle.Width = 1;
            _crosshair.HorizontalLine.LineStyle.Pattern = LinePattern.Dashed;
            _crosshair.HorizontalLine.LabelStyle.IsVisible = false;

            _crosshair.IsVisible = false;

            MainPlot.Plot.Axes.Bottom.TickGenerator = new NumericAutomatic
            {
                LabelFormatter = (value) =>
                {
                    int index = (int)Math.Round(value);
                    if (index >= 0 && index < realDates.Length)
                        return realDates[index].ToString("dd/MM\nHH:mm");
                    return "";
                }
            };

            if (_viewModel.ShouldResetZoom)
                MainPlot.Plot.Axes.AutoScale();
            else
                MainPlot.Plot.Axes.SetLimits(oldLimits);

            MainPlot.Refresh();

            // === EQUITY CURVE ===
            EquityPlot.Plot.Clear();

            System.Diagnostics.Debug.WriteLine($"EquityCurve Count: {result.EquityCurve?.Count ?? 0}");
            
            if (result.EquityCurve != null && result.EquityCurve.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Drawing equity curve with {result.EquityCurve.Count} points");
                
                double[] xValues = Enumerable.Range(0, result.EquityCurve.Count).Select(i => (double)i).ToArray();
                double[] yValues = result.EquityCurve.Select(e => e.Value).ToArray();

                System.Diagnostics.Debug.WriteLine($"Equity range: {yValues.Min()} to {yValues.Max()}");

                var equityLine = EquityPlot.Plot.Add.Scatter(xValues, yValues);
                equityLine.Color = Color.FromHex("#2962FF");
                equityLine.LineWidth = 2;
                equityLine.MarkerSize = 0;

                EquityPlot.Plot.Axes.Bottom.TickGenerator = new NumericAutomatic
                {
                    LabelFormatter = (value) =>
                    {
                        int index = (int)Math.Round(value);
                        if (index >= 0 && index < result.EquityCurve.Count)
                            return result.EquityCurve[index].Time.ToString("dd/MM\nHH:mm");
                        return "";
                    }
                };

                EquityPlot.Plot.Axes.AutoScale();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No equity curve data to display");
            }

            EquityPlot.Refresh();
        });
    }
}