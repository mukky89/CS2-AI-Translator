namespace CS2AITranslator.Core;

public sealed record AudioChunk(
    byte[] Data,
    int SampleRate,
    int BitsPerSample,
    int Channels,
    bool IsIeeeFloat,
    DateTimeOffset CapturedAt);

public sealed record SpeechResult(string Text, string Language, TimeSpan ProcessingTime);
public sealed record TranslationResult(string OriginalText, string TranslatedText, string SourceLanguage, string TargetLanguage, TimeSpan ProcessingTime);

public interface IAudioCaptureService : IAsyncDisposable
{
    event Func<AudioChunk, Task>? ChunkReady;
    bool IsRunning { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface ISpeechToTextService
{
    Task<SpeechResult> TranscribeAsync(AudioChunk chunk, CancellationToken cancellationToken = default);
}

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default);
}
