using UnityEngine;


[CreateAssetMenu()]
public class AudioClipRefSO : ScriptableObject
{
    public AudioClip buttonClicked;
    public AudioClip Explosion;
    public AudioClip bulletShot;
    public AudioClip coinPickup;

    /// <summary>
    /// One per zone, in build order: Woodlands, Desert, Snowy. Declared since the
    /// original audio pass but never read by anything until WeatherAmbience.
    /// </summary>
    public AudioClip[] weather;

    public AudioClip LevelUp;

    [Header("Music")]
    public AudioClip menuMusic;
    public AudioClip gameplayMusic;

    [Header("Vehicle")]
    [Tooltip("Seamless loop. Pitch-shifted with speed rather than swapped.")]
    public AudioClip engineLoop;

    [Header("Generated")]
    // Synthesised by ProceduralSfx rather than recorded - see that script for why each
    // one sounds the way it does.
    public AudioClip treeImpact;
    public AudioClip reloadReady;
    public AudioClip uiMove;
    public AudioClip lowHull;
}
