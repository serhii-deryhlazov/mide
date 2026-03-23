using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Layout;
using Rectangle = System.Drawing.Rectangle;

namespace mide;

internal class CommandSuggestionPortal : PortalContentBase
{
    private readonly ListControl _list;
    private Rectangle            _bounds;

    private static readonly Color Bg       = new Color(20, 20, 30);
    private static readonly Color Fg       = new Color(220, 220, 220);
    private static readonly Color BorderFg = new Color(80, 80, 120);
    private static readonly Color SelBg    = new Color(0, 80, 140);
    private static readonly Color SelFg    = Color.White;

    public bool HasItems => _list.Items.Count > 0;
    public bool IsFocused { get; private set; }

    /// <summary>Fires when user clicks an item — passes the completion text.</summary>
    public event EventHandler<string>? ItemAccepted;

    /// <summary>Returns the completion text of the selected item, only when portal is focused.</summary>
    public string? GetSelected() =>
        IsFocused ? (_list.SelectedItem?.Tag as string ?? _list.SelectedItem?.Text) : null;

    /// <summary>Visually focus/unfocus the portal list.</summary>
    public void SetFocused(bool focused)
    {
        IsFocused      = focused;
        _list.HasFocus = focused;
        Invalidate();
    }

    /// <summary>Move selection down. If not focused, focuses and selects first item.</summary>
    public void SelectNext()
    {
        if (!IsFocused)
        {
            SetFocused(true);
            _list.SelectedIndex = 0;
            return;
        }
        if (_list.SelectedIndex < _list.Items.Count - 1)
            _list.SelectedIndex++;
        Invalidate();
    }

    /// <summary>Move selection up. Returns false when at top or not focused (caller should return focus to prompt).</summary>
    public bool SelectPrev()
    {
        if (!IsFocused) return false;
        if (_list.SelectedIndex > 0)
        {
            _list.SelectedIndex--;
            Invalidate();
            return true;
        }
        // At top — give focus back to prompt
        SetFocused(false);
        return false;
    }

    public void UpdateItems(List<(string Display, string Completion)> items)
    {
        _list.Items = items
            .Select(i => new ListItem(i.Display) { Tag = (object)i.Completion })
            .ToList();
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        SetFocused(false);  // typing resets portal focus — user must re-enter with Down
        Invalidate();
    }

    public CommandSuggestionPortal(List<(string Display, string Completion)> suggestions,
        int anchorX, int anchorY, int windowWidth, int windowHeight)
    {
        DismissOnOutsideClick = true;
        BorderStyle           = BoxChars.Rounded;
        BorderColor           = BorderFg;
        BorderBackgroundColor = Bg;

        _list = new ListControl
        {
            BackgroundColor          = Bg,
            ForegroundColor          = Fg,
            FocusedBackgroundColor   = Bg,
            FocusedForegroundColor   = Fg,
            HighlightBackgroundColor = SelBg,
            HighlightForegroundColor = SelFg,
            HoverHighlightsItems     = false,
            AutoAdjustWidth          = false,
        };
        foreach (var (display, completion) in suggestions)
            _list.AddItem(new ListItem(display) { Tag = (object)completion });
        if (suggestions.Count > 0) _list.SelectedIndex = 0;

        int visibleRows = Math.Min(Math.Max(suggestions.Count, 1), 10);
        int maxItemW    = suggestions.Count > 0 ? suggestions.Max(s => s.Display.Length) : 20;
        int popupW      = Math.Min(windowWidth - 4, maxItemW + 4);
        int popupH      = visibleRows + 2;

        var pos = PortalPositioner.CalculateFromPoint(
            new System.Drawing.Point(anchorX, anchorY),
            new System.Drawing.Size(popupW, popupH),
            new Rectangle(1, 1, windowWidth - 2, windowHeight - 2),
            PortalPlacement.AboveOrBelow,
            new System.Drawing.Size(0, 0));
        _bounds = pos.Bounds;
    }

    public override Rectangle GetPortalBounds() => _bounds;

    public override bool ProcessMouseEvent(MouseEventArgs args)
    {
        if (args.HasFlag(MouseFlags.Button1Clicked))
        {
            SetFocused(true);
            ((IMouseAwareControl)_list).ProcessMouseEvent(args);
            var selected = GetSelected();
            if (selected != null)
                ItemAccepted?.Invoke(this, selected);
            return true;
        }
        return false;
    }

    protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
        LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        ((IDOMPaintable)_list).PaintDOM(buffer, bounds, clipRect, Fg, Bg);
    }
}
