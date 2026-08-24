using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;

namespace FightingGame;

// Loads folders of real .wav recordings from disk (the "sounds" directory, copied next to the exe -
// see the csproj) and picks a random clip from a folder each time an action plays, so repeated
// actions (footsteps, gunshots, etc.) don't all sound identical.
public static class SoundBank
{
    private static readonly string SoundsRoot = Path.Combine(AppContext.BaseDirectory, "sounds");

    // Empty (rather than throwing) if the folder doesn't exist, so a missing/renamed folder just
    // silently drops that sound instead of crashing the game on startup.
    public static List<SoundEffect> LoadFolder(string folderName)
    {
        var effects = new List<SoundEffect>();
        string directory = Path.Combine(SoundsRoot, folderName);
        if (!Directory.Exists(directory))
            return effects;

        foreach (var file in Directory.GetFiles(directory, "*.wav"))
        {
            using var stream = File.OpenRead(file);
            effects.Add(SoundEffect.FromStream(stream));
        }

        return effects;
    }

    public static void PlayRandom(IReadOnlyList<SoundEffect> effects, Random random, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        if (effects == null || effects.Count == 0)
            return;

        effects[random.Next(effects.Count)].Play(volume, pitch, pan);
    }
}
