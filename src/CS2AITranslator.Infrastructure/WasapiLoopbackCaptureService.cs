using CS2AITranslator.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CS2AITranslator.Infrastructure;

public sealed class WasapiLoopbackCaptureService : IAudioCaptureService
{
    private readonly TimeSpan _chunkDuration;
    private readonly object _gate = new();
    private WasapiRecorder? _recorder;
    private MemoryStream _buffer = new();

    public WasapiLoopbackCaptureService(TimeSpan? chunkDuration = null)
        => _chunkDuration = chunkDuration ?? TimeSpan.FromSeconds(1.5);

    public event Func<AudioChunk, Task>? ChunkReady;
    public bool IsRunning => _recorder is not null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_recorder is not null) return Task.CompletedTask;

        _recorder = new WasapiRecorderBuilder()
            .WithLoopbackCapture()
            .WithBufferLength(40)
            .Build();
        _recorder.DataAvailable += OnDataAvailable;
        _recorder.RecordingStopped += OnRecordingStopped;
        _recorder.StartRecording();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _recorder?.StopRecording();
        return Task.CompletedTask;
    }

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        var recorder = _recorder;
        if (recorder is null || buffer.IsEmpty || (flags & AudioClientBufferFlags.Silent) != 0) return;

        byte[]? completed = null;
        lock (_gate)
        {
            _buffer.Write(buffer);
            var targetBytes = recorder.WaveFormat.AverageBytesPerSecond * _chunkDuration.TotalSeconds;
            if (_buffer.Length >= targetBytes)
            {
                completed = _buffer.ToArray();
                _buffer.Dispose();
                _buffer = new MemoryStream();
            }
        }

        if (completed is null) return;

        var format = recorder.WaveFormat;
        var chunk = new AudioChunk(
            completed,
            format.SampleRate,
            format.BitsPerSample,
            format.Channels,
            format.Encoding == WaveFormatEncoding.IeeeFloat,
            DateTimeOffset.UtcNow);

        var handler = ChunkReady;
        if (handler is not null) _ = Task.Run(() => handler(chunk));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        var recorder = _recorder;
        if (recorder is null) return;
        recorder.DataAvailable -= OnDataAvailable;
        recorder.RecordingStopped -= OnRecordingStopped;
        recorder.Dispose();
        _recorder = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _buffer.Dispose();
    }
}
