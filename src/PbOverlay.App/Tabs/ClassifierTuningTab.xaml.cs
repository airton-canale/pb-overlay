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

    private SaturationStateClassifier? _classifier;
    private ClassifierPipeline? _pipeline;
    private int _pipelineSlotCount = -1;
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
        AppServices.Config.Classifier.AliveDeadSaturationThreshold = e.NewValue;
        SatValueText.Text = ((int)e.NewValue).ToString();
        InvalidatePipeline();
    }

    private void VarSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        AppServices.Config.Classifier.EmptyVarianceThreshold = e.NewValue;
        VarValueText.Text = ((int)e.NewValue).ToString();
        InvalidatePipeline();
    }

    private void SmoothSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        AppServices.Config.Classifier.TemporalSmoothingFrames = (int)e.NewValue;
        SmoothValueText.Text = ((int)e.NewValue).ToString();
        InvalidatePipeline();
    }

    private void InvalidatePipeline()
    {
        _pipeline = null;
        _classifier = null;
        _pipelineSlotCount = -1;
    }

    private void EnsurePipeline(int slotsPerTeam)
    {
        if (_pipeline is not null && _pipelineSlotCount == slotsPerTeam) return;
        var c = AppServices.Config.Classifier;
        _classifier = new SaturationStateClassifier(
            new LowVarianceEmptyDetector(c.EmptyVarianceThreshold),
            c.AliveDeadSaturationThreshold);
        int frames = Math.Max(1, c.TemporalSmoothingFrames);
        var sA = new TemporalSmoother(slotsPerTeam, frames);
        var sB = new TemporalSmoother(slotsPerTeam, frames);
        _pipeline = new ClassifierPipeline(_classifier, sA, sB);
        _pipelineSlotCount = slotsPerTeam;
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
                    EnsurePipeline(profile.SlotsPerTeam);
                    var (aStates, bStates, aCount, bCount) = _pipeline!.Process(clone, aRects, bRects);
                    for (int i = 0; i < aStates.Count && i < aRects.Count; i++)
                        DrawSlot(clone, aRects[i], aStates[i]);
                    for (int i = 0; i < bStates.Count && i < bRects.Count; i++)
                        DrawSlot(clone, bRects[i], bStates[i]);
                    aAlive = aCount;
                    bAlive = bCount;
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
        if (_classifier is null) return;
        var obs = _classifier.LastObservations;
        if (obs.Count == 0) return;
        var threshold = SaturationStateClassifier.AutoCalibrateThreshold(obs.Select(o => o.MeanSaturation));
        AppServices.Config.Classifier.AliveDeadSaturationThreshold = threshold;
        SatSlider.Value = threshold;
    }
}
