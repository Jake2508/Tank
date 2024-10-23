using Tank;
using UnityEngine;


public class Enemy : MonoBehaviour, IDamagable, ISetTarget
{
    Transform targetDestination;
    GameObject targetGameObject;
    TankController targetCharacter;
    [SerializeField] float MoveSpeed;
    [SerializeField] float rotationSpeed;
    [SerializeField] int damage = 1;
    [SerializeField] int hp = 1;
    [SerializeField] int experience_reward = 400;
    [SerializeField] float avoidanceRadius = 3f; // Radius for collision avoidance

    [SerializeField] GameObject EnemyExplode;

    private bool alive = true;
    Rigidbody rb;


    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        RotateTowardsTarget();
        MoveTowardsTarget();
        AvoidObstacles();
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
            Instantiate(EnemyExplode, transform.position, transform.rotation);

            SoundManager.Instance.PlayExplosionSound(transform.position);
            Destroy(gameObject);
        }
    }


    public void SetTarget(GameObject target)
    {
        targetGameObject = target;
        targetDestination = target.transform;
    }

    public virtual void PostDamage(Vector3 targetPosition)
    {
        DamagePopup.instance.PostMessage(targetPosition);
    }
}
