using System.Diagnostics;
using CS2AITranslator.Core;

namespace CS2AITranslator.Infrastructure;

// Safe fallback used until a cloud/local translation backend is configured.
// It preserves CS2 terminology and makes the pipeline usable for transcription testing.
public sealed class BasicTranslationService : ITranslationService
{
    public Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var normalized = Cs2Glossary.Normalize(text);
        sw.Stop();
        return Task.FromResult(new TranslationResult(text, normalized, sourceLanguage, targetLanguage, sw.Elapsed));
    }
}
