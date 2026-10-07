using RoboCopy_GUI_Kr.Core.Errors;
using RoboCopy_GUI_Kr.Core.Jobs;
using System.Runtime.Versioning;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class RobocopyErrorCollectorTests
{
    [Fact]
    public void ProcessLine_ClassifiesAndMapsKoreanPathToDestination()
    {
        var collector = new RobocopyErrorCollector(@"C:\원본 폴더", @"D:\대상 폴더");

        var parsed = collector.ProcessLine(
            @"2026/09/29 10:00:00 ERROR 32 (0x00000020) Copying File C:\원본 폴더\자료\잠긴 파일.dat");

        Assert.True(parsed);
        var item = Assert.Single(collector.Items);
        Assert.Equal(RobocopyErrorCategory.Locked, item.Category);
        Assert.Equal(@"C:\원본 폴더\자료\잠긴 파일.dat", item.SourcePath);
        Assert.Equal(@"D:\대상 폴더\자료\잠긴 파일.dat", item.DestinationPath);
    }

    [Fact]
    public void ProcessLine_DeduplicatesRetryMessagesAndCountsOccurrences()
    {
        var collector = new RobocopyErrorCollector(@"C:\Source", @"D:\Target");
        const string line = @"ERROR 5 (0x00000005) Copying File C:\Source\denied.txt";

        Assert.True(collector.ProcessLine(line));
        Assert.True(collector.ProcessLine(line));

        var item = Assert.Single(collector.Items);
        Assert.Equal(RobocopyErrorCategory.AccessDenied, item.Category);
        Assert.Equal(2, item.Occurrences);
    }

    [Theory]
    [InlineData(3, RobocopyErrorCategory.PathNotFound)]
    [InlineData(112, RobocopyErrorCategory.DiskFull)]
    [InlineData(53, RobocopyErrorCategory.NetworkError)]
    [InlineData(1392, RobocopyErrorCategory.FileChanged)]
    [InlineData(999, RobocopyErrorCategory.Unknown)]
    public void Classify_MapsWin32Errors(int code, RobocopyErrorCategory expected)
    {
        Assert.Equal(expected, RobocopyErrorClassifier.Classify(code));
    }

    [Fact]
    public void GetSystemMessage_ReturnsWindowsDescriptionForErrorCode()
    {
        var description = RobocopyErrorDescription.GetSystemMessage(2);

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.DoesNotContain("Unknown error", description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(JobQueueEntryState.Completed, ErrorFileRetryState.Resolved)]
    [InlineData(JobQueueEntryState.Failed, ErrorFileRetryState.RetryFailed)]
    [InlineData(JobQueueEntryState.Stopped, ErrorFileRetryState.RetryFailed)]
    [InlineData(JobQueueEntryState.Paused, ErrorFileRetryState.Paused)]
    public void RetryStateResolver_MapsQueueOutcome(
        JobQueueEntryState outcome,
        ErrorFileRetryState expected)
    {
        Assert.Equal(expected, ErrorFileRetryStateResolver.FromQueueOutcome(outcome));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void RestartManager_GracefulShutdownDoesNotSetForceFlag()
    {
        Assert.Equal(0U, RestartManagerService.GracefulShutdownActionFlags);
    }

    [Fact]
    public void ElevatedRetryRequestValidator_AcceptsDistinctUnicodeAbsolutePaths()
    {
        var request = new ElevatedRetryRequest
        {
            Files = [new ElevatedRetryFile(@"C:\권한 원본\파일 이름.txt", @"D:\권한 대상\파일 이름.txt")],
            ResultPath = @"C:\사용자 데이터\result.json",
        };

        ElevatedRetryRequestValidator.Validate(request);
    }

    [Fact]
    public void ElevatedRetryRequestValidator_AcceptsPairedMoveCleanupRoots()
    {
        var request = new ElevatedRetryRequest
        {
            Files =
            [
                new ElevatedRetryFile(@"C:\source\package\file.dll", @"D:\destination\package\file.dll")
                {
                    MoveAfterCopy = true,
                    MoveSourceRoot = @"C:\source",
                    MoveDestinationRoot = @"D:\destination",
                },
            ],
            ResultPath = @"C:\result.json",
        };

        ElevatedRetryRequestValidator.Validate(request);
    }

    [Fact]
    public void ElevatedRetryRequestValidator_RejectsUnpairedMoveCleanupRoot()
    {
        var request = new ElevatedRetryRequest
        {
            Files =
            [
                new ElevatedRetryFile(@"C:\source\file.dll", @"D:\destination\file.dll")
                {
                    MoveAfterCopy = true,
                    MoveSourceRoot = @"C:\source",
                },
            ],
            ResultPath = @"C:\result.json",
        };

        Assert.Throws<ArgumentException>(() => ElevatedRetryRequestValidator.Validate(request));
    }

    [Fact]
    public void ElevatedRetryRequestValidator_RejectsSameSourceAndDestination()
    {
        var request = new ElevatedRetryRequest
        {
            Files = [new ElevatedRetryFile(@"C:\same.txt", @"C:\same.txt")],
            ResultPath = @"C:\result.json",
        };

        Assert.Throws<ArgumentException>(() => ElevatedRetryRequestValidator.Validate(request));
    }

    [Fact]
    public void ElevatedRetryCommandFactory_DirectoryRetryCopiesOnlySelectedTree()
    {
        var item = new ElevatedRetryFile(
            @"C:\원본 폴더\관리자 전용 폴더\",
            @"D:\대상 폴더\관리자 전용 폴더\");

        var command = ElevatedRetryCommandFactory.Create(item, true, 0, 0, true);

        Assert.Equal(@"C:\원본 폴더\관리자 전용 폴더", command.Arguments[0]);
        Assert.Equal(@"D:\대상 폴더\관리자 전용 폴더", command.Arguments[1]);
        Assert.Equal("*", command.Arguments[2]);
        Assert.Contains("/E", command.Arguments);
        Assert.Contains("/MT:1", command.Arguments);
    }

    [Fact]
    public void ElevatedRetryCommandFactory_FileRetryRemainsNonRecursive()
    {
        var item = new ElevatedRetryFile(
            @"C:\원본 폴더\한글 파일.txt",
            @"D:\대상 폴더\한글 파일.txt");

        var command = ElevatedRetryCommandFactory.Create(item, false, 0, 0, true);

        Assert.Equal(@"C:\원본 폴더", command.Arguments[0]);
        Assert.Equal(@"D:\대상 폴더", command.Arguments[1]);
        Assert.Equal("한글 파일.txt", command.Arguments[2]);
        Assert.DoesNotContain("/E", command.Arguments);
        Assert.DoesNotContain("/S", command.Arguments);
    }
}
