using UnityEngine;

namespace Tank
{
    /// <summary>
    /// Rolls and pitches the tank's visual body in response to what the hull is doing.
    /// Purely cosmetic: it drives the model's local rotation only, so the collider, the
    /// physics rotation and the terrain-alignment in TankController are untouched.
    ///
    /// The angles are deliberately small. The point is that the tank looks like it has
    /// mass being thrown around, not that it visibly wobbles - anything past about six
    /// degrees on this camera reads as a bug rather than as weight.
    ///
    /// The turret is a child of this body but re-aims itself in world space every fixed
    /// step, so it stays level while the hull leans underneath it.
    /// </summary>
    public class TankBodyLean : MonoBehaviour
    {
        [Tooltip("The visual model root. Never the Rigidbody's own transform.")]
        [SerializeField] Transform body;

        [SerializeField] TankController tank;

        [Header("Angles, degrees")]
        [Tooltip("Roll away from the turn at top speed.")]
        [SerializeField] float maxRoll = 5f;

        [Tooltip("Nose lift under full acceleration, and dip under braking.")]
        [SerializeField] float maxPitch = 3f;

        [Tooltip("Yaw against the turn, as though the back end takes a moment to " +
                 "follow the front. Very small - this sells a shift, not a drift.")]
        [SerializeField] float maxYaw = 2.5f;

        [Tooltip("One-off nose lift when the gun fires.")]
        [SerializeField] float recoilPitch = 3.5f;

        [Header("Timing, seconds")]
        [SerializeField] float leanSmoothing = 0.13f;
        [SerializeField] float recoilDecay = 0.28f;

        /// <summary>
        /// Acceleration that counts as "full" for the pitch. Read off the controller's
        /// own ramp: top speed reached in the acceleration time.
        /// </summary>
        const float ReferenceAcceleration = 40f;

        Quaternion baseRotation;
        float roll, pitch, yaw;
        float recoil;

        void Awake()
        {
            if (body == null) body = transform;
            if (tank == null) tank = GetComponentInParent<TankController>();

            // The model may be authored at a non-zero rotation; lean composes onto it.
            baseRotation = body.localRotation;
        }

        void OnEnable()
        {
            if (tank != null) tank.Fired += OnFired;
        }

        void OnDisable()
        {
            if (tank != null) tank.Fired -= OnFired;
        }

        void OnFired()
        {
            recoil = recoilPitch;
        }

        void LateUpdate()
        {
            if (body == null || tank == null) return;

            float dt = Time.deltaTime;

            // Roll scales with speed as well as steering: pivoting on the spot should not
            // throw the hull over, only carrying speed through a turn should.
            float targetRoll = -tank.TurnInput * tank.Speed01 * maxRoll;

            // Positive acceleration lifts the nose, braking drops it. Sign is negated
            // because a nose-up pitch is a negative rotation about X.
            float targetPitch = -Mathf.Clamp(tank.Acceleration / ReferenceAcceleration, -1f, 1f) * maxPitch;

            // Against the turn: the hull rotates a touch less than its heading, which
            // reads as the rear stepping out before it catches up.
            float targetYaw = -tank.TurnInput * tank.Speed01 * maxYaw;

            float blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, leanSmoothing));
            roll = Mathf.Lerp(roll, targetRoll, blend);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);
            yaw = Mathf.Lerp(yaw, targetYaw, blend);

            recoil = Mathf.Lerp(recoil, 0f, 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, recoilDecay)));

            // One write, all three axes: this component owns the model's local
            // rotation, so nothing else may assign it.
            body.localRotation = baseRotation * Quaternion.Euler(pitch - recoil, yaw, roll);
        }
    }
}
