# S13 기술 구조

## 1. 기획 확인과 제작 범위

프로젝트의 텍스트·문서 이름 및 S13/벙커/엔딩 관련 내용을 검색했습니다. 확정 스토리보드는 발견하지 못했습니다. 기존 `Assets/Capstone/Direction/Documentation/Implementation-KO.md`의 S13 25초/무기음/flash/ending fade는 확장 제안입니다. 이번 구현은 사용자 지시의 최소 범위만 사용하며, 기본 6.4초는 조절 가능한 프로토타입 타이밍입니다. 외부 Notion·온라인 문서의 존재/내용은 미확인입니다.

## 2. Scene·Prefab·오브젝트

기준 경로: `Assets/Capstone/Direction/Workspace/Sequences/S13/`

| 경로/오브젝트 | 역할 |
|---|---|
| `Scenes/S13_BunkerExit.unity` | 독립 테스트 씬, S13_TestRig 인스턴스 |
| `Prefabs/S13_TestRig.prefab` | 테스트 환경·플레이어·출력 카메라와 연결된 전체 Rig |
| `Prefabs/S13_SequenceRig.prefab` | 이식용 Rig. 실제 Camera/AudioListener/테스트 플레이어 및 벙커 참조 없음 |
| BunkerBlockout | 바닥·벽·천장·문틀·지지 리브·서비스 파이프·조명·문·백색 출구 블록아웃 |
| ExitDoorHinge | Y축 개방 대상. DoorLeaf/Handle 자식 |
| ExitInteraction | 거리 판정 기준 |
| CameraDoorPose / CameraExitPose | 외부 교체 가능한 카메라 포즈 |
| TEST_ThirdPersonPlayer | CharacterController, 기존 S03FirstPersonPlayer, ViewPivot, 보이는 캡슐 |
| TEST_Cameras/Main Camera | Camera, CinemachineBrain, URP AdditionalCameraData, 단일 AudioListener |
| TEST_Cameras/CM_ThirdPerson | CinemachineCamera priority20, 기존 S03ThirdPersonRig |
| S13_SequenceRig | DirectionSequence, PlayableDirector, SignalReceiver, S03ControlLockAdapter, S13EndingSequence, S13EndingAdapter, DirectionScreen |
| CM_S13_Ending | CinemachineCamera priority0 → 컷신 중100 → 원래 값 |
| S13_FadeCanvas | ScreenSpaceOverlay, sortingOrder32000, CanvasScaler 1280×720 |
| WhiteCoverage | 최상단 전체 Stretch Image, 기본 투명/흰색 전환, raycast 비활성 |
| S13_WhiteVolume | Global Volume, priority40, 기본 weight0 |
| S13_OwnedAudio | ambient/wind/door AudioSource3개. 다른 게임 오디오를 전역 정지하지 않음 |

## 3. 런타임 스크립트

| 파일 | 주요 메서드와 역할 |
|---|---|
| `Scripts/S13Timing.cs` | 시간·문 각도·광량·바람 음량. OpenAt/WhiteAt/CoveredAt/FinishAt 계산 |
| `Scripts/S13EndingSequence.cs` | BeginFromExit 참조·중복·길이 검사, SequenceStarted 스냅샷/입력 잠금, Update/Apply 카메라·문·백색·조명·오디오, OpenDoor/WhiteRise/Covered/Finish Signal, SequenceCompleted 백색 완료, Cancel/Restore 복구, ReleaseWhite/ResetPreview |
| `Scripts/S13EndingAdapter.cs` | StartFromExit/TryStartFromExit 외부 시작, BindGameplay 실제 참조 연결, onEndingRequested 전달, endingState 상태, ReleaseWhiteAfterHostReady |

재사용 코드(수정 없음):

- `Assets/Capstone/Direction/Scripts/DirectionSequence.cs`: 시퀀스 단일 소유권, 입력 Behaviour 상태 저장·복원, Cinemachine 우선순위/활성 상태 저장·복원, Timeline 및 취소 이벤트.
- `Assets/Capstone/Direction/Scripts/DirectionScreen.cs`: 기존 overlay 참조와 Clear 인터페이스. S13은 백색 곡선의 Image 색을 직접 갱신하여 자동 공통 Fade를 중첩하지 않습니다.
- `Assets/Capstone/Direction/Workspace/Sequences/S03/Scripts/S03ControlLockAdapter.cs`: 실제 PlayerController AcquireControlLock/ReleaseControlLock 토큰과 테스트 플레이어의 이전 ControlsLocked 보존.
- `Assets/S03Prototype/Scripts/S03FirstPersonPlayer.cs`와 `Assets/Capstone/Direction/Workspace/Sequences/S03/Scripts/S03ThirdPersonRig.cs`: 독립 씬의 입력/3인칭 추적. 기존 파일을 수정하지 않고 조합합니다.

## 4. Timeline·카메라·화면·사운드 설정

| 항목 | 기본값 |
|---|---|
| Camera Blend | 1초 |
| Door Opening | 1.8초, -95도 Y 회전 |
| White Rise | 3초, SmoothStep(0→1), 카메라 Dolly와 동시 |
| White Hold | 0.6초. 완료 후에도 백색 유지 |
| Timeline | `Timelines/S13_BunkerEnding.playable`, UnscaledGameTime, Hold, 길이6.5초 |
| Signals | OpenDoor1.0 / WhiteRise2.8 / Covered5.8 / Finish6.4초 |
| CinemachineBrain | EaseInOut0.65초, LateUpdate, IgnoreTimeScale |
| 연출 카메라 | FOV55, near0.05m. 현재 출력 포즈 → 문 앞 포즈 → 출구 포즈 |
| 카메라 흔들림 | 없음 |
| URP Bloom | intensity1.1, threshold0.8, scatter0.65 |
| ColorAdjustments | postExposure+1.2, saturation-20, Volume weight로 적용 |
| ExitLight | 시작1.2, 연출 추가광량10, Point/range10 |
| 내부 조명 | Point4개, intensity1.6, range6, 따뜻한 색 |
| 환경음 | 합성 험 volume0.16, 합성 바람 최대0.2. 백색 증가에 맞춰 감소 |
| 문 음량 | 0.35, 기존 `door_creak_short_01.wav` |
| Audio Mixer | 신규 생성 없음. 필요 시 각 AudioSource의 outputAudioMixerGroup에 본편 그룹 지정 |

백색 완전 차폐는 Bloom/HDR 여부에 의존하지 않는 최상단 UI로 보장합니다. URP 조명·Bloom·노출은 그 전에 자연스러운 광량 증가를 보조합니다. Profile은 S13 전용이며 공유 원본을 런타임에 수정하지 않고 Volume.weight만 조절합니다. HDRP API를 사용하지 않습니다.

## 5. 이벤트와 복구

`E + InRange` 또는 `Adapter.StartFromExit` → `BeginFromExit` → `DirectionSequence.Play` → 입력 Behaviour 비활성/카메라 priority100 → `SequenceStarted` → ControlLock.Acquire 및 스냅샷 → Timeline Signals와 Apply → `Covered` → `Finish` → Director.Stop → DirectionSequence 복원 → `SequenceCompleted` → 리소스 복원(백색 유지) → `onEndingReady` → Adapter의 `onEndingRequested`.

취소(X/Cancel), watchdog 기본20초, Director 비정상 중단, 연출 중 예외, 컴포넌트 비활성화는 공통 Cancel과 Restore로 입력·ControlLock·문 회전·가상 카메라 포즈/Lens·광량·Volume·화면 색·오디오 재생 여부/음량/샘플·timeScale/fixedDeltaTime을 복원합니다. 실제 출력 카메라는 원래 게임플레이 가상 카메라로 블렌드합니다. 다른 ControlLock 토큰과 다른 DirectionSequence 소유권을 침범하지 않습니다. 모든 엔딩 이벤트는 정상 경로에서1회입니다.

정상 완료 시 입력·카메라·오디오는 복구하지만 백색은 유지합니다. `ReleaseWhiteAfterHostReady`를 본편이 명시적으로 호출합니다. `ResetPreview`는 독립 테스트 반복용입니다. 완료 콜백 예외 시 백색을 해제하고 CompletionCallbackFailed를 기록합니다. 완료 후 Rig 비활성화도 백색을 해제합니다.

## 6. 메인 게임 통합 계약

1. 실제 벙커 씬에 `Prefabs/S13_SequenceRig.prefab`만 배치합니다. S13_TestRig의 플레이어/카메라/AudioListener는 복사하지 않습니다.
2. 기존 Main Camera에 CinemachineBrain이 있는지, 연출 가상 카메라의 OutputChannel을 수신하는지 확인합니다. URP post-processing/Volume layer 설정은 팀과 합의하여 본편 카메라에서 진행합니다. S13은 본편 카메라에 임의 컴포넌트를 추가하지 않습니다.
3. Inspector로 S13EndingSequence의 player/outputCamera/exitAnchor/door/doorView/exitView/exitLight/screen을 실제 객체에 연결합니다. ControlLockAdapter에는 실제 gamePlayer, DirectionSequence.inputComponents에는 실제 입력 Behaviour를 연결합니다. 중력/전투 등 어떤 컴포넌트를 정지할지는 본편 담당자가 선택합니다.
4. 또는 `S13EndingAdapter.BindGameplay(gamePlayer, playerRoot, camera, inputComponents, effectScreen, exit, leaf, cameraAtDoor, cameraPastDoor)`를 사용합니다. 이 메서드는 활성 연출/백색 대기 중 바인딩을 거부합니다. exitLight와 Volume 및 전용 오디오 참조는 Inspector로 연결합니다.
5. 본편 상호작용 시스템에서 `StartFromExit`를 호출합니다. 이 API는 이미 허가된 이벤트를 받는 것이므로 거리 검사를 하지 않습니다. 기본 E 입력만 InRange를 검사합니다. 중복 입력을 피하려면 allowKeyboard=false로 설정합니다.
6. `onEndingRequested`에서 `endingState`를 읽습니다: sourceSequence=S13, exitReached=true, whiteCovered=true. 대사·엔딩 해석·다음 씬 ID는 제공하지 않습니다.
7. 다음 엔딩 화면이 완전히 준비된 뒤 `ReleaseWhiteAfterHostReady`를 호출합니다. 씬 전환/크레딧/저장/메뉴는 이번 범위에 포함하지 않습니다.

Rig는 DontDestroyOnLoad 전역 백색 Relay를 만들지 않습니다. 씬을 교체할 경우 실제 화면 차폐를 유지하는 본편 호스트의 DirectionScreen을 연결하거나, 수신 씬이 차폐를 확보한 뒤 원본을 언로드해야 합니다. 완료 이벤트만 받고 즉시 S13 씬을 파괴하는 방식은 백색 유지가 보장되지 않습니다.

## 7. 에셋과 한계

기존 Underground Environment에서 조사한 `Models/Environment/Wall_Door_4x3.fbx` 등은 LFS 포인터였습니다. 복원·다운로드하지 않고 S13 안에서 Primitive와 전용 URP 재질로 환경을 생성했습니다. 외부 재질과 에셋은 수정하지 않습니다.

문 소리 참조: `Assets/Gamemaster Audio - Pro Sound Collection/Doors/door_creak_short_01.wav`. 실제 데이터 상태는 Play 검증에서 PCM 읽기로 검사합니다. 금속 벙커문 최종 폴리는 아니며 임시 후보입니다. `Audio/TEMP_BunkerHum.wav`, `Audio/TEMP_ExitWind.wav`는 합성 테스트 신호입니다. 배우 애니메이션·최종 벙커 아트·최종 믹싱·실제 게임 통합·엔딩 콘텐츠는 미완성입니다.

## 8. 검증 도구

`Editor/S13WorkbenchBuilder.cs`: AssetDatabase/PrefabUtility/EditorSceneManager를 이용한 생성, 재실행 시 기존 씬 열기, Timing 적용, 메뉴 제공.

`Editor/S13Validation.cs`: 실제 Play 상태 머신, 정상/취소/예외/watchdog/비활성화/다른 시퀀스 소유권/복구 검사. 활성 Camera·AudioListener 개수와 실제 출력 카메라 이동을 확인합니다. 단독 Camera.Render 캡처와 실제 여러 Camera 출력은 다르므로, 출력 Camera가 정확히1개인지 별도로 검사합니다. 결과는 VALIDATION.md에 기록합니다.

## 9. Game 화면 캡처 검사의 범위

최종 차폐 검사는 GUI Game 백버퍼를 ScreenCapture.CaptureScreenshotAsTexture로 읽습니다. 배치 모드에는 Game 백버퍼가 없어 해당 픽셀 검사만 SKIPPED를 기록합니다. 상태·백색 alpha·이벤트·복구 검사는 계속 실행합니다. Camera.Render용 ScreenSpaceCamera 변환 캡처는 Overlay 이후의 최종 Game 출력과 다르므로 최종 백색 판정에 사용하지 않습니다.

현재 Editor 배율에서 실제 pixelRect는 889.67×484.33, 백버퍼는 890×484였고 하단 RGB(16,16,16) 여백 3행을 관찰했습니다. 픽셀 검사는 내부 모든 픽셀이 백색인지 검사하고, 가장자리 4픽셀 범위에만 RGB 각 채널20 이하의 어두운 readback 여백을 허용합니다. 내부 비백색이나 가장자리의 장면 노출은 실패합니다. 이는 실제 화면 전체를 100% RGB255로 검증했다는 뜻은 아닙니다. 배포 빌드와 다른 해상도는 미검증입니다.
