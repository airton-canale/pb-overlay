namespace PbOverlay.Core.Fps;

public sealed class NullFrameTimingSource : IFrameTimingSource
{
    public event EventHandler<FpsSample>? SampleReady { add { } remove { } }
    public bool IsRunning => false;

    public Task StartAsync(int processId, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
