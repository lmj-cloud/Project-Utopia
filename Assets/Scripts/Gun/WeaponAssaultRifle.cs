using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AmmoEvent : UnityEngine.Events.UnityEvent<int, int> { }
[System.Serializable]
public class ReserveAmmoEvent : UnityEngine.Events.UnityEvent<int> { }

public class WeaponAssaultRifle : MonoBehaviour
{
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
    [SerializeField] private Transform casingSpawnPoint;
    [SerializeField] private Transform bulletSpawnPoint;


    [Header("Audio Clips")]
    [SerializeField] private AudioClip audioClipTakeOutWeapon;
    [SerializeField] private AudioClip audioClipFire;
    [SerializeField] private AudioClip audioClipReload;

    [Header("Reload")]
    [SerializeField, Min(0.1f)] private float reloadDuration = 2f;

    [Header("Weapon Setting")]
    [SerializeField] private WeaponSetting weaponSetting;

    [Header("Shot Raycast Filters")]
    [SerializeField] private LayerMask shotLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private QueryTriggerInteraction shotTriggerInteraction = QueryTriggerInteraction.Collide;
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

    public void ResetAmmoForDemo()
    {
        CancelWeaponActions();
        ammoInitialized = false;
        InitializeAmmo();
    }

    private void InitializeAmmo()
    {
        if (ammoInitialized) return;
        ammoInitialized = true;
        weaponSetting.maxAmmo = Mathf.Max(0, weaponSetting.maxAmmo);
        weaponSetting.initialTotalAmmo = Mathf.Max(0, weaponSetting.initialTotalAmmo);
        weaponSetting.currentAmmo = Mathf.Min(weaponSetting.maxAmmo, weaponSetting.initialTotalAmmo);
        weaponSetting.reserveAmmo = weaponSetting.initialTotalAmmo - weaponSetting.currentAmmo;
    }

    private void NotifyAmmoChanged()
    {
        if (!ammoInitialized) return;
        onAmmoEvent.Invoke(weaponSetting.currentAmmo, weaponSetting.maxAmmo);
        onReserveAmmoEvent.Invoke(weaponSetting.reserveAmmo);
    }

    private void OnEnable()
    {
        PlaySound(audioClipTakeOutWeapon);
        HideMuzzleFlashes();
        NotifyAmmoChanged();
    }

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

    public void StopWeaponAction(int type = 0)
    {
        if (type != 0 || attackRoutine == null)
            return;

        StopCoroutine(attackRoutine);
        attackRoutine = null;
    }

    public void StartReload()
    {
        if (isReload || !CanUse || weaponSetting.reserveAmmo <= 0 || weaponSetting.currentAmmo >= weaponSetting.maxAmmo)
            return;

        StopWeaponAction();
        reloadRoutine = StartCoroutine(OnReload());
    }

    private IEnumerator OnAttackLoop()
    {
        while (CanUse)
        {
            OnAttack();
            yield return null;
        }
        attackRoutine = null;
    }

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

    private void HideMuzzleFlashes()
    {
        if (muzzleFlashEffect != null)
            muzzleFlashEffect.SetActive(false);
        if (firstPersonMuzzleFlashEffect != null)
            firstPersonMuzzleFlashEffect.SetActive(false);
    }

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

    private Vector3 GetTargetPoint()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(ray.origin, ray.direction * weaponSetting.attackDistance, Color.red);

        if (TryGetShotHit(ray.origin, ray.direction, out RaycastHit hit))
            return hit.point;

        return ray.origin + ray.direction * weaponSetting.attackDistance;
    }

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

    private void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }

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
