using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CS2AITranslator.Core;
using CS2AITranslator.Infrastructure;

namespace CS2AITranslator.App;

public partial class MainWindow : Window
{
    private IAudioCaptureService? _capture;
    private ISpeechToTextService? _speech;
    private readonly ITranslationService _translation = new BasicTranslationService();
    private OverlayWindow? _overlay;
    private bool _running;

    public MainWindow() => InitializeComponent();

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_running)
            {
                if (_capture is not null) await _capture.StopAsync();
                _running = false;
                StartButton.Content = "Start translator";
                StatusText.Text = "Stopped";
                return;
            }

            var modelPath = Path.GetFullPath(ModelPathBox.Text.Trim());
            _speech = new WhisperSpeechToTextService(modelPath);
            _capture = new WasapiLoopbackCaptureService(TimeSpan.FromSeconds(3));
            _capture.ChunkReady += ProcessChunkAsync;
            await _capture.StartAsync();

            _running = true;
            StartButton.Content = "Stop translator";
            StatusText.Text = "Listening to Windows output audio…";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "CS2 AI Translator", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ProcessChunkAsync(AudioChunk chunk)
    {
        if (_speech is null) return;
        try
        {
            var total = Stopwatch.StartNew();
            var speech = await _speech.TranscribeAsync(chunk);
            if (string.IsNullOrWhiteSpace(speech.Text)) return;

            var target = ((ComboBoxItem)TargetLanguageBox.SelectedItem).Tag?.ToString() ?? "sk";
            var translated = await _translation.TranslateAsync(speech.Text, speech.Language, target);
            total.Stop();

            await Dispatcher.InvokeAsync(() =>
            {
                OriginalText.Text = $"[{speech.Language}] {speech.Text}";
                TranslatedText.Text = translated.TranslatedText;
                LatencyText.Text = $"STT {speech.ProcessingTime.TotalMilliseconds:0} ms · total {total.Elapsed.TotalMilliseconds:0} ms";
                _overlay?.SetTranslation(speech.Language, speech.Text, translated.TranslatedText, total.Elapsed);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = $"Error: {ex.Message}");
        }
    }

    private void OverlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay is { IsVisible: true })
        {
            _overlay.Hide();
            OverlayButton.Content = "Show overlay";
            return;
        }

        _overlay ??= new OverlayWindow();
        _overlay.Show();
        OverlayButton.Content = "Hide overlay";
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_capture is not null) await _capture.DisposeAsync();
        _overlay?.Close();
        base.OnClosed(e);
    }
}
