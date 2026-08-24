using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FightingGame;

// Shared drawing helpers that give the game's single-pixel-texture renderer soft, anti-aliased
// curves (circles, capsule-shaped limbs, rounded rectangles) instead of hard-edged blocks, without
// needing any real art assets. Everything here is built from two textures: the 1x1 flat "pixel"
// already used throughout the game, and a soft-edged circle generated once at startup.
public static class Primitives2D
{
    // A white circle with a ~2px feathered edge, baked into a texture so scaling/rotating it via
    // SpriteBatch (with the default linear sampler) produces a smooth, anti-aliased silhouette -
    // used directly for circles/ellipses ("blobs"), and as the rounded corners/caps of other shapes.
    public static Texture2D CreateSoftCircleTexture(GraphicsDevice device, int size = 64)
    {
        var data = new Color[size * size];
        float radius = size / 2f;
        var center = new Vector2(radius, radius);
        float featherStart = radius - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha;
                if (dist <= featherStart)
                    alpha = 1f;
                else if (dist >= radius)
                    alpha = 0f;
                else
                    alpha = 1f - (dist - featherStart) / (radius - featherStart);

                // Premultiplied alpha (white * alpha == alpha), so it blends correctly under the
                // default BlendState.AlphaBlend already used everywhere else in the game (including
                // SpriteFont text, which is premultiplied too) - no BlendState changes needed anywhere.
                data[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
        }

        var texture = new Texture2D(device, size, size, false, SurfaceFormat.Color);
        texture.SetData(data);
        return texture;
    }

    public static Vector2 RotateVector(Vector2 v, float angle)
    {
        float cos = System.MathF.Cos(angle), sin = System.MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    // A soft circle stretched to an arbitrary width/height - a circle when size.X == size.Y, an
    // anti-aliased ellipse otherwise. `size` is the full width/height (a diameter, not a radius).
    public static void DrawBlob(SpriteBatch spriteBatch, Texture2D softCircle, Vector2 center, Vector2 size, Color color, float rotation = 0f)
    {
        Vector2 origin = new Vector2(softCircle.Width / 2f, softCircle.Height / 2f);
        Vector2 scale = new Vector2(size.X / softCircle.Width, size.Y / softCircle.Height);
        spriteBatch.Draw(softCircle, center, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
    }

    public static void DrawCircle(SpriteBatch spriteBatch, Texture2D softCircle, Vector2 center, float diameter, Color color)
    {
        DrawBlob(spriteBatch, softCircle, center, new Vector2(diameter, diameter), color);
    }

    // A flat, hard-edged rectangle - the base primitive everything else is built from.
    public static void DrawRect(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, Vector2 size, Color color, float rotation = 0f)
    {
        spriteBatch.Draw(pixel, center, null, color, rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
    }

    // A straight rectangle capped with soft round ends - reads as a rounded "sausage" shape, used for
    // limbs/lines instead of harsh rectangular bars.
    public static void DrawCapsule(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 start, Vector2 end, float thickness, Color color)
    {
        Vector2 edge = end - start;
        float length = edge.Length();

        if (length > 0.01f)
        {
            float angle = System.MathF.Atan2(edge.Y, edge.X);
            spriteBatch.Draw(pixel, start, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        DrawCircle(spriteBatch, softCircle, start, thickness, color);
        DrawCircle(spriteBatch, softCircle, end, thickness, color);
    }

    // A filled rectangle with softly rounded corners: two overlapping straight rects plus a soft
    // circle at each corner (each circle's own edge feather forms the anti-aliased rounding).
    public static void DrawRoundedRect(SpriteBatch spriteBatch, Texture2D pixel, Texture2D softCircle, Vector2 center, Vector2 size, float cornerRadius, Color color, float rotation = 0f)
    {
        float r = MathHelper.Min(cornerRadius, MathHelper.Min(size.X, size.Y) * 0.5f);

        DrawRect(spriteBatch, pixel, center, new Vector2(size.X - r * 2f, size.Y), color, rotation);
        DrawRect(spriteBatch, pixel, center, new Vector2(size.X, size.Y - r * 2f), color, rotation);

        if (r <= 0.01f)
            return;

        Vector2 half = size / 2f - new Vector2(r, r);
        DrawCircle(spriteBatch, softCircle, center + RotateVector(new Vector2(-half.X, -half.Y), rotation), r * 2f, color);
        DrawCircle(spriteBatch, softCircle, center + RotateVector(new Vector2(half.X, -half.Y), rotation), r * 2f, color);
        DrawCircle(spriteBatch, softCircle, center + RotateVector(new Vector2(-half.X, half.Y), rotation), r * 2f, color);
        DrawCircle(spriteBatch, softCircle, center + RotateVector(new Vector2(half.X, half.Y), rotation), r * 2f, color);
    }

    // A soft, low-opacity ellipse under a character's feet to ground them against the backdrop.
    public static void DrawGroundShadow(SpriteBatch spriteBatch, Texture2D softCircle, Vector2 feetPosition, float width, float alpha = 0.35f)
    {
        DrawBlob(spriteBatch, softCircle, feetPosition, new Vector2(width, width * 0.35f), new Color(0f, 0f, 0f, alpha));
    }
}
