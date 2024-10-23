using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class SpawnEnemyPrefab
{
    public GameObject ePrefab;
    [Range(0f, 1f)] public float spawnChance;
}

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager instance;

    [SerializeField] SpawnEnemyPrefab [] enemyPrefabs;
    [SerializeField] Vector2 spawnArea;
    [SerializeField] float spawnTimer;
    [SerializeField] GameObject player;

    float timer;

    private void Awake()
    {
        instance = this;
    }
    private void Update()
    {
        timer -= Time.deltaTime;
        if(timer < 0f)
        {
            SpawnEnemy();
            timer = spawnTimer;
        }
    }


    private void SpawnEnemy()
    {
        Vector3 position = GenerateRandomPosition();
        // Add player position to random modifier
        position += player.transform.position;

        if (enemyPrefabs != null && enemyPrefabs.Length > 0)
        {
            float totalChance = 0f;

            foreach (SpawnEnemyPrefab enemyPrefab in enemyPrefabs)
            {
                totalChance += enemyPrefab.spawnChance;
            }
            float randomValue = Random.value * totalChance;


            foreach (SpawnEnemyPrefab enemyPrefab in enemyPrefabs)
            {
                if (randomValue < enemyPrefab.spawnChance)
                {
                    if (enemyPrefab.ePrefab != null)
                    {
                        GameObject e = Instantiate(enemyPrefab.ePrefab, position, Quaternion.identity);
                        //e.transform.position = transform.position;
                        e.GetComponent<ISetTarget>().SetTarget(player);
                        e.transform.parent = transform;
                    }
                    else
                    {
                        Debug.Log("Prefab is not assigned");
                    }
                    break;
                }
                randomValue -= enemyPrefab.spawnChance;
            }
        }
        else
        {
            Debug.Log("No Drop Prefabs assigned");
        }
    }

    private Vector3 GenerateRandomPosition()
    {
        // Generate random position
        Vector3 position = new Vector3();

        float f = UnityEngine.Random.value > 0.5f ? -1f : 1f;
        if (UnityEngine.Random.value > 0.5)
        {
            position.x = UnityEngine.Random.Range(-spawnArea.x, spawnArea.x);
            position.z = spawnArea.y * f;
        }
        else
        {
            position.z = UnityEngine.Random.Range(-spawnArea.y, spawnArea.y);
            position.x = spawnArea.x * f;
        }
        
        position.y = 2f;

        return position;
    }
}
