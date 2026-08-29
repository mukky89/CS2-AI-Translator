using System.Diagnostics;
using CS2AITranslator.Core;
using NAudio.Wave;
using Whisper.net;

namespace CS2AITranslator.Infrastructure;

public sealed class WhisperSpeechToTextService : ISpeechToTextService
{
    private readonly string _modelPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public WhisperSpeechToTextService(string modelPath) => _modelPath = modelPath;

    public async Task<SpeechResult> TranscribeAsync(AudioChunk chunk, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_modelPath))
            throw new FileNotFoundException("Whisper model was not found. Put a ggml model in the models folder.", _modelPath);

        await _gate.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        var tempWav = Path.Combine(Path.GetTempPath(), $"cs2ai-{Guid.NewGuid():N}.wav");

        try
        {
            var sourceFormat = chunk.IsIeeeFloat
                ? WaveFormat.CreateIeeeFloatWaveFormat(chunk.SampleRate, chunk.Channels)
                : new WaveFormat(chunk.SampleRate, chunk.BitsPerSample, chunk.Channels);

            using (var raw = new RawSourceWaveStream(new MemoryStream(chunk.Data, writable: false), sourceFormat))
            using (var resampler = new MediaFoundationResampler(raw, new WaveFormat(16000, 16, 1)) { ResamplerQuality = 60 })
            {
                WaveFileWriter.CreateWaveFile(tempWav, resampler);
            }

            using var factory = WhisperFactory.FromPath(_modelPath);
            using var processor = factory.CreateBuilder().WithLanguage("auto").Build();
            await using var audio = File.OpenRead(tempWav);

            var text = new System.Text.StringBuilder();
            await foreach (var segment in processor.ProcessAsync(audio, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                    text.Append(segment.Text.Trim()).Append(' ');
            }

            sw.Stop();
            return new SpeechResult(text.ToString().Trim(), "auto", sw.Elapsed);
        }
        finally
        {
            if (File.Exists(tempWav)) File.Delete(tempWav);
            _gate.Release();
        }
    }
}
