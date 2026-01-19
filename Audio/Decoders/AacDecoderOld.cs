using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SharpJaad.AAC;
using SoundFlow.Enums;
using SoundFlow.Interfaces;

namespace Hyleus.Soundboard.Audio.Decoders;
internal sealed class AacDecoderOld : ISoundDecoder, IDisposable {
    private readonly Stream _stream;
    private readonly Decoder _decoder;
    private readonly SampleBuffer _buffer;
    private readonly Queue<float> _sampleQueue = new();
    private bool _eos;
    private bool _isDisposed;

    public int Channels { get; private set; }
    public int SampleRate { get; private set; }
    public int Length => 0; // Unknown for streaming AAC
    public SampleFormat SampleFormat => SampleFormat.F32;
    public bool IsDisposed => _isDisposed;

    public event EventHandler<EventArgs> EndOfStreamReached;

    public AacDecoderOld(Stream stream) {
        var decoderConfig = new DecoderConfig();
        decoderConfig.SetProfile(Profile.AAC_LTP);
        decoderConfig.SetSampleFrequency(SampleFrequency.SAMPLE_FREQUENCY_44100);
        decoderConfig.SetChannelConfiguration((ChannelConfiguration)2);

        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _decoder = new Decoder(decoderConfig);
        _buffer = new SampleBuffer();
    }

    private byte[] ReadNextFrame() {
        // ADTS header is 7 bytes
        Span<byte> header = stackalloc byte[7];
        int read = _stream.Read(header);
        if (read < 7)
            return null;

        // extract frame length (13 bits in header)
        int frameLength = ((header[3] & 0x03) << 11) | (header[4] << 3) | ((header[5] & 0xE0) >> 5);
        //frameLength -= 7; // subtract header

        //Log.Info(frameLength);
        byte[] frame = new byte[frameLength];
        read = _stream.Read(frame, 0, frameLength);
        if (read < frameLength)
            return null;

        return frame;
    }

    public int Decode(Span<float> samples) {
        if (_isDisposed || _eos)
            return 0;

        while (_sampleQueue.Count < samples.Length && !_eos) {
            var frame = ReadNextFrame();
            if (frame == null) {
                _eos = true;
                EndOfStreamReached?.Invoke(this, EventArgs.Empty);
                break;
            }

            _decoder.DecodeFrame(frame, _buffer);

            if (Channels == 0) {
                Channels = _buffer.Channels;
                SampleRate = _buffer.SampleRate;
            }

            // this is probably bad and doesnt work
            ReadOnlySpan<float> floatSpan =
                MemoryMarshal.Cast<byte, float>(_buffer.Data);

            for (int i = 0; i < floatSpan.Length; i += _buffer.Channels)
                for (int c = 0; c < _buffer.Channels; c++)
                    _sampleQueue.Enqueue(floatSpan[i + c]);
        }

        int n = Math.Min(samples.Length, _sampleQueue.Count);
        for (int i = 0; i < n; i++)
            samples[i] = _sampleQueue.Dequeue();

        return n;
    }

    public bool Seek(int offset) {
        if (!_stream.CanSeek)
            return false;

        _stream.Seek(offset, SeekOrigin.Begin);
        _sampleQueue.Clear();
        _eos = false;
        return true;
    }

    public void Dispose() {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
