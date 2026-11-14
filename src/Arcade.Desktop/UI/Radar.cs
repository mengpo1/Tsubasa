using Arcade.Core.Content;
using Arcade.Core.Football;
using Arcade.Core.Football.Teams;
using Arcade.Desktop.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Num = System.Numerics;

namespace Arcade.Desktop.UI;

/// <summary>Miniature pitch showing every player and the ball (the "scanner" of the original game).</summary>
internal static class Radar
{
    public static void Draw(SpriteBatch batch, Texture2D pixel, FootballWorld world, IReadOnlyList<KitDefinition> kits, RectangleF area, float opacity, ViewTransform view)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(kits);
        float scaleX = area.Width / (world.Pitch.HalfWidth * 2f);
        float scaleY = area.Height / (world.Pitch.HalfLength * 2f);
        Vector2 ToRadar(Num.Vector2 p) => new(area.X + ((p.X + world.Pitch.HalfWidth) * scaleX), area.Y + ((p.Y + world.Pitch.HalfLength) * scaleY));

        Shapes.Rectangle(batch, pixel, area, new Color(10, 40, 14) * opacity);
        Shapes.Outline(batch, pixel, area, 1f, Color.White * (0.6f * opacity));
        Shapes.Line(batch, pixel, new Vector2(area.X, area.Y + (area.Height / 2f)), new Vector2(area.Right, area.Y + (area.Height / 2f)), 1f, Color.White * (0.35f * opacity));

        (float left, float top, float right, float bottom) = view.VisibleWorld();
        RectangleF visible = RectangleF.FromCorners(ToRadar(new Num.Vector2(left, top)), ToRadar(new Num.Vector2(right, bottom)));
        Shapes.Outline(batch, pixel, visible, 1f, Color.White * (0.25f * opacity));

        float dot = MathF.Max(2f, area.Width / 22f);
        foreach (var player in world.Players)
        {
            ColorRgb shirt = ColorRgb.Parse(kits[player.TeamIndex].Shirt);
            Vector2 at = ToRadar(player.Position);
            Shapes.Rectangle(batch, pixel, new RectangleF(at.X - (dot / 2f), at.Y - (dot / 2f), dot, dot), new Color(shirt.R, shirt.G, shirt.B) * opacity);
        }

        Vector2 ball = ToRadar(new Num.Vector2(world.Ball.Position.X, world.Ball.Position.Y));
        Shapes.Rectangle(batch, pixel, new RectangleF(ball.X - (dot / 2f), ball.Y - (dot / 2f), dot, dot), Color.White * opacity);
    }
}
