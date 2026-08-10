using UnityEngine;

namespace Tank
{
    /// <summary>
    /// The drift meter and the boost it pays for.
    ///
    /// Replaces DriftMeter, which only watched the tank slide and drove the HUD pips -
    /// its own note said to delete it once a real mechanic landed. This charges on hard
    /// cornering at speed, and spends the whole meter on Shift for a short window of
    /// extra speed, a small repair and total immunity.
    ///
    /// Full meter or nothing: the three HUD pips already say exactly when you can go, so
    /// there is never any doubt about whether the button will do something. Partial
    /// spends would make that readout ambiguous.
    /// </summary>
    public class DriftBoost : MonoBehaviour
    {
        [SerializeField] TankController tank;
        [SerializeField] Rigidbody body;

        [Header("Charging")]
        [Tooltip("Seconds of hard cornering at speed to fill all three pips. Short " +
                 "enough that the boost is a rhythm the player is always working " +
                 "toward, rather than a rare panic button they forget they have.")]
        [SerializeField] float chargeTime = 1.3f;

        [Tooltip("Seconds for the meter to bleed away once you stop cornering. Slower " +
                 "than the charge, so a corner broken up by a dodge does not throw away " +
                 "all the progress made.")]
        [SerializeField] float decayTime = 3.5f;

        [Tooltip("Steering input below which a turn is not sharp enough to count.")]
        [Range(0.1f, 1f)]
        [SerializeField] float minTurn = 0.5f;

        [Tooltip("Fraction of top speed below which cornering is just manoeuvring.")]
        [Range(0f, 1f)]
        [SerializeField] float minSpeed01 = 0.35f;

        [Header("Boost")]
        [Tooltip("Shorter than it was, and faster. A brief decisive window reads as a " +
                 "burst; a long one just becomes the normal driving speed.")]
        [SerializeField] float boostSeconds = 1.8f;

        [Tooltip("Top speed multiplier while boosting.")]
        [SerializeField] float boostSpeed = 1.6f;

        [Tooltip("Repair granted the moment the boost fires.")]
        [SerializeField] int boostHeal = 8;

        [Header("Effects")]
        [Tooltip("Looping sparks off the tracks while the meter is charging.")]
        [SerializeField] ParticleSystem driftSparks;

        [Tooltip("Looping wake while the boost is running.")]
        [SerializeField] ParticleSystem boostAura;

        [Tooltip("Track marks, driven harder while drifting.")]
        [SerializeField] TrailRenderer[] driftTrails;

        public const KeyCode BoostKey = KeyCode.LeftShift;

        /// <summary>Meter must fall this far before the ready chime can sound again.</summary>
        const float ReadyRearm = 0.6f;

        float charge;
        float boostUntil = float.NegativeInfinity;
        bool boosting;
        bool announced;

        public bool IsBoosting => boosting;
        public float Charge => charge;
        public bool Ready => charge >= 0.999f;

        void Awake()
        {
            if (tank == null) tank = GetComponent<TankController>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        void Update()
        {
            var game = GameManager.Instance;
            bool frozen = game != null && (game.IsGamePaused() || game.IsDead());

            if (frozen)
            {
                Emit(driftSparks, false);
                if (boosting) EndBoost();
                return;
            }

            bool drifting = IsDrifting();
            UpdateCharge(drifting);
            UpdateBoost();

            // Sparks only while the corner is actually paying out, so the effect always
            // means "this is charging" rather than merely "you are turning".
            Emit(driftSparks, drifting && !boosting);
            Emit(boostAura, boosting);
            DrawTrails(drifting || boosting);

            var hud = HudController.Instance;
            if (hud != null) hud.SetDrift(drifting || boosting, charge);
        }

        /// <summary>
        /// A hard corner carried at speed. Also counts a genuine slide - the hull
        /// pointing somewhere other than where it is travelling - so a tank thrown
        /// sideways still charges even after the stick has been let go.
        /// </summary>
        bool IsDrifting()
        {
            if (tank == null) return false;
            if (tank.Speed01 < minSpeed01) return false;

            if (Mathf.Abs(tank.TurnInput) >= minTurn) return true;

            if (body == null) return false;

            var travel = body.velocity;
            travel.y = 0f;
            if (travel.sqrMagnitude < 1f) return false;

            var heading = transform.forward;
            heading.y = 0f;

            // Reversing is not drifting, so fold the angle into the first quadrant.
            float slip = Vector3.Angle(heading, travel);
            if (slip > 90f) slip = 180f - slip;

            return slip >= 20f;
        }

        void UpdateCharge(bool drifting)
        {
            // A running boost is spending the meter, not filling it.
            if (boosting) return;

            float rate = drifting ? 1f / Mathf.Max(0.01f, chargeTime)
                                  : -1f / Mathf.Max(0.01f, decayTime);

            charge = Mathf.Clamp01(charge + rate * Time.deltaTime);

            // Latched, not edge-triggered on Ready. A slide that hovers right on the
            // detection threshold flickers the meter either side of full, and announcing
            // every crossing machine-guns the chime. It re-arms only once the meter has
            // actually been spent or has properly drained.
            if (charge < ReadyRearm) announced = false;

            if (!Ready || announced) return;

            announced = true;
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayReloadReady(transform.position);
        }

        void UpdateBoost()
        {
            if (boosting)
            {
                if (Time.time >= boostUntil) EndBoost();
                return;
            }

            if (!Input.GetKeyDown(BoostKey) || !Ready) return;

            BeginBoost();
        }

        void BeginBoost()
        {
            boosting = true;
            boostUntil = Time.time + boostSeconds;
            charge = 0f;
            announced = false;

            if (tank != null)
            {
                tank.SetBoost(true, boostSpeed);
                tank.Heal(boostHeal);
            }

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayAt(SoundManager.Instance.Clips != null
                                                ? SoundManager.Instance.Clips.LevelUp : null,
                                            transform.position, 0.5f, AudioBus.Sfx, 0.05f);

            // Shake, but no freeze: the point of the boost is that you suddenly move,
            // and holding the clock would blunt exactly that.
            if (GameFeel.Instance != null) GameFeel.Instance.Shake(transform.position, 0.45f);
        }

        void EndBoost()
        {
            boosting = false;
            if (tank != null) tank.SetBoost(false, 1f);
            Emit(boostAura, false);
        }

        static void Emit(ParticleSystem particles, bool on)
        {
            if (particles == null) return;

            var emission = particles.emission;
            if (emission.enabled == on) return;

            emission.enabled = on;
            if (on && !particles.isPlaying) particles.Play();
        }

        void DrawTrails(bool hard)
        {
            if (driftTrails == null) return;

            foreach (var trail in driftTrails)
            {
                if (trail == null) continue;
                // Longer-lived marks through a corner, so the arc you carved stays
                // readable behind you.
                trail.time = hard ? 1.6f : 0.55f;
            }
        }
    }
}
