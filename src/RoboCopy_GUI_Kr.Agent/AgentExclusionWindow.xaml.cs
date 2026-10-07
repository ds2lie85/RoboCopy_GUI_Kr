using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RoboCopy_GUI_Kr.Core.Exclusions;

namespace RoboCopy_GUI_Kr.Agent;

public partial class AgentExclusionWindow : Window
{
    private readonly ExclusionSelectionState _selectionState;
    private readonly ObservableCollection<string> _patterns;
    private bool _syncingCommonPatterns;
    private System.Windows.Point? _dragStart;
    private int _dragAnchor = -1;
    private bool _dragging;
    private string[] _selectionBeforeDrag = [];

    public AgentExclusionWindow(string sourceDirectory, IEnumerable<string> directories, IEnumerable<string> files)
    {
        var physicalFiles = files.Where(IsPhysicalPath).ToArray();
        _patterns = new(files.Where(file => !IsPhysicalPath(file)));
        _selectionState = new(directories, physicalFiles);
        RootNodes = [new AgentExclusionTreeNode(Path.GetFullPath(sourceDirectory), true, _selectionState)];
        InitializeComponent(); DataContext = this; PatternListBox.ItemsSource = _patterns; SyncCommonPatterns(); RootNodes[0].IsExpanded = true;
    }
    public ObservableCollection<AgentExclusionTreeNode> RootNodes { get; }
    public IReadOnlyList<string> ExcludedDirectories => _selectionState.Directories;
    public IReadOnlyList<string> ExcludedFiles => _selectionState.Files.Concat(_patterns).ToArray();
    private void TreeItem_Expanded(object sender, RoutedEventArgs e) { if (e.OriginalSource is TreeViewItem { DataContext: AgentExclusionTreeNode node }) node.EnsureChildrenLoaded(); }
    private void AddPattern_Click(object sender, RoutedEventArgs e) { var value=PatternTextBox.Text.Trim(); if(value.Length>0){AddPattern(value); PatternTextBox.Clear(); PatternTextBox.Focus();} }
    private void RemovePatterns_Click(object sender, RoutedEventArgs e) { foreach(var item in PatternListBox.SelectedItems.Cast<string>().ToArray()) _patterns.Remove(item); SyncCommonPatterns(); }
    private void PatternListBox_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { var item=FindParent<ListBoxItem>(e.OriginalSource as DependencyObject); if(item is not null&&!item.IsSelected){PatternListBox.SelectedItems.Clear();item.IsSelected=true;} }
    private void PatternListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { var item=FindParent<ListBoxItem>(e.OriginalSource as DependencyObject); _dragStart=e.GetPosition(PatternListBox); _dragAnchor=item is null?-1:PatternListBox.Items.IndexOf(item.DataContext); _selectionBeforeDrag=PatternListBox.SelectedItems.Cast<string>().ToArray(); _dragging=false; if(_dragAnchor>=0)Mouse.Capture(PatternListBox,CaptureMode.SubTree); }
    private void PatternListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) { if(e.LeftButton!=MouseButtonState.Pressed||_dragStart is not System.Windows.Point start||_dragAnchor<0)return; var current=e.GetPosition(PatternListBox); if(!_dragging&&Math.Abs(current.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(current.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return; var item=FindPatternItemAt(current); if(item is null)return; _dragging=true; var index=PatternListBox.Items.IndexOf(item.DataContext); PatternListBox.SelectedItems.Clear(); if((Keyboard.Modifiers&ModifierKeys.Control)!=0)foreach(var selected in _selectionBeforeDrag)PatternListBox.SelectedItems.Add(selected); for(var i=Math.Min(_dragAnchor,index);i<=Math.Max(_dragAnchor,index);i++)PatternListBox.SelectedItems.Add(PatternListBox.Items[i]); e.Handled=true; }
    private void PatternListBox_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { if(PatternListBox.IsMouseCaptured)PatternListBox.ReleaseMouseCapture(); if(_dragging)e.Handled=true; _dragStart=null;_dragAnchor=-1;_dragging=false;_selectionBeforeDrag=[]; }
    private void CommonPattern_Changed(object sender, RoutedEventArgs e) { if(_syncingCommonPatterns||sender is not ToggleButton { Tag:string pattern } toggle)return; if(toggle.IsChecked==true)AddPattern(pattern); else {var found=_patterns.FirstOrDefault(x=>string.Equals(x,pattern,StringComparison.OrdinalIgnoreCase));if(found is not null)_patterns.Remove(found);} SyncCommonPatterns(); }
    private void AddPattern(string pattern) { if(!_patterns.Any(x=>string.Equals(x,pattern,StringComparison.OrdinalIgnoreCase)))_patterns.Add(pattern); SyncCommonPatterns(); }
    private void SyncCommonPatterns() { _syncingCommonPatterns=true; try { foreach(var toggle in new[]{TmpPatternToggle,BakPatternToggle,ThumbsPatternToggle})toggle.IsChecked=toggle.Tag is string pattern&&_patterns.Any(x=>string.Equals(x,pattern,StringComparison.OrdinalIgnoreCase)); } finally {_syncingCommonPatterns=false;} }
    private void Save_Click(object sender, RoutedEventArgs e)=>DialogResult=true;
    private static bool IsPhysicalPath(string value)=>!value.Contains('*')&&!value.Contains('?')&&Path.IsPathFullyQualified(value);
    private static T? FindParent<T>(DependencyObject? child) where T:DependencyObject { while(child is not null){if(child is T value)return value;child=VisualTreeHelper.GetParent(child);}return null; }
    private ListBoxItem? FindPatternItemAt(System.Windows.Point point) { var hit=FindParent<ListBoxItem>(PatternListBox.InputHitTest(point) as DependencyObject); if(hit is not null)return hit; ListBoxItem? nearest=null; var distance=double.MaxValue; for(var i=0;i<PatternListBox.Items.Count;i++){if(PatternListBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem candidate)continue;var top=candidate.TranslatePoint(new System.Windows.Point(0,0),PatternListBox).Y;var bottom=top+candidate.ActualHeight;var candidateDistance=point.Y<top?top-point.Y:point.Y>bottom?point.Y-bottom:0;if(candidateDistance<distance){nearest=candidate;distance=candidateDistance;}}return nearest; }
}
