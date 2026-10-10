using Unity.VisualScripting;
using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] private Animator firstPersonAnimator;
    [SerializeField] private Animator thirdPersonAnimator;

    [Header("Weapon Animation Layers")]
    [SerializeField, Min(0)] private int firstPersonWeaponLayer = 0;
    [SerializeField, Min(0)] private int thirdPersonWeaponLayer = 1;

    private float currentSpeed;
    private float moveHorizontal;
    private float moveVertical;

    void Awake()
    {
        PrepareAnimator(firstPersonAnimator);
        PrepareAnimator(thirdPersonAnimator);
    }

    private void PrepareAnimator(Animator target)
    {
        if (target == null)
            return;

        // 실제 이동은 PlayerMovement에서만 처리한다.
        target.applyRootMotion = false;
        // Renderer를 숨겨도 재장전 등 애니메이션 시간은 계속 진행한다.
        target.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    public float CurrentSpeed
    {
        get => currentSpeed;
        set
        {
            currentSpeed = value;
            SetFloat("Speed", value);
        }
    }

    public float MoveHorizontal
    {
        get => moveHorizontal;
        set
        {
            moveHorizontal = value;
            SetFloat("MoveX", value);
        }
    }

    public float MoveVertical
    {
        get => moveVertical;
        set
        {
            moveVertical = value;
            SetFloat("MoveY", value);
        }
    }

    private void SetFloat(string parameter, float value)
    {
        if (firstPersonAnimator != null)
            firstPersonAnimator.SetFloat(parameter, value);
        if (thirdPersonAnimator != null)
            thirdPersonAnimator.SetFloat(parameter, value);
    }

    private void SetBool(string parameter, bool value)
    {
        if (firstPersonAnimator != null)
            firstPersonAnimator.SetBool(parameter, value);
        if (thirdPersonAnimator != null)
            thirdPersonAnimator.SetBool(parameter, value);
    }

    public void OnAiming(bool isAiming)
    {
        SetBool("IsAim", isAiming);
    }

    public void OnFire()
    {
        Play("Fire", -1, 0f);
    }

    public void OnReload()
    {
        SetBool("IsReload", true);
        TriggerReload(firstPersonAnimator);
        TriggerReload(thirdPersonAnimator);
    }

    private void TriggerReload(Animator target)
    {
        if (target == null)
            return;

        target.ResetTrigger("onReload");
        target.SetTrigger("onReload");
    }

    public void OnReloadFinished()
    {
        SetBool("IsReload", false);
        if (firstPersonAnimator != null)
            firstPersonAnimator.ResetTrigger("onReload");
        if (thirdPersonAnimator != null)
            thirdPersonAnimator.ResetTrigger("onReload");
    }

    public void Play(string stateName, int layer, float normalizedTime)
    {
        PlayState(firstPersonAnimator, stateName,
            layer < 0 ? firstPersonWeaponLayer : layer, normalizedTime);
        PlayState(thirdPersonAnimator, stateName,
            layer < 0 ? thirdPersonWeaponLayer : layer, normalizedTime);
    }

    private void PlayState(Animator target, string stateName, int layer, float normalizedTime)
    {
        if (target == null)
            return;

        if (layer >= target.layerCount)
        {
            Debug.LogError($"{target.name}: 총기 애니메이션 레이어 {layer}가 없습니다.", target);
            return;
        }

        // 예: Base Layer.Fire / UpperBody.Fire
        string statePath = stateName.Contains(".")
            ? stateName : target.GetLayerName(layer) + "." + stateName;
        target.Play(statePath, layer, normalizedTime);
    }

    public bool CurrentAnimationIs(string name)
    {
        // 기존 호출과 호환: 전신 Animator의 Base Layer를 우선 확인한다.
        Animator target = thirdPersonAnimator != null
            ? thirdPersonAnimator : firstPersonAnimator;
        return target != null && target.GetCurrentAnimatorStateInfo(0).IsName(name);
    }
}
