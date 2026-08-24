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

// A horizontal platform that slides back and forth between two X positions. Height/Thickness stay
// fixed (only StartX/EndX shift), which keeps every existing height-based collision check correct
// unchanged - only the carry logic (see Level.GetCarryDeltaX) needs to know it's mobile.
public class MovingPlatform
{
    public readonly float Height;
    public readonly float Thickness;
    public readonly float Width;
    public readonly float RangeStart;
    public readonly float RangeEnd;
    public readonly float Speed;

    private float _centerX;
    private int _direction;

    public MovingPlatform(float centerX, float width, float height, float thickness, float rangeStart, float rangeEnd, float speed, int direction)
    {
        _centerX = centerX;
        Width = width;
        Height = height;
        Thickness = thickness;
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
        Speed = speed;
        _direction = direction;
    }

    public float StartX => _centerX - Width / 2f;
    public float EndX => _centerX + Width / 2f;

    // How far this platform moved this frame - added to a rider's Position.X so standing on it
    // carries you along instead of leaving you sliding relative to it. See Level.GetCarryDeltaX.
    public float DeltaX { get; private set; }

    public void Update(float delta)
    {
        float previousCenterX = _centerX;
        _centerX += _direction * Speed * delta;

        if (_centerX > RangeEnd)
        {
            _centerX = RangeEnd;
            _direction = -1;
        }
        else if (_centerX < RangeStart)
        {
            _centerX = RangeStart;
            _direction = 1;
        }

        DeltaX = _centerX - previousCenterX;
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

    private const int MinMovingPlatformCount = 0;
    private const int MaxMovingPlatformCount = 3;
    private const float MovingPlatformWidth = 140f;
    private const float MovingPlatformThickness = 20f;
    private const float MovingPlatformMinSpeed = 60f;
    private const float MovingPlatformMaxSpeed = 140f;
    private const float MovingPlatformMinTravel = 150f;
    private const float MovingPlatformMaxTravel = 400f;

    // How close a rider's feet need to be to a moving platform's surface to be carried by it - see
    // GetCarryDeltaX. Wide enough to absorb a frame's worth of platform motion at MovingPlatformMaxSpeed.
    private const float CarryEpsilon = 6f;

    private readonly List<Platform> _platforms = new();
    private readonly List<Wall> _walls = new();
    private readonly List<MovingPlatform> _movingPlatforms = new();

    public Terrain Ground { get; }
    public IReadOnlyList<Platform> Platforms => _platforms;
    public IReadOnlyList<Wall> Walls => _walls;
    public IReadOnlyList<MovingPlatform> MovingPlatforms => _movingPlatforms;

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
        _walls.AddRange(BuildBoundaryWalls(baseGroundHeight, topHeight, width));
        _movingPlatforms.AddRange(BuildMovingPlatforms(baseGroundHeight, topHeight, width, random));
    }

    // Advances every moving platform - call once per frame before updating anything that might be
    // standing on one, so riders see this frame's position/DeltaX rather than last frame's.
    public void Update(float delta)
    {
        foreach (var platform in _movingPlatforms)
            platform.Update(delta);
    }

    public float GetGroundHeightAt(float x) => Ground.GetHeightAt(x);

    // How far a moving platform under this point moved this frame, or 0 if the point isn't
    // standing on one - added to a grounded entity's Position.X so it rides along instead of
    // sliding relative to the platform. See Stickman.Update and Box.Update.
    public float GetCarryDeltaX(float x, float y)
    {
        foreach (var platform in _movingPlatforms)
        {
            if (x < platform.StartX - CarryEpsilon || x > platform.EndX + CarryEpsilon)
                continue;
            if (MathF.Abs(y - platform.Height) > CarryEpsilon)
                continue;

            return platform.DeltaX;
        }

        return 0f;
    }

    // Yields (StartX, EndX, Height, Thickness) for every solid platform span - static tiers plus
    // moving platforms at their current position - so the collision queries below treat both alike.
    private IEnumerable<(float StartX, float EndX, float Height, float Thickness)> AllPlatformSpans()
    {
        foreach (var platform in _platforms)
            yield return (platform.StartX, platform.EndX, platform.Height, platform.Thickness);

        foreach (var platform in _movingPlatforms)
            yield return (platform.StartX, platform.EndX, platform.Height, platform.Thickness);
    }

    // Nearest solid surface reached by falling from previousY to candidateY at this X. An elevated tier
    // only counts if this frame's fall actually crossed its surface - otherwise walking underneath one
    // (where candidateY is already far below it) would wrongly count as having "reached" it.
    public float GetLandingHeightAt(float x, float previousY, float candidateY, bool falling)
    {
        float landing = Ground.GetHeightAt(x);

        if (!falling)
            return landing;

        foreach (var platform in AllPlatformSpans())
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
        foreach (var platform in AllPlatformSpans())
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

        foreach (var platform in AllPlatformSpans())
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

    // Whether a single point (e.g. a gun barrel tip) currently lies inside any wall - used to stop a
    // shot from firing when the muzzle is poking out through the far side of a wall.
    public bool IsPointInsideAnyWall(Vector2 point)
    {
        foreach (var wall in _walls)
        {
            if (wall.GetBounds().Contains(point))
                return true;
        }

        return false;
    }

    // Whether a wall stands between two points - used to stop splash/AoE damage (explosions, ground
    // pound, the AoE aura) from reaching through solid walls the way it currently reaches through
    // floors/platforms unchecked (those are handled separately, this is walls only).
    public bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        foreach (var wall in _walls)
        {
            if (SegmentIntersectsRect(from, to, wall.GetBounds()))
                return false;
        }

        return true;
    }

    // Standard slab-method segment-vs-AABB intersection test.
    private static bool SegmentIntersectsRect(Vector2 from, Vector2 to, Rectangle rect)
    {
        float tMin = 0f, tMax = 1f;
        Vector2 delta = to - from;

        if (!ClipSegment(from.X, delta.X, rect.Left, rect.Right, ref tMin, ref tMax))
            return false;
        if (!ClipSegment(from.Y, delta.Y, rect.Top, rect.Bottom, ref tMin, ref tMax))
            return false;

        return true;
    }

    private static bool ClipSegment(float origin, float direction, float boundsMin, float boundsMax, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < 0.0001f)
            return origin >= boundsMin && origin <= boundsMax;

        float t0 = (boundsMin - origin) / direction;
        float t1 = (boundsMax - origin) / direction;
        if (t0 > t1)
            (t0, t1) = (t1, t0);

        tMin = MathF.Max(tMin, t0);
        tMax = MathF.Min(tMax, t1);
        return tMin <= tMax;
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

    // Two always-on climbable walls right at the screen edges - since arena width varies round to
    // round (see Game1's map-size variance) while the ground/background still render full-screen,
    // these are what visually communicates where the usable area actually ends. Kept thin and
    // anchored to x=0/width (rather than at Game1's minX/maxX movement clamp, ~40px further in) so
    // a clamped player's body never overlaps one - there's no player-geometry constant in Level to
    // check against, so staying comfortably clear of that margin avoids any wall/clamp fighting.
    private static List<Wall> BuildBoundaryWalls(float baseGroundHeight, float topHeight, float width)
    {
        const float boundaryThickness = 16f;
        float top = topHeight - 60f;
        float bottom = baseGroundHeight + 50f;

        return new List<Wall>
        {
            new Wall(boundaryThickness / 2f, boundaryThickness, top, bottom),
            new Wall(width - boundaryThickness / 2f, boundaryThickness, top, bottom)
        };
    }

    private static List<MovingPlatform> BuildMovingPlatforms(float baseGroundHeight, float topHeight, float width, Random random)
    {
        var platforms = new List<MovingPlatform>();
        int count = random.Next(MinMovingPlatformCount, MaxMovingPlatformCount + 1);

        float maxTravel = MathF.Max(80f, width - MovingPlatformWidth * 2f - 80f);
        float travelHigh = MathF.Min(MovingPlatformMaxTravel, maxTravel);
        float travelLow = MathF.Min(MovingPlatformMinTravel, travelHigh);

        for (int i = 0; i < count; i++)
        {
            float travel = MathHelper.Lerp(travelLow, travelHigh, (float)random.NextDouble());

            float rangeStart = MathHelper.Lerp(MovingPlatformWidth, MathF.Max(MovingPlatformWidth, width - MovingPlatformWidth - travel), (float)random.NextDouble());
            float rangeEnd = rangeStart + travel;

            float height = MathHelper.Lerp(topHeight + 40f, baseGroundHeight - 60f, (float)random.NextDouble());
            float speed = MathHelper.Lerp(MovingPlatformMinSpeed, MovingPlatformMaxSpeed, (float)random.NextDouble());
            float centerX = MathHelper.Lerp(rangeStart, rangeEnd, (float)random.NextDouble());
            int direction = random.Next(2) == 0 ? 1 : -1;

            platforms.Add(new MovingPlatform(centerX, MovingPlatformWidth, height, MovingPlatformThickness, rangeStart, rangeEnd, speed, direction));
        }

        return platforms;
    }
}
