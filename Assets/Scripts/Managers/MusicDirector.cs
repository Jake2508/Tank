using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Picks and crossfades the music track.
///
/// Two sources rather than one: swapping a clip on a single AudioSource cuts, and a cut
/// between the menu and a run is the most audible seam in the game. One source fades out
/// while the other fades in, so the change is heard as a transition instead of an edit.
///
/// Everything here runs on unscaled time. The pause, upgrade and end screens all sit at
/// timeScale 0, and those are exactly the moments the music has to keep moving.
/// </summary>
public class MusicDirector : MonoBehaviour
{
    public static MusicDirector Instance { get; private set; }

    [SerializeField] AudioClipRefSO clips;

    [Tooltip("Seconds to cross from one track to another.")]
    [SerializeField] float crossfade = 1.4f;

    [Tooltip("How far the music drops while a menu is open, as a fraction of its level.")]
    [Range(0f, 1f)]
    [SerializeField] float duckedLevel = 0.45f;

    [SerializeField] float duckSpeed = 6f;

    /// <summary>Main menu is build index 0; everything else is a run.</summary>
    const int MainMenuBuildIndex = 0;

    AudioSource active, idle;
    float fade = 1f;
    float duck = 1f, duckTarget = 1f;
    AudioClip pending;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        active = CreateSource("Music_A");
        idle = CreateSource("Music_B");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        Apply(TrackFor(SceneManager.GetActiveScene().buildIndex), instant: true);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    AudioSource CreateSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var source = go.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;                 // music is not in the world
        source.ignoreListenerPause = true;
        source.volume = 0f;
        return source;
    }

    AudioClip TrackFor(int buildIndex)
    {
        if (clips == null) return null;
        return buildIndex == MainMenuBuildIndex ? clips.menuMusic : clips.gameplayMusic;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetDucked(false);
        Apply(TrackFor(scene.buildIndex), instant: false);
    }

    /// <summary>Crossfade to <paramref name="clip"/>, or do nothing if already on it.</summary>
    public void Apply(AudioClip clip, bool instant)
    {
        if (clip == null)
        {
            pending = null;
            return;
        }

        if (active != null && active.clip == clip && active.isPlaying) return;

        pending = clip;

        var swap = idle;
        idle = active;
        active = swap;

        active.clip = clip;
        active.time = 0f;
        active.volume = instant ? Level() : 0f;
        active.Play();

        fade = instant ? 1f : 0f;
    }

    /// <summary>Drop the music under a menu, then bring it back.</summary>
    public void SetDucked(bool ducked)
    {
        duckTarget = ducked ? duckedLevel : 1f;
    }

    public void StopMusic(bool instant = false)
    {
        if (active != null)
        {
            if (instant) active.Stop();
            else pending = null;
        }
        fade = instant ? 1f : fade;
    }

    float Level() => SoundManager.Mix(AudioBus.Music) * duck;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        duck = Mathf.MoveTowards(duck, duckTarget, duckSpeed * dt);

        if (fade < 1f)
            fade = Mathf.Clamp01(fade + dt / Mathf.Max(0.01f, crossfade));

        float level = Level();

        if (active != null) active.volume = level * fade;

        if (idle != null)
        {
            idle.volume = level * (1f - fade);
            // Free the voice once it is silent rather than leaving it looping at zero.
            if (fade >= 1f && idle.isPlaying) idle.Stop();
        }

        // WebGL suspends the audio context until the page has been interacted with, so a
        // track started on load can come back silent. Once input arrives, make sure the
        // intended track is actually running.
        if (pending != null && active != null && !active.isPlaying &&
            (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            active.Play();
        }
    }
}
