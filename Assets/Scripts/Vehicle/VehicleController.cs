using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WheelCollider 주행과 기존 플레이어의 제어권·표시·카메라를 넘겨받는 탑승 및 하차를 처리한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [System.Serializable]
    private class VehicleWheel
    {
        public WheelCollider collider;
        public Transform visual;
        public bool canSteer;
        public bool isDriven;
    }

    [Header("Physics")]
    [SerializeField] private VehicleWheel[] wheels;
    [SerializeField, Min(0f)] private float motorTorque = 1000f;
    [SerializeField, Min(0f)] private float brakeTorque = 2500f;
    [SerializeField, Min(0f)] private float rollingBrakeTorque = 25f;
    [SerializeField, Min(1f)] private float maxSpeed = 25f;
    [SerializeField, Min(0.1f)] private float maxReverseSpeed = 8f;
    [SerializeField, Range(1f, 60f)] private float maxSteeringAngle = 32f;
    [SerializeField, Min(1f)] private float steeringResponse = 90f;

    [Header("Seat and Exit")]
    [SerializeField] private Transform driverSeat;
    [SerializeField] private Transform[] exitPoints;
    [SerializeField, Min(0f)] private float maxExitSpeed = 1.5f;
    [SerializeField] private LayerMask exitObstructionMask = ~0;
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Cameras")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Camera vehicleCamera;
    [SerializeField] private AudioListener gameplayListener;
    [SerializeField] private AudioListener vehicleListener;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
    [SerializeField, Range(0f, 120f)] private float maxLookYaw = 75f;
    [SerializeField, Range(0f, 80f)] private float maxLookPitch = 35f;

    private Rigidbody body;
    private PlayerController driver;
    private PlayerMovement driverMovement;
    private PlayerCameraController driverCamera;
    private CharacterController driverCollider;
    private Renderer[] driverRenderers;
    private bool[] rendererWasHidden;
    private Quaternion[] wheelRotationOffsets;
    private Vector3 entryPosition;
    private Quaternion entryRotation;
    private Quaternion entryViewRotation;
    private bool colliderWasEnabled;
    private bool gameplayCameraWasEnabled;
    private bool gameplayListenerWasEnabled;
    private float throttle;
    private float steeringInput;
    private bool isBraking;
    private float steeringAngle;
    private float lookYaw;
    private float lookPitch;
    private int enteredFrame;

    public bool IsOccupied => driver != null;
    public PlayerController Driver => driver;
    public float CurrentSpeed => body == null ? 0f : body.linearVelocity.magnitude;
    public bool CanEnter => isActiveAndEnabled && !IsOccupied && driverSeat != null &&
        vehicleCamera != null && gameplayCamera != null && body != null && CurrentSpeed <= maxExitSpeed;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (vehicleCamera != null) vehicleCamera.enabled = false;
        if (vehicleListener != null) vehicleListener.enabled = false;
        if (wheels == null) wheels = new VehicleWheel[0];
        wheelRotationOffsets = new Quaternion[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            VehicleWheel wheel = wheels[i];
            if (wheel.collider == null || wheel.visual == null) continue;
            wheelRotationOffsets[i] = Quaternion.Inverse(wheel.collider.transform.rotation) * wheel.visual.rotation;
        }
    }

    /// <summary>
    /// 상호작용 선정이 끝난 기존 플레이어만 예약하고, 자기 소유 잠금으로 이동과 전투를 정지한다.
    /// </summary>
    public bool TryEnter(PlayerController player)
    {
        if (!CanEnter || player == null || !player.CanControlPlayer ||
            (dialogueUI != null && dialogueUI.IsOpen)) return false;
        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        PlayerCameraController camera = player.GetComponent<PlayerCameraController>();
        CharacterController collider = player.GetComponent<CharacterController>();
        if (movement == null || camera == null || collider == null ||
            camera.IsStoryFirstPerson || camera.HasTargetControl || !gameplayCamera.enabled) return false;

        driver = player;
        driverMovement = movement;
        driverCamera = camera;
        driverCollider = collider;
        entryPosition = player.transform.position;
        entryRotation = player.transform.rotation;
        entryViewRotation = gameplayCamera.transform.rotation;
        colliderWasEnabled = collider.enabled;
        gameplayCameraWasEnabled = gameplayCamera.enabled;
        gameplayListenerWasEnabled = gameplayListener != null && gameplayListener.enabled;
        driverRenderers = player.GetComponentsInChildren<Renderer>(true);
        rendererWasHidden = new bool[driverRenderers.Length];
        for (int i = 0; i < driverRenderers.Length; i++)
        {
            rendererWasHidden[i] = driverRenderers[i].forceRenderingOff;
            // 장비 표시 코드가 enabled를 갱신하더라도 탑승 중 외형은 보이지 않게 한다.
            driverRenderers[i].forceRenderingOff = true;
        }
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        camera.AcquireTargetControl(this);
        collider.enabled = false;
        gameplayCamera.enabled = false;
        if (gameplayListener != null) gameplayListener.enabled = false;
        vehicleCamera.enabled = true;
        if (vehicleListener != null) vehicleListener.enabled = true;
        lookYaw = 0f;
        lookPitch = 0f;
        enteredFrame = Time.frameCount;
        return true;
    }

    private void Update()
    {
        throttle = 0f;
        steeringInput = 0f;
        isBraking = false;
        if (!IsOccupied) return;
        bool canDrive = driver.isActiveAndEnabled && driver.IsAlive && !driver.HasOtherControlLock(this) &&
            !driverCamera.IsStoryFirstPerson && (dialogueUI == null || !dialogueUI.IsOpen);
        if (!canDrive)
        {
            isBraking = true;
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) throttle += 1f;
            if (keyboard.sKey.isPressed) throttle -= 1f;
            if (keyboard.aKey.isPressed) steeringInput -= 1f;
            if (keyboard.dKey.isPressed) steeringInput += 1f;
            isBraking = keyboard.spaceKey.isPressed;
            Key key = driver.InteractionKey;
            if (Time.frameCount > enteredFrame && key != Key.None && keyboard[key].wasPressedThisFrame)
            {
                if (TryExit()) return;
            }
        }
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            lookYaw = Mathf.Clamp(lookYaw + delta.x * mouseSensitivity, -maxLookYaw, maxLookYaw);
            lookPitch = Mathf.Clamp(lookPitch - delta.y * mouseSensitivity, -maxLookPitch, maxLookPitch);
        }
    }

    /// <summary>
    /// 입력과 물리를 분리한다. 반대 방향 키는 먼저 제동하고 정지 후 반대 토크를 적용한다.
    /// </summary>
    private void FixedUpdate()
    {
        float signedSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        float steeringLimit = Mathf.Lerp(maxSteeringAngle, maxSteeringAngle * 0.35f, Mathf.Clamp01(CurrentSpeed / maxSpeed));
        steeringAngle = Mathf.MoveTowards(steeringAngle, steeringInput * steeringLimit, steeringResponse * Time.fixedDeltaTime);
        bool hasOppositeInput = (throttle > 0f && signedSpeed < -0.4f) || (throttle < 0f && signedSpeed > 0.4f);
        float motor = 0f;
        // 가속할 때 잔여 제동 토크를 함께 주면 정지한 휠이 계속 잠길 수 있다.
        float brake = Mathf.Abs(throttle) < 0.01f ? rollingBrakeTorque : 0f;
        if (!IsOccupied || isBraking || hasOppositeInput) brake = brakeTorque;
        else if (throttle > 0f && signedSpeed < maxSpeed) motor = throttle * motorTorque;
        else if (throttle < 0f && signedSpeed > -maxReverseSpeed) motor = throttle * motorTorque;
        foreach (VehicleWheel wheel in wheels)
        {
            if (wheel.collider == null) continue;
            wheel.collider.steerAngle = wheel.canSteer ? steeringAngle : 0f;
            wheel.collider.motorTorque = wheel.isDriven ? motor : 0f;
            wheel.collider.brakeTorque = brake;
        }
    }

    private void LateUpdate()
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            VehicleWheel wheel = wheels[i];
            if (wheel.collider == null || wheel.visual == null) continue;
            Vector3 position;
            Quaternion rotation;
            wheel.collider.GetWorldPose(out position, out rotation);
            wheel.visual.SetPositionAndRotation(position, rotation * wheelRotationOffsets[i]);
        }
        if (!IsOccupied) return;
        driver.transform.position = driverSeat.position;
        vehicleCamera.transform.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
    }

    /// <summary>
    /// 저속 상태에서 지면과 플레이어 캡슐 전체의 여유 공간을 확인한 경우에만 하차한다.
    /// </summary>
    public bool TryExit()
    {
        if (!IsOccupied || CurrentSpeed > maxExitSpeed || driver.HasOtherControlLock(this)) return false;
        Vector3 position;
        if (!FindExitPosition(out position)) return false;
        RestorePlayer(position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        return true;
    }

    private bool FindExitPosition(out Vector3 position)
    {
        position = entryPosition;
        if (exitPoints == null || driverCollider == null) return false;
        foreach (Transform point in exitPoints)
        {
            if (point == null) continue;
            RaycastHit ground;
            if (!Physics.Raycast(point.position + Vector3.up * 2f, Vector3.down, out ground, 5f,
                exitObstructionMask, QueryTriggerInteraction.Ignore)) continue;
            if (ground.normal.y < 0.6f || ground.transform.IsChildOf(transform)) continue;
            Vector3 candidate = ground.point + Vector3.up * 0.08f;
            float radius = driverCollider.radius;
            float height = Mathf.Max(driverCollider.height, radius * 2f);
            Vector3 center = candidate + driverCollider.center;
            Vector3 bottom = center - Vector3.up * (height * 0.5f - radius);
            Vector3 top = center + Vector3.up * (height * 0.5f - radius);
            bool isBlocked = false;
            foreach (Collider hit in Physics.OverlapCapsule(bottom, top, radius, exitObstructionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == driver.transform || hit.transform.IsChildOf(driver.transform)) continue;
                isBlocked = true;
                break;
            }
            if (isBlocked) continue;
            position = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 외형과 카메라를 원래 상태로 돌리고 자기 소유 잠금만 해제한다. 탄약과 인벤토리는 변경하지 않는다.
    /// </summary>
    private void RestorePlayer(Vector3 position, Quaternion rotation)
    {
        throttle = 0f;
        steeringInput = 0f;
        isBraking = true;
        if (vehicleCamera != null) vehicleCamera.enabled = false;
        if (vehicleListener != null) vehicleListener.enabled = false;
        if (gameplayCamera != null) gameplayCamera.enabled = gameplayCameraWasEnabled;
        if (gameplayListener != null) gameplayListener.enabled = gameplayListenerWasEnabled;
        if (driver == null) return;
        driverMovement.Teleport(position, rotation);
        if (driverRenderers != null)
            for (int i = 0; i < driverRenderers.Length; i++)
                if (driverRenderers[i] != null) driverRenderers[i].forceRenderingOff = rendererWasHidden[i];
        driverCollider.enabled = colliderWasEnabled;
        driverCamera.ReleaseTargetControl(this, false);
        driverCamera.SynchronizeDirection(entryViewRotation);
        driver.ReleaseMovementSimulationLock(this);
        // 하차 F 입력을 같은 프레임의 플레이어 Update에서 다시 사용하지 않는다.
        PlayerInteraction interaction = driver.GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.SuppressInteractionForCurrentFrame();
        driver.ReleaseControlLock(this);
        driver = null;
    }

    private void OnDisable()
    {
        if (!IsOccupied) return;
        Vector3 position;
        if (!FindExitPosition(out position)) position = entryPosition;
        RestorePlayer(position, entryRotation);
    }
}
