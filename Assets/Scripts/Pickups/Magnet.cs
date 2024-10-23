using UnityEngine;


public class Magnet : MonoBehaviour
{
    public static Magnet Instance { get; private set; }

    public float attractorStrength = 5f;
    public float attractorRange = 5f;
    public LayerMask layerMask;


    private void Awake()
    {
        Instance = this;
    }

    private void FixedUpdate()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, attractorRange, layerMask);
        foreach (Collider hitCollider in hitColliders)
        {
            Vector3 forceDirection = transform.position - hitCollider.transform.position;
            hitCollider.GetComponent<Rigidbody>().velocity = forceDirection.normalized * attractorStrength;
        }
    }


    public void IncreasePickupRange()
    {
        attractorRange = 6.5f;

    }
    public void IncreasePickupStrength()
    {
        attractorStrength = 6.5f;
    }
    public void MaxForce()
    {
        attractorRange = 12f;
        attractorStrength = 12f;
    }

}
