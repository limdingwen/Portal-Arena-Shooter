using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pool manager. Can be used to pool objects.
/// </summary>
public class PoolManager : MonoBehaviour
{
    public static Dictionary<string, PoolManager> instances = new Dictionary<string, PoolManager>();
    
    [Tooltip("The prefab for the object this pool manager shall manage")]
    public GameObject prefab;
    [Tooltip("Can be used to find this pool manager through a singleton")]
    public string poolName;
    [Tooltip("How many objects to be spawned on Start?")]
    public int initialCount;
    [Tooltip("Can this pool expand? If not, null will be returned if pull is fully used.")]
    public bool canExpand = false;

    [Header("Debug")]
    [Tooltip("Enable debug? Disable to save on performance")]
    public bool debugEnabled = true;
    public int debugCount;

    private List<GameObject> pooledObjects = new List<GameObject>();

    private void Awake()
    {
        instances[poolName] = this;
    }

    private void Start()
    {
        for (int i = 0; i < initialCount; i++)
        {
            InstantiateNewPooledObject();
        }
    }

    private void Update()
    {
        if (debugEnabled)
            debugCount = pooledObjects.Count;
    }

    private GameObject InstantiateNewPooledObject()
    {
        GameObject newObject = Instantiate(prefab);
        newObject.SetActive(false);
        pooledObjects.Add(newObject);
        return newObject;
    }

    /// <summary>
    /// Get an unused pooled object. Use SetActive(true) to claim it as used. Returns null if pool is full and cannot be expanded.
    /// </summary>
    /// <returns>An unused pooled object.</returns>
    public GameObject GetAvaliablePooledObject()
    {
        foreach (GameObject pooledObject in pooledObjects)
            if (!pooledObject.activeInHierarchy)
                return pooledObject;

        if (canExpand)
        {
            return InstantiateNewPooledObject();
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Similar to Instantiate(position, rotation). Returns null if pool is full and cannot be expanded.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns>An activated pooled object.</returns>
    public GameObject ActivatePooledObject(Vector3 position, Quaternion rotation)
    {
        GameObject pooledObject = GetAvaliablePooledObject();
        if (pooledObject == null)
        {
            Debug.LogWarning($"Pool {poolName} cannot activate a new object, because it is fully utilized, and it is not allowed to expand!");
            return null;
        }

        pooledObject.transform.SetPositionAndRotation(position, rotation);
        pooledObject.SetActive(true);
        return pooledObject;
    }

    /// <summary>
    /// Similar to Instantiate(). Returns null if pool is full and cannot be expanded.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns>An activated pooled object.</returns>
    public GameObject ActivatePooledObject()
    {
        GameObject pooledObject = GetAvaliablePooledObject();
        if (pooledObject == null)
        {
            Debug.LogWarning($"Pool {poolName} cannot activate a new object, because it is fully utilized, and it is not allowed to expand!");
            return null;
        }

        pooledObject.SetActive(true);
        return pooledObject;
    }
}
