using System.Collections;
using UnityEngine;

namespace S03Prototype
{
    public sealed class S03AudioCues : MonoBehaviour
    {
        [SerializeField] private AudioSource doorSource;
        [SerializeField] private AudioSource crowdSource;
        [SerializeField, Range(0f, 1f)] private float doorVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float crowdTargetVolume = 0.65f;
        [SerializeField, Min(0f)] private float crowdFadeDuration = 2.5f;

        private Coroutine crowdRoutine;
        private bool doorPlayed;
        private bool timelineCrowdStarted;
        public bool DoorCueRequested { get; private set; }
        public bool CrowdCueRequested { get; private set; }
        public bool HasDoorClip => doorSource != null && doorSource.clip != null;
        public bool HasCrowdClip => crowdSource != null && crowdSource.clip != null;

        private void Awake()
        {
            if (doorSource != null) doorSource.playOnAwake = false;
            if (crowdSource != null)
            {
                crowdSource.playOnAwake = false;
                crowdSource.loop = true;
                crowdSource.Stop();
                crowdSource.volume = 0f;
            }
        }

        public void PlayDoorSound()
        {
            if (!isActiveAndEnabled) return;
            DoorCueRequested = true;
            // Missing clips are intentional placeholders during prototyping.
            if (!HasDoorClip || doorPlayed) return;
            doorPlayed = true;
            doorSource.volume = doorVolume;
            doorSource.Play();
        }

        public void StartCrowdAmbience()
        {
            if (!isActiveAndEnabled) return;
            CrowdCueRequested = true;
            if (!HasCrowdClip || crowdRoutine != null || crowdSource.isPlaying) return;
            crowdSource.volume = 0f;
            crowdSource.loop = true;
            crowdSource.Play();
            crowdRoutine = StartCoroutine(FadeCrowdIn());
        }

        private IEnumerator FadeCrowdIn()
        {
            float elapsed = 0f;
            while (elapsed < crowdFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                crowdSource.volume = Mathf.Lerp(0f, crowdTargetVolume,
                    Mathf.Clamp01(elapsed / crowdFadeDuration));
                yield return null;
            }
            crowdSource.volume = crowdTargetVolume;
            crowdRoutine = null;
        }

        public void StopCrowdAmbience()
        {
            if (crowdRoutine != null) StopCoroutine(crowdRoutine);
            crowdRoutine = null;
            timelineCrowdStarted = false;
            if (crowdSource != null)
            {
                crowdSource.Stop();
                crowdSource.volume = 0f;
            }
        }

        public void SetTimelineCrowdProgress(float progress)
        {
            if (!isActiveAndEnabled) return;
            CrowdCueRequested = true;
            if (!HasCrowdClip) return;
            if (!timelineCrowdStarted)
            {
                StopCrowdAmbience();
                timelineCrowdStarted = true;
                crowdSource.loop = true;
                crowdSource.Play();
            }
            crowdSource.volume = Mathf.Lerp(0f, crowdTargetVolume, Mathf.Clamp01(progress));
        }

        private void OnDisable()
        {
            StopCrowdAmbience();
            if (doorSource != null) doorSource.Stop();
        }
    }
}
