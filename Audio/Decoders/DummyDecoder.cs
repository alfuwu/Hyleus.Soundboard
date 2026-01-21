using System;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders;
public sealed class DummySoundDecoder : ISoundDecoder {
    private bool _disposed;
    private int _position;

    public DummySoundDecoder(AudioFormat format) {
        SampleRate = format.SampleRate;
        Channels = format.Channels;
        Length = 0; // 0 means unknown / streaming
        SampleFormat = format.Format;
    }

    public bool IsDisposed => _disposed;

    public int Length { get; }

    public SampleFormat SampleFormat { get; }

    public int Channels { get; }

    public int SampleRate { get; }

    public event EventHandler<EventArgs> EndOfStreamReached;

    public int Decode(Span<float> samples) {
        ThrowIfDisposed();

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
        ThrowIfDisposed();

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

    private void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DummySoundDecoder));
    }
}