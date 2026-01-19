using System;
using System.IO;
using Concentus;
using Concentus.Oggfile;
using Hyleus.Soundboard.Framework;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders;
internal sealed class OggOpusDecoder(Stream stream, AudioFormat format) : ISoundDecoder, IDisposable {
    private readonly OpusOggReadStream _reader = new(OpusCodecFactory.CreateDecoder(48000, format.Channels), stream);
    private bool _eos;

    public int Channels { get; } = format.Channels;
    public int SampleRate => 48000;
    public int TargetSampleRate { get; } = format.SampleRate;
    public SampleFormat SampleFormat => SampleFormat.F32;
    public int Length { get; }
    public bool IsDisposed { get; private set; }
    private float[] _resampleBuffer = [];

    public event EventHandler<EventArgs> EndOfStreamReached;

    public int Decode(Span<float> samples) {
        if (IsDisposed || _eos)
            return 0;

        short[] packet = _reader.DecodeNextPacket();

        if (packet == null || packet.Length == 0) {
            _eos = true;
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        Span<float> floatPacket = stackalloc float[packet.Length];
        for (int i = 0; i < packet.Length; i++)
            floatPacket[i] = packet[i] / 32768f;

        int outCount = AudioEngine.Resample(floatPacket, samples, SampleRate, TargetSampleRate, ref _resampleBuffer);
        return outCount;
    }

    public bool Seek(int offset) {
        if (!_reader.CanSeek)
            return false;

        _reader.SeekTo(TimeSpan.FromSeconds(offset / Channels * 48000.0));
        _eos = false;
        return true;
    }

    public void Dispose() {
        if (IsDisposed)
            return;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
