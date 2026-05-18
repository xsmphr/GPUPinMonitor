using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AstralPinWidget.Models;
using AstralPinWidget.Services;

namespace AstralPinWidget.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly Brush OkBrush = CreateBrush("#19F58A");
    private static readonly Brush WarningBrush = CreateBrush("#FFD166");
    private static readonly Brush CriticalBrush = CreateBrush("#FF4D5D");
    private static readonly Brush MutedBrush = CreateBrush("#8A9891");

    private readonly IPinPowerProvider _provider;
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    private bool _isTopmost = true;
    private string _sourceLine = "ASUS GPU Tweak III";
    private string _totalLine = "Total --";
    private string _lastUpdatedLine = "";
    private string _statusMessage = "Waiting for telemetry";
    private string _overallMark = "?";
    private Brush _overallBrush = MutedBrush;
    private Brush _messageBrush = MutedBrush;

    public MainWindowViewModel(IPinPowerProvider provider)
    {
        _provider = provider;
        Pins = new ObservableCollection<PinViewModel>(
            Enumerable.Range(1, 6).Select(number => new PinViewModel(number)));

        RefreshCommand = new RelayCommand(_ => Refresh());
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => Refresh();
    }

    public ObservableCollection<PinViewModel> Pins { get; }

    public ICommand RefreshCommand { get; }

    public bool IsTopmost
    {
        get => _isTopmost;
        set => SetProperty(ref _isTopmost, value);
    }

    public string SourceLine
    {
        get => _sourceLine;
        private set => SetProperty(ref _sourceLine, value);
    }

    public string TotalLine
    {
        get => _totalLine;
        private set => SetProperty(ref _totalLine, value);
    }

    public string LastUpdatedLine
    {
        get => _lastUpdatedLine;
        private set => SetProperty(ref _lastUpdatedLine, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string OverallMark
    {
        get => _overallMark;
        private set => SetProperty(ref _overallMark, value);
    }

    public Brush OverallBrush
    {
        get => _overallBrush;
        private set => SetProperty(ref _overallBrush, value);
    }

    public Brush MessageBrush
    {
        get => _messageBrush;
        private set => SetProperty(ref _messageBrush, value);
    }

    public void Start()
    {
        Refresh();
        _timer.Start();
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            ApplySnapshot(_provider.Read());
        }
        catch (Exception ex)
        {
            ApplyError(ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _disposed = true;
    }

    private void ApplySnapshot(PinPowerSnapshot snapshot)
    {
        var pinLookup = snapshot.Pins.ToDictionary(pin => pin.Pin);
        var severities = new List<PinSeverity>(Pins.Count);

        foreach (var pinViewModel in Pins)
        {
            if (pinLookup.TryGetValue(pinViewModel.Number, out var pin))
            {
                var severity = GetSeverity(pin.CurrentAmps, snapshot);
                pinViewModel.Update(pin.VoltageVolts, pin.CurrentAmps, severity);
                severities.Add(severity);
            }
            else
            {
                pinViewModel.Update(null, null, PinSeverity.Missing);
                severities.Add(PinSeverity.Missing);
            }
        }

        var overall = GetOverallSeverity(severities);
        SourceLine = snapshot.Source;
        TotalLine = $"Total {snapshot.TotalCurrentAmps:0.00}A / {snapshot.TotalPowerWatts:0}W";
        LastUpdatedLine = snapshot.Timestamp.ToLocalTime().ToString("HH:mm:ss");
        StatusMessage = StatusText(overall, snapshot);
        OverallMark = MarkFor(overall);
        OverallBrush = BrushFor(overall);
        MessageBrush = overall == PinSeverity.Ok ? MutedBrush : BrushFor(overall);
    }

    private void ApplyError(Exception ex)
    {
        foreach (var pin in Pins)
        {
            pin.Update(null, null, PinSeverity.Missing);
        }

        SourceLine = "No ASUS telemetry";
        TotalLine = "Total --";
        LastUpdatedLine = DateTimeOffset.Now.ToString("HH:mm:ss");
        StatusMessage = ex.Message;
        OverallMark = "!";
        OverallBrush = CriticalBrush;
        MessageBrush = CriticalBrush;
    }

    private static PinSeverity GetSeverity(double currentAmps, PinPowerSnapshot snapshot)
    {
        if (snapshot.HardwareAlert || currentAmps >= 9.2)
        {
            return PinSeverity.Critical;
        }

        if (currentAmps >= 8.5)
        {
            return PinSeverity.Warning;
        }

        var pins = snapshot.Pins;
        if (pins.Count >= 2)
        {
            var max = pins.Max(pin => pin.CurrentAmps);
            if (max > 1 && currentAmps / max < 0.72)
            {
                return PinSeverity.Warning;
            }
        }

        return PinSeverity.Ok;
    }

    private static PinSeverity GetOverallSeverity(IEnumerable<PinSeverity> severities)
    {
        if (severities.Any(severity => severity == PinSeverity.Critical))
        {
            return PinSeverity.Critical;
        }

        if (severities.Any(severity => severity == PinSeverity.Warning))
        {
            return PinSeverity.Warning;
        }

        if (severities.All(severity => severity == PinSeverity.Missing))
        {
            return PinSeverity.Missing;
        }

        return PinSeverity.Ok;
    }

    private static string StatusText(PinSeverity severity, PinPowerSnapshot snapshot)
    {
        return severity switch
        {
            PinSeverity.Critical when snapshot.HardwareAlert => "ASUS hardware alert",
            PinSeverity.Critical => "High pin current",
            PinSeverity.Warning => "Pins are uneven",
            PinSeverity.Ok => $"{snapshot.Pins.Count} pins online",
            _ => "Waiting for telemetry"
        };
    }

    private static string MarkFor(PinSeverity severity)
    {
        return severity switch
        {
            PinSeverity.Ok => "OK",
            PinSeverity.Warning => "!",
            PinSeverity.Critical => "!",
            _ => "?"
        };
    }

    private static Brush BrushFor(PinSeverity severity)
    {
        return severity switch
        {
            PinSeverity.Ok => OkBrush,
            PinSeverity.Warning => WarningBrush,
            PinSeverity.Critical => CriticalBrush,
            _ => MutedBrush
        };
    }

    private static Brush CreateBrush(string color)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }
}
