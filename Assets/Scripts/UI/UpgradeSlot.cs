using UnityEngine;
using UnityEngine.UI;

/// <summary>The four upgrade types, in the order they are drawn. Never reorder these.</summary>
public enum UpgradeType
{
    Armor = 0,
    Turret = 1,
    Tracks = 2,
    Magnet = 3,
}

/// <summary>
/// One 42x42 slot in the HUD's upgrade row. Locked until the player takes the
/// upgrade for the first time, then shows its icon and fills a pip per level.
/// All offsets are in 960x540 canvas units.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UpgradeSlot : MonoBehaviour
{
    [SerializeField] UpgradeType type;

    [Header("Refs")]
    [SerializeField] Image body;
    [SerializeField] Image keyline;
    [SerializeField] Image shadow;
    [SerializeField] Image icon;
    [SerializeField] Image padlock;
    [SerializeField] Image[] pips = new Image[3];

    const float UnlockFade = 0.2f;
    const float UnlockPunch = 0.18f;
    const float UnlockPeak = 1.25f;
    const float PipFlash = 0.3f;
    const int PipFlashes = 2;

    RectTransform rt;
    int level;
    bool settled;
    float unlockAt = float.NegativeInfinity;
    float punchAt = float.NegativeInfinity;
    float flashAt = float.NegativeInfinity;
    int flashPip = -1;

    public UpgradeType Type => type;
    public int Level => level;

    void Awake()
    {
        rt = (RectTransform)transform;
        Apply(1f);
        settled = true;
    }

    /// <summary>Level 0 is locked; 1-3 light that many pips.</summary>
    public void SetLevel(int value)
    {
        value = Mathf.Clamp(value, 0, pips.Length);
        if (value == level) return;

        bool wasLocked = level == 0;
        if (value > level && !wasLocked)
        {
            flashPip = value - 1;
            flashAt = Time.unscaledTime;
        }

        level = value;
        settled = false;

        if (wasLocked && level > 0)
        {
            unlockAt = Time.unscaledTime;
            punchAt = Time.unscaledTime + UnlockFade;
        }

        Apply(wasLocked && level > 0 ? 0f : 1f);
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: snap straight to a level, past the unlock animation.</summary>
    public void EditorPose(int value)
    {
        rt = (RectTransform)transform;
        level = Mathf.Clamp(value, 0, pips.Length);
        unlockAt = punchAt = flashAt = float.NegativeInfinity;
        settled = true;
        Apply(1f);
    }
#endif

    void Update()
    {
        bool animating = Time.unscaledTime < unlockAt + UnlockFade ||
                         Time.unscaledTime < punchAt + UnlockPunch ||
                         Time.unscaledTime < flashAt + PipFlash;

        // One more pass after the last animation ends, or the pip stays mid-flash.
        if (!animating && settled) return;
        settled = !animating;

        Apply(Mathf.Clamp01((Time.unscaledTime - unlockAt) / UnlockFade));
    }

    /// <summary>
    /// Draws the whole slot from <see cref="level"/>. <paramref name="unlockBlend"/>
    /// is 0 the frame the slot unlocks and 1 once the padlock has faded out, so the
    /// resting state is just this method called with 1.
    /// </summary>
    void Apply(float unlockBlend)
    {
        bool unlocked = level > 0;
        float blend = unlocked ? unlockBlend : 0f;

        if (body) body.color = Color.Lerp(HudPalette.DeepInkLocked, HudPalette.DeepInkSlot, blend);
        if (keyline) keyline.color = Color.Lerp(HudPalette.CreamFaint, HudPalette.Cream, blend);
        // A locked slot is flat against the terrain; the shadow arrives with the icon.
        if (shadow) shadow.color = Color.Lerp(HudPalette.Clear, HudPalette.Shadow, blend);

        if (icon) icon.color = HudPalette.Fade(HudPalette.Cream, unlocked ? blend : 0f);
        if (padlock) padlock.color = HudPalette.Fade(HudPalette.CreamLock, unlocked ? 1f - blend : 1f);

        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] == null) continue;
            bool filled = i < level;
            var colour = filled ? HudPalette.Yellow : HudPalette.CreamPipEmpty;
            // Cream to yellow and back, twice, on the pip that just filled.
            if (i == flashPip && Time.unscaledTime < flashAt + PipFlash)
            {
                float t = (Time.unscaledTime - flashAt) / PipFlash;
                float wave = Mathf.Abs(Mathf.Sin(t * Mathf.PI * PipFlashes));
                colour = Color.Lerp(HudPalette.Cream, HudPalette.Yellow, wave);
            }
            pips[i].color = HudPalette.Fade(colour, blend);
        }

        if (rt == null) rt = (RectTransform)transform;
        rt.localScale = Vector3.one * Punch();
    }

    float Punch()
    {
        float t = (Time.unscaledTime - punchAt) / UnlockPunch;
        if (t < 0f || t > 1f) return 1f;
        return Mathf.Lerp(1f, UnlockPeak, Mathf.Sin(t * Mathf.PI));
    }
}
