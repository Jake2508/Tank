using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The in-game HUD, built to the "Decals" spec (HUD_3B_IMPLEMENTATION.md).
/// Owns no gameplay state: the timer, the kill counter, the tank and the level
/// script push their values in, and this renders them.
///
/// Every animation runs on unscaled time, because the two states that most need the
/// HUD to keep moving - the pause menu and the upgrade picker - both set timeScale
/// to zero.
///
/// All offsets are in 960x540 canvas units.
/// </summary>
public class HudController : MonoBehaviour
{
    public static HudController Instance { get; private set; }

    [Header("Hull")]
    [SerializeField] RectTransform hullFill;          // width driven; the stripe tiles inside it
    [SerializeField] Image hullStripe;
    [SerializeField] Image hullKeyline;
    [SerializeField] TextMeshProUGUI hullValue;
    [SerializeField] TextMeshProUGUI hullLabel;
    [SerializeField] Image hullLabelChip;
    [SerializeField] Image hullLabelChipShadow;
    [SerializeField] Image hullValueChip;
    [SerializeField] Image hullValueChipShadow;
    [SerializeField] CanvasGroup lowHullVignette;
    [SerializeField] float hullTrackWidth = 420f;

    [Header("Progress")]
    [SerializeField] RectTransform xpFill;
    [SerializeField] RectTransform levelChip;
    [SerializeField] TextMeshProUGUI levelValue;
    [SerializeField] float xpTrackWidth = 312f;

    [Header("Stats")]
    [SerializeField] TextMeshProUGUI timeValue;
    [SerializeField] RectTransform killNumeral;
    [SerializeField] TextMeshProUGUI killValue;

    [Header("Upgrades")]
    [SerializeField] UpgradeSlot[] slots;             // Armor, Turret, Tracks, Magnet - fixed order

    [Header("Drift")]
    [SerializeField] Image[] driftPips;
    [SerializeField] Image driftBody;
    [SerializeField] Image driftKeyline;
    [SerializeField] Image driftGlow;
    [SerializeField] TextMeshProUGUI driftChevron;
    [SerializeField] TextMeshProUGUI driftLabel;

    // ---- timings, seconds ----------------------------------------------------
    const float HullLerp = 0.12f;
    const float HealLerp = 0.2f;
    const float XpLerp = 0.1f;
    const float KeylineFlash = 0.18f;
    const float KillPunch = 0.14f;
    const float LevelPunch = 0.2f;
    const float DimFade = 0.15f;

    const float KillPeak = 1.18f;
    const float LevelPeak = 1.3f;

    // ---- low hull ------------------------------------------------------------
    const float LowEnter = 0.25f;
    const float LowExit = 0.3f;                       // hysteresis, or it strobes at the boundary
    const float PulsePeriod = 1.1f;
    const float PulseFloor = 0.74f;                   // critical text must never become unreadable
    const float VignetteAlpha = 0.5f;
    const float DimAlpha = 0.25f;

    const float DriftPulsePeriod = 0.5f;

    CanvasGroup group;

    float hull = 1f, hullTarget = 1f, hullSpeed = 1f / HullLerp;
    float xp, xpTarget;
    float flashAt = float.NegativeInfinity;
    float killPunchAt = float.NegativeInfinity;
    float levelPunchAt = float.NegativeInfinity;
    bool lowHull;

    float dimTarget = 1f;
    bool hidden;

    bool drifting;
    float driftCharge;

    int shownMinutes = -1, shownSeconds = -1;
    float posedPulse = -1f;                           // editor capture only

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.interactable = false;
            group.blocksRaycasts = false;             // the HUD is read-only; never eat a click
        }
    }

    void Start()
    {
        var game = GameManager.Instance;
        if (game != null)
        {
            game.OnGamePaused += OnDim;
            game.OnGameResumed += OnUndim;
            game.OnLevelUp += OnDim;
            game.OnLevelUpSelected += OnUndim;
            // Not dimmed - hidden. The run is finished and the hull bar is noise.
            game.OnDeath += OnHide;
            game.OnWin += OnHide;
        }

        // The upgrade row is pushed by MenuController via SetUpgrade now, rather than
        // counting its own tiers off the old UpgradeUI's events.
        Redraw();
    }

    void OnDestroy()
    {
        // GameManager survives scene loads, so a HUD that just unsubscribes nothing
        // would keep being invoked after it has been destroyed.
        var game = GameManager.Instance;
        if (game != null)
        {
            game.OnGamePaused -= OnDim;
            game.OnGameResumed -= OnUndim;
            game.OnLevelUp -= OnDim;
            game.OnLevelUpSelected -= OnUndim;
            game.OnDeath -= OnHide;
            game.OnWin -= OnHide;
        }

        if (Instance == this) Instance = null;
    }

    // =========================================================================
    // Public API - one call per value, no polling
    // =========================================================================

    /// <summary>Hull bar and its numeral. Drops flash the keyline; heals do not.</summary>
    public void SetHull(int current, int max)
    {
        if (max <= 0) return;

        float next = Mathf.Clamp01((float)current / max);
        bool dropped = next < hullTarget - 0.0001f;

        hullTarget = next;
        hullSpeed = 1f / (dropped ? HullLerp : HealLerp);
        if (dropped) flashAt = Time.unscaledTime;

        if (hullValue != null) hullValue.text = Mathf.Max(0, current).ToString();

        if (!lowHull && hullTarget <= LowEnter) lowHull = true;
        else if (lowHull && hullTarget >= LowExit) lowHull = false;

        if (hullLabel != null) hullLabel.text = lowHull ? "HULL CRITICAL" : "HULL";
    }

    /// <summary>Progress toward the next upgrade choice, 0-1.</summary>
    public void SetXp(float normalised)
    {
        xpTarget = Mathf.Clamp01(normalised);
    }

    public void SetLevel(int level)
    {
        if (levelValue != null) levelValue.text = "LV " + level;
        levelPunchAt = Time.unscaledTime;
    }

    /// <summary>
    /// Run clock. Takes the two parts rather than a float because that is what the
    /// timer already has, and it keeps the string build to whole-second changes.
    /// </summary>
    public void SetTime(int minutes, int seconds)
    {
        if (minutes == shownMinutes && seconds == shownSeconds) return;
        shownMinutes = minutes;
        shownSeconds = seconds;

        if (timeValue != null)
            timeValue.text = minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    public void SetKills(int kills)
    {
        if (killValue != null) killValue.text = kills.ToString();
        killPunchAt = Time.unscaledTime;
    }

    /// <summary>Level 0 locks the slot.</summary>
    public void SetUpgrade(UpgradeType type, int level)
    {
        if (slots == null) return;
        foreach (var slot in slots)
            if (slot != null && slot.Type == type) slot.SetLevel(level);
    }

    /// <summary>Drift readout. <paramref name="charge"/> is 0-1 across the three pips.</summary>
    public void SetDrift(bool active, float charge)
    {
        drifting = active;
        driftCharge = Mathf.Clamp01(charge);
    }

    /// <summary>
    /// Pause, upgrade pick and game over drop the HUD to a quarter rather than
    /// hiding it - the player still wants to see their hull while they choose.
    /// </summary>
    public void SetDimmed(bool dimmed)
    {
        dimTarget = dimmed ? DimAlpha : 1f;
    }

    /// <summary>
    /// Game over and the win screen take the HUD all the way out rather than dimming
    /// it. Kept separate from <see cref="SetDimmed"/> so the two cannot fight over
    /// dimTarget - hidden always wins while it is set.
    /// </summary>
    public void SetHidden(bool value)
    {
        hidden = value;
    }

    // =========================================================================
    // Draw
    // =========================================================================

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        hull = Mathf.Lerp(hull, hullTarget, Mathf.Clamp01(dt * hullSpeed));
        if (Mathf.Abs(hull - hullTarget) < 0.001f) hull = hullTarget;

        xp = Mathf.Lerp(xp, xpTarget, Mathf.Clamp01(dt / XpLerp));
        if (Mathf.Abs(xp - xpTarget) < 0.001f) xp = xpTarget;

        if (group != null)
        {
            float target = hidden ? 0f : dimTarget;
            group.alpha = Mathf.MoveTowards(group.alpha, target, dt / DimFade * (1f - DimAlpha));
        }

        Redraw();
    }

    void Redraw()
    {
        // One shared cycle for every low-hull element - independent timers look broken.
        float pulse = lowHull
            ? Mathf.Lerp(PulseFloor, 1f, 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime / PulsePeriod * Mathf.PI * 2f))
            : 1f;
        if (posedPulse >= 0f) pulse = posedPulse;

        DrawHull(pulse);
        DrawXp();
        DrawStats();
        DrawDrift();
    }

    void DrawHull(float pulse)
    {
        if (hullFill != null)
        {
            // The stripe sprite is Tiled, so shrinking the rect walks the notches off
            // the leading edge instead of squashing them - which is what makes the
            // damage taken countable.
            var size = hullFill.sizeDelta;
            hullFill.sizeDelta = new Vector2(hullTrackWidth * hull, size.y);

            bool visible = hull > 0.0005f;                   // no 1px sliver at zero
            if (hullFill.gameObject.activeSelf != visible) hullFill.gameObject.SetActive(visible);
        }

        var fillColour = lowHull ? HudPalette.Alarm : HudPalette.Yellow;
        if (hullStripe != null) hullStripe.color = HudPalette.Fade(fillColour, pulse);

        if (hullKeyline != null)
        {
            var resting = lowHull ? HudPalette.Fade(HudPalette.Alarm, pulse) : HudPalette.Cream;
            // A hit flashes the keyline through alarm and back, on top of whatever
            // the resting state is.
            float flash = 1f - Mathf.Clamp01((Time.unscaledTime - flashAt) / KeylineFlash);
            hullKeyline.color = flash > 0f
                ? Color.Lerp(resting, HudPalette.Alarm, Mathf.Sin(flash * Mathf.PI))
                : resting;
        }

        // Small red text on green terrain is illegible, so the labels get solid chips
        // in the low state rather than just a colour change.
        var labelChip = lowHull ? HudPalette.Fade(HudPalette.Alarm, pulse) : HudPalette.Clear;
        var valueChip = lowHull ? HudPalette.Fade(HudPalette.DeepInkChip, pulse) : HudPalette.Clear;
        var chipShadow = lowHull ? HudPalette.Fade(HudPalette.Shadow, pulse) : HudPalette.Clear;

        if (hullLabelChip != null) hullLabelChip.color = labelChip;
        if (hullValueChip != null) hullValueChip.color = valueChip;
        if (hullLabelChipShadow != null) hullLabelChipShadow.color = chipShadow;
        if (hullValueChipShadow != null) hullValueChipShadow.color = chipShadow;

        if (hullLabel != null)
            hullLabel.color = lowHull ? HudPalette.Fade(HudPalette.Cream, pulse) : HudPalette.CreamLabel;
        if (hullValue != null)
            hullValue.color = lowHull ? HudPalette.Fade(HudPalette.Alarm, pulse) : HudPalette.Cream;

        if (lowHullVignette != null)
            lowHullVignette.alpha = lowHull ? VignetteAlpha * pulse : 0f;
    }

    void DrawXp()
    {
        if (xpFill != null)
        {
            var size = xpFill.sizeDelta;
            xpFill.sizeDelta = new Vector2(xpTrackWidth * xp, size.y);
        }

        if (levelChip != null)
            levelChip.localScale = Vector3.one * Punch(levelPunchAt, LevelPunch, LevelPeak);
    }

    void DrawStats()
    {
        if (killNumeral != null)
            killNumeral.localScale = Vector3.one * Punch(killPunchAt, KillPunch, KillPeak);
    }

    void DrawDrift()
    {
        int count = driftPips != null ? driftPips.Length : 0;
        int lit = Mathf.FloorToInt(driftCharge * count + 0.0001f);
        bool ready = driftCharge >= 0.999f;

        // The brightness pulse is the only thing in the HUD that animates forever, so
        // it always means "you are drifting".
        float pulse = drifting
            ? Mathf.Lerp(0.7f, 1f, 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime / DriftPulsePeriod * Mathf.PI * 2f))
            : 1f;

        if (driftPips != null)
        {
            for (int i = 0; i < driftPips.Length; i++)
            {
                if (driftPips[i] == null) continue;
                driftPips[i].color = i < lit
                    ? HudPalette.Fade(HudPalette.Yellow, pulse)
                    : HudPalette.CreamPipEmpty;
            }
        }

        if (driftBody != null)
            driftBody.color = drifting
                ? HudPalette.Fade(HudPalette.Terracotta, pulse)
                : HudPalette.DeepInk;

        var mark = drifting || ready ? HudPalette.Cream : HudPalette.CreamDim;
        if (driftKeyline != null) driftKeyline.color = HudPalette.Fade(mark, pulse);
        if (driftChevron != null) driftChevron.color = HudPalette.Fade(mark, pulse);
        if (driftLabel != null) driftLabel.color = HudPalette.Fade(mark, pulse);

        if (driftGlow != null)
            driftGlow.color = HudPalette.Fade(HudPalette.At(HudPalette.Yellow, 128), drifting ? pulse : 0f);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: snap the whole HUD to a given reading so a layout capture does
    /// not have to enter play mode. Nothing here runs in a build.
    /// </summary>
    public void EditorPose(int hullCurrent, int hullMax, float xpNormalised, int level,
                           int minutes, int seconds, int kills, int[] upgradeLevels,
                           bool driftActive, float driftCharge, float pulse)
    {
        group = GetComponent<CanvasGroup>();

        SetHull(hullCurrent, hullMax);
        hull = hullTarget;

        SetXp(xpNormalised);
        xp = xpTarget;

        SetLevel(level);
        shownMinutes = shownSeconds = -1;
        SetTime(minutes, seconds);
        SetKills(kills);
        SetDrift(driftActive, driftCharge);

        // Past every punch and flash, so the capture shows the resting pose.
        flashAt = killPunchAt = levelPunchAt = float.NegativeInfinity;
        posedPulse = pulse;

        if (slots != null && upgradeLevels != null)
        {
            for (int i = 0; i < slots.Length && i < upgradeLevels.Length; i++)
                if (slots[i] != null) slots[i].EditorPose(upgradeLevels[i]);
        }

        Redraw();
        posedPulse = -1f;
    }
#endif

    static float Punch(float startedAt, float duration, float peak)
    {
        float t = (Time.unscaledTime - startedAt) / duration;
        if (t < 0f || t > 1f) return 1f;
        return Mathf.Lerp(1f, peak, Mathf.Sin(t * Mathf.PI));
    }

    // =========================================================================
    // Game events
    // =========================================================================

    void OnDim(object sender, System.EventArgs e) => SetDimmed(true);
    void OnUndim(object sender, System.EventArgs e) => SetDimmed(false);
    void OnHide(object sender, System.EventArgs e) => SetHidden(true);
}
