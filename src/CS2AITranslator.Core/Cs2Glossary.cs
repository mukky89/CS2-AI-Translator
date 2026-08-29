using System.Text.RegularExpressions;

namespace CS2AITranslator.Core;

public static class Cs2Glossary
{
    private static readonly string[] ProtectedTerms =
    [
        "M4A1-S", "AK-47", "USP-S", "AWP", "M4A4", "Deagle", "Glock",
        "Apartments", "Connector", "Banana", "Heaven", "Hell", "Ramp", "Pit",
        "Mid", "Short", "Long", "Site", "CT", "T",
        "Eco", "Save", "Rotate", "Flash", "Smoke", "Molotov", "Nade"
    ];

    public sealed record ProtectedTranslationText(string Text, IReadOnlyDictionary<string, string> Tokens);

    public static string Normalize(string text)
    {
        var result = Regex.Replace(text.Trim(), "\\s+", " ");
        foreach (var term in ProtectedTerms)
            result = Regex.Replace(result, $"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(term)}(?![\\p{{L}}\\p{{N}}])", term, RegexOptions.IgnoreCase);
        return result;
    }

    public static ProtectedTranslationText ProtectForTranslation(string text)
    {
        var result = Normalize(text);
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var term in ProtectedTerms.OrderByDescending(x => x.Length))
        {
            var pattern = $"(?<![\\p{{L}}\\p{{N}}]){Regex.Escape(term)}(?![\\p{{L}}\\p{{N}}])";
            result = Regex.Replace(result, pattern, match =>
            {
                var token = $"__CS2TERM_{index++:D3}__";
                tokens[token] = term;
                return token;
            }, RegexOptions.IgnoreCase);
        }

        return new ProtectedTranslationText(result, tokens);
    }

    public static string RestoreAfterTranslation(string translatedText, ProtectedTranslationText protectedText)
    {
        var result = translatedText;
        foreach (var (token, term) in protectedText.Tokens)
        {
            result = result.Replace(token, term, StringComparison.OrdinalIgnoreCase)
                .Replace(token.Replace("_", " "), term, StringComparison.OrdinalIgnoreCase);
        }
        return Normalize(result);
    }
}
