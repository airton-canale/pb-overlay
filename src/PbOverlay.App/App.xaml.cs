using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;
using PbOverlay.Core.Capture;
using PbOverlay.Core.Config;
using PbOverlay.Core.Fps;
using Serilog;
using Serilog.Extensions.Logging;

namespace PbOverlay.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!SingleInstance.TryAcquire(@"Global\PbOverlay.SingleInstance", out _instanceMutex))
        {
            MessageBox.Show("PbOverlay is already running.");
            Shutdown();
            return;
        }

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PbOverlay",
            "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDir, "pboverlay-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        AppServices.LoggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);

        try
        {
            AppServices.Config = ConfigStore.Load();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message);
            Shutdown();
            return;
        }

        AppServices.CaptureFactory = new CaptureSourceFactory(AppServices.LoggerFactory);
        AppServices.FpsSource = new NullFrameTimingSource();

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        if (_instanceMutex is not null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { /* not owned — fine */ }
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }
        base.OnExit(e);
    }
}
