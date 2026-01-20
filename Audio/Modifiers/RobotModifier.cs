using System;
using SoundFlow.Abstracts;

namespace Hyleus.Soundboard.Audio.VoiceChangers;
public sealed class RobotModifier(float pitch = 1.08f, float drive = 6.0f, float gain = 1.2f, int bitDepth = 8) : SoundModifier {
    public override string Name { get; set; } = "Robot Modifier";

    public float Pitch = pitch;     // small pitch up
    public float Drive = drive;     // distortion strength
    public float Gain = gain;
    public int BitDepth = bitDepth; // 6–8 works well

    private float _phase;
    private float _lp;              // low-pass state
    private float _hp;              // high-pass state

    public override float ProcessSample(float sample, int channel) {
        // simple pitch shift
        _phase += Pitch;
        while (_phase >= 1f)
            _phase -= 1f;
        sample *= _phase;

        // high-pass (remove bass muddiness)
        _hp = sample - _hp * 0.995f;
        sample = _hp;

        // low-pass (telephone tone)
        _lp += (sample - _lp) * 0.25f;
        sample = _lp;

        // distortion
        sample *= Drive;
        sample = Math.Clamp(sample, -1f, 1f);

        // bit crush
        float steps = 1 << BitDepth;
        sample = MathF.Round(sample * steps) / steps;

        // output gain
        return sample * Gain;
    }
}
