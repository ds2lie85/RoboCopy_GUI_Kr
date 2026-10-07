using System.Globalization;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Performance;
namespace RoboCopy_GUI_Kr.Agent;
public partial class AgentMtBenchmarkHistoryWindow : Window
{
 private readonly MtRecommendationService _service; private readonly string _source; private readonly string _destination;
 public AgentMtBenchmarkHistoryWindow(MtRecommendationService service,string source,string destination){InitializeComponent();_service=service;_source=source;_destination=destination;RefreshHistory();}
 public event EventHandler? HistoryChanged;
 private void RemoveSelected_Click(object sender,RoutedEventArgs e){var rows=HistoryDataGrid.SelectedItems.Cast<Row>().ToArray();if(rows.Length==0)return;var prompt=string.Format(CultureInfo.CurrentCulture,Text("DeleteMtBenchmarkHistoryPromptFormat"),rows.Length);if(System.Windows.MessageBox.Show(this,prompt,Text("DeleteMtBenchmarkHistoryTitle"),MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;var changed=false;foreach(var row in rows)changed|=_service.RemoveBenchmarkHistory(_source,_destination,row.ThreadCount);if(changed){RefreshHistory();HistoryChanged?.Invoke(this,EventArgs.Empty);}}
 private void RefreshHistory(){var rows=_service.GetBenchmarkHistory(_source,_destination).Select(entry=>new Row(entry.ThreadCount,string.Format(CultureInfo.CurrentCulture,Text("MtBenchmarkSpeedDisplayFormat"),entry.AverageBytesPerSecond/1024/1024,(entry.LatestBytesPerSecond>0?entry.LatestBytesPerSecond:entry.AverageBytesPerSecond)/1024/1024),entry.SampleCount,entry.TestedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.CurrentCulture))).ToArray();HistoryDataGrid.ItemsSource=rows;SummaryTextBlock.Text=rows.Length==0?Text("MtBenchmarkHistoryEmpty"):string.Format(CultureInfo.CurrentCulture,Text("MtBenchmarkHistoryCountFormat"),rows.Length);}
 private string Text(string key)=>FindResource(key) as string??key; private void Close_Click(object sender,RoutedEventArgs e)=>Close(); private sealed record Row(int ThreadCount,string SpeedDisplay,int SampleCount,string TestedAt);
}
