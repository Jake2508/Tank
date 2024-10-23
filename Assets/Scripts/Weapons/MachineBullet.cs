using UnityEngine;


public class MachineBullet : MonoBehaviour
{
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private Rigidbody rb;

    private int damage = 3;

    [Header("Visuals")]
    [SerializeField] private GameObject HitImpactEffect;

    private bool collided;
    private float currentHitDistance;


    public void UpdateDamage()
    {
        damage--;
    }

    private void OnCollisionEnter(Collision co)
    {
        // may re-work just to track for a destructibles layer - 
        if(gameObject.tag != "Bullet" && co.gameObject.tag != "Enemy" && !collided)
        {
            collided = true;
            
            ApplyDamage(co);
            ExplosionVisual();

            Destroy(gameObject);
        }
    }

    private void ExplosionVisual()
    {
        // Spawn a particle effect
        GameObject bulletImpactEffect = Instantiate(HitImpactEffect, transform.position, Quaternion.identity);
        bulletImpactEffect.transform.parent = EnemyManager.instance.transform;
    }

    private void ApplyDamage(Collision hitObj)
    {
        IDamagable e = hitObj.gameObject.GetComponent<IDamagable>();
        if (e != null)
        {
            hitObj.gameObject.GetComponent<IDamagable>().TakeDamage(damage);
        }
    }
}
