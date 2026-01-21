using System;
using System.Collections.Generic;
using Hyleus.Soundboard.Audio.Decoders.Matroska;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders;
public class MatroskaDecoder : ISoundDecoder {
    private readonly ISoundDecoder _decoder;

    public int Channels => _decoder.Channels;
    public int SampleRate => _decoder.SampleRate;
    public int TargetSampleRate { get; }
    public int Length => _decoder.Length;

    public bool IsDisposed => _decoder.IsDisposed;
    public SampleFormat SampleFormat => _decoder.SampleFormat;

    public event EventHandler<EventArgs> EndOfStreamReached {
        add { _decoder.EndOfStreamReached += value; }
        remove { _decoder.EndOfStreamReached -= value; }
    }

    public MatroskaDecoder(List<byte[]> packets, string codec, AudioFormat format, AudioFormat? targetFormat) {
        if (codec == "A_OPUS")
            _decoder = new OpusDecoderWrapper(packets, format, targetFormat);
        else if (codec == "A_VORBIS")
            _decoder = new VorbisDecoderWrapper(packets, format, targetFormat);
        else
            throw new NotSupportedException($"Codec {codec} is not supported");

        TargetSampleRate = targetFormat?.SampleRate ?? format.SampleRate;
    }

    public bool Seek(int offset) => _decoder.Seek(offset);
    public int Decode(Span<float> samples) => _decoder.Decode(samples);
    public void Dispose() {
        _decoder.Dispose();
        GC.SuppressFinalize(this);
    }
}