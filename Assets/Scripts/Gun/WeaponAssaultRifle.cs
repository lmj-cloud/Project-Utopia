using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 탄약과 최대 탄약 수를 Inspector 이벤트로 전달한다.
/// </summary>
[System.Serializable]
public class AmmoEvent : UnityEngine.Events.UnityEvent<int, int> { }
/// <summary>
/// 예비 탄약 수를 Inspector 이벤트로 전달한다.
/// </summary>
[System.Serializable]
public class ReserveAmmoEvent : UnityEngine.Events.UnityEvent<int> { }

/// <summary>
/// 소총과 권총의 발사, 조준 기준 Raycast 피해, 탄약, 재장전 및 발사 효과를 처리한다.
/// </summary>
public class WeaponAssaultRifle : MonoBehaviour
{
    /// <summary>
    /// 무기의 기존 동작을 정리한 뒤 소유자와 Animator·카메라를 명시적으로 연결한다.
    /// </summary>
    public bool BindGameplay(PlayerController owner, PlayerAnimatorController animationDriver,
        PlayerCameraController viewController, Camera viewCamera)
    {
        if (owner == null || animationDriver == null || viewController == null || viewCamera == null) return false;
        // 신규 독립 무기를 장착할 때 Scene 검색 없이 소유자와 실제 사격 카메라를 전달한다.
        CancelWeaponActions();
        playerController = owner;
        animator = animationDriver;
        cameraController = viewController;
        mainCamera = viewCamera;
        return true;
    }
    /// <summary>
    /// 총기 종류와 확정된 장탄·예비 탄약 수를 저장하는 스냅샷이다.
    /// </summary>
    [System.Serializable]
    public class AmmoSnapshot
    {
        public WeaponName weaponName;
        public int magazine;
        public int reserve;
    }

    /// <summary>
    /// 총기 종류와 확정된 장탄·예비 탄약 수를 저장용 데이터로 반환한다.
    /// </summary>
    public AmmoSnapshot CaptureAmmo()
    {
        return new AmmoSnapshot { weaponName = WeaponName, magazine = CurrentAmmo, reserve = ReserveAmmo };
    }

    /// <summary>
    /// 저장된 탄약 수의 유효성을 검사하고 진행 중 동작을 취소한 뒤 확정 수량만 복원한다.
    /// </summary>
    public bool RestoreAmmo(AmmoSnapshot snapshot)
    {
        if (snapshot == null || snapshot.weaponName != WeaponName || snapshot.magazine < 0 ||
            snapshot.magazine > MagazineCapacity || snapshot.reserve < 0)
        {
            return false;
        }
        // 발사와 재장전 Coroutine은 재개하지 않고 확정된 탄약 수만 복원한다.
        CancelWeaponActions();
        ammoInitialized = true;
        weaponSetting.currentAmmo = snapshot.magazine;
        weaponSetting.reserveAmmo = snapshot.reserve;
        NotifyAmmoChanged();
        return true;
    }

    public event System.Action<Vector3> OnFired;
    public event System.Action<UnityEngine.Object> OnDamageApplied;
    public event System.Action<WeaponAssaultRifle> OnReloadCompleted;
    public float AttackInterval => weaponSetting.attackRate;
    public bool IsReloading => isReload;

    [HideInInspector]
    public AmmoEvent onAmmoEvent = new AmmoEvent();
    [HideInInspector]
    public ReserveAmmoEvent onReserveAmmoEvent = new ReserveAmmoEvent();

    [Header("Camera")]
    [SerializeField] private PlayerCameraController cameraController;

    [Header("First Person Fire Effects / Spawn Points")]
    [SerializeField] private GameObject firstPersonMuzzleFlashEffect;
    [SerializeField] private Transform firstPersonCasingSpawnPoint;
    [SerializeField] private Transform firstPersonBulletSpawnPoint;

    [Header("Third Person Fire Effects / Spawn Points")]
    [SerializeField] private GameObject muzzleFlashEffect;
    // 탄피를 꺼내 배출할 위치와 방향의 기준점.
    [SerializeField] private Transform casingSpawnPoint;
    // 실제 3인칭 총알 경로를 시작하는 총구 Transform.
    [SerializeField] private Transform bulletSpawnPoint;


    [Header("Audio Clips")]
    [SerializeField] private AudioClip audioClipTakeOutWeapon;
    [SerializeField] private AudioClip audioClipFire;
    [SerializeField] private AudioClip audioClipReload;

    // 실제 탄약 이전을 확정하기 전 재장전을 기다리는 시간.
    [Header("Reload")]
    [SerializeField, Min(0.1f)] private float reloadDuration = 2f;

    // 총기 종류, 탄약 수, 사거리와 피해 등 기존 발사 설정.
    [Header("Weapon Setting")]
    [SerializeField] private WeaponSetting weaponSetting;

    // 총알 Raycast에 포함할 Layer 설정.
    [Header("Shot Raycast Filters")]
    [SerializeField] private LayerMask shotLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private QueryTriggerInteraction shotTriggerInteraction = QueryTriggerInteraction.Collide;
    // 총알 경로 검사에서 자기 몸 등 제외할 Collider 목록.
    [SerializeField] private Collider[] ignoredShotColliders = new Collider[0];

    private float lastAttackTime = float.NegativeInfinity;
    private bool isReload = false;
    private int reloadRequestId;
    private bool ammoInitialized;
    private Coroutine attackRoutine;
    private Coroutine reloadRoutine;
    private bool isEquipped = true;
    private PlayerController playerController;
    private bool CanUse => isEquipped && isActiveAndEnabled &&
        (playerController == null || playerController.CanUseWeapons);

    /// <summary>
    /// 무기의 장착 상태를 바꾸고 남은 발사·재장전과 표시를 정리한다.
    /// </summary>
    public void SetEquipped(bool value)
    {
        if (isEquipped == value) return;
        CancelWeaponActions();
        isEquipped = value;
        if (value && isActiveAndEnabled)
        {
            PlaySound(audioClipTakeOutWeapon);
            NotifyAmmoChanged();
        }
    }

    /// <summary>
    /// 발사·재장전·발광 Coroutine과 소리를 중단하여 이전 동작이 남지 않게 한다.
    /// </summary>
    public void CancelWeaponActions()
    {
        StopWeaponAction();
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        bool wasReloading = isReload;
        isReload = false;
        if (wasReloading)
        {
            if (animator != null) animator.OnReloadCancelled();
        }
        if (muzzleFlashRoutine != null) StopCoroutine(muzzleFlashRoutine);
        muzzleFlashRoutine = null;
        HideMuzzleFlashes();
        if (audioSource != null) audioSource.Stop();
    }

    private Coroutine muzzleFlashRoutine;

    private AudioSource audioSource;
    private PlayerAnimatorController animator;
    private CasingMemoryPool casingMemoryPool;
    private ImpactMemoryPool impactMemoryPool;
    private Camera mainCamera;

    public WeaponName WeaponName => weaponSetting.weaponName;
    public int MagazineCapacity => weaponSetting.maxAmmo;
    public int CurrentAmmo => weaponSetting.currentAmmo;
    public int ReserveAmmo => weaponSetting.reserveAmmo;
    public int TotalAmmo => weaponSetting.currentAmmo + weaponSetting.reserveAmmo;
    public int InitialTotalAmmo => weaponSetting.initialTotalAmmo;

    // Gameplay always uses the actual third-person muzzle. First-person references remain for story effects.
    private Transform ActiveBulletSpawnPoint => bulletSpawnPoint;
    private Transform ActiveCasingSpawnPoint => casingSpawnPoint;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        playerController = GetComponentInParent<PlayerController>();
        animator = GetComponentInParent<PlayerAnimatorController>();
        casingMemoryPool = GetComponent<CasingMemoryPool>();
        impactMemoryPool = GetComponent<ImpactMemoryPool>();
        mainCamera = Camera.main;

        InitializeAmmo();
    }

    /// <summary>
    /// 무기 동작을 중단한 뒤 초기 설정에 따라 탄약을 다시 채운다.
    /// </summary>
    public void ResetAmmoForDemo()
    {
        CancelWeaponActions();
        ammoInitialized = false;
        InitializeAmmo();
    }

    /// <summary>
    /// 중복 초기화를 막고 설정된 총 탄약을 탄창과 예비 탄약으로 나눈다.
    /// </summary>
    private void InitializeAmmo()
    {
        if (ammoInitialized) return;
        ammoInitialized = true;
        weaponSetting.maxAmmo = Mathf.Max(0, weaponSetting.maxAmmo);
        weaponSetting.initialTotalAmmo = Mathf.Max(0, weaponSetting.initialTotalAmmo);
        weaponSetting.currentAmmo = Mathf.Min(weaponSetting.maxAmmo, weaponSetting.initialTotalAmmo);
        weaponSetting.reserveAmmo = weaponSetting.initialTotalAmmo - weaponSetting.currentAmmo;
    }

    /// <summary>
    /// 현재 장탄 수와 예비 탄약 수를 UI 등 구독자에게 전달한다.
    /// </summary>
    private void NotifyAmmoChanged()
    {
        if (!ammoInitialized) return;
        onAmmoEvent.Invoke(weaponSetting.currentAmmo, weaponSetting.maxAmmo);
        onReserveAmmoEvent.Invoke(weaponSetting.reserveAmmo);
    }

    /// <summary>
    /// 컴포넌트 활성화 시 필요한 알림과 초기 표시를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        PlaySound(audioClipTakeOutWeapon);
        HideMuzzleFlashes();
        NotifyAmmoChanged();
    }

    /// <summary>
    /// 총기 종류와 입력 형태에 따라 단발 또는 연속 발사를 시작한다.
    /// </summary>
    public void StartWeaponAction(int type = 0)
    {
        if (type != 0 || isReload || !CanUse)
            return;

        if (weaponSetting.weaponName != WeaponName.Pistol && weaponSetting.isAutomaticAttack)
        {
            if (attackRoutine == null)
                attackRoutine = StartCoroutine(OnAttackLoop());
        }
        else
        {
            OnAttack();
        }
    }

    /// <summary>
    /// 지정된 발사 동작을 중단하고 남은 발사 Coroutine을 해제한다.
    /// </summary>
    public void StopWeaponAction(int type = 0)
    {
        if (type != 0 || attackRoutine == null)
            return;

        StopCoroutine(attackRoutine);
        attackRoutine = null;
    }

    /// <summary>
    /// 장착·탄약·동작 조건을 검사하여 재장전 Coroutine을 시작한다.
    /// </summary>
    public void StartReload()
    {
        if (isReload || !CanUse || weaponSetting.reserveAmmo <= 0 || weaponSetting.currentAmmo >= weaponSetting.maxAmmo)
            return;

        StopWeaponAction();
        reloadRoutine = StartCoroutine(OnReload());
    }

    /// <summary>
    /// 연속 발사 간격 동안 반복해서 실제 발사 판정을 요청한다.
    /// </summary>
    private IEnumerator OnAttackLoop()
    {
        while (CanUse)
        {
            OnAttack();
            yield return null;
        }
        attackRoutine = null;
    }

    /// <summary>
    /// 발사 조건과 탄약을 검사하고 목표·피해를 계산한 뒤 애니메이션과 효과를 요청한다.
    /// </summary>
    public void OnAttack()
    {
        if (isReload || !CanUse || mainCamera == null || ActiveBulletSpawnPoint == null || weaponSetting.currentAmmo <= 0 || Time.time - lastAttackTime <= weaponSetting.attackRate)
            return;

        lastAttackTime = Time.time;
        weaponSetting.currentAmmo--;
        NotifyAmmoChanged();

        Vector3 targetPoint = GetTargetPoint();
        OnFired?.Invoke(targetPoint);
        // Resolve damage before optional animation/audio/casing effects can fail.
        TwoStepRaycast(targetPoint);

        if (animator != null)
            animator.OnFire();

        if (muzzleFlashRoutine != null)
            StopCoroutine(muzzleFlashRoutine);
        muzzleFlashRoutine = StartCoroutine(OnMuzzleFlashEffect());
        PlaySound(audioClipFire);

        Transform spawnPoint = ActiveCasingSpawnPoint;
        if (casingMemoryPool != null && spawnPoint != null)
            casingMemoryPool.SpawnCasing(spawnPoint.position, spawnPoint.right);

    }

    /// <summary>
    /// 짧은 시간 동안 발광 효과를 켠 뒤 끄고 발광 Coroutine 상태를 해제한다.
    /// </summary>
    private IEnumerator OnMuzzleFlashEffect()
    {
        float endTime = Time.time + Mathf.Max(0.01f, weaponSetting.attackRate * 0.3f);
        while (Time.time < endTime)
        {
            // 짧은 발사 효과 도중 시점을 바꾸더라도 현재 시점의 효과만 표시한다.
            bool firstPerson = false;
            if (muzzleFlashEffect != null)
                muzzleFlashEffect.SetActive(!firstPerson);
            if (firstPersonMuzzleFlashEffect != null)
                firstPersonMuzzleFlashEffect.SetActive(firstPerson);
            yield return null;
        }

        HideMuzzleFlashes();
        muzzleFlashRoutine = null;
    }

    /// <summary>
    /// 연결된 1·3인칭 발광 효과를 숨긴다.
    /// </summary>
    private void HideMuzzleFlashes()
    {
        if (muzzleFlashEffect != null)
            muzzleFlashEffect.SetActive(false);
        if (firstPersonMuzzleFlashEffect != null)
            firstPersonMuzzleFlashEffect.SetActive(false);
    }

    /// <summary>
    /// 재장전 대기 후 요청이 유효하면 탄약을 이동하고 완료를 알린다.
    /// </summary>
    private IEnumerator OnReload()
    {
        isReload = true;
        int requestId = ++reloadRequestId;
        if (!isReload || requestId != reloadRequestId) yield break;
        if (!CanUse) { CancelWeaponActions(); yield break; }

        if (animator != null) animator.OnReload();
        PlaySound(audioClipReload);

        yield return new WaitForSeconds(reloadDuration);

        if (!isReload || requestId != reloadRequestId) yield break;
        if (!CanUse)
        {
            isReload = false;
            reloadRoutine = null;
            if (animator != null) animator.OnReloadCancelled();
            yield break;
        }
        int amount = Mathf.Min(Mathf.Max(0, weaponSetting.maxAmmo - weaponSetting.currentAmmo),
            Mathf.Max(0, weaponSetting.reserveAmmo));
        weaponSetting.currentAmmo += amount;
        weaponSetting.reserveAmmo -= amount;
        isReload = false;
        reloadRoutine = null;
        if (animator != null)
            animator.OnReloadFinished();

        NotifyAmmoChanged();
        OnReloadCompleted?.Invoke(this);
    }

    /// <summary>
    /// 실제 플레이 카메라의 조준선에서 총알이 향할 목표점을 계산한다.
    /// </summary>
    private Vector3 GetTargetPoint()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(ray.origin, ray.direction * weaponSetting.attackDistance, Color.red);

        if (TryGetShotHit(ray.origin, ray.direction, out RaycastHit hit))
            return hit.point;

        return ray.origin + ray.direction * weaponSetting.attackDistance;
    }

    /// <summary>
    /// 카메라 조준 목표와 총구의 실제 발사 경로를 나누어 가까운 장애물과 피격을 처리한다.
    /// </summary>
    private void TwoStepRaycast(Vector3 targetPoint)
    {
        // OnFired에서 몸이 회전한 뒤 현재 시점의 총구 위치를 사용한다.
        Transform spawnPoint = ActiveBulletSpawnPoint;
        if (spawnPoint == null)
            return;

        Vector3 delta = targetPoint - spawnPoint.position;
        if (delta.sqrMagnitude < 0.0001f)
            return;

        Vector3 attackDirection = delta.normalized;
        if (TryGetShotHit(spawnPoint.position, attackDirection, out RaycastHit hit))
        {
            ApplyHitDamage(hit.collider);
            if (impactMemoryPool != null)
                impactMemoryPool.SpawnImpact(hit);
        }

        Debug.DrawRay(spawnPoint.position,
            attackDirection * weaponSetting.attackDistance, Color.blue);
    }

    /// <summary>
    /// 발사 경로에서 제외 Collider를 걸러 가장 가까운 유효 충돌을 찾는다.
    /// </summary>
    private bool TryGetShotHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        if (ignoredShotColliders == null || ignoredShotColliders.Length == 0)
            return Physics.Raycast(origin, direction, out hit, weaponSetting.attackDistance, shotLayers, shotTriggerInteraction);

        // RaycastAll is unordered: choose the nearest non-ignored hit, including walls.
        hit = default;
        float nearestDistance = float.PositiveInfinity;
        bool found = false;
        foreach (RaycastHit candidate in Physics.RaycastAll(origin, direction, weaponSetting.attackDistance, shotLayers, shotTriggerInteraction))
        {
            bool ignored = false;
            foreach (Collider excluded in ignoredShotColliders)
                if (excluded != null && excluded == candidate.collider) { ignored = true; break; }
            if (ignored || candidate.distance >= nearestDistance) continue;
            nearestDistance = candidate.distance;
            hit = candidate;
            found = true;
        }
        return found;
    }

    /// <summary>
    /// 충돌한 Hitbox, 적 체력 또는 과녁에 알맞은 피해 호출을 전달한다.
    /// </summary>
    private void ApplyHitDamage(Collider hitCollider)
    {
        if (hitCollider == null) return;
        EnemyHitbox hitbox = hitCollider.GetComponent<EnemyHitbox>();
        if (hitbox != null)
        {
            // A missing EnemyHealth must not fall back to another damage recipient.
            EnemyHealth owner = hitbox.Health;
            bool applied = hitbox.TryTakeDamage(weaponSetting.attackDamage);
            if (applied && owner != null) OnDamageApplied?.Invoke(owner);
            return;
        }
        CombatTarget target = hitCollider.GetComponentInParent<CombatTarget>();
        if (target != null)
        {
            if (target.TryTakeDamage(weaponSetting.attackDamage)) OnDamageApplied?.Invoke(target);
        }
    }

    /// <summary>
    /// 연결된 오디오 소스와 Clip이 있으면 해당 무기 소리를 재생한다.
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        CancelWeaponActions();
        StopAllCoroutines();
        attackRoutine = null;
        muzzleFlashRoutine = null;
        isReload = false;
        HideMuzzleFlashes();
        if (audioSource != null)
            audioSource.Stop();
    }
}
