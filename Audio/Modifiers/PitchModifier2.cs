using System;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using SoundFlow.Abstracts;

namespace Hyleus.Soundboard.Audio.VoiceChangers;
public sealed class PitchModifier2(float pitch) : SoundModifier {
    public override string Name { get; set; } = "Fancy Pitch Modifier";

    public float Pitch { get; } = pitch;

    public override void Process(Span<float> buffer, int channels) {
        float[] resampled = PitchShiftOne(buffer, channels);

        int originalFrames = buffer.Length / channels;
        int resampledFrames = resampled.Length / channels;
        float[] stretchedData = PhaseVocoderTimeStretch(resampled, channels, resampledFrames, originalFrames);
        stretchedData.CopyTo(buffer);
    }

    public float[] PitchShiftOne(Span<float> buffer, int channels) {
        int totalSamples = buffer.Length;
        int inputFrames = totalSamples / channels;

        if (inputFrames == 0)
            return [];

        int outputFrames = Math.Max(1, (int)Math.Round(inputFrames / Pitch));
        float[] output = new float[outputFrames * channels];

        int kernelRadius = 8;

        for (int outFrame = 0; outFrame < outputFrames; outFrame++) {
            double srcPos = outFrame * (inputFrames / (double)outputFrames);
            int srcIndexFloor = (int)Math.Floor(srcPos);
            double frac = srcPos - srcIndexFloor;

            int left = Math.Max(0, srcIndexFloor - kernelRadius + 1);
            int right = Math.Min(inputFrames - 1, srcIndexFloor + kernelRadius);

            for (int c = 0; c < channels; c++) {
                double sum = 0.0;
                double wsum = 0.0;
                
                for (int j = left; j <= right; j++) {
                    double x = srcPos - j;
                    double w = LanczosWindowedSinc(x, kernelRadius);
                    sum += buffer[j * channels + c] * w;
                    wsum += Math.Abs(w);
                }

                float sampleValue = wsum > 1e-12 ? (float)(sum / wsum) : 0.0f;
                output[outFrame * channels + c] = sampleValue;
            }
        }

        return output;
    }

    private static float[] PhaseVocoderTimeStretch(float[] data, int channels, int inputFrames, int targetFrames) {
        if (inputFrames <= 0 || data == null || data.Length == 0)
            return [];

        // Deinterleave
        var inChannels = new float[channels][];
        for (int c = 0; c < channels; c++)
            inChannels[c] = new float[inputFrames];

        for (int f = 0; f < inputFrames; f++)
            for (int c = 0; c < channels; c++)
                inChannels[c][f] = data[f * channels + c];

        var outChannels = new float[channels][];

        for (int c = 0; c < channels; c++)
            outChannels[c] = PhaseVocoderStretchChannel(inChannels[c], inputFrames, targetFrames);

        // Interleave back
        var outData = new float[(long)targetFrames * channels];
        for (int f = 0; f < targetFrames; f++)
            for (int c = 0; c < channels; c++)
                outData[f * channels + c] = outChannels[c].Length > f ? outChannels[c][f] : 0f;

        return outData;
    }

    private static float[] PhaseVocoderStretchChannel(float[] input, int inputFrames, int targetFrames) {
        // Parameters
        int N = 1024; // window size (power of two)
        if (N > inputFrames)
            N = 1 << (int)Math.Ceiling(Math.Log2(Math.Max(256, inputFrames)));

        int Ha = N / 4; // analysis hop

        double stretchRatio = (double)targetFrames / Math.Max(1, inputFrames);
        double HsD = Ha * stretchRatio; // synthesis hop (may be fractional)

        var window = HannWindow(N);

        // number of analysis frames
        int frames = Math.Max(1, (int)Math.Ceiling((inputFrames - N) / (double)Ha)) + 1;

        // pad input to fit
        int padded = (frames - 1) * Ha + N;
        var x = new double[padded];
        for (int i = 0; i < padded; i++)
            x[i] = (i < inputFrames) ? input[i] : 0.0;

        // Prepare arrays
        var magnitudes = new double[frames][];
        var phases = new double[frames][];

        // fft buffers
        Complex[] fftBuf = new Complex[N];

        for (int m = 0; m < frames; m++) {
            int pos = m * Ha;
            for (int n = 0; n < N; n++) {
                double v = x[pos + n] * window[n];
                fftBuf[n] = new Complex(v, 0.0);
            }

            Fourier.Forward(fftBuf, FourierOptions.Matlab);

            magnitudes[m] = new double[N / 2 + 1];
            phases[m] = new double[N / 2 + 1];
            for (int k = 0; k <= N / 2; k++) {
                var c = fftBuf[k];
                magnitudes[m][k] = c.Magnitude;
                phases[m][k] = Math.Atan2(c.Imaginary, c.Real);
            }
        }

        // Phase vocoder processing
        var synthesisPhases = new double[N / 2 + 1];
        var prevPhases = phases[0];

        // angular frequencies
        double[] omega = new double[N / 2 + 1];
        for (int k = 0; k <= N / 2; k++)
            omega[k] = 2.0 * Math.PI * k / N;

        // prepare output length estimate
        int estOutLen = (int)Math.Ceiling((frames - 1) * HsD + N);
        var y = new double[estOutLen + N];

        double synthesisTime = 0.0;
        int outPos = 0;

        for (int m = 0; m < frames; m++) {
            double[] mag = magnitudes[m];
            double[] ph = phases[m];

            if (m == 0) {
                for (int k = 0; k <= N / 2; k++)
                    synthesisPhases[k] = ph[k];
            } else {
                double[] prevPh = prevPhases;

                // phase advance
                for (int k = 0; k <= N / 2; k++) {
                    double delta = ph[k] - prevPh[k] - omega[k] * Ha;
                    delta = WrapPhase(delta);
                    double trueFreq = omega[k] + delta / Ha;
                    synthesisPhases[k] += trueFreq * HsD;
                }

                prevPhases = ph;
            }

            // Construct complex spectrum for synthesis
            for (int k = 0; k <= N / 2; k++) {
                double magv = mag[k];
                double phv = synthesisPhases[k];
                fftBuf[k] = Complex.FromPolarCoordinates(magv, phv);
            }

            // Mirror for negative frequencies
            for (int k = N / 2 + 1; k < N; k++)
                fftBuf[k] = Complex.Conjugate(fftBuf[N - k]);

            // IFFT
            Fourier.Inverse(fftBuf, FourierOptions.Matlab);

            // overlap-add
            for (int n = 0; n < N; n++) {
                int idx = outPos + n;
                if (idx >= 0 && idx < y.Length)
                    y[idx] += fftBuf[n].Real * window[n];
            }

            outPos = (int)Math.Round(++synthesisTime * HsD);
        }

        // Trim or pad to targetFrames
        var output = new float[targetFrames];
        for (int i = 0; i < targetFrames; i++)
            if (i < y.Length)
                output[i] = (float)Math.Clamp(y[i], -1.0, 1.0);
            else
                output[i] = 0f;

        return output;
    }

    private static double WrapPhase(double phase) {
        while (phase > Math.PI)
            phase -= 2.0 * Math.PI;

        while (phase < -Math.PI)
            phase += 2.0 * Math.PI;

        return phase;
    }

    private static double[] HannWindow(int N) {
        var w = new double[N];
        for (int n = 0; n < N; n++)
            w[n] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * n / (N - 1)));

        return w;
    }

    private static double LanczosWindowedSinc(double x, int a) {
        x = Math.Abs(x);
        if (x < 1e-12)
            return 1.0;

        if (x >= a)
            return 0.0;

        double piX = Math.PI * x;
        double sinc1 = Math.Sin(piX) / piX;
        double piXOverA = piX / a;
        double sinc2 = Math.Sin(piXOverA) / (piXOverA == 0.0 ? 1.0 : piXOverA);
        return sinc1 * sinc2;
    }

    public override float ProcessSample(float sample, int channel) => sample;
}
