using RoboCopy_GUI_Kr.Core.Models;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr.Core.Tests;

public sealed class OneWaySyncTests
{
    [Fact]
    public void AreAllDescendantsSelected_ReturnsFalseWhenAnyChildIsCleared()
    {
        var items = new[]
        {
            (RelativePath: "folder\\kept.txt", Selected: true),
            (RelativePath: "folder\\cleared.txt", Selected: false),
            (RelativePath: "other.txt", Selected: true),
        };

        Assert.False(OneWaySyncSelection.AreAllDescendantsSelected(items, "folder"));
        Assert.True(OneWaySyncSelection.AreAllDescendantsSelected(
            items.Select(item => (item.RelativePath, Selected: true)),
            "folder"));
    }

    [Theory]
    [InlineData(@"folder\child.txt", "folder", true)]
    [InlineData(@"folder\nested\child.txt", "folder", true)]
    [InlineData(@"folder-two\child.txt", "folder", false)]
    [InlineData("folder", "folder", false)]
    public void SelectionIdentifiesOnlyItemsBelowSelectedFolder(string candidate, string directory, bool expected)
    {
        Assert.Equal(expected, OneWaySyncSelection.IsDescendant(candidate, directory));
    }

    [Fact]
    public void Planner_FindsDestinationOnlyFilesAndFoldersButHonorsExclusions()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".one-way", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(Path.Combine(destination, "extra-folder"));
            File.WriteAllText(Path.Combine(source, "common.txt"), "source");
            File.WriteAllText(Path.Combine(destination, "common.txt"), "destination");
            File.WriteAllText(Path.Combine(destination, "extra.txt"), "extra");
            File.WriteAllText(Path.Combine(destination, "keep.tmp"), "excluded");
            File.WriteAllText(Path.Combine(destination, "extra-folder", "child.txt"), "extra child");

            var items = new OneWaySyncPlanner().FindDestinationOnlyItems(new RobocopyJobOptions
            {
                OperationMode = JobOperationMode.UpdateSync,
                SourceDirectory = source,
                DestinationDirectory = destination,
                ExcludedFiles = ["*.tmp"],
            });

            Assert.Contains(items, item => item.RelativePath == "extra.txt" && !item.IsDirectory);
            Assert.Contains(items, item => item.RelativePath == Path.Combine("extra-folder", "child.txt") && !item.IsDirectory);
            Assert.Contains(items, item => item.RelativePath == "extra-folder" && item.IsDirectory);
            Assert.DoesNotContain(items, item => item.RelativePath == "common.txt");
            Assert.DoesNotContain(items, item => item.RelativePath == "keep.tmp");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Deletion_DeletesOnlySelectedItemsAndThenEmptySelectedFolders()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".one-way", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        try
        {
            Directory.CreateDirectory(source);
            var extraFolder = Path.Combine(destination, "extra-folder");
            Directory.CreateDirectory(extraFolder);
            var selectedFile = Path.Combine(extraFolder, "delete.txt");
            var retainedFile = Path.Combine(destination, "retain.txt");
            await File.WriteAllTextAsync(selectedFile, "delete");
            await File.WriteAllTextAsync(retainedFile, "retain");
            var selected = new[]
            {
                new OneWaySyncExtraItem(selectedFile, Path.Combine("extra-folder", "delete.txt"), false, 6),
                new OneWaySyncExtraItem(extraFolder, "extra-folder", true, 0),
            };

            var result = await new OneWaySyncDeletionService().DeleteSelectedAsync(
                source,
                destination,
                selected);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(selectedFile));
            Assert.False(Directory.Exists(extraFolder));
            Assert.True(File.Exists(retainedFile));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Deletion_PreservesDestinationWhenSourceAppearsAfterReview()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".one-way", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var sourceFile = Path.Combine(source, "appeared.txt");
            var destinationFile = Path.Combine(destination, "appeared.txt");
            await File.WriteAllTextAsync(sourceFile, "now exists");
            await File.WriteAllTextAsync(destinationFile, "destination");

            var result = await new OneWaySyncDeletionService().DeleteSelectedAsync(
                source,
                destination,
                [new OneWaySyncExtraItem(destinationFile, "appeared.txt", false, 11)]);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(destinationFile));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Quarantine_MovesAndRestoresFileWithoutOverwriting()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".one-way", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        var taskId = Guid.NewGuid();
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(Path.Combine(destination, "하위 폴더"));
            var original = Path.Combine(destination, "하위 폴더", "공백 파일.txt");
            await File.WriteAllTextAsync(original, "restore me");
            var selected = new[]
            {
                new OneWaySyncExtraItem(original, Path.Combine("하위 폴더", "공백 파일.txt"), false, 10),
            };
            var service = new OneWaySyncQuarantineService();

            var quarantined = await service.QuarantineSelectedAsync(source, destination, selected, taskId);
            var item = Assert.Single(service.GetItems(destination, taskId));
            Assert.True((File.GetAttributes(OneWaySyncQuarantineService.GetQuarantineContainer(destination))
                & FileAttributes.Hidden) != 0);
            var restored = await service.RestoreAsync(destination, taskId, [item]);

            Assert.True(quarantined.IsSuccess);
            Assert.True(restored.IsSuccess);
            Assert.Equal(selected[0].RelativePath, Assert.Single(restored.RestoredRelativePaths));
            Assert.True(File.Exists(original));
            Assert.Empty(service.GetItems(destination, taskId));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            var quarantine = OneWaySyncQuarantineService.GetQuarantineRoot(destination, taskId);
            var quarantineContainer = Directory.GetParent(quarantine)?.FullName;
            if (quarantineContainer is not null && Directory.Exists(quarantineContainer))
            {
                Directory.Delete(quarantineContainer, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Quarantine_PermanentDelete_RemovesOnlySelectedQuarantineFile()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".one-way", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        var taskId = Guid.NewGuid();
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var original = Path.Combine(destination, "영구 삭제.txt");
            await File.WriteAllTextAsync(original, "delete me");
            var service = new OneWaySyncQuarantineService();
            await service.QuarantineSelectedAsync(source, destination,
                [new OneWaySyncExtraItem(original, "영구 삭제.txt", false, 9)], taskId);
            var item = Assert.Single(service.GetItems(destination, taskId));

            var result = await service.DeletePermanentlyAsync(destination, taskId, [item]);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.DeletedFiles);
            Assert.False(File.Exists(item.QuarantinePath));
            Assert.Empty(service.GetItems(destination, taskId));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            var quarantine = OneWaySyncQuarantineService.GetQuarantineRoot(destination, taskId);
            var container = Directory.GetParent(quarantine)?.FullName;
            if (container is not null && Directory.Exists(container)) Directory.Delete(container, recursive: true);
        }
    }

    [Fact]
    public async Task Quarantine_EquivalentIdentityUsesPathSizeCreationAndModifiedTime()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".one-way", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "destination");
        var taskId = Guid.NewGuid();
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            var target = Path.Combine(destination, "identity.txt");
            await File.WriteAllTextAsync(target, "same-content");
            var service = new OneWaySyncQuarantineService();
            await service.QuarantineSelectedAsync(source, destination,
                [new OneWaySyncExtraItem(target, "identity.txt", false, new FileInfo(target).Length)], taskId);
            var quarantined = Assert.Single(service.GetItems(destination, taskId));

            await File.WriteAllTextAsync(target, "same-content");
            File.SetCreationTimeUtc(target, quarantined.CreationTimeUtc);
            File.SetLastWriteTimeUtc(target, quarantined.LastWriteTimeUtc);
            var candidate = new OneWaySyncExtraItem(target, "identity.txt", false, new FileInfo(target).Length);
            Assert.Empty(service.ExcludeEquivalentPreviouslyQuarantinedFiles(destination, taskId, [candidate]));

            File.SetLastWriteTimeUtc(target, quarantined.LastWriteTimeUtc.AddSeconds(2));
            Assert.Single(service.ExcludeEquivalentPreviouslyQuarantinedFiles(destination, taskId, [candidate]));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            var container = OneWaySyncQuarantineService.GetQuarantineContainer(destination);
            if (Directory.Exists(container)) Directory.Delete(container, recursive: true);
        }
    }
}
