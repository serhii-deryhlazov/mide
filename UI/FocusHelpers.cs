using SharpConsoleUI;
using SharpConsoleUI.Controls;

namespace mide;

partial class Program
{
    static void SetEditorMode(EditorMode mode, bool focus = true)
    {
        if (_editor == null) return;

        bool editing = mode == EditorMode.Edit;

        _editor.IsEditing        = editing;
        _editor.ShowEditingHints = editing;

        if (!editing)
        {
            _editor.OverwriteMode = false;
            _editor.ClearSelection();
        }

        if (focus)
            _editor.SetFocus(true, FocusReason.Programmatic);

        UpdateEditorVisuals();
    }

    /// <summary>
    /// Single source of truth for editor background and highlight.
    /// Derives visual state from _treeVisible and _editor.IsEditing.
    /// Call after any state change that affects these.
    /// </summary>
    static void UpdateEditorVisuals()
    {
        if (_editor == null) return;

        Color bg = _treeVisible      ? _editorTreeBg
                 : _editor.IsEditing ? _editorEditBg
                 :                     _editorBrowseBg;

        _editor.BackgroundColor        = bg;
        _editor.FocusedBackgroundColor = bg;
        _editor.HighlightCurrentLine   = _editor.IsEditing && !_treeVisible;

        // Status bar contrasts the editor: darker in browse, lighter in edit
        if (_statusBar != null)
        {
            Color barBg;
            if (_editor.IsEditing && !_treeVisible)
            {
                // Edit bg is dark — bar is 30% lighter than edit bg
                var e = _editorEditBg;
                barBg = new Color(
                    (byte)Math.Min(255, e.R * 18 / 10),
                    (byte)Math.Min(255, e.G * 18 / 10),
                    (byte)Math.Min(255, e.B * 18 / 10));
            }
            else
            {
                // Browse/tree bg is lighter — bar is darker (60% of browse bg)
                var b = _editorBrowseBg;
                barBg = new Color((byte)(b.R * 6 / 10), (byte)(b.G * 6 / 10), (byte)(b.B * 6 / 10));
            }
            _statusBar.BackgroundColor         = barBg;
            _statusBar.ForegroundColor         = new Color(180, 220, 255);
            _statusBar.ShortcutForegroundColor = new Color(100, 200, 255);
        }

        UpdateStatusBar();
    }

    static void UpdateLayoutWidths()
    {
        if (_editor == null || _ws == null) return;

        int totalWidth = _ws.DesktopDimensions.Width;
        int treeWidth  = (_treeVisible && _fileTree?.Visible == true) ? (_fileTree?.Width ?? 0) : 0;

        int desired = Math.Max(_config.Layout.MinEditorWidth, totalWidth - treeWidth - _config.Layout.Padding);
        byte editorWidth = (byte)Math.Clamp(desired, _config.Layout.MinEditorWidth, byte.MaxValue);

        _editor.Width = editorWidth;

        if (_layout?.Columns is { Count: >= 2 })
        {
            var left = _layout.Columns[0];
            var right = _layout.Columns[1];

            left.FlexFactor = 0;
            left.Width = _treeVisible ? treeWidth : 0;
            left.MinWidth = _treeVisible ? treeWidth : 0;
            left.MaxWidth = _treeVisible ? treeWidth : 0;

            right.FlexFactor = 1;
            right.Width = editorWidth;
            right.MinWidth = editorWidth;
        }
    }
}
