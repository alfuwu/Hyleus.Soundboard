using Microsoft.Xna.Framework;

namespace Hyleus.Soundboard.Framework.Extensions;
public static class RectangleExtensions {
    public static Rectangle Exflate(this Rectangle rect, int x, int y) => new(rect.X - x, rect.Y - y, rect.Width + x * 2, rect.Height + y * 2);
    public static Rectangle WithPosition(this Rectangle rect, Point pos) => new (pos.X, pos.Y, rect.Width, rect.Height);
}