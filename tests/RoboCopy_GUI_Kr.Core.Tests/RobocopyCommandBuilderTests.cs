using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Robocopy;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopyCommandBuilderTests
{
    [Fact]
    public void Build_SingleFileRetryKeepsUnicodeNameAsOneArgumentAndDoesNotRecurse()
    {
        var command = new RobocopyCommandBuilder().Build(new RobocopyJobOptions
        {
            SourceDirectory = @"C:\원본 폴더\하위",
            DestinationDirectory = @"D:\대상 폴더\하위",
            RecurseSubdirectories = false,
            FileSpecifications = ["잠긴 파일.txt"],
        });

        Assert.Equal(@"C:\원본 폴더\하위", command.Arguments[0]);
        Assert.Equal(@"D:\대상 폴더\하위", command.Arguments[1]);
        Assert.Equal("잠긴 파일.txt", command.Arguments[2]);
        Assert.DoesNotContain("/S", command.Arguments);
        Assert.DoesNotContain("/E", command.Arguments);
    }

    private readonly RobocopyCommandBuilder _builder = new();

    [Fact]
    public void Build_UsesSafeDefaultOptions()
    {
        var command = _builder.Build(CreateOptions());

        Assert.Equal("robocopy.exe", command.FileName);
        Assert.Collection(
            command.Arguments.Take(3),
            source => Assert.Equal(@"D:\원본 폴더", source),
            destination => Assert.Equal(@"E:\백업 폴더", destination),
            recursion => Assert.Equal("/S", recursion));
        Assert.Contains("/Z", command.Arguments);
        Assert.Contains("/R:3", command.Arguments);
        Assert.Contains("/W:5", command.Arguments);
        Assert.Contains("/MT:16", command.Arguments);
        Assert.Contains("/COPY:DAT", command.Arguments);
        Assert.Contains("/DCOPY:DAT", command.Arguments);
        Assert.Contains("/XJ", command.Arguments);
        Assert.Contains("/XJF", command.Arguments);
    }

    [Fact]
    public void Build_AddsDryRunAndEmptyDirectoryOptions()
    {
        var command = _builder.Build(CreateOptions() with
        {
            DryRun = true,
            IncludeEmptyDirectories = true,
        });

        Assert.Contains("/L", command.Arguments);
        Assert.Contains("/E", command.Arguments);
        Assert.DoesNotContain("/S", command.Arguments);
    }

    [Fact]
    public void Build_UpdateSyncCopiesChangesWithoutDeletingDestinationOnlyItems()
    {
        var command = _builder.Build(CreateOptions() with
        {
            OperationMode = JobOperationMode.UpdateSync,
        });

        Assert.DoesNotContain("/MIR", command.Arguments);
        Assert.DoesNotContain("/PURGE", command.Arguments);
        Assert.Contains("/S", command.Arguments);
    }

    [Fact]
    public void Build_AddsMinimumSizeAsSingleArgumentWhenZeroByteFilesAreExcluded()
    {
        var command = _builder.Build(CreateOptions() with { ExcludeZeroByteFiles = true });

        Assert.Contains("/MIN:1", command.Arguments);
        Assert.DoesNotContain("/MIN", command.Arguments);
    }

    [Fact]
    public void Build_SkipExistingUsesAllThreeRobocopyExistingFileFilters()
    {
        var command = _builder.Build(CreateOptions() with { SkipExistingFiles = true });

        Assert.Contains("/XC", command.Arguments);
        Assert.Contains("/XN", command.Arguments);
        Assert.Contains("/XO", command.Arguments);
    }

    [Fact]
    public void Build_KeepsExcludedPathsAsSeparateArguments()
    {
        var command = _builder.Build(CreateOptions() with
        {
            ExcludedDirectories = [@"D:\원본 폴더\Temp Data", "Cache", "cache"],
            ExcludedFiles = ["*.tmp", "Thumbs.db"],
        });

        var directorySwitch = command.Arguments.IndexOf("/XD");
        var fileSwitch = command.Arguments.IndexOf("/XF");
        Assert.True(directorySwitch >= 0);
        Assert.Equal(@"D:\원본 폴더\Temp Data", command.Arguments[directorySwitch + 1]);
        Assert.Equal("Cache", command.Arguments[directorySwitch + 2]);
        Assert.Equal(fileSwitch, directorySwitch + 3);
        Assert.Equal("*.tmp", command.Arguments[fileSwitch + 1]);
        Assert.Equal("Thumbs.db", command.Arguments[fileSwitch + 2]);
    }

    [Fact]
    public void Build_UsesOneLogArgumentEvenWhenPathContainsSpaces()
    {
        var command = _builder.Build(CreateOptions() with
        {
            LogFilePath = @"C:\My Logs\copy.log",
        });

        Assert.Contains("/TEE", command.Arguments);
        Assert.Contains(@"/LOG:C:\My Logs\copy.log", command.Arguments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public void Build_RejectsInvalidThreadCount(int threadCount)
    {
        var options = CreateOptions() with { MultiThreadCount = threadCount };

        Assert.Throws<ArgumentOutOfRangeException>(() => _builder.Build(options));
    }

    [Fact]
    public void DisplayText_QuotesPathsButDoesNotChangeArgumentTokens()
    {
        var command = _builder.Build(CreateOptions());

        Assert.Contains("\"D:\\원본 폴더\"", command.ToDisplayString(), StringComparison.Ordinal);
        Assert.Equal(@"D:\원본 폴더", command.Arguments[0]);
    }

    private static RobocopyJobOptions CreateOptions()
    {
        return new RobocopyJobOptions
        {
            SourceDirectory = @"D:\원본 폴더",
            DestinationDirectory = @"E:\백업 폴더",
            MultiThreadCount = 16,
        };
    }
}

internal static class ReadOnlyListExtensions
{
    public static int IndexOf<T>(this IReadOnlyList<T> values, T value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(values[index], value))
            {
                return index;
            }
        }

        return -1;
    }
}
