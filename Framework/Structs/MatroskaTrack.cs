namespace Hyleus.Soundboard.Framework.Structs;
public struct MatroskaTrack {
    public int TrackNumber { get; set; }
    public ulong TrackUID { get; set; }
    public byte TrackType { get; set; } // 0x01 = vide, 0x02 = audio, ...
    public string Name { get; set; }
    public string CodecID { get; set; }
    public byte[] CodecPrivate { get; set; }
    public byte TrackFlags { get; set; }
}
