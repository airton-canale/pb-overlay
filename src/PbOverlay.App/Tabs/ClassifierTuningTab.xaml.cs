using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using PbOverlay.Core.Capture;
using PbOverlay.Core.Classify;
using PbOverlay.Core.Roi;

namespace PbOverlay.App.Tabs;

public partial class ClassifierTuningTab : UserControl
{
    private WriteableBitmap? _bitmap;

    // TODO: swap to SaturationStateClassifier once fully wired in Core.
    private readonly ClassifierPipeline _pipeline = new(new PlaceholderClassifier());
    private int _busy;

    public ClassifierTuningTab()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var c = AppServices.Config.Classifier;
        SatSlider.Value = c.AliveDeadSaturationThreshold;
        VarSlider.Value = c.EmptyVarianceThreshold;
        SmoothSlider.Value = c.TemporalSmoothingFrames;
        SatValueText.Text = ((int)SatSlider.Value).ToString();
        VarValueText.Text = ((int)VarSlider.Value).ToString();
        SmoothValueText.Text = ((int)SmoothSlider.Value).ToString();

        AppServices.FrameArrived += OnFrame;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AppServices.FrameArrived -= OnFrame;
    }

    private void SatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        AppServices.Config.Classifier.AliveDeadSaturationThreshold = (int)e.NewValue;
        SatValueText.Text = ((int)e.NewValue).ToString();
    }

    private void VarSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        AppServices.Config.Classifier.EmptyVarianceThreshold = (int)e.NewValue;
        VarValueText.Text = ((int)e.NewValue).ToString();
    }

    private void SmoothSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        AppServices.Config.Classifier.TemporalSmoothingFrames = (int)e.NewValue;
        SmoothValueText.Text = ((int)e.NewValue).ToString();
    }

    private void OnFrame(CapturedFrame frame)
    {
        // Drop frames while we're still drawing the last one — the preview is
        // for tuning, not archival.
        if (System.Threading.Interlocked.Exchange(ref _busy, 1) == 1) return;

        var clone = frame.Bgra.Clone();
        int w = frame.Width, h = frame.Height;

        _ = Task.Run(() =>
        {
            try
            {
                var profile = SelectedProfile();
                int aAlive = 0, bAlive = 0;
                if (profile is not null && profile.IsCalibrated)
                {
                    var (aRects, bRects) = RoiMapper.SliceProfile(profile, w, h);
                    var aStates = _pipeline.Classify(clone, aRects);
                    var bStates = _pipeline.Classify(clone, bRects);
                    for (int i = 0; i < aStates.Count; i++)
                        DrawSlot(clone, aRects[i], aStates[i]);
                    for (int i = 0; i < bStates.Count; i++)
                        DrawSlot(clone, bRects[i], bStates[i]);
                    aAlive = aStates.Count(s => s == SlotState.Alive);
                    bAlive = bStates.Count(s => s == SlotState.Alive);
                }

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_bitmap is null || _bitmap.PixelWidth != w || _bitmap.PixelHeight != h)
                        {
                            _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                            PreviewImage.Source = _bitmap;
                        }
                        WriteableBitmapConverter.ToWriteableBitmap(clone, _bitmap);
                        TeamAText.Text = aAlive.ToString();
                        TeamBText.Text = bAlive.ToString();
                    }
                    finally
                    {
                        clone.Dispose();
                    }
                }));
            }
            catch
            {
                clone.Dispose();
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _busy, 0);
            }
        });
    }

    private static void DrawSlot(Mat mat, OpenCvSharp.Rect r, SlotState state)
    {
        var color = state switch
        {
            SlotState.Alive => new Scalar(0, 255, 0, 255),
            SlotState.Dead  => new Scalar(0, 0, 255, 255),
            _               => new Scalar(128, 128, 128, 255),
        };
        Cv2.Rectangle(mat, r, color, 2);
        Cv2.PutText(mat, state.ToString(), new OpenCvSharp.Point(r.X + 2, r.Y + 14),
            HersheyFonts.HersheySimplex, 0.4, color, 1);
    }

    private static RoiProfile? SelectedProfile()
    {
        var profiles = AppServices.Config.RoiProfiles;
        return profiles.Count > 0 ? profiles[0] : null;
    }

    private void AutoCalibrateButton_Click(object sender, RoutedEventArgs e)
    {
        var samples = _pipeline.LastObservations;
        if (samples is null || samples.Count == 0) return;
        var threshold = SaturationStateClassifier.AutoCalibrateThreshold(samples);
        AppServices.Config.Classifier.AliveDeadSaturationThreshold = threshold;
        SatSlider.Value = threshold;
    }
}
