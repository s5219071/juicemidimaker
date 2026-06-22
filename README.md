# JuiceMidiMaker

JuiceMidiMaker is a Windows desktop sample sequencer designed by Kino. It turns a
single WAV, MP3, or AIFF one-shot into a 16-bar, 1/16-note pattern and exports the
result as a standard MIDI file.

## Features

- Fixed 960 PPQ timing with 256 steps across 16 bars
- Overlapping real-time one-shot playback through NAudio
- MIDI export with tempo, 4/4 time signature, note, and velocity data
- `.jmk` JSON presets with configurable storage folder
- Preset search, category filtering, collision-safe saving, and corrupt-file reporting
- Dark orange interface with a docked preset manager

## Requirements

- Windows 10 or later
- .NET 8 SDK for development
- A Windows audio output device for sample preview

## Build and run

```powershell
dotnet restore .\JuiceMidiMaker\JuiceMidiMaker.csproj
dotnet run --project .\JuiceMidiMaker\JuiceMidiMaker.csproj
```

Run the focused model and preset tests with:

```powershell
dotnet test .\JuiceMidiMaker.Tests\JuiceMidiMaker.Tests.csproj
```

Presets default to `Documents\JuiceMidiMaker\Presets`. The folder can be changed
at runtime from the preset panel.
