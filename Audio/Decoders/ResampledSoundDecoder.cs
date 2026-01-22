using System;
using SoundFlow.Enums;
using SoundFlow.Interfaces;

namespace Hyleus.Soundboard.Audio.Decoders;
public abstract class ResampledSoundDecoder : ISoundDecoder {
    public abstract bool IsDisposed { get; protected set; }
    public abstract int Length { get; }

    public abstract SampleFormat SampleFormat { get; }

    public abstract int Channels { get; }
    public abstract int SampleRate { get; }
    public abstract int TargetSampleRate { get; }

    public abstract event EventHandler<EventArgs> EndOfStreamReached;

    protected WindowedSincResampler _resampler;
    private float[] _sourceBuffer = [];
    private bool _eos;

    public void Init() {
        // currently outputting audio at too high speeds
        // TwT
        _resampler = new(
            channels: Channels,
            inputRate: SampleRate,
            outputRate: TargetSampleRate
        );
    }

    public abstract bool Seek(int offset);
    protected abstract int DecodeSource(Span<float> samples);
    public virtual int Decode(Span<float> samples) {
        if (_eos || IsDisposed)
            return 0;

        int totalWritten = 0;

        while (totalWritten < samples.Length) {
            // attempt to resample with existing buffered input
            int written = _resampler.Process(
                [],
                samples[totalWritten..]
            );

            if (written > 0) {
                totalWritten += written;
                continue;
            }

            // need more source data
            EnsureSourceCapacity(4096);

            int decoded = DecodeSource(_sourceBuffer);
            if (decoded == 0) {
                _eos = true;
                GetEndOfStreamReached()?.Invoke(this, EventArgs.Empty);
                break;
            }

            // feed new input into resampler
            written = _resampler.Process(
                _sourceBuffer.AsSpan(0, decoded),
                samples[totalWritten..]
            );

            totalWritten += written;
        }

        return totalWritten;
    }
    public abstract void Dispose();
    private void EnsureSourceCapacity(int samples) {
        if (_sourceBuffer.Length < samples)
            _sourceBuffer = new float[samples];
    }
    public abstract EventHandler<EventArgs> GetEndOfStreamReached();
}
