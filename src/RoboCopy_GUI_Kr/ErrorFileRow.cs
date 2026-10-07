using System.ComponentModel;
using System.Runtime.CompilerServices;
using RoboCopy_GUI_Kr.Core.Errors;

namespace RoboCopy_GUI_Kr;

public sealed class ErrorFileRow : INotifyPropertyChanged
{
    private RobocopyErrorItem _item;
    private ErrorFileRetryState _retryState;
    private string _retryStateText;
    private string _statusText;

    public ErrorFileRow(
        RobocopyErrorItem item,
        string categoryText,
        string errorCodeToolTip,
        ErrorFileRetryState retryState,
        string retryStateText)
    {
        _item = item;
        CategoryText = categoryText;
        _statusText = categoryText;
        ErrorCodeToolTip = errorCodeToolTip;
        _retryState = retryState;
        _retryStateText = retryStateText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RobocopyErrorItem Item => _item;

    public string CategoryText { get; private set; }

    public string StatusText => _statusText;

    public string ErrorCodeToolTip { get; private set; }

    public ErrorFileRetryState RetryState => _retryState;

    public string RetryStateText => _retryStateText;

    public int ErrorCode => _item.ErrorCode;

    public string SourcePath => _item.SourcePath;

    public string DestinationPath => _item.DestinationPath;

    public int Occurrences => _item.Occurrences;

    public bool CanRetry => _retryState is ErrorFileRetryState.Failed or ErrorFileRetryState.RetryFailed;

    public void UpdateError(RobocopyErrorItem item, string categoryText, string errorCodeToolTip)
    {
        _item = item;
        CategoryText = categoryText;
        if (_retryState != ErrorFileRetryState.Resolved)
        {
            _statusText = categoryText;
        }
        ErrorCodeToolTip = errorCodeToolTip;
        OnPropertyChanged(string.Empty);
    }

    public void UpdateRetryState(ErrorFileRetryState state, string stateText, string? completedStatusText = null)
    {
        var statusText = state == ErrorFileRetryState.Resolved && !string.IsNullOrWhiteSpace(completedStatusText)
            ? completedStatusText
            : CategoryText;
        if (_retryState == state
            && string.Equals(_retryStateText, stateText, StringComparison.Ordinal)
            && string.Equals(_statusText, statusText, StringComparison.Ordinal))
        {
            return;
        }

        _retryState = state;
        _retryStateText = stateText;
        _statusText = statusText;
        OnPropertyChanged(nameof(RetryState));
        OnPropertyChanged(nameof(RetryStateText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanRetry));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
