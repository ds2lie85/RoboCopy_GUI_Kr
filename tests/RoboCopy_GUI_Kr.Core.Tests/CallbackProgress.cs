namespace RoboCopy_GUI_Kr.Core.Tests;

internal sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
{
    public void Report(T value) => callback(value);
}
