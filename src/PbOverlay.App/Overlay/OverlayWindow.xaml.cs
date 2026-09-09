using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PbOverlay.Core.Config;
using PbOverlay.Core.Fps;

namespace PbOverlay.App.Overlay;

public partial class OverlayWindow : Window
{
    private OverlayConfig _cfg;

    public OverlayWindow(OverlayConfig cfg)
    {
        _cfg = cfg;
        InitializeComponent();

        SourceInitialized += (_, _) => ApplyClickThrough();
        Loaded += (_, _) => ApplyConfigInternal(applyClickThrough: false);

        MouseLeftButtonDown += (_, _) =>
        {
            if (!_cfg.Locked)
            {
                DragMove();
            }
        };
    }

    public void ApplyConfig(OverlayConfig cfg)
    {
        _cfg = cfg;
        ApplyConfigInternal(applyClickThrough: true);
    }

    public void SetCount(int aliveA, int aliveB)
    {
        Dispatcher.Invoke(() => CountText.Text = $"{aliveA} v {aliveB}");
    }

    public void SetFps(FpsSample? s)
    {
        Dispatcher.Invoke(() =>
        {
            if (s is null)
            {
                FpsText.Text = "n/a";
                return;
            }

            string text = $"{s.AvgFps:F0} fps · {s.OnePercentLowFps:F0} low";
            if (s.FrameTimeMs is double ft)
            {
                text += $" · {ft:F1} ms";
            }
            FpsText.Text = text;
        });
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    private void ApplyConfigInternal(bool applyClickThrough)
    {
        // Font size.
        CountText.FontSize = _cfg.FontSize;
        FpsText.FontSize = Math.Max(8.0, _cfg.FontSize * 0.5);

        // Colors (hex ARGB). Fall back to white/black on invalid input rather than
        // throwing — a bad string in config.json must not brick the overlay.
        var textColor = TryParseColor(_cfg.TextColor, Colors.White);
        var outlineColor = TryParseColor(_cfg.OutlineColor, Colors.Black);
        var textBrush = new SolidColorBrush(textColor);
        CountText.Foreground = textBrush;
        FpsText.Foreground = textBrush;

        // Outline via DropShadowEffect with BlurRadius matching thickness.
        double blur = Math.Max(0.0, _cfg.OutlineThickness);
        CountText.Effect = new DropShadowEffect
        {
            ShadowDepth = 0,
            BlurRadius = blur,
            Color = outlineColor,
            Opacity = 1.0,
        };
        FpsText.Effect = new DropShadowEffect
        {
            ShadowDepth = 0,
            BlurRadius = blur,
            Color = outlineColor,
            Opacity = 1.0,
        };

        // Position: auto top-right if either coord is negative.
        if (_cfg.PositionX < 0 || _cfg.PositionY < 0)
        {
            var wa = SystemParameters.WorkArea;
            // Prefer using ActualWidth/Height; fall back to a small margin if unmeasured yet.
            double w = ActualWidth > 0 ? ActualWidth : 0;
            Left = wa.Right - w - 16;
            Top = wa.Top + 16;
        }
        else
        {
            Left = _cfg.PositionX;
            Top = _cfg.PositionY;
        }

        if (applyClickThrough)
        {
            ApplyClickThrough();
        }
    }

    private void ApplyClickThrough()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        if (_cfg.Locked)
        {
            NativeMethods.MakeClickThrough(hwnd);
        }
        else
        {
            NativeMethods.MakeInteractive(hwnd);
        }
    }

    private static Color TryParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { return fallback; }
        catch (InvalidOperationException) { return fallback; }
    }
}
