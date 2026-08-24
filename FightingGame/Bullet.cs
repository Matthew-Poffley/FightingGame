using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

public class Bullet
{
    private const float BaseSpeed = 900f;
    private const float BaseRadius = 5f;
    private const float Gravity = 350f;

    // A bullet can't hit its own shooter for a brief moment after firing - just long enough to clear
    // the muzzle - so a shot fired straight up can still fall back down and hit them once it returns.
    private const float SelfHitGraceDuration = 0.15f;

    // A bullet can't collide with terrain/walls/boxes/barrels until it's moved this far clear of its
    // own spawn point (scaled by its own radius) - otherwise a big BiggerBullets round can spawn
    // already overlapping the ground/a nearby wall/prop's bounding box and die on the very first
    // frame, which reads as "instantly hitting the ground" the bigger the bullet gets. Doesn't gate
    // hitting other players - point-blank shots should still land.
    private const float SpawnClearanceBuffer = 20f;

    private static readonly Vector2 HorizontalSurfaceNormal = new Vector2(0f, 1f);
    private static readonly Vector2 VerticalSurfaceNormal = new Vector2(1f, 0f);

    private float _age;
    private readonly float _radius;
    private readonly float _gravityMultiplier;
    private readonly float _homingTurnRate; // radians/sec the bullet can curve toward its target
    private readonly Vector2 _spawnPosition;
    private int _bouncesRemaining;

    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Direction { get; private set; }
    public Color Color { get; private set; }
    public Stickman Owner { get; private set; }
    public bool IsAlive = true;

    // Set true only when the bullet dies by punching into solid terrain/a wall (and has no bounces
    // left) - not when it simply drifts off-screen - so Explosive Rounds only detonates on real impacts.
    public bool HitSolid { get; private set; }

    // True only for the single Update() call in which the bullet bounced (Ricochet Rounds) off a
    // wall/floor/platform - lets Game1 detonate Explosive Rounds at each bounce point, not just the
    // bullet's final impact.
    public bool BouncedThisFrame { get; private set; }

    public float ExplosionRadius { get; }

    public bool CanHitOwner => _age >= SelfHitGraceDuration;

    // See SpawnClearanceBuffer - Game1.ResolveBulletHits checks this before letting the bullet hit
    // a box/barrel, mirroring the terrain/wall grace already applied inside Update() below.
    public bool HasClearedSpawn => Vector2.DistanceSquared(Position, _spawnPosition) >= MathF.Pow(_radius + SpawnClearanceBuffer, 2f);

    public Bullet(Vector2 position, Vector2 direction, Color color, Stickman owner, float speedMultiplier = 1f, float radiusMultiplier = 1f, float homingTurnRate = 0f, int bounces = 0, float explosionRadius = 0f, float gravityMultiplier = 1f)
    {
        Position = position;
        _spawnPosition = position;
        Direction = direction;
        Velocity = direction * BaseSpeed * speedMultiplier;
        _radius = BaseRadius * radiusMultiplier;
        _gravityMultiplier = gravityMultiplier;
        _homingTurnRate = homingTurnRate;
        _bouncesRemaining = bounces;
        ExplosionRadius = explosionRadius;
        Color = color;
        Owner = owner;
    }

    // A block sends the bullet straight back the way it came, now belonging to whoever deflected it -
    // so it can hit the original shooter, and the deflector gets a fresh grace period against it.
    public void Deflect(Stickman newOwner, Color newColor)
    {
        Velocity = -Velocity;
        if (Velocity.LengthSquared() > 0.0001f)
            Direction = Vector2.Normalize(Velocity);

        Owner = newOwner;
        Color = newColor;
        _age = 0f;
    }

    public void Update(float delta, Rectangle bounds, Level level, IReadOnlyList<Stickman> targets)
    {
        _age += delta;
        BouncedThisFrame = false;

        if (_homingTurnRate > 0f)
            ApplyHoming(delta, targets);

        Vector2 previousPosition = Position;
        Velocity.Y += Gravity * _gravityMultiplier * delta;
        Position += Velocity * delta;

        // No top-edge cull: a bullet shot off the top of the screen just keeps existing (unseen)
        // under gravity and arcs back down into frame instead of vanishing, same as it would if the
        // screen were simply taller. Left/right/bottom still cull it, since nothing brings it back
        // from those directions.
        if (Position.X < bounds.Left || Position.X > bounds.Right || Position.Y > bounds.Bottom)
        {
            IsAlive = false;
            return;
        }

        // See SpawnClearanceBuffer/HasClearedSpawn - skip terrain/wall collision until the bullet
        // has cleared its own spawn footprint, so a big bullet can't die on the frame it's born.
        if (!HasClearedSpawn)
            return;

        if (level.IsBulletBlockedByTerrain(previousPosition, Position))
        {
            if (TryBounce(HorizontalSurfaceNormal))
                Position.Y = previousPosition.Y; // step back above the surface so next frame's check doesn't immediately re-trigger
            else
            {
                IsAlive = false;
                HitSolid = true;
            }
        }

        if (IsAlive && IsBlockedByWall(level))
        {
            if (TryBounce(VerticalSurfaceNormal))
                Position.X = previousPosition.X;
            else
            {
                IsAlive = false;
                HitSolid = true;
            }
        }
    }

    // Reflects Velocity/Direction about the given surface normal, keeping speed unchanged, as long as
    // a bounce charge (from Ricochet Rounds) is left. Returns false - meaning "destroy the bullet
    // instead" - once bounces run out.
    private bool TryBounce(Vector2 surfaceNormal)
    {
        if (_bouncesRemaining <= 0)
            return false;

        _bouncesRemaining--;
        Velocity -= 2f * Vector2.Dot(Velocity, surfaceNormal) * surfaceNormal;
        if (Velocity.LengthSquared() > 0.0001f)
            Direction = Vector2.Normalize(Velocity);

        BouncedThisFrame = true;
        return true;
    }

    private bool IsBlockedByWall(Level level)
    {
        var bounds = GetBounds();
        foreach (var wall in level.Walls)
        {
            if (bounds.Intersects(wall.GetBounds()))
                return true;
        }

        return false;
    }

    // Curves Direction (and Velocity, keeping its current speed) toward the nearest living enemy,
    // by at most _homingTurnRate radians this frame - a gentle auto-aim rather than a hard lock.
    private void ApplyHoming(float delta, IReadOnlyList<Stickman> targets)
    {
        if (targets == null)
            return;

        Stickman nearest = null;
        float nearestDistanceSquared = float.MaxValue;

        for (int i = 0; i < targets.Count; i++)
        {
            var candidate = targets[i];
            if (candidate == Owner || !candidate.IsAlive)
                continue;

            float distanceSquared = Vector2.DistanceSquared(Position, candidate.Position);
            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearest = candidate;
            }
        }

        if (nearest == null)
            return;

        var hurtbox = nearest.GetHurtbox();
        Vector2 toTarget = new Vector2(hurtbox.Center.X, hurtbox.Center.Y) - Position;
        if (toTarget.LengthSquared() < 0.0001f)
            return;

        float currentAngle = MathF.Atan2(Direction.Y, Direction.X);
        float desiredAngle = MathF.Atan2(toTarget.Y, toTarget.X);
        float turn = MathHelper.WrapAngle(desiredAngle - currentAngle);
        turn = MathHelper.Clamp(turn, -_homingTurnRate * delta, _homingTurnRate * delta);

        float newAngle = currentAngle + turn;
        Direction = new Vector2(MathF.Cos(newAngle), MathF.Sin(newAngle));

        float speed = Velocity.Length();
        Velocity = Direction * speed;
    }

    public Rectangle GetBounds() =>
        new Rectangle((int)(Position.X - _radius), (int)(Position.Y - _radius), (int)(_radius * 2f), (int)(_radius * 2f));

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle)
    {
        // A soft, low-opacity halo behind the solid core gives the bullet a bit of glow/motion feel.
        // Scaling all four channels (not just alpha) is correct here since the soft-circle texture
        // is premultiplied alpha. The halo grows a bit faster than the hitbox-accurate core so a
        // bigger-bullets upgrade reads clearly on screen, not just as a couple of extra pixels.
        Primitives2D.DrawBlob(spriteBatch, softCircle, Position, new Vector2(_radius * 4.2f, _radius * 4.2f), Color * 0.4f);
        Primitives2D.DrawBlob(spriteBatch, softCircle, Position, new Vector2(_radius * 2f, _radius * 2f), Color);
    }
}
