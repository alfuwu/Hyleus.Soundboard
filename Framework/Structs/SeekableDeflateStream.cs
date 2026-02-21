using System;
using System.IO;
using System.IO.Compression;

namespace Hyleus.Soundboard.Framework.Structs;
public sealed class SeekableDeflateStream : Stream {
    private readonly MemoryStream _buffer;

    public SeekableDeflateStream(Stream compressedStream) {
        ArgumentNullException.ThrowIfNull(compressedStream);

        if (!compressedStream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(compressedStream));

        _buffer = new MemoryStream();

        using (var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress, leaveOpen: true))
            deflate.CopyTo(_buffer);

        _buffer.Position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _buffer.Length;

    public override long Position {
        get => _buffer.Position;
        set => _buffer.Position = value;
    }

    public override void Flush() {
        // no-op (read-only stream)
    }

    public override int Read(byte[] buffer, int offset, int count) {
        return _buffer.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin) {
        return _buffer.Seek(offset, origin);
    }

    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing) {
        if (disposing)
            _buffer?.Dispose();
        base.Dispose(disposing);
    }
}