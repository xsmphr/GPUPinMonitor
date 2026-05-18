using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AstralPinWidget.Native;
using AstralPinWidget.Services;
using AstralPinWidget.ViewModels;

namespace AstralPinWidget;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly WindowSettingsStore _settingsStore = new();
    private readonly DispatcherTimer _saveSettingsTimer;
    private bool _settingsLoaded;

    public MainWindow()
    {
        _viewModel = new MainWindowViewModel(new GpuTweakPowerProvider());
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _saveSettingsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _saveSettingsTimer.Tick += (_, _) =>
        {
            _saveSettingsTimer.Stop();
            SaveSettings();
        };

        DataContext = _viewModel;
        InitializeComponent();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Load();
        _viewModel.IsTopmost = settings.IsTopmost;

        if (settings.Left.HasValue && settings.Top.HasValue)
        {
            Left = settings.Left.Value;
            Top = settings.Top.Value;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 28;
            Top = area.Top + 40;
        }

        _settingsLoaded = true;
        _viewModel.Start();
    }

    private void Window_OnClosing(object? sender, CancelEventArgs e)
    {
        _saveSettingsTimer.Stop();
        SaveSettings();
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel.Dispose();
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        NativeWindowStyles.HideFromAltTab(this);
    }

    private void Window_OnLocationChanged(object? sender, EventArgs e)
    {
        ScheduleSaveSettings();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsTopmost))
        {
            ScheduleSaveSettings();
        }
    }

    private void WidgetRoot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            _viewModel.Refresh();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ScheduleSaveSettings()
    {
        if (!_settingsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        _saveSettingsTimer.Stop();
        _saveSettingsTimer.Start();
    }

    private void SaveSettings()
    {
        if (double.IsNaN(Left) || double.IsNaN(Top))
        {
            return;
        }

        _settingsStore.Save(new WidgetSettings(Left, Top, _viewModel.IsTopmost));
    }
}
