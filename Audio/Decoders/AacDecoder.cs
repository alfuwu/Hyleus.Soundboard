using System;
using Hyleus.Soundboard.Framework;
using Hyleus.Soundboard.Framework.Enums;
using Hyleus.Soundboard.Framework.Interfaces;
using SoundFlow.Enums;
using SoundFlow.Interfaces;

namespace Hyleus.Soundboard.Audio.Decoders;
sealed unsafe class AacDecoder : ISoundDecoder, IDisposable {
    private readonly IAacFrameSource _source;
    private readonly IntPtr _decoder;
    private bool _eos;

    private readonly short[] _pcm16;
    private readonly int _maxSamples;

    public int Channels { get; }
    public int SampleRate { get; }
    public int Length { get; }
    public SampleFormat SampleFormat => SampleFormat.F32;
    public bool IsDisposed { get; private set; }

    public event EventHandler<EventArgs> EndOfStreamReached;

    public AacDecoder(IAacFrameSource source, int maxChannels = 2) {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _decoder = FdkAacNative.aacDecoder_Open(
            AacTransportType.TT_MP4_RAW,
            1
        );

        if (_decoder == IntPtr.Zero)
            throw new InvalidOperationException("Failed to open FDK-AAC decoder");

        _maxSamples = 2048 * maxChannels;
        _pcm16 = new short[_maxSamples];

        Channels = FdkAacNative.aacDecoder_GetParam(
            _decoder,
            AacDecoderParam.AAC_PCM_OUTPUT_CHANNELS
        );

        SampleRate = FdkAacNative.aacDecoder_GetParam(
            _decoder,
            AacDecoderParam.AAC_PCM_OUTPUT_SAMPLE_RATE
        );
    }

    public int Decode(Span<float> samples) {
        if (IsDisposed || _eos)
            return 0;

        if (!_source.TryGetNextFrame(out var frame)) {
            _eos = true;
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        fixed (byte* framePtr = frame.Span) {
            IntPtr buf = (IntPtr)framePtr;
            int size = frame.Length;
            int valid = size;

            var err = FdkAacNative.aacDecoder_Fill(
                _decoder,
                ref buf,
                ref size,
                ref valid
            );

            if (err != AacDecoderError.OK)
                return 0;

            fixed (short* pcmPtr = _pcm16) {
                err = FdkAacNative.aacDecoder_DecodeFrame(
                    _decoder,
                    pcmPtr,
                    _pcm16.Length,
                    0
                );

                if (err != AacDecoderError.OK)
                    return 0;

                int sampleCount = Math.Min(samples.Length, _pcm16.Length);

                for (int i = 0; i < sampleCount; i++)
                    samples[i] = _pcm16[i] / 32768f;

                return sampleCount;
            }
        }
    }

    public bool Seek(int offset) {
        _source.SeekToFrame(offset / Channels);
        _eos = false;
        return true;
    }

    public void Dispose() {
        if (IsDisposed)
            return;

        _source?.Dispose();
        FdkAacNative.aacDecoder_Close(_decoder);
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~AacDecoder() => Dispose();
}