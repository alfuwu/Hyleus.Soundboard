using System;
using System.IO;
using Hyleus.Soundboard.Framework.Extensions;

namespace Hyleus.Soundboard.Framework.Structs;
public record struct Category() {
    private const byte FILE_VERSION = 1;

    public string Name { get; set; } = string.Empty;
    public string IconLocation { get; set; } = null;
    public Guid UUID { get; set; } = Guid.NewGuid();

    public static Category FromBinary(BinaryReader reader) {
        var cat = new Category();
        var catVer = reader.ReadByte();
        cat.Name = reader.ReadString();
        cat.IconLocation = reader.ReadStringOrNull();
        cat.UUID = reader.ReadGuid();
        return cat;
    }

    public void WriteBinary(BinaryWriter writer) {
        writer.Write(FILE_VERSION);
        writer.Write(Name ?? string.Empty);
        writer.WriteStringOrNull(IconLocation);
        writer.Write(UUID);
    }
}
