using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Windows.Tests;

public sealed class ShellOverwriteSelectionUiTests
{
    [Fact]
    public void LargeConflictList_RealizesOnlyVisibleGroupsAndPreservesChoicesWhenRecycled()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            App? application = null;
            ShellOverwriteSelectionWindow? window = null;
            try
            {
                application = new App();
                application.InitializeComponent();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                WindowOwnerSelectorTests.VerifyMiniStartupOwners();
                MiniErrorRecoveryUiTests.VerifyRecovery();
                SyncBatchReviewUiTests.VerifySingleDecision();
                var groups = Enumerable.Range(0, 10_000).Select(index =>
                {
                    var destination = $@"C:\대상 공백 & #\파일 {index}.txt";
                    var source = $@"C:\원본 공백 & #\파일 {index}.txt";
                    return new ShellConflictGroupRow(new ShellCopyConflictGroup(destination,
                        [new ShellCopyCandidate(source, destination, 1024, DateTime.Today)],
                        new ShellExistingDestination(destination, 512, DateTime.Today)),
                        "existing", "source", "existing", "source", "target", []);
                });
                window = new ShellOverwriteSelectionWindow(groups);
                var content = (FrameworkElement)window.Content;
                var size = new Size(1040, 610);
                void Layout()
                {
                    content.Measure(size);
                    content.Arrange(new Rect(size));
                    content.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                    content.UpdateLayout();
                }
                Layout();
                var list = (ListBox)window.FindName("OverwriteGroupsList");
                Assert.Equal(10_000, list.Items.Count);
                var panel = Descendants(list).OfType<VirtualizingStackPanel>()
                    .Single(candidate => ItemsControl.GetItemsOwner(candidate) == list);
                Assert.InRange(panel.Children.Count, 1, 50);
                Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(9_999));

                var first = window.Rows[0];
                var firstContainer = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                var grid = Descendants(firstContainer).OfType<DataGrid>().Single();
                grid.SelectedItem = first.Candidates[0];
                Assert.Same(first.Candidates[0], first.SelectedCandidate);
                list.ScrollIntoView(window.Rows[^1]);
                Layout();
                Assert.NotNull(list.ItemContainerGenerator.ContainerFromIndex(9_999));
                Assert.InRange(panel.Children.Count, 1, 50);
                Assert.Null(list.ItemContainerGenerator.ContainerFromIndex(0));
                list.ScrollIntoView(first);
                Layout();
                firstContainer = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                grid = Descendants(firstContainer).OfType<DataGrid>().Single();
                Assert.Same(first.Candidates[0], grid.SelectedItem);
                Assert.Same(first.Candidates[0], first.SelectedCandidate);
                Assert.Same(window.Rows[^1].Candidates[^1], window.Rows[^1].SelectedCandidate);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); application?.Shutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(45)), $"Window layout timed out. Recovery stage: {MiniErrorRecoveryUiTests.Stage}");
        Assert.Null(failure);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
