using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class CutsceneController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private UnityEvent onFinished = new();

    private PlayableDirector director;
    private bool isPlaying;

    private void Awake()
    {
        director = GetComponent<PlayableDirector>();
        director.playOnAwake = false;
        // 마지막 프레임을 유지하지 않고 정상 종료하여 입력 잠금을 해제한다.
        director.extrapolationMode = DirectorWrapMode.None;
    }

    private void OnEnable()
    {
        director.played += OnPlayed;
        director.stopped += OnStopped;
    }

    public void PlayCutscene()
    {
        if (!isActiveAndEnabled || isPlaying || director.playableAsset == null) return;
        if (playerController == null)
        {
            Debug.LogWarning("CutsceneController의 Player Controller를 연결하세요.", this);
            return;
        }
        isPlaying = true;
        playerController.AcquireControlLock(this);
        director.time = 0;
        director.Play();
    }

    public void StopCutscene()
    {
        if (isPlaying) director.Stop();
    }

    private void OnPlayed(PlayableDirector source)
    {
        isPlaying = true;
        if (playerController != null) playerController.AcquireControlLock(this);
    }

    private void OnStopped(PlayableDirector source)
    {
        if (!isPlaying) return;
        isPlaying = false;
        if (playerController != null) playerController.ReleaseControlLock(this);
        onFinished.Invoke();
    }

    private void OnDisable()
    {
        director.played -= OnPlayed;
        director.stopped -= OnStopped;
        if (isPlaying) director.Stop();
        isPlaying = false;
        if (playerController != null) playerController.ReleaseControlLock(this);
    }
}
