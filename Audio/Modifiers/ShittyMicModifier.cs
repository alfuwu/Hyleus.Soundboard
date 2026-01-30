using System;
using SoundFlow.Abstracts;

namespace Hyleus.Soundboard.Audio.VoiceChangers;
public sealed class ShittyMicModifier : SoundModifier {
    public override string Name { get; set; } = "Shitty Mic Modifier";

    // How hard we drive the signal into clipping
    public float PreGain { get; set; } = 1000.0f;

    // Hard clip threshold (lower = harsher)
    public float ClipThreshold { get; set; } = 0.35f;

    // Bit depth for bit crushing (e.g. 6–8 sounds very bad in a good way)
    public int BitDepth { get; set; } = 6;

    // Noise gate
    public float GateThreshold { get; set; } = 0.017f; // raise if room noise still leaks
    public float GateSoftness { get; set; } = 0.01f;  // transition width

    // Final output level
    public float PostGain { get; set; } = 0.2f;

    public override float ProcessSample(float sample, int channel) {
        float abs = MathF.Abs(sample);

        if (abs < GateThreshold - GateSoftness)
            return 0f;

        if (abs < GateThreshold + GateSoftness) {
            float t = (abs - (GateThreshold - GateSoftness)) / (GateSoftness * 2f);
            sample *= Math.Clamp(t, 0f, 1f);
        }

        // drive signal
        float x = sample * PreGain;

        // hard clipping
        x = MathF.Max(-ClipThreshold, MathF.Min(ClipThreshold, x));
        x /= ClipThreshold; // normalize back to -1..1

        // bit crushing
        if (BitDepth > 0) {
            int levels = 1 << BitDepth;
            x = MathF.Round(x * levels) / levels;
        }

        // output level
        x *= PostGain;

        return x;
    }
}
