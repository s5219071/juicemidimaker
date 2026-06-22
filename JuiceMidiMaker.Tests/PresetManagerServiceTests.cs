using JuiceMidiMaker.Models;
using JuiceMidiMaker.Services;

namespace JuiceMidiMaker.Tests;

public sealed class PresetManagerServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "JuiceMidiMaker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveAndLoad_RoundTripsPreset()
    {
        var service = new PresetManagerService(_directory);
        var source = new PresetData
        {
            PresetName = "Hard Kick 128",
            Category = "Kick",
            BPM = 128,
            GridSteps = Enumerable.Range(0, TimingConstants.TotalSteps)
                .Select(index => index % 4 == 0)
                .ToArray()
        };

        var path = service.Save(source);
        var loaded = service.Load(path);

        Assert.EndsWith(PresetManagerService.Extension, path);
        Assert.Equal(source.PresetName, loaded.PresetName);
        Assert.Equal(source.GridSteps, loaded.GridSteps);
    }

    [Fact]
    public void Save_WhenNameAlreadyExists_CreatesUniqueFile()
    {
        var service = new PresetManagerService(_directory);
        var preset = new PresetData { PresetName = "Duplicate" };

        var first = service.Save(preset);
        var second = service.Save(preset);

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void GetAllPresets_MarksCorruptedFileAsError()
    {
        var service = new PresetManagerService(_directory);
        var path = Path.Combine(_directory, "Broken.jmk");
        File.WriteAllText(path, "not valid json");

        var result = Assert.Single(service.GetAllPresets());

        Assert.Equal("Error", result.Category);
        Assert.False(result.IsValid);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
