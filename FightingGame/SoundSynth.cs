using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace FightingGame;

public enum WaveShape { Sine, Square, Triangle }

// Generates simple sound effects as raw PCM waveforms at load time - there are no audio assets in
// this project, so every sound here is synthesized rather than loaded from a file.
public static class SoundSynth
{
    private const int SampleRate = 44100;

    public static SoundEffect CreateTone(float startFrequency, float endFrequency, float duration, float volume, WaveShape shape = WaveShape.Sine, float decayPower = 1f)
    {
        int sampleCount = Math.Max(1, (int)(SampleRate * duration));
        byte[] buffer = new byte[sampleCount * 2];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            float frequency = MathHelper.Lerp(startFrequency, endFrequency, progress);
            phase += frequency / SampleRate;

            float wave = shape switch
            {
                WaveShape.Square => MathF.Sign(MathF.Sin(MathHelper.TwoPi * phase)),
                WaveShape.Triangle => 2f * MathF.Abs(2f * (phase - MathF.Floor(phase + 0.5f))) - 1f,
                _ => MathF.Sin(MathHelper.TwoPi * phase)
            };

            float envelope = MathF.Pow(1f - progress, decayPower);
            WriteSample(buffer, i, wave * envelope * volume);
        }

        return new SoundEffect(buffer, SampleRate, AudioChannels.Mono);
    }

    public static SoundEffect CreateNoiseBurst(float duration, float volume, Random random, float decayPower = 2f)
    {
        int sampleCount = Math.Max(1, (int)(SampleRate * duration));
        byte[] buffer = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            float envelope = MathF.Pow(1f - progress, decayPower);
            float sample = (float)random.NextDouble() * 2f - 1f;
            WriteSample(buffer, i, sample * envelope * volume);
        }

        return new SoundEffect(buffer, SampleRate, AudioChannels.Mono);
    }

    // Concatenates a few tones back to back - used for short melodic stings like a round-win jingle.
    public static SoundEffect CreateArpeggio(float[] frequencies, float noteDuration, float volume)
    {
        int samplesPerNote = Math.Max(1, (int)(SampleRate * noteDuration));
        byte[] buffer = new byte[samplesPerNote * frequencies.Length * 2];

        for (int n = 0; n < frequencies.Length; n++)
        {
            for (int i = 0; i < samplesPerNote; i++)
            {
                float progress = i / (float)samplesPerNote;
                float envelope = MathF.Pow(1f - progress, 1.5f);
                float phase = frequencies[n] * i / (float)SampleRate;
                float sample = MathF.Sin(MathHelper.TwoPi * phase) * envelope * volume;
                WriteSample(buffer, n * samplesPerNote + i, sample);
            }
        }

        return new SoundEffect(buffer, SampleRate, AudioChannels.Mono);
    }

    private static void WriteSample(byte[] buffer, int index, float sample)
    {
        short value = (short)(MathHelper.Clamp(sample, -1f, 1f) * short.MaxValue);
        buffer[index * 2] = (byte)(value & 0xFF);
        buffer[index * 2 + 1] = (byte)((value >> 8) & 0xFF);
    }
}
