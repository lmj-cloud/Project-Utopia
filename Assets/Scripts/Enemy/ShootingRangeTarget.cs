using UnityEngine;

/// <summary>
/// 기존 CombatTarget 체력을 사용하여 사격장 표적의 왕복 이동과 사망 폭발을 처리한다.
/// </summary>
[RequireComponent(typeof(EnemyHealth), typeof(Rigidbody))]
public class ShootingRangeTarget : MonoBehaviour
{
    [Header("Target Movement")]
    [SerializeField] private bool enableMovement = true;
    [SerializeField, Min(0f)] private float forwardDistance = 3f;
    [SerializeField, Min(0f)] private float backwardDistance = 3f;
    [SerializeField, Min(0.01f)] private float moveSpeed = 1.5f;
    [SerializeField] private Vector3 localMoveAxis = Vector3.forward;
    [SerializeField, Min(0f)] private float endpointWaitTime = 0.3f;

    [Header("Target Combat")]
    [SerializeField] private EnemyHealth health;

    [Header("Target Animation")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField, Min(0f)] private float animationDampTime = 0.2f;
    [SerializeField] private string locomotionState = "Base Layer.Locomotion";
    [SerializeField] private string hitState = "Hit Layer.HitAction";
    [SerializeField] private string hitLayerName = "Hit Layer";
    [SerializeField] private string hitIdleState = "Hit Layer.New State";
    [SerializeField] private string hitTriggerName = "onHit";
    [SerializeField, Min(0.01f)] private float hitLayerBlendTime = 0.08f;
    [SerializeField, Min(0.01f)] private float hitReactionDuration = 0.34f;

    [Header("Target Respawn")]
    [SerializeField] private TargetRespawnController respawnController;

    [Header("Target Effects")]
    [SerializeField] private GameObject deathExplosionPrefab;
    [SerializeField] private Vector3 explosionLocalOffset = new Vector3(0f, 0.9f, 0f);

    private Rigidbody targetBody;
    private Vector3 startPosition;
    private Vector3 worldMoveDirection;
    private bool isMovingForward = true;
    private bool hasDied;
    private float waitRemaining;
    private MemoryPool explosionPool;
    private bool isReacting;
    private float hitRemaining;
    private float requestedAnimationDirection;
    private Quaternion startRotation;
    private Vector3 deathPosition;
    private Vector3 initialModelPosition;
    private int hitLayerIndex = -1;
    private int hitStateHash;
    private int hitIdleStateHash;
    private int hitTriggerHash;
    private bool hasEnteredHitState;
    private bool hasValidHitLayer;
    private Collider[] targetColliders;
    private bool[] initialColliderStates;

    public bool IsDead { get { return hasDied; } }

    /// <summary>현재 위치와 회전을 기준으로 왕복 이동 경로를 설정한다.</summary>
    private void Awake()
    {
        targetBody = GetComponent<Rigidbody>();
        if (health == null)
        {
            health = GetComponent<EnemyHealth>();
        }

        startPosition = targetBody.position;
        startRotation = targetBody.rotation;
        targetColliders = GetComponentsInChildren<Collider>(true);
        initialColliderStates = new bool[targetColliders.Length];
        for (int i = 0; i < targetColliders.Length; i++)
        {
            initialColliderStates[i] = targetColliders[i].enabled;
        }
        if (respawnController == null)
        {
            respawnController = GetComponentInParent<TargetRespawnController>();
        }
        Vector3 axis = localMoveAxis;
        if (axis.sqrMagnitude < 0.0001f)
        {
            axis = Vector3.forward;
        }

        worldMoveDirection = transform.rotation * axis.normalized;

        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInChildren<Animator>();
        }
        if (targetAnimator != null)
        {
            initialModelPosition = targetAnimator.transform.localPosition;
            ConfigureHitLayer();
        }
        if (deathExplosionPrefab != null)
        {
            explosionPool = new MemoryPool(deathExplosionPrefab);
        }
    }

    private void Start()
    {
        ResetAnimation();
    }

    /// <summary>설정된 두 끝점 사이를 반복 이동한다.</summary>
    private void FixedUpdate()
    {
        if (hasDied || health == null || health.IsDead || isReacting)
        {
            SetMovementAnimation(0f);
            return;
        }

        if (!enableMovement || forwardDistance + backwardDistance <= 0.0001f)
        {
            SetMovementAnimation(0f);
            return;
        }

        if (waitRemaining > 0f)
        {
            waitRemaining -= Time.fixedDeltaTime;
            SetMovementAnimation(0f);
            return;
        }

        float distance = isMovingForward ? forwardDistance : -backwardDistance;
        Vector3 destination = startPosition + worldMoveDirection * distance;
        Vector3 nextPosition = Vector3.MoveTowards(
            targetBody.position,
            destination,
            moveSpeed * Time.fixedDeltaTime
        );
        targetBody.MovePosition(nextPosition);
        SetMovementAnimation(isMovingForward ? 1f : -1f);

        if ((nextPosition - destination).sqrMagnitude < 0.000001f)
        {
            isMovingForward = !isMovingForward;
            waitRemaining = endpointWaitTime;
        }
    }

    /// <summary>피격 대기를 갱신하고 Animator 평가 주기에 맞춰 이동 파라미터를 감쇠한다.</summary>
    private void Update()
    {
        if (hasDied) return;

        UpdateHitReaction();
        if (targetAnimator == null || targetAnimator.updateMode == AnimatorUpdateMode.Fixed) return;

        float deltaTime = Time.deltaTime;
        if (targetAnimator.updateMode == AnimatorUpdateMode.UnscaledTime)
        {
            deltaTime = Time.unscaledDeltaTime;
        }
        ApplyAnimationParameters(deltaTime);
    }

    /// <summary>CombatTarget의 피해 UnityEvent에서 호출한다. 피격 중 추가 체력 피해는 유지한다.</summary>
    public void OnDamaged()
    {
        if (hasDied || health == null || health.IsDead || isReacting) return;

        isReacting = true;
        // 다음 물리 프레임에 예약된 MovePosition이 남지 않도록 현재 위치로 고정한다.
        targetBody.position = targetBody.position;
        // Trigger 전환이 시작될 여유를 포함한다. 진입 후에는 실제 State 길이로 타임아웃을 보강한다.
        hitRemaining = hitReactionDuration + 0.2f;
        hasEnteredHitState = false;
        SetMovementAnimation(0f);
        if (hasValidHitLayer)
        {
            targetAnimator.ResetTrigger(hitTriggerHash);
            targetAnimator.SetTrigger(hitTriggerHash);
        }
    }

    /// <summary>사망 폭발을 실행하고 표적을 비활성화한다.</summary>
    public void OnDied()
    {
        if (hasDied)
        {
            return;
        }

        // 비활성화 시 Kinematic 보간 값이 바뀌어도 사망 위치를 정확히 유지한다.
        deathPosition = targetBody.position;
        hasDied = true;
        isReacting = false;
        if (explosionPool != null)
        {
            GameObject effect = explosionPool.ActivatePoolItem();
            if (effect != null)
            {
                effect.transform.SetPositionAndRotation(
                    transform.TransformPoint(explosionLocalOffset), Quaternion.identity
                );
                Impact impact = effect.GetComponent<Impact>();
                if (impact != null)
                {
                    impact.Setup(explosionPool);
                }
            }
        }

        gameObject.SetActive(false);
        if (respawnController != null)
        {
            respawnController.RequestRespawn(this);
        }
    }

    /// <summary>물리 이동과 독립된 애니메이션 목표값만 보관한다. 자동 방향 추정은 하지 않는다.</summary>
    private void SetMovementAnimation(float direction)
    {
        requestedAnimationDirection = direction;
        if (targetAnimator != null && targetAnimator.updateMode == AnimatorUpdateMode.Fixed)
        {
            ApplyAnimationParameters(Time.fixedDeltaTime);
        }
    }

    /// <summary>물리 이동 방향을 그대로 PosY에 전달한다. 회전 및 자동/수동 반전은 하지 않는다.</summary>
    private void ApplyAnimationParameters(float deltaTime)
    {
        if (targetAnimator == null)
        {
            return;
        }
        targetAnimator.SetFloat("PosY", requestedAnimationDirection, animationDampTime, deltaTime);
    }

    /// <summary>사용자가 구성한 Hit Layer와 Trigger를 확인한다. Layer 인덱스는 이름으로 찾는다.</summary>
    private void ConfigureHitLayer()
    {
        hitLayerIndex = targetAnimator.GetLayerIndex(hitLayerName);
        hitStateHash = Animator.StringToHash(hitState);
        hitIdleStateHash = Animator.StringToHash(hitIdleState);
        hitTriggerHash = Animator.StringToHash(hitTriggerName);
        bool hasTrigger = false;
        foreach (AnimatorControllerParameter parameter in targetAnimator.parameters)
        {
            if (parameter.nameHash == hitTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasTrigger = true;
                break;
            }
        }
        hasValidHitLayer = hitLayerIndex >= 0 && hasTrigger &&
            targetAnimator.HasState(hitLayerIndex, hitStateHash) &&
            targetAnimator.HasState(hitLayerIndex, hitIdleStateHash);
        if (!hasValidHitLayer)
        {
            Debug.LogWarning("Target의 Hit Layer, HitAction, 기본 State 및 onHit Trigger 연결을 확인하세요.", this);
        }
    }

    /// <summary>상체 State의 종료를 기다린다. Base Layer의 재생 시간은 변경하지 않는다.</summary>
    private void UpdateHitReaction()
    {
        if (isReacting)
        {
            hitRemaining -= Time.deltaTime;
            if (hasValidHitLayer)
            {
                AnimatorStateInfo state = targetAnimator.GetCurrentAnimatorStateInfo(hitLayerIndex);
                if (state.fullPathHash == hitStateHash)
                {
                    if (!hasEnteredHitState)
                    {
                        hasEnteredHitState = true;
                        float playbackSpeed = Mathf.Max(0.01f, Mathf.Abs(state.speed * state.speedMultiplier * targetAnimator.speed));
                        hitRemaining = Mathf.Max(hitRemaining, state.length / playbackSpeed + 0.2f);
                    }
                }
                else if (hasEnteredHitState && !targetAnimator.IsInTransition(hitLayerIndex))
                {
                    isReacting = false;
                }
            }
            if (isReacting && hitRemaining <= 0f)
            {
                // 연결이나 전환이 잘못되어도 이동이 영구 정지하지 않도록 상체만 안전하게 해제한다.
                isReacting = false;
                if (hasValidHitLayer)
                {
                    targetAnimator.ResetTrigger(hitTriggerHash);
                    targetAnimator.CrossFadeInFixedTime(hitIdleStateHash, hitLayerBlendTime, hitLayerIndex);
                }
            }
        }
        if (hasValidHitLayer)
        {
            float targetWeight = 0f;
            if (isReacting)
            {
                targetWeight = 1f;
            }
            float weight = Mathf.MoveTowards(targetAnimator.GetLayerWeight(hitLayerIndex), targetWeight,
                Time.deltaTime / Mathf.Max(0.01f, hitLayerBlendTime));
            targetAnimator.SetLayerWeight(hitLayerIndex, weight);
        }
    }

    /// <summary>리스폰 관리자가 비활성 Target을 복구한다. 원본 Clip과 Root Motion 설정은 건드리지 않는다.</summary>
    public void ResetForRespawn(bool resetPosition)
    {
        hasDied = false;
        isReacting = false;
        hitRemaining = 0f;
        hasEnteredHitState = false;
        waitRemaining = 0f;
        isMovingForward = true;
        requestedAnimationDirection = 0f;
        health.ResetHealth();

        Vector3 restoredPosition = deathPosition;
        if (resetPosition)
        {
            restoredPosition = startPosition;
        }
        // Kinematic은 위치를 직접 복원하여 예약 이동을 취소한다. 속도 설정 API는 Dynamic에만 사용한다.
        targetBody.position = restoredPosition;
        targetBody.rotation = startRotation;
        if (!targetBody.isKinematic)
        {
            targetBody.linearVelocity = Vector3.zero;
            targetBody.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(restoredPosition, startRotation);
        for (int i = 0; i < targetColliders.Length; i++)
        {
            if (targetColliders[i] != null)
            {
                targetColliders[i].enabled = initialColliderStates[i];
            }
        }
        if (targetAnimator != null)
        {
            // 사망 전 위치 누적만 초기화한다. 모델 방향은 코드로 보정하지 않는다.
            targetAnimator.transform.localPosition = initialModelPosition;
        }
        gameObject.SetActive(true);
        ResetAnimation();
    }

    /// <summary>보이지 않는 사망 상태를 끊고 중립 파라미터의 Locomotion을 즉시 준비한다.</summary>
    private void ResetAnimation()
    {
        requestedAnimationDirection = 0f;
        if (targetAnimator == null || !targetAnimator.isActiveAndEnabled) return;
        targetAnimator.Rebind();
        hasEnteredHitState = false;
        if (hasValidHitLayer)
        {
            targetAnimator.ResetTrigger(hitTriggerHash);
            targetAnimator.SetLayerWeight(hitLayerIndex, 0f);
            targetAnimator.Play(hitIdleStateHash, hitLayerIndex, 0f);
        }
        targetAnimator.SetFloat("PosY", 0f);
        if (targetAnimator.HasState(0, Animator.StringToHash(locomotionState)))
        {
            targetAnimator.Play(locomotionState, 0, 0f);
            targetAnimator.Update(0f);
        }
    }

    /// <summary>사망 폭발 풀이 사용 중인 경우 오브젝트 파괴 시 정리한다.</summary>
    private void OnDestroy()
    {
        if (explosionPool != null)
        {
            explosionPool.DestroyObjects();

        }
    }
}
