using UnityEngine;


/// <summary>Mix groups. Each keeps its own volume, saved between runs.</summary>
public enum AudioBus
{
    Sfx = 0,
    Ui = 1,
    Music = 2,
    Ambience = 3,
}

/// <summary>
/// The game's one audio entry point.
///
/// Every one-shot goes through a fixed pool of AudioSources rather than
/// AudioSource.PlayClipAtPoint. That call creates and destroys a GameObject per sound,
/// which is tolerable on desktop and genuinely costly in WebGL, where the allocation
/// churn from a busy firefight shows up as frame hitches.
///
/// Volumes are plain floats rather than an AudioMixer: mixer assets cannot be authored
/// from script, and this project builds its content from editor scripts on purpose.
/// The trade is that these are applied at play time per source instead of by the mixer,
/// which for a game with four buses and no effects costs nothing.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioClipRefSO audioClipRefSO;
    [SerializeField] private AudioSource button;
    [SerializeField] private AudioSource coin;
    [SerializeField] private AudioSource levelUp;

    [Header("Pool")]
    [Tooltip("Concurrent one-shots. Past this the oldest is recycled, which is " +
             "inaudible in a firefight and cheaper than growing the pool.")]
    [SerializeField] private int poolSize = 16;

    [Tooltip("Beyond this distance a one-shot is dropped rather than played. Sounds " +
             "off the edge of a top-down screen only cost voices.")]
    [SerializeField] private float maxAudibleDistance = 60f;

    private float volume = 1f;

    const string PrefKey = "tankeo.audio.";

    /// <summary>
    /// Sfx, Ui, Music, Ambience. Overwritten from the saved settings on first use; these
    /// are only what applies before anything has been loaded.
    /// </summary>
    static readonly float[] busVolume = { 0.5f, 0.5f, 0.5f, 0.5f };
    static float master = 1f;
    static bool loaded;

    AudioSource[] pool;
    int next;

    /// <summary>A small separate ring of 2D sources for UI, which must never be spatial.</summary>
    AudioSource[] flatPool;
    int nextFlat;

    const int FlatPoolSize = 4;

    public AudioClipRefSO Clips => audioClipRefSO;


    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadVolumes();
            BuildPool();
        }
    }

    // =========================================================================
    // Volume
    // =========================================================================

    public static float Master
    {
        get { return master; }
        set
        {
            master = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKey + "master", master);
        }
    }

    public static float GetBus(AudioBus bus) => busVolume[(int)bus];

    /// <summary>
    /// In memory only. Persistence belongs to SettingsMenu, which writes once on exit -
    /// this is called on every frame of a slider drag, and a PlayerPrefs write per frame
    /// is both wasteful and the wrong save semantics.
    /// </summary>
    public static void SetBus(AudioBus bus, float value)
    {
        busVolume[(int)bus] = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Final multiplier for a bus, master included.
    ///
    /// A straight 1:1 mapping from the slider. Music used to carry a hidden headroom
    /// factor so that a default of 70 would not be too loud; with a real slider on
    /// screen that is the wrong trade - it would mean 100 did not actually mean 100.
    /// The default is 50 instead, and what the player sets is what they get.
    /// </summary>
    public static float Mix(AudioBus bus) => master * busVolume[(int)bus];

    /// <summary>
    /// Reads the same two keys the settings screen writes, so there is one source of
    /// truth for volume rather than a private set that drifts from what the sliders say.
    /// </summary>
    static void LoadVolumes()
    {
        if (loaded) return;
        loaded = true;

        master = PlayerPrefs.GetFloat(PrefKey + "master", 1f);

        int music = PlayerPrefs.GetInt(SettingsMenu.MusicKey, SettingsMenu.MusicDefault);
        int sfx = PlayerPrefs.GetInt(SettingsMenu.SfxKey, SettingsMenu.SfxDefault);

        busVolume[(int)AudioBus.Music] = music / 100f;

        // One slider covers everything that is not music.
        float gain = sfx / 100f;
        busVolume[(int)AudioBus.Sfx] = gain;
        busVolume[(int)AudioBus.Ui] = gain;
        busVolume[(int)AudioBus.Ambience] = gain;
    }

    // =========================================================================
    // Pool
    // =========================================================================

    void BuildPool()
    {
        pool = new AudioSource[Mathf.Max(1, poolSize)];

        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("OneShot_" + i);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;                 // positional, like PlayClipAtPoint
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 8f;
            source.maxDistance = maxAudibleDistance;
            // The menus run at timeScale 0 and their sounds still have to be heard.
            source.ignoreListenerPause = true;

            pool[i] = source;
        }

        flatPool = new AudioSource[FlatPoolSize];
        for (int i = 0; i < flatPool.Length; i++)
        {
            var go = new GameObject("Ui_" + i);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;             // 2D, and it stays that way
            source.ignoreListenerPause = true;
            flatPool[i] = source;
        }
    }

    AudioSource TakeFlat()
    {
        if (flatPool == null) BuildPool();

        for (int i = 0; i < flatPool.Length; i++)
        {
            int index = (nextFlat + i) % flatPool.Length;
            if (flatPool[index] != null && !flatPool[index].isPlaying)
            {
                nextFlat = (index + 1) % flatPool.Length;
                return flatPool[index];
            }
        }

        var recycled = flatPool[nextFlat];
        nextFlat = (nextFlat + 1) % flatPool.Length;
        return recycled;
    }

    AudioSource Take()
    {
        if (pool == null) BuildPool();

        // Prefer a free source; fall back to the oldest in the ring.
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (next + i) % pool.Length;
            if (pool[index] != null && !pool[index].isPlaying)
            {
                next = (index + 1) % pool.Length;
                return pool[index];
            }
        }

        var recycled = pool[next];
        next = (next + 1) % pool.Length;
        return recycled;
    }

    // =========================================================================
    // Base Audio Interactions
    // =========================================================================

    public void PlaySound(AudioClip audioClip, Vector3 position, float volume = 1f)
    {
        PlayAt(audioClip, position, volume, AudioBus.Sfx);
    }

    public void PlaySound(AudioClip[] audioClipArray, Vector3 position, float volume = 1f)
    {
        if (audioClipArray == null || audioClipArray.Length == 0) return;
        PlaySound(audioClipArray[Random.Range(0, audioClipArray.Length)], position, volume);
    }

    /// <summary>
    /// One-shot at a world position. <paramref name="pitchJitter"/> varies the pitch a
    /// little each time, which stops repeated shots and impacts from turning into an
    /// obvious loop.
    /// </summary>
    public void PlayAt(AudioClip clip, Vector3 position, float volume, AudioBus bus,
                       float pitchJitter = 0f)
    {
        if (clip == null) return;

        var listener = Camera.main;
        if (listener != null &&
            (listener.transform.position - position).sqrMagnitude >
            maxAudibleDistance * maxAudibleDistance * 4f)
            return;

        var source = Take();
        if (source == null) return;

        source.transform.position = position;
        source.clip = null;
        source.volume = Mathf.Clamp01(volume) * Mix(bus);
        source.pitch = pitchJitter > 0f ? 1f + Random.Range(-pitchJitter, pitchJitter) : 1f;
        source.PlayOneShot(clip, 1f);
    }

    /// <summary>
    /// Non-positional, for UI. Always full strength wherever the camera is.
    ///
    /// Uses its own sources rather than the positional pool. Borrowing a pooled source
    /// meant setting spatialBlend to 2D, starting the clip, and setting it straight back
    /// to 3D for the next caller - which took effect immediately and played the sound in
    /// world space from the SoundManager's position at the origin. With linear rolloff
    /// and the camera anywhere else on the map, that was silent, which is exactly what
    /// the menus were doing.
    /// </summary>
    public void PlayFlat(AudioClip clip, float volume, AudioBus bus)
    {
        if (clip == null) return;

        var source = TakeFlat();
        if (source == null) return;

        source.volume = Mathf.Clamp01(volume) * Mix(bus);
        source.pitch = 1f;
        source.PlayOneShot(clip, 1f);
    }

    // =========================================================================
    // Named hooks
    // =========================================================================

    public void PlayExplosionSound(Vector3 position)
    {
        PlayAt(audioClipRefSO.Explosion, position, volume, AudioBus.Sfx, 0.08f);
    }

    public void FireShotSound(Vector3 position)
    {
        PlayAt(audioClipRefSO.bulletShot, position, volume, AudioBus.Sfx, 0.06f);
    }

    /// <summary>Ramming a tree. Pitched down slightly from the generic impact.</summary>
    public void PlayTreeImpactSound(Vector3 position)
    {
        var clip = audioClipRefSO.treeImpact != null ? audioClipRefSO.treeImpact
                                                     : audioClipRefSO.Explosion;
        PlayAt(clip, position, volume, AudioBus.Sfx, 0.1f);
    }

    public void PlayReloadReady(Vector3 position)
    {
        PlayAt(audioClipRefSO.reloadReady, position, 0.5f, AudioBus.Sfx, 0.03f);
    }

    public void PlayUiMove()
    {
        PlayFlat(audioClipRefSO.uiMove, 0.5f, AudioBus.Ui);
    }

    public void PlayLowHull(Vector3 position)
    {
        PlayAt(audioClipRefSO.lowHull, position, 0.7f, AudioBus.Sfx);
    }

    public void CoinPickedUp()
    {
        Flatten(coin, AudioBus.Sfx);
    }

    /// <summary>The confirm click. Distinct from PlayUiMove, which is the hover.</summary>
    public void PlayButtonSound()
    {
        Flatten(button, AudioBus.Ui);
    }

    public void LevelUpSound()
    {
        Flatten(levelUp, AudioBus.Ui);
    }

    /// <summary>
    /// Plays one of the three authored sources as 2D UI audio.
    ///
    /// These sit on the SoundManager, which lives at the world origin and survives scene
    /// loads. Left spatial they are heard from the origin, so once the camera follows the
    /// tank away from it they fade out entirely - which is why the menu clicks went
    /// missing rather than being merely quiet.
    /// </summary>
    void Flatten(AudioSource source, AudioBus bus)
    {
        if (source == null) return;

        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = Mix(bus);
        source.Play();
    }
}
