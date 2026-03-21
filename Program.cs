using Spectre.Console;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using System.Reflection;

namespace mide;

partial class Program
{
    static ConsoleWindowSystem? _ws;
    static MultilineEditControl? _editor;
    static TreeControl? _fileTree;
    static HorizontalGridControl? _layout;
    static MarkupControl? _statusBar;
    static string? _currentFile;
    static string _rootDir = Directory.GetCurrentDirectory();
    static bool _treeVisible = false;
    static bool _suppressTreeEvent = false;
    static EditorMode _modeBeforeTree = EditorMode.Browse;
    static Config _config = new();

    enum EditorMode { Browse, Edit }
    static Color _editorBrowseBg;
    static CancellationTokenSource _treeCts = new();
    static readonly HashSet<string> _expandedPaths = new(StringComparer.Ordinal);

    static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && Directory.Exists(args[0]))
            _rootDir = Path.GetFullPath(args[0]);

        _config = Config.Load();
        _treeVisible = _config.Tree.VisibleOnStart;

        try
        {
            _ws = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    StatusBarOptions: new StatusBarOptions(ShowTaskBar: false)));

            _ws.StatusBarStateService.TopStatus = _config.App.TopStatusDefault;
            _ws.StatusBarStateService.BottomStatus = string.Empty;

            Console.CancelKeyPress += (_, e) => { e.Cancel = true; _ws?.Shutdown(0); _treeCts.Cancel(); };

            BuildIdeWindow(_ws);

            var refreshTask = StartTreeRefreshLoop(_treeCts.Token);
            await Task.Run(() => _ws.Run());
            _treeCts.Cancel();
            await refreshTask;
            return 0;
        }
        catch (Exception ex)
        {
            Console.Clear();
            Console.WriteLine($"Fatal: {ex.Message}\n{ex.StackTrace}");
            return 1;
        }
    }

    static void BuildIdeWindow(ConsoleWindowSystem ws)
    {
        _editor = Controls.MultilineEdit(Welcome)
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

        _editorBrowseBg  = ParseColor(_config.Editor.BrowseBackgroundColor, Color.FromHex("#001a33"));
        _editor.CurrentLineHighlightColor = ParseColor(_config.Editor.CurrentLineHighlightColor, Color.FromHex("#008b8b"));

        _fileTree = new TreeControl { Name = "fileTree" };
        PopulateTree(_fileTree, _rootDir);
        ApplyTreeVisibility();

        // Keeping folders expansion/collapse state
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
            .WithBackgroundColor(ParseColor(_config.Editor.BackgroundColor, Color.FromHex("#001a33")))
            .WithForegroundColor(ParseColor(_config.Editor.ForegroundColor, Color.FromHex("#fffdf5")))
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
    }

    static int CountLines(string? s) =>
        string.IsNullOrEmpty(s) ? 1 : s.Count(c => c == '\n') + 1;

    static string EscapeMarkup(string s) =>
        s.Replace("[", "[[").Replace("]", "]]");

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
            try { return Color.FromHex(value); } 
            catch { return fallback; }
        }

        return fallback;
    }
}
