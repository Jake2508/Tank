using System;
using System.Collections.Generic;
using UnityEngine;


public class TankShell : MonoBehaviour
{
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private Rigidbody rb;

    [Header("AOE Vars")]
    [Tooltip("Blast radius. Everything with an IDamagable inside this takes the hit.")]
    public float sphereRadius = 1f;
    public LayerMask layerMask;
    [SerializeField] private int damage = 1;

    [Header("Visuals")]
    [SerializeField] private GameObject ExplosionEffect;

    [Tooltip("Trail child, detached on impact so it fades out instead of vanishing " +
             "with the shell.")]
    [SerializeField] private TrailRenderer trail;

    [Header("Flight")]
    [Tooltip("Seconds before an unobstructed shell removes itself. Without this a shot " +
             "fired off the edge of the terrain falls forever.")]
    [SerializeField] private float lifetime = 8f;

    /// <summary>
    /// Gravity multiplier applied to this shell alone.
    ///
    /// Under real gravity a shot that has to arc onto a target 20 units away hangs in
    /// the air for nearly two seconds, which reads as the round sinking rather than
    /// being fired. Heavier gravity forces a correspondingly faster, flatter shot for
    /// the same range - the arc stays readable but the shell gets there in a third of a
    /// second.
    ///
    /// Serialized on the prefab rather than pushed in at launch, and read back by the
    /// gun when it solves the trajectory. That ordering matters: when this was set by a
    /// runtime call after Instantiate, any path where the call did not land left the
    /// shell falling at world gravity while the solver had assumed twelve times that -
    /// so the round sailed far past the reticle and hung in the air, which is exactly
    /// what "shoots further than the reticle and feels floaty" looks like.
    /// </summary>
    [SerializeField] private float gravityScale = 12f;

    public float GravityScale => gravityScale;

    private bool collided;
    public List<GameObject> currentHitObjects = new List<GameObject>();
    private HashSet<GameObject> damagedObjects = new HashSet<GameObject>();

    public event EventHandler OnTankShellImpact;


    private void Awake()
    {
        if (rb == null) rb = GetComponentInChildren<Rigidbody>();
        if (trail == null) trail = GetComponentInChildren<TrailRenderer>();

        // Unconditionally, before any physics step can run: this shell falls at its own
        // rate, applied by hand in FixedUpdate. Leaving Unity's gravity on as well would
        // double it up.
        if (rb != null) rb.useGravity = false;

        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Applied by hand because the shell does not fall at world gravity. Acceleration
        // mode so it is mass-independent, exactly like the built-in gravity it replaces.
        rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);

        // Point the shell along its own arc. The model was keeping the barrel's launch
        // rotation for the whole flight, so as gravity bent the path the round ended up
        // travelling sideways-on - which is what read as the shell drooping oddly.
        Vector3 velocity = rb.velocity;
        if (velocity.sqrMagnitude < 0.01f) return;

        // The shell model and its capsule collider both run along local Y, so that is
        // the axis that has to face the direction of travel.
        transform.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
    }

    private void OnCollisionEnter(Collision co)
    {
        // The original tested this shell's own tag rather than what it hit, so the
        // "don't detonate on the player" guard never actually looked at the player.
        if (collided) return;
        if (co.gameObject.CompareTag("Player")) return;
        if (co.gameObject.CompareTag("Bullet")) return;

        collided = true;

        ExplosionVisual();
        ApplyDamage();
        TriggerAudio();

        // Shake only, never a freeze - shells land constantly and stopping the clock for
        // each one would turn a firefight into a stutter.
        if (GameFeel.Instance != null) GameFeel.Instance.Explosion(transform.position);

        OnTankShellImpact?.Invoke(this, EventArgs.Empty);

        ReleaseTrail();
        Destroy(this.gameObject);
    }

    private void ExplosionVisual()
    {
        // Spawn a particle effect
        GameObject explosion = Instantiate(ExplosionEffect, transform.position, Quaternion.identity);
        if (EnemyManager.instance != null)
            explosion.transform.parent = EnemyManager.instance.transform;
    }

    /// <summary>
    /// Lets the trail outlive the shell by a moment so the streak fades instead of
    /// disappearing on the frame of impact.
    /// </summary>
    private void ReleaseTrail()
    {
        if (trail == null) return;

        trail.transform.SetParent(null, true);
        trail.emitting = false;
        Destroy(trail.gameObject, trail.time + 0.1f);
    }

    private void ApplyDamage()
    {
        // A blast is radial. This was a SphereCastAll swept along transform.forward,
        // which is a capsule pushed *ahead* of the impact - so splash damage only landed
        // on things in front of the shell and missed anything beside or behind it.
        currentHitObjects.Clear();

        Collider[] hits = Physics.OverlapSphere(transform.position, sphereRadius, layerMask,
                                                QueryTriggerInteraction.UseGlobal);

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            // Colliders often sit on a child of the thing that owns the health, which is
            // true of the trees in particular - their capsule is on the leaves.
            IDamagable target = hit.GetComponentInParent<IDamagable>();
            if (target == null) continue;

            // Deduplicate by the object that owns the health, not by the collider, so a
            // multi-collider target cannot be damaged once per collider.
            var component = target as Component;
            var owner = component != null ? component.gameObject : hit.gameObject;
            if (damagedObjects.Contains(owner)) continue;

            damagedObjects.Add(owner);
            currentHitObjects.Add(owner);
            target.TakeDamage(damage);
        }
    }

    private void TriggerAudio()
    {
        SoundManager.Instance.PlayExplosionSound(transform.position);
    }
}
