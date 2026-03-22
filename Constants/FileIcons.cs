namespace mide.Constants;

internal static class FileIcons
{
    // Map file extensions to display icons (Spectre.Console markup)
    private static readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"]     = "[green]C#[/]",
        [".py"]     = "[yellow]PY[/]",
        [".js"]     = "[yellow]JS[/]",
        [".ts"]     = "[blue]TS[/]",
        [".tsx"]    = "[blue]TS[/]",
        [".json"]   = "[orange3]{}[/]",
        [".md"]     = "[white]MD[/]",
        [".txt"]    = "[grey]TX[/]",
        [".xml"]    = "[orange1]XM[/]",
        [".html"]   = "[magenta]HT[/]",
        [".htm"]    = "[magenta]HT[/]",
        [".css"]    = "[cyan]CS[/]",
        [".sh"]     = "[green]SH[/]",
        [".csproj"] = "[blue]PR[/]",
        [".sln"]    = "[blue]PR[/]"
    };

    public static string ForFile(string name)
    {
        var ext = Path.GetExtension(name);
        if (string.IsNullOrEmpty(ext)) return "[grey]  [/]";
        return _map.TryGetValue(ext, out var icon) ? icon : "[grey]  [/]";
    }
}
