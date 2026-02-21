using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace Hyleus.Soundboard.Framework.Extensions;
public static class BinaryExtensions {
    #region Writing
    private static void WriteGenericValue<T>(this BinaryWriter writer, T value) {
        switch (value) {
            case bool b: writer.Write(b); break;
            case byte b: writer.Write(b); break;
            case ushort u: writer.Write(u); break;
            case uint u: writer.Write(u); break;
            case ulong u: writer.Write(u); break;
            case sbyte s: writer.Write(s); break;
            case short s: writer.Write(s); break;
            case int i: writer.Write(i); break;
            case long l: writer.Write(l); break;
            case float f: writer.Write(f); break;
            case double d: writer.Write(d); break;
            case char c: writer.Write(c); break;
            case string s: writer.Write(s); break;
            case Vector2 v: writer.Write(v); break;
            case Color c: writer.Write(c); break;
            case Guid g: writer.Write(g); break;
            case ICollection<dynamic> v: writer.Write(v); break;
            default:
                throw new InvalidOperationException($"No serializer for type {typeof(T)}");
        }
    }
    public static void Write<T>(this BinaryWriter writer, ICollection<T> list, Action<BinaryWriter, T> serializer = null) {
        writer.Write(list.Count);
        if (serializer != null)
            foreach (T item in list)
                serializer(writer, item);
        else
            foreach (T item in list)
                writer.WriteGenericValue(item);
    }
    public static void Write<T>(this BinaryWriter writer, T[] arr, Action<BinaryWriter, T> serializer = null) {
        writer.Write(arr.Length);
        foreach (T item in arr)
            if (serializer != null)
                serializer(writer, item);
            else
                writer.WriteGenericValue(item);
    }
    public static void Write(this BinaryWriter writer, Vector2 vec) {
        writer.Write(vec.X);
        writer.Write(vec.Y);
    }
    public static void Write(this BinaryWriter writer, Color color, bool writeAlpha = true) {
        writer.Write(color.R);
        writer.Write(color.G);
        writer.Write(color.B);
        if (writeAlpha)
            writer.Write(color.A);
    }
    public static void Write(this BinaryWriter writer, string str, int length) {
        if (str.Length > length)
            str = str[..length];
        for (int i = 0; i < length; i++)
            writer.Write(str[i]);
    }
    public static void Write(this BinaryWriter writer, Guid uuid) {
        writer.Write(uuid.ToByteArray());
    }
    public static void WriteStringOrNull(this BinaryWriter writer, string str) {
        if (str != null)
            ArgumentOutOfRangeException.ThrowIfGreaterThan(str.Length, ushort.MaxValue);
        writer.Write((ushort)(str == null ? 0 : str.Length));
        if (str != null)
            writer.Write(str, str.Length);
    }
    #endregion

    #region Reading
    private static T ReadGenericValue<T>(this BinaryReader reader, Type type) {
        object value =
            type == typeof(bool) ? reader.ReadBoolean() :
            type == typeof(byte) ? reader.ReadByte() :
            type == typeof(ushort) ? reader.ReadUInt16() :
            type == typeof(uint) ? reader.ReadUInt32() :
            type == typeof(ulong) ? reader.ReadUInt64() :
            type == typeof(sbyte) ? reader.ReadSByte() :
            type == typeof(short) ? reader.ReadInt16() :
            type == typeof(int) ? reader.ReadInt32() :
            type == typeof(long) ? reader.ReadInt64() :
            type == typeof(float) ? reader.ReadSingle() :
            type == typeof(double) ? reader.ReadDouble() :
            type == typeof(char) ? reader.ReadChar() :
            type == typeof(string) ? reader.ReadString() :
            type == typeof(Vector2) ? reader.ReadVector2() :
            type == typeof(Color) ? reader.ReadColor() :
            type == typeof(Guid) ? reader.ReadGuid() :
            type == typeof(List<dynamic>) ? reader.ReadCollection<List<dynamic>, dynamic>() :
            throw new InvalidOperationException($"No serializer for type {typeof(T)}");
        return (T)value;
    }
    public static G ReadCollection<G, T>(this BinaryReader reader, Func<BinaryReader, T> deserializer = null) where G : ICollection<T>, new() {
        G items = [];
        int count = reader.ReadInt32();
        if (deserializer != null)
            for (int i = 0; i < count; i++)
                items.Add(deserializer(reader));
        else
            for (int i = 0; i < count; i++)
                items.Add(reader.ReadGenericValue<T>(typeof(T)));
        return items;
    }
    public static T[] ReadArray<T>(this BinaryReader reader, Func<BinaryReader, T> deserializer = null) {
        int count = reader.ReadInt32();
        T[] arr = new T[count];
        if (deserializer != null)
            for (int i = 0; i < count; i++)
                arr[i] = deserializer(reader);
        else
            for (int i = 0; i < count; i++)
                arr[i] = reader.ReadGenericValue<T>(typeof(T));
        return arr;
    }
    public static string ReadString(this BinaryReader reader, int length) {
        char[] chars = new char[length];
        reader.Read(chars, 0, length);
        return new string(chars);
    }
    public static string ReadStringOrNull(this BinaryReader reader) {
        ushort n = reader.ReadUInt16();
        return n == 0 ? null : reader.ReadString(n);
    }
    public static Vector2 ReadVector2(this BinaryReader reader) {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        return new Vector2(x, y);
    }
    public static Color ReadColor(this BinaryReader reader, bool hasAlpha = true) {
        byte r = reader.ReadByte();
        byte g = reader.ReadByte();
        byte b = reader.ReadByte();
        byte a = hasAlpha ? reader.ReadByte() : (byte)255;
        return new Color(r, g, b, a);
    }
    public static Guid ReadGuid(this BinaryReader reader) {
        byte[] bytes = reader.ReadBytes(16);
        return new Guid(bytes);
    }
    #endregion
}
