using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Num = System.Numerics;

namespace Arcade.Desktop.Rendering;

/// <summary>
/// Draws goals, players and ball, depth-sorted by ground position (painter's algorithm), from the
/// interpolated simulation state. Reads the world only: rendering never changes the game.
/// </summary>
internal sealed class WorldRenderer
{
    private const string IdleAnimation = "player.idle";
    private const string RunAnimation = "player.run";
    private const string KickAnimation = "player.kick";
    private const string BallAnimation = "ball.roll";
    private const string ShadowTexture = "shadow.default";
    private const string SelectionTexture = "ui.selection";

    /// <summary>Height of the selection marker above the feet (m).</summary>
    private const float MarkerHeight = 2.6f;

    private static readonly Color SelectionColor = new(255, 230, 60);

    private readonly float _basePixelsPerMetre;
    private readonly List<Drawable> _drawables = new(64);

    public WorldRenderer(float basePixelsPerMetre)
    {
        _basePixelsPerMetre = basePixelsPerMetre;
    }

    private enum DrawableKind
    {
        Player,
        Ball,
        TopGoalBack,
        TopGoalFront,
        BottomGoalFrame,
        BottomGoalNet,
    }

    public void Draw(SpriteBatch batch, TextureStore textures, FootballWorld world, IReadOnlyList<KitDefinition> kits, Player? selected, float alpha, ViewTransform view, float? passCharge)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(kits);
        PitchGeometry pitch = world.Pitch;
        _drawables.Clear();
        _drawables.Add(new Drawable(-pitch.HalfLength - pitch.GoalDepth, DrawableKind.TopGoalBack, 0));
        _drawables.Add(new Drawable(-pitch.HalfLength, DrawableKind.TopGoalFront, 0));
        _drawables.Add(new Drawable(pitch.HalfLength, DrawableKind.BottomGoalFrame, 0));
        _drawables.Add(new Drawable(pitch.HalfLength + pitch.GoalDepth, DrawableKind.BottomGoalNet, 0));
        for (int i = 0; i < world.Players.Count; i++)
        {
            Player player = world.Players[i];
            _drawables.Add(new Drawable(Num.Vector2.Lerp(player.PreviousPosition, player.Position, alpha).Y, DrawableKind.Player, i));
        }

        Num.Vector3 ball = Num.Vector3.Lerp(world.Ball.PreviousPosition, world.Ball.Position, alpha);
        _drawables.Add(new Drawable(ball.Y, DrawableKind.Ball, 0));
        _drawables.Sort(static (a, b) => a.Key.CompareTo(b.Key));

        float spriteScale = view.Scale / _basePixelsPerMetre;
        GoalPainter goals = new(textures, pitch, view);
        foreach (Drawable drawable in _drawables)
        {
            switch (drawable.Kind)
            {
                case DrawableKind.Player:
                    Player player = world.Players[drawable.Index];
                    DrawPlayer(batch, textures, world, player, kits[player.TeamIndex], alpha, view, spriteScale);
                    break;
                case DrawableKind.Ball:
                    DrawBall(batch, textures, world, ball, view, spriteScale);
                    break;
                case DrawableKind.TopGoalBack:
                    goals.Back(batch, GoalEnd.Top);
                    break;
                case DrawableKind.TopGoalFront:
                    goals.Roof(batch, GoalEnd.Top);
                    goals.Frame(batch, GoalEnd.Top);
                    break;
                case DrawableKind.BottomGoalFrame:
                    goals.Frame(batch, GoalEnd.Bottom);
                    break;
                case DrawableKind.BottomGoalNet:
                    goals.Roof(batch, GoalEnd.Bottom);
                    goals.Back(batch, GoalEnd.Bottom);
                    break;
            }
        }

        if (selected is not null)
        {
            DrawSelection(batch, textures, selected, alpha, view, spriteScale, passCharge);
        }
    }

    private static void DrawPlayer(SpriteBatch batch, TextureStore textures, FootballWorld world, Player player, KitDefinition kit, float alpha, ViewTransform view, float scale)
    {
        Vector2 feet = view.ToScreen(Num.Vector2.Lerp(player.PreviousPosition, player.Position, alpha));
        Texture2D shadow = textures.Get(ShadowTexture);
        batch.Draw(shadow, feet, null, Color.White, 0f, new Vector2(shadow.Width / 2f, shadow.Height / 2f), scale, SpriteEffects.None, 0f);

        string animationId = player.Action switch
        {
            PlayerAction.Kicking => KickAnimation,
            PlayerAction.Running => RunAnimation,
            _ => IdleAnimation,
        };
        if (!textures.Catalog.Animations.TryGetValue(animationId, out AnimationDefinition? animation))
        {
            return;
        }

        int frame = FrameIndex(animation, player.DistanceRun, world.Tick * world.StepSeconds);
        int row = animation.Directions == 8 && player.Facing != Direction8.None ? player.Facing.ToIndex() : 0;
        Texture2D texture = textures.GetKit(animation.Texture, animation.Palette, kit);
        var source = new Rectangle(animation.Frames[frame] * animation.FrameWidth, row * animation.FrameHeight, animation.FrameWidth, animation.FrameHeight);
        var origin = new Vector2(animation.Anchor[0] - animation.Offset[0], animation.Anchor[1] - animation.Offset[1]);
        batch.Draw(texture, feet, source, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private static void DrawBall(SpriteBatch batch, TextureStore textures, FootballWorld world, Num.Vector3 ball, ViewTransform view, float scale)
    {
        Texture2D shadow = textures.Get(ShadowTexture);
        float shadowScale = scale * 0.5f * MathF.Max(0.35f, 1f - (ball.Z / 8f));
        Vector2 ground = view.ToScreen(new Num.Vector2(ball.X, ball.Y));
        batch.Draw(shadow, ground, null, Color.White, 0f, new Vector2(shadow.Width / 2f, shadow.Height / 2f), shadowScale, SpriteEffects.None, 0f);
        if (!textures.Catalog.Animations.TryGetValue(BallAnimation, out AnimationDefinition? animation))
        {
            return;
        }

        int frame = FrameIndex(animation, world.Ball.DistanceTravelled, 0f);
        Texture2D texture = textures.Get(animation.Texture);
        var source = new Rectangle(animation.Frames[frame] * animation.FrameWidth, 0, animation.FrameWidth, animation.FrameHeight);
        float radius = world.Content.Physics.Ball.Radius;
        Vector2 position = view.ToScreen(new Num.Vector2(ball.X, ball.Y), ball.Z + radius);
        batch.Draw(texture, position, source, Color.White, 0f, new Vector2(animation.Anchor[0], animation.Anchor[1]), scale, SpriteEffects.None, 0f);
    }

    private static void DrawSelection(SpriteBatch batch, TextureStore textures, Player player, float alpha, ViewTransform view, float scale, float? passCharge)
    {
        Texture2D marker = textures.Get(SelectionTexture);
        Vector2 top = view.ToScreen(Num.Vector2.Lerp(player.PreviousPosition, player.Position, alpha), MarkerHeight);
        batch.Draw(marker, top, null, SelectionColor, 0f, new Vector2(marker.Width / 2f, marker.Height), scale, SpriteEffects.None, 0f);
        if (passCharge is float charge)
        {
            float width = 16f * scale;
            float height = 2f * scale;
            var bar = new RectangleF(top.X - (width / 2f), top.Y - (marker.Height * scale) - (4f * scale), width, height);
            Shapes.Rectangle(batch, textures.Pixel, bar, Color.Black * 0.6f);
            Shapes.Rectangle(batch, textures.Pixel, bar with { Width = width * Math.Clamp(charge, 0f, 1f) }, SelectionColor);
        }
    }

    /// <summary>Frame index: synchronised with distance travelled when the animation has a reference speed, else with time.</summary>
    private static int FrameIndex(AnimationDefinition animation, float distance, float seconds)
    {
        float position = animation.ReferenceSpeed > 0f
            ? distance / (animation.ReferenceSpeed * animation.FrameDuration)
            : seconds / animation.FrameDuration;
        return (int)position % animation.Frames.Count;
    }

    private readonly record struct Drawable(float Key, DrawableKind Kind, int Index);

    /// <summary>Goals in the oblique projection: every face projects to an axis-aligned rectangle or a line.</summary>
    private readonly struct GoalPainter(TextureStore textures, PitchGeometry pitch, ViewTransform view)
    {
        private Color FrameColor => PitchRenderer.ToColor(ColorRgb.Parse(pitch.Definition.Visual.GoalColor));

        public void Frame(SpriteBatch batch, GoalEnd end)
        {
            float line = pitch.GoalLineY(end);
            float x = pitch.GoalHalfWidth + pitch.PostRadius;
            float top = pitch.GoalHeight + (2f * pitch.PostRadius);
            float thickness = MathF.Max(2f, MathF.Round(pitch.PostRadius * 2f * view.Scale));
            Color color = FrameColor;
            Vector2 leftFoot = view.ToScreen(-x, line);
            Vector2 rightFoot = view.ToScreen(x, line);
            Vector2 leftTop = view.ToScreen(-x, line, top);
            Vector2 rightTop = view.ToScreen(x, line, top);
            Shapes.Line(batch, textures.Pixel, leftFoot, leftTop, thickness, color);
            Shapes.Line(batch, textures.Pixel, rightFoot, rightTop, thickness, color);
            Shapes.Line(batch, textures.Pixel, leftTop - new Vector2(thickness / 2f, 0f), rightTop + new Vector2(thickness / 2f, 0f), thickness, color);
        }

        public void Roof(SpriteBatch batch, GoalEnd end)
        {
            float line = pitch.GoalLineY(end);
            float back = line + (PitchGeometry.Outward(end) * pitch.GoalDepth);
            NetRectangle(batch, view.ToScreen(-pitch.GoalHalfWidth, line, pitch.GoalHeight), view.ToScreen(pitch.GoalHalfWidth, back, pitch.GoalHeight));
            SideLines(batch, line, back);
        }

        public void Back(SpriteBatch batch, GoalEnd end)
        {
            float back = pitch.GoalLineY(end) + (PitchGeometry.Outward(end) * pitch.GoalDepth);
            NetRectangle(batch, view.ToScreen(-pitch.GoalHalfWidth, back, pitch.GoalHeight), view.ToScreen(pitch.GoalHalfWidth, back, 0f));
        }

        private void SideLines(SpriteBatch batch, float line, float back)
        {
            Color net = Color.White * 0.55f;
            foreach (float x in (ReadOnlySpan<float>)[-pitch.GoalHalfWidth, pitch.GoalHalfWidth])
            {
                Shapes.Line(batch, textures.Pixel, view.ToScreen(x, line, pitch.GoalHeight), view.ToScreen(x, back, pitch.GoalHeight), 1f, net);
                Shapes.Line(batch, textures.Pixel, view.ToScreen(x, back, 0f), view.ToScreen(x, back, pitch.GoalHeight), 1f, net);
                Shapes.Line(batch, textures.Pixel, view.ToScreen(x, line, 0f), view.ToScreen(x, back, 0f), 1f, net);
            }
        }

        private void NetRectangle(SpriteBatch batch, Vector2 a, Vector2 b)
        {
            RectangleF r = RectangleF.FromCorners(a, b);
            var destination = new Rectangle((int)MathF.Round(r.X), (int)MathF.Round(r.Y), (int)MathF.Round(r.Width), (int)MathF.Round(r.Height));
            Texture2D texture = textures.Get(pitch.Definition.Visual.NetTexture);
            batch.Draw(textures.Pixel, destination, Color.Black * 0.12f);
            batch.Draw(texture, destination, new Rectangle(0, 0, destination.Width, destination.Height), Color.White);
        }
    }
}
