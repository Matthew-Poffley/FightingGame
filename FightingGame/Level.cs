using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FightingGame;

public readonly struct Platform
{
    public readonly float StartX;
    public readonly float EndX;
    public readonly float Height;
    public readonly float Thickness;

    public Platform(float startX, float endX, float height, float thickness)
    {
        StartX = startX;
        EndX = endX;
        Height = height;
        Thickness = thickness;
    }
}

public readonly struct Wall
{
    public readonly float X;
    public readonly float Thickness;
    public readonly float Top;
    public readonly float Bottom;

    public Wall(float x, float thickness, float top, float bottom)
    {
        X = x;
        Thickness = thickness;
        Top = top;
        Bottom = bottom;
    }

    public Rectangle GetBounds() =>
        new Rectangle((int)(X - Thickness / 2f), (int)Top, (int)Thickness, (int)(Bottom - Top));
}

// The undulating ground (always solid) plus a random number of elevated solid tiers with gaps in
// them, and a few solid climbing walls. Gaps let you fall through to whatever is below; the only way
// up through a solid tier is aligning a jump with one of its gaps, or wall-hopping past it.
public class Level
{
    private const int MinTierCount = 0;
    private const int MaxTierCount = 5;

    // Kept large enough that even the worst-case combination of tier spacing, segment jitter, and the
    // ground's own wave amplitude still leaves clear room for a standing player (~94px tall).
    private const float MinTierSpacing = 200f;
    private const float MaxTierSpacing = 300f;
    private const float SegmentHeightJitter = 15f;

    // The first tier always uses this narrower, guaranteed-jumpable range (still respecting the same
    // clearance safety margin) so there's always at least one platform reachable by a plain jump.
    private const float Tier1SpacingMin = 225f;
    private const float Tier1SpacingMax = 250f;

    private const int MinGapsPerTier = 1;
    private const int MaxGapsPerTier = 4;
    private const float AbsoluteMinGapWidth = 150f; // comfortably wider than a stickman (~32px across)
    private const float AbsoluteMaxGapWidth = 400f;

    private const float AbsoluteMinPlatformThickness = 8f;
    private const float AbsoluteMaxPlatformThickness = 48f;

    private const int MinWallCount = 1;
    private const int MaxWallCount = 5;
    private const float MinWallThickness = 14f;
    private const float MaxWallThickness = 32f;

    private readonly List<Platform> _platforms = new();
    private readonly List<Wall> _walls = new();

    public Terrain Ground { get; }
    public IReadOnlyList<Platform> Platforms => _platforms;
    public IReadOnlyList<Wall> Walls => _walls;

    public Level(float baseGroundHeight, float width, Random random)
    {
        Ground = new Terrain(baseGroundHeight, random);

        int tierCount = random.Next(MinTierCount, MaxTierCount + 1);

        // Pick this round's "personality" once so every tier shares a look - some rounds end up mostly
        // open with a few thin ledges, others end up chunky and cramped. Big swing round to round.
        float gapWidthLow = MathHelper.Lerp(AbsoluteMinGapWidth, AbsoluteMaxGapWidth - 80f, (float)random.NextDouble());
        float gapWidthHigh = MathHelper.Lerp(gapWidthLow + 40f, AbsoluteMaxGapWidth, (float)random.NextDouble());

        float thicknessLow = MathHelper.Lerp(AbsoluteMinPlatformThickness, AbsoluteMaxPlatformThickness - 15f, (float)random.NextDouble());
        float thicknessHigh = MathHelper.Lerp(thicknessLow + 8f, AbsoluteMaxPlatformThickness, (float)random.NextDouble());

        float height = baseGroundHeight;
        float topHeight = baseGroundHeight;

        for (int tier = 1; tier <= tierCount; tier++)
        {
            float spacing = tier == 1
                ? MathHelper.Lerp(Tier1SpacingMin, Tier1SpacingMax, (float)random.NextDouble())
                : MathHelper.Lerp(MinTierSpacing, MaxTierSpacing, (float)random.NextDouble());
            height -= spacing;
            topHeight = height;

            int gapCount = random.Next(MinGapsPerTier, MaxGapsPerTier + 1);
            _platforms.AddRange(BuildTier(height, width, gapCount, gapWidthLow, gapWidthHigh, thicknessLow, thicknessHigh, random));
        }

        _walls.AddRange(BuildWalls(baseGroundHeight, topHeight, width, random));
    }

    public float GetGroundHeightAt(float x) => Ground.GetHeightAt(x);

    // Nearest solid surface reached by falling from previousY to candidateY at this X. An elevated tier
    // only counts if this frame's fall actually crossed its surface - otherwise walking underneath one
    // (where candidateY is already far below it) would wrongly count as having "reached" it.
    public float GetLandingHeightAt(float x, float previousY, float candidateY, bool falling)
    {
        float landing = Ground.GetHeightAt(x);

        if (!falling)
            return landing;

        foreach (var platform in _platforms)
        {
            if (x < platform.StartX || x > platform.EndX)
                continue;

            bool crossedThisFrame = previousY <= platform.Height && candidateY >= platform.Height;
            if (crossedThisFrame && platform.Height < landing)
                landing = platform.Height;
        }

        return landing;
    }

    // Nearest platform underside crossed while rising from previousY to candidateY at this X, or null if
    // none (open air or a gap). This is what stops a jump from punching straight through a solid tier -
    // the only ways up are through a gap or by wall-hopping.
    public float? GetCeilingHeightAt(float x, float previousY, float candidateY, bool rising)
    {
        if (!rising)
            return null;

        float? ceiling = null;
        foreach (var platform in _platforms)
        {
            if (x < platform.StartX || x > platform.EndX)
                continue;

            float bottom = platform.Height + platform.Thickness;
            bool crossedThisFrame = previousY >= bottom && candidateY <= bottom;
            if (crossedThisFrame && (ceiling == null || bottom > ceiling.Value))
                ceiling = bottom;
        }

        return ceiling;
    }

    // Whether a bullet moving from previousPosition to newPosition this frame would have punched
    // through the ground or an elevated platform. Unlike player landing, this blocks from either side.
    public bool IsBulletBlockedByTerrain(Vector2 previousPosition, Vector2 newPosition)
    {
        if (newPosition.Y >= Ground.GetHeightAt(newPosition.X))
            return true;

        foreach (var platform in _platforms)
        {
            if (newPosition.X < platform.StartX || newPosition.X > platform.EndX)
                continue;

            bool crossedDown = previousPosition.Y < platform.Height && newPosition.Y >= platform.Height;
            bool crossedUp = previousPosition.Y > platform.Height && newPosition.Y <= platform.Height;
            if (crossedDown || crossedUp)
                return true;
        }

        return false;
    }

    // Pushes position out of any overlapping wall. Returns true if a wall was touched (used to
    // refresh a stickman's ability to jump again, letting it "hop" up a wall).
    public bool ResolveWallCollision(ref Vector2 position, float halfWidth, float height)
    {
        float top = position.Y - height;
        float bottom = position.Y;
        bool touched = false;

        foreach (var wall in _walls)
        {
            float wallLeft = wall.X - wall.Thickness / 2f;
            float wallRight = wall.X + wall.Thickness / 2f;

            bool verticalOverlap = top < wall.Bottom && bottom > wall.Top;
            bool horizontalOverlap = position.X + halfWidth > wallLeft && position.X - halfWidth < wallRight;
            if (!verticalOverlap || !horizontalOverlap)
                continue;

            float pushLeft = (position.X + halfWidth) - wallLeft;
            float pushRight = wallRight - (position.X - halfWidth);

            if (pushLeft < pushRight)
                position.X -= pushLeft;
            else
                position.X += pushRight;

            touched = true;
        }

        return touched;
    }

    private static List<Platform> BuildTier(
        float nominalHeight, float width, int gapCount,
        float gapWidthLow, float gapWidthHigh, float thicknessLow, float thicknessHigh, Random random)
    {
        var gaps = new List<(float start, float end)>();
        float zoneWidth = width / (gapCount + 1);

        for (int i = 0; i < gapCount; i++)
        {
            float zoneStart = zoneWidth * (i + 1) - zoneWidth * 0.3f;
            float zoneEnd = zoneWidth * (i + 1) + zoneWidth * 0.3f;
            float center = MathHelper.Lerp(zoneStart, zoneEnd, (float)random.NextDouble());
            float gapWidth = MathHelper.Lerp(gapWidthLow, gapWidthHigh, (float)random.NextDouble());
            gaps.Add((center - gapWidth / 2f, center + gapWidth / 2f));
        }

        gaps.Sort((a, b) => a.start.CompareTo(b.start));

        var platforms = new List<Platform>();
        float cursor = 0f;
        foreach (var gap in gaps)
        {
            if (gap.start > cursor)
                platforms.Add(MakeSegment(cursor, gap.start, nominalHeight, thicknessLow, thicknessHigh, random));
            cursor = MathF.Max(cursor, gap.end);
        }

        if (cursor < width)
            platforms.Add(MakeSegment(cursor, width, nominalHeight, thicknessLow, thicknessHigh, random));

        return platforms;
    }

    private static Platform MakeSegment(float start, float end, float nominalHeight, float thicknessLow, float thicknessHigh, Random random)
    {
        float height = nominalHeight + ((float)random.NextDouble() - 0.5f) * 2f * SegmentHeightJitter;
        float thickness = MathHelper.Lerp(thicknessLow, thicknessHigh, (float)random.NextDouble());
        return new Platform(start, end, height, thickness);
    }

    private static List<Wall> BuildWalls(float baseGroundHeight, float topHeight, float width, Random random)
    {
        var walls = new List<Wall>();
        int wallCount = random.Next(MinWallCount, MaxWallCount + 1);

        for (int i = 0; i < wallCount; i++)
        {
            float x = MathHelper.Lerp(120f, width - 120f, (float)random.NextDouble());
            float thickness = MathHelper.Lerp(MinWallThickness, MaxWallThickness, (float)random.NextDouble());
            float top = MathHelper.Lerp(topHeight, baseGroundHeight - 60f, (float)random.NextDouble());
            float bottom = baseGroundHeight + 50f;
            walls.Add(new Wall(x, thickness, top, bottom));
        }

        return walls;
    }
}
