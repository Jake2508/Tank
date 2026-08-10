using UnityEngine;

namespace Tank
{
    /// <summary>
    /// Sits the aiming reticle on the ground and tilts it to match.
    ///
    /// The single normal returned by the aim raycast is not enough on this terrain: it
    /// is low-poly with hard facets, so a reticle locked to one triangle's normal snaps
    /// through several degrees every time the cursor crosses an edge. Instead this takes
    /// four samples around the aim point and averages them, which gives a normal that
    /// changes continuously as the cursor moves across a slope.
    ///
    /// The result is then blended back toward flat and smoothed over a few frames. Fully
    /// aligning to a steep face would leave the reticle nearly edge-on to a top-down
    /// camera, which is worse than not conforming at all.
    ///
    /// Runs in LateUpdate so it reads the same frame's cursor position rather than
    /// trailing the input by a frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TankReticle : MonoBehaviour
    {
        [Header("Ground")]
        [Tooltip("Ground only - the same layer the aim raycast filters to.")]
        [SerializeField] LayerMask groundMask = 1 << 9;

        [Tooltip("Lift off the surface, so the quad does not z-fight with the terrain.")]
        [SerializeField] float surfaceOffset = 0.08f;

        [Tooltip("Spread of the four probes around the aim point. Roughly the reticle's " +
                 "own radius, so it samples the ground it actually covers.")]
        [SerializeField] float sampleRadius = 0.9f;

        [Header("Feel")]
        [Tooltip("0 keeps the reticle flat, 1 lies it fully on the slope.")]
        [Range(0f, 1f)]
        [SerializeField] float normalBlend = 0.6f;

        [Tooltip("Seconds to catch up to the cursor. Small, or aiming feels laggy.")]
        [SerializeField] float positionSmoothing = 0.035f;

        [Tooltip("Seconds to settle onto a new slope. Larger than position: the tilt is " +
                 "decoration and should never twitch.")]
        [SerializeField] float rotationSmoothing = 0.11f;

        /// <summary>Probe height above the aim point. Clears any local bump.</summary>
        const float ProbeUp = 6f;
        const float ProbeLength = 14f;

        static readonly Vector2[] Probes =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f),
            new Vector2(0f, 1f), new Vector2(0f, -1f),
        };

        TankInputs inputs;
        Renderer[] marks;
        Vector3 velocity;
        bool settled;
        bool shown = true;

        void Start()
        {
            inputs = TankInputs.Instance;
            marks = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// Hidden whenever the cursor belongs to the UI. On death the player is clicking
        /// RETRY and MENU, and an aiming reticle sliding around the map underneath that
        /// reads as the tank still being drivable.
        ///
        /// The renderers are switched rather than the GameObject, because TankController
        /// holds a reference to this transform and deactivating it would strand that.
        /// </summary>
        void SetShown(bool value)
        {
            if (shown == value || marks == null) return;

            shown = value;
            foreach (var mark in marks)
                if (mark != null) mark.enabled = value;
        }

        void LateUpdate()
        {
            if (inputs == null)
            {
                inputs = TankInputs.Instance;
                if (inputs == null) return;
            }

            var game = GameManager.Instance;
            bool uiOwnsCursor = game != null && (game.IsGamePaused() || game.IsDead());

            SetShown(!uiOwnsCursor);
            // Frozen where it was, so resuming from pause does not snap it across the map.
            if (uiOwnsCursor) return;

            Vector3 aim = inputs.RecticlePositon;
            Vector3 normal = inputs.AimValid ? Sample(aim) : Vector3.up;

            // Whatever the tilt, the reticle stays on the surface it is tilting to.
            Vector3 target = aim + normal * surfaceOffset;

            if (!settled)
            {
                // First frame: no easing, or the reticle visibly flies in from the origin.
                transform.position = target;
                transform.rotation = Tilt(normal);
                settled = true;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity,
                                                    positionSmoothing, Mathf.Infinity,
                                                    Time.unscaledDeltaTime);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, Tilt(normal),
                1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, rotationSmoothing)));
        }

        /// <summary>
        /// Averages the ground normal over a small patch. Probes that miss are skipped
        /// rather than counted as flat, so the reticle still conforms at the lip of a
        /// slope where half the samples fall off the edge.
        /// </summary>
        Vector3 Sample(Vector3 aim)
        {
            Vector3 sum = Vector3.zero;
            int hits = 0;

            for (int i = 0; i < Probes.Length; i++)
            {
                Vector3 origin = aim + new Vector3(Probes[i].x, 0f, Probes[i].y) * sampleRadius
                                     + Vector3.up * ProbeUp;

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, ProbeLength,
                                     groundMask, QueryTriggerInteraction.Ignore))
                    continue;

                sum += hit.normal;
                hits++;
            }

            if (hits == 0) return inputs.RecticleNormal;

            Vector3 average = (sum / hits).normalized;
            // Guard against a degenerate average when opposing faces cancel out.
            return average.sqrMagnitude < 0.001f ? Vector3.up : average;
        }

        /// <summary>Rotation for a ground normal, pulled back toward flat by the blend.</summary>
        Quaternion Tilt(Vector3 normal)
        {
            Quaternion full = Quaternion.FromToRotation(Vector3.up, normal);
            return Quaternion.Slerp(Quaternion.identity, full, normalBlend);
        }
    }
}
