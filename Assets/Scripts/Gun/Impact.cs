using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Impact : MonoBehaviour
{
    private ParticleSystem particle;
    private MemoryPool memoryPool;
    private bool isSetup;
    private int startedFrame;

    private void Awake()
    {
        particle = GetComponent<ParticleSystem>();
    }

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
    private void OnDisable()
    {
        isSetup = false;
        memoryPool = null;
        if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
