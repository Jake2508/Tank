using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The end-of-run verdict (MENUS_IMPLEMENTATION.md section 5), used for both outcomes.
///
/// No panels: the wreck stays on screen and the verdict is stamped over it. Death and
/// the ten-minute win share this whole layout and differ in exactly three ways - the
/// word, its colour, and whether the alarm vignette comes up. That is the entire
/// reason the win screen was cheap to bring in line.
///
/// Input is locked for the first second so a player mashing fire cannot skip their own
/// summary.
/// </summary>
public class EndScreen : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Image scrim;
    [SerializeField] CanvasGroup vignetteGroup;
    [SerializeField] CanvasGroup flashGroup;

    [Header("Verdict")]
    [SerializeField] RectTransform verdict;
    [SerializeField] CanvasGroup verdictGroup;
    [SerializeField] TextMeshProUGUI verdictLabel;
    [SerializeField] TextMeshProUGUI subtitle;

    [Header("Stat strip")]
    [SerializeField] CanvasGroup stripGroup;
    [SerializeField] TextMeshProUGUI timeValue;
    [SerializeField] TextMeshProUGUI killsValue;
    [SerializeField] TextMeshProUGUI levelValue;
    [SerializeField] RectTransform levelNumeral;
    [SerializeField] TextMeshProUGUI coinsValue;

    [Tooltip("12 pips: four columns of three, each column bottom-up.")]
    [SerializeField] Image[] pips = new Image[12];

    [Header("Buttons")]
    [SerializeField] MenuFocusGroup focus;
    [SerializeField] MenuButton primaryButton;
    [SerializeField] MenuButton menuButton;
    [SerializeField] CanvasGroup[] buttonGroups;
    [SerializeField] TextMeshProUGUI hint;

    // ---- the five beats, seconds ---------------------------------------------
    const float ScrimSeconds = 0.35f;

    const float VerdictAt = 0.25f;
    const float VerdictSeconds = 0.18f;
    const float VerdictPeak = 1.6f;
    const float FlashSeconds = 0.033f;                  // two frames at 60
    const float FlashAlpha = 15f / 255f;

    const float VignetteAt = 0.50f;
    const float VignetteSeconds = 0.5f;
    const float VignetteLoop = 4f;
    const float VignetteFloor = 0.85f;

    const float StatsAt = 0.60f;
    const float CountSeconds = 0.5f;
    const float CountStagger = 0.08f;
    const float LevelPopSeconds = 0.15f;
    const float LevelPopPeak = 1.3f;
    const float PipStagger = 0.04f;

    const float ButtonsAt = 1.20f;
    const float ButtonSeconds = 0.14f;
    const float ButtonStagger = 0.05f;
    const float ButtonRise = -14f;

    const float InputUnlock = 1.0f;
    const float LeaveSeconds = 0.10f;

    /// <summary>RETRY on a death, CONTINUE on a win.</summary>
    public event Action Primary;
    public event Action MenuRequested;

    enum Phase { Hidden, Playing, Leaving }

    Phase phase = Phase.Hidden;
    float phaseStart;
    Action pendingExit;

    RunStats stats;
    bool alarm;

    Vector2[] buttonHomes;
    bool homesCaptured;

    void Awake()
    {
        CaptureHomes();

        if (primaryButton != null) primaryButton.Activated += () => BeginLeave(() => Primary?.Invoke());
        if (menuButton != null) menuButton.Activated += () => BeginLeave(() => MenuRequested?.Invoke());
    }

    void CaptureHomes()
    {
        if (homesCaptured || buttonGroups == null) return;

        buttonHomes = new Vector2[buttonGroups.Length];
        for (int i = 0; i < buttonGroups.Length; i++)
            if (buttonGroups[i] != null)
                buttonHomes[i] = ((RectTransform)buttonGroups[i].transform).anchoredPosition;

        homesCaptured = true;
    }

    /// <summary>The run ended in a wreck.</summary>
    public void ShowGameOver(RunStats s) => Show(s, true, "WRECKED", "RUN ENDED", "RETRY", "ENTER TO RETRY");

    /// <summary>The run reached the win condition. Same screen, no alarm.</summary>
    public void ShowWin(RunStats s) => Show(s, false, "SURVIVED", "RUN COMPLETE", "CONTINUE", "ENTER TO CONTINUE");

    void Show(RunStats s, bool alarming, string word, string outcome, string primaryLabel, string hintText)
    {
        CaptureHomes();
        gameObject.SetActive(true);

        stats = s;
        alarm = alarming;

        if (verdictLabel != null)
        {
            verdictLabel.text = word;
            // Alarm red is reserved: low hull, or the run is over. A win is yellow.
            verdictLabel.color = alarming ? MenuPalette.Alarm : MenuPalette.Yellow;
        }

        if (subtitle != null) subtitle.text = ZoneName() + " — " + outcome;
        if (primaryButton != null) primaryButton.SetLabel(primaryLabel);
        if (hint != null) hint.text = hintText;

        phase = Phase.Playing;
        phaseStart = Time.unscaledTime;

        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        // RETRY / CONTINUE focused, never the way out.
        if (focus != null) focus.Open(0, InputUnlock);

        Draw(0f);
    }

    public void Hide()
    {
        phase = Phase.Hidden;
        if (focus != null) focus.Close();
        gameObject.SetActive(false);
    }

    void BeginLeave(Action exit)
    {
        if (phase != Phase.Playing) return;

        phase = Phase.Leaving;
        phaseStart = Time.unscaledTime;
        pendingExit = exit;

        if (focus != null) focus.Close();
        if (group != null) group.blocksRaycasts = false;
    }

    void Update()
    {
        if (phase == Phase.Hidden) return;

        float t = Time.unscaledTime - phaseStart;

        if (phase == Phase.Leaving)
        {
            float k = Mathf.Clamp01(t / LeaveSeconds);
            if (group != null) group.alpha = 1f - k;
            if (k < 1f) return;

            var exit = pendingExit;
            pendingExit = null;
            Hide();
            exit?.Invoke();
            return;
        }

        Draw(t);
    }

    void Draw(float t)
    {
        if (scrim != null)
            scrim.color = MenuPalette.Fade(MenuPalette.ScrimGameOver, Mathf.Clamp01(t / ScrimSeconds));

        DrawVerdict(t);
        DrawVignette(t);
        DrawStats(t);
        DrawButtons(t);
    }

    void DrawVerdict(float t)
    {
        float k = Mathf.Clamp01((t - VerdictAt) / VerdictSeconds);
        float eased = 1f - (1f - k) * (1f - k);

        // The rotation lives on the RectTransform and the builder set it; only the
        // scale is animated, so the -7 degrees is never touched here.
        if (verdict != null)
            verdict.localScale = Vector3.one * Mathf.Lerp(VerdictPeak, 1f, eased);

        if (verdictGroup != null)
            verdictGroup.alpha = t >= VerdictAt ? 1f : 0f;

        // A single cream frame at the moment of impact. Any longer and it reads as a
        // fade rather than a stamp.
        if (flashGroup != null)
        {
            float since = t - VerdictAt;
            flashGroup.alpha = since >= 0f && since < FlashSeconds ? FlashAlpha : 0f;
        }
    }

    void DrawVignette(float t)
    {
        if (vignetteGroup == null) return;

        if (!alarm) { vignetteGroup.alpha = 0f; return; }

        float since = t - VignetteAt;
        if (since < 0f) { vignetteGroup.alpha = 0f; return; }

        if (since < VignetteSeconds)
        {
            vignetteGroup.alpha = since / VignetteSeconds;
            return;
        }

        // The only looping thing on the screen once everything has settled.
        float phase01 = (since - VignetteSeconds) / VignetteLoop;
        vignetteGroup.alpha = Mathf.Lerp(VignetteFloor, 1f,
                                         0.5f + 0.5f * Mathf.Cos(phase01 * Mathf.PI * 2f));
    }

    void DrawStats(float t)
    {
        float since = t - StatsAt;
        if (stripGroup != null) stripGroup.alpha = Mathf.Clamp01(since / 0.2f);
        if (since < 0f) since = 0f;

        // Time and kills roll up from zero, staggered; a run's two headline numbers
        // should not land on the same frame.
        if (timeValue != null)
            timeValue.text = RunStats.Clock(stats.time * CountUp(since, 0));

        if (killsValue != null)
            killsValue.text = Mathf.RoundToInt(stats.kills * CountUp(since, 1)).ToString();

        if (coinsValue != null)
            coinsValue.text = Mathf.RoundToInt(stats.Coins * CountUp(since, 2)).ToString();

        if (levelValue != null) levelValue.text = stats.level.ToString();

        if (levelNumeral != null)
        {
            // Level pops rather than counting - it is a small number and a roll from 0
            // to 7 is over before it registers.
            float k = Mathf.Clamp01(since / LevelPopSeconds);
            levelNumeral.localScale = Vector3.one *
                (since <= 0f ? 0f : Mathf.Lerp(LevelPopPeak, 1f, 1f - (1f - k) * (1f - k)));
        }

        DrawPips(since);
    }

    /// <summary>Eased 0-1 for the count-up in slot <paramref name="slot"/>.</summary>
    static float CountUp(float since, int slot)
    {
        float k = Mathf.Clamp01((since - slot * CountStagger) / CountSeconds);
        return 1f - (1f - k) * (1f - k);
    }

    void DrawPips(float since)
    {
        if (pips == null || stats.tiers == null) return;

        int shown = 0;
        for (int column = 0; column < 4; column++)
        {
            int tier = column < stats.tiers.Length ? stats.tiers[column] : 0;

            for (int row = 0; row < 3; row++)
            {
                int i = column * 3 + row;
                if (i >= pips.Length || pips[i] == null) continue;

                bool filled = row < tier;
                // Filled pips arrive one after another across the whole block, so the
                // build reads as a sequence rather than appearing all at once.
                bool arrived = !filled || since >= shown * PipStagger;
                if (filled) shown++;

                pips[i].color = filled && arrived ? MenuPalette.PipFilled : MenuPalette.PipEmpty;
            }
        }
    }

    void DrawButtons(float t)
    {
        if (buttonGroups == null) return;

        for (int i = 0; i < buttonGroups.Length; i++)
        {
            if (buttonGroups[i] == null) continue;

            float k = Mathf.Clamp01((t - ButtonsAt - i * ButtonStagger) / ButtonSeconds);
            buttonGroups[i].alpha = k;

            if (buttonHomes == null || i >= buttonHomes.Length) continue;
            var rt = (RectTransform)buttonGroups[i].transform;
            rt.anchoredPosition = buttonHomes[i] + new Vector2(0f, Mathf.Lerp(ButtonRise, 0f, k));
        }

        if (hint != null)
        {
            var c = hint.color;
            c.a = MenuPalette.HintGameOver.a * Mathf.Clamp01((t - ButtonsAt - 0.1f) / ButtonSeconds);
            hint.color = c;
        }
    }

    /// <summary>"L_Woodlands" reads as "WOODLANDS" on the subtitle line.</summary>
    static string ZoneName()
    {
        string name = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(name)) return "ZONE";
        if (name.StartsWith("L_")) name = name.Substring(2);
        return name.ToUpperInvariant();
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: draw the screen fully settled for a layout capture.</summary>
    public void EditorPose(RunStats s, bool win)
    {
        CaptureHomes();

        if (win) ShowWin(s); else ShowGameOver(s);

        phase = Phase.Playing;

        // Past every beat - the buttons do not start until 1.20s and the counters are
        // still rolling at 1.0, so an earlier pose captures a half-built screen.
        Draw(ButtonsAt + ButtonSeconds + 2f * ButtonStagger + 0.5f);

        // The vignette would be somewhere arbitrary in its 4s loop by then; pin it to
        // the top so the capture is deterministic.
        if (vignetteGroup != null) vignetteGroup.alpha = win ? 0f : 1f;
        if (flashGroup != null) flashGroup.alpha = 0f;

        // Update never runs in edit mode, so the focus state has to be drawn by hand.
        if (primaryButton != null) primaryButton.EditorSnap(true);
        if (menuButton != null) menuButton.EditorSnap(false);
    }
#endif
}
