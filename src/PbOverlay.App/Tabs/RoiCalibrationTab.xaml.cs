using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using PbOverlay.Core.Capture;
using PbOverlay.Core.Roi;
// OpenCvSharp defines Point/Rect/Window too; WPF wins here, OpenCV ones stay fully qualified.
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Window = System.Windows.Window;

namespace PbOverlay.App.Tabs;

/// <summary>
/// Draw-by-drag calibration: live capture feed with three overlaid rects
/// (Bar / Team A / Team B), each with four corner thumbs.
/// </summary>
public partial class RoiCalibrationTab : UserControl
{
    private WriteableBitmap? _bitmap;
    private int _frameWidth;
    private int _frameHeight;
    private bool _frozen;

    private RoiRectHandle? _bar;
    private RoiRectHandle? _teamA;
    private RoiRectHandle? _teamB;

    public RoiCalibrationTab()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProfilesList.ItemsSource = AppServices.Config.RoiProfiles;
        if (AppServices.Config.RoiProfiles.Count > 0)
            ProfilesList.SelectedIndex = 0;
        AppServices.FrameArrived += OnFrame;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AppServices.FrameArrived -= OnFrame;
    }

    private void OnFrame(CapturedFrame frame)
    {
        if (_frozen) return;
        // Producer owns the Mat; clone before hopping to the UI thread.
        var clone = frame.Bgra.Clone();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
                {
                    _bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null);
                    PreviewImage.Source = _bitmap;
                    _frameWidth = frame.Width;
                    _frameHeight = frame.Height;
                    EnsureRects();
                }
                WriteableBitmapConverter.ToWriteableBitmap(clone, _bitmap);
            }
            finally
            {
                clone.Dispose();
            }
        }));
    }

    private void EnsureRects()
    {
        if (_bar is not null) return;
        // Default seed rects — user drags them into place.
        _bar   = new RoiRectHandle(RoiCanvas, new Rect(40, 20, 400, 60), Colors.Yellow);
        _teamA = new RoiRectHandle(RoiCanvas, new Rect(40, 30, 180, 40), Colors.LimeGreen);
        _teamB = new RoiRectHandle(RoiCanvas, new Rect(260, 30, 180, 40), Colors.OrangeRed);
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not RoiProfile p) return;
        SlotsSlider.Value = p.SlotsPerTeam;
        SlotsValueText.Text = p.SlotsPerTeam.ToString();

        if (_frameWidth > 0 && _frameHeight > 0)
        {
            EnsureRects();
            if (!p.Bar.IsEmpty)   _bar!.SetPixelRect(p.Bar.ToPixel(_frameWidth, _frameHeight));
            if (!p.TeamA.IsEmpty) _teamA!.SetPixelRect(p.TeamA.ToPixel(_frameWidth, _frameHeight));
            if (!p.TeamB.IsEmpty) _teamB!.SetPixelRect(p.TeamB.ToPixel(_frameWidth, _frameHeight));
        }
    }

    private void SlotsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var v = (int)e.NewValue;
        SlotsValueText.Text = v.ToString();
        if (ProfilesList.SelectedItem is RoiProfile p) p.SlotsPerTeam = v;
    }

    private void AddProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var p = new RoiProfile { Name = $"profile-{AppServices.Config.RoiProfiles.Count + 1}" };
        AppServices.Config.RoiProfiles.Add(p);
        ProfilesList.Items.Refresh();
        ProfilesList.SelectedItem = p;
    }

    private void RemoveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not RoiProfile p) return;
        AppServices.Config.RoiProfiles.Remove(p);
        ProfilesList.Items.Refresh();
    }

    private void RenameProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not RoiProfile p) return;
        var dlg = new RenameDialog(p.Name) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            p.Name = dlg.NewName;
            ProfilesList.Items.Refresh();
        }
    }

    private void SaveRoiButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not RoiProfile p) return;
        if (_bar is null || _teamA is null || _teamB is null) return;
        if (_frameWidth <= 0 || _frameHeight <= 0) return;

        p.Bar   = NormalizedRect.FromPixel(_bar.GetPixelRect(),   _frameWidth, _frameHeight);
        p.TeamA = NormalizedRect.FromPixel(_teamA.GetPixelRect(), _frameWidth, _frameHeight);
        p.TeamB = NormalizedRect.FromPixel(_teamB.GetPixelRect(), _frameWidth, _frameHeight);
    }

    private void FreezeFrameButton_Click(object sender, RoutedEventArgs e)
    {
        _frozen = !_frozen;
        FreezeFrameButton.Content = _frozen ? "Unfreeze" : "Freeze Frame";
    }
}

/// <summary>
/// One draggable + corner-resizable rectangle on the calibration canvas.
/// Coordinates are canvas-space pixels; the tab converts to normalized on Save.
/// </summary>
// ponytail: canvas-space == image-space assumed (Image is Stretch=Uniform inside same-size Canvas parent);
// if we ever letterbox, translate cursor coords through Image.TransformToVisual.
internal sealed class RoiRectHandle
{
    private readonly Canvas _canvas;
    private readonly Rectangle _rect;
    private readonly Thumb _tl, _tr, _bl, _br;

    private Point _dragStart;
    private double _startLeft, _startTop;

    public RoiRectHandle(Canvas canvas, Rect initial, Color color)
    {
        _canvas = canvas;
        var brush = new SolidColorBrush(color);
        _rect = new Rectangle
        {
            Stroke = brush,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            Cursor = Cursors.SizeAll,
        };
        _canvas.Children.Add(_rect);
        SetPixelRect(new OpenCvSharp.Rect((int)initial.X, (int)initial.Y, (int)initial.Width, (int)initial.Height));

        _rect.MouseLeftButtonDown += RectDown;
        _rect.MouseMove += RectMove;
        _rect.MouseLeftButtonUp += RectUp;

        _tl = MakeThumb(brush, Cursors.SizeNWSE, HandleDrag_TL);
        _tr = MakeThumb(brush, Cursors.SizeNESW, HandleDrag_TR);
        _bl = MakeThumb(brush, Cursors.SizeNESW, HandleDrag_BL);
        _br = MakeThumb(brush, Cursors.SizeNWSE, HandleDrag_BR);
        PositionThumbs();
    }

    public System.Windows.Rect GetBounds() =>
        new(Canvas.GetLeft(_rect), Canvas.GetTop(_rect), _rect.Width, _rect.Height);

    public OpenCvSharp.Rect GetPixelRect()
    {
        var b = GetBounds();
        return new OpenCvSharp.Rect((int)b.X, (int)b.Y, (int)b.Width, (int)b.Height);
    }

    public void SetPixelRect(OpenCvSharp.Rect r)
    {
        Canvas.SetLeft(_rect, r.X);
        Canvas.SetTop(_rect, r.Y);
        _rect.Width = Math.Max(1, r.Width);
        _rect.Height = Math.Max(1, r.Height);
        PositionThumbs();
    }

    private Thumb MakeThumb(Brush brush, Cursor cursor, DragDeltaEventHandler onDrag)
    {
        var t = new Thumb
        {
            Width = 10, Height = 10, Background = brush, Cursor = cursor,
        };
        t.DragDelta += onDrag;
        _canvas.Children.Add(t);
        return t;
    }

    private void PositionThumbs()
    {
        var l = Canvas.GetLeft(_rect);
        var top = Canvas.GetTop(_rect);
        var r = l + _rect.Width;
        var b = top + _rect.Height;
        Canvas.SetLeft(_tl, l - 5); Canvas.SetTop(_tl, top - 5);
        Canvas.SetLeft(_tr, r - 5); Canvas.SetTop(_tr, top - 5);
        Canvas.SetLeft(_bl, l - 5); Canvas.SetTop(_bl, b - 5);
        Canvas.SetLeft(_br, r - 5); Canvas.SetTop(_br, b - 5);
    }

    private void RectDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(_canvas);
        _startLeft = Canvas.GetLeft(_rect);
        _startTop = Canvas.GetTop(_rect);
        _rect.CaptureMouse();
    }

    private void RectMove(object sender, MouseEventArgs e)
    {
        if (!_rect.IsMouseCaptured) return;
        var pos = e.GetPosition(_canvas);
        Canvas.SetLeft(_rect, _startLeft + (pos.X - _dragStart.X));
        Canvas.SetTop(_rect, _startTop + (pos.Y - _dragStart.Y));
        PositionThumbs();
    }

    private void RectUp(object sender, MouseButtonEventArgs e) => _rect.ReleaseMouseCapture();

    private void HandleDrag_TL(object sender, DragDeltaEventArgs e)
    {
        var l = Canvas.GetLeft(_rect) + e.HorizontalChange;
        var t = Canvas.GetTop(_rect) + e.VerticalChange;
        var w = _rect.Width - e.HorizontalChange;
        var h = _rect.Height - e.VerticalChange;
        if (w < 4 || h < 4) return;
        Canvas.SetLeft(_rect, l); Canvas.SetTop(_rect, t);
        _rect.Width = w; _rect.Height = h;
        PositionThumbs();
    }

    private void HandleDrag_TR(object sender, DragDeltaEventArgs e)
    {
        var t = Canvas.GetTop(_rect) + e.VerticalChange;
        var w = _rect.Width + e.HorizontalChange;
        var h = _rect.Height - e.VerticalChange;
        if (w < 4 || h < 4) return;
        Canvas.SetTop(_rect, t);
        _rect.Width = w; _rect.Height = h;
        PositionThumbs();
    }

    private void HandleDrag_BL(object sender, DragDeltaEventArgs e)
    {
        var l = Canvas.GetLeft(_rect) + e.HorizontalChange;
        var w = _rect.Width - e.HorizontalChange;
        var h = _rect.Height + e.VerticalChange;
        if (w < 4 || h < 4) return;
        Canvas.SetLeft(_rect, l);
        _rect.Width = w; _rect.Height = h;
        PositionThumbs();
    }

    private void HandleDrag_BR(object sender, DragDeltaEventArgs e)
    {
        var w = _rect.Width + e.HorizontalChange;
        var h = _rect.Height + e.VerticalChange;
        if (w < 4 || h < 4) return;
        _rect.Width = w; _rect.Height = h;
        PositionThumbs();
    }
}

/// <summary>Minimal name-prompt dialog used by Rename profile.</summary>
internal sealed class RenameDialog : Window
{
    private readonly TextBox _box;
    public string NewName => _box.Text;

    public RenameDialog(string initial)
    {
        Title = "Rename profile";
        Width = 320; Height = 120;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(12) };
        _box = new TextBox { Text = initial };
        panel.Children.Add(_box);
        var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(0, 12, 0, 0) };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        panel.Children.Add(ok);
        Content = panel;
    }
}
