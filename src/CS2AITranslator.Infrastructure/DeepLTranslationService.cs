using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using CS2AITranslator.Core;

namespace CS2AITranslator.Infrastructure;

public sealed class DeepLTranslationService : ITranslationService, IDisposable
{
    private readonly HttpClient _http;

    public DeepLTranslationService(string apiKey, bool useFreeApi = true, HttpMessageHandler? handler = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("DeepL API key is required.", nameof(apiKey));
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(useFreeApi ? "https://api-free.deepl.com" : "https://api.deepl.com");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", apiKey.Trim());
        _http.Timeout = TimeSpan.FromSeconds(6);
    }

    public async Task<TranslationResult> TranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TranslationResult(text, text, sourceLanguage, targetLanguage, TimeSpan.Zero);

        var sw = Stopwatch.StartNew();
        var form = new Dictionary<string, string>
        {
            ["text"] = Cs2Glossary.Normalize(text),
            ["target_lang"] = ToDeepLLanguage(targetLanguage)
        };

        if (!string.IsNullOrWhiteSpace(sourceLanguage) && !sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase))
            form["source_lang"] = ToDeepLLanguage(sourceLanguage);

        using var response = await _http.PostAsync("/v2/translate", new FormUrlEncodedContent(form), cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"DeepL translation failed ({(int)response.StatusCode}): {json}");

        using var document = JsonDocument.Parse(json);
        var translated = document.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? text;
        var detected = document.RootElement.GetProperty("translations")[0].TryGetProperty("detected_source_language", out var detectedEl)
            ? detectedEl.GetString() ?? sourceLanguage
            : sourceLanguage;

        sw.Stop();
        return new TranslationResult(text, Cs2Glossary.Normalize(translated), detected.ToLowerInvariant(), targetLanguage, sw.Elapsed);
    }

    private static string ToDeepLLanguage(string language) => language.Trim().ToLowerInvariant() switch
    {
        "sk" or "slovak" => "SK",
        "cs" or "cz" or "czech" => "CS",
        "en" or "english" => "EN",
        "de" or "german" => "DE",
        "pl" or "polish" => "PL",
        "ru" or "russian" => "RU",
        "uk" or "ua" or "ukrainian" => "UK",
        "fr" or "french" => "FR",
        "es" or "spanish" => "ES",
        "pt" or "portuguese" => "PT-PT",
        "tr" or "turkish" => "TR",
        var other => other.ToUpperInvariant()
    };

    public void Dispose() => _http.Dispose();
}
