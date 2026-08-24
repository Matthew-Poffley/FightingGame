using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

public enum UpgradeType
{
    ExtraMagazine,
    RapidFire,
    BiggerBullets,
    FasterBullets,
    SwiftBoots,
    TrackSpikes,
    HigherJumps,
    ThickSkin,
    VitalSurge,
    HollowPoints,
    HeavyCaliber,
    QuickHands,
    SpeedLoader,
    IronGuard,
    GuardTraining,
    Buckshot,
    ToxicRounds,
    HomingRounds,
    RicochetRounds,
    ExplosiveRounds,
    KnockbackForce,
    CurseWeakness,
    CurseSlowness,
    CurseFragile,
    CurseShrunkenRounds,
    CurseSlowRounds,
    CurseEmptyMag,
    CurseJammedGun,
    HealthRegen,
    ExtraLife,
    ExtraJump,
    KeenEye,
    GroundPoundRadius,
    GroundPoundPower,
    CurseGroundPoundRadius,
    CurseGroundPoundPower,
    AoEAura,
    AoEAuraSize,
    AoEAuraDamage,
    CurseAoERadius,
    CurseAoEDamage,
    FlatTrajectory,
    CurseHeavyRounds,
    LifeSteal,
    CurseLifeSteal,

    // Not part of AllTypes - never drawn from the normal pool. Only ever offered by
    // Upgrades.RollMegaCurse, always Legendary, and applied to the round leader instead of the
    // picker or "everyone else" - see Game1.PickRandomUpgrades/UpdateCardSelection.
    MegaCurseReset
}

// Better tiers roll a bigger Amount (see Upgrades.RollTiered) and show up less often (see Upgrades.RollRarity).
public enum Rarity
{
    Common,
    Rare,
    Legendary
}

// A card is a specific rolled instance of an upgrade type - the same type can turn up weaker or
// stronger from one offer to the next, and its rarity tier scales how much weaker/stronger.
public readonly struct Card
{
    public readonly UpgradeType Type;
    public readonly float Amount;
    public readonly Rarity Rarity;

    public Card(UpgradeType type, float amount, Rarity rarity)
    {
        Type = type;
        Amount = amount;
        Rarity = rarity;
    }
}

public static class Upgrades
{
    public static readonly UpgradeType[] AllTypes =
    {
        UpgradeType.ExtraMagazine,
        UpgradeType.RapidFire,
        UpgradeType.BiggerBullets,
        UpgradeType.FasterBullets,
        UpgradeType.SwiftBoots,
        UpgradeType.TrackSpikes,
        UpgradeType.HigherJumps,
        UpgradeType.ThickSkin,
        UpgradeType.VitalSurge,
        UpgradeType.HollowPoints,
        UpgradeType.HeavyCaliber,
        UpgradeType.QuickHands,
        UpgradeType.SpeedLoader,
        UpgradeType.IronGuard,
        UpgradeType.GuardTraining,
        UpgradeType.Buckshot,
        UpgradeType.ToxicRounds,
        UpgradeType.HomingRounds,
        UpgradeType.RicochetRounds,
        UpgradeType.ExplosiveRounds,
        UpgradeType.KnockbackForce,
        UpgradeType.CurseWeakness,
        UpgradeType.CurseSlowness,
        UpgradeType.CurseFragile,
        UpgradeType.CurseShrunkenRounds,
        UpgradeType.CurseSlowRounds,
        UpgradeType.CurseEmptyMag,
        UpgradeType.CurseJammedGun,
        UpgradeType.HealthRegen,
        UpgradeType.ExtraLife,
        UpgradeType.ExtraJump,
        UpgradeType.KeenEye,
        UpgradeType.GroundPoundRadius,
        UpgradeType.GroundPoundPower,
        UpgradeType.CurseGroundPoundRadius,
        UpgradeType.CurseGroundPoundPower,
        UpgradeType.AoEAura,
        UpgradeType.AoEAuraSize,
        UpgradeType.AoEAuraDamage,
        UpgradeType.CurseAoERadius,
        UpgradeType.CurseAoEDamage,
        UpgradeType.FlatTrajectory,
        UpgradeType.CurseHeavyRounds,
        UpgradeType.LifeSteal,
        UpgradeType.CurseLifeSteal
    };

    // Curse cards are applied to every other player instead of the picker themself - see the
    // application logic in Game1.UpdateCardSelection - so a losing player can sabotage the field
    // instead of only ever buffing themselves.
    public static bool IsCurse(UpgradeType type) => type switch
    {
        UpgradeType.CurseWeakness => true,
        UpgradeType.CurseSlowness => true,
        UpgradeType.CurseFragile => true,
        UpgradeType.CurseShrunkenRounds => true,
        UpgradeType.CurseSlowRounds => true,
        UpgradeType.CurseEmptyMag => true,
        UpgradeType.CurseJammedGun => true,
        UpgradeType.CurseGroundPoundRadius => true,
        UpgradeType.CurseGroundPoundPower => true,
        UpgradeType.CurseAoERadius => true,
        UpgradeType.CurseAoEDamage => true,
        UpgradeType.CurseHeavyRounds => true,
        UpgradeType.CurseLifeSteal => true,
        _ => false
    };

    // Mega curse cards target the round leader instead of the picker or "everyone else" - see
    // Game1.PickRandomUpgrades (where they're substituted in, rare and Legendary-only) and
    // Game1.UpdateCardSelection (where they're applied).
    public static bool IsMegaCurse(UpgradeType type) => type == UpgradeType.MegaCurseReset;

    // Every stat a Mega Curse can reset - every normal buff type, i.e. AllTypes minus the curses
    // (hexing someone's own curse debuffs away would read as a buff, not a curse).
    private static readonly UpgradeType[] ResettableTypes = Array.FindAll(AllTypes, t => !IsCurse(t));

    // Rolls a Mega Curse targeting a random resettable stat - always Legendary, never drawn from
    // the normal AllTypes pool. The target type is encoded in the card's Amount (cast back out in
    // GetName/GetDescription and Stickman.ResetUpgrade).
    public static Card RollMegaCurse(Random random)
    {
        var target = ResettableTypes[random.Next(ResettableTypes.Length)];
        return new Card(UpgradeType.MegaCurseReset, (float)(int)target, Rarity.Legendary);
    }

    // Chances a freshly-offered card comes back Legendary/Rare - anything left over is Common. These
    // are the base (tied-for-the-lead) odds; RollRarity scales them up for a player who's behind.
    private const double LegendaryChance = 0.05;
    private const double RareChance = 0.25;

    // Comeback mechanic: the further behind the round leader's win count you are, the better your
    // loot rolls, so a losing player has a real shot at closing the gap instead of falling further
    // behind. Scaling is capped so there's always at least some chance of a Common.
    private const double LegendaryChancePerWinBehind = 0.03;
    private const double RareChancePerWinBehind = 0.06;
    private const int MaxWinsBehindForRarityBonus = 8;
    private const double MaxCombinedRareOrBetterChance = 0.95;

    public static Rarity RollRarity(Random random, int winsBehind = 0)
    {
        int deficit = Math.Clamp(winsBehind, 0, MaxWinsBehindForRarityBonus);
        double legendaryChance = LegendaryChance + deficit * LegendaryChancePerWinBehind;
        double rareChance = RareChance + deficit * RareChancePerWinBehind;
        if (legendaryChance + rareChance > MaxCombinedRareOrBetterChance)
            rareChance = MaxCombinedRareOrBetterChance - legendaryChance;

        double roll = random.NextDouble();
        if (roll < legendaryChance)
            return Rarity.Legendary;
        if (roll < legendaryChance + rareChance)
            return Rarity.Rare;
        return Rarity.Common;
    }

    public static string GetRarityLabel(Rarity rarity) => rarity switch
    {
        Rarity.Rare => "RARE",
        Rarity.Legendary => "LEGENDARY",
        _ => "COMMON"
    };

    public static Color GetRarityColor(Rarity rarity) => rarity switch
    {
        Rarity.Rare => new Color(70, 140, 255),
        Rarity.Legendary => new Color(255, 175, 40),
        _ => new Color(190, 190, 195)
    };

    // The ranges commented below are the Common tier; Rare/Legendary scale them up via RollTiered.
    public static Card Roll(UpgradeType type, Rarity rarity, Random random)
    {
        float amount = type switch
        {
            UpgradeType.ExtraMagazine => rarity switch                                      // +1 to +3 rounds
            {
                Rarity.Legendary => random.Next(4, 7),
                Rarity.Rare => random.Next(2, 5),
                _ => random.Next(1, 4)
            },
            UpgradeType.RapidFire => RollTiered(10f, 30f, rarity, random),                   // 10-30% faster fire rate
            UpgradeType.BiggerBullets => RollTiered(25f, 75f, rarity, random),               // +25-75% bullet size
            UpgradeType.FasterBullets => RollTiered(15f, 40f, rarity, random),                // +15-40% bullet speed
            UpgradeType.SwiftBoots => RollTiered(8f, 25f, rarity, random),                    // +8-25% move speed
            UpgradeType.TrackSpikes => RollTiered(20f, 50f, rarity, random),                  // +20-50 flat move speed
            UpgradeType.HigherJumps => RollTiered(8f, 25f, rarity, random),                   // +8-25% jump power
            UpgradeType.ThickSkin => RollTiered(10f, 30f, rarity, random),                    // +10-30 max health
            UpgradeType.VitalSurge => RollTiered(8f, 20f, rarity, random),                    // +8-20% max health
            UpgradeType.HollowPoints => RollTiered(2f, 5f, rarity, random),                   // +2-5 damage per hit
            UpgradeType.HeavyCaliber => RollTiered(10f, 25f, rarity, random),                 // +10-25% damage per hit
            UpgradeType.QuickHands => RollTiered(15f, 40f, rarity, random),                   // 15-40% faster reload
            UpgradeType.SpeedLoader => RollTiered(0.15f, 0.4f, rarity, random),               // -0.15-0.4s reload time
            UpgradeType.IronGuard => RollTiered(0.5f, 1.5f, rarity, random),                  // +0.5-1.5s block stamina
            UpgradeType.GuardTraining => RollTiered(15f, 35f, rarity, random),                // +15-35% block stamina
            UpgradeType.Buckshot => rarity switch                                             // +1 to +2 bullets per shot
            {
                Rarity.Legendary => random.Next(3, 5),
                Rarity.Rare => random.Next(2, 4),
                _ => random.Next(1, 3)
            },
            UpgradeType.ToxicRounds => RollTiered(1f, 2.5f, rarity, random),                  // +1-2.5 poison damage/sec, stacks across picks (see Stickman.PoisonDuration)
            UpgradeType.HomingRounds => RollTiered(20f, 40f, rarity, random),                 // +20-40 deg/sec bullet turn rate, stacks across picks
            UpgradeType.RicochetRounds => rarity switch                                       // +1 to +2 bounces off walls/floors/platforms
            {
                Rarity.Legendary => random.Next(3, 5),
                Rarity.Rare => random.Next(2, 4),
                _ => random.Next(1, 3)
            },
            UpgradeType.ExplosiveRounds => RollTiered(50f, 90f, rarity, random),               // +50-90 explosion radius, stacks across picks
            UpgradeType.KnockbackForce => RollTiered(20f, 50f, rarity, random),                // +20-50% knockback force
            UpgradeType.CurseWeakness => RollTiered(6f, 15f, rarity, random),                  // -6-15% damage dealt by every other cow
            UpgradeType.CurseSlowness => RollTiered(6f, 15f, rarity, random),                  // -6-15% move speed for every other cow
            UpgradeType.CurseFragile => RollTiered(6f, 15f, rarity, random),                   // -6-15% max health for every other cow
            UpgradeType.CurseShrunkenRounds => RollTiered(6f, 15f, rarity, random),             // -6-15% bullet size for every other cow
            UpgradeType.CurseSlowRounds => RollTiered(6f, 15f, rarity, random),                 // -6-15% bullet speed for every other cow
            UpgradeType.CurseEmptyMag => RollTiered(6f, 15f, rarity, random),                   // -6-15% magazine size for every other cow
            UpgradeType.CurseJammedGun => rarity switch                                          // -1 to -2 bullets per shot for every other cow
            {
                Rarity.Legendary => random.Next(2, 4),
                Rarity.Rare => random.Next(1, 3),
                _ => 1
            },
            UpgradeType.HealthRegen => RollTiered(1f, 3f, rarity, random),                      // +1-3 health regen per sec, stacks across picks (starts at 0)
            UpgradeType.ExtraLife => rarity switch                                              // +1 to +2 extra lives
            {
                Rarity.Legendary => random.Next(2, 4),
                Rarity.Rare => random.Next(1, 3),
                _ => 1
            },
            UpgradeType.ExtraJump => rarity switch                                              // +1 to +2 extra air jumps
            {
                Rarity.Legendary => random.Next(2, 4),
                Rarity.Rare => random.Next(1, 3),
                _ => 1
            },
            UpgradeType.KeenEye => 1f,                                                          // +1 upgrade card offered in future rounds - flat, no rarity scaling
            UpgradeType.GroundPoundRadius => RollTiered(25f, 60f, rarity, random),               // +25-60 ground pound impact radius, stacks across picks
            UpgradeType.GroundPoundPower => RollTiered(10f, 25f, rarity, random),                 // +10-25% ground pound damage
            UpgradeType.CurseGroundPoundRadius => RollTiered(6f, 15f, rarity, random),             // -6-15% ground pound impact radius for every other cow
            UpgradeType.CurseGroundPoundPower => RollTiered(6f, 15f, rarity, random),               // -6-15% ground pound damage for every other cow
            UpgradeType.AoEAura => RollTiered(40f, 65f, rarity, random),                            // +40-65 AoE aura radius - unlocks the aura, damage/tick derives from radius (see Stickman.EffectiveAoEDamagePerTick)
            UpgradeType.AoEAuraSize => RollTiered(3f, 8f, rarity, random),                          // +3-8 AoE aura radius, small stacking increments
            UpgradeType.AoEAuraDamage => RollTiered(1f, 3f, rarity, random),                        // +1-3 AoE aura damage per tick, stacks
            UpgradeType.CurseAoERadius => RollTiered(6f, 15f, rarity, random),                      // -6-15% AoE aura radius for every other cow
            UpgradeType.CurseAoEDamage => RollTiered(6f, 15f, rarity, random),                      // -6-15% AoE aura damage for every other cow
            UpgradeType.FlatTrajectory => RollTiered(12f, 30f, rarity, random),                     // -12-30% bullet gravity - flatter, more predictable arcs
            UpgradeType.CurseHeavyRounds => RollTiered(12f, 30f, rarity, random),                   // +12-30% bullet gravity for every other cow - shots drop fast, hard to hit anything at range
            UpgradeType.LifeSteal => RollTiered(5f, 12f, rarity, random),                           // +5-12% of damage dealt returned as health, stacks
            UpgradeType.CurseLifeSteal => RollTiered(6f, 15f, rarity, random),                      // -6-15% life steal for every other cow
            _ => 0f
        };

        return new Card(type, amount, rarity);
    }

    private static float Lerp(float min, float max, Random random) => min + (float)random.NextDouble() * (max - min);

    // Scales a Common tier's [min, max] roll range up for Rare/Legendary, rather than tuning
    // separate ranges per type per tier.
    private static float RollTiered(float min, float max, Rarity rarity, Random random)
    {
        float multiplier = rarity switch
        {
            Rarity.Rare => 1.6f,
            Rarity.Legendary => 2.5f,
            _ => 1f
        };

        return Lerp(min * multiplier, max * multiplier, random);
    }

    public static string GetName(UpgradeType type) => type switch
    {
        UpgradeType.ExtraMagazine => "Extended Mag",
        UpgradeType.RapidFire => "Rapid Fire",
        UpgradeType.BiggerBullets => "Big Rounds",
        UpgradeType.FasterBullets => "Hot Loads",
        UpgradeType.SwiftBoots => "Swift Boots",
        UpgradeType.TrackSpikes => "Track Spikes",
        UpgradeType.HigherJumps => "Coiled Legs",
        UpgradeType.ThickSkin => "Thick Skin",
        UpgradeType.VitalSurge => "Vital Surge",
        UpgradeType.HollowPoints => "Hollow Points",
        UpgradeType.HeavyCaliber => "Heavy Caliber",
        UpgradeType.QuickHands => "Quick Hands",
        UpgradeType.SpeedLoader => "Speed Loader",
        UpgradeType.IronGuard => "Iron Guard",
        UpgradeType.GuardTraining => "Guard Training",
        UpgradeType.Buckshot => "Buckshot Rounds",
        UpgradeType.ToxicRounds => "Toxic Rounds",
        UpgradeType.HomingRounds => "Homing Rounds",
        UpgradeType.RicochetRounds => "Ricochet Rounds",
        UpgradeType.ExplosiveRounds => "Explosive Rounds",
        UpgradeType.KnockbackForce => "Bull Rush",
        UpgradeType.CurseWeakness => "Weakening Hex",
        UpgradeType.CurseSlowness => "Hobbling Curse",
        UpgradeType.CurseFragile => "Brittle Bones Hex",
        UpgradeType.CurseShrunkenRounds => "Shrinking Hex",
        UpgradeType.CurseSlowRounds => "Sluggish Rounds Hex",
        UpgradeType.CurseEmptyMag => "Hollow Magazine Hex",
        UpgradeType.CurseJammedGun => "Jamming Curse",
        UpgradeType.HealthRegen => "Regeneration",
        UpgradeType.ExtraLife => "Second Wind",
        UpgradeType.ExtraJump => "Cloven Hooves",
        UpgradeType.KeenEye => "Keen Eye",
        UpgradeType.GroundPoundRadius => "Seismic Hooves",
        UpgradeType.GroundPoundPower => "Heavy Hooves",
        UpgradeType.CurseGroundPoundRadius => "Muffled Stomp Hex",
        UpgradeType.CurseGroundPoundPower => "Softened Stomp Hex",
        UpgradeType.MegaCurseReset => "Great Leveling",
        UpgradeType.AoEAura => "Searing Aura",
        UpgradeType.AoEAuraSize => "Wider Aura",
        UpgradeType.AoEAuraDamage => "Scorching Aura",
        UpgradeType.CurseAoERadius => "Smothered Aura Hex",
        UpgradeType.CurseAoEDamage => "Cooling Aura Hex",
        UpgradeType.FlatTrajectory => "Flat Trajectory",
        UpgradeType.CurseHeavyRounds => "Heavy Rounds Hex",
        UpgradeType.LifeSteal => "Vampiric Bite",
        UpgradeType.CurseLifeSteal => "Withering Hex",
        _ => "Unknown"
    };

    public static string GetDescription(Card card) => card.Type switch
    {
        UpgradeType.ExtraMagazine => $"+{(int)card.Amount} rounds per magazine",
        UpgradeType.RapidFire => $"{card.Amount:0}% faster fire rate",
        UpgradeType.BiggerBullets => $"+{card.Amount:0}% bullet size",
        UpgradeType.FasterBullets => $"+{card.Amount:0}% bullet speed",
        UpgradeType.SwiftBoots => $"+{card.Amount:0}% move speed",
        UpgradeType.TrackSpikes => $"+{card.Amount:0} move speed",
        UpgradeType.HigherJumps => $"+{card.Amount:0}% jump power",
        UpgradeType.ThickSkin => $"+{(int)card.Amount} max health",
        UpgradeType.VitalSurge => $"+{card.Amount:0}% max health",
        UpgradeType.HollowPoints => $"+{card.Amount:0.#} damage per hit",
        UpgradeType.HeavyCaliber => $"+{card.Amount:0}% damage per hit",
        UpgradeType.QuickHands => $"{card.Amount:0}% faster reload",
        UpgradeType.SpeedLoader => $"-{card.Amount:0.##}s reload time",
        UpgradeType.IronGuard => $"+{card.Amount:0.#}s block stamina",
        UpgradeType.GuardTraining => $"+{card.Amount:0}% block stamina",
        UpgradeType.Buckshot => $"+{(int)card.Amount} bullet{((int)card.Amount == 1 ? "" : "s")} per shot",
        UpgradeType.ToxicRounds => $"+{card.Amount:0.#} poison dmg/sec for 3s",
        UpgradeType.HomingRounds => $"+{card.Amount:0} deg/s bullet homing",
        UpgradeType.RicochetRounds => $"+{(int)card.Amount} bounce{((int)card.Amount == 1 ? "" : "s")} off walls/floors",
        UpgradeType.ExplosiveRounds => $"+{card.Amount:0} explosion radius on impact",
        UpgradeType.KnockbackForce => $"+{card.Amount:0}% knockback force",
        UpgradeType.CurseWeakness => $"-{card.Amount:0}% damage dealt by every other cow",
        UpgradeType.CurseSlowness => $"-{card.Amount:0}% move speed for every other cow",
        UpgradeType.CurseFragile => $"-{card.Amount:0}% max health for every other cow",
        UpgradeType.CurseShrunkenRounds => $"-{card.Amount:0}% bullet size for every other cow",
        UpgradeType.CurseSlowRounds => $"-{card.Amount:0}% bullet speed for every other cow",
        UpgradeType.CurseEmptyMag => $"-{card.Amount:0}% magazine size for every other cow",
        UpgradeType.CurseJammedGun => $"-{(int)card.Amount} bullet{((int)card.Amount == 1 ? "" : "s")} per shot for every other cow",
        UpgradeType.HealthRegen => $"+{card.Amount:0.#} health regen per sec",
        UpgradeType.ExtraLife => $"+{(int)card.Amount} extra life{((int)card.Amount == 1 ? "" : "s")}",
        UpgradeType.ExtraJump => $"+{(int)card.Amount} extra air jump{((int)card.Amount == 1 ? "" : "s")}",
        UpgradeType.KeenEye => $"+{(int)card.Amount} upgrade card offered in future rounds",
        UpgradeType.GroundPoundRadius => $"+{card.Amount:0} ground pound impact radius",
        UpgradeType.GroundPoundPower => $"+{card.Amount:0}% ground pound damage",
        UpgradeType.CurseGroundPoundRadius => $"-{card.Amount:0}% ground pound impact radius for every other cow",
        UpgradeType.CurseGroundPoundPower => $"-{card.Amount:0}% ground pound damage for every other cow",
        UpgradeType.MegaCurseReset => $"Resets the round leader's {GetName((UpgradeType)(int)card.Amount)} progress back to zero",
        UpgradeType.AoEAura => $"+{card.Amount:0} radius glowing damage aura around you (never hurts you)",
        UpgradeType.AoEAuraSize => $"+{card.Amount:0} damage aura radius",
        UpgradeType.AoEAuraDamage => $"+{card.Amount:0.#} damage aura damage per tick",
        UpgradeType.CurseAoERadius => $"-{card.Amount:0}% damage aura radius for every other cow",
        UpgradeType.CurseAoEDamage => $"-{card.Amount:0}% damage aura damage for every other cow",
        UpgradeType.FlatTrajectory => $"-{card.Amount:0}% bullet drop",
        UpgradeType.CurseHeavyRounds => $"+{card.Amount:0}% bullet drop for every other cow",
        UpgradeType.LifeSteal => $"+{card.Amount:0}% of damage dealt returned as health",
        UpgradeType.CurseLifeSteal => $"-{card.Amount:0}% life steal for every other cow",
        _ => ""
    };

    private static readonly Color IconShadeColor = new Color(24, 24, 28);

    // A small procedural icon for the upgrade card's art panel, built from the same primitives as
    // everything else in the game (no image assets) - `size` is roughly the icon's bounding height.
    public static void DrawIcon(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, UpgradeType type, Vector2 center, float size, Color color)
    {
        switch (type)
        {
            case UpgradeType.ExtraMagazine:
            {
                float w = size * 0.5f, h = size * 0.9f;
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, new Vector2(w, h), w * 0.3f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, new Vector2(w - 10f, h - 10f), (w - 10f) * 0.25f, IconShadeColor);

                float bulletSpacing = (h - 18f) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 p = center + new Vector2(0f, -h / 2f + 12f + i * bulletSpacing);
                    Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, p, new Vector2(w - 20f, bulletSpacing * 0.55f), 4f, color);
                }
                break;
            }

            case UpgradeType.RapidFire:
            {
                Vector2 top = center + new Vector2(size * 0.16f, -size * 0.45f);
                Vector2 mid = center + new Vector2(-size * 0.16f, 0f);
                Vector2 bottom = center + new Vector2(size * 0.1f, size * 0.45f);
                float thickness = size * 0.16f;
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, top, mid, thickness, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, mid, bottom, thickness, color);
                break;
            }

            case UpgradeType.BiggerBullets:
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(size * 0.08f, size * 0.05f), size * 1.15f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-size * 0.34f, size * 0.34f), size * 0.42f, new Color(90, 90, 96));
                break;

            case UpgradeType.FasterBullets:
            {
                Vector2 bulletPos = center + new Vector2(size * 0.24f, 0f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, bulletPos, size * 0.6f, color);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 s = bulletPos + new Vector2(-size * (0.34f + i * 0.2f), 0f);
                    Vector2 e = s + new Vector2(-size * 0.16f, 0f);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, s, e, size * 0.09f, color * (1f - i * 0.28f));
                }
                break;
            }

            case UpgradeType.SwiftBoots:
            {
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.16f), new Vector2(size * 0.34f, size * 0.55f), size * 0.14f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(size * 0.14f, size * 0.34f), new Vector2(size * 0.6f, size * 0.24f), size * 0.1f, color);
                for (int i = 0; i < 2; i++)
                {
                    Vector2 s = center + new Vector2(-size * 0.55f, -size * 0.12f + i * size * 0.2f);
                    Vector2 e = s + new Vector2(size * 0.2f, 0f);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, s, e, size * 0.055f, color * (1f - i * 0.3f));
                }
                break;
            }

            case UpgradeType.HigherJumps:
            {
                Vector2 tip = center + new Vector2(0f, -size * 0.48f);
                Vector2 left = center + new Vector2(-size * 0.3f, -size * 0.04f);
                Vector2 right = center + new Vector2(size * 0.3f, -size * 0.04f);
                float thickness = size * 0.15f;
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, tip, left, thickness, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, tip, right, thickness, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.1f), center + new Vector2(0f, size * 0.42f), thickness, color);
                break;
            }

            case UpgradeType.ThickSkin:
            {
                float r = size * 0.3f;
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, color, MathHelper.PiOver4);
                break;
            }

            case UpgradeType.HollowPoints:
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.9f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.58f, IconShadeColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.24f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(-size * 0.55f, 0f), center + new Vector2(-size * 0.32f, 0f), size * 0.06f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(size * 0.32f, 0f), center + new Vector2(size * 0.55f, 0f), size * 0.06f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.55f), center + new Vector2(0f, -size * 0.32f), size * 0.06f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.32f), center + new Vector2(0f, size * 0.55f), size * 0.06f, color);
                break;

            case UpgradeType.HeavyCaliber:
                // Same target-ring bullet as Hollow Points, with a percentage badge - a percentage
                // sibling to that flat-amount upgrade.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.9f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.58f, IconShadeColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.24f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(-size * 0.55f, 0f), center + new Vector2(-size * 0.32f, 0f), size * 0.06f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(size * 0.32f, 0f), center + new Vector2(size * 0.55f, 0f), size * 0.06f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.55f), center + new Vector2(0f, -size * 0.32f), size * 0.06f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.32f), center + new Vector2(0f, size * 0.55f), size * 0.06f, color);
                DrawPercentBadge(spriteBatch, pixel, softCircle, center, size, color);
                break;

            case UpgradeType.QuickHands:
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.85f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.7f, IconShadeColor);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center, center + new Vector2(0f, -size * 0.3f), size * 0.07f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center, center + new Vector2(size * 0.24f, size * 0.06f), size * 0.07f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.44f), new Vector2(size * 0.2f, size * 0.14f), size * 0.05f, color);
                break;

            case UpgradeType.SpeedLoader:
            {
                // A revolver speed loader - a ring of cartridges dropped in all at once, in contrast
                // to Quick Hands' dexterity-based percentage speedup.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.38f, IconShadeColor);
                const int cartridgeCount = 6;
                for (int i = 0; i < cartridgeCount; i++)
                {
                    float angle = i / (float)cartridgeCount * MathHelper.TwoPi;
                    Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 cartridgeCenter = center + dir * size * 0.62f;
                    Primitives2D.DrawCircle(spriteBatch, softCircle, cartridgeCenter, size * 0.22f, color);
                }
                break;
            }

            case UpgradeType.IronGuard:
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.18f), new Vector2(size * 0.68f, size * 0.52f), size * 0.14f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.28f), new Vector2(size * 0.5f, size * 0.5f), size * 0.06f, color, MathHelper.PiOver4);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.1f), new Vector2(size * 0.4f, size * 0.36f), size * 0.08f, IconShadeColor);
                break;

            case UpgradeType.TrackSpikes:
            {
                // Same boot as Swift Boots, but with cleats under the sole instead of speed trails -
                // a fixed-amount sibling to that percentage upgrade.
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.16f), new Vector2(size * 0.34f, size * 0.55f), size * 0.14f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(size * 0.14f, size * 0.34f), new Vector2(size * 0.6f, size * 0.24f), size * 0.1f, color);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 s = center + new Vector2(-size * 0.1f + i * size * 0.24f, size * 0.46f);
                    Vector2 e = s + new Vector2(0f, size * 0.22f);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, s, e, size * 0.05f, IconShadeColor);
                }
                break;
            }

            case UpgradeType.VitalSurge:
            {
                // Same heart as Thick Skin, with a percentage badge - a percentage sibling to that
                // flat-amount upgrade.
                float r = size * 0.3f;
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, color, MathHelper.PiOver4);
                DrawPercentBadge(spriteBatch, pixel, softCircle, center, size, color);
                break;
            }

            case UpgradeType.GuardTraining:
                // A smaller shield than Iron Guard, with a percentage badge - a percentage sibling to
                // that flat-amount upgrade.
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.18f), new Vector2(size * 0.6f, size * 0.46f), size * 0.13f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.24f), new Vector2(size * 0.44f, size * 0.44f), size * 0.06f, color, MathHelper.PiOver4);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.1f), new Vector2(size * 0.34f, size * 0.3f), size * 0.07f, IconShadeColor);
                DrawPercentBadge(spriteBatch, pixel, softCircle, center, size, color);
                break;

            case UpgradeType.Buckshot:
            {
                // Pellets fanned out from a single origin, evoking a shotgun spread.
                Vector2 origin = center + new Vector2(-size * 0.5f, size * 0.35f);
                const int pelletCount = 4;
                for (int i = 0; i < pelletCount; i++)
                {
                    float t = i / (float)(pelletCount - 1);
                    float angle = MathHelper.Lerp(-0.55f, 0.15f, t);
                    Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pelletPos = origin + dir * size * (0.55f + t * 0.35f);
                    Primitives2D.DrawCircle(spriteBatch, softCircle, pelletPos, size * 0.26f, color);
                }
                break;
            }

            case UpgradeType.ToxicRounds:
            {
                // A round dripping downward, evoking a poison coating melting off the bullet on impact.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(0f, -size * 0.3f), size * 0.7f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-size * 0.14f, -size * 0.42f), size * 0.22f, IconShadeColor);

                for (int i = 0; i < 3; i++)
                {
                    float t = i / 2f;
                    Vector2 dripCenter = center + new Vector2(MathHelper.Lerp(-size * 0.28f, size * 0.3f, t), size * (0.16f + t * 0.35f));
                    float dripSize = size * (0.24f - t * 0.06f);
                    Primitives2D.DrawBlob(spriteBatch, softCircle, dripCenter, new Vector2(dripSize, dripSize * 1.4f), color * (1f - t * 0.25f));
                }
                break;
            }

            case UpgradeType.HomingRounds:
            {
                // A lock-on reticle - a ring with four tick marks and a center dot, evoking auto-aim.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.85f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.6f, IconShadeColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.16f, color);

                for (int i = 0; i < 4; i++)
                {
                    float angle = i / 4f * MathHelper.TwoPi;
                    Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + dir * size * 0.5f, center + dir * size * 0.85f, size * 0.07f, color);
                }
                break;
            }

            case UpgradeType.RicochetRounds:
            {
                // A bullet's path bouncing off a floor line, evoking a ricochet.
                Vector2 floorCenter = center + new Vector2(0f, size * 0.55f);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, floorCenter, new Vector2(size * 1.7f, size * 0.16f), size * 0.06f, IconShadeColor);

                Vector2 start = center + new Vector2(-size * 0.6f, -size * 0.55f);
                Vector2 bouncePoint = center + new Vector2(0f, size * 0.42f);
                Vector2 end = center + new Vector2(size * 0.6f, -size * 0.15f);

                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, start, bouncePoint, size * 0.09f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, bouncePoint, end, size * 0.09f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, bouncePoint, size * 0.22f, color);
                break;
            }

            case UpgradeType.ExplosiveRounds:
            {
                // A burst of jagged rays around a core, evoking a blast radius.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.5f, color);
                const int rayCount = 8;
                for (int i = 0; i < rayCount; i++)
                {
                    float angle = i / (float)rayCount * MathHelper.TwoPi;
                    Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + dir * size * 0.55f, center + dir * size * 0.95f, size * 0.08f, new Color(255, 170, 60));
                }
                break;
            }

            case UpgradeType.KnockbackForce:
            {
                // A hoof striking outward with impact lines trailing behind it, evoking a shove.
                Vector2 hoof = center + new Vector2(size * 0.25f, 0f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, hoof, size * 0.6f, color);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 s = center + new Vector2(-size * (0.3f + i * 0.22f), (i - 1) * size * 0.28f);
                    Vector2 e = s + new Vector2(-size * 0.18f, 0f);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, s, e, size * 0.08f, color * (1f - i * 0.2f));
                }
                break;
            }

            case UpgradeType.CurseWeakness:
                // A faded, hollowed-out version of the Hollow Points/Heavy Caliber target ring, marked
                // with the hex badge to show it drains the target rather than empowering the shooter.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.9f, IconShadeColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.58f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.24f, IconShadeColor);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;

            case UpgradeType.CurseSlowness:
                // Same boot as Swift Boots/Track Spikes, marked as a hex instead of a self-buff.
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.16f), new Vector2(size * 0.34f, size * 0.55f), size * 0.14f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(size * 0.14f, size * 0.34f), new Vector2(size * 0.6f, size * 0.24f), size * 0.1f, color);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;

            case UpgradeType.CurseFragile:
            {
                // Same heart as Thick Skin/Vital Surge, cracked down the middle and hex-badged.
                float r = size * 0.3f;
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, color, MathHelper.PiOver4);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(-size * 0.08f, -size * 0.15f), center + new Vector2(size * 0.14f, size * 0.4f), size * 0.05f, IconShadeColor);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.CurseShrunkenRounds:
                // Same round-and-shadow pair as Bigger Bullets, sized down instead of up, hex-badged.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(size * 0.08f, size * 0.05f), size * 0.55f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-size * 0.34f, size * 0.34f), size * 0.2f, new Color(90, 90, 96));
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;

            case UpgradeType.CurseSlowRounds:
            {
                // Same trailing-bullet motif as Hot Loads, with the trail reversed to read as
                // dragging/slowing rather than speeding up, hex-badged.
                Vector2 bulletPos = center + new Vector2(-size * 0.24f, 0f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, bulletPos, size * 0.5f, color);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 s = bulletPos + new Vector2(size * (0.34f + i * 0.2f), 0f);
                    Vector2 e = s + new Vector2(size * 0.1f, 0f);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, s, e, size * 0.06f, color * (1f - i * 0.28f));
                }
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.CurseEmptyMag:
            {
                // Same magazine silhouette as Extended Mag, with only one round left inside, hex-badged.
                float w = size * 0.5f, h = size * 0.9f;
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, new Vector2(w, h), w * 0.3f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, new Vector2(w - 10f, h - 10f), (w - 10f) * 0.25f, IconShadeColor);
                Vector2 lastRound = center + new Vector2(0f, h / 2f - 16f);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, lastRound, new Vector2(w - 20f, (h - 18f) / 3f * 0.55f), 4f, color);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.CurseJammedGun:
            {
                // Same fanned pellets as Buckshot, one greyed out to read as a dud round, hex-badged.
                Vector2 origin = center + new Vector2(-size * 0.5f, size * 0.35f);
                const int pelletCount = 4;
                for (int i = 0; i < pelletCount; i++)
                {
                    float t = i / (float)(pelletCount - 1);
                    float angle = MathHelper.Lerp(-0.55f, 0.15f, t);
                    Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Vector2 pelletPos = origin + dir * size * (0.55f + t * 0.35f);
                    Primitives2D.DrawCircle(spriteBatch, softCircle, pelletPos, size * 0.26f, i == pelletCount - 1 ? IconShadeColor : color);
                }
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.HealthRegen:
            {
                // Same heart as Thick Skin/Vital Surge, with a plus-cross badge instead of a
                // percent/hex one - marks it as healing over time rather than an instant boost.
                float r = size * 0.3f;
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, color, MathHelper.PiOver4);
                DrawPlusBadge(spriteBatch, pixel, softCircle, center, size, color);
                break;
            }

            case UpgradeType.ExtraJump:
            {
                // Two stacked chevrons rising above a hoofprint - a second bounce off thin air.
                for (int i = 0; i < 2; i++)
                {
                    Vector2 apex = center + new Vector2(0f, -size * (0.15f + i * 0.42f));
                    Vector2 left = apex + new Vector2(-size * 0.28f, size * 0.22f);
                    Vector2 right = apex + new Vector2(size * 0.28f, size * 0.22f);
                    float thickness = size * 0.13f;
                    Color chevronColor = i == 0 ? color : color * 0.55f;
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, apex, left, thickness, chevronColor);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, apex, right, thickness, chevronColor);
                }
                Primitives2D.DrawBlob(spriteBatch, softCircle, center + new Vector2(0f, size * 0.7f), new Vector2(size * 0.5f, size * 0.18f), IconShadeColor);
                break;
            }

            case UpgradeType.KeenEye:
            {
                // Three fanned card-backs, evoking a wider hand of options to choose from.
                for (int i = 0; i < 3; i++)
                {
                    float angle = (i - 1) * 0.35f;
                    Vector2 offset = new Vector2(MathF.Sin(angle) * size * 0.35f, -MathF.Cos(angle) * size * 0.1f);
                    Color cardColor = i == 1 ? color : color * 0.55f;
                    Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + offset, new Vector2(size * 0.5f, size * 0.85f), size * 0.1f, cardColor, angle);
                }
                break;
            }

            case UpgradeType.GroundPoundRadius:
            {
                // A downward arrow driving into an impact glow on the ground, evoking blast radius.
                Vector2 groundY = center + new Vector2(0f, size * 0.55f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, groundY, size * 1.3f, color * 0.35f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, groundY, size * 0.75f, color * 0.6f);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, groundY, new Vector2(size * 1.6f, size * 0.14f), size * 0.06f, IconShadeColor);
                Vector2 arrowTip = groundY + new Vector2(0f, -size * 0.1f);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size * 0.6f), arrowTip, size * 0.16f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, arrowTip, arrowTip + new Vector2(-size * 0.22f, -size * 0.28f), size * 0.13f, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, arrowTip, arrowTip + new Vector2(size * 0.22f, -size * 0.28f), size * 0.13f, color);
                break;
            }

            case UpgradeType.GroundPoundPower:
            {
                // Same downward hoof-strike as Bull Rush, with a percent badge for the %-scaled damage.
                Vector2 hoof = center + new Vector2(0f, size * 0.25f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, hoof, size * 0.6f, color);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 s = center + new Vector2((i - 1) * size * 0.28f, -size * (0.3f + i * 0.22f));
                    Vector2 e = s + new Vector2(0f, -size * 0.18f);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, s, e, size * 0.08f, color * (1f - i * 0.2f));
                }
                DrawPercentBadge(spriteBatch, pixel, softCircle, center, size, color);
                break;
            }

            case UpgradeType.CurseGroundPoundRadius:
            {
                // Faded version of Seismic Hooves' impact glow, hex-badged.
                Vector2 groundY = center + new Vector2(0f, size * 0.55f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, groundY, size * 1.1f, IconShadeColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, groundY, size * 0.6f, color * 0.5f);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, groundY, new Vector2(size * 1.6f, size * 0.14f), size * 0.06f, IconShadeColor);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.CurseGroundPoundPower:
            {
                // Faded version of Heavy Hooves' hoof strike, hex-badged.
                Vector2 hoof = center + new Vector2(0f, size * 0.25f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, hoof, size * 0.6f, color * 0.6f);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.MegaCurseReset:
            {
                // A cracked hourglass - the leader's progress on one stat draining back to zero.
                Vector2 topCenter = center + new Vector2(0f, -size * 0.55f);
                Vector2 bottomCenter = center + new Vector2(0f, size * 0.55f);
                float thickness = size * 0.1f;

                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, topCenter + new Vector2(-size * 0.4f, 0f), center, thickness, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, topCenter + new Vector2(size * 0.4f, 0f), center, thickness, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center, bottomCenter + new Vector2(-size * 0.4f, 0f), thickness, color);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center, bottomCenter + new Vector2(size * 0.4f, 0f), thickness, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, topCenter, new Vector2(size * 0.9f, size * 0.14f), size * 0.05f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, bottomCenter, new Vector2(size * 0.9f, size * 0.14f), size * 0.05f, color);

                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(-size * 0.15f, -size * 0.25f), center + new Vector2(size * 0.1f, 0f), size * 0.05f, IconShadeColor);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + new Vector2(size * 0.1f, 0f), center + new Vector2(-size * 0.1f, size * 0.25f), size * 0.05f, IconShadeColor);

                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.AoEAura:
                // Concentric glowing rings around a small figure - a damage aura radiating outward.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 1.3f, color * 0.3f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.85f, color * 0.55f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.32f, IconShadeColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.2f, color);
                break;

            case UpgradeType.AoEAuraSize:
            {
                // Same aura rings as Searing Aura, with outward arrows marking the size increase.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.85f, color * 0.4f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.32f, IconShadeColor);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i / 4f * MathHelper.TwoPi + MathHelper.PiOver4;
                    Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, center + dir * size * 0.55f, center + dir * size * 0.95f, size * 0.07f, color);
                }
                break;
            }

            case UpgradeType.AoEAuraDamage:
                // Same aura rings as Searing Aura, with a small flame at the center for the damage boost.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.85f, color * 0.4f);
                Primitives2D.DrawBlob(spriteBatch, softCircle, center + new Vector2(0f, size * 0.1f), new Vector2(size * 0.4f, size * 0.55f), new Color(255, 140, 40));
                Primitives2D.DrawBlob(spriteBatch, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.2f, size * 0.3f), new Color(255, 220, 120));
                break;

            case UpgradeType.CurseAoERadius:
                // Faded version of Searing Aura's rings, hex-badged.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.9f, IconShadeColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.5f, color * 0.4f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.2f, IconShadeColor);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;

            case UpgradeType.CurseAoEDamage:
                // A snuffed-out version of Scorching Aura's flame, hex-badged.
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.7f, color * 0.35f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center, size * 0.2f, IconShadeColor);
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;

            case UpgradeType.FlatTrajectory:
            {
                // A bullet trailing a flat, level dashed line - a straight, predictable arc.
                Vector2 bulletPos = center + new Vector2(size * 0.5f, 0f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, bulletPos, size * 0.35f, color);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 s = bulletPos + new Vector2(-size * (0.55f + i * 0.35f), 0f);
                    Vector2 e = s + new Vector2(-size * 0.22f, 0f);
                    Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, s, e, size * 0.07f, color * (1f - i * 0.25f));
                }
                break;
            }

            case UpgradeType.CurseHeavyRounds:
            {
                // Same bullet, dropping fast along a steep dashed arc instead of flying flat, hex-badged.
                Vector2 bulletPos = center + new Vector2(size * 0.5f, size * 0.35f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, bulletPos, size * 0.3f, color);
                for (int i = 0; i < 3; i++)
                {
                    float t = (i + 1) / 3f;
                    Vector2 dot = bulletPos + new Vector2(-size * 1.1f * t, -size * 0.6f * t * t);
                    Primitives2D.DrawCircle(spriteBatch, softCircle, dot, size * 0.14f * (1f - t * 0.4f), color * (1f - t * 0.3f));
                }
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.LifeSteal:
            {
                // Same heart as Thick Skin/Vital Surge, with a drop of drained health flowing into it.
                float lifeStealHeartRadius = size * 0.3f;
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-lifeStealHeartRadius * 0.62f, -size * 0.16f), lifeStealHeartRadius * 2f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(lifeStealHeartRadius * 0.62f, -size * 0.16f), lifeStealHeartRadius * 2f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, color, MathHelper.PiOver4);

                Vector2 dropStart = center + new Vector2(size * 0.9f, -size * 0.75f);
                Vector2 dropEnd = center + new Vector2(size * 0.15f, -size * 0.05f);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, dropStart, dropEnd, size * 0.08f, new Color(210, 40, 60));
                Primitives2D.DrawCircle(spriteBatch, softCircle, dropStart, size * 0.16f, new Color(210, 40, 60));
                break;
            }

            case UpgradeType.CurseLifeSteal:
            {
                // Same heart as Vampiric Bite, drained/faded, with the drop flowing OUT instead of in, hex-badged.
                float r2 = size * 0.3f;
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-r2 * 0.62f, -size * 0.16f), r2 * 2f, color * 0.5f);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(r2 * 0.62f, -size * 0.16f), r2 * 2f, color * 0.5f);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, color * 0.5f, MathHelper.PiOver4);

                Vector2 dropStart = center + new Vector2(size * 0.15f, -size * 0.05f);
                Vector2 dropEnd = center + new Vector2(size * 0.9f, -size * 0.75f);
                Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, dropStart, dropEnd, size * 0.07f, new Color(140, 140, 145));
                Primitives2D.DrawCircle(spriteBatch, softCircle, dropEnd, size * 0.13f, new Color(140, 140, 145));
                DrawHexBadge(spriteBatch, pixel, softCircle, center, size);
                break;
            }

            case UpgradeType.ExtraLife:
            {
                // A second, fainter heart tucked behind the first - a spare life held in reserve.
                float r = size * 0.3f;
                Vector2 shadowCenter = center + new Vector2(size * 0.22f, size * 0.22f);
                Color shadowColor = color * 0.4f;
                Primitives2D.DrawCircle(spriteBatch, softCircle, shadowCenter + new Vector2(-r * 0.62f, -size * 0.16f), r * 2f, shadowColor);
                Primitives2D.DrawCircle(spriteBatch, softCircle, shadowCenter + new Vector2(r * 0.62f, -size * 0.16f), r * 2f, shadowColor);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, shadowCenter + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, shadowColor, MathHelper.PiOver4);

                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(-r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawCircle(spriteBatch, softCircle, center + new Vector2(r * 0.62f, -size * 0.16f), r * 2f, color);
                Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size * 0.2f), new Vector2(size * 0.64f, size * 0.64f), size * 0.1f, color, MathHelper.PiOver4);
                break;
            }
        }
    }

    // A small badge (dark disc + upward chevron) marking an icon as a percentage-based upgrade, to
    // visually distinguish it from its flat-amount sibling.
    private static void DrawPercentBadge(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 iconCenter, float size, Color color)
    {
        Vector2 badgeCenter = iconCenter + new Vector2(size * 0.62f, -size * 0.62f);
        Primitives2D.DrawCircle(spriteBatch, softCircle, badgeCenter, size * 0.36f, IconShadeColor);

        Vector2 tip = badgeCenter + new Vector2(0f, -size * 0.12f);
        Vector2 left = badgeCenter + new Vector2(-size * 0.1f, size * 0.07f);
        Vector2 right = badgeCenter + new Vector2(size * 0.1f, size * 0.07f);
        float thickness = size * 0.06f;
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, tip, left, thickness, color);
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, tip, right, thickness, color);
    }

    // A small badge (dark disc + plus sign) marking a heart icon as heal-over-time, distinguishing
    // it from the instant flat/percent health upgrades that share the same heart shape.
    private static void DrawPlusBadge(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 iconCenter, float size, Color color)
    {
        Vector2 badgeCenter = iconCenter + new Vector2(size * 0.62f, -size * 0.62f);
        Primitives2D.DrawCircle(spriteBatch, softCircle, badgeCenter, size * 0.36f, IconShadeColor);

        float half = size * 0.12f;
        float thickness = size * 0.06f;
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, badgeCenter + new Vector2(-half, 0f), badgeCenter + new Vector2(half, 0f), thickness, color);
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, badgeCenter + new Vector2(0f, -half), badgeCenter + new Vector2(0f, half), thickness, color);
    }

    private static readonly Color CurseBadgeColor = new Color(40, 10, 45);
    private static readonly Color CurseChevronColor = new Color(210, 60, 220);

    // A small badge (dark disc + downward chevron) marking a card as a curse - it hexes every other
    // player instead of upgrading the picker - so its effect on the field reads clearly at a glance.
    private static void DrawHexBadge(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 iconCenter, float size)
    {
        Vector2 badgeCenter = iconCenter + new Vector2(size * 0.62f, -size * 0.62f);
        Primitives2D.DrawCircle(spriteBatch, softCircle, badgeCenter, size * 0.36f, CurseBadgeColor);

        Vector2 tip = badgeCenter + new Vector2(0f, size * 0.12f);
        Vector2 left = badgeCenter + new Vector2(-size * 0.1f, -size * 0.07f);
        Vector2 right = badgeCenter + new Vector2(size * 0.1f, -size * 0.07f);
        float thickness = size * 0.06f;
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, tip, left, thickness, CurseChevronColor);
        Primitives2D.DrawCapsule(spriteBatch, pixel, softCircle, tip, right, thickness, CurseChevronColor);
    }
}
