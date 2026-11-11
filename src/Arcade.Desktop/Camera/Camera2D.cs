using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Pitch;
using Arcade.Desktop.Hosting;

namespace Arcade.Desktop.Camera;

/// <summary>
/// Follows the ball with a little look-ahead, smoothly and independently of the simulation: it only reads
/// positions and never changes them. Runs at the render frame rate with exponential smoothing.
/// </summary>
internal sealed class Camera2D
{
    private readonly CameraConfig _config;

    public Camera2D(CameraConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public Vector2 Centre { get; private set; }

    public float Zoom { get; private set; } = 1f;

    public void SnapTo(Vector2 position) => Centre = position;

    public void ChangeZoom(int steps) => Zoom = Math.Clamp(Zoom + (steps * _config.ZoomStep), _config.MinZoom, _config.MaxZoom);

    /// <param name="viewSize">Visible area in metres at the current zoom.</param>
    public void Update(float realSeconds, Vector2 target, Vector2 targetVelocity, PitchGeometry pitch, Vector2 viewSize)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        Vector2 lead = VectorMath.ClampLength(targetVelocity * _config.LookAheadSeconds, _config.MaxLookAhead);
        Vector2 goal = Clamp(target + lead, pitch, viewSize);
        float blend = 1f - MathF.Exp(-_config.Smoothing * realSeconds);
        Centre = Clamp(Vector2.Lerp(Centre, goal, blend), pitch, viewSize);
    }

    private static Vector2 Clamp(Vector2 position, PitchGeometry pitch, Vector2 viewSize)
    {
        float maxX = MathF.Max(0f, pitch.BoardsHalfWidth - (viewSize.X / 2f));
        float maxY = MathF.Max(0f, pitch.BoardsHalfLength - (viewSize.Y / 2f));
        return new Vector2(Math.Clamp(position.X, -maxX, maxX), Math.Clamp(position.Y, -maxY, maxY));
    }
}
