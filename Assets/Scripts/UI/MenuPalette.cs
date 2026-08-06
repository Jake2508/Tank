using UnityEngine;

/// <summary>
/// Colours for the three menus that sit on top of gameplay - upgrade selection,
/// pause and game over (MENUS_IMPLEMENTATION.md section 0).
///
/// Same seven tokens as the rest of the game. Kept separate from HudPalette because
/// these screens use their own alphas throughout: the HUD's job is to stay legible
/// over terrain, while a menu is drawing over its own scrim and can sit heavier.
///
/// Alarm appears here only on the game-over verdict. Red on screen means one of two
/// things - low hull, or the run is over - and never anything else.
/// </summary>
public static class MenuPalette
{
    // ---- bases ---------------------------------------------------------------
    public static readonly Color Cream = new Color32(244, 238, 220, 255);
    public static readonly Color Ink = new Color32(31, 23, 18, 255);
    public static readonly Color DeepInk = new Color32(21, 16, 11, 255);
    public static readonly Color Terracotta = new Color32(196, 85, 47, 255);
    public static readonly Color Yellow = new Color32(233, 201, 63, 255);
    public static readonly Color Alarm = new Color32(228, 67, 42, 255);

    /// <summary>
    /// The drop shadow behind cards, buttons and numerals. Deliberately darker than
    /// DeepInk - it reads as a shadow rather than as another panel.
    /// </summary>
    public static readonly Color Shadow = new Color32(16, 10, 6, 140);
    public static readonly Color ShadowSoft = new Color32(16, 10, 6, 115);
    public static readonly Color ShadowFocus = new Color32(16, 10, 6, 128);
    public static readonly Color ShadowNumeral = new Color32(16, 10, 6, 153);

    public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    // ---- scrims --------------------------------------------------------------
    // One ink, three weights. The upgrade screen owns the whole frame, game over is
    // a shade lighter so the wreck still reads through it, and pause is lightest of
    // all because half of pausing is looking at the board.
    public static readonly Color ScrimUpgrade = new Color32(18, 12, 8, 184);
    public static readonly Color ScrimGameOver = new Color32(18, 12, 8, 168);
    public static readonly Color ScrimPause = new Color32(18, 12, 8, 89);

    // ---- upgrade cards -------------------------------------------------------
    public static readonly Color CardBody = new Color32(31, 23, 18, 240);
    public static readonly Color CardBodySelected = new Color32(31, 23, 18, 247);
    public static readonly Color CardBorder = new Color32(244, 238, 220, 128);
    public static readonly Color CardBorderSelected = Cream;
    public static readonly Color CardRule = new Color32(244, 238, 220, 128);

    public static readonly Color IconPlate = new Color32(21, 16, 11, 255);
    public static readonly Color IconPlateBorder = new Color32(244, 238, 220, 77);
    public static readonly Color IconPlateBorderSelected = new Color32(244, 238, 220, 179);

    public static readonly Color Description = new Color32(244, 238, 220, 204);
    public static readonly Color DescriptionSelected = Cream;

    /// <summary>Icons never carry the upgrade's colour - the card head does that.</summary>
    public static readonly Color IconLocked = new Color32(244, 238, 220, 102);

    // ---- tier chips ----------------------------------------------------------
    public static readonly Color ChipNextKeyline = Cream;
    public static readonly Color ChipNextLabel = Cream;
    public static readonly Color ChipLockedKeyline = new Color32(244, 238, 220, 56);
    public static readonly Color ChipLockedLabel = new Color32(244, 238, 220, 82);

    public static readonly Color KeyChipKeyline = new Color32(244, 238, 220, 64);
    public static readonly Color KeyChipLabel = new Color32(244, 238, 220, 115);

    // ---- buttons -------------------------------------------------------------
    // Pause sits on the band, game over sits on the scrim, so their resting bodies
    // differ by one step. Focused is identical on both: the fill inverts.
    public static readonly Color ButtonPause = new Color32(21, 16, 11, 230);
    public static readonly Color ButtonGameOver = new Color32(31, 23, 18, 235);
    public static readonly Color ButtonFocused = Cream;

    public static readonly Color ButtonKeylinePause = new Color32(244, 238, 220, 140);
    public static readonly Color ButtonKeylineGameOver = new Color32(244, 238, 220, 153);
    public static readonly Color ButtonKeylineFocused = Cream;

    public static readonly Color ButtonLabel = Cream;
    public static readonly Color ButtonLabelFocused = new Color32(31, 23, 18, 255);

    // ---- glows ---------------------------------------------------------------
    // Decoration only. Every focus state must survive with the glow switched off -
    // the fill swap is what actually carries it.
    public static readonly Color GlowCard = new Color32(233, 201, 63, 115);
    public static readonly Color GlowButton = new Color32(233, 201, 63, 102);

    // ---- labels and rules ----------------------------------------------------
    public static readonly Color Subtitle = new Color32(244, 238, 220, 153);
    public static readonly Color HintUpgrade = new Color32(244, 238, 220, 115);
    public static readonly Color HintPause = new Color32(244, 238, 220, 102);
    public static readonly Color HintGameOver = new Color32(244, 238, 220, 89);

    public static readonly Color StatLabel = new Color32(244, 238, 220, 115);
    public static readonly Color StatUnderline = new Color32(244, 238, 220, 41);
    public static readonly Color StripRule = new Color32(244, 238, 220, 102);
    public static readonly Color StripLabel = new Color32(244, 238, 220, 140);

    public static readonly Color PipFilled = Yellow;
    public static readonly Color PipEmpty = new Color32(244, 238, 220, 46);

    /// <summary>Same colour at a different alpha, in 0-255 units to match the spec tables.</summary>
    public static Color At(Color color, int alpha)
    {
        return new Color(color.r, color.g, color.b, alpha / 255f);
    }

    /// <summary>Same colour scaled by a 0-1 factor, for the tier-chip pulse and fades.</summary>
    public static Color Fade(Color color, float factor)
    {
        return new Color(color.r, color.g, color.b, color.a * factor);
    }
}
