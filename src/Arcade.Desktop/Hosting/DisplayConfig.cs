using Arcade.Core.Content;

namespace Arcade.Desktop.Hosting;

/// <summary>Screen and camera settings (<c>config/display.json</c>). Never affects the simulation.</summary>
internal sealed record DisplayConfig : IContentData
{
    public const string FileName = "config/display.json";

    /// <summary>Resolution of the pixel-art world view, scaled up to the window.</summary>
    public required int VirtualWidth { get; init; }

    public required int VirtualHeight { get; init; }

    public required int WindowScale { get; init; }

    /// <summary>World scale at zoom 1.</summary>
    public required float PixelsPerMetre { get; init; }

    /// <summary>Screen pixels per metre of height, relative to <see cref="PixelsPerMetre"/> (oblique view).</summary>
    public required float HeightScale { get; init; }

    public required CameraConfig Camera { get; init; }

    public required RadarConfig Radar { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "virtualWidth", VirtualWidth, 160, 3840);
        DataChecks.InRange(problems, "virtualHeight", VirtualHeight, 120, 2160);
        DataChecks.InRange(problems, "windowScale", WindowScale, 1, 8);
        DataChecks.InRange(problems, "pixelsPerMetre", PixelsPerMetre, 2f, 128f);
        DataChecks.InRange(problems, "heightScale", HeightScale, 0f, 2f);
        DataChecks.Nested(problems, "camera", Camera);
        DataChecks.Nested(problems, "radar", Radar);
    }
}

internal sealed record CameraConfig : IContentData
{
    /// <summary>The camera aims where the ball will be after this time (s).</summary>
    public required float LookAheadSeconds { get; init; }

    public required float MaxLookAhead { get; init; }

    /// <summary>Exponential follow rate (1/s). Higher = tighter.</summary>
    public required float Smoothing { get; init; }

    public required float MinZoom { get; init; }

    public required float MaxZoom { get; init; }

    public required float ZoomStep { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NonNegative(problems, "lookAheadSeconds", LookAheadSeconds);
        DataChecks.NonNegative(problems, "maxLookAhead", MaxLookAhead);
        DataChecks.Positive(problems, "smoothing", Smoothing);
        DataChecks.Positive(problems, "minZoom", MinZoom);
        DataChecks.InRange(problems, "maxZoom", MaxZoom, MinZoom, 16f);
        DataChecks.Positive(problems, "zoomStep", ZoomStep);
    }
}

internal sealed record RadarConfig : IContentData
{
    /// <summary>Width of the radar in virtual pixels.</summary>
    public required int Width { get; init; }

    public required bool Visible { get; init; }

    public required float Opacity { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "width", Width, 20, 400);
        DataChecks.InRange(problems, "opacity", Opacity, 0f, 1f);
    }
}
