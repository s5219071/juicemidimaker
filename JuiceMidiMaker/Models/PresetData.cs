namespace JuiceMidiMaker.Models;

public sealed class PresetData
{
    public string PresetName { get; set; } = "Untitled";
    public string Author { get; set; } = "Kino";
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int BPM { get; set; } = 128;
    public bool[] GridSteps { get; set; } = new bool[TimingConstants.TotalSteps];
    public byte MidiNoteNumber { get; set; } = 36;
    public byte Velocity { get; set; } = 100;
    public string? SampleFileName { get; set; }
}
