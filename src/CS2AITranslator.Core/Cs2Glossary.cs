using System.Text.RegularExpressions;

namespace CS2AITranslator.Core;

public static class Cs2Glossary
{
    private static readonly string[] ProtectedTerms =
    [
        "AWP", "AK-47", "M4A1-S", "M4A4", "Deagle", "Glock", "USP-S",
        "mid", "short", "long", "connector", "ramp", "heaven", "hell",
        "Banana", "Apartments", "Pit", "site", "eco", "save", "rotate",
        "flash", "smoke", "molotov", "nade", "CT", "T"
    ];

    public static string Normalize(string text)
    {
        var result = Regex.Replace(text.Trim(), "\\s+", " ");
        foreach (var term in ProtectedTerms)
            result = Regex.Replace(result, $"\\b{Regex.Escape(term)}\\b", term, RegexOptions.IgnoreCase);
        return result;
    }
}
