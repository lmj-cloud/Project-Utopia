using UnityEngine;

// Attach only to the object holding the Animator, then connect its parent action controller.
public class ActionAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMeleeCombat melee;
    [SerializeField] private ItemUseController itemUse;
    [SerializeField] private DoorController door;
    [SerializeField] private string actionState;
    [SerializeField] private int layer;
    private int armedId;
    private int observedId = -1;
    private bool armed;
    private int armedFrame;
    private int CurrentId => melee != null ? melee.RequestId : itemUse != null ? itemUse.RequestId : door != null ? door.RequestId : -1;
    private void Awake() { if (animator == null) animator = GetComponent<Animator>(); }
    private void Update()
    {
        if (CurrentId != observedId) { observedId = CurrentId; armed = false; }
    }
    // Clip: place ArmAction near normalized time 0, then ActionHit/ActionCompleted at its real timing.
    public void ArmAction(AnimationEvent evt)
    {
        if (!MatchesCurrentState(evt) || evt.animatorStateInfo.normalizedTime > 0.15f) return;
        armedId = CurrentId;
        observedId = armedId;
        armedFrame = Time.frameCount;
        armed = true;
    }
    private bool MatchesCurrentState(AnimationEvent evt)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(actionState) || layer < 0 || layer >= animator.layerCount) return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
        return current.IsName(actionState) && current.fullPathHash == evt.animatorStateInfo.fullPathHash &&
            Mathf.Abs(current.normalizedTime - evt.animatorStateInfo.normalizedTime) < 0.15f;
    }
    public void ActionHit(AnimationEvent evt)
    {
        if (!armed || armedId != CurrentId || Time.frameCount <= armedFrame || !MatchesCurrentState(evt)) return;
        armed = false;
        if (melee != null) melee.OnAttackHitFrame(armedId);
    }
    public void ActionCompleted(AnimationEvent evt)
    {
        if (!armed || armedId != CurrentId || Time.frameCount <= armedFrame || !MatchesCurrentState(evt)) return;
        armed = false;
        if (itemUse != null) itemUse.CompleteUseFromAnimation(armedId);
        else if (door != null) door.CompleteOpenFromAnimation(armedId);
    }
    private void OnDisable() { armed = false; observedId = -1; }
}
