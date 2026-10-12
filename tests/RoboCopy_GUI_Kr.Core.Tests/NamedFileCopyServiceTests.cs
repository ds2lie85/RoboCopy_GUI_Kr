using RoboCopy_GUI_Kr.Core.Shell;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class NamedFileCopyServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"RoboCopy named copy {Guid.NewGuid():N}");

    [Fact]
    public async Task CopyAsync_CopiesKoreanPathToRequestedDifferentName()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "\uC6D0\uBCF8 \uC774\uB984.txt");
        var destination = Path.Combine(_root, "\uB300\uC0C1", "\uC6D0\uBCF8 \uC774\uB984 (1).txt");
        await File.WriteAllTextAsync(source, "\uD14C\uC2A4\uD2B8 \uB0B4\uC6A9");

        await new NamedFileCopyService().CopyAsync(source, destination, moveSource: false);

        Assert.True(File.Exists(source));
        Assert.Equal("\uD14C\uC2A4\uD2B8 \uB0B4\uC6A9", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task CopyAsync_MoveDeletesSourceOnlyAfterDestinationCompletes()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "\uC774\uB3D9 \uC6D0\uBCF8.bin");
        var destination = Path.Combine(_root, "\uB300\uC0C1", "\uC774\uB3D9 \uACB0\uACFC (1).bin");
        await File.WriteAllBytesAsync(source, Enumerable.Range(0, 4096).Select(index => (byte)index).ToArray());

        await new NamedFileCopyService().CopyAsync(source, destination, moveSource: true);

        Assert.False(File.Exists(source));
        Assert.Equal(4096, new FileInfo(destination).Length);
    }

    [Fact]
    public async Task CopyAsync_OverwriteReplacesOnlyTheChosenDestination()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "chosen.txt");
        var destination = Path.Combine(_root, "destination.txt");
        await File.WriteAllTextAsync(source, "chosen content");
        await File.WriteAllTextAsync(destination, "old content");

        await new NamedFileCopyService().CopyAsync(
            source,
            destination,
            moveSource: false,
            overwriteDestination: true);

        Assert.Equal("chosen content", await File.ReadAllTextAsync(destination));
    }

    [Fact]
    public async Task CopyAsync_MoveReportsSourceDeletionFailureAfterDestinationCompletes()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "locked source.txt");
        var destination = Path.Combine(_root, "destination", "locked source (1).txt");
        await File.WriteAllTextAsync(source, "content copied before delete");

        await using var lockedSource = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var exception = await Assert.ThrowsAsync<MoveSourceDeletionException>(() =>
            new NamedFileCopyService().CopyAsync(source, destination, moveSource: true));

        Assert.Equal(source, exception.SourcePath);
        Assert.Equal(destination, exception.DestinationPath);
        Assert.Contains(exception.ErrorCode, new[] { 32, 33 });
        Assert.True(File.Exists(source));
        Assert.Equal("content copied before delete", await File.ReadAllTextAsync(destination));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CopyAsync_ApprovedOverwriteHandlesReadOnlyAndPreservesSourceAttributes(bool sourceReadOnly, bool targetReadOnly)
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "원본 공백 & #.txt");
        var target = Path.Combine(_root, "대상 공백 & #.txt");
        await File.WriteAllTextAsync(source, "new content");
        await File.WriteAllTextAsync(target, "old content");
        if (sourceReadOnly) File.SetAttributes(source, File.GetAttributes(source) | FileAttributes.ReadOnly);
        if (targetReadOnly) File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
        await new NamedFileCopyService().CopyAsync(source, target, false, overwriteDestination: true);
        Assert.Equal("new content", await File.ReadAllTextAsync(target));
        Assert.Equal(sourceReadOnly, File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal(sourceReadOnly, File.GetAttributes(source).HasFlag(FileAttributes.ReadOnly));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task CopyAsync_FailedReplacementRestoresReadOnlyTargetAndCleansTemporaryFile()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.txt");
        var target = Path.Combine(_root, "locked target.txt");
        await File.WriteAllTextAsync(source, "new content");
        await File.WriteAllTextAsync(target, "old content");
        File.SetAttributes(source, File.GetAttributes(source) | FileAttributes.ReadOnly);
        File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
        using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var error = await Record.ExceptionAsync(() =>
                new NamedFileCopyService().CopyAsync(source, target, false, overwriteDestination: true));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.Contains(error.HResult & 0xFFFF, new[] { 5, 32, 33 });
        }
        Assert.True(File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal("old content", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task CopyAsync_UnapprovedOverwriteDoesNotChangeReadOnlyTarget()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "source.txt");
        var target = Path.Combine(_root, "target.txt");
        await File.WriteAllTextAsync(source, "new content");
        await File.WriteAllTextAsync(target, "old content");
        File.SetAttributes(source, File.GetAttributes(source) | FileAttributes.ReadOnly);
        File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.ReadOnly);
        await Assert.ThrowsAnyAsync<IOException>(() => new NamedFileCopyService().CopyAsync(source, target, false));
        Assert.True(File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal("old content", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
            Directory.Delete(_root, recursive: true);
        }
    }
}
