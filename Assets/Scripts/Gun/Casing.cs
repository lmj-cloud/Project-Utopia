using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 발사된 탄피의 초기 물리 힘과 충돌 소리, 수명 종료 후 풀 반환을 처리한다.
/// </summary>
public class Casing : MonoBehaviour
{
    [SerializeField] private float deactiveTime = 5.0f;
    [SerializeField] private float casingSpin = 1.0f;
    [SerializeField] private AudioClip[] audioClips;

    private Rigidbody rigidbody3D;
    private AudioSource audioSource;
    private MemoryPool memoryPool;

    /// <summary>
    /// 탄피를 꺼낸 풀과 초기 물리 상태를 설정하고 수명 종료를 예약한다.
    /// </summary>
    public void SetUp(MemoryPool pool, Vector3 direction)
    {
        rigidbody3D = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        memoryPool = pool;

        rigidbody3D.linearVelocity = new Vector3(direction.x, 1.0f, direction.z);
        rigidbody3D.angularVelocity = new Vector3(Random.Range(-casingSpin, casingSpin), 
                                                  Random.Range(-casingSpin, casingSpin), 
                                                  Random.Range(-casingSpin, casingSpin));

        StartCoroutine("DeactivateAfterTime");
    }

    /// <summary>
    /// 탄피의 첫 충돌 등 설정된 물리 충돌에 맞는 후속 처리를 수행한다.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        int index = Random.Range(0, audioClips.Length);
        audioSource.clip = audioClips[index];
        audioSource.Play();
    }

    /// <summary>
    /// 탄피 수명 동안 기다린 뒤 자신을 풀에 반환한다.
    /// </summary>
    private IEnumerator DeactivateAfterTime()
    {
        yield return new WaitForSeconds(deactiveTime);

        memoryPool.DeactivatePoolItem(this.gameObject);
    }
}
