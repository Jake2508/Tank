using UnityEngine;


public class RemoveObject : MonoBehaviour
{
    [SerializeField] private GameObject obj;
    [SerializeField] private float lifeTimer;
    private float currentTime;
    void Update()
    {
        currentTime += Time.deltaTime;
        if(currentTime >= lifeTimer)
        {
            Destroy(gameObject);
        }
    }
}
