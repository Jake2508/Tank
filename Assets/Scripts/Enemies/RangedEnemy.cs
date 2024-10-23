using Tank;
using UnityEngine;


public class RangedEnemy : MonoBehaviour, IDamagable, ISetTarget
{
    Transform targetDestination;
    GameObject targetGameObject;
    TankController targetCharacter;

    [Header("Movement")]
    [SerializeField] float MoveSpeed;
    [SerializeField] float rotationSpeed;
    [SerializeField] float avoidanceRadius = 3f; // Radius for collision avoidance

    [Header("Combat")]
    [SerializeField] int damage = 1;
    [SerializeField] int hp = 1;
    [SerializeField] int experience_reward = 400;
    [SerializeField] float attackRange = 26f;
    private float attackTimer = 0;
    private float attackTimerMax = 1f;

    [Header("Bullet")]
    [SerializeField] GameObject bulletProjectile;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float launchVelocityMultiplier = 50f;

    [SerializeField] GameObject EnemyExplode;

    private bool alive = true;
    private Rigidbody rb;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetTarget(GameObject target)
    {
        targetGameObject = target;
        targetDestination = target.transform;
    }

    private void FixedUpdate()
    {
        RotateTowardsTarget();
        MoveTowardsTarget();
        AvoidObstacles();
    }

    private void Update()
    {
        attackTimer += Time.deltaTime;
        if(attackTimer > attackTimerMax)
        {
            attackTimer = 0f;
            if(inRange())
            {
                RangedAttack();
            }
        }
    }

    private void MoveTowardsTarget()
    {
        Vector3 directionn = (targetDestination.position - transform.position).normalized;
        rb.velocity = directionn * MoveSpeed;
    }

    private void RotateTowardsTarget()
    {
        Vector3 direction = (targetDestination.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
    }

    private void AvoidObstacles()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, avoidanceRadius);

        Vector3 avoidanceForce = Vector3.zero;

        foreach (Collider collider in nearbyColliders)
        {
            if (collider != null && collider.gameObject != gameObject && collider.gameObject != targetGameObject)
            {
                // Calculate avoidance force
                Vector3 avoidanceDirection = transform.position - collider.transform.position;
                float distance = avoidanceDirection.magnitude;
                float combinedRadius = avoidanceRadius + collider.bounds.extents.magnitude;

                // Check for zero distance or overlapping radii to avoid division by zero
                if (distance > 0 && distance < combinedRadius)
                {
                    float strength = Mathf.Clamp01(1 - (distance / combinedRadius));
                    avoidanceForce += avoidanceDirection.normalized * strength;
                }
            }
        }

        // Apply the avoidance force
        rb.AddForce(avoidanceForce * MoveSpeed * Time.deltaTime);
    }

    private void OnCollisionStay(Collision collision)
    {
        if(collision.gameObject == targetGameObject)
        {
            MeleeAttack();
        }
    }

    private void MeleeAttack()
    {
        if(targetCharacter == null)
        {
            targetCharacter = targetGameObject.GetComponent<TankController>();
        }

        targetCharacter.TakeDamage(damage);
        GetComponent<DropOnDestroy>().CheckDrop();
        Destroy(this.gameObject); 
    }
    private void RangedAttack()
    {
        // Spawn Projectile
        GameObject bulletPrefab = Instantiate(bulletProjectile, firePoint.position, firePoint.rotation);
        bulletPrefab.transform.parent = EnemyManager.instance.transform;

        // Calculate distance from player to reticle
        float launchVelocity =50;
        launchVelocity *= launchVelocityMultiplier;

        // Set Projectile
        bulletPrefab.GetComponentInChildren<Rigidbody>().AddRelativeForce(new Vector3(0, launchVelocity, 0f));

        // Update damage if Upgrade is applied
        if(GameManager.Instance)

        // Play sound
        if(SoundManager.Instance != null)
        {
            SoundManager.Instance.FireShotSound(transform.position);
        }
    }
    private bool inRange()
    {
        float distance = Vector3.Distance(targetDestination.transform.position, transform.position);
        //Debug.Log(distance);
        if (distance < 32)
        {
            return true;
        }
        else
        {
            return false;
        }
    }


    public void TakeDamage(int damage)
    {
        hp -= damage;
        if(hp < 1 && alive)
        {
            alive = false;

            // Add Kill 
            KillCounter.Instance.AddKill();
            // Do Visual
            PostDamage(transform.position);
            // Check Drop
            GetComponent<DropOnDestroy>().CheckDrop();
            GameObject c = Instantiate(EnemyExplode, transform.position, transform.rotation);
            c.transform.parent = EnemyManager.instance.transform;

            SoundManager.Instance.PlayExplosionSound(transform.position);
            Destroy(gameObject);
        }
    }

    public virtual void PostDamage(Vector3 targetPosition)
    {
        DamagePopup.instance.PostMessage(targetPosition);
    }
}
