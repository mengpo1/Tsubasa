using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;
using Arcade.Desktop.Hosting;
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
    private const string SelectionTexture = "ui.selection";

    /// <summary>A dribbling run shows the dribble animation for this long after the last touch (s).</summary>
    private const float DribbleWindowSeconds = 1.5f;

    /// <summary>Marker colour of each human player (control set order).</summary>
    private static readonly Color[] SelectionColors = [new(255, 230, 60), new(80, 230, 255), new(255, 110, 220), new(255, 160, 60)];

    private readonly DisplayConfig _display;
    private readonly AnimationStateMap _animations;
    private readonly List<Drawable> _drawables = new(64);

    public WorldRenderer(DisplayConfig display, AnimationStateMap animations)
    {
        _display = display;
        _animations = animations;
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

    /// <summary>Animation state of a player as the renderer sees it (read-only interpretation of the simulation).</summary>
    public static string StateOf(FootballWorld world, Player player)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        bool dribbling = player.LastTouchKind == BallTouchKind.Dribble
            && (world.Tick - player.LastTouchTick) * world.StepSeconds < DribbleWindowSeconds;
        string action = player.Action switch
        {
            PlayerAction.Kicking => "kick",
            PlayerAction.Controlling => "control",
            PlayerAction.Running => dribbling ? "dribble" : "run",
            _ => "idle",
        };
        if (!player.IsGoalkeeper)
        {
            return action;
        }

        if (world.Ball.Status == BallStatus.Carried && world.Ball.CarrierId == player.Id)
        {
            return "keeper.hold";
        }

        return action switch
        {
            "dribble" => "keeper.run",
            "control" => "keeper.idle",
            _ => "keeper." + action,
        };
    }

    public void Draw(SpriteBatch batch, TextureStore textures, FootballWorld world, IReadOnlyList<KitDefinition> kits, IReadOnlyList<ContextualController> controllers, float alpha, ViewTransform view)
    {
        ArgumentNullException.ThrowIfNull(controllers);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(kits);
        PitchGeometry pitch = world.Pitch;
        _drawables.Clear();
        if (_display.DrawGoals)
        {
            _drawables.Add(new Drawable(-pitch.HalfLength - pitch.GoalDepth, DrawableKind.TopGoalBack, 0));
            _drawables.Add(new Drawable(-pitch.HalfLength, DrawableKind.TopGoalFront, 0));
            _drawables.Add(new Drawable(pitch.HalfLength, DrawableKind.BottomGoalFrame, 0));
            _drawables.Add(new Drawable(pitch.HalfLength + pitch.GoalDepth, DrawableKind.BottomGoalNet, 0));
        }

        for (int i = 0; i < world.Players.Count; i++)
        {
            Player player = world.Players[i];
            _drawables.Add(new Drawable(Num.Vector2.Lerp(player.PreviousPosition, player.Position, alpha).Y, DrawableKind.Player, i));
        }

        Num.Vector3 ball = Num.Vector3.Lerp(world.Ball.PreviousPosition, world.Ball.Position, alpha);
        _drawables.Add(new Drawable(ball.Y, DrawableKind.Ball, 0));
        _drawables.Sort(static (a, b) => a.Key.CompareTo(b.Key));

        GoalPainter goals = new(textures, pitch, view);
        foreach (Drawable drawable in _drawables)
        {
            switch (drawable.Kind)
            {
                case DrawableKind.Player:
                    Player player = world.Players[drawable.Index];
                    DrawPlayer(batch, textures, world, player, kits[player.TeamIndex], alpha, view);
                    break;
                case DrawableKind.Ball:
                    DrawBall(batch, textures, world, ball, view);
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

        float uiScale = view.Scale / _display.PixelsPerMetre;
        for (int i = 0; i < controllers.Count; i++)
        {
            ContextualController controller = controllers[i];
            DrawSelection(batch, textures, controller.Player, alpha, view, uiScale, _display.Sprites.MarkerHeight, Charge(world, controller), SelectionColors[i % SelectionColors.Length]);
        }
    }

    private void DrawPlayer(SpriteBatch batch, TextureStore textures, FootballWorld world, Player player, KitDefinition kit, float alpha, ViewTransform view)
    {
        Vector2 feet = Snap(view.ToScreen(Num.Vector2.Lerp(player.PreviousPosition, player.Position, alpha)));
        if (_display.Sprites.PlayerShadows && _animations.Get("player.shadow") is ResolvedAnimation shadow)
        {
            DrawSprite(batch, textures, shadow, null, feet, 0, 0, SpriteScale(shadow.Animation, view), Color.White);
        }

        ResolvedAnimation? resolved = _animations.Get(StateOf(world, player));
        if (resolved is null)
        {
            DrawPlaceholder(batch, textures, feet, view);
            return;
        }

        AnimationDefinition animation = resolved.Animation;
        float seconds = (world.Tick - player.ActionTick) * world.StepSeconds;
        int frame = AnimationFrames.FrameNumber(animation, player.DistanceRun, seconds, 0f);
        int direction = player.Facing != Direction8.None ? player.Facing.ToIndex() : 0;
        DrawSprite(batch, textures, resolved, kit, feet, frame, direction, SpriteScale(animation, view), Color.White);
    }

    /// <summary>
    /// Ball and shadow. A height-driven <c>ball</c> animation (original sheets) shows the low ball with its shadow
    /// drawn in the frames; above its range, <c>ball.high</c> is shown with a separate <c>ball.shadow</c>.
    /// Otherwise the ball rolls with the distance travelled and always has a separate shadow.
    /// </summary>
    private void DrawBall(SpriteBatch batch, TextureStore textures, FootballWorld world, Num.Vector3 ball, ViewTransform view)
    {
        var groundPoint = new Num.Vector2(ball.X, ball.Y);
        Vector2 ground = view.ToScreen(groundPoint);
        ResolvedAnimation? low = _animations.Get("ball");
        ResolvedAnimation? shown = low;
        bool shadowInFrames = false;
        if (low is not null && low.Animation.Driver == AnimationDriver.Height)
        {
            float lowRange = low.Animation.Frames.Count * low.Animation.HeightStep;
            shadowInFrames = HeightPixels(low.Animation, ball.Z) < lowRange;
            if (!shadowInFrames)
            {
                shown = _animations.Get("ball.high") ?? low;
            }
        }

        if (!shadowInFrames && _animations.Get("ball.shadow") is ResolvedAnimation shadow)
        {
            float lifted = MathF.Max(0f, ball.Z) * view.Scale * view.HeightScale;
            IReadOnlyList<float> shift = _display.Sprites.BallShadowShift;
            float shrink = _display.Sprites.BallShadowShrinks ? MathF.Max(0.35f, 1f - (ball.Z / 8f)) : 1f;
            Vector2 at = Snap(ground + new Vector2(shift[0] * lifted, shift[1] * lifted));
            DrawSprite(batch, textures, shadow, null, at, 0, 0, SpriteScale(shadow.Animation, view) * shrink, Color.White);
        }

        float radius = world.Content.Physics.Ball.Radius;
        Vector2 position = Snap(view.ToScreen(groundPoint, ball.Z + radius));
        if (shown is null)
        {
            DrawPlaceholder(batch, textures, position, view);
            return;
        }

        int frame = AnimationFrames.FrameNumber(shown.Animation, world.Ball.DistanceTravelled, world.Tick * world.StepSeconds, HeightPixels(shown.Animation, ball.Z));
        DrawSprite(batch, textures, shown, null, position, frame, 0, SpriteScale(shown.Animation, view), Color.White);
    }

    /// <summary>Displayed height in sprite pixels (zoom 1) of something at <paramref name="z"/> metres.</summary>
    private float HeightPixels(AnimationDefinition animation, float z) =>
        MathF.Max(0f, z) * SheetPixelsPerMetre(animation) * _display.HeightScale;

    private float SheetPixelsPerMetre(AnimationDefinition animation) =>
        animation.PixelsPerMetre > 0f ? animation.PixelsPerMetre : _display.PixelsPerMetre;

    private float SpriteScale(AnimationDefinition animation, ViewTransform view) => view.Scale / SheetPixelsPerMetre(animation);

    private static void DrawSprite(SpriteBatch batch, TextureStore textures, ResolvedAnimation resolved, KitDefinition? kit, Vector2 at, int frame, int direction, float scale, Color color)
    {
        AnimationDefinition animation = resolved.Animation;
        TextureStore store = textures.ForLook(resolved.FromDefaultLook);
        Texture2D texture = kit is null ? store.Get(animation.Texture) : store.GetKit(animation.Texture, animation.Palette, kit);
        (int x, int y) = AnimationFrames.FrameOrigin(animation, frame, direction, texture.Width);
        var source = new Rectangle(x, y, animation.FrameWidth, animation.FrameHeight);
        var origin = new Vector2(animation.Anchor[0] - animation.Offset[0], animation.Anchor[1] - animation.Offset[1]);
        batch.Draw(texture, at, source, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    /// <summary>Visible stand-in when no animation at all is available for an entity.</summary>
    private void DrawPlaceholder(SpriteBatch batch, TextureStore textures, Vector2 at, ViewTransform view)
    {
        Texture2D placeholder = textures.Placeholder;
        float scale = view.Scale / _display.PixelsPerMetre;
        batch.Draw(placeholder, at, null, Color.White, 0f, new Vector2(placeholder.Width / 2f, placeholder.Height), scale, SpriteEffects.None, 0f);
    }

    private static Vector2 Snap(Vector2 point) => new(MathF.Round(point.X), MathF.Round(point.Y));

    /// <summary>Gauge value of a controller: pass aiming time or restart charge, or null.</summary>
    private static float? Charge(FootballWorld world, ContextualController controller)
    {
        if (controller.Phase == ControlPhase.Holding && controller.AimSeconds > 0f)
        {
            return controller.AimSeconds / MathF.Max(0.01f, world.Controls.Pass.ChargeTime);
        }

        return controller.Phase == ControlPhase.SetPiece ? controller.SetPieceCharge : null;
    }

    private static void DrawSelection(SpriteBatch batch, TextureStore textures, Player player, float alpha, ViewTransform view, float scale, float markerHeight, float? passCharge, Color color)
    {
        Texture2D marker = textures.Get(SelectionTexture);
        Vector2 top = Snap(view.ToScreen(Num.Vector2.Lerp(player.PreviousPosition, player.Position, alpha), markerHeight));
        batch.Draw(marker, top, null, color, 0f, new Vector2(marker.Width / 2f, marker.Height), scale, SpriteEffects.None, 0f);
        if (passCharge is float charge)
        {
            float width = 16f * scale;
            float height = 2f * scale;
            var bar = new RectangleF(top.X - (width / 2f), top.Y - (marker.Height * scale) - (4f * scale), width, height);
            Shapes.Rectangle(batch, textures.Pixel, bar, Color.Black * 0.6f);
            Shapes.Rectangle(batch, textures.Pixel, bar with { Width = width * Math.Clamp(charge, 0f, 1f) }, color);
        }
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
