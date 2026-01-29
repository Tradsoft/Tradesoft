using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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

        public ObservableCollection<string> StrategyNames { get; } = new()
        {
            "RSI", "MovingAverageCrossover", "MeanReversion", "Breakout"
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

        public MainViewModel()
        {
            _backtestService = new BacktestService();
            GenerateParameters();
            Task.Delay(500).ContinueWith(_ => TriggerBacktest(resetZoom: true));
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

        private async void TriggerBacktest(bool resetZoom)
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
                    StrategyParameters = new Dictionary<string, object>()
                };
                foreach (var p in Parameters) config.StrategyParameters[p.Name] = p.Value;

                _cts = new CancellationTokenSource();
                var result = await _backtestService.RunBacktestAsync(config, _cts.Token);

                StatusText = $"{result.Bars.Count} bougies chargées.";
                BacktestCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
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
                "MovingAverageCrossover" => new() { { "Short Period", 20 }, { "Long Period", 50 }, { "Lot", 1.0 } },
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
}