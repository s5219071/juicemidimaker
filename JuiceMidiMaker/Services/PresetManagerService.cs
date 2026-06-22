using System.IO;
using System.Text.Json;
using JuiceMidiMaker.Models;

namespace JuiceMidiMaker.Services;

public sealed class PresetManagerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string _presetDirectory;

    public PresetManagerService(string? presetDirectory = null)
    {
        _presetDirectory = presetDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "JuiceMidiMaker",
            "Presets");
        EnsureDirectoryExists();
    }

    public const string Extension = ".jmk";
    public const string FilterString = "JuiceMidiMaker Preset (*.jmk)|*.jmk";

    public string PresetDirectory
    {
        get => _presetDirectory;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _presetDirectory = Path.GetFullPath(value);
            EnsureDirectoryExists();
        }
    }

    public string Save(PresetData preset, string? customDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var directory = customDirectory ?? _presetDirectory;
        Directory.CreateDirectory(directory);

        preset.UpdatedAt = DateTime.UtcNow;
        var safeName = SanitizeFileName(preset.PresetName);
        var filePath = Path.Combine(directory, safeName + Extension);

        if (File.Exists(filePath))
        {
            var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            filePath = Path.Combine(directory, $"{safeName}_{suffix}{Extension}");
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(preset, JsonOptions));
        return filePath;
    }

    public PresetData Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Preset file was not found: {filePath}", filePath);

        var json = File.ReadAllText(filePath);
        var preset = JsonSerializer.Deserialize<PresetData>(json, JsonOptions)
            ?? throw new InvalidDataException("The preset file is empty or invalid.");

        if (string.IsNullOrWhiteSpace(preset.PresetName))
            throw new InvalidDataException("The preset has no name.");

        return preset;
    }

    public IReadOnlyList<PresetFileInfo> GetAllPresets(string? directory = null)
    {
        var targetDirectory = directory ?? _presetDirectory;
        if (!Directory.Exists(targetDirectory))
            return Array.Empty<PresetFileInfo>();

        return Directory
            .EnumerateFiles(targetDirectory, $"*{Extension}", SearchOption.TopDirectoryOnly)
            .Select(CreateFileInfo)
            .OrderBy(info => info.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(info => info.PresetName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Delete(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public string PickDirectory()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select the preset storage folder",
            SelectedPath = _presetDirectory,
            ShowNewFolderButton = true
        };

        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : _presetDirectory;
    }

    private PresetFileInfo CreateFileInfo(string path)
    {
        try
        {
            var preset = Load(path);
            return new PresetFileInfo
            {
                FilePath = path,
                PresetName = preset.PresetName,
                Category = preset.Category ?? "Uncategorized",
                Author = preset.Author,
                UpdatedAt = preset.UpdatedAt,
                Description = preset.Description,
                IsValid = true
            };
        }
        catch
        {
            return new PresetFileInfo
            {
                FilePath = path,
                PresetName = Path.GetFileNameWithoutExtension(path),
                Category = "Error",
                UpdatedAt = File.GetLastWriteTimeUtc(path),
                IsValid = false
            };
        }
    }

    private void EnsureDirectoryExists() => Directory.CreateDirectory(_presetDirectory);

    private static string SanitizeFileName(string name)
    {
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            name = name.Replace(invalidCharacter, '_');

        return string.IsNullOrWhiteSpace(name) ? "Preset" : name.Trim();
    }
}

public sealed class PresetFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string PresetName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Author { get; set; } = "Kino";
    public DateTime UpdatedAt { get; set; }
    public string? Description { get; set; }
    public bool IsValid { get; set; }
}
