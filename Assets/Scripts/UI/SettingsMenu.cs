using UnityEngine;

/// <summary>
/// The settings screen, opened from SETTINGS on the title menu.
///
/// Reuses the pause screen's band through <see cref="SlideBand"/> - same sprite, same
/// anchors, same tween - and changes only what sits in the column, plus the read-only
/// controls plate on the right.
///
/// Volume goes through SoundManager's buses rather than an AudioMixer. The spec's
/// formula, Log10(v/100)*20 dB, is exactly the decibel form of a linear gain of v/100,
/// and AudioSource.volume is linear amplitude - so the two routes are the same numbers
/// and the same silence at zero. A mixer asset cannot be authored from script, and every
/// screen in this project is built by script.
///
/// One write, on exit. Changes are live immediately so the player hears them, but
/// nothing reaches PlayerPrefs until BACK or Esc - which means quitting without going
/// back correctly discards them.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }

    [Header("Band")]
    [SerializeField] CanvasGroup group;
    [SerializeField] SlideBand slide;

    [Tooltip("The controls plate. Fades in slightly after the band lands, so the eye " +
             "reads the column first.")]
    [SerializeField] CanvasGroup plateGroup;
    [SerializeField] RectTransform plate;

    [Tooltip("Keeps the plate clear of the band and on screen at any aspect ratio.")]
    [SerializeField] PlateFit plateFit;

    [Header("Rows")]
    [SerializeField] SettingsSlider musicSlider;
    [SerializeField] SettingsSlider sfxSlider;
    [SerializeField] MenuFocusGroup focus;
    [SerializeField] MenuButton backButton;

    public const string MusicKey = "vol_music";
    public const string SfxKey = "vol_sfx";

    // Both at half. The design called for 70/90, but that mix was too loud in play -
    // and now that there are sliders, the honest fix is a quieter default the player can
    // raise rather than a hidden scaling factor that makes 100 not mean 100.
    public const int MusicDefault = 50;
    public const int SfxDefault = 50;

    // ---- timing, seconds -----------------------------------------------------
    const float PlateDelay = 0.06f;
    const float PlateSeconds = 0.14f;
    const float PlateSlideX = 6f;
    const float CloseSeconds = 0.12f;

    enum Phase { Hidden, Opening, Idle, Closing }

    Phase phase = Phase.Hidden;
    float phaseStart;
    Vector2 plateHome;
    bool plateHomeCaptured;

    void Awake()
    {
        Instance = this;
        CapturePlateHome();

        if (backButton != null) backButton.Activated += Back;
        if (focus != null) focus.Back += Back;

        if (musicSlider != null)
        {
            musicSlider.Changed += OnMusicChanged;
            musicSlider.Settled += PreviewTick;
        }

        if (sfxSlider != null)
        {
            sfxSlider.Changed += OnSfxChanged;
            sfxSlider.Settled += PreviewTick;
        }

        gameObject.SetActive(false);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void CapturePlateHome()
    {
        if (plateHomeCaptured || plate == null) return;
        plateHome = plate.anchoredPosition;
        plateHomeCaptured = true;
    }

    // =========================================================================
    // Open / close
    // =========================================================================

    public void Show()
    {
        CapturePlateHome();
        gameObject.SetActive(true);

        // Before the first Draw: the plate's width and anchor depend on the canvas size,
        // and a frame of it at the wrong width is visible as a jump.
        if (plateFit != null) plateFit.Fit();

        Load();

        phase = Phase.Opening;
        phaseStart = Time.unscaledTime;

        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        // MUSIC on open, not BACK: the player came here to change volume.
        if (focus != null) focus.Open(0);

        Draw(0f);
    }

    public void Hide()
    {
        phase = Phase.Hidden;
        if (focus != null) focus.Close();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// BACK and Esc both land here. The single save point: whatever the sliders read now
    /// is what persists.
    /// </summary>
    public void Back()
    {
        if (phase != Phase.Opening && phase != Phase.Idle) return;

        Save();

        phase = Phase.Closing;
        phaseStart = Time.unscaledTime;

        if (focus != null) focus.Close();
        if (group != null) group.blocksRaycasts = false;
    }

    void Update()
    {
        if (phase == Phase.Hidden) return;

        float t = Time.unscaledTime - phaseStart;

        if (phase == Phase.Closing)
        {
            float k = Mathf.Clamp01(t / CloseSeconds);
            Draw(1f - k);

            if (k < 1f) return;

            Hide();
            if (MainMenuUI.instance != null) MainMenuUI.instance.Show();
            return;
        }

        float open = slide != null ? slide.OpenSeconds : PlateDelay + PlateSeconds;
        Draw(open <= 0f ? 1f : Mathf.Clamp01(t / open));

        if (t >= open) phase = Phase.Idle;
    }

    /// <summary>0 fully out, 1 fully in, so open and close are one path run both ways.</summary>
    void Draw(float k)
    {
        if (slide != null) slide.Draw(k);

        if (plateGroup == null) return;

        float open = slide != null ? slide.OpenSeconds : PlateDelay + PlateSeconds;
        float elapsed = Mathf.Clamp01(k) * open;

        // Starts after the band has landed, so the column is read before the plate.
        float bandLanded = Mathf.Max(0f, open - PlateSeconds - PlateDelay);
        float p = Mathf.Clamp01((elapsed - bandLanded - PlateDelay) / PlateSeconds);

        plateGroup.alpha = p;

        if (plate != null && plateHomeCaptured)
            plate.anchoredPosition = plateHome + new Vector2(Mathf.Lerp(PlateSlideX, 0f, p), 0f);
    }

    // =========================================================================
    // Volume
    // =========================================================================

    void Load()
    {
        int music = PlayerPrefs.GetInt(MusicKey, MusicDefault);
        int sfx = PlayerPrefs.GetInt(SfxKey, SfxDefault);

        if (musicSlider != null) musicSlider.SetValueSilent(music);
        if (sfxSlider != null) sfxSlider.SetValueSilent(sfx);

        // Push both on open, so the mix always matches what the sliders read.
        Apply(music, sfx);
    }

    void Save()
    {
        if (musicSlider != null) PlayerPrefs.SetInt(MusicKey, musicSlider.Value);
        if (sfxSlider != null) PlayerPrefs.SetInt(SfxKey, sfxSlider.Value);
        PlayerPrefs.Save();
    }

    void OnMusicChanged(int v) => SoundManager.SetBus(AudioBus.Music, v / 100f);

    /// <summary>
    /// One slider for everything that is not music. UI clicks and the weather beds ride
    /// with it, or a player who sets this to zero would still hear menu blips and rain.
    /// </summary>
    void OnSfxChanged(int v)
    {
        float gain = v / 100f;
        SoundManager.SetBus(AudioBus.Sfx, gain);
        SoundManager.SetBus(AudioBus.Ui, gain);
        SoundManager.SetBus(AudioBus.Ambience, gain);
    }

    void Apply(int music, int sfx)
    {
        OnMusicChanged(music);
        OnSfxChanged(sfx);
    }

    /// <summary>
    /// One blip when a slider is released, never per frame - dragging otherwise
    /// machine-guns the audio source.
    /// </summary>
    void PreviewTick()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayUiMove();
    }

    /// <summary>
    /// Applied at boot so saved volumes are in force before the player opens anything.
    /// SoundManager loads its own buses from the same keys, so this only has to cover
    /// the SFX slider driving three buses at once.
    /// </summary>
    public static void ApplySaved()
    {
        int music = PlayerPrefs.GetInt(MusicKey, MusicDefault);
        int sfx = PlayerPrefs.GetInt(SfxKey, SfxDefault);

        SoundManager.SetBus(AudioBus.Music, music / 100f);

        float gain = sfx / 100f;
        SoundManager.SetBus(AudioBus.Sfx, gain);
        SoundManager.SetBus(AudioBus.Ui, gain);
        SoundManager.SetBus(AudioBus.Ambience, gain);
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: draw the screen fully open for a layout capture.</summary>
    public void EditorPose(int music, int sfx, int focusedIndex)
    {
        CapturePlateHome();
        gameObject.SetActive(true);
        phase = Phase.Idle;

        // LateUpdate does not tick in an editor render, so the fit has to be forced or
        // the capture shows the plate at its 16:9 width whatever the render size is.
        if (plateFit != null) plateFit.Fit();
        plateHomeCaptured = false;
        CapturePlateHome();

        if (group != null) group.alpha = 1f;

        if (musicSlider != null) musicSlider.EditorPose("MUSIC", music, focusedIndex == 0);
        if (sfxSlider != null) sfxSlider.EditorPose("SFX", sfx, focusedIndex == 1);
        if (backButton != null) backButton.EditorSnap(focusedIndex == 2);

        Draw(1f);
    }
#endif
}
