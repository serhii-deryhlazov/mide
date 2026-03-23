using SharpConsoleUI.Controls;

namespace mide;

partial class Program
{
    static void PopulateTree(TreeControl tree, string dir)
    {
        tree.Clear();

        string folderColor = _config.Tree.FolderColor; 

        foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x))
        {
            var name = Path.GetFileName(d);
            if (name.StartsWith('.') || _config.Tree.IgnoredDirs.Contains(name)) continue;
            
            var node = tree.AddRootNode($"[{folderColor}]{EscapeMarkup(name)}/[/]");
            node.Tag = d;
            AddChildren(node, d, 1);
        }

        foreach (var f in Directory.GetFiles(dir).OrderBy(x => x))
        {
            var name = Path.GetFileName(f);
            var node = tree.AddRootNode($"{FileIcon(name)} {EscapeMarkup(name)}");
            node.Tag = f;
        }
    }

    static void RestoreExpandedPaths(TreeControl tree, HashSet<string> expanded)
    {
        void Walk(IEnumerable<TreeNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.Tag is string p && expanded.Contains(p))
                    n.IsExpanded = true;
                Walk(n.Children);
            }
        }
        Walk(tree.RootNodes);
    }

    static void AddChildren(SharpConsoleUI.Controls.TreeNode parent, string dir, int depth)
    {
        if (depth > _config.Tree.MaxDepth) return;
        try
        {
            foreach (var d in Directory.GetDirectories(dir).OrderBy(x => x))
            {
                var name = Path.GetFileName(d);
                if (name.StartsWith('.') || _config.Tree.IgnoredDirs.Contains(name)) continue;
                var node = parent.AddChild($"[cyan]{EscapeMarkup(name)}/[/]");
                node.Tag = d;
                AddChildren(node, d, depth + 1);
            }
            foreach (var f in Directory.GetFiles(dir).OrderBy(x => x))
            {
                var name = Path.GetFileName(f);
                var node = parent.AddChild($"{FileIcon(name)} {EscapeMarkup(name)}");
                node.Tag = f;
            }
        }
        catch { /* permission denied */ }
    }

    static void SyncTreeSelection(string path, bool expand = true)
    {
        if (_fileTree == null) return;

        var normalized = Path.GetFullPath(path);
        var node = _fileTree.FindNodeByTag(normalized);
        if (node == null) return;

        _suppressTreeEvent = true;
        _fileTree.SelectNode(node);
        _fileTree.EnsureNodeVisible(node);
        _suppressTreeEvent = false;
    }

    static string FileIcon(string name) => Constants.FileIcons.ForFile(name);

    static void ToggleTree()
    {
        _treeVisible = !_treeVisible;
        if (_treeVisible)
            OpenTree();
        else
            ApplyTreeVisibility();
    }

    static void OpenTree()
    {
        _modeBeforeTree = (_editor?.IsEditing == true) ? EditorMode.Edit : EditorMode.Browse;
        _treeVisible = true;
        ApplyTreeVisibility();
        if (_currentFile != null)
            SyncTreeSelection(_currentFile);
        SetEditorMode(EditorMode.Browse, focus: false);
        _fileTree?.SetFocus(true, FocusReason.Programmatic);
    }

    static void ApplyTreeVisibility()
    {
        if (_fileTree != null)
        {
            _fileTree.Visible = _treeVisible;
            _fileTree.IsEnabled = _treeVisible;
            _fileTree.Width = _treeVisible ? _config.Tree.Width : 0;

            if (!_treeVisible)
            {
                var restoredMode = _modeBeforeTree;
                _modeBeforeTree = EditorMode.Browse;
                SetEditorMode(restoredMode);
            }

            UpdateLayoutWidths();
        }
    }

    static async Task StartTreeRefreshLoop(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(_config.Tree.RefreshIntervalSeconds);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(interval, ct);
                if (_treeVisible && _fileTree != null)
                {
                    var selectedPath = _fileTree.SelectedNode?.Tag as string;

                    _suppressTreeEvent = true;
                    PopulateTree(_fileTree, _rootDir);
                    _fileTree.CollapseAll();
                    RestoreExpandedPaths(_fileTree, _expandedPaths);

                    var pathToSelect = selectedPath ?? _currentFile;
                    if (pathToSelect != null)
                        SyncTreeSelection(pathToSelect, false);
                    _suppressTreeEvent = false;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    static void RefreshTree(string? pathToSelect = null)
    {
        if (_fileTree == null) return;

        PopulateTree(_fileTree, _rootDir);
        _fileTree.CollapseAll();
        RestoreExpandedPaths(_fileTree, _expandedPaths);

        if (pathToSelect != null)
        {
            SyncTreeSelection(pathToSelect);
        }
    }
}
