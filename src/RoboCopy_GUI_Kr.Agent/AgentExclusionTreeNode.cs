using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using RoboCopy_GUI_Kr.Core.Exclusions;

namespace RoboCopy_GUI_Kr.Agent;
public sealed class AgentExclusionTreeNode : INotifyPropertyChanged
{
 private readonly ExclusionSelectionState _state; private bool _loaded; private bool? _checked; private bool _expanded;
 internal AgentExclusionTreeNode(string path,bool directory,ExclusionSelectionState state,AgentExclusionTreeNode? parent=null,bool placeholder=false){FullPath=path;IsDirectory=directory;Parent=parent;IsPlaceholder=placeholder;_state=state;Name=placeholder?"불러오는 중...":Display(path);_checked=placeholder?true:state.GetState(path,directory);if(directory&&!placeholder)Children.Add(new(string.Empty,false,state,this,true));}
 public event PropertyChangedEventHandler? PropertyChanged; public string Name{get;} public string FullPath{get;} public bool IsDirectory{get;} public bool IsPlaceholder{get;} public AgentExclusionTreeNode? Parent{get;} public ObservableCollection<AgentExclusionTreeNode> Children{get;}=[]; public bool IsEnabled=>!IsPlaceholder&&(Parent is null||!_state.HasExcludedDirectoryAncestor(FullPath));
 public bool? IsChecked {get=>_checked;set{if(IsPlaceholder||value is null||_checked==value)return;_state.SetIncluded(FullPath,IsDirectory,value.Value);Set(value,true);Parent?.Refresh();}}
 public bool IsExpanded {get=>_expanded;set{if(_expanded==value)return;_expanded=value;Changed();if(value)EnsureChildrenLoaded();}}
 public void EnsureChildrenLoaded(){if(!IsDirectory||_loaded||IsPlaceholder)return;_loaded=true;Children.Clear();try{foreach(var path in Directory.EnumerateDirectories(FullPath).OrderBy(Path.GetFileName,StringComparer.CurrentCultureIgnoreCase))Children.Add(new(path,true,_state,this));foreach(var path in Directory.EnumerateFiles(FullPath).OrderBy(Path.GetFileName,StringComparer.CurrentCultureIgnoreCase))Children.Add(new(path,false,_state,this));}catch(Exception ex)when(ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException){}Refresh();}
 private void Refresh(){var state=_state.GetState(FullPath,IsDirectory);var loaded=Children.Where(x=>!x.IsPlaceholder).ToArray();if(IsDirectory&&_loaded&&loaded.Length>0)state=loaded.All(x=>x.IsChecked==false)?false:loaded.All(x=>x.IsChecked==true)?true:null;Set(state,false);Parent?.Refresh();} private void Set(bool? value,bool children){_checked=value;Changed(nameof(IsChecked));if(children&&value is not null)foreach(var child in Children.Where(x=>!x.IsPlaceholder))child.Set(value,true);foreach(var child in Children)child.Changed(nameof(IsEnabled));}
 private static string Display(string path){var value=path.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);var name=Path.GetFileName(value);return string.IsNullOrEmpty(name)?path:name;} private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
