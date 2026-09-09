using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using PbOverlay.App.Overlay;
using PbOverlay.Core.Capture;
using PbOverlay.Core.Fps;
using Serilog;

namespace PbOverlay.App.Tabs;

public partial class CaptureSourceTab : UserControl
{
    public CaptureSourceTab()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TitleSubstringBox.Text = AppServices.Config.Target.TitleSubstring;
            ProcessNameBox.Text = AppServices.Config.Target.ProcessName;
            RefreshWindowsList();
        };
    }

    private void RefreshWindowsList()
    {
        WindowsList.ItemsSource = WindowEnumerator.EnumerateVisibleWindows();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshWindowsList();

    private void UseSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsList.SelectedItem is not WindowEnumerator.WindowInfo w)
            return;
        TitleSubstringBox.Text = w.Title;
        ProcessNameBox.Text = w.ProcessName;
    }

    private async void StartCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        // Persist the current text-box values into config before searching.
        AppServices.Config.Target.TitleSubstring = TitleSubstringBox.Text;
        AppServices.Config.Target.ProcessName = ProcessNameBox.Text;

        var cfg = AppServices.Config;
        var target = WindowEnumerator.FindTarget(
            WindowEnumerator.EnumerateVisibleWindows(),
            cfg.Target.TitleSubstring,
            cfg.Target.ProcessName);

        if (target is null)
        {
            StatusText.Foreground = Brushes.Red;
            StatusText.Text = "target not found";
            return;
        }

        StatusText.Foreground = Brushes.Black;
        StatusText.Text = $"capturing {target.Title} ({target.ProcessName}, pid {target.ProcessId})";

        var hwnd = target.Hwnd;
        var source = AppServices.CaptureFactory.Create(10, () => hwnd, WindowEnumerator.IsForeground);
        AppServices.PublishCaptureSource(source);
        await source.StartAsync(CancellationToken.None);

        if (AppServices.CurrentOverlay is null)
        {
            AppServices.CurrentOverlay = new OverlayWindow(AppServices.Config.Overlay);
        }
        AppServices.CurrentOverlay.Show();

        await StartFpsAsync(target.ProcessId, cfg);
    }

    private async Task StartFpsAsync(int pid, Core.Config.AppConfig cfg)
    {
        if (!cfg.Fps.Enabled)
        {
            Log.Logger.Information("FPS disabled by config; overlay will show n/a");
            AppServices.CurrentOverlay?.SetFps(null);
            return;
        }
        if (!ElevationCheck.IsProcessElevated())
        {
            Log.Logger.Information("Process not elevated; PresentMon FPS source disabled");
            AppServices.CurrentOverlay?.SetFps(null);
            return;
        }

        var fps = new PresentMonProcessSource(
            AppServices.LoggerFactory.CreateLogger<PresentMonProcessSource>(),
            cfg.Fps.RollingWindowSeconds,
            cfg.Fps.ShowFrameTimeMs);
        fps.SampleReady += (_, sample) =>
            Dispatcher.Invoke(() => AppServices.CurrentOverlay?.SetFps(sample));
        AppServices.FpsSource = fps;
        await fps.StartAsync(pid, CancellationToken.None);
    }
}
