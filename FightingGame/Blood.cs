using Microsoft.Xna.Framework;

namespace FightingGame;

public readonly struct BloodDecal
{
    public readonly Vector2 Position;
    public readonly float Size;
    public readonly Color Color;

    public BloodDecal(Vector2 position, float size, Color color)
    {
        Position = position;
        Size = size;
        Color = color;
    }
}

public class BloodParticle
{
    private const float Gravity = 1400f;

    public Vector2 Position;
    public Vector2 Velocity;
    public readonly Color Color;
    public bool Landed { get; private set; }

    public BloodParticle(Vector2 position, Vector2 velocity, Color color)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
    }

    public void Update(float delta, Level level)
    {
        if (Landed)
            return;

        float previousY = Position.Y;
        Velocity.Y += Gravity * delta;
        Position += Velocity * delta;

        float landingHeight = level.GetLandingHeightAt(Position.X, previousY, Position.Y, Velocity.Y >= 0f);
        if (Position.Y >= landingHeight)
        {
            Position.Y = landingHeight;
            Landed = true;
        }
    }
}
