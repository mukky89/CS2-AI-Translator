using System.Windows;

namespace CS2AITranslator.App;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Left = 40;
        Top = 80;
    }

    public void SetTranslation(string language, string original, string translated, TimeSpan latency)
    {
        OverlayOriginal.Text = $"[{language}] {original}";
        OverlayTranslation.Text = translated;
        OverlayLatency.Text = $"{latency.TotalMilliseconds:0} ms";
    }
}
