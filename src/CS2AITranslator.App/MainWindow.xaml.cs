using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CS2AITranslator.Core;
using CS2AITranslator.Infrastructure;
using Whisper.net.Ggml;

namespace CS2AITranslator.App;

public partial class MainWindow : Window
{
    private readonly WhisperModelManager _modelManager = new();
    private readonly SemaphoreSlim _pipelineGate = new(1, 1);
    private IAudioCaptureService? _capture;
    private WhisperSpeechToTextService? _speech;
    private ITranslationService _translation;
    private OverlayWindow? _overlay;
    private bool _running;
    private string _targetLanguage = "sk";

    public MainWindow()
    {
        InitializeComponent();
        var deepLKey = Environment.GetEnvironmentVariable("DEEPL_API_KEY");
        if (!string.IsNullOrWhiteSpace(deepLKey))
        {
            _translation = new DeepLTranslationService(deepLKey);
            ProviderText.Text = "DeepL API + CS2 terminology";
        }
        else
        {
            _translation = new BasicTranslationService();
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StartButton.IsEnabled = false;
            if (_running)
            {
                await StopTranslatorAsync();
                return;
            }

            _targetLanguage = ((ComboBoxItem)TargetLanguageBox.SelectedItem).Tag?.ToString() ?? "sk";
            var modelType = ParseModelType(((ComboBoxItem)ModelTypeBox.SelectedItem).Tag?.ToString());
            StatusText.Text = "Preparing local Whisper model…";
            ModelStatusText.Text = "checking…";

            var progress = new Progress<double>(value =>
                ModelStatusText.Text = value >= 1 ? "ready" : $"downloading {value:P0}");
            var modelPath = await _modelManager.EnsureModelAsync(modelType, progress);
            _speech?.Dispose();
            _speech = new WhisperSpeechToTextService(modelPath);
            ModelStatusText.Text = "ready";

            var captureMode = ((ComboBoxItem)CaptureModeBox.SelectedItem).Tag?.ToString() ?? "cs2";
            _capture = captureMode == "system"
                ? new WasapiLoopbackCaptureService(TimeSpan.FromSeconds(1.5))
                : new Cs2ProcessLoopbackCaptureService(TimeSpan.FromSeconds(1.2));
            _capture.ChunkReady += ProcessChunkAsync;

            try
            {
                await _capture.StartAsync();
                StatusText.Text = captureMode == "cs2"
                    ? "Listening to CS2 audio only…"
                    : "Listening to Windows output audio…";
            }
            catch when (captureMode == "cs2")
            {
                await _capture.DisposeAsync();
                _capture = new WasapiLoopbackCaptureService(TimeSpan.FromSeconds(1.5));
                _capture.ChunkReady += ProcessChunkAsync;
                await _capture.StartAsync();
                StatusText.Text = "CS2 process capture unavailable — using Windows output fallback.";
            }

            _running = true;
            StartButton.Content = "Stop translator";
        }
        catch (Exception ex)
        {
            await StopTranslatorAsync();
            MessageBox.Show(ex.Message, "CS2 AI Translator", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StartButton.IsEnabled = true;
        }
    }

    private async Task StopTranslatorAsync()
    {
        if (_capture is not null)
        {
            _capture.ChunkReady -= ProcessChunkAsync;
            await _capture.DisposeAsync();
            _capture = null;
        }
        _running = false;
        StartButton.Content = "Start translator";
        StatusText.Text = "Stopped";
    }

    private async Task ProcessChunkAsync(AudioChunk chunk)
    {
        if (_speech is null || !await _pipelineGate.WaitAsync(0)) return;
        try
        {
            var total = Stopwatch.StartNew();
            var speech = await _speech.TranscribeAsync(chunk);
            if (string.IsNullOrWhiteSpace(speech.Text)) return;

            var translated = await _translation.TranslateAsync(speech.Text, speech.Language, _targetLanguage);
            total.Stop();

            await Dispatcher.InvokeAsync(() =>
            {
                OriginalText.Text = $"[{translated.SourceLanguage}] {speech.Text}";
                TranslatedText.Text = translated.TranslatedText;
                LatencyText.Text = $"STT {speech.ProcessingTime.TotalMilliseconds:0} ms · translation {translated.ProcessingTime.TotalMilliseconds:0} ms · total {total.Elapsed.TotalMilliseconds:0} ms";
                _overlay?.SetTranslation(translated.SourceLanguage, speech.Text, translated.TranslatedText, total.Elapsed);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = $"Pipeline error: {ex.Message}");
        }
        finally
        {
            _pipelineGate.Release();
        }
    }

    private static GgmlType ParseModelType(string? value) => value switch
    {
        "Tiny" => GgmlType.Tiny,
        "Small" => GgmlType.Small,
        _ => GgmlType.Base
    };

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
        await StopTranslatorAsync();
        _speech?.Dispose();
        if (_translation is IDisposable disposableTranslation) disposableTranslation.Dispose();
        _pipelineGate.Dispose();
        _overlay?.Close();
        base.OnClosed(e);
    }
}
