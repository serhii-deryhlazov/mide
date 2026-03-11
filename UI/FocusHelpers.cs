using SharpConsoleUI.Controls;

namespace mide;

partial class Program
{
    static void FocusEditor(bool? editing = null)
    {
        if (_editor == null) return;
        if (editing.HasValue) ApplyEditingMode(editing.Value);
        _editor.SetFocus(true, FocusReason.Programmatic);
    }

    static void ApplyEditingMode(bool editing)
    {
        if (_editor == null) return;

        _editor.IsEditing             = editing;
        _editor.HighlightCurrentLine  = editing;
        _editor.ShowEditingHints      = editing;

        // When browsing, match FocusedBackgroundColor to BackgroundColor so the
        // framework's automatic re-focus doesn't produce a visible shade.
        _editor.FocusedBackgroundColor = editing ? _editorFocusedBg : _editor.BackgroundColor;

        if (!editing)
        {
            _editor.OverwriteMode = false;
            _editor.ClearSelection();
            _editor.SetFocus(false, FocusReason.Programmatic);
        }
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
            var left  = _layout.Columns[0];
            var right = _layout.Columns[1];

            left.FlexFactor  = 0;
            left.Width       = _treeVisible ? treeWidth : 0;
            left.MinWidth    = _treeVisible ? treeWidth : 0;
            left.MaxWidth    = _treeVisible ? treeWidth : 0;

            right.FlexFactor = 1;
            right.Width      = editorWidth;
            right.MinWidth   = editorWidth;
        }
    }
}
