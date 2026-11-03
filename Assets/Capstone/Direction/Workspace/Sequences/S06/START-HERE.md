# S06 첫 트라우마 — 독립 기능 프로토타입

## 열고 실행하기

1. Unity 6000.3.21f1에서 프로젝트를 열고 컴파일이 끝날 때까지 기다립니다.
2. 메뉴 **Capstone → Direction → S06 Workspace → Open test scene**을 클릭합니다. 저장되지 않은 다른 씬이 있으면 도구가 중단됩니다. 먼저 본인이 저장 여부를 결정하세요.
3. 열린 씬은 `Scenes/S06_FirstTrauma.unity`입니다. ▶ Play를 누르고 Game 탭을 클릭합니다.
4. WASD와 마우스로 테스트 플레이어를 확인합니다. **E**로 경기 연출을 시작합니다. **X**로 중간 취소합니다. Esc는 마우스 포인터만 해제합니다.
5. 약 12초 동안 자동 진행합니다. 2등 결과 후 조작과 일반 3인칭 카메라가 돌아옵니다. 완료 후 중복 실행은 막혀 있으므로 다시 테스트하려면 Play를 종료하고 재진입합니다. 취소한 실행은 E로 재시도할 수 있습니다.
6. 자동 검사: Play가 꺼진 상태에서 **Capstone → Direction → S06 Workspace → Run Play validation (exits Play when done)**. 종료 후 Console에서 `S06_VALIDATION_PASS` 또는 `S06_VALIDATION_FAIL`을 확인합니다. 테스트용 콜백 예외 `S06_EXPECTED_FAULT`와 타임아웃 경고는 의도적으로 발생시킵니다.

## 실제 구조

작업 루트: `Assets/Capstone/Direction/Workspace/Sequences/S06/`

| 경로 | 역할 |
|---|---|
| Scenes/S06_FirstTrauma.unity | 기존 게임과 분리된 테스트 씬 |
| Prefabs/S06_TestRig.prefab | 환경·플레이어·카메라·오디오·연출 로직 전체 테스트 리그 |
| Timelines/S06_FirstTrauma.playable | 12초 SignalTrack, 애니메이션/전투 판정 트랙 아님 |
| Timelines/*.signal | 총성·주관적 접근음·환각·경기 붕괴·2등 신호 5개 |
| Scripts/S06BoutSequence.cs | Timeline 호출, 상태·효과·복구, 후속 이벤트 계약 |
| Editor/S06WorkbenchBuilder.cs | AssetDatabase/PrefabUtility 기반 생성. 기존 테스트 씬이 있으면 열기만 수행 |
| Editor/S06Validation.cs | 실제 Play 모드 기능 검사. 런타임 테스트 리그 교체는 저장하지 않음 |
| Materials/ | S06 전용 블록아웃 재질·외부 재질 복제본·주관적 VolumeProfile |
| Audio/TEMP_*.wav | 새로 생성한 합성 테스트 음원, 최종 사운드 아님 |

`S06_TEST_ONLY` 아래 Arena, TestPlayer, Opponent_TestRhythm_NoCombat, Cameras, 조명, SubjectivePerception, HUD, S06_Direction, AudioSource들이 있습니다. 실제 복싱 링 메시를 사용하며 벽·천장·단형 관람석은 Primitive 블록아웃입니다. 관중 캐릭터와 전투 애니메이션은 없습니다.

## 진행 순서와 설정

| Timeline 시간 | 이벤트 |
|---|---|
| 0초 | 입력 잠금, Cinemachine 0→100 우선순위, 0.7초 EaseInOut 블렌드, 최소 경기 리듬 테스트 |
| 3초 | 경기장 밖 위치의 출발 총성 테스트 소스 재생 |
| 3.7초 | 주인공 앞 14m→0.6m 가상 음원 접근, 약 2.2초간 볼륨 0.06→0.65 증가 |
| 6초 | 0.75초 첫 트라우마 플래시. 장소 이동 없음 |
| 6.75초 | 환각 종료, 상대 압박 리듬 확대, 집중력 붕괴 표현 |
| 10초 | 임시 HUD에 2등 결과 표시 |
| 12초 | 정상 종료·복구 후 시상식 준비 이벤트 1회 |

- 주관적 효과: Volume weight 0→최대1, Saturation -85 / Contrast 15 / Exposure -0.35 / Vignette 0.35. Bloom·HDRP·별도 전쟁터는 사용하지 않습니다.
- 군중: 기존 chatter WAV 루프, 기본 볼륨 0.38→최저12%, LowPass 22kHz→650Hz. 실제 환호음이 아닙니다.
- 카메라: FOV55, 약 1.8cm·0.6도 이내의 시선 피벗 변화. 제3자 카메라 리그는 기존 S03ThirdPersonRig를 수정 없이 참조합니다.
- 합성 총성/접근음/이명은 효과의 기능 시험용입니다. 현실적인 출발 권총·총알 접근음으로 승인된 리소스가 아닙니다.

## 재사용 및 복구

- 기존 DirectionSequence: 독점 실행, Timeline 수명, 카메라 우선순위 저장/복구, 60초 보조 타임아웃 (실제 S06 제한은 20초 실시간 watchdog). S03식 흰색 씬 이동을 사용하지 않도록 sequence.screen=null, transitionSound/crowdLoop=null로 설정합니다.
- 기존 DirectionScreen: 전용 HUD의 약한 착색 오버레이와 Clear 사용.
- 기존 S03ControlLockAdapter / S03FirstPersonPlayer / S03ThirdPersonRig: 수정 없이 테스트 이동·잠금·카메라에 참조합니다. 이 의존성을 제거하고 싶다면 후속 통합에서 대체할 수 있습니다.
- DirectionAudio는 공통 Sequence의 기존 오디오 슬롯에 연결되어 있지만, 이번 S06의 주관적 필터와 이동 음원은 소유권·중단 제어를 위해 S06 전용 AudioSource를 사용합니다. 현재 Mixer 라우팅은 구성하지 않았습니다.
- X, Timeline 60초 보조 타임아웃 (실제 S06 제한은 20초 실시간 watchdog), 별도 20초 실시간 watchdog, 컴포넌트 비활성화에서 동일 복구 경로를 호출합니다. 오디오 정지, 군중 볼륨·필터 복원, Volume/오버레이 해제, 피벗·상대 위치 복원, 본인 소유 ControlLock 해제.
- 콜백 예외는 공통 Sequence 또는 S06의 예외 처리에서 취소/복구합니다. 모든 엔진 장애나 임의 외부 코드 예외를 포괄하는 전역 예외 처리 시스템은 아닙니다.

## 후속 시상식 연결 계약

`S06BoutSequence.onAwardReady`는 정상 Timeline 종료 및 SecondPlace 단계 통과 후에만 호출됩니다. Inspector에서 수신 컴포넌트의 메서드를 연결하거나 코드에서 AddListener 하세요. 수신 측은 `awardState`를 읽습니다.

- placing=2, nextSequence="AwardCeremony", intentionalFirstPerson=true
- firstTraumaExperienced=true, medalTransformationPending=true
- 취소/타임아웃에서는 호출하지 않음. 정상 완료 후 재실행 차단.
- 실제 다음 씬 로딩은 하지 않습니다. 수신 측에서 상태를 영속 서비스에 복사한 다음 시상식 씬 로드를 담당해야 합니다.
- 본 게임 통합 시 테스트 Capsule/상대 리듬/HUD를 제외하고, ControlLockAdapter.gamePlayer에 실제 PlayerController를 연결하고 prototypePlayer를 비우세요. Cinemachine 카메라·피벗·오디오 참조를 실제 대상에 연결합니다.
- 전투 시스템의 경기 시작에서 BeginBout 호출, 외부 총성 이벤트는 StarterShot으로 연결할 수 있습니다. 현재는 Timeline이 타이밍과 2등 결과를 결정하는 기능 시험입니다. 실제 승패 판정 및 경기 흐름에 맞춘 Timeline/전투 Adapter는 후속 작업입니다. 전투 이벤트와 Timeline을 동시에 중복 호출하지 않도록 단일 소유자를 정하세요.
- 왼손 은메달, 군번줄 변화, 1인칭 고개 움직임, 오른손 권총, 8초 전장, 침실 각성은 이번에 구현하지 않았습니다.

## 외부 에셋 및 LFS 후보

사용 중:
- Assets/3D Boxing Arena Pack/Prefabs/Complete Props/Boxing Ring .prefab
- Assets/Gamemaster Audio - Pro Sound Collection/Backgrounds/background_crowd_people_chatter_loop_01.wav
- Assets/S03Prototype/Fonts/S03PromptFont.asset

추가 복원 제안만 한 파일 (실행하지 않음):
- Assets/Gamemaster Audio - Pro Sound Collection/Guns_Weapons/Guns/gun_pistol_shot_01.wav — LFS 포인터, 원본 288,044바이트
- Assets/Gamemaster Audio - Pro Sound Collection/Guns_Weapons/Bullets/bullet_flyby_01.wav — LFS 포인터, 원본 106,952바이트

합계 394,996바이트. 추후 승인 후 이 두 정확한 경로만 로컬 캐시 존재·해시를 확인하고 `git lfs checkout`하는 범위를 제안합니다. 총성 후보가 실제 육상 출발 권총에 적합한지는 청취 후 결정해야 합니다. 이번 작업에서 LFS 복원·네트워크 다운로드는 실행하지 않았습니다.
