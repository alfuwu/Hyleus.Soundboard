using System;
using System.Collections.Generic;
using System.IO;
using Hyleus.Soundboard.Audio.Decoders;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Codecs;
public sealed class AacCodecFactory : ICodecFactory {
    public string FactoryId => "Hyleus.SharpJaad.Aac";

    public IReadOnlyCollection<string> SupportedFormatIds { get; } =
        ["aac", "m4a"];

    public int Priority => 100;

    public ISoundDecoder CreateDecoder(Stream stream, string formatId, AudioFormat format) =>
        null;// new AacDecoder(stream);

    public ISoundDecoder TryCreateDecoder(
        Stream stream,
        out AudioFormat detectedFormat,
        AudioFormat? hintFormat = null
    ) {
        try {
            /*var reader = new AacDecoder(stream);

            detectedFormat = new AudioFormat() {
                Channels = reader.Channels,
                SampleRate = reader.SampleRate,
                Format = SampleFormat.F32
            };

            return reader;*/
            detectedFormat = new();
            return null;
        } catch (Exception) {
            detectedFormat = new AudioFormat();
            return null;
        }
    }

    public ISoundEncoder CreateEncoder(Stream stream, string formatId, AudioFormat format)
        => null; // NVorbis is decode-only
}
