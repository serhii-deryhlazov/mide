using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;

namespace mide;

partial class Program
{
    static Window? suggestionWindow;

    public static void CloseSuggestions()
    {
        if (_ws != null && suggestionWindow != null)
        {
            _ws.CloseWindow(suggestionWindow);
        }
        suggestionWindow = null;
    }

    public static void ShowSuggestions(string input, 
        (int posX, int posY) commandLinePosition, int commandLineWidth,
        PromptControl prompt)
    {
        if (_ws == null) return;
        if (string.IsNullOrEmpty(input))
        {
            CloseSuggestions();
            return;
        }

        var lines = input.Split('\n');
        int suggHeight = Math.Max(3, lines.Length + 1);

        CloseSuggestions();

        int suggX = commandLinePosition.posX;
        int suggY = Math.Max(0, commandLinePosition.posY - suggHeight - 1);

        suggestionWindow = new WindowBuilder(_ws)
            .WithTitle(string.Empty)
            .WithSize(commandLineWidth, suggHeight)
            .AtPosition(suggX, suggY)
            .Borderless()
            .HideTitle()
            .HideTitleButtons()
            .Build();

        suggestionWindow.AddControl(Controls.Label(input));
        suggestionWindow.PreviewKeyPressed += (_, e) =>
        {
            e.Handled = true;
            prompt.SetFocus(true, FocusReason.Programmatic);
        };
        
        _ws.AddWindow(suggestionWindow);

        prompt.SetFocus(true, FocusReason.Programmatic);
    }
}
