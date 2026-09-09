namespace PbOverlay.Core.Fps;

public interface IFrameTimingSource : IAsyncDisposable
{
    event EventHandler<FpsSample>? SampleReady;
    bool IsRunning { get; }
    Task StartAsync(int processId, CancellationToken cancellationToken);
    Task StopAsync();
}
