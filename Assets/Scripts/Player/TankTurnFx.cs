using UnityEngine;

namespace Tank
{
    /// <summary>
    /// Feedback for turning, separate from the drift meter.
    ///
    /// DriftBoost's sparks are deliberately binary: they mean "this corner is charging
    /// the meter", and they only appear past a speed and steering threshold. That leaves
    /// every ordinary turn with no feedback at all, which is what makes cornering feel
    /// inert. This scales continuously with how hard the tracks are actually working, so
    /// a gentle turn throws a few sparks and a hard one throws a lot.
    ///
    /// Also the hook for a track-bend animation. Nothing here bends a mesh - that needs
    /// rigging the model - but the normalised turn and speed are pushed to an Animator
    /// every frame, so an authored clip can be driven without touching this file again.
    ///
    /// The hull's yaw under a turn lives in TankBodyLean, which already owns the visual
    /// model's local rotation. Writing it from here as well would mean two components
    /// assigning one transform in the same frame, and whichever ran last would win.
    /// </summary>
    public class TankTurnFx : MonoBehaviour
    {
        [SerializeField] TankController tank;

        [Header("Sparks")]
        [Tooltip("One per track. Emission scales with how hard that side is scrubbing.")]
        [SerializeField] ParticleSystem[] trackSparks;

        [Tooltip("Emission rate at a full-lock turn carried at top speed.")]
        [SerializeField] float maxEmission = 26f;

        [Tooltip("Turn effort below which nothing is thrown. Small - the point is that " +
                 "most turns produce something.")]
        [Range(0f, 0.5f)]
        [SerializeField] float sparkThreshold = 0.12f;

        [Header("Animation hook")]
        [Tooltip("Optional. Left empty until the model carries a track-bend clip - see " +
                 "the parameter names below for what it will be driven with.")]
        [SerializeField] Animator trackAnimator;

        [Tooltip("Float, -1 to 1. Negative is a left turn.")]
        [SerializeField] string turnParameter = "Turn";

        [Tooltip("Float, 0 to 1, absolute speed over top speed.")]
        [SerializeField] string speedParameter = "Speed";

        int turnHash, speedHash;
        bool hasTurnParam, hasSpeedParam;

        void Awake()
        {
            if (tank == null) tank = GetComponentInParent<TankController>();

            turnHash = Animator.StringToHash(turnParameter);
            speedHash = Animator.StringToHash(speedParameter);

            // Checked once. Setting a parameter an Animator does not have logs a warning
            // every frame, and the hook is meant to be harmless while it is unwired.
            hasTurnParam = HasParameter(turnHash);
            hasSpeedParam = HasParameter(speedHash);
        }

        bool HasParameter(int hash)
        {
            if (trackAnimator == null || trackAnimator.runtimeAnimatorController == null) return false;

            foreach (var parameter in trackAnimator.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Float)
                    return true;

            return false;
        }

        void Update()
        {
            if (tank == null) return;

            float turn = tank.TurnInput;
            float speed01 = tank.Speed01;

            // How hard the tracks are scrubbing: steering alone does nothing standing
            // still, and speed alone does nothing in a straight line.
            float effort = Mathf.Abs(turn) * speed01;

            DrawSparks(turn, effort);
            DrawAnimator(turn, speed01);
        }

        /// <summary>
        /// The outer track scrubs hardest, so it throws more. Index 0 is the left track.
        /// </summary>
        void DrawSparks(float turn, float effort)
        {
            if (trackSparks == null) return;

            for (int i = 0; i < trackSparks.Length; i++)
            {
                var particles = trackSparks[i];
                if (particles == null) continue;

                bool leftTrack = i == 0;
                // Turning right loads the left track, and the other way round.
                bool outer = leftTrack ? turn > 0f : turn < 0f;
                float share = outer ? 1f : 0.35f;

                float rate = effort < sparkThreshold ? 0f : effort * maxEmission * share;

                var emission = particles.emission;
                emission.rateOverTime = rate;

                bool on = rate > 0.01f;
                if (emission.enabled != on) emission.enabled = on;
                if (on && !particles.isPlaying) particles.Play();
            }
        }

        void DrawAnimator(float turn, float speed01)
        {
            if (trackAnimator == null) return;

            if (hasTurnParam) trackAnimator.SetFloat(turnHash, turn);
            if (hasSpeedParam) trackAnimator.SetFloat(speedHash, speed01);
        }
    }
}
