using UnityEngine;

namespace Tank
{
    /// <summary>
    /// The tank's engine and tracks, as one continuous loop whose pitch and level follow
    /// what the hull is actually doing.
    ///
    /// This is the single biggest thing the game was missing: with only impact sounds,
    /// driving is silent, so the tank reads as a cursor rather than a vehicle. A loop
    /// that rises under throttle and settles at idle ties the new acceleration ramp to
    /// something audible.
    ///
    /// Pitch is driven by speed, volume by speed *and* throttle, so leaning on the stick
    /// against a tree still sounds like effort even when the tank is barely moving.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineAudio : MonoBehaviour
    {
        [SerializeField] TankController tank;
        [SerializeField] AudioClipRefSO clips;

        [Header("Pitch")]
        [SerializeField] float idlePitch = 0.72f;
        [SerializeField] float topPitch = 1.35f;

        [Header("Level")]
        [SerializeField] float idleVolume = 0.25f;
        [SerializeField] float movingVolume = 0.62f;

        [Tooltip("Extra level while the throttle is held, whether or not the tank is " +
                 "actually gaining ground.")]
        [SerializeField] float throttleBoost = 0.15f;

        [Tooltip("Seconds for the engine to catch up. Slower than the hull, so the note " +
                 "trails the movement slightly the way a real drivetrain does.")]
        [SerializeField] float responseTime = 0.18f;

        AudioSource source;
        float pitch, level;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            if (tank == null) tank = GetComponentInParent<TankController>();

            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.35f;         // mostly flat: it is the player's own tank
            source.dopplerLevel = 0f;

            if (clips != null && clips.engineLoop != null) source.clip = clips.engineLoop;

            pitch = idlePitch;
            level = idleVolume;
        }

        void Start()
        {
            if (source.clip != null) source.Play();
        }

        void Update()
        {
            if (tank == null || source.clip == null) return;

            // Unscaled: the engine should not slur to a halt while a menu is open. It is
            // muted instead, below.
            float dt = Time.unscaledDeltaTime;
            float blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, responseTime));

            float speed01 = tank.Speed01;
            float throttle = Mathf.Abs(tank.Throttle);

            float targetPitch = Mathf.Lerp(idlePitch, topPitch, speed01);
            float targetLevel = Mathf.Lerp(idleVolume, movingVolume, speed01)
                                + throttle * throttleBoost;

            // Frozen gameplay means the tank is not driving, whatever the last input was.
            bool frozen = Time.timeScale <= 0.001f;
            if (frozen) targetLevel *= 0.35f;

            var game = GameManager.Instance;
            if (game != null && game.IsDead()) targetLevel = 0f;

            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            level = Mathf.Lerp(level, targetLevel, blend);

            source.pitch = pitch;
            source.volume = level * SoundManager.Mix(AudioBus.Sfx);
        }
    }
}
