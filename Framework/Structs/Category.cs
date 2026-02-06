using System;
using System.IO;
using Hyleus.Soundboard.Framework.Extensions;

namespace Hyleus.Soundboard.Framework.Structs;
public class Category() {
    public const byte FILE_VERSION = 1;

    public string Name { get; set; } = string.Empty;
    public string IconLocation { get; set; } = null;
    public Guid? UUID { get; set; } = Guid.NewGuid();

    public static Category FromBinary(BinaryReader reader, byte schemaVer) => new() {
        Name = reader.ReadString(reader.ReadByte()),
        IconLocation = reader.ReadStringOrNull(),
        UUID = reader.ReadGuid()
    };

    public void WriteBinary(BinaryWriter writer) {
        if (UUID == null)
            throw new InvalidDataException("Category UUID is null");
        string n = string.IsNullOrEmpty(Name) ? "Unknown" : Name;
        byte l = (byte)int.Min(n.Length, byte.MaxValue);
        writer.Write(l);
        writer.Write(n, l);
        writer.WriteStringOrNull(IconLocation);
        writer.Write(UUID.Value);
    }
}
