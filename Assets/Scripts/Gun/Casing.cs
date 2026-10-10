using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Casing : MonoBehaviour
{
    [SerializeField] private float deactiveTime = 5.0f;
    [SerializeField] private float casingSpin = 1.0f;
    [SerializeField] private AudioClip[] audioClips;

    private Rigidbody rigidbody3D;
    private AudioSource audioSource;
    private MemoryPool memoryPool;

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

    private void OnCollisionEnter(Collision collision)
    {
        int index = Random.Range(0, audioClips.Length);
        audioSource.clip = audioClips[index];
        audioSource.Play();
    }

    private IEnumerator DeactivateAfterTime()
    {
        yield return new WaitForSeconds(deactiveTime);

        memoryPool.DeactivatePoolItem(this.gameObject);
    }
}
