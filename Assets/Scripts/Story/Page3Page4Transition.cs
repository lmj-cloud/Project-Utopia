using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>완료된 S10의 검은 화면을 Page4 준비 확인까지 유지한다.</summary>
public class Page3Page4Transition : MonoBehaviour
{
    [SerializeField] private Page3StoryController source;
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private string destinationScene = "Assets/Scenes/Page4.unity";
    public static Page3Page4Transition Active { get; private set; }
    public int LoadCount { get; private set; }
    private bool loading;
    private Page4StoryController receiver;

    private void OnEnable()
    {
        if (source != null) source.OnPage4TransitionRequested.AddListener(Begin);
    }
    private void Begin()
    {
        if (loading || Active != null || source == null || source.Stage != Page3Stage.Completed ||
            source.Page3CompletionCount != 1 || source.BlackAlpha < .999f) return;
        if (!Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogError("[Page3 → Page4] Page4 Scene List 등록을 확인하세요.", this);
            return;
        }
        loading = true;
        Active = this;
        blackOverlay.alpha = 1;
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
        float deadline = Time.realtimeSinceStartup + 20;
        while (receiver == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (receiver == null)
        {
            Debug.LogError("[Page4] 준비 확인 실패. 검은 화면 유지.", this);
            yield break;
        }
        receiver.AcceptArrival();
        if (!receiver.IntroStarted) yield break;
        blackOverlay.alpha = 0;
        Active = null;
        Destroy(gameObject);
    }
    public bool NotifyReady(Page4StoryController value)
    {
        if (!loading || receiver != null || value == null || value.gameObject.scene.path != destinationScene) return false;
        receiver = value;
        return true;
    }
    private void OnDisable()
    {
        if (source != null) source.OnPage4TransitionRequested.RemoveListener(Begin);
        if (Active == this) Active = null;
    }
}
