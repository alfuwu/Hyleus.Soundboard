using System;
using System.IO;
using Hyleus.Soundboard.Framework;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Decoders;
internal sealed class AiffDecoder : ISoundDecoder, IDisposable {
    private readonly Stream _stream;
    private readonly BinaryReader _reader;
    private long _dataStart;
    private long _dataEnd;
    private bool _eos;

    public int Channels { get; }
    public int SampleRate { get; }
    public int TargetSampleRate { get; }
    public int Length { get; }
    public SampleFormat SampleFormat => SampleFormat.F32;
    public bool IsDisposed { get; private set; }
    private double _resamplePosition;
    private double _resampleStep => (double)SampleRate / TargetSampleRate / 2;

    public event EventHandler<EventArgs> EndOfStreamReached;

    private readonly int _bitsPerSample;
    private readonly int _bytesPerSample;

    public AiffDecoder(Stream stream, AudioFormat format) {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
        _reader = new BinaryReader(stream);

        ParseHeader(out var c, out var sr, out _bitsPerSample, out long totalFrames);
        Channels = c;
        SampleRate = sr;
        TargetSampleRate = format.SampleRate;
        _bytesPerSample = (_bitsPerSample + 7) / 8;

        Length = totalFrames > 0 ? (int)(totalFrames * Channels) : 0;
    }

    private void ParseHeader(out int channels, out int sampleRate, out int bitsPerSample, out long totalFrames) {
        channels = 0;
        sampleRate = 0;
        bitsPerSample = 0;
        totalFrames = 0;

        string form = ReadFourCC();
        if (form != "FORM")
            throw new InvalidDataException("Not an AIFF file");

        ReadBEInt32(); // file size
        string formType = ReadFourCC();
        if (formType != "AIFF" && formType != "AIFC")
            throw new InvalidDataException("Unsupported AIFF type");

        while (_stream.Position < _stream.Length) {
            string chunkId = ReadFourCC();
            int chunkSize = ReadBEInt32();
            long chunkStart = _stream.Position;

            switch (chunkId) {
                case "COMM":
                    channels = ReadBEInt16();
                    totalFrames = ReadBEInt32();
                    bitsPerSample = ReadBEInt16();
                    sampleRate = ReadIeeeExtended();
                    break;

                case "SSND":
                    int offset = ReadBEInt32();
                    ReadBEInt32(); // block size
                    _dataStart = _stream.Position + offset;
                    _dataEnd = _dataStart + (chunkSize - 8);
                    _stream.Position = _dataStart;
                    break;
            }

            _stream.Position = chunkStart + chunkSize + (chunkSize & 1);
        }

        if (_dataStart == 0)
            throw new InvalidDataException("Missing SSND chunk");

        _stream.Position = _dataStart;
    }

    public int Decode(Span<float> samples) {
        if (IsDisposed || _eos)
            return 0;

        int written = 0;
        while (written < samples.Length && _stream.Position < _dataEnd) {
            long srcPos = (long)_resamplePosition;
            Seek((int)(srcPos * Channels)); // seek to source frame

            float s1 = ReadSample(); // current frame
            float s2 = _stream.Position < _dataEnd ? ReadSample() : s1; // next frame
            float frac = (float)(_resamplePosition - srcPos);

            samples[written++] = s1 + frac * (s2 - s1); // linear interpolation

            _resamplePosition += _resampleStep;
        }

        if (written == 0) {
            _eos = true;
            EndOfStreamReached?.Invoke(this, EventArgs.Empty);
        }

        return written;
    }

    private float ReadSample() {
        return _bitsPerSample switch {
            8 => (sbyte)_reader.ReadByte() / 128f,
            16 => ReadBEInt16() / 32768f,
            24 => ReadBEInt24() / 8388608f,
            32 => ReadBEInt32() / 2147483648f,
            _ => throw new NotSupportedException($"Unsupported bit depth {_bitsPerSample}")
        };
    }

    public bool Seek(int offset) {
        long frame = offset / Channels;
        _stream.Position = _dataStart + frame * Channels * _bytesPerSample;
        _eos = false;
        return true;
    }

    public void Dispose() {
        if (IsDisposed)
            return;

        _reader?.Dispose();
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    ~AiffDecoder() => Dispose();

    private string ReadFourCC() =>
        new(_reader.ReadChars(4));

    private short ReadBEInt16() =>
        (short)((_reader.ReadByte() << 8) | _reader.ReadByte());

    private int ReadBEInt32() {
        int b1 = _reader.ReadByte();
        int b2 = _reader.ReadByte();
        int b3 = _reader.ReadByte();
        int b4 = _reader.ReadByte();
        return (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;
    }

    private int ReadBEInt24() {
        int value = (_reader.ReadByte() << 16) |
                    (_reader.ReadByte() << 8) |
                    _reader.ReadByte();
        // Sign-extend if negative
        if ((value & 0x800000) != 0) value |= unchecked((int)0xFF000000);
        return value;
    }

    // IEEE 80-bit extended float (AIFF sample rate)
    private int ReadIeeeExtended() {
        byte[] bytes = _reader.ReadBytes(10);

        int expon = ((bytes[0] & 0x7F) << 8) | bytes[1];
        long hiMant = ((long)bytes[2] << 24) |
                      ((long)bytes[3] << 16) |
                      ((long)bytes[4] << 8) |
                      bytes[5];
        long loMant = ((long)bytes[6] << 24) |
                      ((long)bytes[7] << 16) |
                      ((long)bytes[8] << 8) |
                      bytes[9];

        if (expon == 0 && hiMant == 0 && loMant == 0)
            return 0;

        double value =
            (hiMant * Math.Pow(2, expon - 16383 - 31)) +
            (loMant * Math.Pow(2, expon - 16383 - 63));

        return (int)value;
    }
}