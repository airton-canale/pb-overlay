using System.Windows;
using Microsoft.Extensions.Logging;
using PbOverlay.App.Overlay;
using PbOverlay.Core.Config;
using Serilog;

namespace PbOverlay.App;

public partial class MainWindow : Window
{
    private HotkeyService? _hotkey;
    private int _hotkeyId = -1;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            _hotkey = new HotkeyService(this);
            _hotkeyId = _hotkey.Register(
                AppServices.Config.Overlay.ToggleHotkey,
                () => AppServices.CurrentOverlay?.ToggleVisibility());
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex,
                "Failed to register toggle hotkey {Hotkey}",
                AppServices.Config.Overlay.ToggleHotkey);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_hotkey is not null && _hotkeyId != -1)
        {
            try { _hotkey.Unregister(_hotkeyId); } catch { /* ignore */ }
        }
        _hotkey?.Dispose();
        _hotkey = null;
    }

    private void ShowHideButton_Click(object sender, RoutedEventArgs e)
        => AppServices.CurrentOverlay?.ToggleVisibility();

    private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigStore.Save(AppServices.Config);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save config: {ex.Message}");
        }
    }
}
