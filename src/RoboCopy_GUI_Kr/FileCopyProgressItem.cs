using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RoboCopy_GUI_Kr;

using RoboCopy_GUI_Kr.Core.Progress;

public sealed class FileCopyProgressItem : INotifyPropertyChanged
{
    private double _percent;
    private string _destinationPath;

    public FileCopyProgressItem(string fileName, string sourcePath, string destinationPath, double percent)
    {
        FileName = FileProgressDisplayName.Format(fileName, sourcePath);
        SourcePath = sourcePath;
        _destinationPath = destinationPath;
        _percent = percent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FileName { get; }

    public string SourcePath { get; }

    public string DestinationPath
    {
        get => _destinationPath;
        private set => SetField(ref _destinationPath, value);
    }

    public double Percent
    {
        get => _percent;
        private set => SetField(ref _percent, value);
    }

    public void Update(double percent, string destinationPath)
    {
        Percent = percent;
        DestinationPath = destinationPath;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
