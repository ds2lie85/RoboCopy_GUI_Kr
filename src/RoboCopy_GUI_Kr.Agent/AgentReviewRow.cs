using System.ComponentModel;
using RoboCopy_GUI_Kr.Core.Sync;

namespace RoboCopy_GUI_Kr.Agent;

public sealed class AgentReviewRow(OneWaySyncExtraItem item, string typeText, string sizeText) : INotifyPropertyChanged
{
    private bool _delete;

    public OneWaySyncExtraItem Item { get; } = item;

    public string TypeText { get; } = typeText;

    public string RelativePath => Item.RelativePath;

    public string SizeText { get; } = sizeText;
    public string ByteText => Item.IsDirectory ? string.Empty : Item.Size.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool Delete
    {
        get => _delete;
        set
        {
            if (_delete == value)
            {
                return;
            }

            _delete = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Delete)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
