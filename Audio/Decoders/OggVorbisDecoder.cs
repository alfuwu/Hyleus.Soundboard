using System;
using System.IO;
using NVorbis;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders;
internal sealed class OggVorbisDecoder : ISoundDecoder, IDisposable {
    private readonly VorbisReader _reader;
    private bool _eos;

    public int Channels { get; }
    public int SampleRate { get; }
    public int TargetSampleRate { get; }
    public int Length { get; }
    public SampleFormat SampleFormat => SampleFormat.F32;
    public bool IsDisposed { get; private set; }
    private float[] _resampleBuffer = [];

    public event EventHandler<EventArgs> EndOfStreamReached;

    public OggVorbisDecoder(Stream stream, AudioFormat format) {
        ArgumentNullException.ThrowIfNull(stream);

        _reader = new VorbisReader(stream, false);

        Channels = _reader.Channels;
        SampleRate = _reader.SampleRate;
        TargetSampleRate = format.SampleRate;

        if (_reader.TotalSamples > 0)
            Length = (int)(_reader.TotalSamples * Channels);
    }

    public int Decode(Span<float> samples) {
        if (IsDisposed || _eos)
            return 0;

        Span<float> floatPacket = stackalloc float[samples.Length];
        int samplesRead = _reader.ReadSamples(floatPacket);

        if (samplesRead == 0) {
            _eos = true;
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);
        }

        Span<float> resampled = AudioEngine.Resample(floatPacket, SampleRate, TargetSampleRate, ref _resampleBuffer);
        resampled.CopyTo(samples);
        return resampled.Length;
    }

    public bool Seek(int offset) {
        long frame = offset / Channels;
        _reader.SamplePosition = frame;
        _eos = false;
        return true;
    }

    public void Dispose() {
        if (IsDisposed)
            return;

        _reader?.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~OggVorbisDecoder() => Dispose();
}
