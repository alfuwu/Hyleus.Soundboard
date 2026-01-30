using System;
using System.Collections.Generic;
using NVorbis.Contracts;

namespace Hyleus.Soundboard.Audio.Decoders.Matroska;
internal sealed class VorbisPacketProvider(IList<byte[]> packets) : IPacketProvider {
    private readonly IList<byte[]> _packets = packets;
    private int _index;

    public bool CanSeek => false;
    public long ContainerBits {
        get {
            long bits = 0;
            for (int i = 0; i < _packets.Count; i++)
                bits += (long)_packets[i].Length * 8;
            return bits;
        }
    }
    public static long ContainerOverheadBits => 0;
    public long NextPacketBits => (PeekNextPacket() as RawVorbisPacket).Length * 8;

    public int StreamSerial => 0;
    public long GetGranuleCount() => throw new NotSupportedException();

    public IPacket GetNextPacket() {
        if (_index >= _packets.Count)
            return null;

        var data = _packets[_index++];
        return new RawVorbisPacket(data);
    }

    public IPacket PeekNextPacket() {
        if (_index >= _packets.Count)
            return null;

        var data = _packets[_index];
        return new RawVorbisPacket(data);
    }
    public void SeekTo(long granulePos) =>
        throw new NotSupportedException();
    public long SeekTo(long granulePos, int preRoll, GetPacketGranuleCount getPacketGranuleCount) =>
        throw new NotSupportedException();
}