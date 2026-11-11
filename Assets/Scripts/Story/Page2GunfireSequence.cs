using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>기존 Page2 총성 Signal에 연결하는 시각·음향 전용 시퀀스.</summary>
public class Page2GunfireSequence : MonoBehaviour
{
    [Serializable]
    private class Shot
    {
        [Min(0f)] public float delay;
        public Transform start;
        public Transform target;
        public bool metalImpact;
    }

    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private Page2BulletProjectile bulletPrefab;
    [SerializeField] private Transform runtimeEffects;
    [SerializeField] private ParticleSystem sparkImpact;
    [SerializeField] private ParticleSystem dustImpact;
    [SerializeField] private Shot[] shots = new Shot[0];
    [Header("첫 발은 기존 Timeline Audio, 이후 발은 최대 4개의 3D 음원")]
    [SerializeField] private AudioClip gunshot;
    [SerializeField] private AudioSource[] shotVoices = new AudioSource[0];
    [SerializeField] private bool firstShotUsesTimelineAudio = true;
    [SerializeField, Range(0f, 1f)] private float shotVolume = .65f;
    private double startTime;
    private int nextShot;
    private int nextVoice;
    private bool isRunning;
    private bool hasStarted;
    public int SpawnedCount { get; private set; }
    public int ImpactCount { get; private set; }
    public int SoundCount { get; private set; }
    public bool IsRunning { get { return isRunning; } }

    private void OnEnable()
    {
        // 중단·재생·씬 종료에서도 이번 시퀀스가 만든 것만 정리한다.
        if (timeline == null) return;
        timeline.played += TimelinePlayed;
        timeline.stopped += TimelineStopped;
    }

    private void TimelinePlayed(PlayableDirector source)
    {
        StopGunfire();
        hasStarted = false;
    }

    public void StartGunfire()
    {
        if (!Application.isPlaying || hasStarted || isRunning || timeline == null ||
            bulletPrefab == null || runtimeEffects == null) return;
        hasStarted = true;
        isRunning = true;
        nextShot = 0;
        nextVoice = 0;
        SpawnedCount = 0;
        ImpactCount = 0;
        SoundCount = 0;
        startTime = timeline.time;
        FireDueShots();
    }

    private void Update()
    {
        if (isRunning) FireDueShots();
    }

    private void FireDueShots()
    {
        double elapsed = timeline.time - startTime;
        while (nextShot < shots.Length && elapsed >= shots[nextShot].delay)
        {
            Shot shot = shots[nextShot];
            int index = nextShot++;
            if (shot.start == null || shot.target == null) continue;
            Page2BulletProjectile bullet = Instantiate(bulletPrefab, runtimeEffects);
            Vector3 normal = Vector3.up;
            ParticleSystem impact = dustImpact;
            if (shot.metalImpact)
            {
                normal = (shot.start.position - shot.target.position).normalized;
                impact = sparkImpact;
            }
            bullet.Launch(shot.start.position, shot.target.position, normal, impact, runtimeEffects, this);
            SpawnedCount++;
            // 같은 첫 총성을 Timeline과 PlayOneShot에서 중복 재생하지 않는다.
            if (index == 0 && firstShotUsesTimelineAudio) continue;
            PlayShot(shot.start.position);
        }
    }

    private void PlayShot(Vector3 position)
    {
        if (gunshot == null || shotVoices.Length == 0) return;
        AudioSource voice = shotVoices[nextVoice];
        nextVoice = (nextVoice + 1) % shotVoices.Length;
        if (voice == null) return;
        // 긴 잔향이 무한 중첩되지 않게 가장 오래된 음원만 교체한다.
        voice.Stop();
        voice.transform.position = position;
        voice.PlayOneShot(gunshot, shotVolume);
        SoundCount++;
    }

    public void NotifyImpact()
    {
        ImpactCount++;
    }

    public void StopGunfire()
    {
        isRunning = false;
        if (shotVoices != null)
            foreach (AudioSource voice in shotVoices) if (voice != null) voice.Stop();
        if (runtimeEffects == null) return;
        foreach (Transform child in runtimeEffects)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void TimelineStopped(PlayableDirector source)
    {
        StopGunfire();
    }

    private void OnDisable()
    {
        if (timeline != null)
        {
            timeline.played -= TimelinePlayed;
            timeline.stopped -= TimelineStopped;
        }
        StopGunfire();
    }

    private void OnDrawGizmosSelected()
    {
        // 선택 상태로 녹화하더라도 Game View에는 디버그 표시를 그리지 않는다.
        if (Camera.current != null && Camera.current.cameraType != CameraType.SceneView) return;
        if (shots == null) return;
        Color previousColor = Gizmos.color;
        foreach (Shot shot in shots)
        {
            if (shot == null || shot.start == null || shot.target == null) continue;
            float startRadius = .13f;
            float targetRadius = .13f;
#if UNITY_EDITOR
            startRadius = Mathf.Max(startRadius, UnityEditor.HandleUtility.GetHandleSize(shot.start.position) * .06f);
            targetRadius = Mathf.Max(targetRadius, UnityEditor.HandleUtility.GetHandleSize(shot.target.position) * .06f);
#endif
            // 시작점은 S06CameraOrigin을 따르고, 목표점은 실제 링의 월드 위치를 유지한다.
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(shot.start.position, shot.target.position);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(shot.start.position, startRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(shot.target.position, targetRadius);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(shot.start.position + Vector3.up * .2f, shot.start.name);
            UnityEditor.Handles.Label(shot.target.position + Vector3.up * .2f, shot.target.name);
#endif
        }
        Gizmos.color = previousColor;
    }
}
