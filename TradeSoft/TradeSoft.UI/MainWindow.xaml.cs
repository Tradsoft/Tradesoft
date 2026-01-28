using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using TradeSoft.Core.DTO;
using TradeSoft.Core.Services;
using TradeSoft.Engine.Services;
using TradeSoft.Infrastructure.IO;

namespace TradeSoft.UI;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		
		SetupPlotStyle(PlotPrice, "Exécution des Trades (EUR/USD)");
		SetupPlotStyle(PlotEquity, "Performance du Capital (Equity)");
	}

	private void SetupPlotStyle(ScottPlot.WPF.WpfPlot plot, string title)
	{
		
		plot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#121212");
		plot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");

		
		plot.Plot.Axes.Color(ScottPlot.Color.FromHex("#FFFFFF"));

		
		plot.Plot.Title(title, size: 16);
		plot.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");

		plot.Plot.Axes.DateTimeTicksBottom();
		plot.Refresh();
	}

	private async void BtnRun_Click(object sender, RoutedEventArgs e)
	{
		BtnRun.IsEnabled = false;
		TxtStatus.Text = "Calcul en cours...";

		
		string[] cheminsPossibles = new[] {
			@"C:\Users\aleja\OneDrive - Efrei\Documents\GitHub\Tradesoft\TradeSoft\Data\EURUSD\EURUSD",
			@"C:\Users\FlowUP\source\repos\GitHub\Tradesoft\Data\EURUSD",
			Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Data\EURUSD"))
		};
		string dossierData = cheminsPossibles.FirstOrDefault(path => Directory.Exists(path));

		if (string.IsNullOrEmpty(dossierData))
		{
			MessageBox.Show("Dossier de données introuvable !");
			BtnRun.IsEnabled = true;
			return;
		}

		try
		{
			
			double cash = double.Parse(TxtInitialCash.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
			double maxDD = double.Parse(TxtMaxDD.Text.Replace(',', '.'), CultureInfo.InvariantCulture);

			var config = new BacktestConfig
			{
				Symbol = "EURUSD",
				StartDate = PickerStart.SelectedDate ?? new DateTime(2015, 01, 01),
				EndDate = PickerEnd.SelectedDate ?? new DateTime(2015, 01, 02),
				InitialCash = cash,
				Strategy = (StrategyType)ComboStrategy.SelectedIndex,
				MaxDrawdownPercent = maxDD,
				StrategyParameters = new Dictionary<string, object> { { "Period", 14 } }
			};

			using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
			var provider = new MultiFileTickProvider(dossierData, loggerFactory.CreateLogger<MultiFileTickProvider>());
			IBacktestService service = new BacktestService(provider, loggerFactory);

			var result = await service.RunBacktestAsync(config, new Progress<float>(p => ProgBar.Value = p * 100));

			// 1. GRAPHIQUE DES PRIX AVEC TRADES
			PlotPrice.Plot.Clear();
			if (result.Trades != null && result.Trades.Any())
			{
				var line = PlotPrice.Plot.Add.ScatterLine(
					result.Trades.Select(t => t.Time.ToOADate()).ToArray(),
					result.Trades.Select(t => t.Price).ToArray()
				);
				line.Color = ScottPlot.Color.FromHex("#44FFFFFF");

				var buys = result.Trades.Where(t => t.Side.ToLower().Contains("buy")).ToList();
				if (buys.Any())
				{
					var m = PlotPrice.Plot.Add.Markers(buys.Select(b => b.Time.ToOADate()).ToArray(), buys.Select(b => b.Price).ToArray());
					m.Color = ScottPlot.Color.FromHex("#00FF00");
					m.MarkerShape = ScottPlot.MarkerShape.FilledTriangleUp;
					m.MarkerSize = 7;
				}

				var sells = result.Trades.Where(t => t.Side.ToLower().Contains("sell")).ToList();
				if (sells.Any())
				{
					var m = PlotPrice.Plot.Add.Markers(sells.Select(s => s.Time.ToOADate()).ToArray(), sells.Select(s => s.Price).ToArray());
					m.Color = ScottPlot.Color.FromHex("#FF0000");
					m.MarkerShape = ScottPlot.MarkerShape.FilledTriangleDown;
					m.MarkerSize = 7;
				}
			}
			PlotPrice.Plot.Axes.Color(ScottPlot.Color.FromHex("#FFFFFF"));
			PlotPrice.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");
			PlotPrice.Plot.Axes.AutoScale();
			PlotPrice.Refresh();

			// 2. GRAPHIQUE PERFORMANCE 
			PlotEquity.Plot.Clear();
			if (result.EquityCurve != null && result.EquityCurve.Any())
			{
				var clean = result.EquityCurve.Where(x => x.Time.Year > 2000).ToList();
				var sc = PlotEquity.Plot.Add.Scatter(
					clean.Select(x => x.Time.ToOADate()).ToArray(),
					clean.Select(x => x.Value).ToArray()
				);
				sc.Color = ScottPlot.Color.FromHex("#007ACC");
				sc.MarkerSize = 0;
				sc.LineWidth = 2;
			}
			PlotEquity.Plot.Axes.Color(ScottPlot.Color.FromHex("#FFFFFF"));
			PlotEquity.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");
			PlotEquity.Plot.Axes.AutoScale();
			PlotEquity.Refresh();

			// 3. STATS
			TxtPnL.Text = $"{result.NetProfit:C2}";
			TxtWin.Text = $"{result.WinRate:F1}%";
			TxtDD.Text = $"{result.MaxDrawdownPercent:F1}%";
			TxtPF.Text = $"{result.ProfitFactor:F2}";
			TxtPnL.Foreground = result.NetProfit >= 0 ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red;
			TxtStatus.Text = "Analyse terminée.";
		}
		catch (Exception ex)
		{
			MessageBox.Show("Erreur : " + ex.Message);
			TxtStatus.Text = "Erreur.";
		}
		finally
		{
			BtnRun.IsEnabled = true;
		}
	}
}