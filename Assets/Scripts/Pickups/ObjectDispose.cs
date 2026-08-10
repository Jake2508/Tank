using UnityEngine;


/// <summary>
/// Removes scenery and pickups once the player has driven far enough away.
/// </summary>
public class ObjectDispose : MonoBehaviour
{
    Transform playerTransform;
    float maxDistance = 250f;

    /// <summary>Cheap enough to re-check, and there is nothing to do without a player.</summary>
    const float RetrySeconds = 1f;

    float nextRetry;


    private void Start()
    {
        Acquire();
    }

    private void Update()
    {
        // The `== null` here is Unity's overload, which is also true for a destroyed
        // object. That matters: this used to cache the transform once in Start and then
        // read it forever, so a stale reference threw a MissingReferenceException every
        // frame rather than simply re-acquiring.
        if (playerTransform == null)
        {
            if (Time.time < nextRetry) return;
            nextRetry = Time.time + RetrySeconds;

            Acquire();
            if (playerTransform == null) return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if(distance > maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void Acquire()
    {
        var game = GameManager.Instance;
        if (game != null) playerTransform = game.playerTransform;
    }
}
