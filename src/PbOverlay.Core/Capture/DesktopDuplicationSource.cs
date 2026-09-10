using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace PbOverlay.Core.Capture;

/// <summary>
/// DXGI Desktop Duplication capture. Reads the desktop framebuffer through
/// the compositor — the game process is never opened or hooked. When a
/// target HWND is supplied, only the window's rect is cropped out of the
/// full-desktop frame; when it is <see langword="null"/>, the whole primary
/// output is delivered.
///
/// Suitable as the fallback for anything WGC cannot cover (some full-screen
/// modes, some drivers). Fires <see cref="TargetLost"/> on adapter reset or
/// mode change; the caller can then re-Start.
/// </summary>
public sealed class DesktopDuplicationSource : IFrameSource
{
    private readonly ILogger<DesktopDuplicationSource> _log;
    private readonly int _targetFps;
    private readonly Func<IntPtr?> _hwndProvider;
    private readonly Func<IntPtr, bool> _isForeground;

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIOutputDuplication? _dup;
    private ID3D11Texture2D? _staging;
    private int _outputWidth;
    private int _outputHeight;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public event EventHandler<CapturedFrame>? FrameArrived;
    public event EventHandler? TargetLost;
    public bool IsRunning => _loopTask is { IsCompleted: false };

    public DesktopDuplicationSource(
        ILogger<DesktopDuplicationSource> log,
        int targetFps,
        Func<IntPtr?> hwndProvider,
        Func<IntPtr, bool> isForeground)
    {
        _log = log;
        _targetFps = Math.Clamp(targetFps, 1, 60);
        _hwndProvider = hwndProvider;
        _isForeground = isForeground;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning) return Task.CompletedTask;
        InitDup();
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => LoopAsync(_loopCts.Token), _loopCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _loopCts?.Cancel();
        if (_loopTask is not null)
        {
            try { await _loopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _loopTask = null;
        _loopCts?.Dispose();
        _loopCts = null;
        DisposeDup();
    }

    private void InitDup()
    {
        var featureLevels = new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_1 };
        D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels,
            out _device,
            out _context).CheckError();

        using var dxgiDevice = _device!.QueryInterface<IDXGIDevice>();
        using var adapter = dxgiDevice.GetAdapter();
        adapter.EnumOutputs(0, out var primaryOutput).CheckError();
        using var output = primaryOutput;
        using var output1 = output.QueryInterface<IDXGIOutput1>();

        _dup = output1.DuplicateOutput(_device);
        _outputWidth = output.Description.DesktopCoordinates.Right - output.Description.DesktopCoordinates.Left;
        _outputHeight = output.Description.DesktopCoordinates.Bottom - output.Description.DesktopCoordinates.Top;

        _staging = _device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)_outputWidth,
            Height = (uint)_outputHeight,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        });
    }

    private void DisposeDup()
    {
        _staging?.Dispose(); _staging = null;
        _dup?.Dispose(); _dup = null;
        _context?.Dispose(); _context = null;
        _device?.Dispose(); _device = null;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _targetFps);
        var sw = Stopwatch.StartNew();
        var nextTick = sw.Elapsed;

        while (!ct.IsCancellationRequested)
        {
            // Idle when the target window is not foreground — anti-cheat and
            // CPU-friendliness both benefit.
            var hwnd = _hwndProvider();
            if (hwnd is null || !_isForeground(hwnd.Value))
            {
                await Task.Delay(200, ct).ConfigureAwait(false);
                nextTick = sw.Elapsed;
                continue;
            }

            try
            {
                if (!TryGrabFrame(hwnd.Value))
                {
                    await Task.Delay(50, ct).ConfigureAwait(false);
                    continue;
                }
            }
            catch (SharpGenException ex) when (ex.ResultCode == Vortice.DXGI.ResultCode.AccessLost)
            {
                _log.LogWarning("DXGI duplication access lost; re-initialising.");
                DisposeDup();
                try { InitDup(); }
                catch (Exception initEx)
                {
                    _log.LogError(initEx, "Failed to re-init DXGI duplication.");
                    TargetLost?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }

            nextTick += frameInterval;
            var delay = nextTick - sw.Elapsed;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, ct).ConfigureAwait(false);
            else
                nextTick = sw.Elapsed;
        }
    }

    private bool TryGrabFrame(IntPtr hwnd)
    {
        if (_dup is null || _staging is null || _context is null) return false;

        var result = _dup.AcquireNextFrame(50, out var _, out var desktopResource);
        if (result.Failure)
        {
            if (result.Code == Vortice.DXGI.ResultCode.WaitTimeout.Code) return false;
            throw new SharpGenException(result);
        }

        try
        {
            using var srcTex = desktopResource!.QueryInterface<ID3D11Texture2D>();
            _context.CopyResource(_staging, srcTex);
        }
        finally
        {
            _dup.ReleaseFrame();
            desktopResource?.Dispose();
        }

        var rect = GetClientRectOnDesktop(hwnd);
        if (rect.IsEmpty) return false;

        // Clamp rect to the output bounds.
        rect.Intersect(new System.Drawing.Rectangle(0, 0, _outputWidth, _outputHeight));
        if (rect.Width <= 0 || rect.Height <= 0) return false;

        // MapFlags is ambiguous between DXGI and D3D11; the default is MapFlags.None anyway.
        var box = _context.Map(_staging, 0, MapMode.Read);
        try
        {
            // Copy just the cropped rect out into an OpenCV Mat.
            var mat = new Mat(rect.Height, rect.Width, MatType.CV_8UC4);
            var rowBytes = rect.Width * 4;
            for (var y = 0; y < rect.Height; y++)
            {
                var src = IntPtr.Add(box.DataPointer, (rect.Y + y) * (int)box.RowPitch + rect.X * 4);
                var dst = mat.Ptr(y);
                CopyMemory(dst, src, (uint)rowBytes);
            }

            FrameArrived?.Invoke(this, new CapturedFrame(mat, rect.Width, rect.Height, Stopwatch.GetTimestamp()));
            return true;
        }
        finally
        {
            _context.Unmap(_staging, 0);
        }
    }

    private static System.Drawing.Rectangle GetClientRectOnDesktop(IntPtr hwnd)
    {
        if (!GetClientRect(hwnd, out var client)) return System.Drawing.Rectangle.Empty;
        var topLeft = new POINT { X = 0, Y = 0 };
        if (!ClientToScreen(hwnd, ref topLeft)) return System.Drawing.Rectangle.Empty;
        return new System.Drawing.Rectangle(topLeft.X, topLeft.Y, client.Right - client.Left, client.Bottom - client.Top);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    #region P/Invoke
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
    private static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);
    #endregion
}
