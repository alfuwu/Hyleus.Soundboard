using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hyleus.Soundboard.Audio.Codecs;
using Hyleus.Soundboard.Audio.VoiceChangers;
using Hyleus.Soundboard.Framework;
using Hyleus.Soundboard.Framework.Structs;
using SharpJaad.AAC.Syntax;
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
    private readonly List<SoundPlayer> _activeSfxs = [];
    private readonly AudioCaptureDevice _micDevice;
    private readonly AudioPlaybackDevice _playbackDevice;
    private float _sfxVolume;
    private float _micVolume;

    public float SfxVolume { get => _sfxVolume; set { _sfxVolume = value; _activeSfxs.ForEach(sfx => sfx.Volume = _sfxVolume); } }
    public float MicVolume { get => _micVolume; set { _micVolume = value; if (_micPlayer != null) _micPlayer.Volume = _sfxVolume; } }

    public AudioEngine(int sampleRate = 44100, float sfxVolume = 3f, float micVolume = 2.0f) {
        // initalize SoundFlow engine (miniaudio backend)
        _engine = new MiniAudioEngine();
        _format = new() { Channels = 2, SampleRate = sampleRate, Format = SampleFormat.F32 };

        SfxVolume = sfxVolume;
        MicVolume = micVolume;

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
            Name = "MicrophoneInput",
            Volume = MicVolume
        };

        //_micPlayer.AddModifier(new PitchModifier2(2.0f));
        //_micPlayer.AddModifier(new ChorusModifier(_format));
        //_micPlayer.AddModifier(new AutoTuneModifier(sampleRate));
        _micPlayer.AddModifier(new RobotModifier(gain: 2.0f));

        // add mic player to the global mixer
        _playbackDevice.MasterMixer.AddComponent(_micPlayer);

        // start capturing microphone and playback
        _micProvider.StartCapture();
        _micPlayer.Play();

        // registering new decoder codecs
        _engine.RegisterCodecFactory(new AacCodecFactory());
        _engine.RegisterCodecFactory(new AiffCodecFactory());
        _engine.RegisterCodecFactory(new MatroskaCodecFactory());
        _engine.RegisterCodecFactory(new OggOpusCodecFactory());
        _engine.RegisterCodecFactory(new OggVorbisCodecFactory());
    }

    public void PlaySound(string filePath, float volume = 1.0f) {
        try {
            // open file stream to get data
            var fs = File.OpenRead(filePath);
            var stream = new NonClosingStream(fs); // only we are allowed to close the file stream
            // matroska files were closing the stream early for some reason which is why this is here

            // load a file as a data provider
            var fileProvider = new StreamDataProvider(_engine, _format, stream);

            // create a player for that file
            var filePlayer = new SoundPlayer(_engine, _format, fileProvider) {
                Name = Path.GetFileName(filePath),
                Volume = SfxVolume * volume
            };
            // wav, flac, and mp3 are automagically resampled to the correct sr as they're part of MiniAudio's internal library
            // TODO: make it so that PlaybackSpeed isn't used for our codecs because it's quite bad
            /*if (fileProvider.FormatInfo.FormatIdentifier != "wav" &&
                fileProvider.FormatInfo.FormatIdentifier != "mp3" &&
                fileProvider.FormatInfo.FormatIdentifier != "flac" &&
                fileProvider.FormatInfo.FormatIdentifier != "aiff"
            )
                filePlayer.PlaybackSpeed = (float)fileProvider.SampleRate / _format.SampleRate;*/
            
            _activeSfxs.Add(filePlayer);

            // add to master mixer and start playback
            _playbackDevice.MasterMixer.AddComponent(filePlayer);
            filePlayer.Play();

            Log.Info($"Playing sound '{filePlayer.Name}'");
            filePlayer.PlaybackEnded += (s, e) => {
                Log.Info($"Finished playing sound '{filePlayer.Name}'");
                // remove from mixer and dispose when finished
                _playbackDevice.MasterMixer.RemoveComponent(filePlayer);
                filePlayer.Dispose();
                fileProvider.Dispose();

                fs.Dispose();
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
}