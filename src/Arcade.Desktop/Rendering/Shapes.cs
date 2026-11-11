using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.Rendering;

/// <summary>Primitive drawing helpers built on a 1x1 white texture.</summary>
internal static class Shapes
{
    public static void Line(SpriteBatch batch, Texture2D pixel, Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 delta = to - from;
        float length = delta.Length();
        if (length < 0.01f)
        {
            return;
        }

        float angle = MathF.Atan2(delta.Y, delta.X);
        batch.Draw(pixel, from, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public static void Rectangle(SpriteBatch batch, Texture2D pixel, RectangleF rectangle, Color color) =>
        batch.Draw(pixel, new Vector2(rectangle.X, rectangle.Y), null, color, 0f, Vector2.Zero, new Vector2(rectangle.Width, rectangle.Height), SpriteEffects.None, 0f);

    public static void Outline(SpriteBatch batch, Texture2D pixel, RectangleF r, float thickness, Color color)
    {
        Rectangle(batch, pixel, new RectangleF(r.X, r.Y, r.Width, thickness), color);
        Rectangle(batch, pixel, new RectangleF(r.X, r.Bottom - thickness, r.Width, thickness), color);
        Rectangle(batch, pixel, new RectangleF(r.X, r.Y, thickness, r.Height), color);
        Rectangle(batch, pixel, new RectangleF(r.Right - thickness, r.Y, thickness, r.Height), color);
    }

    /// <summary>Arc of a circle given in screen space (angles in radians, 0 = +X, clockwise on screen).</summary>
    public static void Arc(SpriteBatch batch, Texture2D pixel, Vector2 centre, float radius, float fromAngle, float toAngle, float thickness, Color color)
    {
        int segments = Math.Clamp((int)(radius * MathF.Abs(toAngle - fromAngle) / 3f), 6, 96);
        Vector2 previous = centre + (radius * new Vector2(MathF.Cos(fromAngle), MathF.Sin(fromAngle)));
        for (int i = 1; i <= segments; i++)
        {
            float angle = fromAngle + ((toAngle - fromAngle) * i / segments);
            Vector2 next = centre + (radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle)));
            Line(batch, pixel, previous, next, thickness, color);
            previous = next;
        }
    }
}

/// <summary>Float rectangle (MonoGame's Rectangle is integer-only).</summary>
internal readonly record struct RectangleF(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;

    public float Bottom => Y + Height;

    public static RectangleF FromCorners(Vector2 a, Vector2 b) =>
        new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y), MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y));
}
