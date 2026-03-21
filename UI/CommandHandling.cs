using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;

namespace mide;

partial class Program
{
    static void OnWindowPreviewKeyPressed(object? sender, SharpConsoleUI.KeyPressedEventArgs e)
    {
        if (IsBacktick(e.KeyInfo))
        {
            e.Handled = true;
            ShowCommandPrompt();
            return;
        }

        if (!_treeVisible && _editor != null)
        {
            bool isPrintable = !char.IsControl(e.KeyInfo.KeyChar) && e.KeyInfo.KeyChar != '\0';
            if (isPrintable && !_editor.IsEditing)
                SetEditorMode(EditorMode.Edit, focus: true);

            if (_editor.IsEditing
                && e.KeyInfo.Key == ConsoleKey.D
                && e.KeyInfo.Modifiers == ConsoleModifiers.Control)
            {
                e.Handled = true;
                DeleteCurrentLine();
                return;
            }

            if (!_editor.IsEditing && e.KeyInfo.Key == ConsoleKey.LeftArrow)
            {
                e.Handled = true;
                ToggleTree();
                return;
            }

            if (_editor.IsEditing || IsNavigationKey(e.KeyInfo))
            {
                if (!_editor.HasFocus)
                    _editor.SetFocus(true, FocusReason.Programmatic);
                _editor.ProcessKey(e.KeyInfo);
                e.Handled = true;
            }
            return;
        }

        if (_treeVisible && e.KeyInfo.KeyChar == 'd')
        {
            e.Handled = true;
            _ = DeleteSelectedTreeNodeAsync();
            return;
        }

        if (_treeVisible && e.KeyInfo.Key == ConsoleKey.RightArrow)
        {
            e.Handled = true;
            ToggleTree();
            return;
        }

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

    static void ShowCommandPrompt()
    {
        if (_ws == null) return;
        int width = Math.Max(_config.Layout.MinCommandWidth, _ws.DesktopDimensions.Width - 2);
    int height = _config.Dialogs.CommandHeight; // default height; we’ll expand visually only when suggestions show

    // bottom-centered placement
    int posX = Math.Max(0, (_ws.DesktopDimensions.Width - width) / 2);
    int posY = Math.Max(0, _ws.DesktopDimensions.Height - height - 1);

        // Precompute indexes once per prompt invocation
        var pathIndex = BuildPathIndex();
        var fileIndex = pathIndex
            .Where(p => !p.EndsWith("/"))
            .Select(p => Path.Combine(_rootDir, p.Replace('/', Path.DirectorySeparatorChar)))
            .ToList();

        var dialog = new WindowBuilder(_ws)
            .WithTitle("Command")
            .WithSize(width, height)
            .AtPosition(posX, posY)
            .AsModal()
            .Borderless()
            .HideTitle()
            .HideTitleButtons()
            .Build();

        dialog.PreviewKeyPressed += (_, e) =>
        {
            if (IsBacktick(e.KeyInfo))
            {
                e.Handled = true;
                _ws.CloseWindow(dialog);
            }
        };

        var prompt = new PromptControl
        {
            Prompt = string.Empty,
            UnfocusOnEnter = true,
            Input = string.Empty,
            InputWidth = width - 2
        };
        prompt.Entered += async (_, text) =>
        {
            await ExecuteCommand(text);
            _ws.CloseWindow(dialog);
        };
        dialog.AddControl(prompt);

    // suggestion popup (separate borderless modal that won't steal typing focus)
    Window? suggestionWindow = null;

        void CloseSuggestions()
        {
            if (_ws != null && suggestionWindow != null)
            {
                _ws.CloseWindow(suggestionWindow);
            }
            suggestionWindow = null;
        }

        void ShowSuggestions(string text)
        {
            if (_ws == null) return;
            if (string.IsNullOrEmpty(text))
            {
                CloseSuggestions();
                return;
            }

            var lines = text.Split('\n');
            int suggHeight = Math.Max(3, lines.Length + 1);

            CloseSuggestions();

            int suggX = posX;
            int suggY = Math.Max(0, posY - suggHeight - 1);

            suggestionWindow = new WindowBuilder(_ws)
                .WithTitle(string.Empty)
                .WithSize(width, suggHeight)
                .AtPosition(suggX, suggY)
                .Borderless()
                .HideTitle()
                .HideTitleButtons()
                .Build();

            suggestionWindow.AddControl(Controls.Label(text));
            suggestionWindow.PreviewKeyPressed += (_, e) =>
            {
                e.Handled = true;              // prevent suggestion window from consuming keys
                prompt.SetFocus(true, FocusReason.Programmatic);
            };
            _ws.AddWindow(suggestionWindow);

            // keep typing focus on the main prompt
            prompt.SetFocus(true, FocusReason.Programmatic);
        }

        dialog.KeyPressed += (_, e) =>
        {
            // Let the prompt handle input; we only mirror the text to drive suggestions.
            var current = prompt.Input ?? string.Empty;

            // Preview the character that's about to be added/removed.
            if (!char.IsControl(e.KeyInfo.KeyChar) && e.KeyInfo.KeyChar != '\0')
                current += e.KeyInfo.KeyChar;
            else if (e.KeyInfo.Key == ConsoleKey.Backspace && current.Length > 0)
                current = current[..^1];

            var text = BuildSuggestions(current, pathIndex, fileIndex);
            ShowSuggestions(text);
        };

        dialog.OnClosed += (_, _) => CloseSuggestions();

        _ws.AddWindow(dialog);
    }

    static async Task ExecuteCommand(string text)
    {
        var cmd = text.Trim();

        if (string.IsNullOrEmpty(cmd)) return;

        var lower = cmd.ToLowerInvariant();

        if (lower is "tree" or "t")
        {
            ToggleTree();
            Notify("Tree", _treeVisible ? "Shown" : "Hidden", NotificationSeverity.Info);
            return;
        }

        if (lower is "edit" or "e")
        {
            await OpenFileDialogAsync(EditorMode.Edit);
            return;
        }

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

        if (lower is "new" or "n")
        {
            Notify("New", "Specify filename (new <name>)", NotificationSeverity.Warning);
            return;
        }

        if (lower is "save" or "s")
        {
            if (_editor?.IsEditing != true)
            {
                Notify("Save", "Not in edit mode", NotificationSeverity.Warning);
                return;
            }
            await SaveAsync(false);
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

            // Treat leading '/' as workspace-relative (not absolute root) to avoid permission issues on macOS/Linux.
            bool leadingSeparator = name.StartsWith(Path.DirectorySeparatorChar) || name.StartsWith(Path.AltDirectorySeparatorChar);
            var rawPath = leadingSeparator
                ? Path.Combine(_rootDir, name.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : (Path.IsPathRooted(name) ? name : Path.Combine(_rootDir, name));

            // Use the original token (not the rooted version) for intent checks so we respect trailing separators and extensions.
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
                    Notify("New", $"Folder: {normalized}", NotificationSeverity.Success);
                    RefreshTree(normalized);
                }
                else
                {
                    var parent = Path.GetDirectoryName(normalized);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    File.WriteAllText(normalized, string.Empty);
                    Notify("New", Path.GetFileName(normalized), NotificationSeverity.Success);
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

        // :line  |  :line:col  |  :line:e
        if (cmd.StartsWith(':'))
        {
            if (_editor?.IsEditing != true)
            {
                Notify("Go to", "Not in edit mode", NotificationSeverity.Warning);
                return;
            }
            var parts = cmd[1..].Split(':');
            if (int.TryParse(parts[0], out int line) && line >= 1)
            {
                _editor.GoToLine(line);

                if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                {
                    var lines = (_editor.Content ?? "").Split('\n');
                    int lineLength = line <= lines.Length ? lines[line - 1].Length : 0;

                    int col; // 0-based for SetLogicalCursorPosition
                    if (parts[1].Equals("e", StringComparison.OrdinalIgnoreCase))
                        col = lineLength;                                         // after last char
                    else if (int.TryParse(parts[1], out int parsedCol) && parsedCol >= 1)
                        col = Math.Min(parsedCol - 1, lineLength);               // 1-based → 0-based
                    else
                        col = 0;

                    _editor.SetLogicalCursorPosition(new System.Drawing.Point(col, line - 1));
                    Notify("Go to", $"Ln {line}, Col {col + 1}", NotificationSeverity.Info);
                }
                else
                {
                    Notify("Go to", $"Ln {line}", NotificationSeverity.Info);
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
        int lineIdx = _editor.CurrentLine - 1; // CurrentLine is 1-based

        if (lineIdx < 0 || lineIdx >= lines.Count) return;

        lines.RemoveAt(lineIdx);

        // Keep cursor on the same line index (or the last line if we deleted the last one)
        int targetLine = Math.Min(lineIdx + 1, Math.Max(lines.Count, 1));

        _editor.Content = string.Join('\n', lines);
        _editor.GoToLine(targetLine);
    }

    static string BuildSuggestions(string input, List<string> pathIndex, List<string> fileIndex)
    {
        if (input.StartsWith('/'))
        {
            var term = input[1..];
            if (term.Length < 3) return string.Empty;

            return string.Join('\n', pathIndex
                .Where(p => p.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(p => "/" + p.Replace(Path.DirectorySeparatorChar, '/')));
        }
        else if (input.StartsWith('>'))
        {
            var term = input[1..];
            if (term.Length < 3) return string.Empty;

            return string.Join('\n', GetContentSuggestions(term, fileIndex, 5)
                .Select(r => "/" + r.Replace(Path.DirectorySeparatorChar, '/')));
        }

        return string.Empty;
    }

    static List<string> BuildPathIndex()
    {
        var list = new List<string>();
        if (!Directory.Exists(_rootDir)) return list;

        foreach (var dir in Directory.EnumerateDirectories(_rootDir, "*", SearchOption.AllDirectories))
        {
            var rel = ToRelative(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrEmpty(rel)) list.Add(rel + "/");
        }

        foreach (var file in Directory.EnumerateFiles(_rootDir, "*", SearchOption.AllDirectories))
        {
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
                    if (line.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var rel = ToRelative(file);
                        results.Add($"{rel}:{lineNo}");
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
