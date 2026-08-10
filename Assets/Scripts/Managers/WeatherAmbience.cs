using UnityEngine;

/// <summary>
/// The zone's weather bed - rainfall in Woodlands, sandstorm in Desert, snowstorm in
/// Snowy. Those three clips have been in the project since the original audio pass, and
/// AudioClipRefSO.weather was declared to hold them, but nothing ever read the array.
///
/// Deliberately quiet and non-positional. Its job is to stop the map sounding like a
/// vacuum between explosions, not to be noticed.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WeatherAmbience : MonoBehaviour
{
    [SerializeField] AudioClipRefSO clips;

    [Tooltip("Index into AudioClipRefSO.weather for this scene.")]
    [SerializeField] int weatherIndex;

    [SerializeField] float level = 0.5f;

    [Tooltip("Seconds to fade in from silence on load, so the zone arrives rather than " +
             "snapping on with the first frame.")]
    [SerializeField] float fadeIn = 2f;

    AudioSource source;
    float elapsed;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.volume = 0f;

        if (clips != null && clips.weather != null &&
            weatherIndex >= 0 && weatherIndex < clips.weather.Length)
        {
            source.clip = clips.weather[weatherIndex];
        }
    }

    void Start()
    {
        if (source.clip != null) source.Play();
    }

    void Update()
    {
        if (source.clip == null) return;

        if (elapsed < fadeIn) elapsed += Time.unscaledDeltaTime;

        float t = fadeIn <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeIn);
        source.volume = level * t * SoundManager.Mix(AudioBus.Ambience);
    }
}
