using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media; // Pour les couleurs WPF
using ScottPlot;
using ScottPlot.TickGenerators;
using TradeSoft.Core.DTO;
using TradeSoft.UI.ViewModels;
using TradeSoft.Core.Domain;

// Alias indispensables
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
        _viewModel.MarketDataLoaded += OnMarketDataLoaded;

        SetupChartStyle();
        SetupEquityChartStyle();
        SetupCrosshair();
    }

    private void OnMarketDataLoaded(object? sender, List<Bar> bars)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateMainChart(bars);
            EquityPlot.Plot.Clear();
            EquityPlot.Refresh();
        });
    }

    private void OnBacktestCompleted(object? sender, BacktestResult result)
    {
        if (result.Bars == null || result.Bars.Count == 0) return;

        Dispatcher.Invoke(() =>
        {
            // 1. Affiche les bougies
            UpdateMainChart(result.Bars);

            // 2. Affiche les trades (Marqueurs)
            if (result.Trades != null && result.Trades.Count > 0)
            {
                // Calcul pour décaler les flèches et ne pas toucher les bougies
                double avgHeight = result.Bars.Average(b => b.High - b.Low);
                double offset = avgHeight * 1.5; // Marge confortable

                var buyX = new List<double>();
                var buyY = new List<double>();
                var sellX = new List<double>();
                var sellY = new List<double>();

                foreach (var trade in result.Trades)
                {
                    // Trouver la bougie qui contient le temps du trade
                    var closestBar = result.Bars.Select((b, i) => new { Bar = b, Index = i })
                                                .Where(x => x.Bar.Timestamp <= trade.Time) // Prend la bougie qui a commencé avant le trade
                                                .LastOrDefault();

                    if (closestBar != null)
                    {
                        double xIndex = closestBar.Index;
                        string side = trade.Side.ToString().ToUpper(); // Force majuscule

                        if (side == "BUY")
                        {
                            buyX.Add(xIndex);
                            // ACHAT : Triangle VERT en BAS (pointant vers le haut)
                            // Position : Low - offset
                            buyY.Add(closestBar.Bar.Low - offset);
                        }
                        else if (side == "SELL")
                        {
                            sellX.Add(xIndex);
                            // VENTE : Triangle ROUGE en HAUT (pointant vers le bas)
                            // Position : High + offset
                            sellY.Add(closestBar.Bar.High + offset);
                        }
                    }
                }

                // Dessin des marqueurs Achat (Vert)
                if (buyX.Any())
                {
                    var scat = MainPlot.Plot.Add.Scatter(buyX.ToArray(), buyY.ToArray());
                    scat.LineWidth = 0;
                    scat.MarkerStyle.Shape = MarkerShape.FilledTriangleUp;
                    scat.MarkerStyle.Size = 12;
                    scat.Color = Color.FromHex("#00E676"); // Vert
                    scat.LegendText = "Buy"; // Pour la légende éventuelle
                }

                // Dessin des marqueurs Vente (Rouge)
                if (sellX.Any())
                {
                    var scat = MainPlot.Plot.Add.Scatter(sellX.ToArray(), sellY.ToArray());
                    scat.LineWidth = 0;
                    scat.MarkerStyle.Shape = MarkerShape.FilledTriangleDown;
                    scat.MarkerStyle.Size = 12;
                    scat.Color = Color.FromHex("#FF1744"); // Rouge
                    scat.LegendText = "Sell";
                }
            }

            // 3. Ré-ajoute le Crosshair et Update
            SetupCrosshair();
            MainPlot.Refresh();

            // 4. Affiche l'Equity
            DrawEquityCurve(result);
        });
    }

    private void UpdateMainChart(List<Bar> bars)
    {
        _currentBars = bars;
        MainPlot.Plot.Clear();

        var oldLimits = MainPlot.Plot.Axes.GetLimits();
        var candles = bars.Select((b, i) => new OHLC(b.Open, b.High, b.Low, b.Close, DateTime.FromOADate(i), TimeSpan.FromDays(1))).ToList();

        var series = MainPlot.Plot.Add.Candlestick(candles);
        series.RisingColor = Color.FromHex("#2962FF");
        series.FallingColor = Color.FromHex("#F23645");

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

        // Note: Le Refresh est fait par l'appelant pour éviter le clignotement
    }

    private void DrawEquityCurve(BacktestResult result)
    {
        EquityPlot.Plot.Clear();
        if (result.EquityCurve != null && result.EquityCurve.Count > 0)
        {
            var x = Enumerable.Range(0, result.EquityCurve.Count).Select(i => (double)i).ToArray();
            var y = result.EquityCurve.Select(e => e.Value).ToArray();

            var line = EquityPlot.Plot.Add.ScatterLine(x, y);
            line.Color = Color.FromHex("#2962FF");
            line.LineWidth = 2;
            line.MarkerSize = 0; // Pas de points, juste la ligne

            EquityPlot.Plot.Axes.Bottom.TickGenerator = new NumericAutomatic
            {
                LabelFormatter = (value) => {
                    int idx = (int)Math.Round(value);
                    if (idx >= 0 && idx < result.EquityCurve.Count) return result.EquityCurve[idx].Time.ToString("dd/MM");
                    return "";
                }
            };
            EquityPlot.Plot.Axes.AutoScale();
        }
        EquityPlot.Refresh();
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
        if (_crosshair != null)
        {
            MainPlot.Plot.Remove(_crosshair);
        }

        _crosshair = MainPlot.Plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;

        var grey = Color.FromHex("#787B86");

        // Configuration compatible ScottPlot 5
        _crosshair.VerticalLine.LineStyle.Color = grey;
        _crosshair.VerticalLine.LineStyle.Pattern = LinePattern.Dashed;
        _crosshair.VerticalLine.LabelStyle.IsVisible = false;

        _crosshair.HorizontalLine.LineStyle.Color = grey;
        _crosshair.HorizontalLine.LineStyle.Pattern = LinePattern.Dashed;
        _crosshair.HorizontalLine.LabelStyle.IsVisible = false;

        // Réinitialisation des événements souris
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

        // Format: Date + Prix (5 décimales) + %
        string text = $"📅 {bar.Timestamp:dd/MM HH:mm}   " +
                      $"O: {bar.Open:F5}   " +
                      $"H: {bar.High:F5}   " +
                      $"L: {bar.Low:F5}   " +
                      $"C: {bar.Close:F5}   " +
                      $"({sign}{percent:F2}%)";

        TxtOHLC.Text = text;

        if (change >= 0)
            TxtOHLC.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 166, 154));
        else
            TxtOHLC.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 83, 80));
    }
}