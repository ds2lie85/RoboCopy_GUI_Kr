using RoboCopy_GUI_Kr.Core.Performance;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class MtBenchmarkServiceTests
{
    [Fact]
    public async Task RunAsync_SkipsExclusivelyLockedSourceFile()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-benchmark", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "잠금 원본");
        var destination = Path.Combine(testRoot, "대상");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        FileStream? exclusiveLock = null;

        try
        {
            var readablePath = Path.Combine(source, "읽기 가능.bin");
            var lockedPath = Path.Combine(source, "잠긴 파일.bin");
            await File.WriteAllBytesAsync(readablePath, new byte[128 * 1024]);
            await File.WriteAllBytesAsync(lockedPath, new byte[256 * 1024]);
            exclusiveLock = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);

            var result = await new MtBenchmarkService().RunAsync(source, destination);

            Assert.NotEmpty(result.Measurements);
            Assert.Equal(128 * 1024, result.SequentialMeasurement.CopiedBytes);
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        }
        finally
        {
            exclusiveLock?.Dispose();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_UsesReadOnlySourceSamplesAndRemovesTemporaryOutput()
    {
        var testRoot = Path.Combine(AppContext.BaseDirectory, ".mt-benchmark", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(testRoot, "한글 원본 폴더");
        var destination = Path.Combine(testRoot, "대상 폴더");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        var originalFiles = new List<string>();

        try
        {
            for (var index = 0; index < 8; index++)
            {
                var path = Path.Combine(source, $"표본 파일 {index}.bin");
                await File.WriteAllBytesAsync(path, new byte[256 * 1024]);
                originalFiles.Add(path);
            }

            var result = await new MtBenchmarkService().RunAsync(source, destination);

            Assert.NotEmpty(result.Measurements);
            Assert.Contains(result.RecommendedThreadCount, result.Measurements.Select(item => item.ThreadCount));
            Assert.Equal(100, result.MixedSampleCount);
            Assert.InRange(result.SampleBytes, 1, 128L * 1024 * 1024);
            Assert.Equal(256 * 1024, result.SequentialMeasurement.CopiedBytes);
            Assert.All(originalFiles, path => Assert.Equal(256 * 1024, new FileInfo(path).Length));
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
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
