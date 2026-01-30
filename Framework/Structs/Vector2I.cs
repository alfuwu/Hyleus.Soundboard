using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.Xna.Framework;

namespace Hyleus.Soundboard.Framework.Structs;
[DataContract]
public struct Vector2I : IEquatable<Vector2I> {

    private static readonly Vector2I zeroVector = new(0, 0);
    private static readonly Vector2I unitVector = new(1, 1);
    private static readonly Vector2I unitXVector = new(1, 0);
    private static readonly Vector2I unitYVector = new(0, 1);

    public static Vector2I Zero => zeroVector;
    public static Vector2I One => unitVector;
    public static Vector2I UnitX => unitXVector;
    public static Vector2I UnitY => unitYVector;

    [DataMember] public int X;
    [DataMember] public int Y;

    public Vector2I(int val) {
        X = val;
        Y = val;
    }

    public Vector2I(int x, int y) {
        X = x;
        Y = y;
    }

    public Vector2I(Vector2 vec) {
        X = (int)vec.X;
        Y = (int)vec.Y;
    }

    public readonly override string ToString() => $"{{X:{X}, Y:{Y}}}";

    #region Arithmetic
    public static Vector2I operator -(Vector2I value) {
        value.X = -value.X;
        value.Y = -value.Y;
        return value;
    }
    public static Vector2I operator +(Vector2I i1, Vector2I i2) {
        i1.X += i2.X;
        i1.Y += i2.Y;
        return i1;
    }
    public static Vector2I operator -(Vector2I i1, Vector2I i2) {
        i1.X -= i2.X;
        i1.Y -= i2.Y;
        return i1;
    }
    public static Vector2I operator *(Vector2I i1, Vector2I i2) {
        i1.X *= i2.X;
        i1.Y *= i2.Y;
        return i1;
    }
    public static Vector2I operator *(Vector2I i, int scaleFactor) {
        i.X *= scaleFactor;
        i.Y *= scaleFactor;
        return i;
    }
    public static Vector2I operator *(int scaleFactor, Vector2I i) {
        i.X *= scaleFactor;
        i.Y *= scaleFactor;
        return i;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2I operator /(Vector2I i1, Vector2I i2) {
        i1.X /= i2.X;
        i1.Y /= i2.Y;
        return i1;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2I operator /(Vector2I i, int divider) {
        float num = 1f / divider;
        i.X = (int)(i.X * num);
        i.Y *= (int)(i.Y * num);
        return i;
    }
    public static Vector2I operator *(Vector2I i, float scaleFactor) {
        i.X = (int)(i.X * scaleFactor);
        i.Y = (int)(i.Y * scaleFactor);
        return i;
    }
    public static Vector2I operator *(float scaleFactor, Vector2I i) {
        i.X = (int)(i.X * scaleFactor);
        i.Y = (int)(i.X * scaleFactor);
        return i;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2I operator /(Vector2I i, float divider) {
        float num = 1 / divider;
        i.X = (int)(i.X * num);
        i.Y *= (int)(i.Y * num);
        return i;
    }
    public static Rectangle operator +(Rectangle rect, Vector2I pos) => new(rect.X + pos.X, rect.Y + pos.Y, rect.Width, rect.Height);
    #endregion

    #region Casting
    public static implicit operator Vector2(Vector2I i) => new(i.X, i.Y);
    public static implicit operator Vector2I(Vector2 vec) => new(vec);
    public static implicit operator Point(Vector2I i) => new(i.X, i.Y);
    public static implicit operator Vector2I(Point p) => new(p.X, p.Y);
    #endregion

    #region Equality
    public readonly bool Equals(Vector2I other) => X == other.X && Y == other.Y;
    public override readonly bool Equals(object obj) => obj is Vector2I i && Equals(i);
    public static bool operator ==(Vector2I left, Vector2I right) => left.Equals(right);
    public static bool operator !=(Vector2I left, Vector2I right) => !(left == right);
    public override readonly int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();
    #endregion
}
