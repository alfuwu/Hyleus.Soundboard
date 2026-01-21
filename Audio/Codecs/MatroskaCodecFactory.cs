using System;
using System.Collections.Generic;
using System.IO;
using Hyleus.Soundboard.Audio.Decoders;
using Hyleus.Soundboard.Framework;
using Hyleus.Soundboard.Framework.Enums;
using Hyleus.Soundboard.Framework.Extensions;
using Hyleus.Soundboard.Framework.Structs;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Codecs;
public sealed class MatroskaCodecFactory : ICodecFactory {
    public string FactoryId => "Hyleus.Matroska.OpusVorbis";

    public IReadOnlyCollection<string> SupportedFormatIds { get; } =
        ["mkv", "webm"];

    public int Priority => 100;

    public static unsafe int ReadVINT(Stream stream) {
        Span<byte> length = stackalloc byte[1];
        var read = stream.Read(length);

        if (read != 1)
            throw new EndOfStreamException("Unexpected end of stream");
        if (length[0] == 0)
            throw new Exception("Invalid EBML VINT");

        var count = 0;
        for (int i = 7; i >= 0; i--) {
            if ((length[0] & (1 << i)) != 0)
                break;
            count += 1;
        }

        if (count > 7)
            throw new Exception("EBML VINT too large");

        int value = length[0] ^ (1 << (7 - count));
        if (count > 0) {
            Span<byte> extraData = stackalloc byte[count];
            
            read = stream.Read(extraData);
            if (read != count)
                throw new EndOfStreamException("Unexpected end of stream");

            foreach (byte b in extraData)
                value = (value << 8) | b;
        }

        return value;
    }

    public static unsafe int ReadVINT(Span<byte> bytes, out int value, int offset = 0) {
        var length = bytes[offset];

        if (length == 0)
            throw new Exception("Invalid EBML VINT");

        int count = 0;
        for (int i = 7; i >= 0; i--) {
            if ((length & (1 << i)) != 0)
                break;
            count += 1;
        }

        if (count > 7)
            throw new Exception("EBML VINT too large");

        value = length ^ (1 << (7 - count));
        for (int i = 0; i < count; i++)
            value = (value << 8) | bytes[offset + i + 1];

        return count + 1;
    }

    // should i have just made an EBML parser?
    // yes, probably
    // do i care?
    // well i care about the sanity i lost making this its been 5 hours please help
    public unsafe MatroskaContainer ReadMatroska(Stream stream) {
        Span<byte> magic = stackalloc byte[4];
        int read = stream.Read(magic);

        // HEADER segment magic bytes
        if (read < 4 || magic[0] != 0x1A || magic[1] != 0x45 || magic[2] != 0xDF || magic[3] != 0xA3)
            throw new Exception("Invalid Matroska file");

        int len = ReadVINT(stream);
        Span<byte> header = stackalloc byte[len];
        read = stream.Read(header);

        if (read != len)
            throw new EndOfStreamException("Unexpected end of stream");

        ContainerType? container = null;
        ulong timescale = 0;
        List<MatroskaTrack> tracks = [];

        int bytes = 0;
        while (bytes < len) {
            bytes += ReadVINT(header, out int type, bytes); // yeah yeah not supposed to remove the marker bit whatever bite me
            bytes += ReadVINT(header, out int size, bytes);
            if (type == 642) // DocType
                container = header[bytes..(bytes + size)].Decode() switch {
                    "matroska" => ContainerType.Matroska, // .mkv
                    "webm" => ContainerType.WebM,         // .webm
                    _ => throw new Exception("Invalid container type")
                };
            else
                bytes += size;
        }
        if (container == null)
            throw new Exception("Invalid Matroska file; missing DocType EBML");

        int id = ReadVINT(stream); // 0x08538067
        long llen = stream.Position + ReadVINT(stream); // bytes in segment (generally goes to EOF)

        while (stream.Position < llen) {
            id = ReadVINT(stream);
            Log.Info(id, "0x" + id.ToString("X8"));

            switch (id) {
                case 88713574: // INFO segment
                    int seglen = ReadVINT(stream);
                    long pos = stream.Position;
                    while (stream.Position < pos + seglen) {
                        int infoId = ReadVINT(stream);
                        int size = ReadVINT(stream);
                        if (read != size)
                            throw new EndOfStreamException("Unexpected end of stream");
                        if (infoId == 710577) {
                            Span<byte> data = stackalloc byte[size];
                            read = stream.Read(data);
                            foreach (byte b in data)
                                timescale = (timescale << 8) | b;
                            break;
                        } else {
                            stream.Seek(size, SeekOrigin.Current);
                        }
                    }
                    stream.Seek(pos + seglen, SeekOrigin.Begin);
                    break;
                case 106212971: // TRACKS segment
                    seglen = ReadVINT(stream);
                    pos = stream.Position;
                    var trackId = ReadVINT(stream);
                    while (trackId == 0x2E) {
                        int trackSize = ReadVINT(stream);
                        var dataId = ReadVINT(stream);
                        MatroskaTrack track = new();
                        while (dataId != 0x2E) {
                            switch (dataId) {
                                case 0xD7:
                                    ReadVINT(stream);
                                    track.TrackNumber = stream.ReadByte();
                                    break;
                                case 0x73C9:
                                    ReadVINT(stream);
                                    track.TrackUID = stream.ReadULong();
                                    Log.Info(track.TrackUID.ToString("X16"), "0xC5 88 88 16 D4 C4 29 86 1D B3 9C");
                                    break;
                                case 0x9C:
                                    ReadVINT(stream);
                                    stream.ReadByte();
                                    break;
                            }
                            dataId = stream.ReadByte();
                        }
                        trackId = dataId;
                    }
                    stream.Seek(pos + seglen, SeekOrigin.Begin);
                    break;
                default:
                    seglen = ReadVINT(stream);
                    stream.Seek(seglen, SeekOrigin.Current);
                    break;
            }
        }
        Log.Info(timescale);

        return default;
    }

    public ISoundDecoder CreateDecoder(Stream stream, string formatId, AudioFormat format) {
        Log.Info(stream.Position);
        ReadMatroska(stream);
        return new DummySoundDecoder(format);// new OggOpusDecoder(stream, format);
    }

    public ISoundDecoder TryCreateDecoder(
        Stream stream,
        out AudioFormat detectedFormat,
        AudioFormat? hintFormat = null
    ) {
        ReadMatroska(stream);
        //var decoder = new OggOpusDecoder(stream, new AudioFormat() { Channels = 2 });

        //detectedFormat = new AudioFormat() {
        //    Channels = decoder.Channels,
        //    SampleRate = decoder.SampleRate,
        //    Format = SampleFormat.F32
        //};
        detectedFormat = new();

        return new DummySoundDecoder(hintFormat ?? new());// decoder;
    }

    public ISoundEncoder CreateEncoder(Stream stream, string formatId, AudioFormat format)
        => null;
}
