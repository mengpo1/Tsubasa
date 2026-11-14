namespace Arcade.Core.Content;

/// <summary>Frame selection and sheet coordinates of an <see cref="AnimationDefinition"/>. Pure functions, testable without graphics.</summary>
public static class AnimationFrames
{
    /// <summary>Position in <see cref="AnimationDefinition.Frames"/> to show.</summary>
    /// <param name="distance">Distance travelled by the entity (m), for distance-driven animations.</param>
    /// <param name="seconds">Time (s), for time-driven animations.</param>
    /// <param name="heightPixels">Displayed height (pixels), for height-driven animations.</param>
    public static int FrameNumber(AnimationDefinition animation, float distance, float seconds, float heightPixels)
    {
        ArgumentNullException.ThrowIfNull(animation);
        int count = animation.Frames.Count;
        AnimationDriver driver = animation.Driver == AnimationDriver.Auto
            ? (animation.ReferenceSpeed > 0f ? AnimationDriver.Distance : AnimationDriver.Time)
            : animation.Driver;
        return driver switch
        {
            AnimationDriver.Height => Math.Clamp((int)(MathF.Max(0f, heightPixels) / animation.HeightStep), 0, count - 1),
            AnimationDriver.Distance when animation.ReferenceSpeed > 0f => Wrap((int)(distance / (animation.ReferenceSpeed * animation.FrameDuration)), count),
            _ => Wrap((int)(seconds / animation.FrameDuration), count),
        };
    }

    /// <summary>Top-left pixel of a frame in the sheet.</summary>
    /// <param name="frameNumber">Position in <see cref="AnimationDefinition.Frames"/>.</param>
    /// <param name="direction">Direction index 0..7 (0 = up, clockwise), ignored for one-direction animations.</param>
    /// <param name="textureWidth">Width of the sheet, used when <see cref="AnimationDefinition.SheetColumns"/> is 0.</param>
    public static (int X, int Y) FrameOrigin(AnimationDefinition animation, int frameNumber, int direction, int textureWidth)
    {
        ArgumentNullException.ThrowIfNull(animation);
        int frame = animation.Frames[Wrap(frameNumber, animation.Frames.Count)];
        int row = animation.Directions == 8 ? direction : 0;
        if (animation.DirectionStride <= 0)
        {
            return (frame * animation.FrameWidth, row * animation.FrameHeight);
        }

        int columns = animation.SheetColumns > 0 ? animation.SheetColumns : Math.Max(1, textureWidth / animation.FrameWidth);
        int index = frame + (row * animation.DirectionStride);
        return (index % columns * animation.FrameWidth, index / columns * animation.FrameHeight);
    }

    private static int Wrap(int value, int count) => ((value % count) + count) % count;
}
