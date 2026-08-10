using Cinemachine;
using UnityEngine;

/// <summary>
/// The two bits of impact feedback that are not sound or particles: a momentary freeze
/// on a kill, and a shove to the camera.
///
/// Both are cheap and both do more for how a hit lands than any amount of extra debris.
/// A kill currently reads as an enemy simply disappearing; a couple of frames of held
/// time is what makes it register as something the player did.
///
/// The freeze deliberately does not own timeScale outright. GameManager sets it to zero
/// for pause, the upgrade picker and death, and a hitstop that blindly restored 1 would
/// resume gameplay underneath an open menu.
/// </summary>
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    [Header("Hit stop")]
    [Tooltip("Time scale held during a freeze. Not zero - a hair of motion reads better " +
             "than a hard stop and keeps particles alive.")]
    [Range(0f, 0.5f)]
    [SerializeField] float stopScale = 0.06f;

    [Tooltip("Seconds of real time a kill freezes for. Very short: past about a tenth " +
             "of a second it stops being punctuation and starts being a stutter.")]
    [SerializeField] float killFreeze = 0.055f;

    [Tooltip("Freeze when the player is hit. Slightly longer - being hurt should land " +
             "harder than landing a hit.")]
    [SerializeField] float playerHitFreeze = 0.08f;

    [Header("Shake")]
    [SerializeField] CinemachineImpulseSource impulse;

    [SerializeField] float killShake = 0.22f;
    [SerializeField] float playerHitShake = 0.5f;
    [SerializeField] float explosionShake = 0.35f;

    /// <summary>
    /// Freezes are not allowed to stack. A wave dying at once would otherwise hold the
    /// clock down for as long as the kills kept coming.
    /// </summary>
    float resumeAt = -1f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (impulse == null) impulse = GetComponent<CinemachineImpulseSource>();
    }

    void OnDestroy()
    {
        if (Instance != this) return;

        // Never leave the game frozen because this was destroyed mid-hitstop.
        if (resumeAt >= 0f && Playing()) Time.timeScale = 1f;
        Instance = null;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>An enemy died. The standard punctuation mark.</summary>
    public void Kill(Vector3 at)
    {
        Freeze(killFreeze);
        Shake(at, killShake);
    }

    /// <summary>The player took a hit. Heavier, and no freeze while boosting.</summary>
    public void PlayerHit(Vector3 at)
    {
        Freeze(playerHitFreeze);
        Shake(at, playerHitShake);
    }

    /// <summary>A shell went off. Shake only - explosions are constant and must not freeze.</summary>
    public void Explosion(Vector3 at)
    {
        Shake(at, explosionShake);
    }

    /// <summary>
    /// Holds the clock for <paramref name="seconds"/> of real time.
    ///
    /// Ignored entirely unless gameplay currently owns timeScale, so a kill landing on
    /// the same frame as a level-up cannot un-freeze the upgrade screen.
    /// </summary>
    public void Freeze(float seconds)
    {
        if (seconds <= 0f || !Playing()) return;

        Time.timeScale = stopScale;
        // Extends rather than restarts, so overlapping kills cannot compound.
        resumeAt = Mathf.Max(resumeAt, Time.unscaledTime + seconds);
    }

    public void Shake(Vector3 at, float force)
    {
        if (impulse == null || force <= 0f) return;

        // Downward and slightly random: a flat vertical kick reads as a thud, and the
        // jitter stops repeated kills from shaking identically.
        Vector3 velocity = new Vector3(Random.Range(-0.35f, 0.35f), -1f, Random.Range(-0.35f, 0.35f));
        impulse.GenerateImpulseAt(at, velocity * force);
    }

    void Update()
    {
        if (resumeAt < 0f) return;
        if (Time.unscaledTime < resumeAt) return;

        resumeAt = -1f;

        // A menu may have opened during the freeze and taken the clock; if so it is not
        // ours to give back.
        if (Playing()) Time.timeScale = 1f;
    }

    /// <summary>True while gameplay, rather than a menu, owns the time scale.</summary>
    static bool Playing()
    {
        var game = GameManager.Instance;
        if (game == null) return true;
        return !game.IsGamePaused() && !game.IsDead();
    }
}
