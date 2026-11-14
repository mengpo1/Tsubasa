using Arcade.Core.Content;
using Arcade.Core.Football.Pitch;
using Arcade.Desktop.Hosting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.Rendering;

/// <summary>
/// Draws the ground: either a full-pitch image of the active look (<c>pitch.bitmap.&lt;pitch id&gt;</c>, placed by
/// <see cref="DisplayConfig.PitchBitmap"/>), or procedurally: surroundings, grass with mowing stripes, boards and markings.
/// </summary>
internal sealed class PitchRenderer
{
    /// <summary>How far the grass texture extends beyond the lines (m).</summary>
    private const float GrassOverrun = 2f;

    /// <summary>Thickness of the boards drawn around the playing area (m).</summary>
    private const float BoardThickness = 0.5f;

    private static readonly Color BoardColor = new(30, 34, 48);

    public const string BitmapPrefix = "pitch.bitmap.";

    private readonly float _basePixelsPerMetre;
    private readonly PitchBitmapConfig? _bitmap;

    public PitchRenderer(float basePixelsPerMetre, PitchBitmapConfig? bitmap = null)
    {
        _basePixelsPerMetre = basePixelsPerMetre;
        _bitmap = bitmap;
    }

    public void Draw(SpriteBatch batch, TextureStore textures, PitchGeometry pitch, ViewTransform view)
    {
        string bitmapId = BitmapPrefix + pitch.Definition.Id;
        if (_bitmap is not null && textures.IsAvailable(bitmapId))
        {
            DrawBitmap(batch, textures.Get(bitmapId), _bitmap, view);
            return;
        }

        PitchVisual visual = pitch.Definition.Visual;
        float hw = pitch.HalfWidth;
        float hl = pitch.HalfLength;
        float grassMargin = MathF.Min(GrassOverrun, pitch.Definition.BoundaryMargin - BoardThickness);

        Tile(batch, textures.Get(visual.SurroundTexture), view, -pitch.BoardsHalfWidth, -pitch.BoardsHalfLength, pitch.BoardsHalfWidth, pitch.BoardsHalfLength);
        Tile(batch, textures.Get(visual.GrassTexture), view, -hw - grassMargin, -hl - grassMargin, hw + grassMargin, hl + grassMargin);
        DrawStripes(batch, textures.Pixel, pitch, visual, view, grassMargin);
        DrawBoards(batch, textures.Pixel, pitch, view);
        DrawMarkings(batch, textures.Pixel, pitch, visual, view);
    }

    private static void DrawBitmap(SpriteBatch batch, Texture2D texture, PitchBitmapConfig bitmap, ViewTransform view)
    {
        float left = -bitmap.Centre[0] / bitmap.PixelsPerMetre;
        float top = -bitmap.Centre[1] / bitmap.PixelsPerMetre;
        Vector2 a = view.ToScreen(left, top);
        Vector2 b = view.ToScreen(left + (texture.Width / bitmap.PixelsPerMetre), top + (texture.Height / bitmap.PixelsPerMetre));
        var destination = new Rectangle((int)MathF.Round(a.X), (int)MathF.Round(a.Y), (int)MathF.Round(b.X - a.X), (int)MathF.Round(b.Y - a.Y));
        batch.Draw(texture, destination, Color.White);
    }

    private void Tile(SpriteBatch batch, Texture2D texture, ViewTransform view, float left, float top, float right, float bottom)
    {
        Vector2 a = view.ToScreen(left, top);
        Vector2 b = view.ToScreen(right, bottom);
        var destination = new Rectangle((int)MathF.Round(a.X), (int)MathF.Round(a.Y), (int)MathF.Round(b.X - a.X), (int)MathF.Round(b.Y - a.Y));
        var source = new Rectangle(
            (int)MathF.Round(left * _basePixelsPerMetre),
            (int)MathF.Round(top * _basePixelsPerMetre),
            (int)MathF.Round((right - left) * _basePixelsPerMetre),
            (int)MathF.Round((bottom - top) * _basePixelsPerMetre));
        batch.Draw(texture, destination, source, Color.White);
    }

    private static void DrawStripes(SpriteBatch batch, Texture2D pixel, PitchGeometry pitch, PitchVisual visual, ViewTransform view, float margin)
    {
        if (visual.StripeWidth <= 0f || visual.StripeShade == 1f)
        {
            return;
        }

        Color overlay = visual.StripeShade < 1f
            ? Color.Black * (1f - visual.StripeShade)
            : Color.White * (visual.StripeShade - 1f);
        int index = 0;
        for (float y = -pitch.HalfLength; y < pitch.HalfLength - 0.001f; y += visual.StripeWidth, index++)
        {
            if (index % 2 == 1)
            {
                float bottom = MathF.Min(y + visual.StripeWidth, pitch.HalfLength);
                Shapes.Rectangle(batch, pixel, RectangleF.FromCorners(view.ToScreen(-pitch.HalfWidth - margin, y), view.ToScreen(pitch.HalfWidth + margin, bottom)), overlay);
            }
        }
    }

    private static void DrawBoards(SpriteBatch batch, Texture2D pixel, PitchGeometry pitch, ViewTransform view)
    {
        float w = pitch.BoardsHalfWidth;
        float l = pitch.BoardsHalfLength;
        float t = BoardThickness;
        Shapes.Rectangle(batch, pixel, RectangleF.FromCorners(view.ToScreen(-w, -l), view.ToScreen(w, -l + t)), BoardColor);
        Shapes.Rectangle(batch, pixel, RectangleF.FromCorners(view.ToScreen(-w, l - t), view.ToScreen(w, l)), BoardColor);
        Shapes.Rectangle(batch, pixel, RectangleF.FromCorners(view.ToScreen(-w, -l), view.ToScreen(-w + t, l)), BoardColor);
        Shapes.Rectangle(batch, pixel, RectangleF.FromCorners(view.ToScreen(w - t, -l), view.ToScreen(w, l)), BoardColor);
    }

    private static void DrawMarkings(SpriteBatch batch, Texture2D pixel, PitchGeometry pitch, PitchVisual visual, ViewTransform view)
    {
        PitchDefinition d = pitch.Definition;
        Color color = ToColor(ColorRgb.Parse(visual.LineColor));
        float thickness = MathF.Max(1f, MathF.Round(d.LineWidth * view.Scale));
        float hw = pitch.HalfWidth;
        float hl = pitch.HalfLength;

        RectangleLines(batch, pixel, view, -hw, -hl, hw, hl, thickness, color);
        Shapes.Line(batch, pixel, view.ToScreen(-hw, 0f), view.ToScreen(hw, 0f), thickness, color);
        Vector2 centre = view.ToScreen(0f, 0f);
        Shapes.Arc(batch, pixel, centre, d.CentreCircleRadius * view.Scale, 0f, MathF.Tau, thickness, color);
        Spot(batch, pixel, centre, thickness, color);

        foreach (GoalEnd end in PitchGeometry.Ends)
        {
            float line = pitch.GoalLineY(end);
            float inward = -PitchGeometry.Outward(end);
            RectangleLines(batch, pixel, view, -d.PenaltyAreaWidth / 2f, line, d.PenaltyAreaWidth / 2f, line + (inward * d.PenaltyAreaDepth), thickness, color);
            RectangleLines(batch, pixel, view, -d.GoalAreaWidth / 2f, line, d.GoalAreaWidth / 2f, line + (inward * d.GoalAreaDepth), thickness, color);
            Vector2 spot = view.ToScreen(pitch.PenaltySpot(end));
            Spot(batch, pixel, spot, thickness, color);

            // Penalty arc: the part of the 9.15 m circle around the spot that lies outside the area.
            float outside = (d.PenaltyAreaDepth - d.PenaltySpotDistance) / d.CentreCircleRadius;
            if (outside is > -1f and < 1f)
            {
                float a = MathF.Asin(outside);
                (float from, float to) = end == GoalEnd.Top ? (a, MathF.PI - a) : (MathF.PI + a, MathF.Tau - a);
                Shapes.Arc(batch, pixel, spot, d.CentreCircleRadius * view.Scale, from, to, thickness, color);
            }
        }

        float corner = d.CornerArcRadius * view.Scale;
        Shapes.Arc(batch, pixel, view.ToScreen(-hw, -hl), corner, 0f, MathF.PI / 2f, thickness, color);
        Shapes.Arc(batch, pixel, view.ToScreen(hw, -hl), corner, MathF.PI / 2f, MathF.PI, thickness, color);
        Shapes.Arc(batch, pixel, view.ToScreen(hw, hl), corner, MathF.PI, MathF.PI * 1.5f, thickness, color);
        Shapes.Arc(batch, pixel, view.ToScreen(-hw, hl), corner, MathF.PI * 1.5f, MathF.Tau, thickness, color);
    }

    private static void RectangleLines(SpriteBatch batch, Texture2D pixel, ViewTransform view, float x0, float y0, float x1, float y1, float thickness, Color color)
    {
        RectangleF r = RectangleF.FromCorners(view.ToScreen(x0, y0), view.ToScreen(x1, y1));
        Shapes.Outline(batch, pixel, new RectangleF(r.X - (thickness / 2f), r.Y - (thickness / 2f), r.Width + thickness, r.Height + thickness), thickness, color);
    }

    private static void Spot(SpriteBatch batch, Texture2D pixel, Vector2 at, float thickness, Color color)
    {
        float size = thickness * 2f;
        Shapes.Rectangle(batch, pixel, new RectangleF(at.X - (size / 2f), at.Y - (size / 2f), size, size), color);
    }

    public static Color ToColor(ColorRgb color) => new(color.R, color.G, color.B);
}
