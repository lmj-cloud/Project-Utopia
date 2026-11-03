# S07 기술 문서

## 범위와 상태

작업 루트: `Assets/Capstone/Direction/Workspace/Sequences/S07/`

Unity 6000.3.21f1, URP, Cinemachine 3.1.7, Timeline 1.8.12를 대상으로 하는 독립 시네마틱 기능 프로토타입입니다. 기존 S03/S06/공통 코드/팀원 씬/PlayerController/Packages/ProjectSettings/외부 원본은 수정하지 않습니다. 실제 검증 결과는 VALIDATION.md를 별도로 봅니다.

## 이벤트 계약

### 실제 S06에서 확인한 계약

`Assets/Capstone/Direction/Workspace/Sequences/S06/Scripts/S06BoutSequence.cs`

- 타입: `Capstone.Direction.Workspace.S06.S06AwardState`
- 필드: placing, nextSequence, intentionalFirstPerson, firstTraumaExperienced, medalTransformationPending
- `S06BoutSequence.onAwardReady`는 인수 없는 UnityEvent입니다.
- S06 정상 종료 시 owned 상태와 Phase==SecondPlace를 검사한 후 복구하고 Completed=true, firstTraumaExperienced=true, CompletionCount++를 설정한 다음 이벤트를 호출합니다.
- 취소/타임아웃은 완료 이벤트를 호출하지 않습니다.

### S07 입력

`S07AwardSequence.AcceptAward(S06AwardState state)`는 성공 여부 bool을 반환합니다. 2등, 의도적 1인칭, 첫 트라우마 경험, 메달 변화 대기 상태를 요구합니다. 이미 실행 중이거나 완료된 경우 거부합니다. 입력은 새 객체로 복사하므로 S06의 상태를 수정하지 않습니다.

`StartStandalone()`은 같은 타입의 임시 2등 데이터를 주입합니다. 저장된 테스트 프리팹은 autoStartStandalone=true입니다.

`S07S06Adapter`는 Source.onAwardReady를 OnEnable에서 구독하고 OnDisable에서 해제합니다. ReceiveAward는 Source.Completed 및 CompletionCount==1을 확인하고 Target.AcceptAward(Source.awardState)를 호출합니다. 이 Adapter는 S07 폴더에만 존재합니다. 실제 S06→S07 합동 씬 재생 검증과 비동기 씬 로딩은 후속 통합 범위입니다.

### S07 출력

`onCompleted`는 정상 Finish 신호, 소품 교체 완료, 전장 정면 최소 7.9초 조건을 확인하고 복구한 다음 한 번 호출합니다. `wakeState`는 previousPlacing=2, rememberedFallenComrades=true, nextStoryPoint="BedroomAfterNightmare"를 전달합니다. 수신 측이 상태를 복사하고 다음 게임 진행을 시작해야 합니다. `onStarted`, `onCancelled`도 제공합니다.

## 오브젝트와 컴포넌트

| 경로/오브젝트 | 역할 |
|---|---|
| Scenes/S07_AwardMemory.unity | 독립 씬. 전체 테스트 프리팹 인스턴스 |
| Prefabs/S07_TestRig.prefab | 전체 테스트 환경·카메라·플레이어·연출 |
| S07_TEST_ONLY/Award_Blockout | 시상대와 2등 문구, 시상식 조명 |
| Battlefield_Memory_Blockout | x=80의 폐허·전우 유니폼 상징·연기/먼지·차가운 조명 |
| Bedroom_Blockout | x=160의 침대·베개·협탁·따뜻한 조명 |
| TestPlayer | CharacterController와 기존 S03FirstPersonPlayer, 3인칭 테스트 몸체 |
| TestPlayer/CinematicTorso/HeadPose | 상체 숙임과 독립 고개 시선 |
| CinematicTorso/FirstPersonHands/LeftGrip_Stable | 고정된 왼손, 은메달과 군번줄 공통 부모 |
| FirstPersonHands/RightHand | 머리 짚는 손과 비발사 권총 |
| Cameras/Main Camera | Camera, AudioListener, CinemachineBrain, URP 카메라 데이터 |
| Cameras/CM_ThirdPerson | 기존 S03ThirdPersonRig로 테스트 플레이어 추적 |
| HeadPose/CM_FirstPerson_S07 | 고개에 고정된 S07 전용 1인칭 CinemachineCamera |
| HUD/BlackEyeClosure | 검정 uGUI Image. HUD보다 뒤에 생성하여 검정이 안내 텍스트까지 차폐 |
| S07_Direction | S07AwardSequence, S07S06Adapter, DirectionSequence/Screen, PlayableDirector, SignalReceiver, S03ControlLockAdapter |
| S07_TraumaVolume | S07 전용 전역 Volume. 기존 RenderSettings 변경 없음 |
| TEMP_* AudioSource 6개 | 시상식/전장/실내/금속/이명/호흡 합성 PCM 테스트 소리 |

손은 Palm·CurledFinger·Forearm Primitive입니다. 실제 Humanoid 애니메이션/IK는 없습니다. 은메달은 원판과 리본, 군번줄은 두 금속 판과 구슬 체인, 권총은 그립과 슬라이드 블록아웃입니다. 권총에는 발사/전투 스크립트가 없습니다.

## 런타임 스크립트

### Scripts/S07AwardSequence.cs

- AcceptAward / StartStandalone: 입력 검증, 재진입 차단, 공통 Sequence 시작
- SequenceStarted: 위치·자세·활성 상태·오디오·시간척도 스냅샷, 본인 소유 ControlLock 확보, 1인칭 손 표시
- Update: E/X 처리, 실시간 watchdog, 실제 Timeline 시간에 따른 자세 갱신, 예외 시 Cancel
- ApplyPoseAndCover: 고개·상체·오른손 위치, 왼손 고정, 눈 감김 곡선, 음량/Volume 제어
- CoveredSwap: 완전 검정 검증 후 메달→군번줄, 권총 노출 준비, 전장 위치 이동
- WarFront / WarEnd: 정면 구간 실제 실시간 길이 측정, 이명 시작
- CoveredBedroom / Wake: 완전 검정 검증 후 침실 이동, 방 소리·거친 호흡 시작
- FinishCue / SequenceCompleted: 정상 완료 판정과 후속 상태/이벤트
- Cancel / SequenceCancelled / Restore: 입력·카메라·화면·오디오·시간척도 복구
- InjectFaultOnSwap: 검증용 진단 프로퍼티이며 저장 프리팹에서 사용하지 않음

### Scripts/S07S06Adapter.cs

S06의 실제 인수 없는 이벤트를 S07의 타입 지정 입력 메서드로 연결하는 작은 Adapter입니다. Source/Target이 없는 독립 테스트에서는 아무 구독도 수행하지 않습니다.

### 기존 코드 재사용

- `Assets/Capstone/Direction/Scripts/DirectionSequence.cs`: 독점 실행, 카메라 우선순위 저장·복구, Timeline 종료/취소 수명. screen=null로 설정해 S03의 흰색 전환을 수행하지 않습니다. 보조 타임아웃은 180초이며 실제 제한은 S07 50초 실시간 watchdog입니다.
- `Assets/Capstone/Direction/Scripts/DirectionScreen.cs`: 기존 Image 참조 및 Clear. S07의 검정 커브는 전용 코드에서 같은 Image를 제어합니다.
- `Assets/Capstone/Direction/Workspace/Sequences/S03/Scripts/S03ControlLockAdapter.cs`: 테스트 ControlsLocked 저장·복원 및 실제 PlayerController.Acquire/ReleaseControlLock 호환
- `.../S03/Scripts/S03ThirdPersonRig.cs`: 테스트용 3인칭 복귀 카메라
- `Assets/S03Prototype/Scripts/S03FirstPersonPlayer.cs`: 테스트 이동/마우스, 기존 코드 변경 없음
- `Assets/S03Prototype/Fonts/S03PromptFont.asset`: 기존 글꼴 참조

DirectionAudio는 S07에서 사용하지 않습니다. 효과의 소유권과 기존 재생 상태를 정확히 복원하기 위해 S07 전용 AudioSource를 사용합니다. Mixer나 공통 버스 음량을 수정하지 않습니다.

테스트 이동 컴포넌트는 ControlsLocked 상태에서도 중력을 적용하므로, S07의 DirectionSequence.inputComponents에 그 테스트 컴포넌트만 지정해 실행 중 enabled=false로 저장/복구합니다. 실제 PlayerController를 비활성화하는 구현은 아닙니다. 테스트 위치 이동은 기존 S03FirstPersonPlayer.Teleport로 CharacterController와 수직 속도를 안전하게 재설정합니다.

## Timeline과 카메라

`Timelines/S07_AwardMemory.playable`은 FixedLength 29초의 SignalTrack입니다. 애니메이션 클립/Cinemachine Track 대신 Timeline 신호와 S07의 절차적 자세 커브를 사용합니다.

| 시간 | 신호/동작 |
|---|---|
| 0~1.5 | 1인칭 전환, 왼손 은메달로 시선 이동 |
| 2.1 | Headache: 오른손 머리 짚기 시작 |
| 2.5~3.3 | 고개 오른쪽 (+10도 목표) |
| 3.5~4.3 | 고개 왼쪽 (-12도 목표) |
| 4.5~5.3 | 고개 오른쪽 (+11도 목표) |
| 6~7 | CloseEyes: SmoothStep 검정 알파 0→1 |
| 7.1 | CoveredSwap: 검정 중 소품/배경 교체 |
| 7.8~8.6 | 검정 알파 1→0, 왼손 군번줄 확인 |
| 10~13 | Rise: pitch43/yaw-27에서 정면0으로, 상체 숙임 해제 |
| 13~21 | WarFront: 정면 전장 약 8초 |
| 21~22 | WarEnd: 이명, 최대0.65도 추가 흔들림, 검정 증가 |
| 22.3 | CoveredBedroom: 검정 중 침실 이동 |
| 23~24 | Wake: 검정 해제 |
| 23~28.5 | 거친 호흡, 1.15Hz·1.5cm 머리/상체 운동 |
| 28.5 | Finish: Director Stop, 공통 카메라 복구 후 완료 이벤트 |

DirectorUpdateMode.UnscaledGameTime입니다. 실제 재생 중인 Timeline이 Signal을 보내고, S07 Update는 director.time을 읽어 Playhead와 자세/눈 감김 커브를 갱신합니다. 수동 Evaluate는 패키지에서 Signal을 억제하므로 사용하지 않습니다. 시간척도와 무관하게 재생하며 watchdog은 실제 경과 50초에서 복구합니다. Editor가 긴 시간 멈춰 전환 구간을 건너뛴 경우에도 차폐 검사에 실패하면 노출된 소품 교체를 수행하지 않습니다. 런타임 수동 스크러빙과 재생 속도 변경은 이 프로토타입의 검증 범위 밖입니다.

Cinemachine FOV62, near clip0.025m, 기본 EaseInOut 블렌드0.65초. 시네마틱 우선순위는 공통 Sequence가100으로 확보하고 원래0으로 복원합니다. 일반 카메라는20, 추적 오프셋은 (0.6,0.45,-2.3)m입니다. 손은 HeadPose 자식이 아니라 공통 상체의 형제 리그에 있으므로 고개 회전과 독립적으로 같은 쥔 자세를 유지합니다. 왼손 앵커는 (-0.28,1.15,0.48)m, 오른손 기본 앵커는 (0.29,1.32,0.5)m이며, 머리 짚기 목표는 (0.18,1.62,0.10)m입니다. 메달과 군번줄은 같은 왼손 앵커 아래에서 손바닥 앞·아래로 늘어뜨려 읽을 수 있게 배치합니다. 실제 손/카메라 자연스러움은 수동 평가 대상입니다.

## 화면·조명·음향

- 흰색 페이드·HDRP·Bloom을 추가하지 않습니다. 검정 시야 차폐만 사용합니다.
- Volume: saturation -65, contrast12, exposure -0.15, vignette0.28/smoothness0.65, 전장 weight0.8
- 시상식 방향광1.1, 전장0.85, 침실0.75. 각 환경의 활성화와 함께 해당 조명을 전환합니다. 원본 재질이나 전역 RenderSettings를 수정하지 않습니다.
- 모든 WAV는 S07 내부에서 생성한 22,050Hz/16-bit/mono PCM, 최종 음원 아님
- 시상식 루프2초/볼륨0.25, 전장 루프4초/기본0.3, 실내 루프2초/기본0.16
- 금속0.5초/볼륨0.25, 이명2초 루프/0.18, 거친 호흡2.6초 루프/시작0.45→0.22
- 현재 전장음은 저주파·잡음 기반 상징적 테스트 소리이며 실제 전투 Foley·거리별 공간 음향·성우 호흡이 아닙니다. AudioSource.spatialBlend=0, Mixer 라우팅 없음.
- 실제 라이브러리 LFS 복원/다운로드는 수행하지 않았습니다.

## 복구 정책

시작 시 자세/위치/환경 활성/소품/몸체/Volume weight/시간척도·fixedDeltaTime/소유 AudioSource 재생·볼륨·샘플 위치를 저장합니다. 공통 Sequence가 카메라 우선순위를 저장합니다.

정상 완료: 침실 위치·환경은 유지하고, 1인칭 손을 숨기고 3인칭 몸체·카메라·입력을 복구합니다. 전용 오디오는 시작 전 상태로 복원합니다. 방의 계속되는 환경음은 후속 게임 측에서 인계받아 구성해야 합니다.

취소/오류/타임아웃/비활성화: 시작 위치와 환경·메달 상태로 복귀하고 동일한 화면·입력·음향 복구를 수행합니다. 완료 이벤트를 보내지 않습니다. 다른 시스템이 가진 ControlLock은 해제하지 않습니다. S07은 시간척도를 변경하지 않지만 시작 전 값을 저장해 복구 시 원래 값으로 돌립니다. 외부 시스템이 동시에 시간척도를 소유하는 통합 상황에서는 소유권 정책 조정이 필요합니다.

모든 엔진 장애나 임의의 외부 스크립트 예외를 포괄하는 전역 복구 시스템은 아닙니다. 유효하지 않은 입력/누락 참조는 잠금 확보 전에 거부하고, S07 Update의 오류는 Cancel로 처리합니다. 테스트로 입증한 범위는 VALIDATION.md에만 확정적으로 기록합니다.

## 기존 게임 통합 순서

1. 별도의 통합 씬/바인더에서 S07의 연출 리그만 인스턴스화하고 autoStartStandalone=false로 설정합니다.
2. 기존 출력 Camera/AudioListener/CinemachineBrain 한 세트를 사용합니다. 전체 S06·S07 테스트 프리팹을 그대로 함께 넣지 않습니다.
3. ControlLockAdapter.prototypePlayer를 비우고 gamePlayer에 실제 PlayerController를 연결합니다. 배우/머리/상체/손/스폰과 카메라 참조를 실제 캐릭터에 연결합니다. 현재 Primitive 리그를 교체할 때 손 소품의 동일 부모/차폐 계약을 보존해야 합니다.
4. S07S06Adapter.Source/Target을 S06/S07 컴포넌트에 연결합니다. S06은 기존 코드와 이벤트 그대로 사용합니다.
5. 씬이 따로 로딩되는 구조에서는 S06 상태를 먼저 영속 서비스에 복사하고, S07 로딩 후 AcceptAward를 호출합니다. 현재 Adapter가 비동기 로딩까지 담당하지는 않습니다.
6. S07.onCompleted 수신 측에서 wakeState를 복사하고 다음 게임 진행을 시작합니다. onCancelled는 실패/취소 분기로 처리합니다.
7. 실제 게임의 입력·추가 카메라·시간척도·오디오 소유권과 함께 합동 검증합니다.
