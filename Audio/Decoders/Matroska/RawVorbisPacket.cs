using System;
using NVorbis;

namespace Hyleus.Soundboard.Audio.Decoders.Matroska;
internal sealed class RawVorbisPacket : DataPacket {
    private readonly byte[] _data;
    private int _offset;

    public RawVorbisPacket(byte[] data, bool isEndOfStream = false) {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _offset = 0;
        IsEndOfStream = isEndOfStream;
    }

    protected override int TotalBits => _data.Length * 8;
    public int Length => _data.Length;

    protected override int ReadNextByte() {
        if (_offset >= _data.Length)
            return -1;

        return _data[_offset++];
    }

    public override void Reset() {
        base.Reset();
        _offset = 0;
    }
}