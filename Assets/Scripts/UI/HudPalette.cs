using UnityEngine;

/// <summary>
/// The in-game HUD's colours, in one place because the controller lerps between
/// them at runtime and the builder authors them into the scene - the menu screens
/// each keep their own copy, but nothing there has to animate a tint.
///
/// Same five colours as the menus, plus Alarm, which is reserved for the low-hull
/// state so that red on screen only ever means one thing.
/// </summary>
public static class HudPalette
{
    public static readonly Color Cream = new Color32(244, 238, 220, 255);
    public static readonly Color CreamLabel = new Color32(244, 238, 220, 179);
    public static readonly Color CreamDim = new Color32(244, 238, 220, 128);
    public static readonly Color CreamFaint = new Color32(244, 238, 220, 64);
    public static readonly Color CreamTrough = new Color32(244, 238, 220, 31);
    public static readonly Color CreamPipEmpty = new Color32(244, 238, 220, 51);
    public static readonly Color CreamLock = new Color32(244, 238, 220, 107);

    public static readonly Color Ink = new Color32(31, 23, 18, 225);
    public static readonly Color DeepInk = new Color32(21, 16, 11, 204);
    public static readonly Color DeepInkSlot = new Color32(21, 16, 11, 217);
    public static readonly Color DeepInkLocked = new Color32(21, 16, 11, 153);
    public static readonly Color DeepInkChip = new Color32(21, 16, 11, 217);
    public static readonly Color Shadow = new Color32(21, 16, 11, 115);
    public static readonly Color ShadowSoft = new Color32(21, 16, 11, 100);

    public static readonly Color Terracotta = new Color32(196, 85, 47, 255);
    public static readonly Color Yellow = new Color32(233, 201, 63, 255);
    public static readonly Color Alarm = new Color32(228, 67, 42, 255);

    /// <summary>
    /// The bottom-edge darkening. Its sprite carries a 0-1 alpha ramp, so this
    /// alpha is what holds the darkest end to the 50% the spec allows.
    /// </summary>
    public static readonly Color Scrim = new Color32(20, 14, 10, 128);

    public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    /// <summary>Same colour at a different alpha, in 0-255 units to match the spec tables.</summary>
    public static Color At(Color color, int alpha)
    {
        return new Color(color.r, color.g, color.b, alpha / 255f);
    }

    /// <summary>Same colour scaled by a 0-1 factor, for the low-hull pulse.</summary>
    public static Color Fade(Color color, float factor)
    {
        return new Color(color.r, color.g, color.b, color.a * factor);
    }
}
