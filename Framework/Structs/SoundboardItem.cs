using System;
using System.IO;
using Hyleus.Soundboard.Framework.Extensions;
using Microsoft.Xna.Framework.Input;

namespace Hyleus.Soundboard.Framework.Structs;
public class SoundboardItem() {
    public const byte FILE_VERSION = 0;
    private byte _flags = 0b00000000;
    private float _volume = 1.0f;
    private float _speed = 1.0f;
    
    public string Name { get; set; } = string.Empty;
    public string IconLocation { get; set; } = null;
    public string SoundLocation { get; set; } = null;
    public float Volume { get => _volume; set { OnVolumeChanged?.Invoke(_volume, value); _volume = value; } }
    public float Speed { get => _speed; set { OnSpeedChanged?.Invoke(_speed, value); _speed = value; } }
    public Keys? Keybind { get; set; } = null;
    public bool RequiresControl { get => (_flags & 1) != 0; set { if (value) _flags |= 1; else unchecked { _flags &= (byte)~1; } } }
    public bool RequiresShift { get => (_flags & 2) != 0; set { if (value) _flags |= 2; else unchecked { _flags &= (byte)~2; } } }
    public bool RequiresAlt { get => (_flags & 4) != 0; set { if (value) _flags |= 4; else unchecked { _flags &= (byte)~4; } } }
    public Guid UUID { get; set; } = Guid.NewGuid();
    public Guid? CategoryID { get; set; } = null;

    public delegate void FloatChangedEvent(float oldValue, float newValue);
    public event FloatChangedEvent OnVolumeChanged;
    public event FloatChangedEvent OnSpeedChanged;

    public static SoundboardItem FromBinary(BinaryReader reader, byte schemaVer) {
        var item = new SoundboardItem {
            Name = reader.ReadString(),
            IconLocation = reader.ReadStringOrNull(),
            SoundLocation = reader.ReadString(),
            Volume = float.Max(reader.ReadSingle(), 0),
            Speed = float.Max(reader.ReadSingle(), float.Epsilon),
            Keybind = reader.ReadByte() == 0 ? null : (Keys)reader.ReadUInt16(),
            UUID = reader.ReadGuid(),
            _flags = reader.ReadByte()
        };
        return item;
    }

    public void WriteBinary(BinaryWriter writer) {
        if (SoundLocation == null) {
            Log.Error("SoundLocation must be set (missing for SoundboardItem " + ToString() + ")");
            return;
        }
        if (!File.Exists(SoundLocation)) {
            Log.Error("No file found at SoundLocation '" + SoundLocation + "' (missing for SoundboardItem " + ToString() + ")");
            return;
        }
        writer.Write(Name ?? string.Empty);
        writer.WriteStringOrNull(IconLocation);
        writer.Write(SoundLocation);
        writer.Write(Volume);
        writer.Write(Speed);
        writer.Write((byte)(Keybind == null ? 0 : 1));
        if (Keybind != null)
            writer.Write((ushort)Keybind);
        writer.Write(UUID);
        writer.Write(_flags);
    }
}
