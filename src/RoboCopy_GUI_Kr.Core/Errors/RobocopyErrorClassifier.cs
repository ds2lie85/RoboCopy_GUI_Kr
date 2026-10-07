namespace RoboCopy_GUI_Kr.Core.Errors;

public static class RobocopyErrorClassifier
{
    public static RobocopyErrorCategory Classify(int errorCode) => errorCode switch
    {
        5 or 65 or 1314 => RobocopyErrorCategory.AccessDenied,
        32 or 33 => RobocopyErrorCategory.Locked,
        2 or 3 or 15 or 123 or 206 => RobocopyErrorCategory.PathNotFound,
        39 or 112 => RobocopyErrorCategory.DiskFull,
        53 or 64 or 67 or 121 or 1231 => RobocopyErrorCategory.NetworkError,
        23 or 59 or 1392 => RobocopyErrorCategory.FileChanged,
        _ => RobocopyErrorCategory.Unknown,
    };
}
