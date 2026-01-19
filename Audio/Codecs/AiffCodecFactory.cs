using System;
using System.Collections.Generic;
using System.IO;
using Hyleus.Soundboard.Audio.Decoders;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Codecs;
public sealed class AiffCodecFactory : ICodecFactory {
    public string FactoryId => "Hyleus.Soundboard.AIFF";

    public IReadOnlyCollection<string> SupportedFormatIds { get; } =
        ["aiff"];

    public int Priority => 100;

    public ISoundDecoder CreateDecoder(Stream stream, string formatId, AudioFormat format) =>
        new AiffDecoder(stream, format);

    public ISoundDecoder TryCreateDecoder(
        Stream stream,
        out AudioFormat detectedFormat,
        AudioFormat? hintFormat = null
    ) {
        try {
            var decoder = new AiffDecoder(stream, hintFormat ?? new AudioFormat() { Channels = 2, SampleRate = 44100 });

            detectedFormat = new AudioFormat() {
                Channels = decoder.Channels,
                SampleRate = decoder.SampleRate,
                Format = SampleFormat.F32
            };

            return decoder;
        } catch (Exception) {
            detectedFormat = new AudioFormat();
            return null;
        }
    }

    public ISoundEncoder CreateEncoder(Stream stream, string formatId, AudioFormat format)
        => null;
}
