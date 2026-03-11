using Spectre.Console;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;

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
    static bool _treeWasVisibleBeforeEdit = false;
    static Config _config = new();

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

            _ws.StatusBarStateService.TopStatus    = _config.App.TopStatusDefault;
            _ws.StatusBarStateService.BottomStatus = string.Empty;

            Console.CancelKeyPress += (_, e) => { e.Cancel = true; _ws?.Shutdown(0); };

            BuildIdeWindow(_ws);

            await Task.Run(() => _ws.Run());
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
            .WithSyntaxHighlighter(new IdeSyntaxHighlighter(IdeSyntaxHighlighter.Language.CSharp))
            .WrapWords()
            .IsEditing(_config.Editor.StartInEditMode)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .WithName("editor")
            .Build();

        _editor.ContentChanged        += (_, _) => UpdateStatusBar();
        _editor.CursorPositionChanged += (_, _) => UpdateStatusBar();
        _editor.OverwriteModeChanged  += (_, _) => UpdateStatusBar();
        _editor.EditingModeChanged    += (_, _) => UpdateStatusBar();

        _fileTree = new TreeControl { Name = "fileTree" };
        PopulateTree(_fileTree, _rootDir);
        ApplyTreeVisibility();

        _fileTree.NodeActivated += (_, e) =>
        {
            if (e.Node?.Tag is string path && File.Exists(path))
            {
                _suppressTreeEvent = true;
                OpenFile(path, fromTree: true, editMode: true, focusEditor: true);

                if (_treeVisible)
                {
                    _treeWasVisibleBeforeEdit = true;
                    _treeVisible = false;
                    ApplyTreeVisibility();
                }

                _fileTree?.SetFocus(false, FocusReason.Programmatic);
                ApplyEditingMode(true);
                FocusEditor(editing: true);
                _editor?.EnsureCursorVisible();
                _suppressTreeEvent = false;
            }
        };

        _fileTree.SelectedNodeChanged += (_, e) =>
        {
            if (_suppressTreeEvent) return;
            if (e.Node?.Tag is string path && File.Exists(path))
                OpenFile(path, fromTree: true, focusEditor: false);
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
            .WithBackgroundColor(Color.Grey11)
            .WithForegroundColor(Color.White)
            .AddControl(layout)
            .BuildAndShow();

        window.PreviewKeyPressed += OnWindowPreviewKeyPressed;

        UpdateLayoutWidths();

        LoadInitialFile();
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
}
