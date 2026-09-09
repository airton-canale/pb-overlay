using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace PbOverlay.App.Tabs;

public partial class OverlayAppearanceTab : UserControl
{
    public OverlayAppearanceTab()
    {
        InitializeComponent();
        Loaded += (_, _) => Load();
    }

    private void Load()
    {
        var o = AppServices.Config.Overlay;
        PositionXBox.Text = o.PositionX.ToString(CultureInfo.InvariantCulture);
        PositionYBox.Text = o.PositionY.ToString(CultureInfo.InvariantCulture);
        FontSizeSlider.Value = o.FontSize;
        TextColorBox.Text = o.TextColor;
        OutlineColorBox.Text = o.OutlineColor;
        OutlineThicknessSlider.Value = o.OutlineThickness;
        LockedCheck.IsChecked = o.Locked;
        HotkeyBox.Text = o.ToggleHotkey;
    }

    private void SaveApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var o = AppServices.Config.Overlay;

        if (!int.TryParse(PositionXBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var px))
        { ValidationText.Text = "Position X must be an integer."; return; }
        if (!int.TryParse(PositionYBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var py))
        { ValidationText.Text = "Position Y must be an integer."; return; }
        if (!IsHexArgb(TextColorBox.Text))
        { ValidationText.Text = "Text color must be #AARRGGBB hex."; return; }
        if (!IsHexArgb(OutlineColorBox.Text))
        { ValidationText.Text = "Outline color must be #AARRGGBB hex."; return; }

        ValidationText.Text = string.Empty;

        o.PositionX = px;
        o.PositionY = py;
        o.FontSize = FontSizeSlider.Value;
        o.TextColor = TextColorBox.Text;
        o.OutlineColor = OutlineColorBox.Text;
        o.OutlineThickness = OutlineThicknessSlider.Value;
        o.Locked = LockedCheck.IsChecked == true;

        AppServices.CurrentOverlay?.ApplyConfig(o);
    }

    private static bool IsHexArgb(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.StartsWith('#') ? s[1..] : s;
        if (t.Length != 8) return false;
        return uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }
}
