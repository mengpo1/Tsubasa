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

    /// <summary>On-screen width / height of one virtual pixel (1 = square; about 1.07 for a PAL Amiga low-res screen on a 4:3 monitor).</summary>
    public float PixelAspect { get; init; } = 1f;

    /// <summary>Draw the goals with lines and nets. Off when the look's pitch images already contain them.</summary>
    public bool DrawGoals { get; init; } = true;

    public SpriteConfig Sprites { get; init; } = new();

    /// <summary>
    /// When set, a pitch is drawn from the image <c>pitch.bitmap.&lt;pitch id&gt;</c> (if available) instead of
    /// procedurally. Purely visual: the playing area keeps the dimensions of the pitch data.
    /// </summary>
    public PitchBitmapConfig? PitchBitmap { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.InRange(problems, "pixelAspect", PixelAspect, 0.5f, 2f);
        DataChecks.Nested(problems, "sprites", Sprites);
        if (PitchBitmap is not null)
        {
            DataChecks.Nested(problems, "pitchBitmap", PitchBitmap);
        }

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

internal sealed record SpriteConfig : IContentData
{
    /// <summary>Draw a separate shadow under each player (off when the sprites contain their shadow).</summary>
    public bool PlayerShadows { get; init; } = true;

    /// <summary>Ball shadow offset from the ground point, in screen pixels per pixel of displayed height ([0, 0] = straight below).</summary>
    public IReadOnlyList<float> BallShadowShift { get; init; } = [0f, 0f];

    /// <summary>The ball shadow gets smaller as the ball rises.</summary>
    public bool BallShadowShrinks { get; init; } = true;

    /// <summary>Height of the selection marker above the player's ground point (m).</summary>
    public float MarkerHeight { get; init; } = 2.6f;

    public void Validate(ICollection<string> problems)
    {
        if (BallShadowShift.Count != 2)
        {
            problems.Add("ballShadowShift must be [x, y]");
        }

        DataChecks.InRange(problems, "markerHeight", MarkerHeight, 0f, 10f);
    }
}

/// <summary>Placement of full-pitch images: which image pixel is the centre spot, and the image scale.</summary>
internal sealed record PitchBitmapConfig : IContentData
{
    public required IReadOnlyList<float> Centre { get; init; }

    public required float PixelsPerMetre { get; init; }

    public void Validate(ICollection<string> problems)
    {
        if (Centre.Count != 2)
        {
            problems.Add("centre must be [x, y]");
        }

        DataChecks.InRange(problems, "pixelsPerMetre", PixelsPerMetre, 1f, 128f);
    }
}
