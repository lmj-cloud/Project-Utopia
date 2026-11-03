using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 충돌 효과 풀에서 사용할 효과 종류의 인덱스를 구분한다.
/// </summary>
public enum ImpactType { Normal = 0, Target}

/// <summary>
/// 충돌 표면에 맞는 파티클 프리팹 풀을 관리하고 Raycast 위치에 효과를 배치한다.
/// </summary>
public class ImpactMemoryPool : MonoBehaviour
{
    [SerializeField] private GameObject[] impactPrefab;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.02f;
    private MemoryPool[] memoryPool;
    // Target별 전용 프리팹을 기존 MemoryPool로 재사용한다. 일반 표면 풀과 분리한다.
    private readonly Dictionary<GameObject, MemoryPool> targetPools = new Dictionary<GameObject, MemoryPool>();

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
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

    /// <summary>
    /// Raycast 충돌 표면의 종류에 맞는 효과 재생을 요청한다.
    /// </summary>
    public void SpawnImpact(RaycastHit hit)
    {
        if (!isActiveAndEnabled || hit.collider == null) return;

        if (hit.collider.CompareTag("ImpactNormal") || hit.collider.CompareTag("Wall"))
        {
            OnSpawnImpact(ImpactType.Normal, hit.point + hit.normal * Mathf.Max(0f, surfaceOffset), Quaternion.LookRotation(hit.normal));
        }
        if (hit.collider.CompareTag("Target"))
        {
            OnSpawnImpact(ImpactType.Target, hit.point + hit.normal * Mathf.Max(0f, surfaceOffset), Quaternion.LookRotation(hit.normal));
        }
    }

    /// <summary>무기가 제거될 때 이 컴포넌트가 소유한 Target 효과 풀을 정리한다.</summary>
    private void OnDestroy()
    {
        foreach (MemoryPool pool in targetPools.Values)
        {
            pool.DestroyObjects();
        }
        targetPools.Clear();
    }

    /// <summary>
    /// 해당 충돌 효과 풀에서 오브젝트를 꺼내 위치·회전과 재생을 설정한다.
    /// </summary>
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
