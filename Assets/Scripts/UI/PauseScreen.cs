using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pause band (MENUS_IMPLEMENTATION.md section 4).
///
/// An angled band down the left, deliberately not a centred panel: the tank stays
/// visible on the right, because half of pausing is looking at the board. That is
/// also why the dim is only A89 here against the upgrade screen's A184.
///
/// Nothing moves when focus changes - the band is tight, and motion at this size
/// reads as jitter. The fill cross-fade carries it.
/// </summary>
public class PauseScreen : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] CanvasGroup group;
    [SerializeField] Image dim;
    [SerializeField] RectTransform band;

    [Tooltip("In top-to-bottom order; they fade in on a stagger.")]
    [SerializeField] CanvasGroup[] contentItems;

    [Header("Stats")]
    [SerializeField] TextMeshProUGUI timeValue;
    [SerializeField] TextMeshProUGUI killsValue;
    [SerializeField] TextMeshProUGUI levelValue;

    [Header("Focus")]
    [SerializeField] MenuFocusGroup focus;
    [SerializeField] MenuButton resumeButton;
    [SerializeField] MenuButton restartButton;
    [SerializeField] MenuButton menuButton;

    // ---- animation, seconds --------------------------------------------------
    const float BandSeconds = 0.16f;
    const float BandOffscreenX = -420f;
    const float DimSeconds = 0.12f;
    const float ItemSeconds = 0.12f;
    const float ItemStagger = 0.03f;
    const float ItemSlideX = -8f;

    const float ResumeSeconds = 0.12f;
    const float LeaveSeconds = 0.10f;

    public event Action Resumed;
    public event Action Restarted;
    public event Action MenuRequested;

    enum Phase { Hidden, Opening, Idle, Resuming, Leaving }

    Phase phase = Phase.Hidden;
    float phaseStart;
    Action pendingExit;

    float bandHomeX;
    Vector2[] itemHomes;
    bool homesCaptured;

    void Awake()
    {
        CaptureHomes();

        if (resumeButton != null) resumeButton.Activated += BeginResume;
        if (restartButton != null) restartButton.Activated += () => BeginLeave(() => Restarted?.Invoke());
        if (menuButton != null) menuButton.Activated += () => BeginLeave(() => MenuRequested?.Invoke());

        // Esc resumes from anywhere in this menu, whatever is focused.
        if (focus != null) focus.Back += BeginResume;
    }

    void CaptureHomes()
    {
        if (homesCaptured) return;

        if (band != null) bandHomeX = band.anchoredPosition.x;

        if (contentItems != null)
        {
            itemHomes = new Vector2[contentItems.Length];
            for (int i = 0; i < contentItems.Length; i++)
                if (contentItems[i] != null)
                    itemHomes[i] = ((RectTransform)contentItems[i].transform).anchoredPosition;
        }

        homesCaptured = true;
    }

    public void Show(RunStats stats)
    {
        CaptureHomes();
        gameObject.SetActive(true);

        if (timeValue != null) timeValue.text = RunStats.Clock(stats.time);
        if (killsValue != null) killsValue.text = stats.kills.ToString();
        if (levelValue != null) levelValue.text = stats.level.ToString();

        phase = Phase.Opening;
        phaseStart = Time.unscaledTime;

        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        // RESUME on open - never a destructive option under the cursor.
        if (focus != null) focus.Open(0);

        Draw(0f);
    }

    public void Hide()
    {
        phase = Phase.Hidden;
        if (focus != null) focus.Close();
        gameObject.SetActive(false);
    }

    /// <summary>Esc or the RESUME button - the same path either way.</summary>
    public void BeginResume()
    {
        if (phase != Phase.Opening && phase != Phase.Idle) return;

        phase = Phase.Resuming;
        phaseStart = Time.unscaledTime;
        if (focus != null) focus.Close();
    }

    void BeginLeave(Action exit)
    {
        if (phase != Phase.Opening && phase != Phase.Idle) return;

        phase = Phase.Leaving;
        phaseStart = Time.unscaledTime;
        pendingExit = exit;
        if (focus != null) focus.Close();

        // The band stays put on the way out; only the group fades. A scene change on a
        // hard cut is jarring, but sliding the band away first would double the wait.
        if (group != null) group.blocksRaycasts = false;
    }

    void Update()
    {
        if (phase == Phase.Hidden) return;

        float t = Time.unscaledTime - phaseStart;

        switch (phase)
        {
            case Phase.Opening:
                Draw(Mathf.Clamp01(t / BandSeconds));
                if (t >= BandSeconds + contentTail) phase = Phase.Idle;
                break;

            case Phase.Resuming:
            {
                // Reverse of the open, and timeScale is restored on the last frame by
                // MenuController rather than here.
                float k = Mathf.Clamp01(t / ResumeSeconds);
                Draw(1f - k);
                if (k >= 1f)
                {
                    Hide();
                    Resumed?.Invoke();
                }
                break;
            }

            case Phase.Leaving:
            {
                float k = Mathf.Clamp01(t / LeaveSeconds);
                if (group != null) group.alpha = 1f - k;
                if (k >= 1f)
                {
                    var exit = pendingExit;
                    pendingExit = null;
                    Hide();
                    exit?.Invoke();
                }
                break;
            }
        }
    }

    float contentTail => contentItems != null && contentItems.Length > 0
        ? (contentItems.Length - 1) * ItemStagger + ItemSeconds
        : 0f;

    /// <summary>
    /// Draws the whole screen at <paramref name="k"/>, 0 fully out and 1 fully in, so
    /// the open and the resume are the same code run in opposite directions.
    /// </summary>
    void Draw(float k)
    {
        float eased = 1f - (1f - k) * (1f - k);                  // ease out

        if (band != null)
        {
            var p = band.anchoredPosition;
            p.x = Mathf.Lerp(BandOffscreenX, bandHomeX, eased);
            band.anchoredPosition = p;
        }

        if (dim != null)
            dim.color = MenuPalette.Fade(MenuPalette.ScrimPause,
                                         Mathf.Clamp01(k * BandSeconds / DimSeconds));

        if (contentItems == null) return;

        float elapsed = k * (BandSeconds + contentTail);

        for (int i = 0; i < contentItems.Length; i++)
        {
            if (contentItems[i] == null) continue;

            float a = Mathf.Clamp01((elapsed - i * ItemStagger) / ItemSeconds);
            contentItems[i].alpha = a;

            if (itemHomes == null || i >= itemHomes.Length) continue;
            var rt = (RectTransform)contentItems[i].transform;
            rt.anchoredPosition = itemHomes[i] + new Vector2(Mathf.Lerp(ItemSlideX, 0f, a), 0f);
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: draw the band fully open for a layout capture.</summary>
    public void EditorPose(RunStats stats, int focusedIndex)
    {
        CaptureHomes();
        gameObject.SetActive(true);
        phase = Phase.Idle;

        if (timeValue != null) timeValue.text = RunStats.Clock(stats.time);
        if (killsValue != null) killsValue.text = stats.kills.ToString();
        if (levelValue != null) levelValue.text = stats.level.ToString();

        if (group != null) group.alpha = 1f;
        Draw(1f);

        var buttons = new[] { resumeButton, restartButton, menuButton };
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i] != null) buttons[i].EditorSnap(i == focusedIndex);
    }
#endif
}
