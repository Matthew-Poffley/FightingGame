using System;
using Microsoft.Xna.Framework;

namespace FightingGame;

// Ground height as a function of X, built from a few summed sine waves for smooth random rolling hills.
public class Terrain
{
    private readonly float[] _amplitudes;
    private readonly float[] _frequencies;
    private readonly float[] _phases;
    private readonly float _baseHeight;

    public Terrain(float baseHeight, Random random)
    {
        _baseHeight = baseHeight;

        // Kept modest (58px total worst-case swing) so a ground peak never eats into an elevated tier's clearance.
        float[] maxAmplitudes = { 30f, 18f, 10f };
        float[] baseWavelengths = { 700f, 350f, 160f };

        _amplitudes = new float[maxAmplitudes.Length];
        _frequencies = new float[maxAmplitudes.Length];
        _phases = new float[maxAmplitudes.Length];

        for (int i = 0; i < maxAmplitudes.Length; i++)
        {
            _amplitudes[i] = (float)random.NextDouble() * maxAmplitudes[i];
            float wavelength = baseWavelengths[i] * (0.6f + (float)random.NextDouble() * 0.8f);
            _frequencies[i] = MathHelper.TwoPi / wavelength;
            _phases[i] = (float)random.NextDouble() * MathHelper.TwoPi;
        }
    }

    public float GetHeightAt(float x)
    {
        float height = _baseHeight;
        for (int i = 0; i < _amplitudes.Length; i++)
            height += _amplitudes[i] * MathF.Sin(x * _frequencies[i] + _phases[i]);
        return height;
    }
}
