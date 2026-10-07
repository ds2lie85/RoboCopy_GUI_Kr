using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using RoboCopy_GUI_Kr.Core.Exclusions;

namespace RoboCopy_GUI_Kr.Exclusions;

public sealed class ExclusionTreeNode : INotifyPropertyChanged
{
    private readonly ExclusionSelectionState _state;
    private bool _childrenLoaded;
    private bool? _isChecked;
    private bool _isExpanded;

    internal ExclusionTreeNode(
        string fullPath,
        bool isDirectory,
        ExclusionSelectionState state,
        ExclusionTreeNode? parent = null,
        bool isPlaceholder = false)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Parent = parent;
        IsPlaceholder = isPlaceholder;
        _state = state;
        Name = isPlaceholder ? "불러오는 중..." : GetDisplayName(fullPath);
        _isChecked = isPlaceholder ? true : state.GetState(fullPath, isDirectory);

        if (isDirectory && !isPlaceholder)
        {
            Children.Add(new ExclusionTreeNode(string.Empty, false, state, this, isPlaceholder: true));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public bool IsPlaceholder { get; }

    public ExclusionTreeNode? Parent { get; }

    public ObservableCollection<ExclusionTreeNode> Children { get; } = [];

    public bool IsEnabled => !IsPlaceholder && (Parent is null || !_state.HasExcludedDirectoryAncestor(FullPath));

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (IsPlaceholder || value is null || _isChecked == value)
            {
                return;
            }

            _state.SetIncluded(FullPath, IsDirectory, value.Value);
            SetVisualState(value, updateChildren: true);
            Parent?.RefreshFromState();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
            if (value)
            {
                EnsureChildrenLoaded();
            }
        }
    }

    public void EnsureChildrenLoaded()
    {
        if (!IsDirectory || _childrenLoaded || IsPlaceholder)
        {
            return;
        }

        _childrenLoaded = true;
        Children.Clear();

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(FullPath).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
            {
                Children.Add(new ExclusionTreeNode(directory, true, _state, this));
            }

            foreach (var file in Directory.EnumerateFiles(FullPath).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
            {
                Children.Add(new ExclusionTreeNode(file, false, _state, this));
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            // 읽을 수 없는 폴더는 빈 노드로 유지하고 복사 단계에서 Robocopy가 오류를 보고하도록 둡니다.
        }

        RefreshFromState();
    }

    private void RefreshFromState()
    {
        var state = _state.GetState(FullPath, IsDirectory);
        var loadedChildren = Children.Where(child => !child.IsPlaceholder).ToArray();
        if (IsDirectory && _childrenLoaded && loadedChildren.Length > 0)
        {
            state = loadedChildren.All(child => child.IsChecked == false)
                ? false
                : loadedChildren.All(child => child.IsChecked == true)
                    ? true
                    : null;
        }
        SetVisualState(state, updateChildren: false);
        Parent?.RefreshFromState();
    }

    private void SetVisualState(bool? value, bool updateChildren)
    {
        _isChecked = value;
        OnPropertyChanged(nameof(IsChecked));

        if (updateChildren && value is not null)
        {
            foreach (var child in Children.Where(child => !child.IsPlaceholder))
            {
                child.SetVisualState(value, updateChildren: true);
            }
        }

        foreach (var child in Children)
        {
            child.OnPropertyChanged(nameof(IsEnabled));
        }
    }

    private static string GetDisplayName(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
