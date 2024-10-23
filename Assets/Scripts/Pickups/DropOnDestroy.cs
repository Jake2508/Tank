using UnityEngine;


[System.Serializable]
public class DropPrefab
{
    public GameObject prefab;
    [Range(0f, 1f)] public float dropChance;
}

public class DropOnDestroy : MonoBehaviour
{
    [SerializeField] DropPrefab[] dropPrefabs;

    bool isQuitting = false;

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    public void CheckDrop()
    {
        if (isQuitting) { return; }

        if (dropPrefabs != null && dropPrefabs.Length > 0)
        {
            float totalChance = 0f;

            foreach (DropPrefab dropPrefab in dropPrefabs)
            {
                totalChance += dropPrefab.dropChance;
            }

            float randomValue = Random.value * totalChance;

            foreach (DropPrefab dropPrefab in dropPrefabs)
            {
                if (randomValue < dropPrefab.dropChance)
                {
                    if (dropPrefab.prefab != null)
                    {
                        Transform t = Instantiate(dropPrefab.prefab).transform;
                        t.position = transform.position;
                        t.transform.parent = DropManager.instance.gameObject.transform;
                    }
                    else
                    {
                        Debug.Log("Prefab is not assigned");
                    }

                    break;
                }

                randomValue -= dropPrefab.dropChance;
            }
        }
        else
        {
            Debug.Log("No Drop Prefabs assigned");
        }
    }
}
