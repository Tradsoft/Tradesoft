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

// Alias indispensables
using Bar = TradeSoft.Core.Domain.Bar;
using Color = ScottPlot.Color;

namespace TradeSoft.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Stockage local pour l'interaction souris
    private List<Bar>? _currentBars;
    private ScottPlot.Plottables.Crosshair? _crosshair;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = (MainViewModel)DataContext;
        _viewModel.BacktestCompleted += OnBacktestCompleted;

        SetupChartStyle();
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

        // 1. Récupérer la position de la souris WPF (Pixels logiques)
        Point mousePoint = e.GetPosition(MainPlot);

        // --- ✅ CORRECTION DU DÉCALAGE (DPI SCALING) ---
        // On récupère le facteur de zoom de l'écran (ex: 1.25 pour 125%)
        var source = PresentationSource.FromVisual(MainPlot);
        double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        // On convertit les pixels logiques en pixels physiques pour ScottPlot
        float pixelX = (float)(mousePoint.X * dpiScaleX);
        float pixelY = (float)(mousePoint.Y * dpiScaleY);

        // 2. Convertir en coordonnées du graphique
        var coordinates = MainPlot.Plot.GetCoordinates(pixelX, pixelY);

        // 3. Mettre à jour la position du réticule
        _crosshair.X = coordinates.X;
        _crosshair.Y = coordinates.Y;
        _crosshair.IsVisible = true;

        // 4. Trouver la bougie correspondante
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

        // 5. Rafraîchir
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
            _currentBars = null;
            return;
        }

        _currentBars = result.Bars;

        Application.Current.Dispatcher.Invoke(() =>
        {
            // 1. Préparation des données
            var oldLimits = MainPlot.Plot.Axes.GetLimits();
            var scottCandles = result.Bars.Select((b, i) => new OHLC(
                b.Open, b.High, b.Low, b.Close,
                DateTime.FromOADate(i),
                TimeSpan.FromDays(0.8)
            )).ToList();
            DateTime[] realDates = result.Bars.Select(b => b.Timestamp).ToArray();

            // 2. Nettoyage
            MainPlot.Plot.Clear();

            // 3. DESSIN DES BOUGIES EN PREMIER (Fond)
            var series = MainPlot.Plot.Add.Candlestick(scottCandles);
            series.RisingColor = Color.FromHex("#2962FF");
            series.FallingColor = Color.FromHex("#F23645");

            // 4. AJOUT DU CROSSHAIR EN DERNIER (Premier plan)
            _crosshair = MainPlot.Plot.Add.Crosshair(0, 0);

            // Configuration explicite des lignes pour être sûr qu'elles s'affichent
            // On force le blanc pour tester la visibilité
            var lineColor = Color.FromHex("#FFFFFF");

            _crosshair.VerticalLine.Color = lineColor;
            _crosshair.VerticalLine.LineStyle.Width = 1;
            _crosshair.VerticalLine.LineStyle.Pattern = LinePattern.Dashed;
            _crosshair.VerticalLine.LabelStyle.IsVisible = false;

            _crosshair.HorizontalLine.Color = lineColor;
            _crosshair.HorizontalLine.LineStyle.Width = 1;
            _crosshair.HorizontalLine.LineStyle.Pattern = LinePattern.Dashed;
            _crosshair.HorizontalLine.LabelStyle.IsVisible = false;

            // Masquer par défaut jusqu'au mouvement de souris
            _crosshair.IsVisible = false;

            // 5. Axe X (Dates)
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

            // 6. Zoom
            if (_viewModel.ShouldResetZoom)
                MainPlot.Plot.Axes.AutoScale();
            else
                MainPlot.Plot.Axes.SetLimits(oldLimits);

            MainPlot.Refresh();
        });
    }
}