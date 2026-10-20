# Project-Utopia 연출 기반 및 S03 — 2026-10-11

## 실제 조사 결과
- 프로젝트: C:\Users\djdjd\Desktop\DB\Project-Utopia
- Unity 6000.3.21f1. URP 17.3.0, Cinemachine 3.1.7, Timeline 1.8.12, Input System 1.20.0, uGUI 2.0.0. manifest와 lock 파일 확인.
- 씬: TestScene, jungwoo. 처음 관찰한 열린 씬은 jungwoo. 작업 중 Editor 창이 닫힌 것을 두 번 확인. Pipeline 패키지는 manifest에 없으며 CLI 연결 불가.
- PlayerController/PlayerMovement: 이동·점프·달리기·무기 제어. PlayerCameraController: Cinemachine 3의 1/3인칭 카메라. 기존 기능은 유지.
- PlayerInteraction: F키, 주변 HighlightableObject와 NPC 검색. InteractableObject.onInteracted는 기존 UnityEvent이며 S03 진입 연결에 재사용.
- NPCDialogue/DialogueUI: 텍스트 대화 및 타이핑. 음성 동기화 이벤트는 아직 없음.
- WeaponAssaultRifle.OnFired(Action<Vector3>) 있음. 무기·탄피가 자체 AudioSource 사용. 새 오디오 시스템과 중복 재생하지 않도록 기존 발사음을 유지.
- jungwoo에 Main Camera/AudioListener, CM_FirstPerson·3인칭 카메라, Global Volume, Player, NPC와 시험 오브젝트 존재. 저장 파일의 Main Camera post processing=true. 실제 런타임 카메라/Volume mask와 profile override 검증은 필요.
- 기존 AudioMixer와 Timeline .playable 에셋은 파일 검색에서 발견하지 못함.
- 재사용: 사격·탄피·무기 꺼내기·재장전 소리, 무기·탄피·명중·총구 프리팹, 이동/사격/재장전 애니메이션. Dizziness.shadergraph/DizzyScreen_Mat도 있으나 기존 의도와 적용 경로 확인 전 사용하지 않음.
- 경기장·집·차량·병원·가족/의료진 캐릭터와 해당 연기·대사·BGM·관중음의 완성 여부는 확인되지 않음. 존재한다고 가정하지 않음.

## 기존 변경과 보존
원래 브랜치 feature/jungwoo. 작업 전 변경: Toon Muzzleflash 1.prefab, EditorBuildSettings.asset, ShaderGraphSettings.asset; 미추적 jungwoo.unity 및 meta, Project-Utopia.slnx. 덮어쓰거나 스테이징하지 않음.
별도 feature/direction-s03-foundation 브랜치 생성은 .git 내부 쓰기 권한 제한으로 실패. 브랜치 변경/커밋 없음. 이번 추가는 Assets/Capstone/Direction 아래 새 파일만.

## 이번에 작성한 파일과 기능
Scripts/DirectionAudio.cs: 5개 버스, MixerGroup 연결, 유한한 효과음 source pool, 2D/3D, 중복 억제, BGM/환경음 loop 및 fade, 볼륨.
Scripts/DirectionScreen.cs: uGUI Overlay Image의 검정/백색 페이드 기반.
Scripts/DirectionSequence.cs: 기존 UnityEvent 연결, 실행 중복 방지, 입력 Behaviour 상태 저장/복원, Cinemachine priority 전환/복원, Director 실행/정지/완료/timeout, 종료·취소 이벤트.
Scripts/DirectionVolume.cs: 독립 runtime VolumeProfile, Bloom/색감/Vignette/LensDistortion/ChromaticAberration. shared 기존 에셋 수정 없음. 왜곡은 URP 대안이며 흐림/Depth of Field는 미구현.
Scripts/DirectionCue.cs: Timeline Signal/UnityEvent에 노출할 음성·효과음·Impulse 흔들림·flash·피격 표현.
Scripts/DirectionTestAudio.cs: 테스트용 합성 신호음. 문 소리나 관중 녹음이 아니며 테스트 전용.
Editor/DirectionSetup.cs: Unity API로 별도 S03 테스트 씬 및 유효한 Timeline 생성. 120초 빈 시네마틱 Timeline 생성 메뉴. 수작업 YAML 생성 없음.

## 초심자 S03 독립 테스트
1. Unity에서 Project-Utopia를 연다. 스크립트 가져오기가 끝난 뒤 Console 오류 여부 확인.
2. 상단 Capstone > Direction > Create isolated S03 test scene 실행. 기존 씬은 저장하거나 닫지 않고 새 씬을 Additive로 만든다.
3. Project 창 Assets/Capstone/Direction/S03_Test.unity를 단독으로 연다. 기존 씬이 함께 로드되면 Main Camera/AudioListener 중복이 생기므로 Play 전에 단독으로 열 것.
4. Play 후 Game 창에 포커스하고 F8. 합성 전환음 → 백색 페이드 → 합성 환경음 페이드인 → 화면 복귀 → 약 2초 Timeline 완료. 반복 F8에 중복 실행이 없어야 함.
5. 임시 경기장 큐브만 존재. 실제 공간 이동·문 상호작용·플레이어가 없는 테스트 씬이며 최종 장면 완성이 아님.
6. S03_TEST_ONLY의 DirectionSequence On Completed에 테스트 UI/로그 콜백을 연결해 완료 1회 확인. 플레이어 복구는 아래 실제 통합 후 확인.

## 기존 게임 씬 통합 (아직 적용하지 않음)
1. 별도 빈 GameObject DirectionRoot를 만든다. DirectionAudio, DirectionScreen, DirectionSequence를 추가한다. 연출 오브젝트를 플레이어/문 아래 두지 않는다(문 비활성화 시 연출 취소 방지).
2. Canvas를 Screen Space Overlay로 만들고 Image를 자식으로 만든다. Image Anchor를 사방 Stretch, offsets=0, 색 alpha=0, Raycast Target=false. Canvas Sorting Order=32000. Image를 DirectionScreen.Overlay에 연결.
3. DirectionSequence.Screen과 Audio System에 위 컴포넌트를 연결한다.
4. Input Components 배열에 기존 Player의 PlayerController와 PlayerInteraction을 연결한다. 기존 PlayerMovement/CharacterController는 연결하지 않는다. PlayerController.OnDisable이 이동/사격을 멈추며 기존 camera Look 입력도 끊긴다. 비활성화 전 상태로 복구한다.
5. Director에는 별도 S03 PlayableDirector를 연결. Play On Awake=false, Wrap Mode=None, Update Method=Unscaled Game Time. Timeline Timeout은 실제 컷 길이보다 크게 설정(2분 영상이면 130초 이상).
6. 시네마틱 CinemachineCamera를 별도로 만들고 Cinematic Camera 필드 연결. 기존 Main Camera/CinemachineBrain/AudioListener는 교체하지 않는다. Priority는 재생 중 100, 종료 후 이전 값으로 복구. 기존 카메라 priority가 100 이상이면 팀과 협의해 조정.
7. 현관문의 기존 InteractableObject > On Interacted에 DirectionRoot의 DirectionSequence.Play를 추가. 기존 이벤트 항목 삭제 금지. 실제 문 오브젝트가 없으면 F8 테스트 옵션 사용.
8. Transition Sound에 문/공간 전환 clip, Crowd Loop에 실제 관중 loop 연결. 테스트 합성음 컴포넌트는 실제 게임에 추가하지 않는다.
9. On Transition Covered는 화면이 하얗게 덮인 시점. 메인 개발자의 공간 이동/씬 로딩 메서드를 연결한다. 씬 로딩이 비동기라면 이 단순 S03 프로토타입에 로딩 완료 대기 인터페이스를 추가한 다음 사용. 현재 다른 씬으로 전환하면 DirectionRoot가 파괴되어 취소됨.
10. On Completed에서 게임 진행 이벤트 연결. Cancel은 취소 이벤트만 호출하며 성공 완료를 호출하지 않음. 연출 오브젝트 비활성화/Timeline timeout도 입력·카메라를 복구.
11. NPC 대화나 다른 입력 잠금이 동시에 실행되는 경우는 아직 검증 안 됨. 팀 공용 lock-token 인터페이스가 있으면 대체 어댑터 필요. 현재는 잠금 시작 당시 Behaviour.enabled 복원으로 제한.

## AudioMixer 수동 설정
Assets/Capstone/Direction/Audio 폴더에서 Create > Audio Mixer. Master 아래 BGM/SFX/Ambient/Voice/UI 그룹 생성.
DirectionAudio.Groups Size=5, 순서 0 BGM / 1 SFX / 2 Ambient / 3 Voice / 4 UI로 해당 그룹을 드래그.
Mixer에는 생성한 mixer 연결(참조용); 현재 볼륨 메서드는 Source gain을 제어한다. Master slider는 믹서 Master 그룹에서 제어하거나 SetMasterVolume 사용. 기존 무기와 탄피 Source는 합의 후 SFX 그룹 연결; 이번 작업에서 기존 source 수정 안 함.
기존 소리의 import 설정은 변경하지 않음. 긴 BGM/환경음은 플랫폼과 길이 확인 후 Streaming/Compressed 결정.
BGM clip 교체는 새 clip fade-in이며 두 곡의 동시 crossfade는 미구현.

## 후처리와 카메라 흔들림
DirectionVolume은 Default layer의 독립 high-priority runtime Volume을 만든다. Main Camera의 Volume Layer Mask에 Default가 포함되어야 함. URP Camera Render Post Processing=true, Renderer PostProcessData 및 HDR 확인.
SetTrauma(0..1), SetBloom(float), SetTint(Color), ResetEffects를 이벤트에서 호출. 기존 profile을 저장/변경하지 않음. 회복 시 ResetEffects 호출 필요. 피격 효과 지속시간 자동 복구는 미구현.
DirectionCue.Impulse에 CinemachineImpulseSource 연결. 기존 카메라에 CinemachineImpulseListener를 합의 후 추가하고 동일 채널 설정. ImpulseDefinition/강도 설정은 Inspector에서 진행. 임의로 player camera 구조를 바꾸지 않음.
부드러운 시점 이동·회전은 새 시네마틱 카메라 transform AnimationTrack과 CinemachineBrain blend로 제작. 자동 샷 경로/실제 카메라 컷은 아직 미제작.

## S01~S13 확장 계획 — 합계 300초 제안, 아직 장면 제작 아님
|장면|게임 시간|재사용 기반|필요한 팀 이벤트/에셋|
|---|---:|---|---|
|S01|20|검정 fade, warm tint, Ambient|기상 애니메이션, 침실, 아침음|
|S02|25|camera Timeline, Voice|트로피 강조 컷, 가족 대사·자막 cue|
|S03|15|이번 테스트 전환|현관문 이벤트, 공간 이동, 실제 경기장/관중음|
|S04|20|camera Timeline, Voice/Ambient|선수 등장 애니메이션, 아나운서|
|S05|40|Hit/Shake/SFX|메인 개발자 공격·피격·방어·경기종료 이벤트; 전투 미구현|
|S06|20|Trauma/flash/Ambient fade|총성·이명, 전쟁 환경; 슬로모션 소유권 인터페이스 필요|
|S07|25|Timeline/flash/Voice|메달·군번줄 모델, 전환 애니메이션, 플래시컷|
|S08|15|Animation+Signal audio|복약/손 떨림 애니메이션, 호흡|
|S09|25|Voice/Ambient/fade|가족·차량, 문·엔진음|
|S10|25|Timeline/Shake/flash|헤드라이트·충돌·유리 VFX, 경적/이명; 물리차량 미구현|
|S11|25|fade/cold tint|병원·의료진·장비음; 흐림 효과 추가 필요|
|S12|20|BGM/SFX/Ambient|무기고/금속문, 획득·장착 이벤트|
|S13|25|기존 무기음, flash, ending fade|발사 OnFired, 명중/경보/문열림/엔딩 이벤트|
5분 제한에는 조작/대화 시간을 포함하여 실제 플레이 테스트로 재배분 필요. 이번 작업은 다른 12개 장면이 완성되었다는 의미가 아님.

## 별도 120초 시네마틱 기반
Capstone > Direction > Create 120 second cinematic Timeline 메뉴로 빈 유효한 Timeline 생성. 영상이나 카메라 컷이 생성되는 것은 아님.
기존 환경/캐릭터의 복제 또는 별도 씬에서 재사용. 시네마틱 전용 CinemachineCamera들과 Director 생성. Timeline에 Animation Track, Audio Track, Cinemachine Track, Signal Track을 추가하고 scene instance를 각각 Bind.
예시 편집 분배: 기상/가족 0~20, 경기/환각 20~50, 메달/약 50~70, 외출/사고 70~95, 병원/무기고/벙커 95~120초. 실제 카메라 컷·애니메이션·믹싱은 후속 제작.
Recorder 패키지는 manifest에 없음. 임의 설치 안 함. 팀 승인 후 Unity Recorder를 설치하거나 화면 녹화 사용. 해상도/FPS/출력 경로를 먼저 결정하고 120초를 실측 검수. 실제 영상 파일은 미생성.

## 검증 상태 및 다음 순서
실제 설치 버전 라이브러리를 참조한 외부 C# 컴파일 검사 통과(최종 전체 파일 검사 결과는 별도 보고 확인). Unity Editor import/Play 모드/S03 완료 이벤트/입력 복구/소리 청취/후처리 시각 QA는 미검증.
기존 파일 수정 없음. 실제 Timeline/씬/Mixer는 생성 도구를 실행하기 전에는 아직 존재하지 않음.
우선순위: Editor 컴파일 → 독립 F8 테스트 → 실제 Player 입력 복구/취소 테스트 → 문 이벤트/공간 이동 연결 → 실제 소리 교체 → S01~13 순차 제작 → 120초 시네마틱 컷/영상 출력.
검수: F8 연타, 정상 완료 1회, 중간 Cancel/disable, Timeline 무한 loop timeout, 기존 disabled input 유지, Timeline 없는 실행, AudioClip 누락, 시간척도 0에서도 fade/timeout 작동, 기존 카메라 복귀, 기존 팀 변경 보존.
