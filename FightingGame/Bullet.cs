using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

public class Bullet
{
    private const float Speed = 900f;
    private const float Radius = 4f;
    private const float Gravity = 350f;

    // A bullet can't hit its own shooter for a brief moment after firing - just long enough to clear
    // the muzzle - so a shot fired straight up can still fall back down and hit them once it returns.
    private const float SelfHitGraceDuration = 0.15f;

    private float _age;

    public Vector2 Position;
    public Vector2 Velocity;
    public Vector2 Direction { get; private set; }
    public Color Color { get; private set; }
    public Stickman Owner { get; private set; }
    public bool IsAlive = true;

    public bool CanHitOwner => _age >= SelfHitGraceDuration;

    public Bullet(Vector2 position, Vector2 direction, Color color, Stickman owner)
    {
        Position = position;
        Direction = direction;
        Velocity = direction * Speed;
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

    public void Update(float delta, Rectangle bounds, Level level)
    {
        _age += delta;

        Vector2 previousPosition = Position;
        Velocity.Y += Gravity * delta;
        Position += Velocity * delta;

        if (!bounds.Contains(Position))
        {
            IsAlive = false;
            return;
        }

        if (level.IsBulletBlockedByTerrain(previousPosition, Position))
            IsAlive = false;
    }

    public Rectangle GetBounds() =>
        new Rectangle((int)(Position.X - Radius), (int)(Position.Y - Radius), (int)(Radius * 2f), (int)(Radius * 2f));

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel, Position, null, Color, 0f, new Vector2(0.5f, 0.5f), new Vector2(Radius * 2f, Radius * 2f), SpriteEffects.None, 0f);
    }
}
