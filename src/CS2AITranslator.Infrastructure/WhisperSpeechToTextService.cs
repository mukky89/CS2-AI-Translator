using System.Diagnostics;
using System.Text;
using CS2AITranslator.Core;
using NAudio.Wave;
using Whisper.net;

namespace CS2AITranslator.Infrastructure;

public sealed class WhisperSpeechToTextService : ISpeechToTextService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly WhisperFactory _factory;
    private readonly WhisperProcessor _processor;
    private bool _disposed;

    public WhisperSpeechToTextService(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Whisper model was not found.", modelPath);

        _factory = WhisperFactory.FromPath(modelPath);
        _processor = _factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();
    }

    public async Task<SpeechResult> TranscribeAsync(AudioChunk chunk, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();

        try
        {
            await using var wav = BuildWhisperWav(chunk);
            var text = new StringBuilder();
            await foreach (var segment in _processor.ProcessAsync(wav, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                    text.Append(segment.Text.Trim()).Append(' ');
            }

            sw.Stop();
            return new SpeechResult(text.ToString().Trim(), "auto", sw.Elapsed);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static MemoryStream BuildWhisperWav(AudioChunk chunk)
    {
        var sourceFormat = chunk.IsIeeeFloat
            ? WaveFormat.CreateIeeeFloatWaveFormat(chunk.SampleRate, chunk.Channels)
            : new WaveFormat(chunk.SampleRate, chunk.BitsPerSample, chunk.Channels);

        using var raw = new RawSourceWaveStream(new MemoryStream(chunk.Data, writable: false), sourceFormat);
        using var output = new MemoryStream();

        if (chunk.SampleRate == 16000 && chunk.BitsPerSample == 16 && chunk.Channels == 1 && !chunk.IsIeeeFloat)
        {
            using (var writer = new WaveFileWriter(output, sourceFormat))
            {
                raw.CopyTo(writer);
            }
        }
        else
        {
            using var resampler = new MediaFoundationResampler(raw, new WaveFormat(16000, 16, 1)) { ResamplerQuality = 60 };
            WaveFileWriter.WriteWavFileToStream(output, resampler);
        }

        return new MemoryStream(output.ToArray(), writable: false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _processor.Dispose();
        _factory.Dispose();
        _gate.Dispose();
    }
}
