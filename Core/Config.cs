using System.Text.Json;
using System.Text.Json.Serialization;

namespace mide;

sealed class AppSettings
{
    [JsonPropertyName("title")] public string Title { get; init; } = "mide";
    [JsonPropertyName("topStatusDefault")] public string TopStatusDefault { get; init; } = " mide ";
}

public sealed class EditorSettings
{
    [JsonPropertyName("lineNumbers")] public bool LineNumbers { get; init; } = true;
    [JsonPropertyName("highlightCurrentLine")] public bool HighlightCurrentLine { get; init; } = true;
    [JsonPropertyName("autoIndent")] public bool AutoIndent { get; init; } = true;
    [JsonPropertyName("startInEditMode")] public bool StartInEditMode { get; init; } = false;

    // Editor surface colors
    [JsonPropertyName("backgroundColor")] public string BackgroundColor { get; init; } = "#001a33";
    [JsonPropertyName("foregroundColor")] public string ForegroundColor { get; init; } = "#fffdf5";
    [JsonPropertyName("browseBackgroundColor")] public string BrowseBackgroundColor { get; init; } = "#001a33";
    [JsonPropertyName("editBackgroundColor")] public string EditBackgroundColor { get; init; } = "#000d1a";
    [JsonPropertyName("treeBackgroundColor")] public string TreeBackgroundColor { get; init; } = "#00080f";
    [JsonPropertyName("lineNumberColor")] public string LineNumberColor { get; init; } = "#008b8b";
    [JsonPropertyName("currentLineHighlightColor")] public string CurrentLineHighlightColor { get; init; } = "#008b8b";

    // Syntax colors
    [JsonPropertyName("syntaxKeywordColor")] public string SyntaxKeywordColor { get; init; } = "#1e90ff";
    [JsonPropertyName("syntaxTypeColor")] public string SyntaxTypeColor { get; init; } = "#48d1cc";
    [JsonPropertyName("syntaxStringColor")] public string SyntaxStringColor { get; init; } = "#cd8500";
    [JsonPropertyName("syntaxCommentColor")] public string SyntaxCommentColor { get; init; } = "#00ff00";
    [JsonPropertyName("syntaxNumberColor")] public string SyntaxNumberColor { get; init; } = "#00ffff";
    [JsonPropertyName("syntaxPunctuationColor")] public string SyntaxPunctuationColor { get; init; } = "#00bfff";
    [JsonPropertyName("syntaxIdentifierColor")] public string SyntaxIdentifierColor { get; init; } = "#ffecb3";
    [JsonPropertyName("syntaxJsonKeyColor")] public string SyntaxJsonKeyColor { get; init; } = "#ffd700";
    [JsonPropertyName("syntaxMarkdownHeadColor")] public string SyntaxMarkdownHeadColor { get; init; } = "#ffff00";
    [JsonPropertyName("syntaxMarkdownBoldColor")] public string SyntaxMarkdownBoldColor { get; init; } = "#ffffff";
    [JsonPropertyName("syntaxPythonDecorColor")] public string SyntaxPythonDecorColor { get; init; } = "#eeaeee";
}

sealed class TreeSettings
{
    [JsonPropertyName("highlightBackgroundColor")] public string HighlightBackgroundColor { get; init; } = "#000080";
    [JsonPropertyName("visibleOnStart")] public bool VisibleOnStart { get; init; } = false;
    [JsonPropertyName("folderColor")] public string FolderColor { get; init; } = "#ffd700";
    [JsonPropertyName("width")] public int Width { get; init; } = 30;
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; init; } = 3;
    [JsonPropertyName("ignoredDirs")] public string[] IgnoredDirs { get; init; } = ["bin", ".git", "obj", "node_modules", "__pycache__"];
    [JsonPropertyName("refreshIntervalSeconds")] public int RefreshIntervalSeconds { get; init; } = 5;
}

sealed class LayoutSettings
{
    [JsonPropertyName("padding")] public int Padding { get; init; } = 2;
    [JsonPropertyName("minEditorWidth")] public int MinEditorWidth { get; init; } = 10;
    [JsonPropertyName("minCommandWidth")] public int MinCommandWidth { get; init; } = 20;
}

sealed class DialogsSettings
{
    [JsonPropertyName("fileFilter")] public string FileFilter { get; init; } = "*.cs;*.py;*.js;*.ts;*.json;*.md;*.txt;*.*";
    [JsonPropertyName("findWidth")] public int FindWidth { get; init; } = 50;
    [JsonPropertyName("findHeight")] public int FindHeight { get; init; } = 8;
    [JsonPropertyName("gotoWidth")] public int GotoWidth { get; init; } = 40;
    [JsonPropertyName("gotoHeight")] public int GotoHeight { get; init; } = 7;
    [JsonPropertyName("commandHeight")] public int CommandHeight { get; init; } = 3;
}

sealed class StartupSettings
{
    [JsonPropertyName("readmeCandidates")] public string[] ReadmeCandidates { get; init; } = ["README.md", "Readme.md", "README.txt", "README"];
}

sealed class Config
{
    [JsonPropertyName("app")] public AppSettings App { get; init; } = new();
    [JsonPropertyName("editor")] public EditorSettings Editor { get; init; } = new();
    [JsonPropertyName("tree")] public TreeSettings Tree { get; init; } = new();
    [JsonPropertyName("layout")] public LayoutSettings Layout { get; init; } = new();
    [JsonPropertyName("dialogs")] public DialogsSettings Dialogs { get; init; } = new();
    [JsonPropertyName("startup")] public StartupSettings Startup { get; init; } = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Config Load()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Settings", "default.config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Settings", "default.config.json")
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Config>(json, _jsonOptions) ?? new Config();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[config] Failed to load '{path}': {ex.Message} — using defaults.");
            }
        }

        return new Config();
    }
}
