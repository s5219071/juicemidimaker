using CommunityToolkit.Mvvm.ComponentModel;

namespace JuiceMidiMaker.Models;

public partial class StepViewModel : ObservableObject
{
    public StepViewModel(int index)
    {
        Index = index;
        BarNumber = (index / 16) + 1;
        StepInBar = (index % 16) + 1;
        IsBeat = index % 4 == 0;
    }

    public int Index { get; }
    public int BarNumber { get; }
    public int StepInBar { get; }
    public bool IsBeat { get; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isPlayhead;
}
