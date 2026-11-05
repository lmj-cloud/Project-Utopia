using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Page2의 정상 완료와 검은 화면을 확인한 뒤 화면 소유권만 다음 씬으로 인계한다.</summary>
public class Page2Page3Transition : MonoBehaviour
{
    [SerializeField] private Page2StoryController source;
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private string destinationScene = "Assets/Scenes/Page3.unity";
    public static Page2Page3Transition Active { get; private set; }
    public int LoadCount { get; private set; }
    public bool IsTransitioning { get; private set; }
    private Page3StoryController receiver;

    private void OnEnable()
    {
        if (source != null) source.OnPage2Completed.AddListener(Begin);
    }

    private void Begin()
    {
        if (IsTransitioning || Active != null || source == null || source.Stage != Page2Stage.Completed ||
            source.CompletionCount != 1 || source.BlackAlpha < .999f) return;
        if (blackOverlay == null || !Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogError("[Page2 → Page3] Page3 빌드 등록과 인계 Overlay를 확인하세요.", this);
            return;
        }
        blackOverlay.alpha = 1;
        blackOverlay.blocksRaycasts = true;
        IsTransitioning = true;
        Active = this;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(Load());
    }

    private IEnumerator Load()
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        if (operation == null) yield break;
        LoadCount++;
        yield return operation;
        float deadline = Time.realtimeSinceStartup + 20f;
        while (receiver == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (receiver == null)
        {
            Debug.LogError("[Page3] 준비 확인 실패. 검은 화면을 유지합니다.", this);
            yield break;
        }
        // Page3의 검은 Overlay와 Intro 잠금이 먼저 준비되어 있으므로 틈이 생기지 않는다.
        receiver.AcceptArrival();
        if (!receiver.MemoryStarted) yield break;
        blackOverlay.alpha = 0;
        Active = null;
        Destroy(gameObject);
    }

    public bool NotifyReady(Page3StoryController value)
    {
        if (!IsTransitioning || receiver != null || value == null || value.gameObject.scene.path != destinationScene) return false;
        receiver = value;
        return true;
    }

    private void OnDisable()
    {
        if (source != null) source.OnPage2Completed.RemoveListener(Begin);
        if (Active == this) Active = null;
    }
}
