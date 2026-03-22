using System.Text.RegularExpressions;

namespace mide.Constants;

internal static class SyntaxConstants
{
    // ── Punctuation ──────────────────────────────────────────────────────────
    public static readonly HashSet<char> PunctuationChars = new()
    {
        '(', ')', '{', '}', '[', ']', ';', ':', ',', '.', '+', '-', '*', '/', '%', '=', '&', '|', '!', '<', '>', '?', '~'
    };

    // ── C# ───────────────────────────────────────────────────────────────────
    public static readonly HashSet<string> CsKeywords = new(StringComparer.Ordinal)
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

    public static readonly HashSet<string> CsTypeKeywords = new(StringComparer.Ordinal)
    {
        "bool","byte","char","decimal","double","float","int","long","object",
        "sbyte","short","string","uint","ulong","ushort","void","var","dynamic",
        "nint","nuint","Task","List","Dictionary","IEnumerable","IList","Array"
    };

    public static readonly Regex CsTokenPattern = new(
        @"//.*$|""(?:[^""\\]|\\.)*""|@""(?:[^""]|"""")*""|\b\d+(?:\.\d+)?(?:[fFdDmM])?\b|\b[a-zA-Z_]\w*\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ── Python ───────────────────────────────────────────────────────────────
    public static readonly HashSet<string> PyKeywords = new(StringComparer.Ordinal)
    {
        "False","None","True","and","as","assert","async","await","break","class",
        "continue","def","del","elif","else","except","finally","for","from",
        "global","if","import","in","is","lambda","nonlocal","not","or","pass",
        "raise","return","try","while","with","yield","self","cls","super"
    };

    public static readonly Regex PyTokenPattern = new(
        @"#.*$|""""""[\s\S]*?""""""|'''[\s\S]*?'''|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|\b\d+(?:\.\d+)?\b|@\w+|\b[a-zA-Z_]\w*\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ── JavaScript / TypeScript ───────────────────────────────────────────────
    public static readonly HashSet<string> JsKeywords = new(StringComparer.Ordinal)
    {
        "break","case","catch","class","const","continue","debugger","default",
        "delete","do","else","export","extends","false","finally","for","function",
        "if","import","in","instanceof","new","null","return","super","switch",
        "this","throw","true","try","typeof","undefined","var","void","while",
        "with","yield","async","await","let","of","from","static","get","set",
        "abstract","as","declare","enum","implements","interface","is","keyof",
        "module","namespace","never","readonly","require","type","unknown","any"
    };

    public static readonly Regex JsTokenPattern = new(
        @"//.*$|/\*[\s\S]*?\*/|`(?:[^`\\]|\\.)*`|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|\b\d+(?:\.\d+)?\b|\b[a-zA-Z_$]\w*\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ── JSON ──────────────────────────────────────────────────────────────────
    public static readonly Regex JsonPattern = new(
        @"""(?:[^""\\]|\\.)*""\s*:|""(?:[^""\\]|\\.)*""|\b(?:true|false|null)\b|\b-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b",
        RegexOptions.Compiled);

    // ── Markdown ──────────────────────────────────────────────────────────────
    public static readonly Regex MdHeading = new(@"^#{1,6}\s",         RegexOptions.Compiled);
    public static readonly Regex MdBold    = new(@"\*\*.*?\*\*|__.*?__", RegexOptions.Compiled);
    public static readonly Regex MdCode    = new(@"`[^`]+`",            RegexOptions.Compiled);
    public static readonly Regex MdLink    = new(@"\[.*?\]\(.*?\)",     RegexOptions.Compiled);
}
