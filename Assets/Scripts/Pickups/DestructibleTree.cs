using Tank;
using UnityEngine;

/// <summary>
/// A tree the player can flatten and shells can blow apart, but which enemies can
/// never destroy.
///
/// The asymmetry is the point: trees stay standing as cover and as obstacles for the
/// AI, so ploughing a lane through them is something only the player can do. Enemies
/// still collide with the capsule and steer around it - they simply have no route to
/// this component, because the only things that call into it are the shell's blast and
/// the player's own collision.
///
/// Ram damage scales with impact speed so that brushing a trunk while manoeuvring is
/// cheap and charging one at top speed is not.
/// </summary>
public class DestructibleTree : MonoBehaviour, IDamagable
{
    [Header("Ramming")]
    [Tooltip("Speed at or below which a ram does no damage at all.")]
    [SerializeField] float harmlessSpeed = 4f;

    [Tooltip("Speed at which a ram does the full damage below.")]
    [SerializeField] float fullImpactSpeed = 15f;

    [Tooltip("Damage at fullImpactSpeed. Scaled down linearly toward harmlessSpeed.")]
    [SerializeField] int maxRamDamage = 8;

    [Tooltip("Fraction of the tank's speed left after flattening a tree. The tank " +
             "punches through rather than stopping dead, but pays for it.")]
    [Range(0f, 1f)]
    [SerializeField] float speedRetainedOnRam = 0.45f;

    [Header("Destruction")]
    [Tooltip("Splinter burst spawned at the point of impact.")]
    [SerializeField] GameObject destroyEffect;

    [Tooltip("Hit points against shellfire. One shell carries 1 damage by default.")]
    [SerializeField] int health = 1;

    [Header("Falling")]
    [Tooltip("Seconds to go from upright to flat.")]
    [SerializeField] float fallSeconds = 0.55f;

    [Tooltip("Seconds spent sinking out of sight once it has landed.")]
    [SerializeField] float sinkSeconds = 0.35f;

    [Tooltip("How far it sinks while fading, so it leaves rather than blinking out.")]
    [SerializeField] float sinkDepth = 2.5f;

    bool felled;

    // ---- toppling state ---------------------------------------------------
    bool falling;
    float fallStart;
    Vector3 fallAxis;
    Quaternion uprightRotation;
    Vector3 uprightPosition;

    /// <summary>
    /// Shellfire. Enemies never reach this: nothing in the enemy code path damages
    /// anything except the player.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (felled) return;

        health -= damage;
        if (health > 0) return;

        // Blown apart rather than driven over: fall away from the blast.
        Fell(transform.position - Vector3.up);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (felled) return;

        // Only the player flattens trees. An enemy shoving one is just an obstacle.
        var tank = collision.gameObject.GetComponentInParent<TankController>();
        if (tank == null) return;

        // The controller's own speed rather than the Rigidbody's: movement is driven by
        // MovePosition, so rb.velocity is residual push, not how fast the tank is going.
        float speed = Mathf.Abs(tank.CurrentSpeed);

        float t = Mathf.InverseLerp(harmlessSpeed, fullImpactSpeed, speed);
        int damage = Mathf.RoundToInt(maxRamDamage * t);

        if (damage > 0) tank.TakeDamage(damage);

        // Bleed speed even on a soft touch, so contact always registers physically.
        tank.ApplyImpactSlowdown(Mathf.Lerp(1f, speedRetainedOnRam, t));

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayTreeImpactSound(transform.position);

        Fell(collision.transform.position);
    }

    /// <summary>
    /// Starts the topple. <paramref name="from"/> is what hit it, so the tree goes over
    /// away from the impact - which is what makes the fall read as a consequence of the
    /// hit rather than as a canned animation.
    /// </summary>
    void Fell(Vector3 from)
    {
        felled = true;

        // Stops it being rammed or shot a second time while it is going down.
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;

        if (destroyEffect != null)
        {
            // At the base, where the trunk actually breaks, not at the object's origin.
            var at = transform.position + Vector3.up * 0.5f;
            Instantiate(destroyEffect, at, Quaternion.identity);
        }

        // Trees are spawned by the terrain tiles and may carry a loot roll.
        var drop = GetComponent<DropOnDestroy>();
        if (drop != null) drop.CheckDrop();

        Vector3 away = transform.position - from;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;

        // Rotate about the horizontal axis perpendicular to the push, so the trunk lays
        // down in the direction it was shoved.
        fallAxis = Vector3.Cross(Vector3.up, away.normalized);
        if (fallAxis.sqrMagnitude < 0.01f) fallAxis = Vector3.right;

        uprightRotation = transform.rotation;
        uprightPosition = transform.position;
        fallStart = Time.time;
        falling = true;

        Destroy(gameObject, fallSeconds + sinkSeconds);
    }

    void Update()
    {
        if (!falling) return;

        float t = (Time.time - fallStart) / Mathf.Max(0.01f, fallSeconds);

        if (t < 1f)
        {
            // Accelerating, like something pivoting on its base under its own weight.
            float eased = t * t;
            transform.rotation = Quaternion.AngleAxis(90f * eased, fallAxis) * uprightRotation;
            return;
        }

        transform.rotation = Quaternion.AngleAxis(90f, fallAxis) * uprightRotation;

        // Down through the ground rather than a fade: the models are opaque and
        // untinted, so sinking is the cheaper and better-looking exit.
        float sink = Mathf.Clamp01((Time.time - fallStart - fallSeconds) /
                                   Mathf.Max(0.01f, sinkSeconds));
        transform.position = uprightPosition - Vector3.up * (sink * sinkDepth);
    }
}
