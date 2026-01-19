using System;

namespace Hyleus.Soundboard.Framework.Interfaces;
internal interface IAacFrameSource : IDisposable {
    bool TryGetNextFrame(out ReadOnlyMemory<byte> frame);
    void SeekToFrame(long frameIndex);
}
