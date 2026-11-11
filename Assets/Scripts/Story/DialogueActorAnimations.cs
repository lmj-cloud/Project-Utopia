using System;
using UnityEngine;

/// <summary>실제 발화자 이름 이벤트로 여러 NPC 중 현재 화자의 Talking만 재생한다.</summary>
public class DialogueActorAnimations : MonoBehaviour
{
    [Serializable]
    private class Actor
    {
        public string speaker;
        public Animator animator;
        public string[] aliases = new string[0];
        public string idleState = "Base Layer.NPC_Idle";
        public string talkingState = "Base Layer.NPC_Talk";
    }
    [SerializeField] private DialogueUI dialogue;
    [SerializeField] private Actor[] actors = new Actor[0];
    [SerializeField, Min(0f)] private float blendDuration = .15f;
    private bool speaking;

    private void OnEnable()
    {
        if (dialogue != null) dialogue.LinePresented += Presented;
    }
    private void Presented(int index, string name, string text)
    {
        speaking = true;
        foreach (Actor actor in actors)
        {
            bool matches = false;
            if (!string.IsNullOrEmpty(name))
            {
                foreach (string token in name.Split(','))
                {
                    string label = token.Trim();
                    if (Matches(label, actor.speaker)) matches = true;
                    if (actor.aliases != null)
                    {
                        foreach (string alias in actor.aliases)
                            if (Matches(label, alias)) matches = true;
                    }
                }
            }
            Play(actor, matches);
        }
    }
    private bool Matches(string label, string name)
    {
        return !string.IsNullOrEmpty(name) &&
            (label == name || label.StartsWith(name + "(") || label.StartsWith(name + " "));
    }
    private void Play(Actor actor, bool talking)
    {
        if (actor.animator == null || !actor.animator.isActiveAndEnabled || !actor.animator.isInitialized) return;
        int state = Animator.StringToHash(talking ? actor.talkingState : actor.idleState);
        if (actor.animator.HasState(0, state)) actor.animator.CrossFadeInFixedTime(state, blendDuration, 0);
    }
    private void ResetActors()
    {
        if (!speaking) return;
        speaking = false;
        foreach (Actor actor in actors) Play(actor, false);
    }
    private void LateUpdate()
    {
        // 기존 UI의 실제 닫힘을 확인해 완료·취소 모두 한 번만 Idle로 복구한다.
        if (speaking && (dialogue == null || !dialogue.IsOpen)) ResetActors();
    }
    private void OnDisable()
    {
        if (dialogue != null) dialogue.LinePresented -= Presented;
        ResetActors();
    }
}
