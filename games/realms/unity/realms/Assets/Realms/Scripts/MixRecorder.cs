using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Realms
{
    /// <summary>
    /// A silent capture's recording of the whole mix (the sound plan's mix.wav, for measuring
    /// loudness, peaks and the music's share), as 32-bit float WAV beside the frames. It is the
    /// listener's last filter: it copies each block of the finished mix and then clears it, so the
    /// game mixes at the players' level while the speakers get silence. The listener stays at
    /// volume 0 until the filter runs, and goes back to 0 whenever the filter stops running and
    /// before the recorder goes, so a capture is never heard.
    /// </summary>
    public sealed class MixRecorder : MonoBehaviour
    {
        const int HeaderBytes = 56;
        const int Capacity = 1 << 22; // floats waiting for the main thread: 43 s of 48 kHz stereo

        // The audio thread's side, under the lock.
        readonly object gate = new object();
        float[] pending = new float[Capacity];
        float[] spare = new float[Capacity];
        int filled, channels;
        bool dropped;
        long firstTicks;
        int blocks;

        // The main thread's side.
        FileStream file;
        int rate, seenBlocks;
        long frames, startTicks;
        double startTime, seenAt;
        bool loud, started, lostNoted;

        /// <summary>
        /// Puts the recorder on the listener, writing into <paramref name="path"/>; the listener
        /// is turned up only once the recorder's filter clears its output.
        /// </summary>
        public static MixRecorder Attach(string path)
        {
            AudioListener.volume = 0f;
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                Sounds.Note("mix: no listener, no mix.wav");
                return null;
            }
            var recorder = listener.gameObject.AddComponent<MixRecorder>();
            recorder.rate = AudioSettings.outputSampleRate;
            recorder.startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            recorder.startTime = Time.realtimeSinceStartupAsDouble;
            recorder.file = new FileStream(path, FileMode.Create, FileAccess.Write);
            recorder.file.Write(new byte[HeaderBytes], 0, HeaderBytes);
            return recorder;
        }

        void OnAudioFilterRead(float[] data, int count)
        {
            lock (gate)
            {
                if (blocks == 0)
                {
                    channels = count;
                    firstTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                }
                if (filled + data.Length <= pending.Length)
                {
                    Array.Copy(data, 0, pending, filled, data.Length);
                    filled += data.Length;
                }
                else
                    dropped = true;
            }
            // Nothing reaches the speakers.
            Array.Clear(data, 0, data.Length);
            Interlocked.Increment(ref blocks);
        }

        void Update()
        {
            if (file == null)
                return;
            int seen = Volatile.Read(ref blocks);
            double now = Time.realtimeSinceStartupAsDouble;
            if (seen != seenBlocks)
            {
                seenBlocks = seen;
                seenAt = now;
            }
            // The game is heard by the filter only while the filter clears the output.
            bool running = seen > 0 && now - seenAt < 0.25;
            if (running != loud)
            {
                loud = running;
                AudioListener.volume = loud ? 1f : 0f;
                if (!loud)
                    Sounds.Note("mix: the filter stopped running, the listener is silent again");
            }
            Flush();
        }

        void Flush()
        {
            int count, inChannels;
            bool lost;
            long first;
            lock (gate)
            {
                (pending, spare) = (spare, pending);
                count = filled;
                filled = 0;
                lost = dropped;
                dropped = false;
                inChannels = channels;
                first = firstTicks;
            }
            if (lost && !lostNoted)
            {
                lostNoted = true;
                Sounds.Note("mix: some of the output was lost (the main thread stalled too long)");
            }
            if (count == 0)
                return;
            if (!started)
            {
                // The time of the first block on the log's clock: the recording's zero.
                started = true;
                double at = startTime + (first - startTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
                Sounds.Note($"mix starts at {at:0.000}, {rate} Hz, {inChannels} channels, sliders {SoundSettings.Describe()}");
            }
            var bytes = new byte[count * 4];
            Buffer.BlockCopy(spare, 0, bytes, 0, bytes.Length);
            file.Write(bytes, 0, bytes.Length);
            frames += count / Math.Max(1, inChannels);
            WriteHeader(inChannels);
        }

        // RIFF, fmt (IEEE float), fact and data, rewritten after every write so that a capture cut
        // off by its timeout still leaves a readable file.
        void WriteHeader(int inChannels)
        {
            long end = file.Position;
            long data = frames * inChannels * 4;
            using var header = new MemoryStream(HeaderBytes);
            using var writer = new BinaryWriter(header);
            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write((uint)(HeaderBytes - 8 + data));
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16u);
            writer.Write((ushort)3);
            writer.Write((ushort)inChannels);
            writer.Write((uint)rate);
            writer.Write((uint)(rate * inChannels * 4));
            writer.Write((ushort)(inChannels * 4));
            writer.Write((ushort)32);
            writer.Write(new[] { (byte)'f', (byte)'a', (byte)'c', (byte)'t' });
            writer.Write(4u);
            writer.Write((uint)frames);
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write((uint)data);
            file.Seek(0, SeekOrigin.Begin);
            file.Write(header.GetBuffer(), 0, HeaderBytes);
            file.Seek(end, SeekOrigin.Begin);
        }

        void OnApplicationQuit() => Close();

        void OnDisable() => Close();

        void Close()
        {
            // Silent first, while this filter still clears the output.
            AudioListener.volume = 0f;
            loud = false;
            if (file == null)
                return;
            Flush();
            Sounds.Note($"mix ends, {frames / (double)Math.Max(1, rate):0.0} s");
            file.Dispose();
            file = null;
        }
    }
}
