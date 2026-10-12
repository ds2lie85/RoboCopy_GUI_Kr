using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using RoboCopy_GUI_Kr.Core.Jobs;
using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr.Windows.Tests;

internal static class SyncBatchReviewUiTests
{
    internal static void VerifySingleDecision()
    {
        foreach (var delete in new[] { false, true })
        {
            var root = Path.Combine(AppContext.BaseDirectory, ".sync-review", Guid.NewGuid().ToString("N"));
            var main = new MainWindow();
            var queue = (JobQueue)typeof(MainWindow).GetField("_jobQueue", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
            var ids = new List<Guid>();
            foreach (var folder in new[] { "A", "B" })
            {
                var source = Directory.CreateDirectory(Path.Combine(root, "source", folder)).FullName;
                var destination = Directory.CreateDirectory(Path.Combine(root, "destination", folder, "nested")).Parent!.FullName;
                File.WriteAllText(Path.Combine(destination, "nested", "extra.txt"), "extra");
                ids.Add(queue.Enqueue(new RobocopyJobOptions
                {
                    SourceDirectory = source, DestinationDirectory = destination, OperationMode = JobOperationMode.UpdateSync,
                }).Id);
            }
            var shown = 0;
            var previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(main.Dispatcher));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) =>
            {
                var review = Application.Current.Windows.OfType<OneWaySyncReviewWindow>().FirstOrDefault(window => window.IsVisible);
                if (review is null) return;
                shown++;
                if (delete) typeof(OneWaySyncReviewWindow).GetMethod("SetAll", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(review, [true]);
                review.DialogResult = true;
            };
            void Pump(Task<bool> task)
            {
                if (task.IsCompleted) { Assert.True(task.GetAwaiter().GetResult()); return; }
                var frame = new DispatcherFrame();
                var completionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
                completionTimer.Tick += (_, _) => { if (task.IsCompleted) frame.Continue = false; };
                completionTimer.Start();
                try { Dispatcher.PushFrame(frame); }
                finally { completionTimer.Stop(); }
                Assert.True(task.GetAwaiter().GetResult());
            }
            try
            {
                timer.Start();
                Pump(main.ReviewSyncBatchAsync(ids[0], CancellationToken.None));
                Pump(main.ReviewSyncBatchAsync(ids[1], CancellationToken.None));
                Assert.Equal(1, shown);
                var selections = (Dictionary<Guid, IReadOnlyList<OneWaySyncExtraItem>>)typeof(MainWindow)
                    .GetField("_syncDeletionSelections", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(main)!;
                Assert.All(ids, id => Assert.Equal(delete ? 2 : 0, selections[id].Count));
            }
            finally
            {
                timer.Stop();
                SynchronizationContext.SetSynchronizationContext(previousContext);
                main.Close();
                Directory.Delete(root, true);
            }
        }
    }
}
