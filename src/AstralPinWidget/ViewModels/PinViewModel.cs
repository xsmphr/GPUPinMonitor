using System.Windows.Media;

namespace AstralPinWidget.ViewModels;

public sealed class PinViewModel : ObservableObject
{
    private const double MaxConnectorPowerWatts = 600.0;
    private const double NominalVoltageVolts = 12.0;
    private const double ConnectorPinCount = 6.0;
    private const double MaxPinCurrentAmps = MaxConnectorPowerWatts / NominalVoltageVolts / ConnectorPinCount;
    private const double MaxBarWidth = 110.0;
    private const double MinVisibleBarWidth = 2.0;

    private static readonly Brush MissingBrush = CreateBrush("#4A5651");
    private static readonly Brush OkBrush = CreateBrush("#19F58A");
    private static readonly Brush WarningBrush = CreateBrush("#FFD166");
    private static readonly Brush CriticalBrush = CreateBrush("#FF4D5D");
    private static readonly Brush QuietTextBrush = CreateBrush("#8B9993");
    private static readonly Brush TextBrush = CreateBrush("#F0FFF8");

    private double? _voltageVolts;
    private double? _currentAmps;
    private double _barWidth = 2;
    private double _towerHeight = 9;
    private Brush _statusBrush = MissingBrush;
    private Brush _valueBrush = QuietTextBrush;
    private PinSeverity _severity = PinSeverity.Missing;

    public PinViewModel(int number)
    {
        Number = number;
    }

    public int Number { get; }

    public string Label => $"Pin {Number}";

    public double? VoltageVolts
    {
        get => _voltageVolts;
        private set
        {
            if (SetProperty(ref _voltageVolts, value))
            {
                OnPropertyChanged(nameof(VoltageTooltip));
            }
        }
    }

    public double? CurrentAmps
    {
        get => _currentAmps;
        private set
        {
            if (SetProperty(ref _currentAmps, value))
            {
                OnPropertyChanged(nameof(DisplayCurrent));
            }
        }
    }

    public string DisplayCurrent => CurrentAmps.HasValue
        ? $"{CurrentAmps.Value:0.00}A"
        : "--";

    public string VoltageTooltip => VoltageVolts.HasValue
        ? $"{VoltageVolts.Value:0.00}V"
        : "No voltage data";

    public string StatusTooltip => _severity switch
    {
        PinSeverity.Ok => $"{Label}: OK",
        PinSeverity.Warning => $"{Label}: uneven current",
        PinSeverity.Critical => $"{Label}: high current or ASUS hardware alert",
        _ => $"{Label}: no data"
    };

    public double BarWidth
    {
        get => _barWidth;
        private set => SetProperty(ref _barWidth, value);
    }

    public double TowerHeight
    {
        get => _towerHeight;
        private set => SetProperty(ref _towerHeight, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public Brush ValueBrush
    {
        get => _valueBrush;
        private set => SetProperty(ref _valueBrush, value);
    }

    public void Update(double? voltageVolts, double? currentAmps, PinSeverity severity)
    {
        VoltageVolts = voltageVolts;
        CurrentAmps = currentAmps;
        if (_severity != severity)
        {
            _severity = severity;
            OnPropertyChanged(nameof(StatusTooltip));
        }

        StatusBrush = BrushFor(severity);
        ValueBrush = severity == PinSeverity.Missing ? QuietTextBrush : TextBrush;

        if (currentAmps.HasValue)
        {
            var normalizedCurrent = currentAmps.Value / MaxPinCurrentAmps;
            BarWidth = Math.Clamp(normalizedCurrent * MaxBarWidth, MinVisibleBarWidth, MaxBarWidth);
            TowerHeight = Math.Clamp(13 + currentAmps.Value * 4, 13, 42);
        }
        else
        {
            BarWidth = 2;
            TowerHeight = 9;
        }
    }

    private static Brush BrushFor(PinSeverity severity)
    {
        return severity switch
        {
            PinSeverity.Ok => OkBrush,
            PinSeverity.Warning => WarningBrush,
            PinSeverity.Critical => CriticalBrush,
            _ => MissingBrush
        };
    }

    private static Brush CreateBrush(string color)
    {
        var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
        brush.Freeze();
        return brush;
    }
}
