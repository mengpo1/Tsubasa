using System.Globalization;
using System.Text;
using Arcade.Core.Football;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Players;
using Arcade.Desktop.Rendering;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Num = System.Numerics;

namespace Arcade.Desktop.UI;

/// <summary>
/// Optional developer view (F1): contact radii, velocity vectors, predicted ball path, controller state,
/// timings and recent events. Only reads the world; the prediction runs on a copy of the ball state.
/// Developer-facing, so its labels are deliberately not translated.
/// </summary>
internal sealed class DebugOverlay
{
    private const int PredictionSteps = 150;
    private const int MaxEvents = 10;

    private readonly Queue<string> _events = new();
    private readonly StringBuilder _text = new(1024);
    private readonly Vector2[] _prediction = new Vector2[PredictionSteps / 3];
    private double _fps;
    private double _simulationMilliseconds;

    public bool Enabled { get; set; }

    public void RecordFrame(double frameSeconds, double simulationMilliseconds)
    {
        if (frameSeconds > 0)
        {
            _fps = (_fps * 0.9) + (0.1 / frameSeconds);
        }

        _simulationMilliseconds = (_simulationMilliseconds * 0.9) + (0.1 * simulationMilliseconds);
    }

    public void OnEvent(MatchEvent matchEvent)
    {
        string detail = matchEvent.Type switch
        {
            MatchEventType.BallTouched => ((BallTouchKind)matchEvent.Detail).ToString(),
            MatchEventType.BallKicked => string.Create(CultureInfo.InvariantCulture, $"{(KickKind)matchEvent.Detail} {matchEvent.Value:0.0} m/s"),
            MatchEventType.BallBounced => string.Create(CultureInfo.InvariantCulture, $"{matchEvent.Value:0.0} m/s"),
            _ => string.Empty,
        };
        _events.Enqueue(string.Create(CultureInfo.InvariantCulture, $"{matchEvent.Tick,6} {matchEvent.Type} {detail}"));
        while (_events.Count > MaxEvents)
        {
            _events.Dequeue();
        }
    }

    public void DrawWorld(SpriteBatch batch, Texture2D pixel, FootballWorld world, ContextualController? controller, float alpha, ViewTransform view)
    {
        if (!Enabled)
        {
            return;
        }

        float foot = world.Controls.Contact.FootRadius * view.Scale;
        foreach (Player player in world.Players)
        {
            Vector2 at = view.ToScreen(Num.Vector2.Lerp(player.PreviousPosition, player.Position, alpha));
            Shapes.Arc(batch, pixel, at, foot, 0f, MathF.Tau, 1f, Color.Cyan * 0.8f);
            Vector2 velocity = new(player.Velocity.X * view.Scale * 0.25f, player.Velocity.Y * view.Scale * 0.25f);
            Shapes.Line(batch, pixel, at, at + velocity, 1f, Color.LimeGreen);
            Num.Vector2 facing = Arcade.Core.Input.Direction8Extensions.ToVector(player.Facing);
            Shapes.Line(batch, pixel, at, at + new Vector2(facing.X, facing.Y) * (foot * 1.5f), 1f, Color.Orange);
        }

        // Predicted ball path (no players, no random bumps): where the ball goes if nobody touches it.
        BallState state = world.Ball.Current;
        int count = 0;
        float h = world.StepSeconds;
        for (int i = 0; i < PredictionSteps; i++)
        {
            world.BallPhysics.Integrate(ref state, world.Surface, world.Wind, Num.Vector3.Zero, h, null);
            GoalFrameCollider.Resolve(ref state, world.Pitch, world.Content.Physics.GoalFrame, world.Content.Physics.Boards, world.Content.Physics.Ball.Radius);
            if (i % 3 == 0 && count < _prediction.Length)
            {
                _prediction[count++] = view.ToScreen(new Num.Vector2(state.Position.X, state.Position.Y), state.Position.Z);
            }
        }

        for (int i = 0; i < count; i++)
        {
            Shapes.Rectangle(batch, pixel, new RectangleF(_prediction[i].X - 1f, _prediction[i].Y - 1f, 2f, 2f), Color.Yellow * (1f - (i / (float)count)));
        }

        Num.Vector3 ball = Num.Vector3.Lerp(world.Ball.PreviousPosition, world.Ball.Position, alpha);
        Vector2 ballAt = view.ToScreen(new Num.Vector2(ball.X, ball.Y), ball.Z);
        Vector2 ballVelocity = new(world.Ball.Velocity.X * view.Scale * 0.1f, (world.Ball.Velocity.Y - (world.Ball.Velocity.Z * view.HeightScale)) * view.Scale * 0.1f);
        Shapes.Line(batch, pixel, ballAt, ballAt + ballVelocity, 1f, Color.Yellow);
        if (world.Ball.Aftertouch.IsActive)
        {
            Shapes.Arc(batch, pixel, ballAt, 4f, 0f, MathF.Tau, 1f, Color.Magenta);
        }

        if (controller is not null)
        {
            Vector2 player = view.ToScreen(controller.Player.Position);
            Shapes.Arc(batch, pixel, player, world.Controls.Shot.Reach * view.Scale, 0f, MathF.Tau, 1f, Color.Red * 0.4f);
        }
    }

    public void DrawText(SpriteBatch batch, Texture2D pixel, SpriteFontBase font, FootballWorld world, ContextualController? controller, IReadOnlyList<string> packs, float x, float y)
    {
        if (!Enabled)
        {
            return;
        }

        _text.Clear();
        CultureInfo c = CultureInfo.InvariantCulture;
        _text.Append(c, $"DEBUG  fps {_fps:0}  sim {_simulationMilliseconds:0.00} ms/frame  tick {world.Tick}\n");
        _text.Append(c, $"surface {world.Surface.Id}  wind {world.Wind.X:0.0},{world.Wind.Y:0.0}  packs {string.Join(" > ", packs)}\n");
        if (controller is not null)
        {
            Player p = controller.Player;
            PlayerIntent intent = p.CurrentIntent;
            _text.Append(c, $"player #{p.Profile.Number} {p.Profile.Name}  pos {p.Position.X:0.00},{p.Position.Y:0.00}  v {p.Speed:0.00} m/s  energy {p.Energy:0.00}\n");
            _text.Append(c, $"phase {controller.Phase}  decision {controller.LastDecision}  intent move {intent.Move} face {intent.Face} trap {intent.TrapReady} keep {intent.KeepsBall}\n");
            _text.Append(c, $"last touch {p.LastTouchKind} {world.SecondsSince(p.LastTouchTick):0.00}s ago  aim {controller.AimSeconds:0.00}s\n");
        }

        Num.Vector3 b = world.Ball.Position;
        Num.Vector3 v = world.Ball.Velocity;
        _text.Append(c, $"ball pos {b.X:0.00},{b.Y:0.00} h {b.Z:0.00}  v {v.X:0.0},{v.Y:0.0},{v.Z:0.0} ({v.Length():0.0} m/s)  toucher {world.Ball.LastToucherId}  aftertouch {world.Ball.Aftertouch.IsActive}\n");
        foreach (string line in _events)
        {
            _text.Append(line).Append('\n');
        }

        string text = _text.ToString();
        Vector2 size = font.MeasureString(text);
        Shapes.Rectangle(batch, pixel, new RectangleF(x - 4f, y - 4f, size.X + 8f, size.Y + 8f), Color.Black * 0.6f);
        font.DrawText(batch, text, new Vector2(x, y), Color.LightGreen);
    }
}
