using System;

namespace Hyleus.Soundboard.Audio;
public sealed class WindowedSincResampler(
    int channels,
    int inputRate,
    int outputRate,
    int kernelRadius = 16
) {
    private readonly int _channels = channels;
    //private readonly int _inputRate = inputRate;
    //private readonly int _outputRate = outputRate;
    private readonly int _kernelRadius = kernelRadius;
    private readonly float _cutoff = Math.Min(1f, (float)outputRate / inputRate);
    private readonly double _rateRatio = (double)inputRate / outputRate;

    private double _position; // fractional input frame position
    private float[] _inputBuffer = [];
    private int _inputFrames;

    public int Process(
        ReadOnlySpan<float> input,
        Span<float> output) {
        int inputFrames = input.Length / _channels;
        EnsureInputCapacity(inputFrames);

        input.CopyTo(_inputBuffer);
        _inputFrames = inputFrames;

        int outFrames = output.Length / _channels;
        int framesWritten = 0;

        while (framesWritten < outFrames) {
            int baseFrame = (int)_position;
            if (baseFrame + _kernelRadius >= _inputFrames)
                break;

            double frac = _position - baseFrame;

            for (int ch = 0; ch < _channels; ch++) {
                double sum = 0.0;

                for (int k = -_kernelRadius; k <= _kernelRadius; k++) {
                    int frameIndex = baseFrame + k;
                    if ((uint)frameIndex >= (uint)_inputFrames)
                        continue;

                    float sample = _inputBuffer[frameIndex * _channels + ch];
                    double x = k - frac;

                    sum += sample * Sinc(x * _cutoff) * HannWindow(x);
                }

                output[framesWritten * _channels + ch] = (float)(sum * _cutoff);
            }

            _position += _rateRatio;
            framesWritten++;
        }

        // remove consumed input frames
        int consumed = (int)_position;
        _position -= consumed;

        int remainingFrames = _inputFrames - consumed;
        if (remainingFrames > 0) {
            Array.Copy(
                _inputBuffer,
                consumed * _channels,
                _inputBuffer,
                0,
                remainingFrames * _channels);
        }

        _inputFrames = remainingFrames;

        return framesWritten * _channels;
    }

    private static double Sinc(double x) {
        if (x == 0.0)
            return 1.0;

        x *= Math.PI;
        return Math.Sin(x) / x;
    }

    private double HannWindow(double x) {
        double n = x / _kernelRadius;
        if (Math.Abs(n) > 1.0)
            return 0.0;

        return 0.5 * (1.0 + Math.Cos(Math.PI * n));
    }

    private void EnsureInputCapacity(int frames) {
        int needed = frames * _channels;
        if (_inputBuffer.Length < needed)
            _inputBuffer = new float[needed];
    }
}
