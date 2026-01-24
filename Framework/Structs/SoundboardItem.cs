using System;
using System.IO;
using Hyleus.Soundboard.Framework.Extensions;
using Microsoft.Xna.Framework.Input;

namespace Hyleus.Soundboard.Framework.Structs;
public class SoundboardItem() {
    private byte _flags = 0b00000000;
    
    public string Name { get; set; } = string.Empty;
    public string IconLocation { get; set; } = null;
    public string SoundLocation { get; set; } = null;
    private float _volume = 1.0f;
    public float Volume { get => _volume; set { OnVolumeChanged?.Invoke(_volume, value); _volume = value; } }
    public Keys? Keybind { get; set; } = null;
    public bool RequiresControl { get => (_flags & 1) != 0; set { if (value) _flags |= 1; else unchecked { _flags &= (byte)~1; } } }
    public bool RequiresShift { get => (_flags & 2) != 0; set { if (value) _flags |= 2; else unchecked { _flags &= (byte)~2; } } }
    public bool RequiresAlt { get => (_flags & 4) != 0; set { if (value) _flags |= 4; else unchecked { _flags &= (byte)~4; } } }
    public Guid UUID { get; set; } = Guid.NewGuid();

    public delegate void VolumeChangedEvent(float oldVolume, float newVolume);
    public event VolumeChangedEvent OnVolumeChanged;

    public static SoundboardItem FromBinary(BinaryReader reader) => new() {
        Name = reader.ReadString(),
        IconLocation = reader.ReadStringOrNull(),
        SoundLocation = reader.ReadString(),
        Volume = float.Max(reader.ReadSingle(), 0),
        Keybind = reader.ReadByte() == 0 ? null : (Keys)reader.ReadUInt16(),
        UUID = reader.ReadGuid(),
        _flags = reader.ReadByte()
    };

    public void WriteBinary(BinaryWriter writer) {
        if (SoundLocation == null)
            throw new InvalidDataException("SoundLocation must be set");
        if (!File.Exists(SoundLocation))
            throw new FileNotFoundException("Sound does not exist");
        writer.Write(Name ?? string.Empty);
        writer.WriteStringOrNull(IconLocation);
        writer.Write(SoundLocation);
        writer.Write(Volume);
        writer.Write((byte)(Keybind == null ? 0 : 1));
        if (Keybind != null)
            writer.Write((ushort)Keybind);
        writer.Write(UUID);
        writer.Write(_flags);
    }
}
