using System;
using _Game.Scripts;
using UnityEngine;

public class MosquitosDeadBodiesManager : SceneSingleton<MosquitosDeadBodiesManager>
{
    public GameObject standardDeadMosquitoPrefab;
    public GameObject infectedDeadMosquitoPrefab;
    public int deadBodiesPoolSize = 66;
    
    private GameObject[] _deadBodiesPool;

    private void Start()
    {
        _deadBodiesPool = new GameObject[deadBodiesPoolSize];
        
        for (int i = 0; i < deadBodiesPoolSize; i++)
        {
            GameObject prefabToInstantiate = (i % 2 == 0) ? standardDeadMosquitoPrefab : infectedDeadMosquitoPrefab;
            _deadBodiesPool[i] = Instantiate(prefabToInstantiate, transform);
            _deadBodiesPool[i].SetActive(false);
        }
    }
    
    public void SpawnDeadMosquito(Vector3 position, Quaternion rotation, MosquitoType mosquitoType)
    {
        GameObject prefabToUse = (mosquitoType == MosquitoType.Infected) ? infectedDeadMosquitoPrefab : standardDeadMosquitoPrefab;

        for (int i = 0; i < deadBodiesPoolSize; i++)
        {
            if (!_deadBodiesPool[i].activeInHierarchy && _deadBodiesPool[i].name.Contains(prefabToUse.name))
            {
                _deadBodiesPool[i].transform.position = position;
                _deadBodiesPool[i].transform.rotation = rotation;
                _deadBodiesPool[i].SetActive(true);
                return;
            }
        }
        ExpandPoolIfNeeded(mosquitoType);
    }
    
    private void ExpandPoolIfNeeded(MosquitoType mosquitoType)
    {
        GameObject prefabToUse = (mosquitoType == MosquitoType.Infected) ? infectedDeadMosquitoPrefab : standardDeadMosquitoPrefab;

        for (int i = 0; i < deadBodiesPoolSize; i++)
        {
            if (!_deadBodiesPool[i].activeInHierarchy && _deadBodiesPool[i].name.Contains(prefabToUse.name))
            {
                return; // Found an inactive object, no need to expand
            }
        }

        // No inactive object found, expand the pool
        GameObject newDeadBody = Instantiate(prefabToUse, transform);
        newDeadBody.SetActive(false);
        
        Array.Resize(ref _deadBodiesPool, _deadBodiesPool.Length + 1);
        _deadBodiesPool[_deadBodiesPool.Length - 1] = newDeadBody;
    }
}
