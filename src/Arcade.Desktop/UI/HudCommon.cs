using Arcade.Core.Localization;
using Arcade.Desktop.Rendering;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.UI;

/// <summary>HUD pieces shared by every scene.</summary>
internal static class HudCommon
{
    public static void DrawPaused(SpriteBatch batch, Texture2D pixel, FontSet fonts, Localizer localizer, Rectangle screen, float ui)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(localizer);
        Shapes.Rectangle(batch, pixel, new RectangleF(0, 0, screen.Width, screen.Height), Color.Black * 0.4f);
        SpriteFontBase big = fonts.Bold(20f * ui);
        string text = localizer.Get("hud.paused");
        Vector2 size = big.MeasureString(text);
        big.DrawText(batch, text, new Vector2((screen.Width - size.X) / 2f, (screen.Height - size.Y) / 2f), Color.White);
    }
}
