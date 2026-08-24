using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

public class Box
{
    private const float Width = 50f;
    private const float Height = 50f;
    private const float Friction = 3.5f;
    private const float MinVelocity = 5f;
    private const float Gravity = 1600f;
    private const float MaxHealth = 50f;

    private static readonly Color FillColor = new Color(120, 85, 45);
    private static readonly Color EdgeColor = new Color(70, 45, 20);

    private float _verticalVelocity;

    // Bottom-center position. Falls under gravity and settles on whatever surface is below it -
    // ground or any tier - so it can rest on an elevated platform, not just the ground floor.
    public Vector2 Position;
    public float VelocityX;
    public float Health { get; private set; } = MaxHealth;
    public bool IsBroken => Health <= 0f;

    public Box(Vector2 position)
    {
        Position = position;
    }

    public void ApplyDamage(float amount) => Health = MathHelper.Max(0f, Health - amount);

    public void Update(float delta, Level level, float minX, float maxX)
    {
        // Riding a moving platform - carry along whatever it moved this frame. Safe to call
        // unconditionally: it's a no-op unless this box's feet are actually on one right now.
        Position.X += level.GetCarryDeltaX(Position.X, Position.Y);

        Position.X += VelocityX * delta;
        Position.X = MathHelper.Clamp(Position.X, minX, maxX);

        if (level.ResolveWallCollision(ref Position, Width / 2f, Height))
            VelocityX = 0f;

        VelocityX *= MathF.Max(0f, 1f - Friction * delta);
        if (MathF.Abs(VelocityX) < MinVelocity)
            VelocityX = 0f;

        float previousY = Position.Y;
        _verticalVelocity += Gravity * delta;
        float candidateY = Position.Y + _verticalVelocity * delta;

        float landingHeight = level.GetLandingHeightAt(Position.X, previousY, candidateY, falling: true);
        if (candidateY >= landingHeight)
        {
            Position.Y = landingHeight;
            _verticalVelocity = 0f;
        }
        else
        {
            Position.Y = candidateY;
        }
    }

    public void ApplyImpulse(float velocityDelta) => VelocityX += velocityDelta;

    public Rectangle GetBounds() =>
        new Rectangle((int)(Position.X - Width / 2f), (int)(Position.Y - Height), (int)Width, (int)Height);

    private const float CornerRadius = 8f;

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle)
    {
        var bounds = GetBounds();
        var center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
        var size = new Vector2(bounds.Width, bounds.Height);

        // Charred/battered look scales with damage taken, so a box visibly nears breaking before it does.
        float damageFraction = 1f - Health / MaxHealth;
        Color fillColor = Color.Lerp(FillColor, Color.Black, damageFraction * 0.45f);
        Color edgeColor = Color.Lerp(EdgeColor, Color.Black, damageFraction * 0.45f);

        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, size, CornerRadius, fillColor);
        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, size - new Vector2(6f, 6f), CornerRadius - 3f, edgeColor);
        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, size - new Vector2(12f, 12f), CornerRadius - 3f, fillColor);
    }
}
