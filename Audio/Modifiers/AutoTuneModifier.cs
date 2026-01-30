using System;
using SoundFlow.Abstracts;

namespace Hyleus.Soundboard.Audio.VoiceChangers;
public class AutoTuneModifier(int sampleRate = 44100) : SoundModifier {
    public override string Name { get; set; } = "Autotune Modifier";

    // Hard-coded C major scale
    private static readonly int[] Scale = [0, 2, 4, 5, 7, 9, 11];

    private readonly int MaxFrames = sampleRate / 10; // 100ms buffer at 44.1kHz
    private readonly float[] _ringBuffer = new float[sampleRate / 5];
    private int _writeFrame;
    private float _readFrame;

    // Optional: minimum and maximum pitch detection
    private const float MinFreq = 80f;
    private const float MaxFreq = 1200f;

    public override void Process(Span<float> buffer, int channels) {
        if (!Enabled)
            return;

        int frames = buffer.Length / channels;

        // Copy incoming samples into ring buffer
        for (int f = 0; f < frames; f++) {
            int ringIndex = (_writeFrame % MaxFrames) * channels;
            int bufIndex = f * channels;
            for (int ch = 0; ch < channels; ch++)
                _ringBuffer[ringIndex + ch] = buffer[bufIndex + ch];
            _writeFrame++;
        }

        // Pitch detection (autocorrelation on mono average)
        float pitch = DetectPitch(buffer, channels);

        if (pitch > 0) {
            float targetPitch = QuantizePitch(pitch);
            float pitchRatio = targetPitch / pitch;

            // Read out samples at adjusted pitch
            for (int f = 0; f < frames; f++) {
                int i0 = ((int)_readFrame) % MaxFrames;
                int i1 = (i0 + 1) % MaxFrames;
                float frac = _readFrame - (int)_readFrame;
                int dstIndex = f * channels;

                for (int ch = 0; ch < channels; ch++) {
                    float s0 = _ringBuffer[i0 * channels + ch];
                    float s1 = _ringBuffer[i1 * channels + ch];
                    buffer[dstIndex + ch] = s0 + (s1 - s0) * frac;
                }

                _readFrame += pitchRatio;
            }
        } else {
            // No pitch detected: pass-through
            _readFrame += 1f;
        }

        // Prevent runaway
        if (_readFrame > _writeFrame - 1) _readFrame = _writeFrame - 1;
    }

    public override float ProcessSample(float sample, int channel) => sample;

    private float DetectPitch(Span<float> buffer, int channels) {
        // Simple autocorrelation on mono average
        int frames = buffer.Length / channels;
        if (frames < 32) return -1;

        // Mono average
        Span<float> mono = stackalloc float[frames];
        for (int i = 0; i < frames; i++) {
            float sum = 0;
            for (int ch = 0; ch < channels; ch++)
                sum += buffer[i * channels + ch];
            mono[i] = sum / channels;
        }

        int bestLag = 0;
        float maxCorr = 0f;
        int minLag = (int)(44100f / MaxFreq);
        int maxLag = (int)(44100f / MinFreq);

        for (int lag = minLag; lag <= maxLag; lag++) {
            float corr = 0f;
            for (int i = 0; i < frames - lag; i++)
                corr += mono[i] * mono[i + lag];
            if (corr > maxCorr) {
                maxCorr = corr;
                bestLag = lag;
            }
        }

        if (bestLag == 0) return -1;

        return 44100f / bestLag;
    }

    private static float QuantizePitch(float pitchHz) {
        float midi = 69 + 12 * MathF.Log2(pitchHz / 440f);
        int nearestNote = (int)MathF.Round(midi);

        // Find nearest note in the scale
        int octave = nearestNote / 12;
        int noteInOctave = nearestNote % 12;

        // Find closest scale note
        int closestScaleNote = Scale[0];
        int minDistance = 12; // max possible distance
        foreach (int scaleNote in Scale) {
            int distance = Math.Abs(scaleNote - noteInOctave);
            if (distance < minDistance) {
                minDistance = distance;
                closestScaleNote = scaleNote;
            }
        }

        int quantizedMidi = octave * 12 + closestScaleNote;

        // Convert back to Hz
        return 440f * MathF.Pow(2, (quantizedMidi - 69) / 12f);
    }
}
