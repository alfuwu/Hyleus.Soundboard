using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hyleus.Soundboard.Framework;
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
    private readonly LinkedList<float> _sampleQueue = [];
    private bool _eos;
    private bool _isDisposed;

    public int Channels { get; private set; }
    public int SampleRate { get; private set; }
    public int TargetSampleRate { get; }
    public int Length => 0;
    public SampleFormat SampleFormat => SampleFormat.F32;
    public bool IsDisposed => _isDisposed;
    private double _resamplePosition;
    private double _resampleStep => (double)SampleRate / TargetSampleRate;

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

            Enqueue(_sampleQueue, _buffer);
        }

        int n = Math.Min(samples.Length, _sampleQueue.Count);
        for (int i = 0; i < n; i++) {
            samples[i] = _sampleQueue.First.Value;
            _sampleQueue.RemoveFirst();
        }

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

    public static void Enqueue(LinkedList<float> sampleQueue, SampleBuffer buffer) {
        byte[] data = buffer.Data;
        int bytesPerSample = buffer.BitsPerSample / 8;

        if (bytesPerSample < 1 || bytesPerSample > 4)
            throw new NotSupportedException($"Unsupported PCM bit depth ({buffer.BitsPerSample}); expected one of 8, 16, 24, 32");

        if (!buffer.BigEndian)
            buffer.SetBigEndian(true);

        int sampleCount = data.Length / bytesPerSample;

        // hell
        if (bytesPerSample == 1) {
            for (int i = 0; i < sampleCount; i++)
                sampleQueue.AddLast(((sbyte)data[i]) / 128f);
        } else if (bytesPerSample == 2) {
            for (int i = 0; i < sampleCount; i++) {
                short pcm = (short)((data[i * 2] << 8) | data[i * 2 + 1]);
                sampleQueue.AddLast(pcm / 32768f);
            }
        } else if (bytesPerSample == 3) {
            for (int i = 0; i < sampleCount; i++) {
                int pcm = (data[i * 3] << 16) | (data[i * 3 + 1] << 8) | data[i * 3 + 2];
                sampleQueue.AddLast(pcm / 8388608f);
            }
        } else {
            for (int i = 0; i < sampleCount; i++) {
                int pcm = (data[i * 4] << 24) | (data[i * 4 + 1] << 16) | (data[i * 4 + 2] << 8) | data[i * 4 + 3];
                sampleQueue.AddLast(pcm / 2147483648f);
            }
        }
    }
}
