using System.Diagnostics;
using CS2AITranslator.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CS2AITranslator.Infrastructure;

public sealed class Cs2ProcessLoopbackCaptureService : IAudioCaptureService
{
    private readonly TimeSpan _chunkDuration;
    private readonly object _gate = new();
    private WasapiRecorder? _recorder;
    private MemoryStream _buffer = new();

    public Cs2ProcessLoopbackCaptureService(TimeSpan? chunkDuration = null)
        => _chunkDuration = chunkDuration ?? TimeSpan.FromSeconds(1.2);

    public event Func<AudioChunk, Task>? ChunkReady;
    public bool IsRunning => _recorder is not null;
    public int? ProcessId { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_recorder is not null) return;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            throw new PlatformNotSupportedException("Per-process loopback requires Windows 10 build 19041 or newer.");

        var process = Process.GetProcessesByName("cs2")
            .OrderByDescending(p => p.StartTime)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("CS2 is not running. Start Counter-Strike 2 first.");

        ProcessId = process.Id;
        _recorder = await Task.Run(() => new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)process.Id, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(new WaveFormat(16000, 16, 1))
            .WithBufferLength(40)
            .BuildAsync(), cancellationToken);

        _recorder.DataAvailable += OnDataAvailable;
        _recorder.RecordingStopped += OnRecordingStopped;
        _recorder.StartRecording();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _recorder?.StopRecording();
        return Task.CompletedTask;
    }

    private void OnDataAvailable(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        if (_recorder is null || buffer.IsEmpty || (flags & AudioClientBufferFlags.Silent) != 0) return;

        byte[]? completed = null;
        lock (_gate)
        {
            _buffer.Write(buffer);
            var targetBytes = _recorder.WaveFormat.AverageBytesPerSecond * _chunkDuration.TotalSeconds;
            if (_buffer.Length >= targetBytes)
            {
                completed = _buffer.ToArray();
                _buffer.Dispose();
                _buffer = new MemoryStream();
            }
        }

        if (completed is null) return;
        var chunk = new AudioChunk(completed, 16000, 16, 1, false, DateTimeOffset.UtcNow);
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
        ProcessId = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _buffer.Dispose();
    }
}
