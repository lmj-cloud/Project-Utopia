using UnityEngine;
using System.Collections;
using Capstone.Direction.Workspace.S10;

/// <summary>원본 S10의 Signal·트럭 연출을 보존하며 직접 운전 차량에서 연출 리그로 인계한다.</summary>
public class Page3S10Adapter : MonoBehaviour
{
    [SerializeField] private Page3StoryController story;
    [SerializeField] private PlayerController player;
    [SerializeField] private VehicleController vehicle;
    [SerializeField] private Rigidbody vehicleBody;
    [SerializeField] private Camera vehicleCamera;
    [SerializeField] private AudioListener vehicleListener;
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private S10CrashSequence sequence;
    [SerializeField] private S10StoryAdapter storyAdapter;
    [SerializeField] private Light homeSun;
    [SerializeField] private OfficialDialoguePlayer officialDialogue;
    [SerializeField] private PageTimelineSequence presentation;
    private bool pending;
    public S10CrashSequence Sequence { get { return sequence; } }
    public int StartCount { get; private set; }
    public int CompletionCount { get; private set; }
    private bool owned;
    private bool completed;
    private bool wasKinematic;
    private bool sunEnabled;
    private Vector3 velocity;
    private Vector3 angularVelocity;
    private Renderer[] renderers;
    private bool[] hidden;

    private void OnEnable()
    {
        if (storyAdapter != null) storyAdapter.onHospitalRequested.AddListener(HospitalRequested);
        if (sequence != null)
        {
            sequence.onCancelled.AddListener(Cancelled);
            sequence.sequence.director.played += OriginalTimelineStarted;
        }
    }
    private void OriginalTimelineStarted(UnityEngine.Playables.PlayableDirector source)
    {
        if (!owned || presentation == null || !presentation.IsRunning) return;
        presentation.Director.time = 0;
        presentation.Director.Evaluate();
    }

    public void RequestAccident(VehicleController incoming)
    {
        if (pending || owned || completed || incoming != vehicle || !vehicle.IsOccupied || vehicle.Driver != player || story.Stage != Page3Stage.Driving) return;
        pending = true;
        StartCoroutine(WaitForFamilyDialogue());
    }
    private IEnumerator WaitForFamilyDialogue()
    {
        // 실제 사고 경계에 진입한 뒤에만 기존 차량의 잠금/브레이크 정책으로 대사 순서를 보장한다.
        player.AcquireControlLock(this);
        while (story.Stage == Page3Stage.Driving && !story.CanStartAccident) yield return null;
        player.ReleaseControlLock(this);
        pending = false;
        if (story.CanStartAccident) TryStartAccident(vehicle);
    }

    public bool TryStartAccident(VehicleController incoming)
    {
        // 도보 진입, 빈 차량, 다른 차량, 재진입은 사고를 시작하지 않는다.
        if (owned || completed || incoming != vehicle || !vehicle.IsOccupied || vehicle.Driver != player ||
            !story.CanStartAccident) return false;
        owned = true;
        wasKinematic = vehicleBody.isKinematic;
        velocity = vehicleBody.linearVelocity;
        angularVelocity = vehicleBody.angularVelocity;
        vehicleBody.linearVelocity = Vector3.zero;
        vehicleBody.angularVelocity = Vector3.zero;
        vehicleBody.isKinematic = true;
        renderers = vehicle.GetComponentsInChildren<Renderer>(true);
        hidden = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            hidden[i] = renderers[i].forceRenderingOff;
            renderers[i].forceRenderingOff = true;
        }
        // 실제 차량의 진입 방향을 원본 리그의 로컬 +Z와 맞춘다. 원본 트럭 궤적은 유지한다.
        Vector3 forward = Vector3.ProjectOnPlane(vehicle.transform.forward, Vector3.up).normalized;
        presentationRoot.transform.SetPositionAndRotation(vehicle.transform.position, Quaternion.LookRotation(forward));
        vehicleCamera.enabled = false;
        if (vehicleListener != null) vehicleListener.enabled = false;
        sunEnabled = homeSun != null && homeSun.enabled;
        if (homeSun != null) homeSun.enabled = false;
        story.AccidentStarted();
        presentationRoot.SetActive(true);
        sequence.enabled = true;
        sequence.outputCamera.enabled = true;
        AudioListener listener = sequence.outputCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = true;
        storyAdapter.incoming = new S10DriveState { vehicleBoarded = true, departureReady = true, sourceSequence = "Page3Driving" };
        // Director를 직접 Play하지 않고 기존 S10StoryAdapter가 가진 진입 API 하나만 사용한다.
        storyAdapter.ReceiveS09();
        if (!sequence.Active)
        {
            Cancelled();
            return false;
        }
        StartCount++;
        if (presentation != null) presentation.Begin();
        return true;
    }

    private void Update()
    {
        if (owned && !completed && sequence.Phase == "Blackout") story.AccidentFadeStarted();
    }

    private void HospitalRequested()
    {
        S10HospitalState state = storyAdapter.LastHospitalState;
        if (!owned || completed || !sequence.Completed || sequence.CompletionCount != 1 || sequence.ImpactCount != 1 ||
            sequence.HandoffAlpha < .999f || state == null || !state.accidentOccurred || !state.requiresBlackStart) return;
        completed = true;
        CompletionCount++;
        // Page4가 아직 없으므로 원본의 병원 인계 사실만 받아 검은 화면을 유지한다.
        if (presentation != null) presentation.StopVisuals();
        story.S10Completed();
    }

    private void Cancelled()
    {
        if (!owned || completed) return;
        Restore();
        story.AccidentCancelled();
    }

    private void Restore()
    {
        if (!owned) return;
        owned = false;
        if (presentationRoot != null) presentationRoot.SetActive(false);
        if (vehicleBody != null)
        {
            vehicleBody.isKinematic = wasKinematic;
            if (!wasKinematic) { vehicleBody.linearVelocity = velocity; vehicleBody.angularVelocity = angularVelocity; }
        }
        if (renderers != null)
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].forceRenderingOff = hidden[i];
        if (vehicleCamera != null) vehicleCamera.enabled = true;
        if (vehicleListener != null) vehicleListener.enabled = true;
        if (homeSun != null) homeSun.enabled = sunEnabled;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (presentation != null) presentation.Cancel();
        if (pending && player != null) player.ReleaseControlLock(this);
        pending = false;
        if (storyAdapter != null) storyAdapter.onHospitalRequested.RemoveListener(HospitalRequested);
        if (sequence != null)
        {
            sequence.onCancelled.RemoveListener(Cancelled);
            sequence.sequence.director.played -= OriginalTimelineStarted;
            if (sequence.Active) sequence.Cancel();
        }
        Restore();
    }
}
