using System.Collections.Generic;
using System.IO;
using Hyleus.Soundboard.Audio.Decoders;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Codecs;
public sealed class OggOpusCodecFactory : ICodecFactory {
    public string FactoryId => "Hyleus.Concentus.OggOpus";

    public IReadOnlyCollection<string> SupportedFormatIds { get; } =
        ["ogg", "opus"];

    public int Priority => 100;

    public ISoundDecoder CreateDecoder(Stream stream, string formatId, AudioFormat format)
        => new OggOpusDecoder(stream, format);

    public ISoundDecoder TryCreateDecoder(
        Stream stream,
        out AudioFormat detectedFormat,
        AudioFormat? hintFormat = null
    ) {
        var decoder = new OggOpusDecoder(stream, hintFormat ?? new AudioFormat() { Channels = 2, SampleRate = 44100 });

        detectedFormat = new AudioFormat() {
            Channels = decoder.Channels,
            SampleRate = decoder.SampleRate,
            Format = SampleFormat.F32
        };

        return decoder;
    }

    public ISoundEncoder CreateEncoder(Stream stream, string formatId, AudioFormat format)
        => null;
}
