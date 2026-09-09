using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PbOverlay.Core.Fps;

public sealed class PresentMonProcessSource : IFrameTimingSource
{
    private readonly ILogger<PresentMonProcessSource> _log;
    private readonly int _rollingWindowSeconds;
    private readonly bool _emitFrameTimeMs;

    private Process? _proc;
    private Task? _readerTask;
    private CancellationTokenSource? _cts;
    private readonly List<(long tsMs, double frameTimeMs)> _samples = new();
    private long _lastEmitTsMs;
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public event EventHandler<FpsSample>? SampleReady;
    public bool IsRunning => _readerTask is { IsCompleted: false };

    public PresentMonProcessSource(
        ILogger<PresentMonProcessSource> log,
        int rollingWindowSeconds = 5,
        bool emitFrameTimeMs = false)
    {
        _log = log;
        _rollingWindowSeconds = Math.Max(1, rollingWindowSeconds);
        _emitFrameTimeMs = emitFrameTimeMs;
    }

    public Task StartAsync(int processId, CancellationToken cancellationToken)
    {
        if (IsRunning) return Task.CompletedTask;

        var exe = Path.Combine(AppContext.BaseDirectory, "tools", "PresentMon", "PresentMon.exe");
        if (!File.Exists(exe))
        {
            _log.LogWarning("PresentMon binary not found at {Path}; FPS readout disabled.", exe);
            return Task.CompletedTask;
        }

        if (!ElevationCheck.IsProcessElevated())
        {
            _log.LogWarning("PresentMon requires the app to be launched elevated; FPS readout disabled.");
            return Task.CompletedTask;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--process_id");
        psi.ArgumentList.Add(processId.ToString());
        psi.ArgumentList.Add("--output_stdout");
        psi.ArgumentList.Add("--stop_existing_session");
        psi.ArgumentList.Add("--terminate_on_proc_exit");

        _proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to spawn PresentMon.");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readerTask = Task.Run(() => ConsumeAsync(_proc.StandardOutput, _cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync(StreamReader reader, CancellationToken ct)
    {
        Dictionary<string, int>? cols = null;
        int frameTimeCol = -1;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) break;
            if (line.Length == 0) continue;

            if (cols is null)
            {
                cols = ParseHeader(line);
                if (!cols.TryGetValue("MsBetweenPresents", out frameTimeCol)
                    && !cols.TryGetValue("msBetweenPresents", out frameTimeCol))
                {
                    _log.LogError("PresentMon CSV header missing MsBetweenPresents column: {Header}", line);
                    return;
                }
                continue;
            }

            // ponytail: naive Split(',') — safe today because PresentMon does not quote
            // its fields. If a future version starts emitting quoted commas, swap in
            // TextFieldParser or Sylvan.Data.Csv.
            var parts = line.Split(',');
            if (parts.Length <= frameTimeCol) continue;
            if (!double.TryParse(parts[frameTimeCol], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var frameTimeMs)) continue;

            var nowMs = _sw.ElapsedMilliseconds;
            _samples.Add((nowMs, frameTimeMs));
            EvictOld(nowMs);

            if (nowMs - _lastEmitTsMs >= 250)
            {
                _lastEmitTsMs = nowMs;
                EmitSample();
            }
        }
    }

    private void EvictOld(long nowMs)
    {
        var cutoff = nowMs - _rollingWindowSeconds * 1000L;
        var keep = 0;
        for (var i = 0; i < _samples.Count; i++)
        {
            if (_samples[i].tsMs >= cutoff) { keep = i; break; }
            keep = i + 1;
        }
        if (keep > 0) _samples.RemoveRange(0, keep);
    }

    private void EmitSample()
    {
        if (_samples.Count == 0) return;
        var sum = 0.0;
        for (var i = 0; i < _samples.Count; i++) sum += _samples[i].frameTimeMs;
        var avgFrameTime = sum / _samples.Count;
        var avgFps = avgFrameTime > 0 ? 1000.0 / avgFrameTime : 0.0;

        var sorted = _samples.Select(s => s.frameTimeMs).OrderBy(x => x).ToList();
        var idx = (int)Math.Clamp(Math.Floor(sorted.Count * 0.99), 0, sorted.Count - 1);
        var worstFrameTime = sorted[idx];
        var onePercentLow = worstFrameTime > 0 ? 1000.0 / worstFrameTime : 0.0;

        var sample = new FpsSample(avgFps, onePercentLow, _emitFrameTimeMs ? avgFrameTime : null);
        SampleReady?.Invoke(this, sample);
    }

    private static Dictionary<string, int> ParseHeader(string header)
    {
        var cols = header.Split(',');
        var map = new Dictionary<string, int>(cols.Length, StringComparer.Ordinal);
        for (var i = 0; i < cols.Length; i++) map[cols[i]] = i;
        return map;
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_proc is { HasExited: false })
        {
            try { _proc.CloseMainWindow(); } catch { }
            if (!_proc.WaitForExit(500))
            {
                try { _proc.Kill(entireProcessTree: true); } catch { }
            }
        }
        if (_readerTask is not null)
        {
            try { await _readerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        _proc?.Dispose();
        _proc = null;
        _readerTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
