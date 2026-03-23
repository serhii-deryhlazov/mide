using System.Reflection;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Themes;

namespace mide;

partial class Program
{
    static void BuildIdeWindow(ConsoleWindowSystem ws)
    {
        _editor = Controls.MultilineEdit(Constants.WelcomeContent.Text)
            .WithLineNumbers(_config.Editor.LineNumbers)
            .WithHighlightCurrentLine(_config.Editor.HighlightCurrentLine)
            .WithAutoIndent(_config.Editor.AutoIndent)
            .WithSyntaxHighlighter(new IdeSyntaxHighlighter(IdeSyntaxHighlighter.Language.CSharp, _config.Editor))
            .WrapWords()
            .IsEditing(_config.Editor.StartInEditMode)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .WithName("editor")
            .Build();

        _editor.ContentChanged += (_, _) => UpdateStatusBar();
        _editor.CursorPositionChanged += (_, _) => UpdateStatusBar();
        _editor.OverwriteModeChanged += (_, _) => UpdateStatusBar();
        _editor.EditingModeChanged += (_, _) => UpdateStatusBar();

        _editor.LineNumberColor = ParseColor(_config.Editor.LineNumberColor, new Color(200, 255, 200));

        _editorBrowseBg  = ParseColor(_config.Editor.BrowseBackgroundColor, new Color(0, 26, 51));
        _editor.CurrentLineHighlightColor = ParseColor(_config.Editor.CurrentLineHighlightColor, new Color(0, 139, 139));

        _fileTree = new TreeControl { Name = "fileTree" };
        PopulateTree(_fileTree, _rootDir);
        ApplyTreeVisibility();

        // Saving folders expansion/collapse state
        _fileTree.NodeExpandCollapse += (_, e) =>
        {
            if (e.Node?.Tag is string p)
            {
                if (e.Node.IsExpanded) _expandedPaths.Add(p);
                else _expandedPaths.Remove(p);
            }
        };

        // Open file (on Enter)
        _fileTree.NodeActivated += (_, e) =>
        {
            if (e.Node?.Tag is string path && File.Exists(path))
            {
                _suppressTreeEvent = true;

                _editor!.Content = File.ReadAllText(path);
                _currentFile = path;
                _editor.SyntaxHighlighter = IdeSyntaxHighlighter.ForExtension(
                    Path.GetExtension(path), _config.Editor);

                UpdateTitle();
                UpdateStatusBar();

                _treeVisible = false;
                ApplyTreeVisibility();

                SetEditorMode(EditorMode.Edit, focus: true);
                _editor.EnsureCursorVisible();
                _suppressTreeEvent = false;
            }
        };

        // Show file on selection in a tree (without Enter)
        _fileTree.SelectedNodeChanged += (_, e) =>
        {
            if (_suppressTreeEvent) return;
            if (e.Node?.Tag is string path && File.Exists(path))
                OpenFile(path, fromTree: true, focus: false);
        };

        _fileTree.HighlightBackgroundColor = ParseColor(_config.Tree.HighlightBackgroundColor, Color.Navy);

        var layout = Controls.HorizontalGrid()
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Column(col => col.Add(_fileTree))
            .Column(col => col.Add(_editor))
            .Build();

        _layout = layout;

        var window = new WindowBuilder(ws)
            .WithTitle("mide")
            .Maximized()
            .HideTitle()
            .HideTitleButtons()
            .Borderless()
            .WithBackgroundColor(ParseColor(_config.Editor.BackgroundColor, new Color(0, 26, 51)))
            .WithForegroundColor(ParseColor(_config.Editor.ForegroundColor, new Color(255, 253, 245)))
            .AddControl(layout)
            .BuildAndShow();

        window.PreviewKeyPressed += OnWindowPreviewKeyPressed;

        UpdateLayoutWidths();

        LoadInitialFile();

        _fileTree?.CollapseAll();

        SetEditorMode(_config.Editor.StartInEditMode ? EditorMode.Edit : EditorMode.Browse);
        UpdateStatusBar();
    }

    static void UpdateTitle()
    {
        if (_ws == null) return;
        var name = _currentFile != null ? Path.GetFileName(_currentFile) : "untitled";
        _ws.StatusBarStateService.TopStatus = $" mide – {name} ";
    }

    static void UpdateStatusBar()
    {
        if (_statusBar == null || _editor == null) return;
        var ln    = _editor.CurrentLine;
        var col   = _editor.CurrentColumn;
        var mode  = _editor.IsEditing
            ? (_editor.OverwriteMode ? "[red]OVR[/]" : "[green]INS[/]")
            : "[dim]BROWSE[/]";
        var chars = _editor.Content?.Length ?? 0;
        var wrap  = _editor.WrapMode switch
        {
            WrapMode.NoWrap => "NoWrap",
            WrapMode.Wrap   => "Wrap",
            _               => "WordWrap"
        };
        var file = _currentFile != null
            ? $"[cyan]{EscapeMarkup(Path.GetFileName(_currentFile))}[/]"
            : "[dim]untitled[/]";

        _statusBar.SetContent(new List<string>
        {
            $" {file}   Ln [yellow]{ln}[/], Col [yellow]{col}[/] | {mode} | {chars} chars | {wrap} | [dim]Enter to edit · Esc to browse[/]"
        });
    }

    static void Notify(string title, string message, NotificationSeverity severity)
    {
        if (_ws == null) return;
        _ws.NotificationStateService.ShowNotification(title, message, severity);
    }

    static Color ParseColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        // named color lookup (case-insensitive)
        var prop = typeof(Color).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(Color));
        if (prop?.GetValue(null) is Color named) return named;

        // hex (#RRGGBB) support
        if (value.StartsWith('#'))
        {
            if (Color.TryFromHex(value, out var hex)) return hex;
        }

        return fallback;
    }
}
