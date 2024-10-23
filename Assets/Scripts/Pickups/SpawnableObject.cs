using UnityEngine;


public class SpawnableObject : MonoBehaviour
{
    [SerializeField] GameObject toSpawn;
    [SerializeField][Range(0f, 1f)] float probaility;


    public void Spawn()
    {
        if(Random.value < probaility)
        {
            GameObject go = Instantiate(toSpawn, transform.position, transform.rotation);
            go.transform.parent = this.transform;
        }
    }

}
