using MonoGame.OpenGL;

namespace Hyleus.Soundboard.Framework;
public class WindowInfo(nint handle) : IWindowInfo {
    public nint Handle { get; private set; } = handle;
}