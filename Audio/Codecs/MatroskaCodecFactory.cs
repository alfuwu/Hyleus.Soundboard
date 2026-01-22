using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hyleus.Soundboard.Audio.Decoders;
using Matroska;
using Matroska.Models;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Codecs;
public sealed class MatroskaCodecFactory : ICodecFactory {
    public string FactoryId => "Hyleus.Matroska.Matroska";

    public IReadOnlyCollection<string> SupportedFormatIds { get; } =
        ["mkv", "webm"];

    public int Priority => 100;

    public ISoundDecoder CreateDecoder(Stream stream, string formatId, AudioFormat format) =>
        DecoderFromDoc(MatroskaSerializer.Deserialize(stream), format, out _);

    public ISoundDecoder TryCreateDecoder(
        Stream stream,
        out AudioFormat detectedFormat,
        AudioFormat? hintFormat = null
    ) {
        try {
            return DecoderFromDoc(MatroskaSerializer.Deserialize(stream), hintFormat, out detectedFormat);
        } catch (Exception) {
            stream.Seek(0, SeekOrigin.Begin);
            detectedFormat = new AudioFormat();
            return null;
        }
    }

    private static List<byte[]> ParseVorbisCodecPrivate(byte[] codecPrivate) {
        int offset = 0;

        int packetCount = codecPrivate[offset++] + 1;
        if (packetCount != 3)
            throw new InvalidDataException("Expected 3 Vorbis header packets");

        int[] sizes = new int[packetCount - 1];

        for (int i = 0; i < sizes.Length; i++) {
            int size = 0;
            byte b;
            do {
                b = codecPrivate[offset++];
                size += b;
            }
            while (b == 255);

            sizes[i] = size;
        }

        var packets = new List<byte[]>(3);

        for (int i = 0; i < sizes.Length; i++) {
            var packet = new byte[sizes[i]];
            Buffer.BlockCopy(codecPrivate, offset, packet, 0, sizes[i]);
            offset += sizes[i];
            packets.Add(packet);
        }

        // last packet takes the remaining bytes
        var lastPacket = new byte[codecPrivate.Length - offset];
        Buffer.BlockCopy(codecPrivate, offset, lastPacket, 0, lastPacket.Length);
        packets.Add(lastPacket);

        return packets;
    }

    private static MatroskaDecoder DecoderFromDoc(MatroskaDocument doc, AudioFormat? targetFormat, out AudioFormat detectedFormat) {
        TrackEntry track = doc.Segment.Tracks.TrackEntries
            .FirstOrDefault(t => t.Audio != null) ?? throw new Exception("No audio track found");

        var packets = track.CodecID switch {
            "A_VORBIS" => ParseVorbisCodecPrivate(track.CodecPrivate),
            "A_AAC" => [track.CodecPrivate!],
            _ => []
        };

        foreach (var cluster in doc.Segment.Clusters) {
            if (cluster.BlockGroups != null)
                foreach (var blockGroup in cluster.BlockGroups)
                    foreach (var block in blockGroup.Blocks)
                        if (block.TrackNumber == track.TrackNumber)
                            packets.Add(block.Data);

            if (cluster.SimpleBlocks != null)
                foreach (var simpleBlock in cluster.SimpleBlocks)
                    if (simpleBlock.TrackNumber == track.TrackNumber)
                        packets.Add(simpleBlock.Data);
        }

        // set up the opus decoder
        var channels = (int)track.Audio.Channels;
        var sampleRate = (int)track.Audio.SamplingFrequency;

        detectedFormat = new AudioFormat() {
            Channels = channels,
            SampleRate = sampleRate,
            Format = SampleFormat.F32
        };

        return new MatroskaDecoder(packets, track.CodecID, detectedFormat, targetFormat);
    }

    public ISoundEncoder CreateEncoder(Stream stream, string formatId, AudioFormat format)
        => null;
}
