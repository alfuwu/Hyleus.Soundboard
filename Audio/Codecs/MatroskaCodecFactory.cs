using System.Collections.Generic;
using System.IO;
using Hyleus.Soundboard.Audio.Decoders;
using Hyleus.Soundboard.Framework;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio.Codecs;
public sealed class MatroskaCodecFactory : ICodecFactory {
    public string FactoryId => "Hyleus.Matroska.OggOpus";

    public IReadOnlyCollection<string> SupportedFormatIds { get; } =
        ["mkv", "webm"];

    public int Priority => 100;

    public ISoundDecoder CreateDecoder(Stream stream, string formatId, AudioFormat format) {
        Log.Info(stream.Position);
        var opusStream = new MemoryStream();
        //MatroskaDemuxer.ExtractOggOpusAudio(stream, opusStream);
        opusStream.Position = 0;
        return new OggOpusDecoder(opusStream, format);
    }

    public ISoundDecoder TryCreateDecoder(
        Stream stream,
        out AudioFormat detectedFormat,
        AudioFormat? hintFormat = null
    ) {
        Log.Info(stream.Position);
        var opusStream = new MemoryStream();
        //MatroskaDemuxer.ExtractOggOpusAudio(stream, opusStream);
        opusStream.Position = 0;
        var decoder = new OggOpusDecoder(opusStream, new AudioFormat() { Channels = 2 });

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
