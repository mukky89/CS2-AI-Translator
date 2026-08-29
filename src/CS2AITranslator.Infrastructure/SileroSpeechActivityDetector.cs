using System.Diagnostics;
using CS2AITranslator.Core;
using NAudio.Wave;
using Whisper.net;

namespace CS2AITranslator.Infrastructure;

public sealed class SileroSpeechActivityDetector : ISpeechActivityDetector, IDisposable
{
    private readonly WhisperVadFactory _factory;
    private readonly WhisperVadProcessor _processor;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public SileroSpeechActivityDetector(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Silero VAD model was not found.", modelPath);

        _factory = WhisperVadFactory.FromPath(modelPath);
        _processor = _factory.CreateBuilder()
            .WithUseGpu(false)
            .WithThreshold(0.50f)
            .WithMinSpeechDuration(TimeSpan.FromMilliseconds(90))
            .WithMinSilenceDuration(TimeSpan.FromMilliseconds(140))
            .WithMaxSpeechDuration(TimeSpan.FromSeconds(4))
            .WithSpeechPadding(TimeSpan.FromMilliseconds(80))
            .Build();
    }

    public async Task<SpeechActivityResult> DetectAsync(AudioChunk chunk, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        var sw = Stopwatch.StartNew();
        try
        {
            var samples = ToMono16KhzFloatSamples(chunk);
            var segments = await Task.Run(() => _processor.DetectSpeech(samples), cancellationToken);
            sw.Stop();
            return new SpeechActivityResult(segments.Count > 0, sw.Elapsed, segments.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static float[] ToMono16KhzFloatSamples(AudioChunk chunk)
    {
        if (chunk.SampleRate == 16000 && chunk.BitsPerSample == 16 && chunk.Channels == 1 && !chunk.IsIeeeFloat)
        {
            var samples = new float[chunk.Data.Length / 2];
            for (var i = 0; i < samples.Length; i++)
            {
                var value = BitConverter.ToInt16(chunk.Data, i * 2);
                samples[i] = value / 32768f;
            }
            return samples;
        }

        var sourceFormat = chunk.IsIeeeFloat
            ? WaveFormat.CreateIeeeFloatWaveFormat(chunk.SampleRate, chunk.Channels)
            : new WaveFormat(chunk.SampleRate, chunk.BitsPerSample, chunk.Channels);

        using var raw = new RawSourceWaveStream(new MemoryStream(chunk.Data, writable: false), sourceFormat);
        using var resampler = new MediaFoundationResampler(raw, new WaveFormat(16000, 16, 1)) { ResamplerQuality = 60 };
        using var converted = new MemoryStream();
        var buffer = new byte[32 * 1024];
        int read;
        while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
            converted.Write(buffer, 0, read);

        var bytes = converted.ToArray();
        var result = new float[bytes.Length / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
        return result;
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
