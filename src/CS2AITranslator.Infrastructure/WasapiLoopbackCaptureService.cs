using CS2AITranslator.Core;
using NAudio.Wave;

namespace CS2AITranslator.Infrastructure;

public sealed class WasapiLoopbackCaptureService : IAudioCaptureService
{
    private readonly TimeSpan _chunkDuration;
    private readonly object _gate = new();
    private WasapiLoopbackCapture? _capture;
    private MemoryStream _buffer = new();

    public WasapiLoopbackCaptureService(TimeSpan? chunkDuration = null)
        => _chunkDuration = chunkDuration ?? TimeSpan.FromSeconds(3);

    public event Func<AudioChunk, Task>? ChunkReady;
    public bool IsRunning => _capture is not null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_capture is not null) return Task.CompletedTask;

        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _capture?.StopRecording();
        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _capture;
        if (capture is null || e.BytesRecorded == 0) return;

        byte[]? completed = null;
        lock (_gate)
        {
            _buffer.Write(e.Buffer, 0, e.BytesRecorded);
            var targetBytes = capture.WaveFormat.AverageBytesPerSecond * _chunkDuration.TotalSeconds;
            if (_buffer.Length >= targetBytes)
            {
                completed = _buffer.ToArray();
                _buffer.Dispose();
                _buffer = new MemoryStream();
            }
        }

        if (completed is null) return;

        var format = capture.WaveFormat;
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
        if (_capture is null) return;
        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _capture = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _buffer.Dispose();
    }
}
