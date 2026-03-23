using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;

namespace mide;

partial class Program
{
    static ConsoleWindowSystem? _ws;
    static MultilineEditControl? _editor;
    static TreeControl? _fileTree;
    static HorizontalGridControl? _layout;
    static StatusBarControl? _statusBar;
    static StatusBarItem?    _sbFile;
    static StatusBarItem?    _sbPosition;
    static StatusBarItem?    _sbMode;
    static StatusBarItem?    _sbChars;
    static StatusBarItem?    _sbWrap;
    static StatusBarItem?    _sbHint;
    static string? _currentFile;
    static string _rootDir = Directory.GetCurrentDirectory();
    static bool _treeVisible = false;
    static bool _suppressTreeEvent = false;
    static EditorMode _modeBeforeTree = EditorMode.Browse;
    static Config _config = new();

    enum EditorMode { Browse, Edit }
    static Color _editorBrowseBg;
    static Color _editorEditBg;
    static Color _editorTreeBg;
    static CancellationTokenSource _treeCts = new();
    static readonly HashSet<string> _expandedPaths = new(StringComparer.Ordinal);

    static Window?        _mainWindow;
    static PromptControl? _commandBar;
    static bool           _commandBarVisible = false;
    static LayoutNode?    _suggestionPortalNode;
    static CommandSuggestionPortal? _suggestionPortal;

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

    static int CountLines(string? s) =>
        string.IsNullOrEmpty(s) ? 1 : s.Count(c => c == '\n') + 1;

    static string EscapeMarkup(string s) =>
        s.Replace("[", "[[").Replace("]", "]]");
}
