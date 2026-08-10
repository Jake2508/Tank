using System.Collections.Generic;
using UnityEngine;


public class TerrainTile : MonoBehaviour
{
    [SerializeField] Vector2Int tilePosition;
    [SerializeField] List<SpawnableObject> spawnObjects;


    private void Start()
    {
        GetComponentInParent<ProceduralTerrain>().Add(gameObject, tilePosition);

        // May not be good idea
        transform.position = new Vector3(-100, -100, 0f);
    }

    public void Spawn()
    {
        for (int i = 0; i < spawnObjects.Count; i++)
        {
            spawnObjects[i].Spawn();
        }
    }
}
