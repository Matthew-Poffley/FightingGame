using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

// A static explosive prop - unlike Box, it never moves (no push/friction physics), it just sits
// where it's dropped and detonates (see Game1.TriggerSplashDamage) once destroyed. Catching a
// second barrel in that blast breaks it too, chaining the explosion.
public class Barrel
{
    private const float Width = 40f;
    private const float Height = 58f;
    private const float MaxHealth = 30f;

    public const float ExplosionRadius = 220f;
    public const float ExplosionDamage = 55f;

    private static readonly Color FillColor = new Color(180, 70, 40);
    private static readonly Color BandColor = new Color(60, 45, 35);
    private static readonly Color WarningColor = new Color(255, 200, 60);

    // Bottom-center position, same convention as Box.
    public Vector2 Position;
    public float Health { get; private set; } = MaxHealth;
    public bool IsBroken => Health <= 0f;

    public Barrel(Vector2 position)
    {
        Position = position;
    }

    public void ApplyDamage(float amount) => Health = MathHelper.Max(0f, Health - amount);

    public Rectangle GetBounds() =>
        new Rectangle((int)(Position.X - Width / 2f), (int)(Position.Y - Height), (int)Width, (int)Height);

    private const float CornerRadius = 10f;

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle)
    {
        var bounds = GetBounds();
        var center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
        var size = new Vector2(bounds.Width, bounds.Height);

        float damageFraction = 1f - Health / MaxHealth;
        Color fillColor = Color.Lerp(FillColor, Color.Black, damageFraction * 0.45f);

        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center, size, CornerRadius, fillColor);

        // A couple of dark bands and a warning chevron, so it reads as a barrel rather than a crate.
        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, -size.Y * 0.22f), new Vector2(size.X + 4f, 6f), 3f, BandColor);
        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, center + new Vector2(0f, size.Y * 0.22f), new Vector2(size.X + 4f, 6f), 3f, BandColor);

        Vector2 warnCenter = center;
        Primitives2D.DrawCircle(spriteBatch, softCircle, warnCenter, size.X * 0.28f, WarningColor * (1f - damageFraction * 0.5f));
        Primitives2D.DrawRoundedRect(spriteBatch, pixel, softCircle, warnCenter + new Vector2(0f, -3f), new Vector2(5f, 12f), 2f, BandColor);
        Primitives2D.DrawCircle(spriteBatch, softCircle, warnCenter + new Vector2(0f, 8f), 2.5f, BandColor);
    }
}
