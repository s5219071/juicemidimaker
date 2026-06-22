using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JuiceMidiMaker.Models;
using JuiceMidiMaker.Services;
using Microsoft.Win32;

namespace JuiceMidiMaker.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SequencerSession _session = new();
    private readonly AudioPlaybackEngine _audioEngine;
    private readonly MidiExportService _midiExportService;
    private readonly PresetManagerService _presetService;
    private readonly DispatcherTimer _playbackTimer;
    private readonly Stopwatch _playbackClock = new();
    private SampleTrack _track = new();
    private long _processedStepCount;
    private long _clockAnchorStepCount;
    private bool _disposed;

    public MainViewModel()
        : this(new AudioPlaybackEngine(), new MidiExportService(), new PresetManagerService())
    {
    }

    internal MainViewModel(
        AudioPlaybackEngine audioEngine,
        MidiExportService midiExportService,
        PresetManagerService presetService)
    {
        _audioEngine = audioEngine;
        _midiExportService = midiExportService;
        _presetService = presetService;

        Steps = new ObservableCollection<StepViewModel>(
            Enumerable.Range(0, TimingConstants.TotalSteps).Select(index => new StepViewModel(index)));
        BarGroups = SequencerLayout.CreateBarGroups(Steps);

        PresetDirectory = _presetService.PresetDirectory;
        FilteredPresets = CollectionViewSource.GetDefaultView(PresetList);
        FilteredPresets.Filter = FilterPreset;

        _playbackTimer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(4)
        };
        _playbackTimer.Tick += PlaybackTimerOnTick;

        RefreshPresetList();
        StatusMessage = "Load a WAV or MP3 sample to begin.";
    }

    public event Action<string, bool>? NotificationRequested;
    public event Action? GridRefreshRequested;

    public ObservableCollection<StepViewModel> Steps { get; }
    public IReadOnlyList<BarGroupViewModel> BarGroups { get; }
    public ObservableCollection<PresetFileInfo> PresetList { get; } = new();
    public ICollectionView FilteredPresets { get; }
    public IReadOnlyList<string> Categories { get; } = ["All", "Kick", "Snare", "Hat", "Custom"];
    public bool CanUseSelectedPreset => SelectedPreset is not null;

    [ObservableProperty]
    private int _bpm = 128;

    [ObservableProperty]
    private byte _midiNoteNumber = 36;

    [ObservableProperty]
    private byte _velocity = 100;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _playheadIndex;

    [ObservableProperty]
    private string _sampleFileName = "No sample loaded";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private PresetFileInfo? _selectedPreset;

    [ObservableProperty]
    private string _presetSaveName = string.Empty;

    [ObservableProperty]
    private string _presetCategory = "Custom";

    [ObservableProperty]
    private string _presetDescription = string.Empty;

    [ObservableProperty]
    private string _presetDirectory = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    partial void OnBpmChanged(int value)
    {
        if (!_session.IsBpmValid(value))
        {
            StatusMessage = "BPM must be between 20 and 400.";
            return;
        }

        _session.BPM = value;
        if (IsPlaying)
            ReanchorPlaybackClock();
    }

    partial void OnMidiNoteNumberChanged(byte value)
    {
        if (value <= 127)
            _session.MidiNoteNumber = value;
        else
            StatusMessage = "MIDI note must be between 0 and 127.";
    }

    partial void OnVelocityChanged(byte value)
    {
        if (value <= 127)
            _session.Velocity = value;
        else
            StatusMessage = "Velocity must be between 0 and 127.";
    }

    partial void OnSearchTextChanged(string value) => FilteredPresets.Refresh();

    partial void OnSelectedCategoryChanged(string value) => FilteredPresets.Refresh();

    partial void OnSelectedPresetChanged(PresetFileInfo? value)
        => OnPropertyChanged(nameof(CanUseSelectedPreset));

    [RelayCommand]
    private void ToggleStep(StepViewModel? step)
    {
        if (step is null)
            return;

        step.IsActive = !step.IsActive;
        _session.GridSteps[step.Index] = step.IsActive;
    }

    [RelayCommand]
    private void ClearPattern()
    {
        foreach (var step in Steps)
            step.IsActive = false;

        Array.Clear(_session.GridSteps);
        Report("Pattern cleared.");
    }

    [RelayCommand]
    private void CreateFourOnFloor()
    {
        foreach (var step in Steps)
        {
            step.IsActive = step.Index % 4 == 0;
            _session.GridSteps[step.Index] = step.IsActive;
        }

        Report("Created a four-on-the-floor pattern.");
    }

    [RelayCommand]
    private void LoadSample()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load audio sample",
            Filter = "Audio files (*.wav;*.mp3;*.aiff)|*.wav;*.mp3;*.aiff|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _track = _audioEngine.LoadSample(dialog.FileName);
            SampleFileName = _track.FileName;
            Report($"Loaded {_track.FileName} ({_track.SampleRate} Hz, {_track.Channels} ch).", notify: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Report($"Could not load the sample. {exception.Message}", isError: true, notify: true);
        }
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (IsPlaying)
        {
            StopPlayback();
            return;
        }

        if (!_session.IsBpmValid(Bpm))
        {
            Report("BPM must be between 20 and 400.", isError: true, notify: true);
            return;
        }

        _session.IsPlaying = true;
        _session.PlayheadIndex = 0;
        IsPlaying = true;
        ProcessStep(0);
        if (!IsPlaying)
            return;

        _processedStepCount = 1;
        _clockAnchorStepCount = _processedStepCount;
        _playbackClock.Restart();
        _playbackTimer.Start();
        Report(_audioEngine.HasSample ? "Playing pattern." : "Playing silently: load a sample for audio.");
    }

    [RelayCommand]
    private void ExportMidi()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export MIDI pattern",
            Filter = "MIDI file (*.mid)|*.mid",
            DefaultExt = ".mid",
            AddExtension = true,
            FileName = string.IsNullOrWhiteSpace(PresetSaveName) ? "JuicePattern.mid" : $"{PresetSaveName}.mid"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            SyncSessionFromUi();
            _midiExportService.Export(dialog.FileName, _session);
            Report($"MIDI exported to {dialog.FileName}", notify: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Report($"Could not export MIDI. {exception.Message}", isError: true, notify: true);
        }
    }

    [RelayCommand]
    private void SavePreset()
    {
        if (string.IsNullOrWhiteSpace(PresetSaveName))
        {
            Report("Please enter a preset name.", isError: true, notify: true);
            return;
        }

        try
        {
            SyncSessionFromUi();
            var preset = _session.ToPreset(PresetSaveName, PresetDescription, PresetCategory);
            preset.SampleFileName = string.IsNullOrWhiteSpace(_track.FileName) ? null : _track.FileName;
            var savedPath = _presetService.Save(preset);
            RefreshPresetList();
            Report($"Preset saved to {savedPath}", notify: true);
        }
        catch (UnauthorizedAccessException)
        {
            Report("You do not have permission to save in that folder.", isError: true, notify: true);
        }
        catch (IOException exception)
        {
            Report($"Could not save the preset. {exception.Message}", isError: true, notify: true);
        }
    }

    [RelayCommand]
    private void LoadPreset()
    {
        if (SelectedPreset is null)
            return;

        try
        {
            var preset = _presetService.Load(SelectedPreset.FilePath);
            StopPlayback();
            _session.ApplyPreset(preset);
            Bpm = _session.BPM;
            MidiNoteNumber = _session.MidiNoteNumber;
            Velocity = _session.Velocity;
            RefreshGridUi();
            Report($"Loaded preset: {preset.PresetName}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Report($"The preset could not be loaded. {exception.Message}", isError: true, notify: true);
        }
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (SelectedPreset is null)
            return;

        try
        {
            var name = SelectedPreset.PresetName;
            _presetService.Delete(SelectedPreset.FilePath);
            RefreshPresetList();
            Report($"Deleted preset: {name}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report($"Could not delete the preset. {exception.Message}", isError: true, notify: true);
        }
    }

    [RelayCommand]
    private void ChangePresetDirectory()
    {
        try
        {
            var selectedDirectory = _presetService.PickDirectory();
            _presetService.PresetDirectory = selectedDirectory;
            PresetDirectory = selectedDirectory;
            RefreshPresetList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Report($"Could not use that preset folder. {exception.Message}", isError: true, notify: true);
        }
    }

    [RelayCommand]
    private void OpenPresetFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _presetService.PresetDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is IOException or System.ComponentModel.Win32Exception)
        {
            Report($"Could not open the preset folder. {exception.Message}", isError: true, notify: true);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopPlayback();
        _playbackTimer.Tick -= PlaybackTimerOnTick;
        _audioEngine.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void PlaybackTimerOnTick(object? sender, EventArgs e)
    {
        if (!IsPlaying)
            return;

        var expectedStepCount = SequencerTimeline.GetExpectedProcessedStepCount(
            _clockAnchorStepCount,
            _playbackClock.Elapsed.TotalMilliseconds,
            Bpm);
        if (expectedStepCount <= _processedStepCount)
            return;

        // Do not bunch a long backlog of samples after the UI thread was blocked.
        if (expectedStepCount - _processedStepCount > 2)
            _processedStepCount = expectedStepCount - 1;

        while (_processedStepCount < expectedStepCount && IsPlaying)
        {
            ProcessStep(SequencerTimeline.GetStepIndex(_processedStepCount));
            _processedStepCount++;
        }
    }

    private void ProcessStep(int currentStep)
    {
        SetPlayhead(currentStep);

        if (_session.GridSteps[currentStep])
        {
            try
            {
                _audioEngine.TriggerSample(Velocity / 127f);
            }
            catch (Exception exception) when (exception is NAudio.MmException or NotSupportedException)
            {
                StopPlayback();
                Report($"Audio playback failed. {exception.Message}", isError: true, notify: true);
                return;
            }
        }

        _session.PlayheadIndex = (currentStep + 1) % TimingConstants.TotalSteps;
    }

    private void StopPlayback()
    {
        _playbackTimer.Stop();
        _playbackClock.Reset();
        _audioEngine.StopAll();
        _session.IsPlaying = false;
        _session.PlayheadIndex = 0;
        _processedStepCount = 0;
        _clockAnchorStepCount = 0;
        IsPlaying = false;
        SetPlayhead(-1);
    }

    private void ReanchorPlaybackClock()
    {
        _clockAnchorStepCount = _processedStepCount;
        _playbackClock.Restart();
    }

    private void SetPlayhead(int index)
    {
        if (PlayheadIndex >= 0 && PlayheadIndex < Steps.Count)
            Steps[PlayheadIndex].IsPlayhead = false;

        PlayheadIndex = index;
        if (index >= 0 && index < Steps.Count)
            Steps[index].IsPlayhead = true;
    }

    private void SyncSessionFromUi()
    {
        if (!_session.IsBpmValid(Bpm))
            throw new ArgumentOutOfRangeException(nameof(Bpm), "BPM must be between 20 and 400.");
        if (MidiNoteNumber > 127)
            throw new ArgumentOutOfRangeException(nameof(MidiNoteNumber), "MIDI note must be between 0 and 127.");
        if (Velocity > 127)
            throw new ArgumentOutOfRangeException(nameof(Velocity), "Velocity must be between 0 and 127.");

        _session.BPM = Bpm;
        _session.MidiNoteNumber = MidiNoteNumber;
        _session.Velocity = Velocity;
        for (var index = 0; index < Steps.Count; index++)
            _session.GridSteps[index] = Steps[index].IsActive;
    }

    private void RefreshGridUi()
    {
        for (var index = 0; index < Steps.Count; index++)
            Steps[index].IsActive = _session.GridSteps[index];

        GridRefreshRequested?.Invoke();
    }

    private void RefreshPresetList()
    {
        var selectedPath = SelectedPreset?.FilePath;
        PresetList.Clear();
        foreach (var preset in _presetService.GetAllPresets())
            PresetList.Add(preset);

        SelectedPreset = PresetList.FirstOrDefault(preset => preset.FilePath == selectedPath);
        FilteredPresets.Refresh();
    }

    private bool FilterPreset(object item)
    {
        if (item is not PresetFileInfo preset)
            return false;

        var matchesSearch = string.IsNullOrWhiteSpace(SearchText)
            || preset.PresetName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || preset.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        var matchesCategory = string.IsNullOrWhiteSpace(SelectedCategory)
            || SelectedCategory == "All"
            || preset.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase);

        return matchesSearch && matchesCategory;
    }

    private void Report(string message, bool isError = false, bool notify = false)
    {
        StatusMessage = message;
        if (notify)
            NotificationRequested?.Invoke(message, isError);
    }

}
