using System;
using System.Collections.Generic;
using NVorbis;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders.Matroska;
internal sealed class VorbisDecoderWrapper(List<byte[]> packets, AudioFormat format, AudioFormat? targetFormat) : ISoundDecoder {
    private readonly StreamDecoder _vorbis = new(new VorbisPacketProvider(packets));
    
    public int Channels { get; } = format.Channels;
    public int SampleRate { get; } = format.SampleRate;
    public int TargetSampleRate { get; } = targetFormat?.SampleRate ?? format.SampleRate;
    public int Length => 0;
    public bool IsDisposed { get; private set; }
    public SampleFormat SampleFormat => SampleFormat.F32;

    public event EventHandler<EventArgs> EndOfStreamReached;

    public bool Seek(int offset) {
        _vorbis.SamplePosition = offset;
        return true;
    }

    public int Decode(Span<float> samples) {
        int read = _vorbis.Read(samples, 0, samples.Length);
        if (read == 0)
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);
        return read;
    }

    public void Dispose() {
        _vorbis.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
