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
        _editor.EditingModeChanged += (_, _) => { UpdateStatusBar(); UpdateTitle(); UpdateEditorVisuals(); };

        _editor.LineNumberColor = ParseColor(_config.Editor.LineNumberColor, new Color(200, 255, 200));

        _editorBrowseBg = ParseColor(_config.Editor.BrowseBackgroundColor, new Color(0, 26, 51));
        _editorEditBg   = ParseColor(_config.Editor.EditBackgroundColor,   new Color(0, 13, 26));
        _editorTreeBg   = ParseColor(_config.Editor.TreeBackgroundColor,   new Color(0,  8, 15));

        // BackgroundColor and FocusedBackgroundColor are set by SetEditorMode on each transition
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
                try
                {
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
                }
                finally { _suppressTreeEvent = false; }
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

        _mainWindow = window;

        _commandBar = new PromptControl
        {
            Prompt     = string.Empty,
            Input      = string.Empty,
            InputWidth = _ws.DesktopDimensions.Width - 2,
            StickyPosition = StickyPosition.Bottom,
            Visible    = false,
        };
        _commandBar.Entered += async (_, text) =>
        {
            DismissSuggestionPortal();
            try { await ExecuteCommand(text, BuildPathIndex()); }
            catch (Exception ex) { Notify("Error", ex.Message, NotificationSeverity.Danger); }
            HideCommandBar();
        };
        _commandBar.InputChanged += (_, input) => ShowSuggestionPortal(input);
        window.AddControl(_commandBar);

        _statusBar = Controls.StatusBar()
            .StickyBottom()
            .Build();

        _sbFile     = _statusBar.AddLeftText("untitled");
        _statusBar.AddLeftSeparator();
        _sbPosition = _statusBar.AddLeftText("Ln 1, Col 1");
        _statusBar.AddLeftSeparator();
        _sbMode     = _statusBar.AddCenterText("BROWSE",
            onClick: () => { if (_editor != null) _editor.OverwriteMode = !_editor.OverwriteMode; });
        _statusBar.AddRightSeparator();
        _sbChars    = _statusBar.AddRightText("0 chars");
        _statusBar.AddRightSeparator();
        _sbWrap     = _statusBar.AddRightText("WordWrap");
        _statusBar.AddRightSeparator();
        _sbHint = _statusBar.AddRight("Enter", "Edit",
            onClick: () =>
            {
                if (_editor?.IsEditing == true) SetEditorMode(EditorMode.Browse, focus: true);
                else                            SetEditorMode(EditorMode.Edit,   focus: true);
            });
        window.AddControl(_statusBar);

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
            ? (_editor.OverwriteMode ? "OVR" : "INS")
            : "BROWSE";
        var chars = _editor.Content?.Length ?? 0;
        var wrap  = _editor.WrapMode switch
        {
            WrapMode.NoWrap => "NoWrap",
            WrapMode.Wrap   => "Wrap",
            _               => "WordWrap"
        };
        var fileName = _currentFile != null
            ? Path.GetFileName(_currentFile)
            : "untitled";

        bool editing = _editor.IsEditing;
        _statusBar.BatchUpdate(() =>
        {
            if (_sbFile     != null) _sbFile.Label     = fileName;
            if (_sbPosition != null) _sbPosition.Label = $"Ln {ln}, Col {col}";
            if (_sbMode     != null) _sbMode.Label     = mode;
            if (_sbChars    != null) _sbChars.Label    = $"{chars} chars";
            if (_sbWrap     != null) _sbWrap.Label     = wrap;
            if (_sbHint != null)
            {
                _sbHint.Shortcut = editing ? "Esc"   : "Enter";
                _sbHint.Label    = editing ? "Browse" : "Edit";
            }
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
