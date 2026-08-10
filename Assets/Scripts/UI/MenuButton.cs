using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A slab button on the pause or game-over screen (MENUS_IMPLEMENTATION.md sections
/// 4.2 and 5.3).
///
/// Focusing inverts the fill: an ink body with a cream label becomes a cream body
/// with an ink label. That swap is the whole focus signal - the yellow glow behind it
/// is decoration and the button has to read correctly with it switched off.
///
/// Pause and game over differ only in their resting body and keyline, so the builder
/// sets those two and everything else is shared.
/// </summary>
public class MenuButton : MenuFocusItem
{
    [Header("Refs")]
    [SerializeField] Image fill;
    [SerializeField] Image keyline;
    [SerializeField] Image shadow;
    [SerializeField] Image glow;
    [SerializeField] TextMeshProUGUI label;

    [Header("Resting look")]
    // Defaults are the pause preset; the builder overrides them for the game-over row.
    [SerializeField] Color restFill = new Color32(21, 16, 11, 230);
    [SerializeField] Color restKeyline = new Color32(244, 238, 220, 140);
    [SerializeField] Color restShadow = new Color32(16, 10, 6, 115);

    protected override void Awake()
    {
        base.Awake();
        Render(IsFocused ? 1f : 0f);
    }

    /// <summary>Builder hook - the game-over row rests a step lighter than pause.</summary>
    public void SetRestingLook(Color body, Color line, Color drop)
    {
        restFill = body;
        restKeyline = line;
        restShadow = drop;
        MarkDirty();
    }

    public void SetLabel(string text)
    {
        if (label != null) label.text = text;
    }

    protected override void Render(float t)
    {
        if (fill != null)
            fill.color = Color.Lerp(restFill, MenuPalette.ButtonFocused, t);

        if (keyline != null)
            keyline.color = Color.Lerp(restKeyline, MenuPalette.ButtonKeylineFocused, t);

        if (shadow != null)
            shadow.color = Color.Lerp(restShadow, MenuPalette.ShadowFocus, t);

        if (label != null)
            label.color = Color.Lerp(MenuPalette.ButtonLabel, MenuPalette.ButtonLabelFocused, t);

        if (glow != null)
            glow.color = MenuPalette.Fade(MenuPalette.GlowButton, t);

        // A button that has been switched off keeps its shape but stops advertising
        // itself - it never happens on pause, only on a maxed upgrade's neighbours.
        if (!Interactable && fill != null)
            fill.color = MenuPalette.Fade(fill.color, 0.6f);
    }
}
