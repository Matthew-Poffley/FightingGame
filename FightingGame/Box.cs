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

    private static readonly Color FillColor = new Color(120, 85, 45);
    private static readonly Color EdgeColor = new Color(70, 45, 20);

    private float _verticalVelocity;

    // Bottom-center position. Falls under gravity and settles on whatever surface is below it -
    // ground or any tier - so it can rest on an elevated platform, not just the ground floor.
    public Vector2 Position;
    public float VelocityX;

    public Box(Vector2 position)
    {
        Position = position;
    }

    public void Update(float delta, Level level, float minX, float maxX)
    {
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

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        var bounds = GetBounds();
        spriteBatch.Draw(pixel, bounds, FillColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3), EdgeColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), EdgeColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 3, bounds.Height), EdgeColor);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 3, bounds.Y, 3, bounds.Height), EdgeColor);
    }
}
