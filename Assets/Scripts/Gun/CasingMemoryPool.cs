using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기존 탄피 프리팹을 풀에서 꺼내 발사 위치와 방향에 맞게 배치한다.
/// </summary>
public class CasingMemoryPool : MonoBehaviour
{
    [SerializeField] private GameObject casingPrefab;
    private MemoryPool memoryPool;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        memoryPool = new MemoryPool(casingPrefab);
    }

    /// <summary>
    /// 풀에서 탄피를 꺼내 발사 기준점과 배출 방향을 적용한다.
    /// </summary>
    public void SpawnCasing(Vector3 position, Vector3 direction)
    {
        GameObject item = memoryPool.ActivatePoolItem();
        item.transform.position = position;
        item.transform.rotation = Random.rotation;
        item.GetComponent<Casing>().SetUp(memoryPool, direction);
    }
}
