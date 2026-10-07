using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Progress;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class FileWorkloadAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_CountsFilesAndHonorsDirectoryAndPatternExclusions()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".analysis", Guid.NewGuid().ToString("N"));
        var excludedDirectory = Path.Combine(testRoot, "제외 폴더");
        var includedDirectory = Path.Combine(testRoot, "포함 폴더");

        try
        {
            Directory.CreateDirectory(excludedDirectory);
            Directory.CreateDirectory(includedDirectory);
            await File.WriteAllBytesAsync(Path.Combine(testRoot, "root.bin"), new byte[10]);
            await File.WriteAllBytesAsync(Path.Combine(includedDirectory, "keep.dat"), new byte[20]);
            await File.WriteAllBytesAsync(Path.Combine(includedDirectory, "skip.tmp"), new byte[30]);
            await File.WriteAllBytesAsync(Path.Combine(excludedDirectory, "excluded.bin"), new byte[40]);

            var snapshot = await new FileWorkloadAnalyzer().AnalyzeAsync(new RobocopyJobOptions
            {
                SourceDirectory = testRoot,
                DestinationDirectory = Path.Combine(testRoot, "대상"),
                ExcludedDirectories = [excludedDirectory],
                ExcludedFiles = ["*.tmp"],
            }, CancellationToken.None);

            Assert.Equal(2, snapshot.TotalFiles);
            Assert.Equal(30, snapshot.TotalBytes);
            Assert.Contains(Path.Combine(testRoot, "root.bin"), snapshot.FileSizes.Keys);
            Assert.Contains(Path.Combine(includedDirectory, "keep.dat"), snapshot.FileSizes.Keys);
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
    public async Task AnalyzeAsync_ExcludesZeroByteFilesWhenRequested()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".zero-byte-analysis", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(testRoot);
            var emptyFile = Path.Combine(testRoot, ".gitkeep");
            var contentFile = Path.Combine(testRoot, "내용.bin");
            await File.WriteAllBytesAsync(emptyFile, []);
            await File.WriteAllBytesAsync(contentFile, new byte[1]);

            var snapshot = await new FileWorkloadAnalyzer().AnalyzeAsync(new RobocopyJobOptions
            {
                SourceDirectory = testRoot,
                DestinationDirectory = Path.Combine(testRoot, "대상"),
                ExcludeZeroByteFiles = true,
            }, CancellationToken.None);

            Assert.Single(snapshot.FileSizes);
            Assert.DoesNotContain(emptyFile, snapshot.FileSizes.Keys);
            Assert.Contains(contentFile, snapshot.FileSizes.Keys);
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
