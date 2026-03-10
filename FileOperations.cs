using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Dialogs;

namespace mide;

partial class Program
{
    // ── Open / Save ───────────────────────────────────────────────────────
    static void OpenFile(string path, bool fromTree = false, bool editMode = false, bool focusEditor = false)
    {
        if (_editor == null) return;
        try
        {
            _editor.Content           = File.ReadAllText(path);
            _currentFile              = path;
            ApplyEditingMode(editMode);
            _editor.SyntaxHighlighter = IdeSyntaxHighlighter.ForExtension(Path.GetExtension(path));
            UpdateTitle();
            UpdateStatusBar();
            Notify("Opened", Path.GetFileName(path), NotificationSeverity.Success);

            if (!fromTree)
                SyncTreeSelection(path);

            if (focusEditor)
                FocusEditor(editMode);
        }
        catch (Exception ex)
        {
            Notify("Error", $"Cannot open: {ex.Message}", NotificationSeverity.Danger);
        }
    }

    static async Task OpenFileDialogAsync(bool editMode = false, bool focusEditor = false)
    {
        if (_ws == null) return;
        var path = await FileDialogs.ShowFilePickerAsync(_ws, filter: "*.cs;*.py;*.js;*.ts;*.json;*.md;*.txt;*.*");
        if (path != null) OpenFile(path, editMode: editMode, focusEditor: focusEditor);
    }

    static async Task OpenFolderDialogAsync()
    {
        if (_ws == null) return;
        var dir = await FileDialogs.ShowFolderPickerAsync(_ws);
        if (dir != null && Directory.Exists(dir))
        {
            _rootDir = dir;
            if (_fileTree != null) PopulateTree(_fileTree, _rootDir);
            _ws.StatusBarStateService.TopStatus = $" mide – {Path.GetFileName(dir)}/ ";
            Notify("Folder", Path.GetFileName(dir), NotificationSeverity.Info);
        }
    }

    static void NewFile()
    {
        if (_editor == null) return;
        _editor.Content           = string.Empty;
        _editor.IsEditing         = true;
        _editor.SyntaxHighlighter = new IdeSyntaxHighlighter();
        _currentFile              = null;
        UpdateTitle();
        UpdateStatusBar();
    }

    static async Task SaveAsync(bool saveAs)
    {
        if (_ws == null || _editor == null) return;
        string? path = _currentFile;
        if (saveAs || path == null)
        {
            var def = _currentFile != null ? Path.GetFileName(_currentFile) : "untitled.cs";
            path = await FileDialogs.ShowSaveFileAsync(_ws, filter: "*.cs;*.py;*.js;*.ts;*.json;*.md;*.txt;*.*", defaultFileName: def);
        }
        if (path != null)
        {
            try
            {
                File.WriteAllText(path, _editor.Content ?? string.Empty);
                _currentFile = path;
                ApplyEditingMode(false);
                UpdateTitle();
                Notify("Saved", Path.GetFileName(path), NotificationSeverity.Success);
                if (_fileTree != null) PopulateTree(_fileTree, _rootDir);
            }
            catch (Exception ex)
            {
                Notify("Error", $"Cannot save: {ex.Message}", NotificationSeverity.Danger);
            }
        }
    }

    static void LoadInitialFile()
    {
        if (_editor == null) return;

        var candidates = new[] { "README.md", "Readme.md", "README.txt", "README" };
        foreach (var name in candidates)
        {
            var path = Path.Combine(_rootDir, name);
            if (File.Exists(path))
            {
                OpenFile(path);
                return;
            }
        }

        // fallback: welcome content
        _editor.Content   = Welcome;
        _currentFile      = null;
        _editor.IsEditing = false;
        UpdateTitle();
    }
}
