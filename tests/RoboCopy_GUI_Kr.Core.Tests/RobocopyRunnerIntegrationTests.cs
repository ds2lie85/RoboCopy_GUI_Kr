using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopyRunnerIntegrationTests
{
    [Fact]
    public async Task RunAsync_UpdateSyncUpdatesChangedFileAndPreservesDestinationOnlyFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var testRoot = Path.Combine(AppContext.BaseDirectory, ".integration", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "source");
        var destination = Path.Combine(testRoot, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(source, "changed.txt"), "new content");
            await File.WriteAllTextAsync(Path.Combine(destination, "changed.txt"), "old");
            await File.WriteAllTextAsync(Path.Combine(destination, "destination-only.txt"), "keep me");

            var command = new RobocopyCommandBuilder().Build(new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.UpdateSync,
                SourceDirectory = source,
                DestinationDirectory = destination,
                MultiThreadCount = 1,
                RetryCount = 0,
                RetryWaitSeconds = 0,
            });
            var result = await new RobocopyRunner().RunAsync(command);

            Assert.True(RobocopyExitCodeInterpreter.Interpret(result.ExitCode).IsSuccess);
            Assert.Equal("new content", await File.ReadAllTextAsync(Path.Combine(destination, "changed.txt")));
            Assert.Equal("keep me", await File.ReadAllTextAsync(Path.Combine(destination, "destination-only.txt")));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_CopiesSmallFileWithWindowsRobocopy()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var testRoot = Path.Combine(AppContext.BaseDirectory, ".integration", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "원본 폴더");
        var destination = Path.Combine(testRoot, "대상 폴더");

        try
        {
            Directory.CreateDirectory(source);
            await File.WriteAllTextAsync(Path.Combine(source, "한글 파일.txt"), "RoboCopy_GUI_Kr integration test");

            var command = new RobocopyCommandBuilder().Build(new RobocopyJobOptions
            {
                SourceDirectory = source,
                DestinationDirectory = destination,
                MultiThreadCount = 1,
                RetryCount = 0,
                RetryWaitSeconds = 0,
            });

            var result = await new RobocopyRunner().RunAsync(command);

            Assert.True(RobocopyExitCodeInterpreter.Interpret(result.ExitCode).IsSuccess);
            Assert.Equal(
                "RoboCopy_GUI_Kr integration test",
                await File.ReadAllTextAsync(Path.Combine(destination, "한글 파일.txt")));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_ZeroByteOptionExcludesEmptyFilesButCopiesContentFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var testRoot = Path.Combine(AppContext.BaseDirectory, ".integration", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "원본 폴더");
        var destination = Path.Combine(testRoot, "대상 폴더");

        try
        {
            Directory.CreateDirectory(source);
            await File.WriteAllBytesAsync(Path.Combine(source, ".gitkeep"), []);
            await File.WriteAllTextAsync(Path.Combine(source, "내용.txt"), "복사 대상");

            var command = new RobocopyCommandBuilder().Build(new RobocopyJobOptions
            {
                SourceDirectory = source,
                DestinationDirectory = destination,
                MultiThreadCount = 1,
                RetryCount = 0,
                RetryWaitSeconds = 0,
                ExcludeZeroByteFiles = true,
            });

            var result = await new RobocopyRunner().RunAsync(command);

            Assert.True(RobocopyExitCodeInterpreter.Interpret(result.ExitCode).IsSuccess);
            Assert.False(File.Exists(Path.Combine(destination, ".gitkeep")));
            Assert.True(File.Exists(Path.Combine(destination, "내용.txt")));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_ThreeSequentialJobsDrainSharedUpdateBufferBetweenJobs()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var testRoot = Path.Combine(AppContext.BaseDirectory, ".integration", Guid.NewGuid().ToString("N"));
        var runner = new RobocopyRunner();
        var buffer = new RobocopyUpdateBuffer(maximumPendingOutputLines: 128, maximumPendingFileUpdates: 64);

        try
        {
            for (var job = 0; job < 3; job++)
            {
                var source = Path.Combine(testRoot, $"원본 #{job}");
                var destination = Path.Combine(testRoot, $"대상 #{job}");
                Directory.CreateDirectory(source);

                for (var file = 0; file < 30; file++)
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(source, $"한글 파일 #{file}.txt"),
                        $"job={job}, file={file}");
                }

                var command = new RobocopyCommandBuilder().Build(new RobocopyJobOptions
                {
                    SourceDirectory = source,
                    DestinationDirectory = destination,
                    MultiThreadCount = 1,
                    RetryCount = 0,
                    RetryWaitSeconds = 0,
                });

                buffer.Clear();
                var result = await runner.RunAsync(
                    command,
                    buffer.EnqueueOutput,
                    progress => buffer.EnqueueOverallProgress(progress.Percent),
                    CancellationToken.None,
                    buffer.EnqueueFileProgress);
                var batch = buffer.Drain(int.MaxValue, int.MaxValue);

                Assert.True(RobocopyExitCodeInterpreter.Interpret(result.ExitCode).IsSuccess);
                Assert.NotEmpty(batch.OutputLines);
                Assert.False(buffer.HasPendingUpdates);
                Assert.Equal(30, Directory.GetFiles(destination).Length);
            }
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
