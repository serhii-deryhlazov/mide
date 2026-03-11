using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;

namespace mide;

partial class Program
{
    static async Task ShowFindDialogAsync()
    {
        if (_ws == null) return;
        var tcs = new TaskCompletionSource<string?>();

        var dialog = new WindowBuilder(_ws)
            .WithTitle("Find in File")
            .WithSize(_config.Dialogs.FindWidth, _config.Dialogs.FindHeight)
            .Centered()
            .AsModal()
            .Build();

        dialog.AddControl(Controls.Label("[bold]Search (case-insensitive):[/]"));

        var prompt = new PromptControl { Prompt = "Find: ", UnfocusOnEnter = true };
        prompt.Entered += (_, text) => { _ws.CloseWindow(dialog); tcs.TrySetResult(text); };
        dialog.AddControl(prompt);

        dialog.AddControl(Controls.Button("Cancel")
            .OnClick((_, _, _) => { _ws.CloseWindow(dialog); tcs.TrySetResult(null); })
            .Build());

        dialog.OnClosed += (_, _) => tcs.TrySetResult(null);
        _ws.AddWindow(dialog);

        var term = await tcs.Task;
        if (term != null) FindInEditor(term);
    }

    static void FindInEditor(string term)
    {
        if (_editor == null || string.IsNullOrEmpty(term)) return;
        var content = _editor.Content ?? string.Empty;
        int idx = content.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            int line = content[..idx].Count(c => c == '\n') + 1;
            _editor.GoToLine(line);
            Notify("Found", $"'{term}' at line {line}", NotificationSeverity.Info);
        }
        else
        {
            Notify("Not Found", $"'{term}' not found", NotificationSeverity.Warning);
        }
    }

    static async Task ShowGotoDialogAsync()
    {
        if (_ws == null || _editor == null) return;
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new WindowBuilder(_ws)
            .WithTitle("Go to Line")
            .WithSize(_config.Dialogs.GotoWidth, _config.Dialogs.GotoHeight)
            .Centered()
            .AsModal()
            .Build();

        int totalLines = CountLines(_editor.Content);
        dialog.AddControl(Controls.Label($"Lines: 1–{totalLines}"));
        var prompt = new PromptControl { Prompt = "Line #: ", UnfocusOnEnter = true };
        prompt.Entered += (_, text) =>
        {
            if (int.TryParse(text, out int ln) && ln >= 1) _editor.GoToLine(ln);
            _ws.CloseWindow(dialog);
            tcs.TrySetResult(true);
        };
        dialog.AddControl(prompt);
        dialog.OnClosed += (_, _) => tcs.TrySetResult(false);
        _ws.AddWindow(dialog);
        await tcs.Task;
    }

    static async Task DeleteSelectedTreeNodeAsync()
    {
        if (_ws == null || _fileTree == null) return;

        var node = _fileTree.SelectedNode;
        if (node?.Tag is not string path) return;

        bool isDir  = Directory.Exists(path);
        bool isFile = !isDir && File.Exists(path);
        if (!isDir && !isFile) return;

        var name = isDir
            ? Path.GetFileName(path) + "/"
            : Path.GetFileName(path);
        var confirmed = await ConfirmDeleteAsync(name);
        if (!confirmed) return;

        try
        {
            if (isDir)
            {
                Directory.Delete(path, recursive: true);
                // Remove any expanded-path bookmarks that were inside the deleted folder
                _expandedPaths.RemoveWhere(p => p == path || p.StartsWith(path + Path.DirectorySeparatorChar));
                // Clear current file if it was inside the deleted folder
                if (_currentFile != null &&
                    Path.GetFullPath(_currentFile).StartsWith(Path.GetFullPath(path) + Path.DirectorySeparatorChar))
                {
                    _currentFile = null;
                    _editor?.SetContent(string.Empty);
                    UpdateStatusBar();
                }
            }
            else
            {
                File.Delete(path);
                if (_currentFile != null && Path.GetFullPath(_currentFile) == Path.GetFullPath(path))
                {
                    _currentFile = null;
                    _editor?.SetContent(string.Empty);
                    UpdateStatusBar();
                }
            }

            if (_fileTree != null)
            {
                PopulateTree(_fileTree, _rootDir);
                _fileTree.CollapseAll();
                RestoreExpandedPaths(_fileTree, _expandedPaths);
            }
        }
        catch (Exception ex)
        {
            Notify("Delete", ex.Message, NotificationSeverity.Danger);
        }
    }

    static async Task<bool> ConfirmDeleteAsync(string fileName)
    {
        if (_ws == null) return false;
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new WindowBuilder(_ws)
            .WithTitle("Confirm delete")
            .WithSize(48, 4)
            .Centered()
            .AsModal()
            .HideTitle()
            .HideTitleButtons()
            .Borderless()
            .Build();

        dialog.AddControl(Controls.Label($" Delete [red]{EscapeMarkup(fileName)}[/]?"));
        dialog.AddControl(Controls.Label($" [dim]Enter[/] confirm   [dim]Bksp[/] cancel"));

        // A zero-width prompt gives the dialog a focusable control so it receives keys.
        var prompt = new PromptControl
        {
            Prompt = string.Empty,
            Input = string.Empty,
            InputWidth = 0,
            UnfocusOnEnter = false,
        };
        // Enter pressed
        prompt.Entered += (_, _) =>
        {
            tcs.TrySetResult(true);
            _ws.CloseWindow(dialog);
        };
        dialog.AddControl(prompt);

        // Backspace / Esc caught at window level (prompt input is empty so it bubbles up)
        dialog.PreviewKeyPressed += (_, e) =>
        {
            if (e.KeyInfo.Key == ConsoleKey.Backspace || e.KeyInfo.Key == ConsoleKey.Escape)
            {
                e.Handled = true;
                tcs.TrySetResult(false);
                _ws.CloseWindow(dialog);
            }
        };

        dialog.OnClosed += (_, _) => tcs.TrySetResult(false);
        _ws.AddWindow(dialog);

        return await tcs.Task;
    }
}
