using UnityEngine;


namespace Tank
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent (typeof(TankInputs))]
    public class TankController : MonoBehaviour, IDamagable
    {
        [HideInInspector] public Level level;

        #region Variables
        [Header("Movement")]
        public float tankSpeed = 15f;
        public float tankRotationSpeed = 5f;
        public float Drag = 0.98f;
        public TrailRenderer[] trails;

        [Header("Movement Feel")]
        [Tooltip("Seconds to wind up from a standstill to top speed. Short on purpose: " +
                 "this is a top-down dodging game, and anything past about a fifth of a " +
                 "second reads as input lag rather than as weight.")]
        [SerializeField] float accelerationTime = 0.18f;

        [Tooltip("Seconds to shed top speed. Shorter than acceleration - a tank that " +
                 "cannot stop faster than it starts feels unresponsive rather than heavy.")]
        [SerializeField] float brakingTime = 0.13f;

        [Tooltip("Reverse as a fraction of forward top speed. Near 1 on purpose: this is " +
                 "an arcade game and backing out of trouble should be as quick as driving " +
                 "into it, so forward and reverse can be swapped freely.")]
        [Range(0.2f, 1f)]
        [SerializeField] float reverseFactor = 0.92f;

        [Tooltip("Fraction of the turn rate still available at top speed. Below 1 the " +
                 "tank pivots hard when slow and arcs wide at pace, which is what makes " +
                 "throttle control matter - but too far below and fast corners feel like " +
                 "wading rather than driving.")]
        [Range(0.2f, 1f)]
        [SerializeField] float turnRateAtTopSpeed = 0.78f;

        [Tooltip("Speed below which the track trails stop drawing.")]
        [SerializeField] float trailThreshold = 1.5f;


        [Header("Turret Properties")]
        public Transform turretTransform;
        public float turretLagSpeed = 0.5f;
        [SerializeField] private Transform firePoint;

        [Header("Weapons")]
        private int tankContactDamage = 1;
        [SerializeField] private GameObject shellProjectile;
        [SerializeField] private GameObject BigShellProjectile;

        [Tooltip("Elevation the shell leaves the barrel at. The launch speed is then " +
                 "solved from the range, so the arc looks the same at every distance. " +
                 "Shallow, so the shell reads as a gun rather than a mortar.")]
        [Range(10f, 70f)]
        [SerializeField] private float launchAngle = 20f;

        [Tooltip("Fallback gravity scale, used only if the shell prefab has no TankShell " +
                 "to read it from. The real value lives on the shell so the round and the " +
                 "solver can never disagree about how fast it falls.")]
        [Range(1f, 30f)]
        [SerializeField] private float shellGravityScale = 12f;

        [Tooltip("Ceiling on the solved launch speed, so a shot at the far edge of the " +
                 "map cannot fire a shell fast enough to tunnel through the world.")]
        [SerializeField] private float maxLaunchSpeed = 140f;

        [Tooltip("Backward nudge when the gun fires. Small - it should read on the " +
                 "hull, not shove the tank around.")]
        [SerializeField] private float recoilImpulse = 2.5f;


        [Header("Reticle Properties")]
        public Transform reticleTransform;

        [Header("Health Properties")]
        [SerializeField] private int maxHealth = 100;
        private int currentHealth;
        public int armor;

        private Rigidbody rb;
        private TankInputs inputs;
        private Vector3 finalTurretLookDir;

        [Tooltip("Multiplier on the climbing assist. Was previously shadowed by a local " +
                 "of the same name inside ApplyTraction, so the inspector value did " +
                 "nothing at all; 1 keeps the behaviour that was actually shipping.")]
        public float slopeForce = 1f;
        public float maxSlopeAngle = 45f;
        public float maxGroundDistance = 0.5f;

        private bool reloading = false;
        private float reloadTime = 0.5f;

        // ---- movement state ---------------------------------------------------
        private float currentSpeed;
        private float acceleration;
        private bool hasReticleComponent;

        // ---- drift boost ------------------------------------------------------
        private float speedMultiplier = 1f;
        private bool invulnerable;

        #endregion


        #region Properties

        /// <summary>Signed speed along the hull's forward axis, in units per second.</summary>
        public float CurrentSpeed => currentSpeed;

        /// <summary>Absolute speed as a fraction of top speed, for audio and lean.</summary>
        public float Speed01 => tankSpeed <= 0f ? 0f : Mathf.Clamp01(Mathf.Abs(currentSpeed) / tankSpeed);

        /// <summary>
        /// The same fraction without the clamp, so a boost reads as roughly 1.6 rather
        /// than pinning at 1.
        ///
        /// Speed01 saturates the moment the boost raises the ceiling, which means the
        /// entire boost - the one moment the player most wants the screen to react - is
        /// invisible to anything driven off it. Gameplay maths still wants the clamped
        /// version; effects want this one.
        /// </summary>
        public float SpeedUnclamped => tankSpeed <= 0f ? 0f : Mathf.Abs(currentSpeed) / tankSpeed;

        /// <summary>Change in speed per second. Negative under braking.</summary>
        public float Acceleration => acceleration;

        /// <summary>Steering input actually being applied, -1 to 1.</summary>
        public float TurnInput => inputs != null ? Mathf.Clamp(inputs.RotationInput, -1f, 1f) : 0f;

        public float Throttle => inputs != null ? Mathf.Clamp(inputs.ForwardInput, -1f, 1f) : 0f;

        /// <summary>Raised the frame the gun fires, for the hull's recoil kick.</summary>
        public event System.Action Fired;

        /// <summary>Where the shell leaves the barrel, for muzzle effects.</summary>
        public Transform FirePoint => firePoint;

        /// <summary>Top speed while a drift boost is running. 1 is normal.</summary>
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>True while the drift boost is protecting the player.</summary>
        public bool Invulnerable => invulnerable;

        /// <summary>
        /// Driven by DriftBoost. Kept here rather than there because top speed and damage
        /// intake belong to the tank - the boost only says when.
        /// </summary>
        public void SetBoost(bool active, float multiplier)
        {
            speedMultiplier = active ? Mathf.Max(1f, multiplier) : 1f;
            invulnerable = active;
        }

        #endregion


        #region Builtin Methods
        private void Awake()
        {
            level = GetComponent<Level>();
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = Vector3.down * 20f;

            currentHealth = maxHealth;
        }

        private void Start()
        {
            inputs = GetComponent<TankInputs>();

            // The reticle drives itself when it has its own component - it needs to run
            // in LateUpdate to conform to the ground without trailing the cursor, which
            // a FixedUpdate write from here would fight.
            hasReticleComponent = reticleTransform != null &&
                                  reticleTransform.GetComponent<TankReticle>() != null;

            ReportHealth();
        }

        private void Update()
        {
            if(Input.GetButtonDown("Fire1") && GameManager.Instance.IsGamePaused() != true && GameManager.Instance.IsDead()!= true)
            {
                if(reloading)
                {
                    return;
                }
                FireTurret();
                SoundManager.Instance.FireShotSound(transform.position);
            }
        }

        private void FixedUpdate()
        {
            if(rb && inputs && GameManager.Instance.IsDead() != true)
            {
                HandleMovement();
                HandleTurret();
                HandleReticle();

                ApplyDrag();
                ApplyTraction();
                DrawTrails();
            }
        }

        private void OnCollisionEnter(Collision obj)
        {
            if(GameManager.Instance.IsDead() != true)
            {
                // Trees own their own collision: they scale the damage by how hard they
                // were hit and bleed the tank's speed off, neither of which the flat
                // contact damage below can express.
                if (obj.gameObject.GetComponentInParent<DestructibleTree>() != null) return;

                // Attempt to cast the hitObject to the interface type
                IDamagable damagable = obj.gameObject.GetComponent<IDamagable>();
                Enemy enemy = obj.gameObject.GetComponent<Enemy>();

                // Check if the cast was successful - the hitObject implements the interface)
                if (damagable != null)
                {
                    damagable.TakeDamage(tankContactDamage);

                    TakeDamage(3);
                }
                else
                {
                    // The hitObject does not implement the interface
                }
            }
        }

        #endregion


        #region Custom Methods

        /// <summary>
        /// Throttle ramps into speed rather than setting it. The old version assigned the
        /// full speed on the first frame of input and zero the frame it was released,
        /// which is what made the tank feel like it was sliding on rails rather than
        /// driving.
        /// </summary>
        protected virtual void HandleMovement()
        {
            float throttle = Mathf.Clamp(inputs.ForwardInput, -1f, 1f);
            float top = tankSpeed * speedMultiplier;
            float targetSpeed = throttle * top * (throttle < 0f ? reverseFactor : 1f);

            // Building speed in the direction already travelling uses the acceleration
            // ramp; anything else - lifting off, or reversing into forward motion - is
            // braking, and brakes bite harder than the engine pulls.
            bool building = Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) &&
                            (currentSpeed == 0f || Mathf.Sign(targetSpeed) == Mathf.Sign(currentSpeed));

            float rampTime = building ? accelerationTime : brakingTime;
            // Ramp scales with the boosted top speed, so a boost accelerates you into it
            // rather than taking longer to reach a higher number.
            float ramp = top / Mathf.Max(0.01f, rampTime);

            float previous = currentSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, ramp * Time.fixedDeltaTime);
            acceleration = (currentSpeed - previous) / Time.fixedDeltaTime;

            rb.MovePosition(rb.position + transform.forward * currentSpeed * Time.fixedDeltaTime);

            // Steering tightens as the tank slows: a full-speed turn arcs wide, and
            // easing off the throttle is what lets you pivot. This is the whole reason
            // the throttle ramp above is worth having.
            float turnRate = tankRotationSpeed * Mathf.Lerp(1f, turnRateAtTopSpeed, Speed01);
            Quaternion rotation = Quaternion.Euler(
                Vector3.up * (turnRate * inputs.RotationInput * Time.fixedDeltaTime));
            rb.MoveRotation(rb.rotation * rotation);
        }

        /// <summary>
        /// Bleeds speed off after an impact. <paramref name="retained"/> is the fraction
        /// of the current speed left, so 0.25 means a heavy hit.
        /// </summary>
        public void ApplyImpactSlowdown(float retained)
        {
            currentSpeed *= Mathf.Clamp01(retained);
        }

        private void DrawTrails()
        {
            if (trails == null) return;

            bool rolling = Mathf.Abs(currentSpeed) > trailThreshold;
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] == null) continue;
                if (trails[i].emitting != rolling) trails[i].emitting = rolling;
            }
        }

        private void ApplyDrag()
        {
            rb.velocity *= Drag;
        }
        private void ApplyTraction()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, -transform.up, out hit, maxGroundDistance))
            {
                // Calculate the slope angle
                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

                // Calculate the rotation needed to align with the terrain normal
                Quaternion toRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;

                // Smoothly rotate towards the terrain normal
                transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, Time.deltaTime);

                // Project tank's forward direction onto the terrain plane
                Vector3 forwardOnTerrain = Vector3.ProjectOnPlane(transform.forward, hit.normal);

                // Calculate forward force along the terrain normal
                float slopeForceFactor = Mathf.Clamp01((slopeAngle - maxSlopeAngle) / (90 - maxSlopeAngle));
                Vector3 climb = forwardOnTerrain * slopeForceFactor * tankSpeed * slopeForce;
                rb.AddForce(climb);

            }
        }

        protected virtual void HandleTurret()
        {
            if(turretTransform)
            {
                Vector3 turretLookDir = inputs.RecticlePositon - turretTransform.position;
                turretLookDir.y = 0f;

                finalTurretLookDir = Vector3.Lerp(finalTurretLookDir, turretLookDir, Time.deltaTime * turretLagSpeed);
                if (finalTurretLookDir.sqrMagnitude > 0.0001f)
                    turretTransform.rotation = Quaternion.LookRotation(finalTurretLookDir);
            }

        }
        protected virtual void HandleReticle()
        {
            // Fallback only. TankReticle does this properly when it is present.
            if (reticleTransform && !hasReticleComponent)
            {
                reticleTransform.position = inputs.RecticlePositon;
            }
        }

        private void FireTurret()
        {
            // Spawn Projectile. Deliberately NOT parented to the tank: a shell parented
            // to a moving hull is dragged along by it after launch, which read as the
            // round curving away and drooping in flight.
            GameObject launchedObject = Instantiate(shellProjectile, firePoint.position, firePoint.rotation);

            Vector3 target = reticleTransform != null ? reticleTransform.position
                                                      : inputs.RecticlePositon;

            // Read from the shell itself. Pushing a value in after Instantiate meant the
            // solver and the round could disagree whenever the push did not land, and a
            // shell falling at world gravity against a solution computed for twelve times
            // that overshoots the reticle badly and hangs while it does it.
            TankShell shell = launchedObject.GetComponentInChildren<TankShell>();
            float scale = shell != null ? shell.GravityScale : shellGravityScale;
            float gravity = Mathf.Abs(Physics.gravity.y) * scale;

            Vector3 launchVelocity = SolveBallisticVelocity(firePoint.position, target,
                                                            launchAngle, maxLaunchSpeed, gravity);

            Rigidbody shellBody = launchedObject.GetComponentInChildren<Rigidbody>();
            if (shellBody != null)
            {
                // Assigned rather than pushed with AddForce: a one-frame Force is scaled
                // by fixedDeltaTime and mass, so the shell's actual launch speed silently
                // depended on both. The solver already knows the exact speed required.
                shellBody.velocity = launchVelocity;
            }

            if (rb != null)
            {
                Vector3 kick = -new Vector3(launchVelocity.x, 0f, launchVelocity.z).normalized;
                rb.AddForce(kick * recoilImpulse, ForceMode.Impulse);
            }

            Fired?.Invoke();

            reloading = true;
            Invoke("ReloadShell", reloadTime);
        }

        /// <summary>
        /// Launch velocity that lands a shell on <paramref name="target"/> from
        /// <paramref name="from"/>, fired at a fixed elevation.
        ///
        /// The old code used the distance to the reticle as the launch force directly.
        /// Ballistic range goes with the square of speed, not linearly with it, so a
        /// linear rule overshoots badly at long range and falls short up close - the
        /// shell could only ever land on the reticle at one specific distance.
        ///
        /// The textbook solution assumes continuous motion, but PhysX integrates in fixed
        /// steps - velocity first, then position - so after time t the shell has actually
        /// fallen by (g/2)*t*(t + dt) rather than (g/2)*t^2. Solving the continuous form
        /// leaves the shell landing a little long, growing with flight time; at the far
        /// end of the map that was a third of a unit. Solving the stepped form instead
        /// makes the shot land on the reticle at every range.
        ///
        /// With u as the flight time, the vertical equation
        ///     dy = v*sin(a)*u - (g/2)*u*(u + dt)
        /// and the horizontal one u = R / (v*cos(a)) combine to a quadratic in u:
        ///     g*u^2 + g*dt*u + 2*(dy - R*tan(a)) = 0
        /// </summary>
        /// <param name="gravityMagnitude">
        /// The downward acceleration the shell will actually experience, which is not
        /// world gravity - TankShell runs on its own multiplier so shots stay fast and
        /// flat. Solving against a different value than the shell flies under is the one
        /// way to reintroduce the miss this method exists to remove.
        /// </param>
        public static Vector3 SolveBallisticVelocity(Vector3 from, Vector3 target,
                                                     float angleDegrees, float maxSpeed,
                                                     float gravityMagnitude)
        {
            Vector3 delta = target - from;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float range = flat.magnitude;

            if (range < 0.05f) return Vector3.up * Mathf.Min(10f, maxSpeed);

            Vector3 direction = flat / range;
            float gravity = Mathf.Max(0.01f, gravityMagnitude);
            float step = Mathf.Max(0.0001f, Time.fixedDeltaTime);

            // Firing at ground level from a raised barrel makes dy negative, which only
            // makes the shot easier. It is unsolvable only when aiming at ground far
            // above the muzzle, where a steeper angle is needed to reach at all.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float theta = Mathf.Deg2Rad * Mathf.Min(80f, angleDegrees + attempt * 18f);
                float cos = Mathf.Cos(theta);

                float c = 2f * (delta.y - range * Mathf.Tan(theta));
                float discriminant = gravity * gravity * step * step - 4f * gravity * c;
                if (discriminant < 0f) continue;

                float flightTime = (-gravity * step + Mathf.Sqrt(discriminant)) / (2f * gravity);
                if (flightTime <= 0.0001f) continue;

                float speed = range / (flightTime * cos);
                if (float.IsNaN(speed) || speed <= 0f) continue;

                speed = Mathf.Min(speed, maxSpeed);
                return direction * (speed * cos) + Vector3.up * (speed * Mathf.Sin(theta));
            }

            // Unreachable at any sane elevation: fire flat and fast and let it fall short.
            return direction * maxSpeed;
        }

        private void ReloadShell()
        {
            reloading = false;

            // A quiet two-note blip when the gun comes back. With a half-second reload
            // this is the difference between guessing and knowing when you can fire.
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlayReloadReady(transform.position);
        }


        public void TakeDamage(int damage)
        {
            // The boost's whole promise is that you can drive through anything for its
            // duration, so this has to come before armour and before the hull readout.
            if (invulnerable) return;

            if(GameManager.Instance.IsDead() != true)
            {
                ApplyArmor(ref damage);

                // Only once armour has had its say: a hit fully absorbed did not land,
                // and shaking the screen for it would misreport what happened.
                if (damage > 0 && GameFeel.Instance != null)
                    GameFeel.Instance.PlayerHit(transform.position);

                currentHealth = currentHealth - damage;
                //Debug.Log(damage);
                if (currentHealth <= 0)
                {
                    //Debug.Log("DEAD - Game Over");
                    GameManager.Instance.Death();
                }
                ReportHealth();
            }
        }

        /// <summary>Pushes the hull reading to the HUD, which may not be in the scene.</summary>
        private void ReportHealth()
        {
            if (HudController.Instance != null)
            {
                HudController.Instance.SetHull(currentHealth, maxHealth);
            }
        }

        private void ApplyArmor(ref int damage)
        {

            damage -= armor;
            if(damage < 0) { damage = 0; }
        }

        public void Heal (int amount)
        {
            if(currentHealth <= 0) { return; }

            currentHealth += amount;
            if(currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
            }

            ReportHealth();
        }

        public void RegenHeal()
        {
            if (currentHealth >= maxHealth)
            {
                currentHealth = maxHealth;
                return;
            }
            currentHealth += 3;
            if (currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
            }
            ReportHealth();
        }

        // Pretty messy should refactor/change entirely
        public void IncreaseSpeed()
        {
            tankSpeed = 17;
        }
        public void IncreaseRotationSpeed()
        {
            tankRotationSpeed = 120;
        }
        public void HyperSpeed()
        {
            tankSpeed = 20;
            tankRotationSpeed = 150;
        }
        public void IncreaseArmour()
        {
            armor = 1;
        }

        public void ArmorRegen()
        {
            InvokeRepeating("RegenHeal", 1, 5);
        }

        public void ReduceReload()
        {
            reloadTime = .3f;
        }
        public void IncreaseTurretRotation()
        {
            turretLagSpeed = 4;
        }
        public void ImproveTurretShell()
        {
            shellProjectile = BigShellProjectile;
        }

        public bool CanFireP()
        {
            return !reloading;
        }

        #endregion


    }
}
