using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;

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

        var dialog = new WindowBuilder(_ws)
            .WithTitle("Command")
            .WithSize(width, _config.Dialogs.CommandHeight)
            .Centered()
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

        _ws.AddWindow(dialog);
    }

    static async Task ExecuteCommand(string text)
    {
        var cmd = text.Trim();
        if (string.IsNullOrEmpty(cmd)) return;

        var lower = cmd.ToLowerInvariant();

        if (lower is "tree" or "t" or "toggle tree" or "toggle")
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

        if (lower.StartsWith("new ") || lower.StartsWith("n "))
        {
            var name = cmd[(cmd.IndexOf(' ') + 1)..].Trim();
            if (string.IsNullOrEmpty(name))
            {
                Notify("New", "Specify filename", NotificationSeverity.Warning);
                return;
            }
            var path = Path.IsPathRooted(name) ? name : Path.Combine(_rootDir, name);
            try
            {
                File.WriteAllText(path, string.Empty);
                OpenFile(path, mode: EditorMode.Edit, focus: true);
                Notify("New", Path.GetFileName(path), NotificationSeverity.Success);
            }
            catch (Exception ex)
            {
                Notify("New", ex.Message, NotificationSeverity.Danger);
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
}
