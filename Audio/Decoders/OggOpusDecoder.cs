using System;
using System.IO;
using Concentus;
using Concentus.Oggfile;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders;
internal sealed class OggOpusDecoder : ResampledSoundDecoder, IDisposable {
    private readonly OpusOggReadStream _reader;
    private bool _eos;

    public override int Channels { get; }
    public override int SampleRate => 48000;
    public override int TargetSampleRate { get; }
    public override SampleFormat SampleFormat => SampleFormat.F32;
    public override int Length { get; }
    public override bool IsDisposed { get; protected set; }

    public override event EventHandler<EventArgs> EndOfStreamReached;
    public override EventHandler<EventArgs> GetEndOfStreamReached() => EndOfStreamReached;

    internal OggOpusDecoder(Stream stream, AudioFormat format) {
        _reader = new(OpusCodecFactory.CreateDecoder(48000, format.Channels), stream);
        Channels = format.Channels;
        TargetSampleRate = format.SampleRate;

        Init();
    }

    protected override int DecodeSource(Span<float> samples) {
        if (IsDisposed || _eos)
            return 0;

        short[] packet = _reader.DecodeNextPacket();

        if (packet == null || packet.Length == 0) {
            _eos = true;
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        int count = int.Min(packet.Length, samples.Length);
        for (int i = 0; i < count; i++)
            samples[i] = packet[i] / 32768f;

        return count;
    }

    public override bool Seek(int offset) {
        if (!_reader.CanSeek)
            return false;

        _reader.SeekTo(TimeSpan.FromSeconds(offset / Channels * 48000.0));
        _eos = false;
        return true;
    }

    public override void Dispose() {
        if (IsDisposed)
            return;

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
