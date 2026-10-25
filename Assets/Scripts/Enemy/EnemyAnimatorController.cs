using UnityEngine;

/// <summary>
/// MMA 적의 전투 판단과 분리하여 Animator 파라미터와 실제 Clip 이벤트만 전달한다.
/// 모션 재생을 명시적으로 허용하고 모든 Clip을 연결하기 전에는 빈 상태를 구동하지 않는다.
/// </summary>
public class EnemyAnimatorController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Empty Controller 준비 단계에서는 끈다. 실제 State Motion과 아래 Clip 참조를 함께 연결한 후 켠다.")]
    [SerializeField] private bool enableMotionPlayback;
    [Header("실제 State Motion과 일치해야 하는 Clip")]
    [SerializeField] private AnimationClip idleMotion;
    [SerializeField] private AnimationClip walkMotion;
    [SerializeField] private AnimationClip punchLeftMotion;
    [SerializeField] private AnimationClip punchRightMotion;
    [SerializeField] private AnimationClip guardMotion;
    [SerializeField] private AnimationClip hitMotion;

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int IsGuarding = Animator.StringToHash("IsGuarding");
    private static readonly int PunchLeft = Animator.StringToHash("PunchLeft");
    private static readonly int PunchRight = Animator.StringToHash("PunchRight");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int IsAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int IsTraining = Animator.StringToHash("IsTraining");
    private bool hasWarned;
    private bool isAttacking;
    private SparringOpponent attackOwner;
    private int requestId;
    private bool isRightPunch;
    private int startedFrame;
    private int armedFrame;
    private bool isArmed;
    private bool hasConsumedHit;
    private bool hasConsumedComplete;

    public bool MotionPlaybackEnabled { get { return enableMotionPlayback; } }

    /// <summary>실제 애니메이션 기반 대련을 시작해도 되는지 공개 준비 검사를 제공한다.</summary>
    public bool IsReadyForPlayback()
    {
        return CanAnimate();
    }

    /// <summary>현재 전용 Controller와 Clip 연결이 실제 모션 재생 준비를 마쳤는지 검사한다.</summary>
    private bool CanAnimate(bool allowInactive = false)
    {
        if (!allowInactive && !isActiveAndEnabled) return false;
        if (animator == null)
        {
            WarnOnce("Animator 참조가 없습니다. 모션 재생은 생략합니다.");
            return false;
        }
        if (!enableMotionPlayback) return false;
        if (!animator.isActiveAndEnabled || !animator.isInitialized || animator.runtimeAnimatorController == null)
        {
            WarnOnce("Animator가 활성화·초기화되지 않았거나 Controller가 없습니다.");
            return false;
        }
        if (!HasParameter(Speed, AnimatorControllerParameterType.Float) ||
            !HasParameter(IsGuarding, AnimatorControllerParameterType.Bool) ||
            !HasParameter(PunchLeft, AnimatorControllerParameterType.Trigger) ||
            !HasParameter(PunchRight, AnimatorControllerParameterType.Trigger) ||
            !HasParameter(Hit, AnimatorControllerParameterType.Trigger) ||
            !HasParameter(IsAttacking, AnimatorControllerParameterType.Bool) ||
            !HasParameter(IsTraining, AnimatorControllerParameterType.Bool))
        {
            WarnOnce("전용 파라미터 7개의 이름과 자료형을 확인하세요.");
            return false;
        }
        if (!HasMotion("Idle", idleMotion, false) || !HasMotion("Walk", walkMotion, false) ||
            !HasMotion("PunchLeft", punchLeftMotion, true) || !HasMotion("PunchRight", punchRightMotion, true) ||
            !HasMotion("GuardIdle", guardMotion, false) || !HasMotion("HitReaction", hitMotion, true))
        {
            WarnOnce("실제 State Motion과 Clip 6개를 연결하고 단발 모션의 Loop를 해제하세요.");
            return false;
        }
        return true;
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash && parameter.type == type) return true;
        }
        return false;
    }

    private bool HasMotion(string state, AnimationClip clip, bool oneShot)
    {
        if (clip == null || clip.length <= 0f || (oneShot && clip.isLooping) ||
            !animator.HasState(0, Animator.StringToHash("Base Layer." + state))) return false;
        foreach (AnimationClip connected in animator.runtimeAnimatorController.animationClips)
        {
            if (connected == clip) return true;
        }
        return false;
    }

    /// <summary>실제 모션 준비가 된 경우에만 NavMesh 이동 속도를 전달한다.</summary>
    public void SetMovementSpeed(float speed)
    {
        if (CanAnimate()) animator.SetFloat(Speed, Mathf.Max(0f, speed));
    }

    /// <summary>반대쪽 Trigger를 비우고 왼손 공격을 요청한다.</summary>
    public void PlayPunchLeft()
    {
        if (!CanAnimate()) return;
        animator.ResetTrigger(PunchRight);
        animator.ResetTrigger(PunchLeft);
        animator.SetTrigger(PunchLeft);
    }

    /// <summary>반대쪽 Trigger를 비우고 오른손 공격을 요청한다.</summary>
    public void PlayPunchRight()
    {
        if (!CanAnimate()) return;
        animator.ResetTrigger(PunchLeft);
        animator.ResetTrigger(PunchRight);
        animator.SetTrigger(PunchRight);
    }

    /// <summary>공격 중 방어 금지를 표현하고 방어 상태를 전달한다. 판단 자체는 AI가 한다.</summary>
    public void SetGuard(bool guarding)
    {
        if (CanAnimate()) animator.SetBool(IsGuarding, guarding && !isAttacking);
    }

    /// <summary>공격 준비부터 회복 종료까지의 상태를 전달한다.</summary>
    public void SetAttacking(bool attacking)
    {
        isAttacking = attacking;
        if (!CanAnimate()) return;
        animator.SetBool(IsAttacking, attacking);
        if (attacking) animator.SetBool(IsGuarding, false);
    }

    /// <summary>실제 피해에 대응하는 피격 Trigger를 요청한다. 피해 계산은 수행하지 않는다.</summary>
    public void PlayHit()
    {
        if (!CanAnimate()) return;
        animator.ResetTrigger(Hit);
        animator.SetTrigger(Hit);
    }

    /// <summary>대련 진행 여부를 전용 bool 파라미터에 전달한다.</summary>
    public void SetTraining(bool training)
    {
        if (CanAnimate()) animator.SetBool(IsTraining, training);
    }

    /// <summary>대련 종료 시 Trigger와 행동 상태를 정리한다. 빈 Idle을 재생하지 않는다.</summary>
    public void ResetCombatAnimation()
    {
        attackOwner = null;
        isArmed = false;
        isAttacking = false;
        if (!CanAnimate(true)) return;
        animator.ResetTrigger(PunchLeft);
        animator.ResetTrigger(PunchRight);
        animator.ResetTrigger(Hit);
        animator.SetFloat(Speed, 0f);
        animator.SetBool(IsGuarding, false);
        animator.SetBool(IsAttacking, false);
        animator.SetBool(IsTraining, false);
        animator.Play(Animator.StringToHash("Base Layer.Idle"), 0, 0f);
    }

    /// <summary>이벤트 모드에서 실제 사용할 손별 모션이 준비됐는지 확인한다.</summary>
    public bool CanPlayPunch(bool right)
    {
        if (!CanAnimate()) return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        return !current.IsName("Base Layer.HitReaction") || current.normalizedTime >= 1f;
    }

    /// <summary>실행 시의 토큰과 손을 기록한다. Clip에 고정된 요청 번호를 저장하지 않는다.</summary>
    public void BindAttackRequest(SparringOpponent owner, int id, bool right)
    {
        attackOwner = owner;
        requestId = id;
        isRightPunch = right;
        startedFrame = Time.frameCount;
        isArmed = false;
        hasConsumedHit = false;
        hasConsumedComplete = false;
    }

    /// <summary>현재 토큰의 수신 상태와 남은 공격 Trigger만 정리한다.</summary>
    public void ClearAttackRequest(int id)
    {
        if (attackOwner == null || requestId != id) return;
        attackOwner = null;
        isArmed = false;
        if (!CanAnimate()) return;
        animator.ResetTrigger(PunchLeft);
        animator.ResetTrigger(PunchRight);
    }

    private bool MatchesEvent(AnimationEvent animationEvent)
    {
        if (attackOwner == null || attackOwner.AttackTiming != ActionTimingMode.AnimationEvent ||
            !attackOwner.OwnsAttack(requestId, isRightPunch) || animationEvent == null || !CanAnimate() ||
            Time.frameCount < startedFrame || animator.IsInTransition(0)) return false;
        string state = isRightPunch ? "Base Layer.PunchRight" : "Base Layer.PunchLeft";
        AnimationClip clip = isRightPunch ? punchRightMotion : punchLeftMotion;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        return current.IsName(state) && current.fullPathHash == animationEvent.animatorStateInfo.fullPathHash &&
            animationEvent.animatorClipInfo.clip == clip &&
            Mathf.Abs(current.normalizedTime - animationEvent.animatorStateInfo.normalizedTime) < 0.1f;
    }

    /// <summary>공격 Clip 초반 0~0.15 구간의 Arm 이벤트로 후속 이벤트 수신을 준비한다.</summary>
    public void ArmAttack(AnimationEvent animationEvent)
    {
        if (!MatchesEvent(animationEvent) || animationEvent.animatorStateInfo.normalizedTime > 0.15f) return;
        isArmed = true;
        armedFrame = Time.frameCount;
    }

    /// <summary>검증된 Clip의 타격 이벤트를 토큰과 손 구분을 유지해 한 번 전달한다.</summary>
    public void OnAttackHitFrame(AnimationEvent animationEvent)
    {
        if (!isArmed || Time.frameCount <= armedFrame || hasConsumedHit || hasConsumedComplete || !MatchesEvent(animationEvent)) return;
        hasConsumedHit = true;
        attackOwner.OnAttackHitFrame(requestId, isRightPunch);
    }

    /// <summary>타격과 별도로 실제 Clip의 완료 이벤트를 한 번 전달한다.</summary>
    public void OnAttackAnimationFinished(AnimationEvent animationEvent)
    {
        if (!isArmed || Time.frameCount <= armedFrame || hasConsumedComplete || !MatchesEvent(animationEvent)) return;
        hasConsumedComplete = true;
        attackOwner.OnAttackAnimationFinished(requestId, isRightPunch);
    }

    private void WarnOnce(string reason)
    {
        if (hasWarned) return;
        hasWarned = true;
        Debug.LogWarning("EnemyAnimatorController: " + reason, this);
    }

    /// <summary>비활성화 시 진행 중 요청을 취소하고 남은 전투 표현을 정리한다.</summary>
    private void OnDisable()
    {
        if (attackOwner != null) attackOwner.EndTraining();
        ResetCombatAnimation();
    }
}
