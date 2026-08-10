using Cinemachine;
using Tank;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The continuous half of the game's feedback, and the counterpart to GameFeel:
/// GameFeel punctuates discrete events, this tracks how fast you are actually going.
///
/// Everything the game had before this reacted either to a hit or to turning. Driving
/// fast in a straight line changed nothing on screen, which is why speed did not read
/// as speed - the tank simply covered ground more quickly.
///
/// One normalised speed value is smoothed here and fanned out to three places, so they
/// can never disagree or pop against each other:
///   - the weight of a dedicated post-processing Volume (motion blur, chromatic
///     aberration, vignette),
///   - the virtual camera's field of view,
///   - the glow on the track trails.
///
/// Motion blur is the reason the Volume is driven by weight rather than left on: URP
/// skips the motion-blur pass entirely at zero intensity, so a HUD that idles at zero
/// is cheaper than the constant blur this replaces, not more expensive. It also stops
/// the game looking soft while parked.
/// </summary>
public class SpeedFeel : MonoBehaviour
{
    public static SpeedFeel Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] TankController tank;

    [Header("Targets")]
    [Tooltip("Volume holding only the speed overrides. Its weight is driven 0-1; the " +
             "profile itself is where the full-speed look is tuned.")]
    [SerializeField] Volume speedVolume;

    [SerializeField] CinemachineVirtualCamera vcam;

    [Tooltip("Emissive trails that glow at speed. The dark scuff marks are separate and " +
             "belong to the tank.")]
    [SerializeField] TrailRenderer[] glowTrails;

    [Header("Response")]
    [Tooltip("Speed fraction below which nothing happens at all, so manoeuvring around " +
             "a chest does not tint the screen.")]
    [Range(0f, 1f)]
    [SerializeField] float quietBelow = 0.25f;

    [Tooltip("Speed fraction at which the effects are at full strength. Above 1 on " +
             "purpose: normal top speed should sit a little short of the ceiling so " +
             "that hitting the boost still visibly takes it further.")]
    [SerializeField] float fullAt = 1.25f;

    [Tooltip("Seconds for the effects to catch up with a change in speed. Long enough " +
             "that a hop over a kerb does not strobe the screen.")]
    [SerializeField] float smoothing = 0.22f;

    [Header("Field of view")]
    [SerializeField] float baseFov = 50f;

    [Tooltip("Field of view at full drive. A small pull: the camera already lags the " +
             "tank by its own damping, and the two compound.")]
    [SerializeField] float topFov = 56f;

    [Header("Track glow")]
    [Tooltip("Trail colour as the glow starts. Held below HDR so it stays out of bloom " +
             "until the tank is genuinely moving.")]
    [ColorUsage(true, true)]
    [SerializeField] Color glowCold = new Color(0.55f, 0.30f, 0.10f, 0.55f);

    [Tooltip("Trail colour at full drive. Above 1 so bloom catches it - the bloom " +
             "threshold on these profiles is 0.95.")]
    [ColorUsage(true, true)]
    [SerializeField] Color glowHot = new Color(3.2f, 1.85f, 0.35f, 1f);

    [Tooltip("Drive below which the glow trails stop emitting entirely.")]
    [SerializeField] float glowFrom = 0.5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock block;
    float drive;                // smoothed, unclamped speed fraction
    float velocity;             // SmoothDamp state
    float applied = -1f;        // last t written, so a still frame writes nothing

    void Awake()
    {
        Instance = this;
        block = new MaterialPropertyBlock();

        if (tank == null) tank = FindObjectOfType<TankController>();

        // baseFov is deliberately NOT read off the camera here. This component writes
        // the lens every frame, so a scene saved mid-effect would seed the resting FOV
        // from an already-widened one and creep further out every run.
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnDisable()
    {
        // Never leave the screen tinted or the lens wide because this was switched off.
        Apply(0f);
    }

    void Update()
    {
        // Unscaled: GameFeel drops timeScale to a crawl for hitstop, and the effects
        // easing off mid-freeze reads as the game stuttering rather than as a hit.
        float dt = Time.unscaledDeltaTime;

        drive = Mathf.SmoothDamp(drive, Target(), ref velocity, smoothing, Mathf.Infinity, dt);
        Apply(Mathf.Clamp01(Mathf.InverseLerp(quietBelow, fullAt, drive)));
    }

    /// <summary>Where the drive wants to be this frame, before smoothing.</summary>
    float Target()
    {
        if (tank == null) return 0f;

        // Menus and death own the screen; a vignette pulsing behind the upgrade picker
        // fights it for attention.
        var game = GameManager.Instance;
        if (game != null && (game.IsGamePaused() || game.IsDead())) return 0f;

        return tank.SpeedUnclamped;
    }

    void Apply(float t)
    {
        if (Mathf.Abs(t - applied) < 0.0005f) return;
        applied = t;

        if (speedVolume != null) speedVolume.weight = t;

        if (vcam != null) vcam.m_Lens.FieldOfView = Mathf.Lerp(baseFov, topFov, t);

        DrawGlow(t);
    }

    /// <summary>
    /// The glow rides the top of the range only, so ordinary driving leaves the plain
    /// scuff marks and only real pace lights them up.
    /// </summary>
    void DrawGlow(float t)
    {
        if (glowTrails == null) return;

        float g = Mathf.Clamp01(Mathf.InverseLerp(glowFrom, 1f, t));
        bool lit = g > 0.01f;
        var colour = Color.Lerp(glowCold, glowHot, g);

        foreach (var trail in glowTrails)
        {
            if (trail == null) continue;

            if (trail.emitting != lit) trail.emitting = lit;
            if (!lit) continue;

            // Through a property block rather than the shared material: the colour has
            // to exceed 1 to reach bloom, which vertex colours cannot carry, and writing
            // the material directly would leak the tint into the asset.
            trail.GetPropertyBlock(block);
            block.SetColor(BaseColorId, colour);
            trail.SetPropertyBlock(block);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only: hold the whole system at one drive value so a capture can show what
    /// full speed looks like without entering play mode.
    /// </summary>
    public void EditorPose(float driveValue)
    {
        if (block == null) block = new MaterialPropertyBlock();

        drive = driveValue;
        applied = -1f;
        Apply(Mathf.Clamp01(Mathf.InverseLerp(quietBelow, fullAt, driveValue)));
    }
#endif
}
