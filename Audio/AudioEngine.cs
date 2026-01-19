using System;
using System.IO;
using System.Linq;
using Hyleus.Soundboard.Audio.Codecs;
using Hyleus.Soundboard.Audio.VoiceChangers;
using Hyleus.Soundboard.Framework;
using Microsoft.Xna.Framework.Graphics;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace Hyleus.Soundboard.Audio;
public class AudioEngine : IDisposable {
    private readonly MiniAudioEngine _engine;
    private readonly AudioFormat _format;
    private readonly MicrophoneDataProvider _micProvider;
    private readonly SoundPlayer _micPlayer;
    private readonly AudioCaptureDevice _micDevice;
    private readonly AudioPlaybackDevice _playbackDevice;

    public AudioEngine(int sampleRate = 44100) {
        // initalize SoundFlow engine (miniaudio backend)
        _engine = new MiniAudioEngine();
        _format = new() { Channels = 2, SampleRate = sampleRate, Format = SampleFormat.F32 };

        _engine.UpdateAudioDevicesInfo();
        //DeviceInfo? deviceInfo = _engine.PlaybackDevices.FirstOrDefault(d => d.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));
        DeviceInfo? deviceInfo = _engine.PlaybackDevices.FirstOrDefault(d => d.IsDefault);
        if (deviceInfo == null) {
            Log.Error("VB-Audio Cable playback device not found. Failed to initialize AudioEngine.");
            throw new Exception("VB-Audio Cable playback device not found. Failed to initialize AudioEngine.");
        }

        Log.Info(deviceInfo);
        _playbackDevice = _engine.InitializePlaybackDevice(deviceInfo.Value, _format);
        _playbackDevice.Start();

        // setup mic capture
        DeviceInfo? micDeviceInfo = _engine.CaptureDevices.FirstOrDefault(d => d.IsDefault);
        if (micDeviceInfo == null) {
            Log.Error("No microphone device found. Failed to initialize AudioEngine.");
            throw new Exception("No microphone device found. Failed to initialize AudioEngine.");
        }

        Log.Info(micDeviceInfo);
        _micDevice = _engine.InitializeCaptureDevice(micDeviceInfo.Value, _format);
        _micDevice.Start();

        _micProvider = new MicrophoneDataProvider(_micDevice);

        // wrap mic in a player so it can be added to the mixer
        _micPlayer = new SoundPlayer(_engine, _format, _micProvider) {
            Name = "MicrophoneInput"
        };

        //_micPlayer.AddModifier(new PitchModifier2(2.0f));
        //_micPlayer.AddModifier(new ChorusModifier(_format));
        //_micPlayer.AddModifier(new AutoTuneModifier(sampleRate));
        _micPlayer.AddModifier(new RobotModifier(gain: 2.0f));
        //_micPlayer.AddModifier(new JunglePitchShifter(2.0f, sampleRate));

        // add mic player to the global mixer
        _playbackDevice.MasterMixer.AddComponent(_micPlayer);

        // start capturing microphone and playback
        _micProvider.StartCapture();
        _micPlayer.Play();

        // registering new decoder codecs
        //_engine.RegisterCodecFactory(new AacCodecFactory());
        _engine.RegisterCodecFactory(new AiffCodecFactory());
        //_engine.RegisterCodecFactory(new MatroskaCodecFactory());
        _engine.RegisterCodecFactory(new OggOpusCodecFactory());
        _engine.RegisterCodecFactory(new OggVorbisCodecFactory());
    }

    public void PlaySound(string filePath) {
        try {
            // open file stream to get data
            var fileStream = File.OpenRead(filePath);

            // load a file as a data provider
            var fileProvider = new StreamDataProvider(_engine, _format, fileStream);

            // create a player for that file
            var filePlayer = new SoundPlayer(_engine, _format, fileProvider);

            // add to master mixer and start playback
            _playbackDevice.MasterMixer.AddComponent(filePlayer);
            filePlayer.Play();

            Log.Info($"Playing sound '{filePath}'");
            filePlayer.PlaybackEnded += (s, e) => {
                // remove from mixer and dispose when finished
                _playbackDevice.MasterMixer.RemoveComponent(filePlayer);
                filePlayer.Dispose();
                fileProvider.Dispose();

                fileStream.Close();
                fileStream.Dispose();
            };
        } catch (Exception ex) {
            Log.Error($"Error playing sound '{filePath}': {ex.Message}");
        }
    }

    public void Dispose() {
        // stop mic playback/capture
        _micPlayer?.Stop();
        _micProvider?.StopCapture();

        // remove from master mixer
        _playbackDevice.MasterMixer.RemoveComponent(_micPlayer);

        // dispose engine and providers
        _micProvider?.Dispose();
        _micPlayer?.Dispose();
        _engine?.Dispose();

        GC.SuppressFinalize(this);
    }

    public static int Resample(Span<float> input, Span<float> output, int sampleRate, int targetSampleRate, ref float[] resampleBuffer) {
        if (sampleRate == targetSampleRate) {
            input.CopyTo(output);
            return input.Length;
        }

        double ratio = (double)targetSampleRate / sampleRate;
        int outLen = Math.Min(output.Length, (int)(input.Length * ratio));

        if (resampleBuffer.Length < input.Length)
            resampleBuffer = new float[input.Length];

        input.CopyTo(resampleBuffer);

        for (int i = 0; i < outLen; i++) {
            double pos = i / ratio;
            int index = (int)pos;
            double frac = pos - index;

            float sample = resampleBuffer[index];
            if (index + 1 < input.Length)
                sample += (resampleBuffer[index + 1] - resampleBuffer[index]) * (float)frac;

            output[i] = sample;
        }

        return outLen;
    }
}