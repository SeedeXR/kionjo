#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;

namespace SeedeXR.Kionjo
{
    /// <summary>
    /// The scene's mixed sound, rendered in step with the capture clock (AudioRenderer), one capture
    /// frame's worth of samples per call to Frame, written as a 16-bit WAV at the end.
    /// </summary>
    public class KionjoAudioCapture
    {
        readonly List<float> _samples = new();
        int _channels;
        bool _running;

        public bool Start(out string warning)
        {
            warning = null;
            _channels = Channels(AudioSettings.speakerMode);
            try
            {
                _running = AudioRenderer.Start();
            }
            catch (Exception ex)
            {
                warning = $"No sound: audio rendering could not start ({ex.Message}).";
                return false;
            }
            if (!_running) warning = "No sound: audio rendering could not start (audio may be disabled).";
            return _running;
        }

        /// <summary>
        /// Call once per captured frame. A frame the renderer gives no or too few samples for (it gives none on the
        /// first frames after Start) is filled with silence in place, so the sound covers every video frame and stays
        /// in sync; otherwise the mux's -shortest would cut the video to the sound.
        /// </summary>
        public void Frame(float fps)
        {
            if (!_running) return;
            int count = AudioRenderer.GetSampleCountForCaptureFrame() * _channels;
            if (count > 0)
            {
                using var buffer = new NativeArray<float>(count, Allocator.Temp);
                if (AudioRenderer.Render(buffer)) _samples.AddRange(buffer);
            }
            _frames++;
            if (fps <= 0f) return;
            long due = (long)Math.Round(_frames * (double)AudioSettings.outputSampleRate / fps) * _channels;
            while (_samples.Count < due) _samples.Add(0f);
        }

        int _frames;

        /// <summary>Stops, writes the WAV to path (when given and there is sound) and returns it, else null with the reason.</summary>
        public string Stop(string path, out string warning)
        {
            warning = null;
            if (!_running)
            {
                warning = "No sound was rendered.";
                return null;
            }
            _running = false;
            AudioRenderer.Stop();
            if (path == null) return null;
            if (_samples.Count == 0)
            {
                warning = "No sound: the audio renderer gave no samples.";
                return null;
            }
            Write(path, _samples, _channels, AudioSettings.outputSampleRate);
            return path;
        }

        static int Channels(AudioSpeakerMode mode) => mode switch
        {
            AudioSpeakerMode.Mono => 1,
            AudioSpeakerMode.Quad => 4,
            AudioSpeakerMode.Surround => 5,
            AudioSpeakerMode.Mode5point1 => 6,
            AudioSpeakerMode.Mode7point1 => 8,
            _ => 2,
        };

        static void Write(string path, List<float> samples, int channels, int rate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Count * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)channels);
            w.Write(rate);
            w.Write(rate * channels * 2);
            w.Write((short)(channels * 2));
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(bytes);
            foreach (var s in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}
#endif
