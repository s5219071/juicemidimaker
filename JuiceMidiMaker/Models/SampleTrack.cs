namespace JuiceMidiMaker.Models;

public sealed class SampleTrack
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public float[]? AudioWaveBuffer { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
}
