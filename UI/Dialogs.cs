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
}
