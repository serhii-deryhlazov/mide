using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;

namespace mide;

partial class Program
{
    // ── Command prompt (backtick) ───────────────────────────────────────
    static void OnWindowPreviewKeyPressed(object? sender, SharpConsoleUI.KeyPressedEventArgs e)
    {
        if (IsBacktick(e.KeyInfo))
        {
            e.Handled = true;
            ShowCommandPrompt();
            return;
        }

        // When tree is hidden, route all keys to the editor.
        if (!_treeVisible && _editor != null)
        {
            // Typing a printable character always enters edit mode.
            bool isPrintable = !char.IsControl(e.KeyInfo.KeyChar) && e.KeyInfo.KeyChar != '\0';
            if (isPrintable && !_editor.IsEditing)
            {
                _editor.IsEditing = true;
                _editor.HighlightCurrentLine = true;
            }

            // Only give focus and process keys if in editing mode.
            if (_editor.IsEditing)
            {
                if (!_editor.HasFocus)
                    _editor.SetFocus(true, FocusReason.Programmatic);
                _editor.ProcessKey(e.KeyInfo);
                e.Handled = true;
            }
            return;
        }
    }

    static bool IsBacktick(ConsoleKeyInfo key)
    {
        if (key.KeyChar == '`') return true;
        if (key.Key == ConsoleKey.Oem3) return true; // common backtick/tilde
        if (key.Key == ConsoleKey.Oem8) return true; // some layouts map backtick here
        return false;
    }

    static void ShowCommandPrompt()
    {
        if (_ws == null) return;
        int width = Math.Max(20, _ws.DesktopDimensions.Width - 2);

        var dialog = new WindowBuilder(_ws)
            .WithTitle("Command")
            .WithSize(width, 3)
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
            await OpenFileDialogAsync(editMode: true, focusEditor: true);
            FocusEditor(editing: true);
            return;
        }

        if (lower is "open" or "o")
        {
            await OpenFileDialogAsync(editMode: false, focusEditor: true);
            FocusEditor(editing: false);
            return;
        }

        if (lower.StartsWith("open ") || lower.StartsWith("o "))
        {
            var path = cmd[(cmd.IndexOf(' ') + 1)..].Trim();
            if (!Path.IsPathRooted(path)) path = Path.Combine(_rootDir, path);
            if (File.Exists(path))
                OpenFile(path, editMode: false, focusEditor: true);
            else
                Notify("Open", $"Not found: {path}", NotificationSeverity.Warning);
            return;
        }

        if (lower.StartsWith("edit ") || lower.StartsWith("e "))
        {
            var path = cmd[(cmd.IndexOf(' ') + 1)..].Trim();
            if (!Path.IsPathRooted(path)) path = Path.Combine(_rootDir, path);
            if (File.Exists(path))
            {
                OpenFile(path, editMode: true, focusEditor: true);
                FocusEditor(editing: true);
            }
            else
            {
                Notify("Edit", $"Not found: {path}", NotificationSeverity.Warning);
            }
            return;
        }

        if (lower is "new" or "n")
        {
            Notify("New", "Specify filename (new <name>)", NotificationSeverity.Warning);
            return;
        }

        if (lower is "save" or "s")
        {
            await SaveAsync(false);
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
                OpenFile(path);
                _editor!.IsEditing = true;
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
}
