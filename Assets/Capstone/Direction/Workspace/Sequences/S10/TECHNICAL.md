# S10 기술 문서 — 운전석 1인칭 개정

## 1. 범위와 보존

작업 루트: Assets/Capstone/Direction/Workspace/Sequences/S10/

기존 Scenes/S10_FamilyAccident.unity, Prefabs/S10_TestRig.prefab, Timelines/S10_FamilyAccident.playable, Signal7개, S10Timing.asset을 재사용합니다. 기존 .meta GUID를 유지하고 Scene을 복제하지 않습니다. 씬은 기존 프리팹 참조를 통해 갱신됩니다. 외부 3인칭 사고 샷은 폐기했으며, 일반 카메라는 취소/명시적 시험 초기화용으로만 남깁니다.

Unity 6000.3.21f1 / URP17.3.0 / Cinemachine3.1.7 / Timeline1.8.12. 이번 검증은 D3D11 실행 옵션이며 ProjectSettings를 변경하지 않습니다. S11의 디스토피아 세계관 병실/침대/의료진/눈뜨기 장면은 없습니다.

## 2. 확정 좌석과 내부

- 주인공: 왼쪽 운전석. DriverSeatEye 로컬(-0.43,1.64,-0.17), 기본pitch3도. 자기 머리 메시를 숨겨 얼굴 안쪽이 보이지 않게 합니다.
- 아내: 오른쪽 앞 조수석 Wife_FrontPassenger.
- 아이들: Child_RearLeft / Child_RearRight. 임시2명이며 최종 인원/나이/성별/외형은 미확정입니다.
- DriverInterior: 운전대, 대시보드, 투명 앞유리, 지붕, ImpactCracks, 거울 가족 실루엣.
- 거울 실루엣은 정적 임시 표현이고 실제 반사/실시간 카메라가 아닙니다. 정상 주행 초반 실제 카메라가 조수석으로 잠깐 돌아갑니다.

구체적 대사·부상·충돌 원인은 추가하지 않습니다. 붉은 화면은 주인공의 주관적 시야이며 혈액/신체 손상 효과가 아닙니다.

## 3. 프로젝트 트럭의 실제 사용

원본 후보: Assets/Single_detailed_truck/3dobjects/truck.fbx, 같은 폴더의 Materials/truck-*.mat, Prefabs/Truck.prefab.

원본 FBX와 BMP는 LFS 포인터입니다. git lfs checkout/pull을 실행하거나 원본을 수정하는 대신, 이미 존재하는 .git/lfs/objects에서 일치하는 SHA-256 데이터만 읽어 S10 전용 사본으로 만들었습니다.

| 사본 경로 (S10 기준) | 원본 | 실제 데이터 크기 |
|---|---|---:|
| Resources/TruckFromLocalCache/truck.fbx | Single_detailed_truck/3dobjects/truck.fbx | 12,235,824 bytes |
| Resources/TruckFromLocalCache/truck_color_01.bmp | Single_detailed_truck/Textures/truck_color_01.bmp | 12,582,966 bytes |
| Resources/TruckFromLocalCache/wheels.bmp | Single_detailed_truck/Textures/wheels.bmp | 3,145,782 bytes |

SOURCE.json에 원본/사본 경로와 전체 SHA-256을 기록했습니다. FBX SHA-256은 c5eea8327d74553bc7e9f2d98be1756569304dc09b022bd5721bdaa97cd3a3b4입니다. 네트워크 다운로드와 LFS 정리/원본 복원은 하지 않았습니다.

사본 FBX는 PrefabUtility.InstantiatePrefab으로 기존 truck 루트 아래 ImportedSingleDetailedTruck으로 배치합니다. URP 재질도 S10/Materials/Imported_*.mat로 새로 만들어 원본 Standard 재질을 수정하지 않습니다. 폭2.6m, 높이약3.12m로 정규화했고 앞부분을 로컬Z3.8에 정렬했습니다. wheel_front/wheels_back 위치를 비교해 앞뒤를 판별하고, 루트Y180도로 운전석 정면을 향하게 합니다. 전방Z48→15→6으로 접근하며 최종 앞부분은 약Z2.2로 승용차 앞부분에 도달합니다. 실제 Rigidbody 충돌이 아닌 시네마틱 보간입니다.

이는 프로젝트에 이미 임포트된 Single_detailed_truck 모델의 실제 데이터 사용입니다. 원본 프리팹은 포인터 메시를 참조해 직접 사용할 수 없어 사본 모델 인스턴스를 사용합니다. Asset Store의 현재 가격/계정 라이선스를 별도로 조회한 것은 아닙니다.

## 4. 파일과 컴포넌트 역할

| 파일 | 역할 |
|---|---|
| Scripts/S10Timing.cs | 기존 구간 시간 + 두 번의 닫기/유지/열기/휴지, 마지막 닫기/유지, 이명 곡선/상한, 적용 서명 |
| Scripts/S10CrashSequence.cs | 기존 S09 입력, 공통 Sequence 수명, 차량/조명/음향/고정 파편, onImpact, S11 완료, 취소/복구 |
| Scripts/S10DriverEffects.cs | 운전석 카메라, 조수석 시선 이동, 붉은 UI/URP/흐림, 균열, 눈 주기, 이명 증가, 원상 복구 |
| Scripts/S10BlackoutRelay.cs | 다음 장면 준비까지 유지되는 DontDestroyOnLoad 검정 Canvas, 수신자 검증,2프레임 인계, 대기 타임아웃 |
| Scripts/S10StoryAdapter.cs | 기존 ReceiveS09 / onHospitalRequested / LastHospitalState. ConfirmDestinationBlack 추가 |
| Editor/S10WorkbenchBuilder.cs | 기존 생성·열기·시간 적용 메뉴. 기존 파일은 Driver Upgrade 후 사용 |
| Editor/S10DriverUpgrade.cs | 기존 프리팹만 수정, 좌석/내부/실제 트럭/카메라/Volume 설치. 반복 적용 시 재질 중복 방지 |
| Editor/S10Validation.cs | 실제 Play 정상 흐름·복구·반복 눈 감김·암전 인계·소스 삭제/실패 검사 |

기존 DirectionSequence/DirectionScreen/S03ControlLockAdapter/S03FirstPersonPlayer/안내 글꼴은 수정 없이 참조합니다. 공통 Sequence.screen=null로 두고 S10 이미지의 흰 충격·반복 검정은 전용 코드가 제어합니다. 공통 Sequence가 카메라 Priority 및 입력 컴포넌트 enabled 상태를 저장/복구합니다. ControlLockAdapter는 본인 소유 잠금만 해제합니다.

## 5. 카메라·시각·음향

CM_S10_DriverFirstPerson은 DriverSeatEye 기준으로만 움직입니다. 활성 중 외부 카메라 경로로 이동하지 않습니다. 시작 시 Cinemachine 블렌드를 Cut으로 확보하고 원래 블렌드를 복구합니다. 기본FOV72→위험 시68, nearclip0.025m. 정상 주행 진동약0.003m. 초반 조수석 시선은 최대38도이고 트럭 접근 시 정면으로 돌아옵니다.

충돌 때 차체 반동과 머리의 상대 전진약0.18m, pitch약2.3도/roll0.7도 뒤 감쇠 진동을 적용합니다. 테스트에서 모든 활성 구간의 카메라가 운전석 기준0.27m 안에 있는지 검사합니다.

ImpactCracks와 고정 파편16개를 사용하며 대량 물리 오브젝트를 생성하지 않습니다. 충격 점광원과 짧은 흰 섬광 뒤 붉은 시야를 적용합니다. 붉은 Image는 알파0.05→0.18로 시야를 남기고, Volume의 red colorFilter(1,0.5,0.43), saturation-15, 붉은 vignette0.52와 Gaussian DepthOfField(start0.25,end2.5,radius1.25)를 점점 강화합니다. Camera의 URP depth texture 요청은 S10 카메라에만 설정했습니다. HDRP/ProjectSettings 변경 없음.

기존 TEMP_*.wav9개를 그대로 사용합니다. 이명은 Tinnitus Growth와 Tinnitus Maximum으로 조절하며 실제 volume은0~0.25로 제한합니다. 기본 maximum0.18, 성장 키값은 0→0.12 / 0.35→0.35 / 0.7→0.7 / 1→1입니다. 앞 두 번의 재개방 뒤에도 증가하며 마지막 완전 검정에 도달할 때 정점, Black Hold 동안 부드럽게0으로 감쇠합니다. 주변 엔진/도로/실내/트럭 소리는 점차 멀어지고 road LowPass는22kHz→700Hz로 낮아집니다.

합성 음원은 최종 충돌/경적/유리/이명 사운드가 아닙니다. 사운드 청취 미감·차량 내부 차음·3D 공간 음향·Mixer 통합은 미검증/후속 작업입니다.

## 6. Timeline과 눈 감김 데이터

기존 Signal7개와 GUID를 유지합니다. UnscaledGameTime으로 재생하며 수동 Evaluate는 하지 않습니다. Apply timing to Timeline이 marker.time과 fixedDuration을 같은 에셋에 갱신합니다. 총길이가 같은 재배분도 적용 서명으로 누락을 검출합니다.

| 기본 시점 | 신호/동작 |
|---|---|
| 0 | 운전석 시작 / 짧은 조수석 시선 |
| 3.00 | Approach |
| 6.00 | Danger |
| 7.50 | Impact |
| 7.75 | Confusion / 이명 시작 |
| 10.75 | 기존 Blackout 신호: 첫 번째 눈 감김 시작 |
| 10.97~11.25 | 첫 번째 완전 감김0.28초 |
| 11.25~11.67 | 첫 번째 눈뜨기0.42초 |
| 12.12~12.67 | 두 번째 감김0.55초 |
| 12.67~13.52 | 두 번째 완전 감김0.85초 |
| 13.52~14.47 | 두 번째 눈뜨기0.95초 |
| 15.02~16.52 | 세 번째 마지막 감김1.50초 |
| 16.52 | 기존 Covered 신호, 완전 검정 |
| 16.52~17.72 | 마지막 검정 유지1.20초 / 이명 감쇠 |
| 17.72 | 기존 Finish 신호, S11 요청 |

첫 닫기0.22초보다 두 번째0.55초가 길고, 마지막1.5초가 더 깁니다. 유지 시간도0.28→0.85→1.2초입니다. 재개방은 두 번만 있으며, 세 번째 뒤에는 재개방 커브/신호가 없습니다.

## 7. 완료·복구와 독립 모드

SequenceCompleted는 완전 검정·충돌1회·완료 신호를 확인한 뒤 기존 자원 스냅샷을 복구합니다. 차량/카메라/입력/시간척도/조명/Volume/붉은 화면/균열/오디오는 복구하고, 검정 화면만 남긴 상태에서 onS11Ready를 한 번 호출합니다. Completed 이후 E 중복 실행은 거부합니다.

독립 시험에서도 마지막 검정을 자동 해제하지 않습니다. 명시적 ResetStandalonePreview 또는 Play 종료/재진입으로 시험 화면을 초기화합니다. 이것은 다음 장면의 눈뜨기가 아닙니다. X 취소, 전체 시퀀스 watchdog40초, 충돌 도중 주입/실제 예외, 컴포넌트 비활성화는 기존 상태로 복구합니다. 공통 보조 타임아웃은180초입니다.

## 8. S09 입력과 S11 암전 인계

기존 S10DriveState / BeginFromS09 / S10StoryAdapter.ReceiveS09 계약은 유지합니다. S10HospitalState / onS11Ready / onHospitalRequested도 유지하고 unconscious=true, requiresBlackStart=true를 추가했습니다. 사고 원인/가족 부상 필드는 추가하지 않습니다.

통합에서는 autoStartStandalone=false, integrationMode=true로 설정하고 실제 S09 준비 이벤트를 Adapter에 연결합니다. 실제 플레이어/입력/카메라/음향 참조를 연결하는 것은 별도 통합 작업입니다.

완료 시 S10BlackoutRelay가 자체 검정 Canvas를 DontDestroyOnLoad로 생성합니다. S10 원본/카메라/오버레이가 삭제돼도 이 Canvas는 남습니다. 로더는 onHospitalRequested에서 LastHospitalState를 독립 저장하고, 자기 소유 입력 잠금과 S11 로딩을 담당합니다. S10은 자기 잠금만 복구하므로 로딩 중 입력 잠금은 수신 측 소유여야 합니다.

수신용 Image가 준비되면 아래 계약을 따릅니다.

1. S10/Relay와 별개의 Canvas 루트 오브젝트에 Image를 직접 자식으로 둡니다.
2. Display0의 ScreenSpaceOverlay, 전체 화면 Stretch/offset0, scale1/rotation0, sprite없음/기본UI재질, 부모 CanvasGroup alpha1을 사용합니다.
3. Image의 색을 검정/alpha1로 유지합니다.
4. S10StoryAdapter.ConfirmDestinationBlack(image) 또는 정적 S10BlackoutRelay.TransferToReadyDestination(image)를 호출합니다. 소스가 파괴된 경우에도 정적 API를 사용할 수 있습니다.
5. 반환 true는 접수이지 즉시 해제 완료가 아닙니다.2프레임 뒤에도 유효하면 소스 검정을 해제하고 Relay를 제거합니다.
6. S10BlackoutRelay.Current가 null이 될 때까지 수신 화면은 검정이어야 합니다. 이후 S11이 소유한 눈뜨기를 구현합니다. 이번에는 그 장면을 만들지 않았습니다.

대기 타임아웃 기본30초는 원본이 있을 때 onHandoffTimeout으로 실패를 알리고, 삭제된 경우에도 Relay.TimedOut 상태를 남기지만 **검정을 자동으로 해제하지 않습니다**. 원본이 없어져도 그대로 유지합니다. 유효한 수신 화면으로 넘기거나 명시적 CancelPending/원본의 X 취소로 작업자가 실패 복구를 선택합니다. 타임아웃만으로 준비되지 않은 새 장면이 노출되지 않습니다.

## 9. 검증 범위와 남은 작업

기본 정상 시퀀스는 실제 길이로 검사합니다. 인계/로딩 실패 검사는 프로젝트 에셋을 바꾸지 않는 메모리 전용 단축 Timeline/설정을 사용합니다. 실제 S11 씬을 생성하거나 로딩한 검사는 아닙니다. 상태와 완전 검정 합성 캡처 검사는 자동화했고, 움직임·손상 표현·사운드의 자연스러움은 별도 수동 평가입니다. 최신 실행 결과는 VALIDATION.md, 실제 파일 변경은 FILES.md에 기록합니다.
## 10. FBX 내장 카메라 출력 충돌 수정

실제 GUI/DX12 실행에서 ImportedSingleDetailedTruck 아래 Camera(enabled=true, depth0)가 Main Camera와 함께 렌더링하여 트럭 전면이 화면을 덮었습니다. Main Camera의 좌표만 검사하거나 Camera.Render로 단독 캡처하면 발견하지 못합니다. S10DriverEffects.Awake에서 트럭 자식 Camera만 비활성화하며 종료/취소 뒤에도 내장 카메라는 켜지지 않습니다. InstallRealTruck도 같은 비활성 상태를 Prefab에 저장합니다. 외부 FBX/원본 Importer는 수정하지 않습니다.

S10PlaybackDiagnostics는 메뉴 Toggle playback diagnostics를 켰을 때만 일반 자동 시작을 관찰합니다. S10_DIRECTOR_INTERRUPTED는 중단 시 director 시간/길이/상태를 기록합니다. 기존 중단 사례의 직접 원인은 미확정이며 새 일반 재생에서는 Completed까지 실행됐습니다.
