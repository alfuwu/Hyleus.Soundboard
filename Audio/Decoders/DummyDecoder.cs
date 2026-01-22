using System;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders;
public sealed class DummySoundDecoder(AudioFormat format) : ISoundDecoder {
    private bool _disposed;
    private int _position;

    public bool IsDisposed => _disposed;
    public int Channels { get; } = format.Channels;
    public int SampleRate { get; } = format.SampleRate;
    public int Length { get; } = 0; // 0 means unknown / streaming
    public SampleFormat SampleFormat { get; } = format.Format;

    public event EventHandler<EventArgs> EndOfStreamReached;

    public int Decode(Span<float> samples) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (samples.Length == 0)
            return 0;

        if (Length > 0 && _position >= Length) {
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        int samplesToWrite = samples.Length;

        if (Length > 0) {
            int remaining = Length - _position;
            samplesToWrite = Math.Min(samplesToWrite, remaining);
        }

        samples[..samplesToWrite].Clear();

        _position += samplesToWrite;

        if (Length > 0 && _position >= Length)
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);

        return samplesToWrite;
    }

    public bool Seek(int offset) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (offset < 0)
            return false;

        if (Length > 0 && offset > Length)
            return false;

        _position = offset;
        return true;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _disposed = true;
    }
}