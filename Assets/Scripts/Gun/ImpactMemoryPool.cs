using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ImpactType { Normal = 0, Obstacle}

public class ImpactMemoryPool : MonoBehaviour
{
    [SerializeField] private GameObject[] impactPrefab;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.02f;
    private MemoryPool[] memoryPool;

    void Awake()
    {
        if (impactPrefab == null) return;
        memoryPool = new MemoryPool[impactPrefab.Length];
        for(int i = 0; i < impactPrefab.Length; i++)
        {
            if (impactPrefab[i] == null || impactPrefab[i].GetComponent<Impact>() == null ||
                impactPrefab[i].GetComponent<ParticleSystem>() == null)
            {
                Debug.LogWarning("ImpactMemoryPool: Connect a prefab with Impact and ParticleSystem.", this);
                continue;
            }
            memoryPool[i] = new MemoryPool(impactPrefab[i]);
        }
    }

    public void SpawnImpact(RaycastHit hit)
    {
        if (!isActiveAndEnabled || hit.collider == null) return;
        if (hit.collider.CompareTag("ImpactNormal"))
        {
            OnSpawnImpact(ImpactType.Normal, hit.point + hit.normal * Mathf.Max(0f, surfaceOffset), Quaternion.LookRotation(hit.normal));
        }
    }

    public void OnSpawnImpact(ImpactType type, Vector3 position, Quaternion rotation)
    {
        int index = (int)type;
        if (!isActiveAndEnabled || memoryPool == null || index < 0 || index >= memoryPool.Length || memoryPool[index] == null) return;
        MemoryPool pool = memoryPool[index];
        GameObject item = pool.ActivatePoolItem();
        if (item == null) return;
        item.transform.position = position;
        item.transform.rotation = rotation;
        Impact impact = item.GetComponent<Impact>();
        if (impact == null) { pool.DeactivatePoolItem(item); return; }
        impact.Setup(pool);
    }
}
