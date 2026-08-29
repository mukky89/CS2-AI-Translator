using Whisper.net.Ggml;

namespace CS2AITranslator.Infrastructure;

public sealed class WhisperModelManager
{
    public WhisperModelManager(string? modelDirectory = null)
    {
        ModelDirectory = modelDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CS2AITranslator",
            "models");
    }

    public string ModelDirectory { get; }

    public async Task<string> EnsureModelAsync(
        GgmlType modelType = GgmlType.Base,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ModelDirectory);
        var fileName = GetFileName(modelType);
        var destination = Path.Combine(ModelDirectory, fileName);
        if (File.Exists(destination) && new FileInfo(destination).Length > 1_000_000)
            return destination;

        var temp = destination + ".download";
        if (File.Exists(temp)) File.Delete(temp);

        await using var source = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(modelType);
        await using var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true);
        await CopyWithProgressAsync(source, target, progress, cancellationToken);
        await target.FlushAsync(cancellationToken);
        File.Move(temp, destination, true);
        return destination;
    }

    public async Task<string> EnsureSileroVadModelAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ModelDirectory);
        var destination = Path.Combine(ModelDirectory, "ggml-silero-v6.2.0.bin");
        if (File.Exists(destination) && new FileInfo(destination).Length > 100_000)
            return destination;

        var temp = destination + ".download";
        if (File.Exists(temp)) File.Delete(temp);

        await using var source = await WhisperGgmlDownloader.Default.GetGgmlSileroVadModelAsync();
        await using var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, true);
        await CopyWithProgressAsync(source, target, progress, cancellationToken);
        await target.FlushAsync(cancellationToken);
        File.Move(temp, destination, true);
        return destination;
    }

    private static string GetFileName(GgmlType type) => type switch
    {
        GgmlType.Tiny => "ggml-tiny.bin",
        GgmlType.Base => "ggml-base.bin",
        GgmlType.Small => "ggml-small.bin",
        GgmlType.Medium => "ggml-medium.bin",
        GgmlType.LargeV3 => "ggml-large-v3.bin",
        GgmlType.LargeV3Turbo => "ggml-large-v3-turbo.bin",
        _ => $"ggml-{type.ToString().ToLowerInvariant()}.bin"
    };

    private static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 128];
        long copied = 0;
        var length = source.CanSeek ? source.Length : -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            if (length > 0) progress?.Report((double)copied / length);
        }
        progress?.Report(1.0);
    }
}
