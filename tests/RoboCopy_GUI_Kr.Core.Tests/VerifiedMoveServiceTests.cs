using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Moves;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class VerifiedMoveServiceTests
{
    [Fact]
    public async Task ExecuteAsync_AfterCancellation_ResumesFromRemainingSourceFiles()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "source");
        var destination = Path.Combine(testRoot, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            for (var index = 0; index < 3; index++)
            {
                await File.WriteAllTextAsync(Path.Combine(source, $"{index}.txt"), $"data-{index}");
                await File.WriteAllTextAsync(Path.Combine(destination, $"{index}.txt"), $"data-{index}");
            }

            var options = new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.Move,
                SourceDirectory = source,
                DestinationDirectory = destination,
            };
            using var cancellation = new CancellationTokenSource();
            var progress = new CallbackProgress<VerifiedMoveProgress>(update =>
            {
                if (update.CompletedFiles == 1)
                {
                    cancellation.Cancel();
                }
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new VerifiedMoveService().ExecuteAsync(options, progress, cancellation.Token));
            Assert.Equal(2, Directory.EnumerateFiles(source).Count());

            var resumed = await new VerifiedMoveService().ExecuteAsync(options);

            Assert.True(resumed.IsSuccess);
            Assert.False(Directory.Exists(source));
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
    public async Task ExecuteAsync_LeavesAbsoluteExcludedErrorAndMovesOtherVerifiedFile()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "source");
        var destination = Path.Combine(testRoot, "destination");

        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var failedSource = Path.Combine(source, "locked.txt");
            var verifiedSource = Path.Combine(source, "ready.txt");
            await File.WriteAllTextAsync(failedSource, "not copied");
            await File.WriteAllTextAsync(verifiedSource, "copied");
            await File.WriteAllTextAsync(Path.Combine(destination, "ready.txt"), "copied");

            var result = await new VerifiedMoveService().ExecuteAsync(new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.Move,
                SourceDirectory = source,
                DestinationDirectory = destination,
                ExcludedFiles = [failedSource],
            });

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(verifiedSource));
            Assert.True(File.Exists(failedSource));
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
    public async Task ExecuteAsync_SharingViolationIsReportedAndCanBeRetriedAfterUnlock()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "source");
        var destination = Path.Combine(testRoot, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var sourceFile = Path.Combine(source, "System.Text.Json.dll");
            var destinationFile = Path.Combine(destination, "System.Text.Json.dll");
            await File.WriteAllTextAsync(sourceFile, "identical content");
            await File.WriteAllTextAsync(destinationFile, "identical content");
            var options = new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.Move,
                SourceDirectory = source,
                DestinationDirectory = destination,
            };

            VerifiedMoveResult lockedResult;
            using (File.Open(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                lockedResult = await new VerifiedMoveService().ExecuteAsync(options);
            }

            var failure = Assert.Single(lockedResult.Failures);
            Assert.Contains(failure.ErrorCode, new[] { 32, 33 });
            Assert.True(File.Exists(sourceFile));

            var retried = await new VerifiedMoveService().ExecuteAsync(options);

            Assert.True(retried.IsSuccess);
            Assert.False(File.Exists(sourceFile));
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
    public async Task ExecuteAsync_DeletesSourceRootWhenMoveScopeBecomesEmpty()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "source", "retry-folder");
        var destination = Path.Combine(testRoot, "destination", "retry-folder");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            await File.WriteAllTextAsync(Path.Combine(source, "retry.txt"), "same");
            await File.WriteAllTextAsync(Path.Combine(destination, "retry.txt"), "same");

            var result = await new VerifiedMoveService().ExecuteAsync(new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.Move,
                SourceDirectory = source,
                DestinationDirectory = destination,
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.False(Directory.Exists(source));
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
    public void DeleteVerifiedEmptyAncestors_RemovesEmptyMovedTreeButKeepsScopeRoot()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(testRoot, "source");
        var destinationRoot = Path.Combine(testRoot, "destination");
        var sourceLeaf = Path.Combine(sourceRoot, "package", "13.0.1", "lib", "net8.0");
        var destinationLeaf = Path.Combine(destinationRoot, "package", "13.0.1", "lib", "net8.0");
        try
        {
            Directory.CreateDirectory(sourceLeaf);
            Directory.CreateDirectory(destinationLeaf);

            var deleted = new VerifiedMoveService().DeleteVerifiedEmptyAncestors(
                sourceLeaf,
                sourceRoot,
                destinationRoot);

            Assert.Equal(4, deleted);
            Assert.True(Directory.Exists(sourceRoot));
            Assert.False(Directory.Exists(Path.Combine(sourceRoot, "package")));
            Assert.True(Directory.Exists(destinationLeaf));
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
    public void DeleteVerifiedEmptyAncestors_StopsWhenSourceAncestorContainsAnotherFile()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(testRoot, "source");
        var destinationRoot = Path.Combine(testRoot, "destination");
        var sourceLeaf = Path.Combine(sourceRoot, "package", "lib");
        var destinationLeaf = Path.Combine(destinationRoot, "package", "lib");
        try
        {
            Directory.CreateDirectory(sourceLeaf);
            Directory.CreateDirectory(destinationLeaf);
            File.WriteAllText(Path.Combine(sourceRoot, "package", "keep.txt"), "keep");

            var deleted = new VerifiedMoveService().DeleteVerifiedEmptyAncestors(
                sourceLeaf,
                sourceRoot,
                destinationRoot);

            Assert.Equal(1, deleted);
            Assert.True(Directory.Exists(Path.Combine(sourceRoot, "package")));
            Assert.True(File.Exists(Path.Combine(sourceRoot, "package", "keep.txt")));
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
    public async Task ExecuteAsync_DeletesOnlyContentVerifiedFilesAndCopiedEmptyDirectories()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "원본 폴더");
        var destination = Path.Combine(testRoot, "대상 폴더");

        try
        {
            Directory.CreateDirectory(Path.Combine(source, "빈 폴더"));
            Directory.CreateDirectory(Path.Combine(destination, "빈 폴더"));
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var verifiedSource = Path.Combine(source, "검증 성공.txt");
            var failedSource = Path.Combine(source, "검증 실패.txt");
            await File.WriteAllTextAsync(verifiedSource, "same content");
            await File.WriteAllTextAsync(Path.Combine(destination, "검증 성공.txt"), "same content");
            await File.WriteAllTextAsync(failedSource, "source content");
            await File.WriteAllTextAsync(Path.Combine(destination, "검증 실패.txt"), "different content");

            var result = await new VerifiedMoveService().ExecuteAsync(new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.Move,
                SourceDirectory = source,
                DestinationDirectory = destination,
                IncludeEmptyDirectories = true,
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(1, result.DeletedFiles);
            Assert.Equal(1, result.DeletedDirectories);
            Assert.False(File.Exists(verifiedSource));
            Assert.True(File.Exists(failedSource));
            var failure = Assert.Single(result.Failures);
            Assert.Equal(failedSource, failure.SourcePath);
            Assert.Equal(Path.Combine(destination, "검증 실패.txt"), failure.DestinationPath);
            Assert.True(failure.ErrorCode > 0);
            Assert.False(string.IsNullOrWhiteSpace(failure.Reason));
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
    public async Task ExecuteAsync_LeavesExcludedAndZeroByteFilesAtSource()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".move", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "source");
        var destination = Path.Combine(testRoot, "destination");

        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var excluded = Path.Combine(source, "do-not-move.keep");
            var zeroByte = Path.Combine(source, "empty.dat");
            await File.WriteAllTextAsync(excluded, "excluded");
            await File.WriteAllBytesAsync(zeroByte, []);

            var result = await new VerifiedMoveService().ExecuteAsync(new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.Move,
                SourceDirectory = source,
                DestinationDirectory = destination,
                ExcludedFiles = ["*.keep"],
                ExcludeZeroByteFiles = true,
            });

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.DeletedFiles);
            Assert.True(File.Exists(excluded));
            Assert.True(File.Exists(zeroByte));
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
