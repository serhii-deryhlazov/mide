using System.Text.RegularExpressions;
using Spectre.Console;
using SharpConsoleUI;
using SharpConsoleUI.Controls;

namespace mide;

/// <summary>
/// Multi-language syntax highlighter for the MultilineEditControl.
/// Supports C#, Python, JavaScript/TypeScript, JSON, Markdown, and plain text.
/// </summary>
public class IdeSyntaxHighlighter : ISyntaxHighlighter
{
    private static readonly Color KeywordColor      = Color.DodgerBlue2;
    private static readonly Color TypeKeywordColor  = Color.MediumTurquoise;
    private static readonly Color StringColor       = Color.Orange3;
    private static readonly Color CommentColor      = Color.Green;
    private static readonly Color NumberColor       = Color.Cyan1;
    private static readonly Color PunctuationColor  = Color.Grey70;
    private static readonly Color JsonKeyColor      = Color.Gold1;
    private static readonly Color MarkdownHeadColor = Color.Yellow;
    private static readonly Color MarkdownBoldColor = Color.White;
    private static readonly Color PythonDecorColor  = Color.Plum2;

    private readonly Language _language;

    public enum Language { CSharp, Python, JavaScript, TypeScript, Json, Markdown, PlainText }

    public IdeSyntaxHighlighter(Language language = Language.CSharp)
    {
        _language = language;
    }

    public static IdeSyntaxHighlighter ForExtension(string ext) => ext.ToLower() switch
    {
        ".cs"   => new IdeSyntaxHighlighter(Language.CSharp),
        ".py"   => new IdeSyntaxHighlighter(Language.Python),
        ".js"   => new IdeSyntaxHighlighter(Language.JavaScript),
        ".ts" or ".tsx" => new IdeSyntaxHighlighter(Language.TypeScript),
        ".json" => new IdeSyntaxHighlighter(Language.Json),
        ".md" or ".markdown" => new IdeSyntaxHighlighter(Language.Markdown),
        _ => new IdeSyntaxHighlighter(Language.PlainText)
    };

    // ── C# ─────────────────────────────────────────────────────────────────
    private static readonly HashSet<string> CsKeywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","break","case","catch","checked","class","const",
        "continue","default","delegate","do","else","enum","event","explicit",
        "extern","false","finally","fixed","for","foreach","goto","if","implicit",
        "in","interface","internal","is","lock","namespace","new","null","operator",
        "out","override","params","private","protected","public","readonly","ref",
        "return","sealed","sizeof","stackalloc","static","struct","switch","this",
        "throw","true","try","typeof","unchecked","unsafe","using","virtual",
        "volatile","while","async","await","yield","where","get","set","init",
        "record","required","with","not","and","or","file","scoped","managed",
        "unmanaged","notnull","default","nameof","typeof"
    };
    private static readonly HashSet<string> CsTypeKeywords = new(StringComparer.Ordinal)
    {
        "bool","byte","char","decimal","double","float","int","long","object",
        "sbyte","short","string","uint","ulong","ushort","void","var","dynamic",
        "nint","nuint","Task","List","Dictionary","IEnumerable","IList","Array"
    };
    private static readonly Regex CsTokenPattern = new(
        @"//.*$|""(?:[^""\\]|\\.)*""|@""(?:[^""]|"""")*""|\b\d+(?:\.\d+)?(?:[fFdDmM])?\b|\b[a-zA-Z_]\w*\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ── Python ─────────────────────────────────────────────────────────────
    private static readonly HashSet<string> PyKeywords = new(StringComparer.Ordinal)
    {
        "False","None","True","and","as","assert","async","await","break","class",
        "continue","def","del","elif","else","except","finally","for","from",
        "global","if","import","in","is","lambda","nonlocal","not","or","pass",
        "raise","return","try","while","with","yield","self","cls","super"
    };
    private static readonly Regex PyTokenPattern = new(
        @"#.*$|""""""[\s\S]*?""""""|'''[\s\S]*?'''|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|\b\d+(?:\.\d+)?\b|@\w+|\b[a-zA-Z_]\w*\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ── JavaScript / TypeScript ─────────────────────────────────────────────
    private static readonly HashSet<string> JsKeywords = new(StringComparer.Ordinal)
    {
        "break","case","catch","class","const","continue","debugger","default",
        "delete","do","else","export","extends","false","finally","for","function",
        "if","import","in","instanceof","new","null","return","super","switch",
        "this","throw","true","try","typeof","undefined","var","void","while",
        "with","yield","async","await","let","of","from","static","get","set",
        "abstract","as","declare","enum","implements","interface","is","keyof",
        "module","namespace","never","readonly","require","type","unknown","any"
    };
    private static readonly Regex JsTokenPattern = new(
        @"//.*$|/\*[\s\S]*?\*/|`(?:[^`\\]|\\.)*`|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|\b\d+(?:\.\d+)?\b|\b[a-zA-Z_$]\w*\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ── JSON ────────────────────────────────────────────────────────────────
    private static readonly Regex JsonPattern = new(
        @"""(?:[^""\\]|\\.)*""\s*:|""(?:[^""\\]|\\.)*""|\b(?:true|false|null)\b|\b-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b",
        RegexOptions.Compiled);

    // ── Markdown ────────────────────────────────────────────────────────────
    private static readonly Regex MdHeading  = new(@"^#{1,6}\s", RegexOptions.Compiled);
    private static readonly Regex MdBold     = new(@"\*\*.*?\*\*|__.*?__", RegexOptions.Compiled);
    private static readonly Regex MdCode     = new(@"`[^`]+`", RegexOptions.Compiled);
    private static readonly Regex MdLink     = new(@"\[.*?\]\(.*?\)", RegexOptions.Compiled);

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

    private static IReadOnlyList<SyntaxToken> TokenizeCSharp(string line)
    {
        var tokens = new List<SyntaxToken>();
        int commentStart = FindLineCommentStart(line, "//");

        if (commentStart == 0) { tokens.Add(new SyntaxToken(0, line.Length, CommentColor)); return tokens; }
        if (commentStart > 0)  tokens.Add(new SyntaxToken(commentStart, line.Length - commentStart, CommentColor));

        foreach (Match m in CsTokenPattern.Matches(line))
        {
            if (commentStart >= 0 && m.Index >= commentStart) continue;
            var t = m.Value;
            if (t.StartsWith("//")) continue;
            if (t.StartsWith("\"") || t.StartsWith("@\"")) { tokens.Add(new SyntaxToken(m.Index, m.Length, StringColor)); continue; }
            if (char.IsDigit(t[0])) { tokens.Add(new SyntaxToken(m.Index, m.Length, NumberColor)); continue; }
            if (CsTypeKeywords.Contains(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, TypeKeywordColor)); continue; }
            if (CsKeywords.Contains(t))     { tokens.Add(new SyntaxToken(m.Index, m.Length, KeywordColor)); continue; }
        }
        return tokens;
    }

    private static IReadOnlyList<SyntaxToken> TokenizePython(string line)
    {
        var tokens = new List<SyntaxToken>();
        int commentStart = line.IndexOf('#');
        if (commentStart == 0) { tokens.Add(new SyntaxToken(0, line.Length, CommentColor)); return tokens; }
        if (commentStart > 0)  tokens.Add(new SyntaxToken(commentStart, line.Length - commentStart, CommentColor));

        foreach (Match m in PyTokenPattern.Matches(line))
        {
            if (commentStart >= 0 && m.Index >= commentStart) continue;
            var t = m.Value;
            if (t.StartsWith("#")) continue;
            if (t.StartsWith("@")) { tokens.Add(new SyntaxToken(m.Index, m.Length, PythonDecorColor)); continue; }
            if (t.StartsWith("\"") || t.StartsWith("'")) { tokens.Add(new SyntaxToken(m.Index, m.Length, StringColor)); continue; }
            if (char.IsDigit(t[0])) { tokens.Add(new SyntaxToken(m.Index, m.Length, NumberColor)); continue; }
            if (PyKeywords.Contains(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, KeywordColor)); continue; }
        }
        return tokens;
    }

    private static IReadOnlyList<SyntaxToken> TokenizeJs(string line)
    {
        var tokens = new List<SyntaxToken>();
        int commentStart = FindLineCommentStart(line, "//");
        if (commentStart == 0) { tokens.Add(new SyntaxToken(0, line.Length, CommentColor)); return tokens; }
        if (commentStart > 0)  tokens.Add(new SyntaxToken(commentStart, line.Length - commentStart, CommentColor));

        foreach (Match m in JsTokenPattern.Matches(line))
        {
            if (commentStart >= 0 && m.Index >= commentStart) continue;
            var t = m.Value;
            if (t.StartsWith("//") || t.StartsWith("/*")) continue;
            if (t.StartsWith("\"") || t.StartsWith("'") || t.StartsWith("`")) { tokens.Add(new SyntaxToken(m.Index, m.Length, StringColor)); continue; }
            if (char.IsDigit(t[0])) { tokens.Add(new SyntaxToken(m.Index, m.Length, NumberColor)); continue; }
            if (JsKeywords.Contains(t)) { tokens.Add(new SyntaxToken(m.Index, m.Length, KeywordColor)); continue; }
        }
        return tokens;
    }

    private static IReadOnlyList<SyntaxToken> TokenizeJson(string line)
    {
        var tokens = new List<SyntaxToken>();
        foreach (Match m in JsonPattern.Matches(line))
        {
            var t = m.Value;
            // key (ends with colon after quote)
            if (t.EndsWith(":") || (t.EndsWith(": ") == false && m.Value.Contains("\":") ))
            {
                // JSON key  — grab just the quoted part
                int colon = t.LastIndexOf(':');
                if (colon > 0) { tokens.Add(new SyntaxToken(m.Index, colon, JsonKeyColor)); continue; }
            }
            if (t.StartsWith("\"")) { tokens.Add(new SyntaxToken(m.Index, m.Length, StringColor)); continue; }
            if (t == "true" || t == "false" || t == "null") { tokens.Add(new SyntaxToken(m.Index, m.Length, KeywordColor)); continue; }
            if (char.IsDigit(t[0]) || t[0] == '-') { tokens.Add(new SyntaxToken(m.Index, m.Length, NumberColor)); continue; }
        }
        return tokens;
    }

    private static IReadOnlyList<SyntaxToken> TokenizeMarkdown(string line)
    {
        var tokens = new List<SyntaxToken>();
        if (MdHeading.IsMatch(line)) { tokens.Add(new SyntaxToken(0, line.Length, MarkdownHeadColor)); return tokens; }
        foreach (Match m in MdBold.Matches(line))  tokens.Add(new SyntaxToken(m.Index, m.Length, MarkdownBoldColor));
        foreach (Match m in MdCode.Matches(line))  tokens.Add(new SyntaxToken(m.Index, m.Length, StringColor));
        foreach (Match m in MdLink.Matches(line))  tokens.Add(new SyntaxToken(m.Index, m.Length, KeywordColor));
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
}
