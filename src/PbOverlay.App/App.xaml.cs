using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using PbOverlay.App.Overlay;
using PbOverlay.Core.Capture;
using PbOverlay.Core.Config;
using PbOverlay.Core.Fps;
using Serilog;
using Serilog.Extensions.Logging;

namespace PbOverlay.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private LiveClassifierRunner? _classifierRunner;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch-all so a rogue capture/classifier exception (fullscreen switch,
        // resolution change, OpenCV native fault) logs and continues instead of
        // taking the whole overlay down.
        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;

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

        _classifierRunner = new LiveClassifierRunner();

        // ponytail: temporary layout-review switch, delete once mockup becomes MainWindow
        if (e.Args.Contains("--mockup"))
            new MockupWindow().Show();
        else
            new MainWindow().Show();
    }

    private static void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unhandled dispatcher exception");
        e.Handled = true;
    }

    private static void OnAppDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Logger.Error(ex, "Unhandled AppDomain exception (terminating={Terminating})", e.IsTerminating);
    }

    private static void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
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
