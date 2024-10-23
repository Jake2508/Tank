using System;
using System.Collections.Generic;
using UnityEngine;


public class TankShell : MonoBehaviour
{
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private Rigidbody rb;

    [Header("AOE Vars")]
    public float sphereRadius = 1f;
    public float maxDistance;
    public LayerMask layerMask;
    private Vector3 dir;
    private int damage = 1;

    [Header("Visuals")]
    [SerializeField] private GameObject ExplosionEffect;

    private bool collided;
    private float currentHitDistance; 
    public List<GameObject> currentHitObjects = new List<GameObject>();
    private HashSet<GameObject> damagedObjects = new HashSet<GameObject>();

    public event EventHandler OnTankShellImpact;


    private void OnCollisionEnter(Collision co)
    {
        // may re-work just to track for a destructibles layer - 
        if(gameObject.tag != "Bullet" && co.gameObject.tag != "Player" && !collided)
        {
            collided = true;
            ExplosionVisual();
            ApplyDamage();
            TriggerAudio();
            Destroy(this.gameObject);
        }
    }

    private void ExplosionVisual()
    {
        // Spawn a particle effect
        GameObject explosion = Instantiate(ExplosionEffect, transform.position, Quaternion.identity);
        explosion.transform.parent = EnemyManager.instance.transform;
        // Pro Builder split projectile + add force -> This was a bit too jank
    }
    private void ApplyDamage()
    {
        // SphereCast - Find all overlapping objects
        dir = transform.forward;
        currentHitObjects.Clear();
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, sphereRadius, dir, maxDistance, layerMask, QueryTriggerInteraction.UseGlobal);

        foreach (RaycastHit hit in hits)
        {
            
            currentHitObjects.Add(hit.transform.gameObject);
            currentHitDistance = hit.distance;
        }

        // Check for hits
        if (currentHitObjects.Count == 0)
        {
            return;
        }
        else
        {
            foreach (GameObject obj in currentHitObjects)
            {
                if (!damagedObjects.Contains(obj))
                {
                    IDamagable e = obj.GetComponent<IDamagable>();
                    if (e != null)
                    {
                        e.TakeDamage(damage);
                        damagedObjects.Add(obj);
                    }
                }
            }
        }
    }

    private void TriggerAudio()
    {
        SoundManager.Instance.PlayExplosionSound(transform.position);
    }
}
