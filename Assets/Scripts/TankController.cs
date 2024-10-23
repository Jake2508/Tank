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
        public float maxSpeed = 15f;
        public float Traction = 1f;
        public TrailRenderer[] trails;


        [Header("Turret Properties")]
        public Transform turretTransform;
        public float turretLagSpeed = 0.5f;
        [SerializeField] private Transform firePoint;

        [Header("Weapons")]
        private int tankContactDamage = 1;
        [SerializeField] private GameObject shellProjectile;
        [SerializeField] private GameObject BigShellProjectile;
        public float launchVelocityMultiplier = 50f;
        

        [Header("Reticle Properties")]
        public Transform reticleTransform;

        [Header("Health Properties")]
        [SerializeField] StatusBar healthBar;
        [SerializeField] private int maxHealth = 100;
        private int currentHealth;
        public int armor;

        private Rigidbody rb;
        private TankInputs inputs;
        private Vector3 finalTurretLookDir;
        private bool isMoving;

        public float slopeForce = 10f;
        public float maxSlopeAngle = 45f;
        public float maxGroundDistance = 0.5f;

        private bool reloading = false;
        private float reloadTime = 0.5f;

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
            }
        }

        private void OnCollisionEnter(Collision obj)
        {
            if(GameManager.Instance.IsDead() != true)
            {
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
        protected virtual void HandleMovement()
        {
            // Move Tank Forward
            Vector3 movement = transform.forward * inputs.ForwardInput * tankSpeed * Time.deltaTime;
            rb.MovePosition(rb.position + movement);

            // Steering / Rotation
            Quaternion rotation = Quaternion.Euler(Vector3.up * (tankRotationSpeed * inputs.RotationInput * Time.deltaTime));
            rb.MoveRotation(rb.rotation * rotation);
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
                Vector3 slopeForce = forwardOnTerrain * slopeForceFactor * tankSpeed;
                rb.AddForce(slopeForce);

            }
        }

        protected virtual void HandleTurret()
        {
            if(turretTransform)
            {
                Vector3 turretLookDir = inputs.RecticlePositon - turretTransform.position;
                turretLookDir.y = 0f;

                finalTurretLookDir = Vector3.Lerp(finalTurretLookDir, turretLookDir, Time.deltaTime * turretLagSpeed);
                turretTransform.rotation = Quaternion.LookRotation(finalTurretLookDir);
            }

        }
        protected virtual void HandleReticle()
        {
            if (reticleTransform)
            {
                reticleTransform.position = inputs.RecticlePositon;
            }
        }

        private void FireTurret()
        {
            // Spawn Projectile
            GameObject launchedObject = Instantiate(shellProjectile, firePoint.position, firePoint.rotation);
            launchedObject.transform.parent = transform;

            // Calculate distance from player to reticle
            float launchVelocity = Vector3.Distance(transform.position, reticleTransform.position);
            launchVelocity *= launchVelocityMultiplier;

            // Set Projectile
            launchedObject.GetComponentInChildren<Rigidbody>().AddRelativeForce(new Vector3 (0, launchVelocity, 0f));

            // iTween Projectile modifier - Later Experiment
            //iTween.PunchPosition(launchedObject, new Vector3(Random.Range(-arcRange, arcRange), Random.Range(-arcRange, arcRange), 0f), Random.Range(0.5f, 2));

            reloading = true;
            Invoke("ReloadShell", reloadTime);
        }

        private void ReloadShell()
        {
            reloading = false;
        }
        

        public void TakeDamage(int damage)
        {
            if(GameManager.Instance.IsDead() != true)
            {
                ApplyArmor(ref damage);
                currentHealth = currentHealth - damage;
                //Debug.Log(damage);
                if (currentHealth <= 0)
                {
                    //Debug.Log("DEAD - Game Over");
                    GameManager.Instance.Death();
                }
                healthBar.SetState(currentHealth, maxHealth);
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

            healthBar.SetState(currentHealth, maxHealth);
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
            healthBar.SetState(currentHealth, maxHealth);
        }

        // Pretty messy should refactor/change entirely
        public void IncreaseSpeed()
        {
            tankSpeed = 17;
            maxSpeed = 17;

        }
        public void IncreaseRotationSpeed()
        {
            tankRotationSpeed = 120;
        }
        public void HyperSpeed()
        {
            tankSpeed = 20;
            maxSpeed = 20;
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
