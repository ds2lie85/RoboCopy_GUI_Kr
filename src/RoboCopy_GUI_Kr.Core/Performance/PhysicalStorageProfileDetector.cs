using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace RoboCopy_GUI_Kr.Core.Performance;

public sealed record PhysicalStorageDevice(
    int DiskNumber,
    string FriendlyName,
    string BusType,
    string UniqueId);

public sealed record PhysicalStorageProfile(
    PhysicalStorageDevice? SourceDevice,
    PhysicalStorageDevice? DestinationDevice,
    bool? SamePhysicalDisk);

public sealed class PhysicalStorageProfileDetector
{
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;
    private const uint ShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint IoctlVolumeGetVolumeDiskExtents = 0x00560000;
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private readonly StorageDetector _storageDetector = new();

    public async Task<PhysicalStorageProfile> DetectAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new PhysicalStorageProfile(null, null, null);
        }

        return await Task.Run(
            () => Detect(sourcePath, destinationPath, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    public static string GetBusTypeName(int busType)
    {
        return busType switch
        {
            1 => "SCSI",
            2 => "ATAPI",
            3 => "ATA",
            4 => "IEEE 1394",
            6 => "Fibre Channel",
            7 => "USB",
            8 => "RAID",
            9 => "iSCSI",
            10 => "SAS",
            11 => "SATA",
            12 => "SD",
            13 => "MMC",
            14 => "가상 디스크",
            15 => "파일 기반 가상 디스크",
            16 => "Storage Spaces",
            17 => "NVMe",
            18 => "SCM",
            19 => "UFS",
            _ => string.Empty,
        };
    }

    private PhysicalStorageProfile Detect(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceVolume = _storageDetector.GetVolumeKey(sourcePath);
        var destinationVolume = _storageDetector.GetVolumeKey(destinationPath);
        var sourceDevice = DetectVolume(sourceVolume);
        cancellationToken.ThrowIfCancellationRequested();
        var destinationDevice = string.Equals(sourceVolume, destinationVolume, StringComparison.OrdinalIgnoreCase)
            ? sourceDevice
            : DetectVolume(destinationVolume);
        bool? samePhysicalDisk = sourceDevice is null || destinationDevice is null
            ? null
            : sourceDevice.DiskNumber == destinationDevice.DiskNumber;
        return new PhysicalStorageProfile(sourceDevice, destinationDevice, samePhysicalDisk);
    }

    private static PhysicalStorageDevice? DetectVolume(string? volume)
    {
        if (volume is null
            || volume.Length != 2
            || volume[1] != ':'
            || !char.IsAsciiLetter(volume[0]))
        {
            return null;
        }

        using var volumeHandle = CreateFile(
            $@"\\.\{char.ToUpperInvariant(volume[0])}:",
            0,
            ShareRead | ShareWrite | ShareDelete,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (volumeHandle.IsInvalid)
        {
            return null;
        }

        var extentBuffer = new byte[1024];
        if (!DeviceIoControl(
                volumeHandle,
                IoctlVolumeGetVolumeDiskExtents,
                null,
                0,
                extentBuffer,
                (uint)extentBuffer.Length,
                out var extentBytes,
                IntPtr.Zero)
            || extentBytes < 12)
        {
            return null;
        }

        var diskNumber = BitConverter.ToInt32(extentBuffer, 8);
        return DetectPhysicalDisk(diskNumber);
    }

    private static PhysicalStorageDevice? DetectPhysicalDisk(int diskNumber)
    {
        using var diskHandle = CreateFile(
            $@"\\.\PhysicalDrive{diskNumber}",
            0,
            ShareRead | ShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (diskHandle.IsInvalid)
        {
            return new PhysicalStorageDevice(diskNumber, string.Empty, string.Empty, $"PhysicalDrive{diskNumber}");
        }

        var query = new byte[12];
        var descriptor = new byte[4096];
        if (!DeviceIoControl(
                diskHandle,
                IoctlStorageQueryProperty,
                query,
                (uint)query.Length,
                descriptor,
                (uint)descriptor.Length,
                out var descriptorBytes,
                IntPtr.Zero)
            || descriptorBytes < 36)
        {
            return new PhysicalStorageDevice(diskNumber, string.Empty, string.Empty, $"PhysicalDrive{diskNumber}");
        }

        var vendor = ReadDescriptorString(descriptor, BitConverter.ToInt32(descriptor, 12));
        var product = ReadDescriptorString(descriptor, BitConverter.ToInt32(descriptor, 16));
        var serial = ReadDescriptorString(descriptor, BitConverter.ToInt32(descriptor, 24));
        var friendlyName = string.Join(' ', new[] { vendor, product }.Where(value => value.Length > 0));
        return new PhysicalStorageDevice(
            diskNumber,
            friendlyName,
            GetBusTypeName(BitConverter.ToInt32(descriptor, 28)),
            string.IsNullOrWhiteSpace(serial) ? $"PhysicalDrive{diskNumber}" : serial);
    }

    private static string ReadDescriptorString(byte[] descriptor, int offset)
    {
        if (offset <= 0 || offset >= descriptor.Length)
        {
            return string.Empty;
        }

        var end = Array.IndexOf(descriptor, (byte)0, offset);
        if (end < 0)
        {
            end = descriptor.Length;
        }

        return Encoding.ASCII.GetString(descriptor, offset, end - offset).Trim();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        byte[]? inputBuffer,
        uint inputBufferSize,
        byte[] outputBuffer,
        uint outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);
}
