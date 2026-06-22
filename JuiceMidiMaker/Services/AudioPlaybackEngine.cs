using System.IO;
using JuiceMidiMaker.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace JuiceMidiMaker.Services;

public sealed class AudioPlaybackEngine : IDisposable
{
    private const int MixerSampleRate = 44100;
    private const int MixerChannels = 2;

    private readonly object _sync = new();
    private CachedSample? _sample;
    private MixingSampleProvider? _mixer;
    private IWavePlayer? _output;
    private bool _disposed;

    public bool HasSample => _sample is not null;

    public SampleTrack LoadSample(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var sample = CachedSample.Load(filePath);
        lock (_sync)
            _sample = sample;

        return new SampleTrack
        {
            FileName = Path.GetFileName(filePath),
            FilePath = Path.GetFullPath(filePath),
            AudioWaveBuffer = CreateWaveformPreview(sample.AudioData, sample.WaveFormat.Channels),
            SampleRate = sample.WaveFormat.SampleRate,
            Channels = sample.WaveFormat.Channels
        };
    }

    public void TriggerSample(float volume = 1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (_sample is null)
                return;

            EnsureOutput();
            var source = new CachedSampleProvider(_sample);
            ISampleProvider converted = source;

            if (source.WaveFormat.SampleRate != MixerSampleRate)
                converted = new WdlResamplingSampleProvider(converted, MixerSampleRate);

            converted = converted.WaveFormat.Channels switch
            {
                1 => new MonoToStereoSampleProvider(converted),
                2 => converted,
                _ => throw new NotSupportedException("Only mono and stereo samples are supported.")
            };

            var gain = new VolumeSampleProvider(converted)
            {
                Volume = Math.Clamp(volume, 0f, 1f)
            };

            _mixer!.AddMixerInput(gain);
        }
    }

    public void StopAll()
    {
        lock (_sync)
            _mixer?.RemoveAllMixerInputs();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_sync)
        {
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _mixer = null;
            _sample = null;
            _disposed = true;
        }
    }

    private void EnsureOutput()
    {
        if (_output is not null)
            return;

        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(
            MixerSampleRate,
            MixerChannels))
        {
            ReadFully = true
        };

        _output = new WaveOutEvent();
        _output.Init(_mixer);
        _output.Play();
    }

    private static float[] CreateWaveformPreview(float[] samples, int channels)
    {
        const int previewPoints = 256;
        var result = new float[previewPoints];
        var frames = samples.Length / Math.Max(channels, 1);
        if (frames == 0)
            return result;

        for (var point = 0; point < previewPoints; point++)
        {
            var startFrame = point * frames / previewPoints;
            var endFrame = Math.Max(startFrame + 1, (point + 1) * frames / previewPoints);
            var peak = 0f;

            for (var frame = startFrame; frame < Math.Min(endFrame, frames); frame++)
            {
                for (var channel = 0; channel < channels; channel++)
                    peak = Math.Max(peak, Math.Abs(samples[(frame * channels) + channel]));
            }

            result[point] = peak;
        }

        return result;
    }

    private sealed class CachedSample
    {
        private CachedSample(float[] audioData, WaveFormat waveFormat)
        {
            AudioData = audioData;
            WaveFormat = waveFormat;
        }

        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }

        public static CachedSample Load(string filePath)
        {
            using var reader = new AudioFileReader(filePath);
            if (reader.WaveFormat.Channels is not (1 or 2))
                throw new NotSupportedException("Only mono and stereo samples are supported.");

            var samples = new List<float>((int)Math.Min(reader.Length / 4, int.MaxValue));
            var readBuffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int samplesRead;

            while ((samplesRead = reader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                samples.AddRange(readBuffer.AsSpan(0, samplesRead).ToArray());

            return new CachedSample(samples.ToArray(), reader.WaveFormat);
        }
    }

    private sealed class CachedSampleProvider : ISampleProvider
    {
        private readonly CachedSample _sample;
        private int _position;

        public CachedSampleProvider(CachedSample sample) => _sample = sample;

        public WaveFormat WaveFormat => _sample.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var available = _sample.AudioData.Length - _position;
            var samplesToCopy = Math.Min(available, count);
            Array.Copy(_sample.AudioData, _position, buffer, offset, samplesToCopy);
            _position += samplesToCopy;
            return samplesToCopy;
        }
    }
}
