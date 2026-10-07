using RoboCopy_GUI_Kr.Core.Models;

namespace RoboCopy_GUI_Kr.Core.Robocopy;

/// <summary>
/// 사용자 입력을 Robocopy 인자 토큰으로 변환합니다.
/// </summary>
public sealed class RobocopyCommandBuilder
{
    public RobocopyCommand Build(RobocopyJobOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var arguments = new List<string>
        {
            options.SourceDirectory.Trim(),
            options.DestinationDirectory.Trim(),
        };

        arguments.AddRange(options.FileSpecifications
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

        if (options.RecurseSubdirectories)
        {
            arguments.Add(options.IncludeEmptyDirectories ? "/E" : "/S");
        }

        if (options.Restartable)
        {
            arguments.Add("/Z");
        }

        arguments.Add($"/R:{options.RetryCount}");
        arguments.Add($"/W:{options.RetryWaitSeconds}");
        arguments.Add($"/MT:{options.MultiThreadCount}");
        arguments.Add("/COPY:DAT");
        arguments.Add("/DCOPY:DAT");
        arguments.Add("/BYTES");
        arguments.Add("/ETA");
        arguments.Add("/FP");

        if (options.ExcludeZeroByteFiles)
        {
            arguments.Add("/MIN:1");
        }

        if (options.SkipExistingFiles)
        {
            arguments.Add("/XC");
            arguments.Add("/XN");
            arguments.Add("/XO");
        }

        AddLinkPolicy(arguments, options.SymbolicLinkPolicy);
        AddExclusions(arguments, "/XD", options.ExcludedDirectories);
        AddExclusions(arguments, "/XF", options.ExcludedFiles);

        if (options.DryRun)
        {
            arguments.Add("/L");
        }

        if (!string.IsNullOrWhiteSpace(options.LogFilePath))
        {
            arguments.Add("/TEE");
            arguments.Add($"/LOG:{options.LogFilePath.Trim()}");
        }

        return new RobocopyCommand("robocopy.exe", arguments);
    }

    private static void AddLinkPolicy(List<string> arguments, SymbolicLinkPolicy policy)
    {
        switch (policy)
        {
            case SymbolicLinkPolicy.Exclude:
                arguments.Add("/XJ");
                arguments.Add("/XJF");
                break;
            case SymbolicLinkPolicy.CopyLinkItself:
                arguments.Add("/SL");
                arguments.Add("/SJ");
                break;
            case SymbolicLinkPolicy.FollowTarget:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown symbolic link policy.");
        }
    }

    private static void AddExclusions(List<string> arguments, string switchName, IEnumerable<string> values)
    {
        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            return;
        }

        arguments.Add(switchName);
        arguments.AddRange(normalized);
    }

    private static void Validate(RobocopyJobOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SourceDirectory))
        {
            throw new ArgumentException("Source directory is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.DestinationDirectory))
        {
            throw new ArgumentException("Destination directory is required.", nameof(options));
        }

        if (options.MultiThreadCount is < 1 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Multi-thread count must be between 1 and 128.");
        }

        if (options.RetryCount is < 0 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Retry count must be between 0 and 999.");
        }

        if (options.RetryWaitSeconds is < 0 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Retry wait must be between 0 and 3600 seconds.");
        }
    }
}
