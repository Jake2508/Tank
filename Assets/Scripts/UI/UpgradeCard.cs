using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One of the four columns on the upgrade screen (MENUS_IMPLEMENTATION.md section 3.2).
///
/// Three things move this card and they compose rather than fight: the focus tween
/// from <see cref="MenuFocusItem"/> lifts it 10px and warms its head, the open/take
/// animation drives a separate offset, scale and alpha, and the tier-chip pulse runs
/// off the shared clock. All three are folded together in <see cref="ApplyTransform"/>,
/// so nothing ever stamps on another's transform write.
///
/// The card sprites are all 234 wide and lean +-19px; every other child is upright and
/// stays inside the middle 164px so it never crosses a slanted edge.
/// </summary>
public class UpgradeCard : MenuFocusItem
{
    [Header("Identity")]
    [SerializeField] UpgradeType type;

    [Header("Body")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Image body;
    [SerializeField] Image border;
    [SerializeField] Image head;
    [SerializeField] Image rule;
    [SerializeField] Image glow;
    [SerializeField] TextMeshProUGUI headLabel;

    [Header("Icon")]
    [SerializeField] Image iconPlate;
    [SerializeField] Image iconPlateBorder;
    [SerializeField] Image icon;

    [Header("Copy")]
    [SerializeField] TextMeshProUGUI description;

    [Header("Footer")]
    [SerializeField] TextMeshProUGUI keyChipLabel;
    [SerializeField] Image keyChipKeyline;
    [SerializeField] Image[] tierFills = new Image[3];
    [SerializeField] Image[] tierKeylines = new Image[3];
    [SerializeField] TextMeshProUGUI[] tierLabels = new TextMeshProUGUI[3];

    // ---- timings, seconds ----------------------------------------------------
    const float FocusRise = 10f;
    const float IntroRise = 26f;
    const float IntroSeconds = 0.18f;

    const float TakeSwellSeconds = 0.08f;
    const float TakeSwellPeak = 1.06f;
    const float TakeFallSeconds = 0.14f;
    const float TakeFallScale = 0.9f;

    const float DismissSeconds = 0.12f;

    /// <summary>
    /// Same 1.1s cycle and 74% floor as the HUD's low-hull state. Sampled from the
    /// shared clock rather than owned by a coroutine, so every "next" chip on screen
    /// is in step by construction.
    /// </summary>
    const float PulsePeriod = 1.1f;
    const float PulseFloor = 0.74f;

    enum Phase { Idle, Intro, Taken, Dismissed }

    Phase phase = Phase.Idle;
    float phaseStart;
    float phaseDelay;

    Vector2 home;
    float focusT;
    float animY, animScale = 1f, animAlpha = 1f;

    int tier;
    bool posed;

    public UpgradeType Type => type;
    public int Tier => tier;

    /// <summary>A card already at tier 3 is drawn, but cannot be taken again.</summary>
    public bool Maxed => tier >= tierFills.Length;

    protected override void Awake()
    {
        base.Awake();
        home = Rect.anchoredPosition;
        Render(0f);
    }

    /// <summary>
    /// Fills the card in for the run's current state. <paramref name="ownedTier"/> is
    /// how many tiers the player already has, 0-3.
    /// </summary>
    public void SetOffer(int ownedTier, string nextDescription, int keyNumber)
    {
        tier = Mathf.Clamp(ownedTier, 0, tierFills.Length);

        if (description != null) description.text = nextDescription;
        if (keyChipLabel != null) keyChipLabel.text = keyNumber.ToString();

        Interactable = !Maxed;
        MarkDirty();
    }

    public void SetHeadLabel(string text)
    {
        if (headLabel != null) headLabel.text = text;
    }

    // =========================================================================
    // Animation - driven by MenuController's open and take sequences
    // =========================================================================

    /// <summary>Rise and fade in after <paramref name="delay"/> seconds.</summary>
    public void PlayIntro(float delay)
    {
        phase = Phase.Intro;
        phaseStart = Time.unscaledTime;
        phaseDelay = delay;

        animY = -IntroRise;
        animScale = 1f;
        animAlpha = 0f;
        ApplyTransform();
    }

    /// <summary>The chosen card: a small swell, then away.</summary>
    public void PlayTaken()
    {
        phase = Phase.Taken;
        phaseStart = Time.unscaledTime;
        phaseDelay = 0f;
    }

    /// <summary>The other three: straight out, no motion, so the eye stays on the pick.</summary>
    public void PlayDismissed()
    {
        phase = Phase.Dismissed;
        phaseStart = Time.unscaledTime;
        phaseDelay = 0f;
    }

    /// <summary>Snap to the resting pose - used when the screen opens fresh.</summary>
    public void ResetPose()
    {
        phase = Phase.Idle;
        animY = 0f;
        animScale = 1f;
        animAlpha = 1f;
        ApplyTransform();
        MarkDirty();
    }

    protected override void Tick(float dt)
    {
        // A pulsing "next" chip means this card has to redraw every frame; a resting
        // one does not, and most of the time nothing on screen is moving at all.
        if (IsFocused && !Maxed) MarkDirty();

        if (phase == Phase.Idle) return;

        float t = Time.unscaledTime - phaseStart - phaseDelay;
        if (t < 0f) return;

        switch (phase)
        {
            case Phase.Intro:
            {
                float k = Mathf.Clamp01(t / IntroSeconds);
                float eased = 1f - (1f - k) * (1f - k);          // ease out
                animY = Mathf.Lerp(-IntroRise, 0f, eased);
                animAlpha = k;
                if (k >= 1f) phase = Phase.Idle;
                break;
            }

            case Phase.Taken:
            {
                if (t <= TakeSwellSeconds)
                {
                    animScale = Mathf.Lerp(1f, TakeSwellPeak, t / TakeSwellSeconds);
                    animAlpha = 1f;
                }
                else
                {
                    float k = Mathf.Clamp01((t - TakeSwellSeconds) / TakeFallSeconds);
                    animScale = Mathf.Lerp(TakeSwellPeak, TakeFallScale, k);
                    animAlpha = 1f - k;
                    if (k >= 1f) phase = Phase.Idle;
                }
                break;
            }

            case Phase.Dismissed:
            {
                float k = Mathf.Clamp01(t / DismissSeconds);
                animAlpha = 1f - k;
                if (k >= 1f) phase = Phase.Idle;
                break;
            }
        }

        ApplyTransform();
    }

    // =========================================================================
    // Draw
    // =========================================================================

    protected override void Render(float t)
    {
        focusT = t;

        if (body != null)
            body.color = Color.Lerp(MenuPalette.CardBody, MenuPalette.CardBodySelected, t);

        if (border != null)
            border.color = Color.Lerp(MenuPalette.CardBorder, MenuPalette.CardBorderSelected, t);

        // The head carries the upgrade's colour, which is why the icon never does.
        if (head != null)
            head.color = Color.Lerp(MenuPalette.Terracotta, MenuPalette.Yellow, t);

        if (headLabel != null)
            headLabel.color = Color.Lerp(MenuPalette.Cream, MenuPalette.Ink, t);

        if (rule != null) rule.color = MenuPalette.CardRule;

        if (iconPlate != null) iconPlate.color = MenuPalette.IconPlate;

        if (iconPlateBorder != null)
            iconPlateBorder.color = Color.Lerp(MenuPalette.IconPlateBorder,
                                               MenuPalette.IconPlateBorderSelected, t);

        if (icon != null)
            icon.color = Maxed ? MenuPalette.IconLocked : MenuPalette.Cream;

        if (description != null)
            description.color = Color.Lerp(MenuPalette.Description,
                                           MenuPalette.DescriptionSelected, t);

        if (glow != null)
            glow.color = MenuPalette.Fade(MenuPalette.GlowCard, t);

        if (keyChipKeyline != null) keyChipKeyline.color = MenuPalette.KeyChipKeyline;
        if (keyChipLabel != null) keyChipLabel.color = MenuPalette.KeyChipLabel;

        DrawTiers();
        ApplyTransform();
    }

    void DrawTiers()
    {
        // Floored at 74%: this is the only thing moving while the player reads, and it
        // must never dim far enough to be mistaken for a disabled chip.
        float pulse = posed
            ? 1f
            : Mathf.Lerp(PulseFloor, 1f,
                         0.5f + 0.5f * Mathf.Cos(Time.unscaledTime / PulsePeriod * Mathf.PI * 2f));

        for (int i = 0; i < tierFills.Length; i++)
        {
            bool owned = i < tier;
            // "Next" is a selected-card state only - an unselected card's next chip
            // sits quiet as locked, or four cards would all be pulsing at once.
            bool next = i == tier && !Maxed && IsFocused;

            Color fill, keyline, label;

            if (owned)
            {
                fill = MenuPalette.Yellow;
                keyline = MenuPalette.Clear;
                label = MenuPalette.Ink;
            }
            else if (next)
            {
                fill = MenuPalette.Clear;
                keyline = MenuPalette.Fade(MenuPalette.ChipNextKeyline, pulse);
                label = MenuPalette.Fade(MenuPalette.ChipNextLabel, pulse);
            }
            else
            {
                fill = MenuPalette.Clear;
                keyline = MenuPalette.ChipLockedKeyline;
                label = MenuPalette.ChipLockedLabel;
            }

            if (tierFills[i] != null) tierFills[i].color = fill;
            if (tierKeylines[i] != null) tierKeylines[i].color = keyline;
            if (tierLabels[i] != null) tierLabels[i].color = label;
        }
    }

    /// <summary>
    /// The single place the card's transform is written. Focus lift and animation
    /// offset are added, never assigned, so a card taken mid-focus does not snap.
    /// </summary>
    void ApplyTransform()
    {
        Rect.anchoredPosition = home + new Vector2(0f, focusT * FocusRise + animY);
        Rect.localScale = Vector3.one * animScale;
        if (group != null) group.alpha = animAlpha;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: snap the card to a state for a layout capture, past every
    /// animation and with the chip pulse held at full so the PNG is deterministic.
    /// </summary>
    public void EditorPose(int ownedTier, string nextDescription, int keyNumber, bool focused)
    {
        // Awake has not run in edit mode, so the resting position has to be picked up
        // here or ApplyTransform would drag the card to the canvas origin. Any offset
        // left over from a previous pose is backed out first - CaptureAll poses the same
        // cards eight times in one session, and re-reading a raised card's position as
        // its home would ratchet it 10px higher every time it was focused.
        home = Rect.anchoredPosition - new Vector2(0f, focusT * FocusRise + animY);

        posed = true;
        SetOffer(ownedTier, nextDescription, keyNumber);
        SnapFocus(focused);

        phase = Phase.Idle;
        animY = 0f;
        animScale = 1f;
        animAlpha = 1f;

        // IsFocused, not the requested flag: SnapFocus refuses focus on a maxed card,
        // and drawing the request would show a highlight the player can never put there.
        Render(IsFocused ? 1f : 0f);
        posed = false;
    }
#endif
}
