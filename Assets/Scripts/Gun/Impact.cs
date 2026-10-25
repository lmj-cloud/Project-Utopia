using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 충돌 파티클을 재생하고 재생 종료 후 해당 오브젝트를 풀로 반환한다.
/// </summary>
public class Impact : MonoBehaviour
{
    private ParticleSystem particle;
    private MemoryPool memoryPool;
    private bool isSetup;
    private int startedFrame;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        particle = GetComponent<ParticleSystem>();
    }

    /// <summary>
    /// 연결된 풀을 보관하고 충돌 파티클을 처음부터 재생한다.
    /// </summary>
    public void Setup(MemoryPool pool)
    {
        memoryPool = pool;
        isSetup = pool != null;
        startedFrame = Time.frameCount;
        if (particle == null) return;
        // Placement and pool ownership are ready before replaying the time-zero burst.
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (isSetup) particle.Play(true);
    }

    /// <summary>
    /// 생성 첫 프레임을 지나 파티클 재생이 끝났으면 현재 충돌 효과를 풀로 반환한다.
    /// </summary>
    private void Update()
    {
        if (!isSetup || memoryPool == null || Time.frameCount <= startedFrame) return;
        if (particle == null || !particle.IsAlive(true))
        {
            isSetup = false;
            MemoryPool pool = memoryPool;
            memoryPool = null;
            pool.DeactivatePoolItem(gameObject);
        }
    }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        isSetup = false;
        memoryPool = null;
        if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
