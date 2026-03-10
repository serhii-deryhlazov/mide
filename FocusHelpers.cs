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
        _editor.HighlightCurrentLine  = editing;        // only highlight current line in edit mode
        _editor.ShowEditingHints      = editing;        // hints only in edit mode

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
        const int padding = 2; // small margin for borders

        int desired = Math.Max(10, totalWidth - treeWidth - padding);
        byte editorWidth = (byte)Math.Clamp(desired, 10, byte.MaxValue);

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
