using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TradeSoft.Core.DTO;
using TradeSoft.UI.Services;

namespace TradeSoft.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly BacktestService _backtestService;
        private CancellationTokenSource? _cts;

        private DateTime _startDate = new DateTime(2015, 01, 01);
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); TriggerBacktest(resetZoom: true); }
        }

        private DateTime _endDate = new DateTime(2015, 01, 05);
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); TriggerBacktest(resetZoom: true); }
        }

        public ObservableCollection<TimeSpan> AvailableTimeframes { get; } = new()
        {
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromHours(1),
            TimeSpan.FromHours(4), TimeSpan.FromDays(1), TimeSpan.FromDays(30)
        };

        private TimeSpan _selectedTimeframe = TimeSpan.FromMinutes(5);
        public TimeSpan SelectedTimeframe
        {
            get => _selectedTimeframe;
            set { _selectedTimeframe = value; OnPropertyChanged(); TriggerBacktest(resetZoom: true); }
        }

        // Risk Management Properties
        private double _initialCash = 10000.0;
        public double InitialCash
        {
            get => _initialCash;
            set 
            { 
                if (Math.Abs(_initialCash - value) > 0.01)
                {
                    _initialCash = value; 
                    OnPropertyChanged();
                }
            }
        }

        private double _leverage = 30.0;
        public double Leverage
        {
            get => _leverage;
            set 
            { 
                if (Math.Abs(_leverage - value) > 0.01)
                {
                    _leverage = value; 
                    OnPropertyChanged();
                }
            }
        }

        private double _minLotSize = 100.0;
        public double MinLotSize
        {
            get => _minLotSize;
            set 
            { 
                if (Math.Abs(_minLotSize - value) > 0.01)
                {
                    _minLotSize = value; 
                    OnPropertyChanged();
                }
            }
        }

        private double _maxRiskPerTrade = 1.0;
        public double MaxRiskPerTrade
        {
            get => _maxRiskPerTrade;
            set 
            { 
                if (Math.Abs(_maxRiskPerTrade - value) > 0.01)
                {
                    _maxRiskPerTrade = value; 
                    OnPropertyChanged();
                }
            }
        }

        private int _maxPositions = 5;
        public int MaxPositions
        {
            get => _maxPositions;
            set 
            { 
                if (_maxPositions != value)
                {
                    _maxPositions = value; 
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> StrategyNames { get; } = new()
        {
            "RSI", "MAC", "MeanReversion", "Breakout"
        };

        private string _selectedStrategyName = "RSI";
        public string SelectedStrategyName
        {
            get => _selectedStrategyName;
            set
            {
                _selectedStrategyName = value;
                OnPropertyChanged();
                GenerateParameters();
                TriggerBacktest(resetZoom: false);
            }
        }

        public ObservableCollection<StrategyParameterViewModel> Parameters { get; } = new();

        private string _statusText = "Prêt.";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        public event EventHandler<BacktestResult>? BacktestCompleted;
        public bool ShouldResetZoom { get; private set; }

        public ICommand RunBacktestCommand { get; }

        public MainViewModel()
        {
            _backtestService = new BacktestService();
            RunBacktestCommand = new RelayCommand(async () => await RunBacktestAsync());
            GenerateParameters();
            
            // Lancer le backtest initial après un délai pour laisser l'UI se charger
            Task.Delay(500).ContinueWith(_ => 
            {
                try
                {
                    TriggerBacktest(resetZoom: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in initial backtest: {ex}");
                    StatusText = $"Erreur initialisation: {ex.Message}";
                }
            });
        }

        public async Task RunBacktestAsync()
        {
            await TriggerBacktest(resetZoom: true);
        }

        private void GenerateParameters()
        {
            Parameters.Clear();
            var defaults = GetDefaultParameters(_selectedStrategyName);
            foreach (var kvp in defaults)
            {
                var paramVM = new StrategyParameterViewModel(kvp.Key, kvp.Value);
                paramVM.ValueChanged += (s, e) => TriggerBacktest(resetZoom: false);
                Parameters.Add(paramVM);
            }
        }

        private async Task TriggerBacktest(bool resetZoom)
        {
            if (IsBusy) return;
            IsBusy = true;
            ShouldResetZoom = resetZoom;
            StatusText = "Calcul en cours...";

            try
            {
                var config = new BacktestConfig
                {
                    StartDate = StartDate,
                    EndDate = EndDate,
                    Timeframe = SelectedTimeframe,
                    Strategy = Enum.Parse<StrategyType>(_selectedStrategyName),
                    StrategyParameters = new Dictionary<string, object>(),
                    InitialCash = InitialCash,
                    Leverage = Leverage,
                    MinLotSize = MinLotSize,
                    MaxRiskPerTradePercent = MaxRiskPerTrade,
                    MaxConcurrentPositions = MaxPositions
                };
                foreach (var p in Parameters) config.StrategyParameters[p.Name] = p.Value;

                _cts = new CancellationTokenSource();
                var result = await _backtestService.RunBacktestAsync(config, _cts.Token);

                StatusText = $"{result.Bars.Count} bougies chargées.";
                BacktestCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backtest error: {ex}");
                StatusText = $"Erreur : {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private Dictionary<string, double> GetDefaultParameters(string strategyName)
        {
            return strategyName switch
            {
                "RSI" => new() { { "Period", 14 }, { "Oversold", 30 }, { "Overbought", 70 }, { "Lot", 1.0 } },
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
        public event EventHandler? ValueChanged;

        public double Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > 0.00001)
                {
                    _value = value;
                    OnPropertyChanged();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public StrategyParameterViewModel(string name, double defaultValue) { Name = name; Value = defaultValue; }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object? parameter) => await _execute();
    }
}