using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr;

public partial class App : Application
{
    private static readonly TimeSpan ExplorerSelectionBatchWindow = TimeSpan.FromSeconds(2);
    private ShellSelectionComServer? _shellSelectionComServer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(argument => string.Equals(argument, "--shell-com-server", StringComparison.Ordinal)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _shellSelectionComServer = new ShellSelectionComServer(Dispatcher, SaveComShellSelection);
            try
            {
                _shellSelectionComServer.Start();
            }
            catch (Exception exception)
            {
                WriteShellServerDiagnostic(exception.ToString());
                Shutdown();
            }

            return;
        }

        // Explorer can start one process per selected item. This branch must never create a window.
        if (e.Args.Length > 0 && string.Equals(e.Args[0], "--shell-set-selection", StringComparison.Ordinal))
        {
            AppendExplorerSelectionAndExit(e.Args.Skip(1).ToArray());
            return;
        }

        var command = ShellStartupCommand.Parse(e.Args);

        if (command.Action is ShellStartupAction.RegisterCopyMenu or ShellStartupAction.UnregisterMenu)
        {
            UpdateExplorerRegistrationAndExit(command.Action);
            return;
        }

        if (command.Action is ShellStartupAction.SetSelection or ShellStartupAction.SetSource)
        {
            var paths = command.Action == ShellStartupAction.SetSelection ? command.Paths : [command.Path!];
            SaveShellSelectionAndExit(paths);
            return;
        }

        if (command.Action == ShellStartupAction.PasteOperation)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = StartPasteOperationAsync(command);
            return;
        }

        var requests = command.Action switch
        {
            ShellStartupAction.PasteSavedSource => CreateSavedSourceRequest(command.Path),
            ShellStartupAction.PasteClipboard => CreateClipboardRequests(command.Path),
            _ => Array.Empty<ShellCopyRequest>(),
        };

        var window = new MainWindow(requests);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shellSelectionComServer?.Dispose();
        base.OnExit(e);
    }

    private async Task StartPasteOperationAsync(ShellStartupCommand command)
    {
        ShellConflictScanProgressWindow? scanWindow = null;
        try
        {
            scanWindow = new ShellConflictScanProgressWindow();
            scanWindow.Show();
            scanWindow.Activate();
            await System.Windows.Threading.Dispatcher.Yield(
                System.Windows.Threading.DispatcherPriority.Background);

            var operationRequests = CreateOperationRequests(command.Path, command.OperationMode);
            if (operationRequests.Count == 0)
            {
                scanWindow.Close();
                Shutdown();
                return;
            }

            var reviewedRequests = await ReviewShellConflictsAsync(operationRequests, scanWindow);
            scanWindow = null;
            if (reviewedRequests is null)
            {
                Shutdown();
                return;
            }

            var operationWindow = new ShellActionPromptWindow();
            if (operationWindow.ShowDialog() != true || operationWindow.Decision == ShellActionDecision.Cancel)
            {
                Shutdown();
                return;
            }

            reviewedRequests = reviewedRequests.Select(request => request with
                {
                    StartImmediately = operationWindow.Decision == ShellActionDecision.StartWithDefaults,
                })
                .ToArray();
            if (reviewedRequests.All(request => request.OperationMode == Core.Models.JobOperationMode.ContinuousSync))
            {
                var workflow = new ContinuousSyncWorkflow();
                if (operationWindow.Decision == ShellActionDecision.StartWithDefaults)
                {
                    workflow.StartWithDefaults(null, reviewedRequests.Select(CreateContinuousSyncOptions));
                }
                else
                {
                    foreach (var request in reviewedRequests)
                    {
                        workflow.ConfigureAndStart(null, CreateContinuousSyncOptions(request));
                    }
                }

                Shutdown();
                return;
            }

            var operationMainWindow = new MainWindow(
                reviewedRequests,
                shellConflictsAlreadyReviewed: true,
                preferMiniUi: operationWindow.Decision == ShellActionDecision.StartWithDefaults);
            MainWindow = operationMainWindow;
            if (operationWindow.Decision == ShellActionDecision.StartWithDefaults)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                await operationMainWindow.StartWithMiniUiAsync();
            }
            else
            {
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                operationMainWindow.Show();
            }
        }
        catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or ArgumentException
                                               or InvalidOperationException)
        {
            scanWindow?.Close();
            MessageBox.Show(exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task<IReadOnlyList<ShellCopyRequest>?> ReviewShellConflictsAsync(
        IReadOnlyList<ShellCopyRequest> requests,
        ShellConflictScanProgressWindow scanWindow)
    {
        var candidates = requests
            .Where(request => request.OperationMode is Core.Models.JobOperationMode.Copy or Core.Models.JobOperationMode.Move)
            .ToArray();
        if (candidates.Length == 0)
        {
            scanWindow.Close();
            return requests;
        }

        ShellCopyConflictAnalysis analysis;
        try
        {
            var progress = new Progress<ShellConflictScanProgress>(scanWindow.Report);
            analysis = await new ShellCopyConflictPlanner().AnalyzeAsync(candidates, progress);
        }
        finally
        {
            scanWindow.Close();
        }

        if (analysis.Groups.Count == 0)
        {
            return requests;
        }

        var conflictWindow = new ShellCopyConflictWindow(analysis.Groups);
        if (conflictWindow.ShowDialog() != true)
        {
            return null;
        }

        return new ShellConflictResolutionPlanner().Apply(
            requests,
            conflictWindow.Resolutions,
            analysis.PlannedDestinationPaths);
    }

    private static void SaveComShellSelection(IReadOnlyList<string> paths)
    {
        var validPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (validPaths.Length == 0)
        {
            return;
        }

        new ShellSourceRepository().SaveSelection(validPaths);
        new ExplorerContextMenuService().ShowPasteMenu();
    }

    private static void WriteShellServerDiagnostic(string message)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RoboCopy_GUI_Kr",
                "shell");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "com-server.log"), $"{DateTimeOffset.Now:O}{Environment.NewLine}{message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _ = exception;
        }
    }

    private void AppendExplorerSelectionAndExit(IReadOnlyList<string> paths)
    {
        try
        {
            using var mutex = new Mutex(initiallyOwned: false, @"Local\RoboCopy_GUI_Kr_ExplorerSelection");
            var lockTaken = false;
            try
            {
                try
                {
                    lockTaken = mutex.WaitOne(TimeSpan.FromSeconds(10));
                }
                catch (AbandonedMutexException)
                {
                    lockTaken = true;
                }

                if (lockTaken)
                {
                    new ShellSourceRepository().AppendSelectionInvocation(paths, ExplorerSelectionBatchWindow);
                    if (paths.Count > 0)
                    {
                        new ExplorerContextMenuService().ShowPasteMenu();
                    }
                }
            }
            finally
            {
                if (lockTaken)
                {
                    mutex.ReleaseMutex();
                }
            }
        }
        catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or ArgumentException
                                               or System.Security.SecurityException)
        {
            // Copy selection is intentionally background-only. Errors are handled when Paste validates the manifest.
            _ = exception;
        }

        Shutdown();
    }

    private void SaveShellSelectionAndExit(IReadOnlyList<string> paths)
    {
        var validPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (validPaths.Length == 0)
        {
            MessageBox.Show(GetText("ShellInvalidSelection"), GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        try
        {
            new ShellSourceRepository().SaveSelection(validPaths);
            new ExplorerContextMenuService().ShowPasteMenu();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Shutdown();
    }

    private void UpdateExplorerRegistrationAndExit(ShellStartupAction action)
    {
        try
        {
            var service = new ExplorerContextMenuService();
            if (action == ShellStartupAction.RegisterCopyMenu)
            {
                service.RegisterCopyMenu();
            }
            else
            {
                service.Unregister();
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            MessageBox.Show(exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Shutdown();
    }

    private IReadOnlyList<ShellCopyRequest> CreateOperationRequests(string? destination, Core.Models.JobOperationMode? operationMode)
    {
        if (!IsDirectory(destination) || operationMode is null)
        {
            MessageBox.Show(GetText("ShellInvalidFolder"), GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return [];
        }

        try
        {
            var selectedPaths = new ShellSourceRepository().LoadSelection();
            if (selectedPaths.Count == 0)
            {
                MessageBox.Show(GetText("ShellSourceMissing"), GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Warning);
                new ExplorerContextMenuService().RegisterCopyMenu();
                return [];
            }

            var requests = operationMode == Core.Models.JobOperationMode.ContinuousSync
                ? ContinuousSyncShellRequestFactory.Create(Path.GetFullPath(destination!), selectedPaths)
                : ShellCopyRequestFactory.Create(Path.GetFullPath(destination!), selectedPaths);
            return requests
                .Select(request => request with { OperationMode = operationMode.Value })
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            MessageBox.Show(exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
            return [];
        }
    }

    private IReadOnlyList<ShellCopyRequest> CreateSavedSourceRequest(string? destination)
    {
        try
        {
            var sources = new ShellSourceRepository().LoadSelection();
            if (sources.Count == 0)
            {
                MessageBox.Show(GetText("ShellSourceMissing"), GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return [];
            }

            return IsDirectory(destination)
                ? ShellCopyRequestFactory.Create(Path.GetFullPath(destination!), sources)
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            MessageBox.Show(exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
            return [];
        }
    }

    private IReadOnlyList<ShellCopyRequest> CreateClipboardRequests(string? destination)
    {
        if (!IsDirectory(destination))
        {
            return [];
        }

        try
        {
            var paths = Clipboard.ContainsFileDropList()
                ? Clipboard.GetFileDropList().Cast<string>()
                : [];
            var requests = ShellCopyRequestFactory.Create(Path.GetFullPath(destination!), paths);
            if (requests.Count == 0)
            {
                MessageBox.Show(GetText("ShellClipboardEmpty"), GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return requests;
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.ExternalException or ArgumentException or NotSupportedException)
        {
            MessageBox.Show(exception.Message, GetText("ExplorerIntegration"), MessageBoxButton.OK, MessageBoxImage.Error);
            return [];
        }
    }

    private static bool IsDirectory(string? path) => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    private static Core.Models.RobocopyJobOptions CreateContinuousSyncOptions(ShellCopyRequest request) => new()
    {
        OperationMode = Core.Models.JobOperationMode.ContinuousSync,
        SourceDirectory = request.SourceDirectory,
        DestinationDirectory = request.DestinationDirectory,
        FileSpecifications = request.FileSpecifications,
        RecurseSubdirectories = request.RecurseSubdirectories,
        ExcludedDirectories = request.ExcludedDirectories,
        ExcludedFiles = request.ExcludedFiles,
        DestinationFileName = request.DestinationFileName,
        OverwriteDestination = request.OverwriteDestination,
        SkipExistingFiles = request.SkipExistingFiles,
    };

    private string GetText(string key) => TryFindResource(key) as string ?? key;
}
