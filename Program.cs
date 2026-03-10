#nullable disable warnings
using Spectre.Console;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
#nullable restore warnings

namespace mide;

partial class Program
{
    // ── IDE state ──────────────────────────────────────────────────────────
    static ConsoleWindowSystem? _ws;
    static MultilineEditControl? _editor;
    static TreeControl? _fileTree;
    static HorizontalGridControl? _layout;
    static MarkupControl? _statusBar;
    static string? _currentFile;
    static string _rootDir = Directory.GetCurrentDirectory();
    static bool _treeVisible = false;
    static bool _suppressTreeEvent = false;

    // ── Welcome screen is defined in WelcomeText.cs ──────────────────────

    static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && Directory.Exists(args[0]))
            _rootDir = Path.GetFullPath(args[0]);

        try
        {
            _ws = new ConsoleWindowSystem(
                new NetConsoleDriver(RenderMode.Buffer),
                options: new ConsoleWindowSystemOptions(
                    StatusBarOptions: new StatusBarOptions(ShowTaskBar: false)));

            _ws.StatusBarStateService.TopStatus    = " mide ";
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

    // ── Main IDE window ───────────────────────────────────────────────────
    static void BuildIdeWindow(ConsoleWindowSystem ws)
    {
        // ─ Editor ─
        _editor = Controls.MultilineEdit(Welcome)
            .WithLineNumbers(true)
            .WithHighlightCurrentLine(true)
            .WithAutoIndent(true)
            .WithSyntaxHighlighter(new IdeSyntaxHighlighter(IdeSyntaxHighlighter.Language.CSharp))
            .WrapWords()
            .IsEditing(false)
            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .WithName("editor")
            .Build();

        _editor.ContentChanged        += (_, _) => UpdateStatusBar();
        _editor.CursorPositionChanged += (_, _) => UpdateStatusBar();
        _editor.OverwriteModeChanged  += (_, _) => UpdateStatusBar();
        _editor.EditingModeChanged    += (_, _) => UpdateStatusBar();

        // ─ File tree ─
        _fileTree = new TreeControl { Name = "fileTree" };
        PopulateTree(_fileTree, _rootDir);
        ApplyTreeVisibility();

        _fileTree.NodeActivated += (_, e) =>
        {
            if (e.Node?.Tag is string path && File.Exists(path))
            {
                _suppressTreeEvent = true;
                OpenFile(path, fromTree: true, editMode: true, focusEditor: true);

                // Hide tree so arrows stay in editor
                if (_treeVisible)
                {
                    _treeVisible = false;
                    ApplyTreeVisibility();
                }

                _fileTree?.SetFocus(false, FocusReason.Programmatic);
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

        // (Status bar intentionally omitted per user request)

        // ─ Layout: [tree col | editor col] ─
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

    // ── Menu ──────────────────────────────────────────────────────────────
    static MenuControl BuildMenu(ConsoleWindowSystem ws)
    {
        var menu = Controls.Menu()
            .Horizontal()
            .Sticky()
            .WithName("mainMenu")
            .AddItem("File", m => m
                .AddItem("New",            "Ctrl+N", NewFile)
                .AddItem("Open...",        "Ctrl+O", () => _ = OpenFileDialogAsync())
                .AddItem("Open Folder...", null,     () => _ = OpenFolderDialogAsync())
                .AddItem("Save",           "Ctrl+S", () => _ = SaveAsync(false))
                .AddItem("Save As...",     null,     () => _ = SaveAsync(true))
                .AddSeparator()
                .AddItem("Exit",           "Ctrl+Q", () => ws.Shutdown(0)))
            .AddItem("Edit", m => m
                .AddItem("Undo",           "Ctrl+Z", () => { })
                .AddItem("Redo",           "Ctrl+Y", () => { })
                .AddSeparator()
                .AddItem("Find...",        "Ctrl+F", () => _ = ShowFindDialogAsync())
                .AddItem("Go to Line...",  "Ctrl+G", () => _ = ShowGotoDialogAsync()))
            .AddItem("View", m => m
                .AddItem("Toggle File Tree",    "Ctrl+B", ToggleTree)
                .AddItem("Toggle Line Numbers", null,     () => { if (_editor != null) _editor.ShowLineNumbers = !_editor.ShowLineNumbers; })
                .AddItem("Toggle Word Wrap",    null,     () =>
                {
                    if (_editor != null)
                        _editor.WrapMode = _editor.WrapMode == WrapMode.NoWrap
                            ? WrapMode.WrapWords : WrapMode.NoWrap;
                })
                .AddSeparator()
                .AddItem("Theme: Classic",    null, () => ws.ThemeStateService.SwitchTheme("Classic"))
                .AddItem("Theme: ModernGray", null, () => ws.ThemeStateService.SwitchTheme("ModernGray")))
            .AddItem("Help", m => m
                .AddItem("About", "F1", ShowAbout))
            .Build();

        menu.StickyPosition = StickyPosition.Top;
        return menu;
    }


    // ── About ─────────────────────────────────────────────────────────────
    static void ShowAbout()
    {
        if (_ws == null) return;
        var dialog = new WindowBuilder(_ws)
            .WithTitle("About mide")
            .WithSize(46, 12)
            .Centered()
            .AsModal()
            .Build();

        dialog.AddControl(new MarkupControl(new List<string>
        {
            "[bold yellow]mide[/] – terminal IDE",
            "",
            "Built with [bold cyan]SharpConsoleUI[/] v2.4.40",
            "and [bold cyan].NET 9[/]",
            "",
            "Supports: C#, Python, JS/TS, JSON, Markdown",
            "",
            "[dim]https://github.com/nickprotop/ConsoleEx[/]"
        }));

        dialog.AddControl(Controls.Button("Close").WithWidth(10)
            .OnClick((_, _, _) => _ws.CloseWindow(dialog))
            .Build());

        _ws.AddWindow(dialog);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
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
        // Notifications disabled to avoid popup windows per user request.
    }

    static int CountLines(string? s) =>
        string.IsNullOrEmpty(s) ? 1 : s.Count(c => c == '\n') + 1;

    static string EscapeMarkup(string s) =>
        s.Replace("[", "[[").Replace("]", "]]");
}
