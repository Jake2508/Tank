using UnityEngine;


public class ObjectDispose : MonoBehaviour
{
    Transform playerTransform;
    float maxDistance = 250f;


    private void Start()
    {
        playerTransform = GameManager.Instance.playerTransform;
    }

    private void Update()
    {
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        if(distance > maxDistance)
        {
            Destroy(gameObject);
        }
    }
}
