using SharpConsoleUI.Controls;

namespace mide;

partial class Program
{
    static void SetEditorMode(EditorMode mode, bool focus = true)
    {
        if (_editor == null) return;

        bool editing = mode == EditorMode.Edit;

        _editor.IsEditing            = editing;
        _editor.HighlightCurrentLine = editing;
        _editor.ShowEditingHints     = editing;
        _editor.FocusedBackgroundColor = _editorBrowseBg;

        if (!editing)
        {
            _editor.OverwriteMode = false;
            _editor.ClearSelection();
        }

        if (focus)
            _editor.SetFocus(editing, FocusReason.Programmatic);
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
