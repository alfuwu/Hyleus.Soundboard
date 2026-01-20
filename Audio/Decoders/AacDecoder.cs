using System;
using System.Collections.Generic;
using System.IO;
using SharpJaad.AAC;
using SharpJaad.MP4;
using SharpJaad.MP4.API;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;
using static SharpJaad.MP4.API.AudioTrack;

namespace Hyleus.Soundboard.Audio.Decoders;
internal sealed class AacDecoder : ISoundDecoder, IDisposable {
    private readonly Stream _stream;
    private readonly AudioTrack _track;
    private readonly Decoder _decoder;
    private readonly SampleBuffer _buffer;
    private readonly Queue<float> _sampleQueue = new();
    private bool _eos;
    private bool _isDisposed;

    public int Channels { get; private set; }
    public int SampleRate { get; private set; }
    public int TargetSampleRate { get; }
    public int Length => 0; // Unknown for streaming AAC
    public SampleFormat SampleFormat => SampleFormat.F32;
    public bool IsDisposed => _isDisposed;

    public event EventHandler<EventArgs> EndOfStreamReached;

    public AacDecoder(Stream stream, AudioFormat format) {
        MP4Container container = new(stream);
        Movie movie = container.GetMovie();

        foreach (var track in movie.GetTracks()) {
            if (track is AudioTrack audio && (AudioCodec)audio.GetCodec() == AudioCodec.AAC) {
                _track = audio;
                break;
            }
        }

        if (_track == null)
            throw new Exception("Provided file possesses no audio track");

        Channels = _track.GetChannelCount();
        SampleRate = _track.GetSampleRate();
        TargetSampleRate = format.SampleRate;

        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _decoder = new Decoder(_track.GetDecoderSpecificInfo());
        _buffer = new SampleBuffer();
    }

    public int Decode(Span<float> samples) {
        if (_isDisposed || _eos)
            return 0;

        while (_sampleQueue.Count < samples.Length && !_eos) {
            var frame = _track.ReadNextFrame();
            if (frame == null) {
                _eos = true;
                EndOfStreamReached?.Invoke(this, EventArgs.Empty);
                break;
            }

            _decoder.DecodeFrame(frame.GetData(), _buffer);

            byte[] data = _buffer.Data;
            int bytesPerSample = _buffer.BitsPerSample / 8;

            if (bytesPerSample < 1 || bytesPerSample > 4)
                throw new NotSupportedException($"Unsupported PCM bit depth ({_buffer.BitsPerSample}); expected one of 8, 16, 24, 32");

            if (!_buffer.BigEndian)
                _buffer.SetBigEndian(true);

            int sampleCount = data.Length / bytesPerSample;

            // hell
            if (bytesPerSample == 1) {
                for (int i = 0; i < sampleCount; i++)
                    _sampleQueue.Enqueue(((sbyte)data[i]) / 128f);
            } else if (bytesPerSample == 2) {
                for (int i = 0; i < sampleCount; i++) {
                    short pcm = (short)((data[i * 2] << 8) | data[i * 2 + 1]);
                    _sampleQueue.Enqueue(pcm / 32768f);
                }
            } else if (bytesPerSample == 3) {
                for (int i = 0; i < sampleCount; i++) {
                    int pcm = (data[i * 2] << 16) | (data[i * 2 + 1] << 8) | data[i * 2 + 2];
                    _sampleQueue.Enqueue(pcm / 8388608f);
                }
            } else {
                for (int i = 0; i < sampleCount; i++) {
                    int pcm = (data[i * 2] << 24) | (data[i * 2 + 1] << 16) | (data[i * 2 + 2] << 8) | data[i * 2 + 3];
                    _sampleQueue.Enqueue(pcm / 2147483648f);
                }
            }
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
