using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;

namespace mide;

partial class Program
{
    static void OnWindowPreviewKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        // Backtick toggles command bar
        if (IsBacktick(e.KeyInfo))
        {
            e.Handled = true;
            if (_commandBarVisible) HideCommandBar();
            else ShowCommandBar();
            return;
        }

        // When command bar is active, handle Escape + portal navigation
        if (_commandBarVisible)
        {
            if (e.KeyInfo.Key == ConsoleKey.Escape)
            {
                e.Handled = true;
                if (_suggestionPortal?.IsFocused == true)
                    _suggestionPortal.SetFocused(false);  // Esc in portal → back to prompt
                else
                    HideCommandBar();                     // Esc in prompt → close bar
                return;
            }
            if (_suggestionPortal != null)
            {
                if (e.KeyInfo.Key == ConsoleKey.DownArrow)
                {
                    e.Handled = true;
                    _suggestionPortal.SelectNext();
                    return;
                }
                if (e.KeyInfo.Key == ConsoleKey.UpArrow)
                {
                    e.Handled = true;
                    if (!_suggestionPortal.SelectPrev())
                    {
                        // Was at top — focus returned to prompt by SelectPrev
                        _mainWindow?.FocusControl(_commandBar!);
                    }
                    return;
                }
                if (_suggestionPortal.IsFocused &&
                    (e.KeyInfo.Key == ConsoleKey.Enter || e.KeyInfo.Key == ConsoleKey.Tab))
                {
                    e.Handled = true;
                    var sel = _suggestionPortal.GetSelected();
                    if (sel != null && _commandBar != null)
                        _commandBar.Input = sel;
                    return;
                }
            }
            // Do NOT route keys to editor/tree when command bar is visible
            return;
        }

        if (!_treeVisible && _editor != null)
        {
            bool isPrintable = !char.IsControl(e.KeyInfo.KeyChar) && e.KeyInfo.KeyChar != '\0';
            if (isPrintable && !_editor.IsEditing)
                SetEditorMode(EditorMode.Edit, focus: true);

            // Ctrl+D to delete current line (when editing)
            if (_editor.IsEditing
                && e.KeyInfo.Key == ConsoleKey.D
                && e.KeyInfo.Modifiers == ConsoleModifiers.Control)
            {
                e.Handled = true;
                DeleteCurrentLine();
                return;
            }

            // Open tree with LeftArrow (when not editing)
            if (!_editor.IsEditing && e.KeyInfo.Key == ConsoleKey.LeftArrow)
            {
                e.Handled = true;
                ToggleTree();
                return;
            }

            // For other keys - let editor handle them
            if (_editor.IsEditing || IsNavigationKey(e.KeyInfo))
            {
                if (!_editor.HasFocus)
                    _editor.SetFocus(true, FocusReason.Programmatic);
                _editor.ProcessKey(e.KeyInfo);
                e.Handled = true;
            }
            return;
        }

        // Delete file/folder with Ctrl+D (when tree is visible)
        if (_treeVisible && e.KeyInfo.KeyChar == 'd' && e.KeyInfo.Modifiers == ConsoleModifiers.Control)
        {
            e.Handled = true;
            _ = DeleteSelectedTreeNodeAsync();
            return;
        }

        // Close tree with RightArrow (when tree is visible)
        if (_treeVisible && e.KeyInfo.Key == ConsoleKey.RightArrow)
        {
            e.Handled = true;
            ToggleTree();
            return;
        }

        // Handle tree navigation keys (when tree is visible)
        if (_treeVisible && _fileTree != null && IsTreeKey(e.KeyInfo))
        {
            if (!_fileTree.HasFocus)
                _fileTree.SetFocus(true, FocusReason.Programmatic);
            _fileTree.ProcessKey(e.KeyInfo);
            e.Handled = true;
            return;
        }
    }

    static bool IsNavigationKey(ConsoleKeyInfo key) =>
        key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow
                or ConsoleKey.PageUp or ConsoleKey.PageDown
                or ConsoleKey.Home or ConsoleKey.End;

    static bool IsTreeKey(ConsoleKeyInfo key) =>
        key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow
                or ConsoleKey.PageUp or ConsoleKey.PageDown
                or ConsoleKey.Home or ConsoleKey.End
                or ConsoleKey.Enter or ConsoleKey.Spacebar
                or ConsoleKey.LeftArrow;

    static bool IsBacktick(ConsoleKeyInfo key)
    {
        if (key.KeyChar == '`') return true;
        if (key.Key == ConsoleKey.Oem3) return true;
        if (key.Key == ConsoleKey.Oem8) return true;
        return false;
    }

    // ── Command bar ──────────────────────────────────────────────────────────

    private static readonly List<(string Display, string Completion)> _allCommands = new()
    {
        ("open <path>   open file (browse)",  "open "),
        ("edit <path>   open file (edit)",    "edit "),
        ("new  <name>   create new file",     "new "),
        ("save          save current file",   "save"),
        ("tree          toggle file tree",    "tree"),
        (":line         go to line",          ":"),
        ("/path         find file by path",   "/"),
        (">term         search in files",     ">"),
    };

    static void ShowCommandBar()
    {
        if (_commandBar == null || _mainWindow == null) return;
        _commandBarVisible  = true;
        _commandBar.Input   = string.Empty;
        _commandBar.Visible = true;
        if (_statusBar != null) _statusBar.Visible = false;
        _mainWindow.FocusControl(_commandBar);
        ShowSuggestionPortal(string.Empty);
    }

    static void HideCommandBar()
    {
        DismissSuggestionPortal();
        _commandBarVisible = false;
        if (_commandBar != null) _commandBar.Visible = false;
        if (_statusBar != null) _statusBar.Visible = true;
        if (!_treeVisible && _editor != null)
            _editor.SetFocus(true, FocusReason.Programmatic);
        else if (_treeVisible && _fileTree != null)
            _fileTree.SetFocus(true, FocusReason.Programmatic);
    }

    static void DismissSuggestionPortal()
    {
        if (_suggestionPortalNode != null && _mainWindow != null && _commandBar != null)
            _mainWindow.RemovePortal(_commandBar, _suggestionPortalNode);
        _suggestionPortalNode = null;
        _suggestionPortal     = null;
    }

    static void ShowSuggestionPortal(string input)
    {
        if (_commandBar == null || _mainWindow == null || _ws == null) return;

        var pathIndex = BuildPathIndex();
        var fileIndex = pathIndex
            .Where(p => !p.EndsWith("/"))
            .Select(p => Path.Combine(_rootDir, p.Replace('/', Path.DirectorySeparatorChar)))
            .ToList();

        var suggestions = BuildSuggestionList(input, pathIndex, fileIndex);

        // If portal already exists, just update its items (avoids flicker)
        if (_suggestionPortal != null)
        {
            if (suggestions.Count == 0) { DismissSuggestionPortal(); return; }
            _suggestionPortal.UpdateItems(suggestions);
            _mainWindow.FocusControl(_commandBar);
            return;
        }

        if (suggestions.Count == 0) return;

        int anchorX = _commandBar.ActualX;
        int anchorY = (_commandBar.ActualY > 0
            ? _commandBar.ActualY
            : _ws.DesktopDimensions.Height - 1) - 2;

        _suggestionPortal = new CommandSuggestionPortal(
            suggestions, anchorX, anchorY,
            _ws.DesktopDimensions.Width, _ws.DesktopDimensions.Height);

        _suggestionPortal.ItemAccepted += (_, item) =>
        {
            if (_commandBar != null) _commandBar.Input = item;
            // InputChanged will re-filter portal automatically
        };

        _suggestionPortalNode = _mainWindow.CreatePortal(_commandBar, _suggestionPortal);
        _mainWindow.FocusControl(_commandBar);
    }

    static List<(string Display, string Completion)> BuildSuggestionList(
        string input, List<string> pathIndex, List<string> fileIndex)
    {
        // Empty input → show all commands
        if (string.IsNullOrEmpty(input))
            return _allCommands.ToList();

        // "open <term>" / "o <term>" / "edit <term>" / "e <term>" → file suggestions
        var lower = input.ToLowerInvariant();
        if (lower.StartsWith("open ") || lower.StartsWith("o ")
         || lower.StartsWith("edit ") || lower.StartsWith("e "))
        {
            var term = input[(input.IndexOf(' ') + 1)..];
            return pathIndex
                .Where(p => !p.EndsWith("/") && p.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .Select(p =>
                {
                    var rel = p.Replace(Path.DirectorySeparatorChar, '/');
                    return (rel, Path.Combine(_rootDir, p.Replace('/', Path.DirectorySeparatorChar)));
                })
                .ToList();
        }

        if (input.StartsWith('/'))
        {
            var term = input[1..];
            return pathIndex
                .Where(p => p.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .Select(p =>
                {
                    var display = "/" + p.Replace(Path.DirectorySeparatorChar, '/');
                    return (display, display);
                })
                .ToList();
        }

        if (input.StartsWith('>') && input.Length > 1)
        {
            var term = input[1..];
            return GetContentSuggestions(term, fileIndex, 8)
                .Select(r =>
                {
                    var display = r.Replace(Path.DirectorySeparatorChar, '/');
                    var filePart = display.Contains(':') ? display[..display.IndexOf(':')] : display;
                    return (display, filePart);
                })
                .ToList();
        }

        // Command suggestions — filter by typed text
        return _allCommands
            .Where(c => c.Completion.StartsWith(input, StringComparison.OrdinalIgnoreCase)
                     || c.Display.Contains(input, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // ── Command execution ────────────────────────────────────────────────────

    static async Task ExecuteCommand(string text, List<string> pathIndex)
    {
        var cmd = text.Trim();

        if (string.IsNullOrEmpty(cmd)) return;

        var lower = cmd.ToLowerInvariant();

        if (lower is "open" or "o")
        {
            await OpenFileDialogAsync(EditorMode.Browse);
            return;
        }

        if (lower.StartsWith("open ") || lower.StartsWith("o "))
        {
            var path = cmd[(cmd.IndexOf(' ') + 1)..].Trim();

            if (!Path.IsPathRooted(path)) path = Path.Combine(_rootDir, path);

            if (File.Exists(path))
                OpenFile(path, mode: EditorMode.Browse, focus: true);
            else
                Notify("Open", $"Not found: {path}", NotificationSeverity.Warning);
            return;
        }

        if (lower.StartsWith("/"))
        {
            var pathPart = cmd[1..];

            string? match = pathIndex.Where(p => p.Contains(pathPart, StringComparison.OrdinalIgnoreCase))
                .Take(5).Select(p => "/" + p.Replace(Path.DirectorySeparatorChar, '/')).FirstOrDefault();

            if (string.IsNullOrEmpty(match)) return;

            OpenFile(Path.Combine(_rootDir, match.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)), mode: EditorMode.Browse, focus: true);

            return;
        }

        if (lower.StartsWith(">"))
        {
            var term = cmd[1..].Trim();
            if (string.IsNullOrEmpty(term)) return;

            var fileIndex = pathIndex
                .Where(p => !p.EndsWith("/"))
                .Select(p => Path.Combine(_rootDir, p.Replace('/', Path.DirectorySeparatorChar)))
                .ToList();

            var hit = GetContentSuggestions(term, fileIndex, 1).FirstOrDefault();
            if (hit == null) { Notify("Find", $"Not found: {term}", NotificationSeverity.Info); return; }

            // hit format: "rel/path:lineNo ..."
            var colon = hit.IndexOf(':');
            var relPath = colon > 0 ? hit[..colon] : hit;
            int lineNo = 1;
            if (colon > 0 && int.TryParse(hit[(colon + 1)..].Split(' ')[0], out int ln)) lineNo = ln;

            OpenFile(Path.Combine(_rootDir, relPath.Replace('/', Path.DirectorySeparatorChar)), mode: EditorMode.Browse, focus: true);
            _editor?.GoToLine(lineNo);
            return;
        }

        if (lower is "edit" or "e")
        {
            await OpenFileDialogAsync(EditorMode.Edit);
            return;
        }

        if (lower.StartsWith("edit ") || lower.StartsWith("e "))
        {
            var path = cmd[(cmd.IndexOf(' ') + 1)..].Trim();
            if (!Path.IsPathRooted(path)) path = Path.Combine(_rootDir, path);
            if (File.Exists(path))
                OpenFile(path, mode: EditorMode.Edit, focus: true);
            else
                Notify("Edit", $"Not found: {path}", NotificationSeverity.Warning);
            return;
        }

        if (lower is "tree" or "t" or "toggle")
        {
            ToggleTree();
            return;
        }

        if (lower is "save" or "s")
        {
            if (_editor?.IsEditing != true)
            {
                Notify("Save", "Switch to edit mode first (press Enter)", NotificationSeverity.Warning);
                return;
            }
            await SaveAsync(false);
            return;
        }

        if (lower is "new" or "n")
        {
            Notify("New", "Specify filename (new <name>)", NotificationSeverity.Warning);
            return;
        }

        if (lower.StartsWith("new ") || lower.StartsWith("n "))
        {
            var name = cmd[(cmd.IndexOf(' ') + 1)..].Trim();
            if (string.IsNullOrEmpty(name))
            {
                Notify("New", "Specify path", NotificationSeverity.Warning);
                return;
            }

            bool leadingSeparator = name.StartsWith(Path.DirectorySeparatorChar) || name.StartsWith(Path.AltDirectorySeparatorChar);
            var rawPath = leadingSeparator
                ? Path.Combine(_rootDir, name.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : (Path.IsPathRooted(name) ? name : Path.Combine(_rootDir, name));

            bool endsWithSep = name.EndsWith(Path.DirectorySeparatorChar) || name.EndsWith(Path.AltDirectorySeparatorChar);
            bool hasExt = Path.HasExtension(name);
            bool hasSeparator = name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar);
            bool treatAsDir = endsWithSep || (!hasExt && hasSeparator);

            var normalized = Path.GetFullPath(rawPath);

            try
            {
                if (treatAsDir)
                {
                    Directory.CreateDirectory(normalized);
                    RefreshTree(normalized);
                }
                else
                {
                    var parent = Path.GetDirectoryName(normalized);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    File.WriteAllText(normalized, string.Empty);
                    RefreshTree(normalized);
                    OpenFile(normalized, mode: EditorMode.Edit, focus: true);
                }
            }
            catch (Exception ex)
            {
                Notify("New", ex.Message, NotificationSeverity.Danger);
            }

            return;
        }

        if (lower.StartsWith("f ") || lower.StartsWith("find "))
        {
            if (_editor?.IsEditing != true)
                return;

            var subj = cmd[(cmd.IndexOf(' ') + 1)..].Trim();

            if (_editor.Content.IndexOf(subj) is int idx && idx >= 0)
            {
                int line = CountLines(_editor.Content[..idx]);
                _editor.GoToLine(line);
                _editor.SetFocus(true, FocusReason.Programmatic);
            }
            else
            {
                Notify("Find", $"Not found: {subj}", NotificationSeverity.Info);
            }
            return;
        }

        // :line  |  :line:col  |  :line:e
        if (cmd.StartsWith(':'))
        {
            if (_editor == null) return;
            var parts = cmd[1..].Split(':');
            if (int.TryParse(parts[0], out int line) && line >= 1)
            {
                _editor.GoToLine(line);

                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                {
                    var lines = (_editor.Content ?? "").Split('\n');
                    int lineLength = line <= lines.Length ? lines[line - 1].Length : 0;

                    int col;
                    if (parts[1].Equals("e", StringComparison.OrdinalIgnoreCase))
                        col = lineLength;
                    else if (int.TryParse(parts[1], out int parsedCol) && parsedCol >= 1)
                        col = Math.Min(parsedCol - 1, lineLength);
                    else
                        col = 0;

                    _editor.SetLogicalCursorPosition(new System.Drawing.Point(col, line - 1));
                }
            }
            else
            {
                Notify("Go to", "Usage: :line  :line:col  :line:e", NotificationSeverity.Warning);
            }
            return;
        }

        Notify("Command", "Unknown command", NotificationSeverity.Warning);
    }

    static void DeleteCurrentLine()
    {
        if (_editor == null) return;

        var content = _editor.Content ?? string.Empty;
        var lines   = content.Split('\n').ToList();
        int lineIdx = _editor.CurrentLine - 1;

        if (lineIdx < 0 || lineIdx >= lines.Count) return;

        lines.RemoveAt(lineIdx);

        int targetLine = Math.Min(lineIdx + 1, Math.Max(lines.Count, 1));

        _editor.Content = string.Join('\n', lines);
        _editor.GoToLine(targetLine);
    }

    static List<string> BuildPathIndex()
    {
        var list = new List<string>();

        if (!Directory.Exists(_rootDir)) return list;

        var forbidden = new HashSet<string>(_config.Tree.IgnoredDirs, StringComparer.OrdinalIgnoreCase);

        bool IsForbidden(string path) =>
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => forbidden.Contains(part));

        foreach (var dir in Directory.EnumerateDirectories(_rootDir, "*", SearchOption.AllDirectories))
        {
            if (IsForbidden(Path.GetRelativePath(_rootDir, dir))) continue;
            var rel = ToRelative(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrEmpty(rel)) list.Add(rel + "/");
        }

        foreach (var file in Directory.EnumerateFiles(_rootDir, "*", SearchOption.AllDirectories))
        {
            if (IsForbidden(Path.GetRelativePath(_rootDir, file))) continue;
            var rel = ToRelative(file);
            if (!string.IsNullOrEmpty(rel)) list.Add(rel);
        }

        return list;
    }

    static IEnumerable<string> GetContentSuggestions(string term, List<string> files, int max)
    {
        var results = new List<string>();
        foreach (var file in files)
        {
            if (results.Count >= max) break;
            try
            {
                int lineNo = 0;
                foreach (var line in File.ReadLines(file))
                {
                    lineNo++;
                    int index = line.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        var rel   = ToRelative(file);
                        int start = Math.Max(0, index - 30);
                        int end   = Math.Min(line.Length, index + 30);
                        results.Add($"{rel}:{lineNo} ..{line[start..end].Trim()}..");
                        break;
                    }
                }
            }
            catch { /* ignore unreadable files */ }
        }
        return results;
    }

    static string ToRelative(string path)
    {
        var rel = Path.GetRelativePath(_rootDir, path);
        return rel.Replace(Path.DirectorySeparatorChar, '/');
    }
}
