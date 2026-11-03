# S10 운전석 1인칭 정면충돌 / 의식 소실

## 1. 기존 씬 열기와 실행

1. Unity 6000.3.21f1에서 컴파일/임포트가 끝날 때까지 기다립니다.
2. 다른 씬에 미저장 변경이 있으면 직접 저장 여부를 결정합니다. 도구는 자동 저장하거나 변경을 버리지 않습니다.
3. 상단 **Capstone → Direction → S10 Workspace → Open test scene**을 클릭합니다.
4. 기존 **S10_FamilyAccident** 씬에서 ▶ Play를 누릅니다. 임시 S09 준비 데이터로 자동 시작합니다.
5. 운전석에서 짧게 조수석을 보고 정면으로 돌아온 뒤 트럭 정면충돌 → 붉은 시야 → 눈 감김/재개방 → 최종 암전을 봅니다. 기본 약17.72초입니다.
6. 재생 중 X 취소, 취소 후 E 재시도입니다. Game 창을 선택해야 키 입력을 받습니다. 종료는 Ctrl+P 또는 상단 Play 버튼입니다.

**정상 완료 후 계속 검정인 것이 정상입니다. 마지막 세 번째 눈 감김 뒤 자동으로 다시 열지 않습니다.**

## 2. 가족 좌석과 리소스

주인공은 운전석, 아내는 앞 조수석, 아이들은 뒷좌석입니다. 아이들 메시2개는 인원/나이/성별을 확정하는 최종 모델이 아닌 임시 배치입니다. 초반 조수석 시선 이동과 거울의 임시 가족 실루엣으로 동승자를 암시합니다. 거울은 아직 실시간 반사/RenderTexture가 아닙니다.

트럭은 프로젝트의 Single_detailed_truck 실제 FBX를 사용합니다. 원본이 LFS 포인터이므로 로컬 캐시에서 SHA-256을 확인한 동일 데이터 사본을 S10/Resources/TruckFromLocalCache에 두고 인스턴스화했습니다. 외부 원본과 LFS 상태를 변경하지 않았습니다.

## 3. 시간과 이명 조절

1. S10 폴더의 **S10Timing.asset**을 선택합니다.
2. 기존 Normal Drive / Truck Approach / Danger / Impact / Confusion은 유지합니다.
3. Blink1 Close/Hold/Open/Rest와 Blink2 Close/Hold/Open/Rest로 앞의 두 번을 조절합니다.
4. **Fade To Black은 세 번째 마지막 눈 감김 시간**, **Black Hold는 마지막 완전 검정 유지 시간**입니다. 마지막 재개방 설정은 없습니다.
5. 두 번째 감김/유지/열림은 첫 번째보다 길게, 마지막 감김/유지도 두 번째보다 길게 유지해야 유효합니다.
6. 시간을 바꿨다면 Play를 끄고 **Apply timing to Timeline**을 실행합니다. 미적용 설정은 시작을 거부합니다.
7. **Tinnitus Growth**는 충돌 뒤부터 마지막 완전 검정까지의 증가 곡선입니다. **Tinnitus Maximum**은 기본0.18이며 코드에서0.25 이하로 제한합니다. 마지막 검정 유지 중에는 부드럽게 감쇠합니다. 이 두 음향 값만 바꾸면 Timeline 재적용은 필요 없습니다.
8. 전체 길이를40초 이상으로 늘리면 S10_Direction → S10CrashSequence → Watchdog Seconds도 여유 있게 조절합니다.

현재 효과음은 합성 테스트 음원이며 최종 사운드가 아닙니다.

## 4. 독립 테스트의 완료와 초기화

독립 모드(Integration Mode 꺼짐)도 마지막에는 검정을 유지합니다. 차량·카메라·입력·조명·음향·시간척도는 복구하지만 화면만 자동 해제하지 않습니다.

다시 보려면 Play를 종료하고 재진입하세요. Play를 유지한 채 시험 화면을 되돌리려면 Hierarchy의 S10_TEST_ONLY → S10_Direction을 선택하고 S10CrashSequence 컴포넌트의 점3개 메뉴에서 **Reset standalone preview (explicit operator action)**를 실행한 뒤 Game 화면에서 E를 누릅니다. 이는 작업자의 명시적인 시험 초기화이며 S10 스토리 안의 눈뜨기 장면이 아닙니다.

## 5. 실제 통합 모드

S10CrashSequence의 Integration Mode를 켜면 완료 때 별도 영속 검정 Canvas가 생성됩니다. S10 오브젝트/씬을 제거해도 검정은 남습니다. 실제 로더는 S11과 독립된 수신용 전체 화면 검정 Image를 준비한 뒤 S10StoryAdapter.ConfirmDestinationBlack 또는 S10BlackoutRelay.TransferToReadyDestination을 호출합니다.

수신 화면은 Display0 / ScreenSpaceOverlay / Canvas 직속 / 전체 Stretch·offset0 / scale1·rotation0 / 기본 재질·sprite없음 / 불투명 검정이어야 합니다. 자기 자신의 S10/Relay 화면을 수신자로 넘길 수 없습니다. 승인 후2프레임을 기다려 인계합니다. Current가 없어지기 전에는 수신 화면을 밝히지 마세요.

로딩 대기 타임아웃도 검정을 자동 해제하지 않습니다. 원본이 남아 있으면 onHandoffTimeout으로 실패를 알리고, 원본이 삭제된 뒤에는 로더가 Relay의 TimedOut 상태를 확인합니다. 안전한 수신 화면 또는 명시적 취소를 기다립니다. 원본이 남아 있다면 X로 대기를 명시적으로 취소할 수 있습니다. 실제 S11의 디스토피아 세계관 병실/눈뜨기/씬 로더는 이번에 만들지 않았습니다.

## 6. 생성 도구와 자동 검사

- **Generate first prototype**: 기존 결과가 있으면 같은 파일을 Upgrade하고 엽니다. 중복 씬을 생성하지 않습니다.
- **Upgrade existing to driver POV**: 기존 프리팹·설정·Timeline을 1인칭 버전으로 갱신합니다. GUID를 유지하며 예전 외부 카메라 구도로 되돌리지 않습니다.
- **Run Play validation**: 실제 Play에서 정상 흐름, 눈 감김, 이명, 복구, 암전 인계를 검사합니다. 검사 종료 후 S10_DRIVER_VALIDATION_PASS/FAIL을 봅니다.

검사 중 S10_EXPECTED_IMPACT_FAULT, 타임아웃, 미적용 설정 경고는 의도한 출력입니다. 최신 결과와 미확인 품질은 VALIDATION.md를 참고하세요.
## 7. 카메라 화면이 고정되어 보일 때

상단 Capstone → Direction → S10 Workspace → Play S10 in Game view를 실행합니다. 기존 테스트 씬을 열고 Game 탭에 초점을 맞춘 뒤 Play를 시작합니다. Scene 탭은 편집용 시점이므로 연출 카메라를 따라가지 않습니다. 정상 완료 후 암전은 유지되므로 다시 보려면 Play를 종료하고 재실행합니다.

S10DriverEffects는 컷신 중 CinemachineBrain을 ManualUpdate로 소유하고 LateUpdate에서 실제 출력 카메라를 갱신합니다. 기존 갱신 방식, IgnoreTimeScale, 블렌드 및 Solo 선택은 종료/취소 때 복원합니다. 사용자 환경에서 고정된 단일 원인은 미확인이며, 출력 카메라 동작 검사를 추가했습니다.

## 8. 트럭 전면으로 고정되던 실제 원인 수정

트럭 FBX의 내장 Camera가 Main Camera와 같은 Display를 렌더링하던 문제가 확인되었습니다. S10 런타임과 생성 코드에서 해당 내장 카메라를 비활성화했고 기존 Prefab에도 반영했습니다. 별도 수동 카메라 설정 없이 기존 씬에서 Play를 다시 시작하면 됩니다. 종료 후 검정 화면은 의도된 최종 암전입니다.
