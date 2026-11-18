using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DefaultExecutionOrder(-20)]
public sealed class Page4CorridorPerformance : MonoBehaviour
{
    private const float OfficerGlanceSeconds = .75f;
    private const float OfficerGlanceBlendSpeed = 4f;

    [SerializeField] private PlayableDirector director;
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private Animator officer;
    [SerializeField] private Animator player;
    [SerializeField] private AnimationClip talk;
    [SerializeField] private AvatarMask upperBody;
    [SerializeField] private CanvasGroup blackout;

    private AnimationLayerMixerPlayable layers;
    private AnimationClipPlayable speech;
    private Transform officerHead;
    private Transform officerChest;
    private Transform playerHead;
    private Transform playerChest;
    private bool officerSpeaking;
    private bool playerSpeaking;
    private float talkWeight;
    private float lookWeight;
    private float officerGlanceUntil;
    private float officerGlanceWeight;

    private void Awake()
    {
        officerHead = officer.GetBoneTransform(HumanBodyBones.Head);
        officerChest = officer.GetBoneTransform(HumanBodyBones.Chest);
        playerHead = player.GetBoneTransform(HumanBodyBones.Head);
        playerChest = player.GetBoneTransform(HumanBodyBones.Chest);
    }

    private void OnEnable()
    {
        director.played += Started;
        director.stopped += Stopped;
        dialogue.LinePresented += Presented;
        dialogueUI.LineCompleted += LineCompleted;
    }

    private void OnDisable()
    {
        director.played -= Started;
        director.stopped -= Stopped;
        dialogue.LinePresented -= Presented;
        dialogueUI.LineCompleted -= LineCompleted;
    }

    private void Started(PlayableDirector source)
    {
        if (source.time < .05) blackout.alpha = 1f;
    }

    private void Stopped(PlayableDirector source)
    {
        officerSpeaking = playerSpeaking = false;
        talkWeight = lookWeight = 0f;
        officerGlanceUntil = officerGlanceWeight = 0f;
        layers = default;
        speech = default;
    }

    private void Presented(OfficialStoryScript.Line line)
    {
        if (!IsWalking()) return;
        officerSpeaking = line.speaker == "장교";
        playerSpeaking = line.speakerKind == DialogueSpeaker.Player;
        if (officerSpeaking)
        {
            officerGlanceUntil = Time.unscaledTime + OfficerGlanceSeconds;
        }
    }

    private void LineCompleted(int lineIndex) => officerSpeaking = playerSpeaking = false;

    private bool IsWalking() => director.state == PlayState.Playing && director.time < 31.85;

    private void Update()
    {
        if (!IsWalking() || !director.playableGraph.IsValid()) return;
        if (!layers.IsValid()) AttachSpeech();
        if (!layers.IsValid()) return;

        bool speaking = dialogueUI.IsOpen && officerSpeaking;
        if (speaking && talkWeight <= 0f) speech.SetTime(0);
        talkWeight = Mathf.MoveTowards(talkWeight, speaking ? .55f : 0f, Time.unscaledDeltaTime * 3f);
        layers.SetInputWeight(1, talkWeight);
    }

    private void AttachSpeech()
    {
        var graph = director.playableGraph;
        for (int i = 0; i < graph.GetOutputCount(); i++)
        {
            var output = graph.GetOutput(i);
            if (output.GetPlayableOutputType() != typeof(AnimationPlayableOutput)) continue;
            var animation = (AnimationPlayableOutput)output;
            var source = animation.GetSourcePlayable();
            if (animation.GetTarget() != officer || !source.IsValid()) continue;

            int port = animation.GetSourceOutputPort();
            if (port < 0 || port >= source.GetInputCount()) continue;
            var walk = source.GetInput(port);
            if (!walk.IsValid()) continue;

            // Timeline outputs select a track via the root input index.
            float trackWeight = source.GetInputWeight(port);
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            layers.SetOutputCount(1);
            speech = AnimationClipPlayable.Create(graph, talk);
            speech.SetApplyFootIK(false);
            graph.Disconnect(source, port);
            graph.Connect(walk, 0, layers, 0);
            graph.Connect(layers, 0, source, port);
            source.SetInputWeight(port, trackWeight);
            graph.Connect(speech, 0, layers, 1);
            layers.SetInputWeight(0, 1f);
            layers.SetLayerMaskFromAvatarMask(1, upperBody);

            return;
        }
    }

    private void LateUpdate()
    {
        if (!IsWalking()) return;
        bool speaking = dialogueUI.IsOpen && (officerSpeaking || playerSpeaking);
        ApplyOfficerGlance();
        lookWeight = Mathf.MoveTowards(lookWeight, speaking ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (playerHead != null && officerHead != null)
        {
            Vector3 direction = officerHead.position - playerHead.position;
            Vector3 facing = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
            float angle = Mathf.Clamp(Vector3.SignedAngle(facing, Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up), -10f, 10f) * lookWeight;
            if (playerChest != null) playerChest.rotation = Quaternion.AngleAxis(angle * .25f, Vector3.up) * playerChest.rotation;
            playerHead.rotation = Quaternion.AngleAxis(angle * .75f, Vector3.up) * playerHead.rotation;
        }
    }

    private void ApplyOfficerGlance()
    {
        if (officerHead == null || playerHead == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(officer.transform.forward, Vector3.up).normalized;
        Vector3 toPlayer = playerHead.position - officerHead.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(toPlayer, Vector3.up);
        float yaw = Vector3.SignedAngle(forward, horizontal, Vector3.up);
        bool glancing = dialogueUI.IsOpen && officerSpeaking &&
            Time.unscaledTime < officerGlanceUntil && Mathf.Abs(yaw) < 125f;
        officerGlanceWeight = Mathf.MoveTowards(officerGlanceWeight, glancing ? 1f : 0f,
            Time.unscaledDeltaTime * OfficerGlanceBlendSpeed);
        if (officerGlanceWeight <= 0f) return;

        // 이동 방향과 보행 자세는 Timeline에 두고, 짧은 시선만 평가된 포즈에 더한다.
        float glanceYaw = Mathf.Clamp(yaw, -30f, 30f) * officerGlanceWeight;
        float pitch = Mathf.Atan2(-toPlayer.y, horizontal.magnitude) * Mathf.Rad2Deg;
        float glancePitch = Mathf.Clamp(pitch, -12f, 12f) * officerGlanceWeight;
        float chestYaw = 0f;
        if (officerChest != null)
        {
            chestYaw = glanceYaw * .25f;
            officerChest.rotation = Quaternion.AngleAxis(chestYaw, Vector3.up) * officerChest.rotation;
        }

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        officerHead.rotation = Quaternion.AngleAxis(glanceYaw - chestYaw, Vector3.up) *
            Quaternion.AngleAxis(glancePitch, right) * officerHead.rotation;
    }
}



