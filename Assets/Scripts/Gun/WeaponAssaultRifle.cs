using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AmmoEvent : UnityEngine.Events.UnityEvent<int, int> { }
[System.Serializable]
public class MagazineEvent : UnityEngine.Events.UnityEvent<int> { }

public class WeaponAssaultRifle : MonoBehaviour
{
    public event System.Action<Vector3> OnFired;
    public float AttackInterval => weaponSetting.attackRate;
    public bool IsReloading => isReload;

    [HideInInspector]
    public AmmoEvent onAmmoEvent = new AmmoEvent();
    [HideInInspector]
    public MagazineEvent onMagazineEvent = new MagazineEvent();

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

    private float lastAttackTime = 0;
    private bool isReload = false;
    private Coroutine attackRoutine;
    private Coroutine muzzleFlashRoutine;

    private AudioSource audioSource;
    private PlayerAnimatorController animator;
    private CasingMemoryPool casingMemoryPool;
    private ImpactMemoryPool impactMemoryPool;
    private Camera mainCamera;

    public WeaponName WeaponName => weaponSetting.weaponName;
    public int CurrentMagazine => weaponSetting.currentMagazine;
    public int MaxMagazine => weaponSetting.maxMagazine;

    private bool UseFirstPerson => cameraController != null && !cameraController.IsThirdPerson;

    private Transform ActiveBulletSpawnPoint => UseFirstPerson && firstPersonBulletSpawnPoint != null ? firstPersonBulletSpawnPoint : bulletSpawnPoint;

    private Transform ActiveCasingSpawnPoint => UseFirstPerson && firstPersonCasingSpawnPoint != null ? firstPersonCasingSpawnPoint : casingSpawnPoint;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        animator = GetComponentInParent<PlayerAnimatorController>();
        casingMemoryPool = GetComponent<CasingMemoryPool>();
        impactMemoryPool = GetComponent<ImpactMemoryPool>();
        mainCamera = Camera.main;

        weaponSetting.currentMagazine = weaponSetting.maxMagazine;
        weaponSetting.currentAmmo = weaponSetting.maxAmmo;
    }

    private void OnEnable()
    {
        PlaySound(audioClipTakeOutWeapon);
        HideMuzzleFlashes();
        onMagazineEvent.Invoke(weaponSetting.currentMagazine);
        onAmmoEvent.Invoke(weaponSetting.currentAmmo, weaponSetting.maxAmmo);
    }

    public void StartWeaponAction(int type = 0)
    {
        if (type != 0 || isReload || !isActiveAndEnabled)
            return;

        if (weaponSetting.isAutomaticAttack)
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
        if (isReload || !isActiveAndEnabled || weaponSetting.currentMagazine <= 0 || weaponSetting.currentAmmo >= weaponSetting.maxAmmo)
            return;

        StopWeaponAction();
        StartCoroutine(OnReload());
    }

    private IEnumerator OnAttackLoop()
    {
        while (true)
        {
            OnAttack();
            yield return null;
        }
    }

    public void OnAttack()
    {
        if (isReload || !isActiveAndEnabled || mainCamera == null || ActiveBulletSpawnPoint == null || weaponSetting.currentAmmo <= 0 || Time.time - lastAttackTime <= weaponSetting.attackRate)
            return;

        lastAttackTime = Time.time;
        weaponSetting.currentAmmo--;
        onAmmoEvent.Invoke(weaponSetting.currentAmmo, weaponSetting.maxAmmo);

        Vector3 targetPoint = GetTargetPoint();
        OnFired?.Invoke(targetPoint);

        if (animator != null)
            animator.OnFire();

        if (muzzleFlashRoutine != null)
            StopCoroutine(muzzleFlashRoutine);
        muzzleFlashRoutine = StartCoroutine(OnMuzzleFlashEffect());
        PlaySound(audioClipFire);

        Transform spawnPoint = ActiveCasingSpawnPoint;
        if (casingMemoryPool != null && spawnPoint != null)
            casingMemoryPool.SpawnCasing(spawnPoint.position, spawnPoint.right);

        TwoStepRaycast(targetPoint);
    }

    private IEnumerator OnMuzzleFlashEffect()
    {
        float endTime = Time.time + Mathf.Max(0.01f, weaponSetting.attackRate * 0.3f);
        while (Time.time < endTime)
        {
            // 짧은 발사 효과 도중 시점을 바꾸더라도 현재 시점의 효과만 표시한다.
            bool firstPerson = UseFirstPerson && firstPersonMuzzleFlashEffect != null;
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

        animator.OnReload();
        PlaySound(audioClipReload);

        yield return new WaitForSeconds(reloadDuration);

        weaponSetting.currentMagazine--;
        weaponSetting.currentAmmo = weaponSetting.maxAmmo;
        isReload = false;
        if (animator != null)
            animator.OnReloadFinished();

        onMagazineEvent.Invoke(weaponSetting.currentMagazine);
        onAmmoEvent.Invoke(weaponSetting.currentAmmo, weaponSetting.maxAmmo);
    }

    private Vector3 GetTargetPoint()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(ray.origin, ray.direction * weaponSetting.attackDistance, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, weaponSetting.attackDistance))
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
        if (Physics.Raycast(spawnPoint.position, attackDirection, out RaycastHit hit, weaponSetting.attackDistance))
        {
            if (impactMemoryPool != null)
                impactMemoryPool.SpawnImpact(hit);
        }

        Debug.DrawRay(spawnPoint.position,
            attackDirection * weaponSetting.attackDistance, Color.blue);
    }

    private void PlaySound(AudioClip clip)
    {
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        attackRoutine = null;
        muzzleFlashRoutine = null;
        isReload = false;
        HideMuzzleFlashes();
        if (animator != null)
            animator.OnReloadFinished();
        if (audioSource != null)
            audioSource.Stop();
    }
}
