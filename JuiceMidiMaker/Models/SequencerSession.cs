namespace JuiceMidiMaker.Models;

public sealed class SequencerSession
{
    public int BPM { get; set; } = 128;
    public bool IsPlaying { get; set; }
    public int PlayheadIndex { get; set; }
    public bool[] GridSteps { get; set; } = new bool[TimingConstants.TotalSteps];
    public byte MidiNoteNumber { get; set; } = 36;
    public byte Velocity { get; set; } = 100;

    public bool IsBpmValid(int value) => value is >= 20 and <= 400;

    public PresetData ToPreset(string name, string? description = null, string? category = null)
        => new()
        {
            PresetName = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            BPM = BPM,
            GridSteps = (bool[])GridSteps.Clone(),
            MidiNoteNumber = MidiNoteNumber,
            Velocity = Velocity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    public void ApplyPreset(PresetData preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        BPM = IsBpmValid(preset.BPM) ? preset.BPM : 128;
        GridSteps = NormalizeGrid(preset.GridSteps);
        MidiNoteNumber = Math.Min(preset.MidiNoteNumber, (byte)127);
        Velocity = Math.Min(preset.Velocity, (byte)127);
        PlayheadIndex = 0;
        IsPlaying = false;
    }

    private static bool[] NormalizeGrid(bool[]? source)
    {
        var result = new bool[TimingConstants.TotalSteps];
        if (source is not null)
            Array.Copy(source, result, Math.Min(source.Length, result.Length));
        return result;
    }
}
