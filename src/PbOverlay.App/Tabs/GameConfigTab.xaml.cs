using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PbOverlay.Core.GameConfig;

namespace PbOverlay.App.Tabs;

public partial class GameConfigTab : UserControl
{
    public GameConfigTab()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true) PathBox.Text = dlg.FileName;
    }

    private void ReadButton_Click(object sender, RoutedEventArgs e)
    {
        var path = PathBox.Text;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var reader = new AutoDetectGameConfigReader(
                AppServices.LoggerFactory.CreateLogger<AutoDetectGameConfigReader>());
            var result = reader.Read(path);

            FilePathRun.Text = result.FilePath;
            LastModifiedRun.Text = result.LastModifiedUtc.ToString("u");
            DetectedFormatRun.Text = result.DetectedFormat.ToString();
            HexBox.Text = result.First256BytesHex;
            EntriesList.ItemsSource = result.Entries;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to read: {ex.Message}");
        }
    }
}
