namespace RoboCopy_GUI_Kr.Core.Errors;

public static class ElevatedRetryRequestValidator
{
    public static void Validate(ElevatedRetryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Files.Count == 0)
        {
            throw new ArgumentException("At least one retry file is required.", nameof(request));
        }

        if (request.RetryCount is < 0 or > 999 || request.RetryWaitSeconds is < 0 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        foreach (var file in request.Files)
        {
            if (!Path.IsPathFullyQualified(file.SourcePath)
                || !Path.IsPathFullyQualified(file.DestinationPath)
                || string.Equals(Path.GetFullPath(file.SourcePath), Path.GetFullPath(file.DestinationPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Retry paths must be distinct absolute paths.", nameof(request));
            }

            if ((file.MoveSourceRoot is not null || file.MoveDestinationRoot is not null)
                && (!file.MoveAfterCopy
                    || string.IsNullOrWhiteSpace(file.MoveSourceRoot)
                    || string.IsNullOrWhiteSpace(file.MoveDestinationRoot)
                    || !Path.IsPathFullyQualified(file.MoveSourceRoot)
                    || !Path.IsPathFullyQualified(file.MoveDestinationRoot)))
            {
                throw new ArgumentException("Move cleanup roots must be paired absolute paths.", nameof(request));
            }
        }

        if (!Path.IsPathFullyQualified(request.ResultPath))
        {
            throw new ArgumentException("Result path must be absolute.", nameof(request));
        }
    }
}
