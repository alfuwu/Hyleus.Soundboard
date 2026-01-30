using System;
using System.Collections.Generic;
using System.IO;
using Hyleus.Soundboard.Audio.Decoders;
using NVorbis;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Codecs;
public sealed class OggVorbisCodecFactory : ICodecFactory {
    public string FactoryId => "Hyleus.NVorbis.OggVorbis";

    public IReadOnlyCollection<string> SupportedFormatIds { get; } =
        ["ogg", "vorbis"];

    public int Priority => 100;

    public ISoundDecoder CreateDecoder(Stream stream, string formatId, AudioFormat format) {
        try {
            return new OggVorbisDecoder(stream, format);
        } catch (ArgumentException e) {
            if (e.Message.Contains("Found OPUS bitstream"))
                return null;
            else
                throw;
        }
    }

    public ISoundDecoder TryCreateDecoder(
        Stream stream,
        out AudioFormat detectedFormat,
        AudioFormat? hintFormat = null
    ) {
        try {
            using var reader = new VorbisReader(stream, false);

            detectedFormat = new AudioFormat() {
                Channels = reader.Channels,
                SampleRate = reader.SampleRate,
                Format = SampleFormat.F32
            };

            stream.Position = 0;
            return new OggVorbisDecoder(stream, detectedFormat);
        } catch (Exception) {
            stream.Seek(0, SeekOrigin.Begin);
            detectedFormat = new AudioFormat();
            return null;
        }
    }

    public ISoundEncoder CreateEncoder(Stream stream, string formatId, AudioFormat format)
        => null; // NVorbis is decode-only
}
