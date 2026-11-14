using Microsoft.Xna.Framework;

namespace Arcade.Desktop.Rendering;

/// <summary>
/// World (metres) to virtual screen (pixels) projection: top-down with heights drawn upwards (oblique view).
/// The camera centre is snapped to whole pixels so that pixel art does not shimmer.
/// </summary>
internal readonly struct ViewTransform
{
    public ViewTransform(System.Numerics.Vector2 cameraCentre, float pixelsPerMetre, float heightScale, Point screenSize)
    {
        Scale = pixelsPerMetre;
        HeightScale = heightScale;
        ScreenSize = screenSize;
        CameraCentre = new System.Numerics.Vector2(
            MathF.Round(cameraCentre.X * pixelsPerMetre) / pixelsPerMetre,
            MathF.Round(cameraCentre.Y * pixelsPerMetre) / pixelsPerMetre);
    }

    public System.Numerics.Vector2 CameraCentre { get; }

    /// <summary>Pixels per metre, zoom included.</summary>
    public float Scale { get; }

    public float HeightScale { get; }

    public Point ScreenSize { get; }

    public Vector2 ToScreen(System.Numerics.Vector2 world, float height = 0f) => new(
        ((world.X - CameraCentre.X) * Scale) + (ScreenSize.X / 2f),
        ((world.Y - CameraCentre.Y) * Scale) + (ScreenSize.Y / 2f) - (height * Scale * HeightScale));

    public Vector2 ToScreen(float x, float y, float height = 0f) => ToScreen(new System.Numerics.Vector2(x, y), height);

    /// <summary>World rectangle visible on screen (ground plane), in metres.</summary>
    public (float Left, float Top, float Right, float Bottom) VisibleWorld()
    {
        float halfWidth = ScreenSize.X / 2f / Scale;
        float halfHeight = ScreenSize.Y / 2f / Scale;
        return (CameraCentre.X - halfWidth, CameraCentre.Y - halfHeight, CameraCentre.X + halfWidth, CameraCentre.Y + halfHeight);
    }
}
