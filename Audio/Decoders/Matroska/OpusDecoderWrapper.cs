using System;
using System.Collections.Generic;
using System.Linq;
using Concentus;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders.Matroska;
internal class OpusDecoderWrapper : ISoundDecoder {
    private readonly IOpusDecoder _decoder;
    private readonly List<byte[]> _packets;
    private int _currentPacketIndex = 0;
    private readonly float[] _decodeBuffer = new float[960 * 2];
    private int _decodeBufferLength = 0;
    private int _decodeBufferPosition = 0;

    public int Channels { get; }
    public int SampleRate { get; }
    public int TargetSampleRate => SampleRate;
    public int Length => _packets.Sum(p => p.Length); // optional
    public bool IsDisposed { get; private set; }
    public SampleFormat SampleFormat => SampleFormat.F32;

    public event EventHandler<EventArgs> EndOfStreamReached;

    public OpusDecoderWrapper(List<byte[]> packets, AudioFormat format, AudioFormat? targetFormat) {
        _packets = packets;
        Channels = format.Channels;
        SampleRate = format.SampleRate;
        _decoder = OpusCodecFactory.CreateDecoder(SampleRate, Channels);
    }

    public bool Seek(int offset) {
        _currentPacketIndex = 0;
        _decodeBufferLength = 0;
        _decodeBufferPosition = 0;
        return true;
    }

    public int Decode(Span<float> samples) {
        int totalWritten = 0;
        while (totalWritten < samples.Length) {
            if (_decodeBufferPosition >= _decodeBufferLength) {
                if (_currentPacketIndex >= _packets.Count) {
                    EndOfStreamReached?.Invoke(this, EventArgs.Empty);
                    break;
                }
                var packet = _packets[_currentPacketIndex++];
                _decodeBufferLength = _decoder.Decode(packet, _decodeBuffer, _decodeBuffer.Length / Channels);
                _decodeBufferPosition = 0;
            }

            int remainingInBuffer = _decodeBufferLength * Channels - _decodeBufferPosition;
            int remainingInOutput = samples.Length - totalWritten;
            int toCopy = Math.Min(remainingInBuffer, remainingInOutput);

            for (int i = 0; i < toCopy; i++)
                samples[totalWritten++] = _decodeBuffer[_decodeBufferPosition++];
        }

        return totalWritten;
    }

    public void Dispose() {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
