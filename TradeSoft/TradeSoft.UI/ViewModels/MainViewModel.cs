using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using TradeSoft.Core.Domain;
using TradeSoft.Core.DTO;
using TradeSoft.UI.Services;

namespace TradeSoft.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BacktestService _backtestService;
        private CancellationTokenSource? _cts;

        private DateTime _startDate = new DateTime(2015, 01, 01);
        public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged(); LoadMarketData(); } }

        private DateTime _endDate = new DateTime(2015, 01, 05);
        public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged(); LoadMarketData(); } }

        public ObservableCollection<TimeSpan> AvailableTimeframes { get; } = new() {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromHours(1),
            TimeSpan.FromHours(4), TimeSpan.FromDays(1)
        };
        private TimeSpan _selectedTimeframe = TimeSpan.FromMinutes(5);
        public TimeSpan SelectedTimeframe { get => _selectedTimeframe; set { _selectedTimeframe = value; OnPropertyChanged(); LoadMarketData(); } }

        private double _initialCash = 10000.0;
        public double InitialCash { get => _initialCash; set { _initialCash = value; OnPropertyChanged(); } }

        private double _leverage = 30.0;
        public double Leverage { get => _leverage; set { _leverage = value; OnPropertyChanged(); } }

        private double _commission = 2.5;
        public double Commission { get => _commission; set { _commission = value; OnPropertyChanged(); } }

        private double _slippage = 0.0;
        public double Slippage { get => _slippage; set { _slippage = value; OnPropertyChanged(); } }

        private double _maxRiskPerTrade = 1.0;
        public double MaxRiskPerTrade { get => _maxRiskPerTrade; set { _maxRiskPerTrade = value; OnPropertyChanged(); } }

        private int _maxPositions = 5;
        public int MaxPositions { get => _maxPositions; set { _maxPositions = value; OnPropertyChanged(); } }

        private double _maxDrawdown = 10.0;
        public double MaxDrawdown { get => _maxDrawdown; set { _maxDrawdown = value; OnPropertyChanged(); } }

        private double _maxDailyLoss = 5.0;
        public double MaxDailyLoss { get => _maxDailyLoss; set { _maxDailyLoss = value; OnPropertyChanged(); } }

        private double _minRR = 1.0;
        public double MinRiskReward { get => _minRR; set { _minRR = value; OnPropertyChanged(); } }

        public ObservableCollection<string> StrategyNames { get; } = new() { "RSI", "MAC", "MeanReversion", "Breakout" };
        private string _selectedStrategyName = "RSI";
        public string SelectedStrategyName
        {
            get => _selectedStrategyName;
            set { _selectedStrategyName = value; OnPropertyChanged(); GenerateParameters(); }
        }

        public ObservableCollection<StrategyParameterViewModel> Parameters { get; } = new();

        private string _statusText = "Prêt.";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        public event EventHandler<BacktestResult>? BacktestCompleted;
        public event EventHandler<List<Bar>>? MarketDataLoaded;
        public bool ShouldResetZoom { get; private set; }
        public ICommand RunBacktestCommand { get; }

        public MainViewModel()
        {
            _backtestService = new BacktestService();
            RunBacktestCommand = new RelayCommand(async () => await RunBacktestAsync());
            GenerateParameters();
            Task.Delay(500).ContinueWith(_ => LoadMarketData());
        }

        private async void LoadMarketData()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusText = "Chargement données...";
            try
            {
                var config = CreateConfig();
                var bars = await _backtestService.GetHistoricalBarsAsync(config);
                ShouldResetZoom = true;
                MarketDataLoaded?.Invoke(this, bars);
                StatusText = $"{bars.Count} bougies chargées.";
            }
            catch (Exception)
            {
                StatusText = "Erreur chargement.";
            }
            finally { IsBusy = false; }
        }

        public async Task RunBacktestAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ShouldResetZoom = false;
            StatusText = "Calcul stratégie...";

            try
            {
                var config = CreateConfig();
                _cts = new CancellationTokenSource();
                var result = await _backtestService.RunBacktestAsync(config, _cts.Token);

                StatusText = $"{result.TotalTrades} trades. PnL: {result.NetProfit:F2}";
                BacktestCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                StatusText = $"Erreur: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private BacktestConfig CreateConfig()
        {
            var config = new BacktestConfig
            {
                StartDate = StartDate,
                EndDate = EndDate,
                Timeframe = SelectedTimeframe,
                Symbol = "EURUSD",
                Strategy = Enum.Parse<StrategyType>(_selectedStrategyName),
                StrategyParameters = new Dictionary<string, object>(),

                InitialCash = InitialCash,
                Leverage = Leverage,
                CommissionPerLot = Commission,
                CommissionType = CommissionType.Fixed,
                SlippagePips = Slippage,

                MaxRiskPerTradePercent = MaxRiskPerTrade,
                MaxConcurrentPositions = MaxPositions,
                MaxDrawdownPercent = MaxDrawdown,
                MaxDailyLossPercent = MaxDailyLoss,
                MinRiskRewardRatio = MinRiskReward,

                MinLotSize = 1.0,
                LotSize = 100000.0,
                TickSize = 0.01
            };
            foreach (var p in Parameters) config.StrategyParameters[p.Name] = p.Value;
            return config;
        }

        private void GenerateParameters()
        {
            Parameters.Clear();
            var defaults = GetDefaultParameters(_selectedStrategyName);
            foreach (var kvp in defaults) Parameters.Add(new StrategyParameterViewModel(kvp.Key, kvp.Value));
        }

        private Dictionary<string, double> GetDefaultParameters(string strategyName)
        {
            return strategyName switch
            {
                "RSI" => new() { { "Period", 14 }, { "Oversold", 30 }, { "Overbought", 70 }, { "Lot", 1.0 }, { "SL Pips", 50 }, { "TP Pips", 100 }, { "Tick Size", 0.01 } },
                "MAC" => new() { { "Short Period", 20 }, { "Long Period", 50 }, { "Lot", 1.0 } },
                "MeanReversion" => new() { { "Period", 20 }, { "K", 2.0 }, { "Lot", 1.0 } },
                "Breakout" => new() { { "Lookback", 20 }, { "Lot", 1.0 } },
                _ => new()
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class StrategyParameterViewModel : INotifyPropertyChanged
    {
        public string Name { get; }
        private double _value;
        public double Value { get => _value; set { _value = value; OnPropertyChanged(); } }
        public StrategyParameterViewModel(string name, double val) { Name = name; Value = val; }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        public RelayCommand(Func<Task> execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public async void Execute(object? parameter) => await _execute();
    }
}