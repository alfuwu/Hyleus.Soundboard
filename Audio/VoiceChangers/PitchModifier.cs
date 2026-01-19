using System;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using SoundFlow.Abstracts;

namespace Hyleus.Soundboard.Audio.VoiceChangers;
public sealed class PitchModifier : SoundModifier {
    public override string Name { get; set; } = "Pitch Modifier";

    // 1.0 = normal
    // <1.0 = lower pitch
    // >1.0 = higher pitch
    public float Pitch { get; set; }

    private readonly int frameSize = 1024;
    private readonly int hopSize;
    private readonly double[] window;

    public PitchModifier(float pitch = 1.0f, int frameSize = 1024) {
        this.frameSize = frameSize;
        hopSize = frameSize / 4; // 75% overlap
        Pitch = pitch;

        // Hann window
        window = new double[frameSize];
        for (int n = 0; n < frameSize; n++)
            window[n] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / (frameSize - 1));
    }

    public override void Process(Span<float> buffer, int channels) {
        if (!Enabled) return;

        int numFrames = (buffer.Length - frameSize) / hopSize;
        var output = new float[buffer.Length];

        for (int f = 0; f <= numFrames; f++) {
            int offset = f * hopSize;

            // 1. Copy and window frame
            var frame = new Complex[frameSize];
            for (int n = 0; n < frameSize; n++)
                frame[n] = new Complex(buffer[offset + n] * window[n], 0);

            // 2. FFT
            Fourier.Forward(frame, FourierOptions.Matlab);

            // 3. Pitch shift in frequency domain with interpolation
            var pitched = new Complex[frameSize];
            for (int k = 0; k < frameSize; k++) {
                double idx = k / Pitch;
                int i0 = (int)Math.Floor(idx);
                int i1 = i0 + 1;
                double frac = idx - i0;

                if (i0 >= 0 && i1 < frameSize)
                    pitched[k] = frame[i0] * (1 - frac) + frame[i1] * frac;
            }

            // 4. Inverse FFT
            Fourier.Inverse(pitched, FourierOptions.Matlab);

            // 5. Overlap-add with window
            for (int n = 0; n < frameSize; n++) {
                int outIndex = offset + n;
                if (outIndex < output.Length)
                    output[outIndex] += (float)(pitched[n].Real * window[n]);
            }
        }

        // Copy result back to buffer
        output.CopyTo(buffer);
    }

    // unused
    public override float ProcessSample(float sample, int channel) => sample;
}
