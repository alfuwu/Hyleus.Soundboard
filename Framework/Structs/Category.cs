using System;
using System.IO;
using Hyleus.Soundboard.Framework.Extensions;

namespace Hyleus.Soundboard.Framework.Structs;
public class Category() {
    public const byte FILE_VERSION = 1;

    public string Name { get; set; } = string.Empty;
    public string IconLocation { get; set; } = null;
    public Guid UUID { get; set; } = Guid.NewGuid();

    public static Category FromBinary(BinaryReader reader, byte schemaVer) => new() {
        Name = reader.ReadString(),
        IconLocation = reader.ReadStringOrNull(),
        UUID = reader.ReadGuid()
    };

    public readonly void WriteBinary(BinaryWriter writer) {
        writer.Write(Name ?? string.Empty);
        writer.WriteStringOrNull(IconLocation);
        writer.Write(UUID);
    }
}
