using SharpConsoleUI.Controls;

namespace mide;

partial class Program
{
    // ── File tree helpers ─────────────────────────────────────────────────
    static void PopulateTree(TreeControl tree, string dir)
    {
        tree.Clear();
        var root = tree.AddRootNode($"[bold yellow]{EscapeMarkup(Path.GetFileName(dir))}/[/]");
        root.Tag = dir;
        AddChildren(root, dir, 0);
    }

    static void AddChildren(SharpConsoleUI.Controls.TreeNode parent, string dir, int depth)
    {
        if (depth > 3) return;
        try
        {
            foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x))
            {
                var name = Path.GetFileName(d);
                if (name.StartsWith('.') || name is "bin" or "obj" or "node_modules" or "__pycache__") continue;
                var node = parent.AddChild($"[cyan]{EscapeMarkup(name)}/[/]");
                node.Tag = d;
                AddChildren(node, d, depth + 1);
            }
            foreach (var f in Directory.GetFiles(dir).OrderBy(x => x))
            {
                var name = Path.GetFileName(f);
                var node = parent.AddChild($"{FileIcon(name)} {EscapeMarkup(name)}");
                node.Tag = f;
            }
        }
        catch { /* permission denied */ }
    }

    static void SyncTreeSelection(string path)
    {
        if (_fileTree == null) return;

        var normalized = Path.GetFullPath(path);
        var node = _fileTree.FindNodeByTag(normalized);
        if (node == null) return;

        _suppressTreeEvent = true;
        _fileTree.SelectNode(node);
        _fileTree.EnsureNodeVisible(node);
        _suppressTreeEvent = false;
    }

    static string FileIcon(string name) => Path.GetExtension(name).ToLower() switch
    {
        ".cs"               => "[green]C#[/]",
        ".py"               => "[yellow]PY[/]",
        ".js"               => "[yellow]JS[/]",
        ".ts" or ".tsx"     => "[blue]TS[/]",
        ".json"             => "[orange3]{}[/]",
        ".md"               => "[white]MD[/]",
        ".txt"              => "[grey]TX[/]",
        ".xml"              => "[orange1]XM[/]",
        ".html" or ".htm"   => "[magenta]HT[/]",
        ".css"              => "[cyan]CS[/]",
        ".sh"               => "[green]SH[/]",
        ".csproj" or ".sln" => "[blue]PR[/]",
        _                   => "[grey]  [/]"
    };

    // ── Toggle tree ─────────────────────────────────────────────────────--
    static void ToggleTree()
    {
        _treeVisible = !_treeVisible;
        ApplyTreeVisibility();
    }

    static void ApplyTreeVisibility()
    {
        if (_fileTree != null)
        {
            _fileTree.Visible   = _treeVisible;
            _fileTree.IsEnabled = _treeVisible;
            _fileTree.Width     = _treeVisible ? 30 : 0;

            if (!_treeVisible)
                FocusEditor(editing: _editor?.IsEditing ?? false);

            UpdateLayoutWidths();
        }
    }
}
