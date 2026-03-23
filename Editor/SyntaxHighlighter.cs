using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using mide.Constants;

namespace mide;

/// <summary>
/// Multi-language syntax highlighter for the MultilineEditControl.
/// Supports C#, Python, JavaScript/TypeScript, JSON, Markdown, and plain text.
/// </summary>
public class IdeSyntaxHighlighter : ISyntaxHighlighter
{
    private readonly Color _keywordColor;
    private readonly Color _typeKeywordColor;
    private readonly Color _stringColor;
    private readonly Color _commentColor;
    private readonly Color _numberColor;
    private readonly Color _punctuationColor;
    private readonly Color _identifierColor;
    private readonly Color _jsonKeyColor;
    private readonly Color _markdownHeadColor;
    private readonly Color _markdownBoldColor;
    private readonly Color _pythonDecorColor;
    private readonly Language _language;

    public enum Language { CSharp, Python, JavaScript, TypeScript, Json, Markdown, PlainText }

    public IdeSyntaxHighlighter(Language language = Language.CSharp, EditorSettings? settings = null)
    {
        _language = language;
        var s = settings ?? new EditorSettings();
        _keywordColor      = ParseColor(s.SyntaxKeywordColor, Color.DodgerBlue2);
        _typeKeywordColor  = ParseColor(s.SyntaxTypeColor, Color.MediumTurquoise);
        _stringColor       = ParseColor(s.SyntaxStringColor, Color.Orange3);
        _commentColor      = ParseColor(s.SyntaxCommentColor, Color.Green);
        _numberColor       = ParseColor(s.SyntaxNumberColor, Color.Cyan1);
        _punctuationColor  = ParseColor(s.SyntaxPunctuationColor, Color.LightSlateGrey);
        _identifierColor   = ParseColor(s.SyntaxIdentifierColor, Color.Silver);
        _jsonKeyColor      = ParseColor(s.SyntaxJsonKeyColor, Color.Gold1);
        _markdownHeadColor = ParseColor(s.SyntaxMarkdownHeadColor, Color.Yellow);
        _markdownBoldColor = ParseColor(s.SyntaxMarkdownBoldColor, Color.White);
        _pythonDecorColor  = ParseColor(s.SyntaxPythonDecorColor, Color.Plum1);
    }

    public static IdeSyntaxHighlighter ForExtension(string ext, EditorSettings? settings = null) => ext.ToLower() switch
    {
        ".cs"   => new IdeSyntaxHighlighter(Language.CSharp, settings),
        ".py"   => new IdeSyntaxHighlighter(Language.Python, settings),
        ".js"   => new IdeSyntaxHighlighter(Language.JavaScript, settings),
        ".ts" or ".tsx" => new IdeSyntaxHighlighter(Language.TypeScript, settings),
        ".json" => new IdeSyntaxHighlighter(Language.Json, settings),
        ".md" or ".markdown" => new IdeSyntaxHighlighter(Language.Markdown, settings),
        _ => new IdeSyntaxHighlighter(Language.PlainText, settings)
    };

    // ── C# ─────────────────────────────────────────────────────────────────

    // ── Python ─────────────────────────────────────────────────────────────

    // ── JavaScript / TypeScript ─────────────────────────────────────────────

    // ── JSON ────────────────────────────────────────────────────────────────

    // ── Markdown ────────────────────────────────────────────────────────────

    // ───────────────────────────────────────────────────────────────────────
    public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
        Tokenize(string line, int lineIndex, SyntaxLineState startState)
    {
        var tokens = _language switch
        {
            Language.CSharp      => TokenizeCSharp(line),
            Language.Python      => TokenizePython(line),
            Language.JavaScript  => TokenizeJs(line),
            Language.TypeScript  => TokenizeJs(line),
            Language.Json        => TokenizeJson(line),
            Language.Markdown    => TokenizeMarkdown(line),
            _                    => Array.Empty<SyntaxToken>()
        };
        return (tokens, SyntaxLineState.Initial);
    }

    private IReadOnlyList<SyntaxToken> TokenizeCSharp(string line)
    {
        var tokens = new List<SyntaxToken>();
        var protectedRanges = new List<(int start, int length)>();
        int commentStart = FindLineCommentStart(line, "//");

        if (commentStart == 0) { tokens.Add(new SyntaxToken(0, line.Length, _commentColor)); return tokens; }
        if (commentStart > 0)  tokens.Add(new SyntaxToken(commentStart, line.Length - commentStart, _commentColor));

        foreach (Match m in SyntaxConstants.CsTokenPattern.Matches(line))
        {
            if (commentStart >= 0 && m.Index >= commentStart) continue;
            var t = m.Value;
            if (t.StartsWith("//")) continue;
            if (t.StartsWith("\"") || t.StartsWith("@\"")) { tokens.Add(new SyntaxToken(m.Index, m.Length, _stringColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
            if (char.IsDigit(t[0])) { tokens.Add(new SyntaxToken(m.Index, m.Length, _numberColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
            if (SyntaxConstants.CsTypeKeywords.Contains(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, _typeKeywordColor)); continue; }
            if (SyntaxConstants.CsKeywords.Contains(t))     { tokens.Add(new SyntaxToken(m.Index, m.Length, _keywordColor)); continue; }
            if (LooksLikeTypeIdentifier(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, _typeKeywordColor)); continue; }
            tokens.Add(new SyntaxToken(m.Index, m.Length, _identifierColor));
        }
        AddPunctuationTokens(line, tokens, commentStart, protectedRanges);
        return tokens;
    }

    private IReadOnlyList<SyntaxToken> TokenizePython(string line)
    {
        var tokens = new List<SyntaxToken>();
        var protectedRanges = new List<(int start, int length)>();
        int commentStart = line.IndexOf('#');
        if (commentStart == 0) { tokens.Add(new SyntaxToken(0, line.Length, _commentColor)); return tokens; }
        if (commentStart > 0)  tokens.Add(new SyntaxToken(commentStart, line.Length - commentStart, _commentColor));

        foreach (Match m in SyntaxConstants.PyTokenPattern.Matches(line))
        {
            if (commentStart >= 0 && m.Index >= commentStart) continue;
            var t = m.Value;
            if (t.StartsWith("#")) continue;
            if (t.StartsWith("@")) { tokens.Add(new SyntaxToken(m.Index, m.Length, _pythonDecorColor)); continue; }
            if (t.StartsWith("\"") || t.StartsWith("'")) { tokens.Add(new SyntaxToken(m.Index, m.Length, _stringColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
            if (char.IsDigit(t[0])) { tokens.Add(new SyntaxToken(m.Index, m.Length, _numberColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
            if (SyntaxConstants.PyKeywords.Contains(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, _keywordColor)); continue; }
            if (LooksLikeTypeIdentifier(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, _typeKeywordColor)); continue; }
            tokens.Add(new SyntaxToken(m.Index, m.Length, _identifierColor));
        }
        AddPunctuationTokens(line, tokens, commentStart, protectedRanges);
        return tokens;
    }

    private IReadOnlyList<SyntaxToken> TokenizeJs(string line)
    {
        var tokens = new List<SyntaxToken>();
        var protectedRanges = new List<(int start, int length)>();
        int commentStart = FindLineCommentStart(line, "//");
        if (commentStart == 0) { tokens.Add(new SyntaxToken(0, line.Length, _commentColor)); return tokens; }
        if (commentStart > 0)  tokens.Add(new SyntaxToken(commentStart, line.Length - commentStart, _commentColor));

        foreach (Match m in SyntaxConstants.JsTokenPattern.Matches(line))
        {
            if (commentStart >= 0 && m.Index >= commentStart) continue;
            var t = m.Value;
            if (t.StartsWith("//") || t.StartsWith("/*")) continue;
            if (t.StartsWith("\"") || t.StartsWith("'") || t.StartsWith("`")) { tokens.Add(new SyntaxToken(m.Index, m.Length, _stringColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
            if (char.IsDigit(t[0])) { tokens.Add(new SyntaxToken(m.Index, m.Length, _numberColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
            if (SyntaxConstants.JsKeywords.Contains(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, _keywordColor)); continue; }
            if (LooksLikeTypeIdentifier(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, _typeKeywordColor)); continue; }
            tokens.Add(new SyntaxToken(m.Index, m.Length, _identifierColor));
        }
        AddPunctuationTokens(line, tokens, commentStart, protectedRanges);
        return tokens;
    }

    private IReadOnlyList<SyntaxToken> TokenizeJson(string line)
    {
        var tokens = new List<SyntaxToken>();
        var protectedRanges = new List<(int start, int length)>();
        foreach (Match m in SyntaxConstants.JsonPattern.Matches(line))
        {
            var t = m.Value;
            // key (ends with colon after quote)
            if (t.EndsWith(":") || (t.EndsWith(": ") == false && m.Value.Contains("\":")))
            {
                // JSON key  — grab just the quoted part
                int colon = t.LastIndexOf(':');
                if (colon > 0) { tokens.Add(new SyntaxToken(m.Index, colon, _jsonKeyColor)); continue; }
            }
            if (t.StartsWith("\"")) { tokens.Add(new SyntaxToken(m.Index, m.Length, _stringColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
            if (t == "true" || t == "false" || t == "null") { tokens.Add(new SyntaxToken(m.Index, m.Length, _keywordColor)); continue; }
            if (char.IsDigit(t[0]) || t[0] == '-') { tokens.Add(new SyntaxToken(m.Index, m.Length, _numberColor)); protectedRanges.Add((m.Index, m.Length)); continue; }
        }
        AddPunctuationTokens(line, tokens, -1, protectedRanges);
        return tokens;
    }

    private IReadOnlyList<SyntaxToken> TokenizeMarkdown(string line)
    {
        var tokens = new List<SyntaxToken>();
        if (SyntaxConstants.MdHeading.IsMatch(line)) { tokens.Add(new SyntaxToken(0, line.Length, _markdownHeadColor)); return tokens; }
        foreach (Match m in SyntaxConstants.MdBold.Matches(line))  tokens.Add(new SyntaxToken(m.Index, m.Length, _markdownBoldColor));
        foreach (Match m in SyntaxConstants.MdCode.Matches(line))  tokens.Add(new SyntaxToken(m.Index, m.Length, _stringColor));
        foreach (Match m in SyntaxConstants.MdLink.Matches(line))  tokens.Add(new SyntaxToken(m.Index, m.Length, _keywordColor));
        return tokens;
    }

    private static int FindLineCommentStart(string line, string marker)
    {
        bool inString = false;
        for (int i = 0; i <= line.Length - marker.Length; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) inString = !inString;
            if (!inString && line.Substring(i, marker.Length) == marker) return i;
        }
        return -1;
    }

    private void AddPunctuationTokens(string line, List<SyntaxToken> tokens, int commentStart, List<(int start, int length)> protectedRanges)
    {
        for (int i = 0; i < line.Length; i++)
        {
            if (commentStart >= 0 && i >= commentStart) break;
            if (SyntaxConstants.PunctuationChars.Contains(line[i]) && IsOutsideRanges(i, protectedRanges))
            {
                tokens.Add(new SyntaxToken(i, 1, _punctuationColor));
            }
        }
    }

    private static bool IsOutsideRanges(int index, List<(int start, int length)> ranges)
    {
        foreach (var (start, length) in ranges)
        {
            if (index >= start && index < start + length) return false;
        }
        return true;
    }

    private static bool LooksLikeTypeIdentifier(string token)
    {
        // Heuristic: starts with uppercase letter and not just a single letter
        return token.Length > 1 && char.IsLetter(token[0]) && char.IsUpper(token[0]);
    }

    private static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var prop = typeof(Color).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(Color));
        if (prop?.GetValue(null) is Color named) return named;
        if (value.StartsWith('#'))
        {
            if (Color.TryFromHex(value, out var hex)) return hex;
        }
        return fallback;
    }
}
