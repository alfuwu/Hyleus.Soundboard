using System;
using System.Collections.Generic;
using SharpJaad.AAC;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders.Matroska;
internal sealed class AacDecoderWrapper : ISoundDecoder, IDisposable {
    private readonly List<byte[]> _frames;
    private readonly Decoder _decoder;
    private readonly SampleBuffer _buffer;
    private readonly LinkedList<float> _sampleQueue = [];
    private int _currentFrameIndex;
    private bool _eos;
    private bool _isDisposed;

    public int Channels { get; private set; }
    public int SampleRate { get; private set; }
    public int TargetSampleRate { get; private set; }
    public int Length => 0;
    public SampleFormat SampleFormat => SampleFormat.F32;
    public bool IsDisposed => _isDisposed;
    private double _resamplePosition;
    private double _resampleStep => (double)SampleRate / TargetSampleRate / 2;

    public event EventHandler<EventArgs> EndOfStreamReached;

    public AacDecoderWrapper(List<byte[]> frames, AudioFormat format, AudioFormat? targetFormat) {
        if (frames == null || frames.Count == 0)
            throw new ArgumentException("No audio frames provided", nameof(frames));

        _frames = frames;
        byte[] decoderSpecificInfo = _frames[0];
        _frames.RemoveAt(0);

        Channels = format.Channels; 
        SampleRate = format.SampleRate;
        TargetSampleRate = targetFormat?.SampleRate ?? SampleRate;

        _decoder = new Decoder(decoderSpecificInfo);
        _buffer = new SampleBuffer();
    }

    public int Decode(Span<float> samples) {
        if (_isDisposed || _eos)
            return 0;

        while (_sampleQueue.Count < samples.Length && !_eos) {
            if (_currentFrameIndex >= _frames.Count) {
                _eos = true;
                EndOfStreamReached?.Invoke(this, EventArgs.Empty);
                break;
            }

            byte[] frameData = _frames[_currentFrameIndex++];
            _decoder.DecodeFrame(frameData, _buffer);

            AacDecoder.Enqueue(_sampleQueue, _buffer);
        }

        int n = Math.Min(samples.Length, _sampleQueue.Count);
        int written = 0;
        while (written < n) {
            long srcPos = (long)_resamplePosition;

            float s1 = _sampleQueue.First.Value; // current frame
            float s2 = _sampleQueue.First.Next?.Value ?? s1; // next frame
            float frac = (float)(_resamplePosition - srcPos);

            samples[written++] = s1 + frac * (s2 - s1); // linear interpolation

            _resamplePosition += _resampleStep;
            if ((long)_resamplePosition - srcPos > 1)
                _sampleQueue.RemoveFirst();
        }

        return n;
    }

    public bool Seek(int frameIndex) {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
            return false;

        _currentFrameIndex = frameIndex;
        _sampleQueue.Clear();
        _eos = false;
        return true;
    }

    public void Dispose() {
        if (_isDisposed)
            return;

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
