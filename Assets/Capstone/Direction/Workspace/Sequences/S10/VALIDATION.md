# S10 운전석 개정 검증 기록

## 1. 최종 실제 실행 결과

2026-11-01 / Unity6000.3.21f1 / D3D11 실행 옵션 / feature/jungwoo.

최종 **S10_REBUILD_PASS**, **S10_DRIVER_VALIDATION_PASS**, **프로세스 종료 코드0**을 확인했습니다. 이전 외부 시점의 검증을 이번 운전석 수정 버전의 결과로 대신하지 않았습니다. ProjectSettings의 그래픽 API를 바꾸지 않고 -force-d3d11로 실행했습니다. 이번 버전의 DX12/배포 빌드는 미검증입니다.

최종 로그:
`C:/Users/djdjd/.codex/visualizations/2026/10/28/01a180e6-40a1-7490-802b-6f31bb333c7f/S10-driver-release-validation.log`

## 2. 자동 검사 항목

| 항목 | 실제 결과 |
|---|---|
| Builder 반복 실행 | 같은 에셋을2회 Upgrade 후 GUID 동일, 재질 수 증가 없음, 이후 1인칭 Play 정상 |
| 시간 적용 | 실제 Timeline 길이 갱신/원상 복구와 GUID 유지. 총길이가 같은 미적용 시간 재배분도 거부 |
| S09 입력 | 탑승 미완료 거부, 입력 복사, 시작1회, 중복 실행 차단 |
| 실제 카메라 | CinemachineBrain의 CM_S10_DriverFirstPerson 활성 확인. 활성 시퀀스의 모든 관찰 프레임에서 운전석 앵커로부터0.27m 이내 |
| 가족 배치 | 운전석x<0, 아내 조수석x>0, 두 아이 임시 메시가 뒤쪽z<0인 참조/배치 확인 |
| 트럭 실사용 | realTruckUsed=true 및 MeshFilter의 vertexCount/에셋 경로가 S10의 실제 FBX 사본임을 확인. 블록아웃 Mesh가 아님 |
| 트럭 접근 | 전방Z 감소, 카메라 시야 내 투영, 정상/접근 캡처 |
| 충돌 | 실제 onImpact 시점의 충돌 AudioSource·점광원·파편 활성 확인 |
| 충돌 뒤 시야 | 붉은 UI와 Volume weight 활성, 앞유리 균열 활성, road LowPass2kHz 미만 |
| 눈 감김 | 첫 완전 감김→재개방, 두 번째 완전 감김→재개방, 세 번째 완전 검정 순서 확인 |
| 시간 증가 | 두 번째 감김/유지/열림이 첫 번째보다 길고, 마지막 감김/유지가 두 번째보다 긴 설정 확인 |
| 이명 증가 | 첫 재개방 이후약0.08734 → 두 번째 이후약0.14735 → 마지막 감김 말미0.18로 증가. 관찰 프레임에서 설정 상한/코드상0.25 제한 초과 없음 |
| 마지막 검정 | 세 번째 뒤 자동 재개방 없음. 최종1280×720 합성 캡처의 모든 픽셀이 RGB 각2/255 이하 |
| S11 이벤트 | 실제 onS11Ready 콜백 시 알파1, 기존 Adapter 출력 각각1회, unconscious/ requiresBlackStart=true |
| 독립 완료 | 완료0.8초 후에도 검정 유지, 완료 후 중복 시작 거부. 명시적 ResetStandalonePreview에서 전체 시험 상태 복구 |
| 중간 취소 | 접근 및 충돌 직후 취소, 완료 이벤트 없이 복구 |
| 부분 예외 | 충돌 소리·파편·조명을 켠 뒤 S10_EXPECTED_IMPACT_FAULT 주입, 복구 및 완료 없음 |
| 시퀀스 타임아웃 | watchdog0.2초로 강제 축소해 취소/복구 |
| 비활성화 | 활성 S10 컴포넌트를 끄면 공통 Sequence와 자원 복구 |
| 통합 암전 생존 | DontDestroyOnLoad Relay의 완전 검정 확인. S10 루트/카메라/원본 오버레이를 실제로 삭제한 뒤에도 검정 유지 |
| 잘못된 수신자 | null·투명·부분 크기·자기 자신의 Relay/소스 이미지를 수신자로 주면 거부 |
| 유효 수신자 | 별도 전체 화면 검정 수신자를 승인하고 최소2프레임 뒤 Relay 제거. 수신자 검정은 유지 |
| 대기 타임아웃 | 소스 삭제 후에도 대기 시간이 지나면 검정 유지. 소스가 남아 있을 때도 준비 안 된 S11을 노출하지 않음 |
| 명시적 대기 취소 | 타임아웃 이후 Cancel을 호출하면 암전 소유권 해제/상태 복구 |
| 공통 복구 | 차량 Transform, 카메라 포즈/Priority, 입력 enabled/ControlLock, 검정/붉은 UI, 균열, Volume, 조명/필터, 음원/고정 파편 복구 |
| 시간척도 | timeScale0.7 / fixedDeltaTime0.014에서 재생 후 그 값으로 복구 확인. 검사 종료 시 원래 값 복구 |

기본 정상 시퀀스는 저장된 실제 시간으로 재생했습니다. 통합 인계/대기 실패 검사는 메모리 전용의 약3.35초 단축 설정과 Timeline을 만들고 기존 Signal/Receiver를 연결했습니다. 저장된 S10Timing/Timeline/Signal을 단축 버전으로 바꾸거나 S11 씬을 만들지 않았습니다.

검사 중 미적용 설정, S10_EXPECTED_IMPACT_FAULT, watchdog/인계 타임아웃 경고는 의도한 출력입니다. 최종 로그에서 그 밖의 S10 C# 컴파일 오류/예외는 확인되지 않았습니다. 모든 엔진/다른 시스템 오류를 검증한 결과는 아닙니다.

## 3. 캡처와 수동 확인

로그와 같은 프로젝트 외부 폴더에 다음 PNG를 저장했습니다.

1. S10-driver-normal.png — 운전대/정상 시야/조수석 가족 단서
2. S10-driver-approach.png — 앞유리 너머 실제 트럭 접근
3. S10-driver-impact.png — 근접 트럭·균열·충격
4. S10-driver-red.png — 붉은 시야와 흐림
5. S10-driver-final-black.png — 마지막 완전 검정

캡처는 실제 실행 상태의 카메라를1280×720 RT로 렌더하고, 오버레이 Canvas를 잠시 카메라 모드로 합성한 뒤 원래 상태로 되돌리는 방식입니다. 편집기 창 전체 스크린 녹화는 아닙니다. 합성 시 디버그 UI도 후처리의 영향을 받을 수 있어 실제 ScreenSpaceOverlay Game 표시와 세부 흐림은 다를 수 있습니다.

직접 평가가 필요한 부분:

1. 운전감·조수석 시선 이동·머리 충격·실신의 자연스러움과 편안함
2. 가족 임시 메시와 실제 손/운전자 애니메이션. 현재 운전대를 잡는 실제 손 리그는 없음
3. 실제 거울 반사. 현재는 정적 실루엣과 조수석 시선 단서만 있음
4. 충돌·유리·이명·호흡/가족 소리의 최종 청취 미감. 현재는 기존 합성 음원9개, 대사 없음
5. E/X 실제 키 조작·포커스, 다양한 해상도/GPU, 실제 빌드
6. 실제 S09→S10→S11 비동기 씬 로딩/본편 PlayerController/오디오 버스. 이번에는 인터페이스와 독립 테스트 수신 화면만 검증
7. 임의의 모든 시간 조합/효과 토글/사용자 편집 이명 곡선. 기본 설정 외 조합은 별도 검증 필요
8. 정상 기존 재생 중 오디오의 샘플 정밀 인계. 이번 소유 음원은 시작 시 정지였으므로 종료 후 정지 상태를 확인함

## 4. 보존과 미구현

기존 S10 .meta GUID47개 전부 동일, 새 .unity 생성 없음. 원본 트럭은 여전히 LFS 포인터이며 변경하지 않았습니다. 로컬 캐시 사본3개의 SHA-256은 SOURCE.json과 일치합니다.

S03/S06/S07/공통 코드/팀원 스크립트·씬/Single_detailed_truck 원본/Packages/ProjectSettings/_Recovery 등 보호 파일514개의 SHA-256 비교에서 변경/누락0개를 확인했습니다. 변경·추가 프로젝트 파일은 S10 안에 한정됩니다. 브랜치 변경, LFS 정리/원본 복원, 스테이징·Commit·Push·Merge는 하지 않았습니다.

실제 물리 기반 차량 파괴, 최종 캐릭터/손/좌석 애니메이션, 최종 Foley/공간 음향, 실제 S11 병실/눈뜨기/씬 로더는 미구현입니다. 붉은 시야는 주관적 시각 효과이며 가족 부상 상태나 사고 원인을 확정하는 정보가 아닙니다.
## 6. 출력 카메라 고정 대응 검증 (2026-11-01)

Unity 6000.3.21f1 / D3D11 실제 Play 검사에서 rendering camera actually rotates across frames, rendering camera follows procedural pose 및 S10_DRIVER_VALIDATION_PASS를 확인했습니다. 실제 출력 Camera 회전 변화 5도 초과, 가상 카메라와 회전 오차 3도 미만/위치 오차 0.05m 미만을 검사했습니다. 기존 정상 종료·취소·예외·타임아웃·비활성화·암전 인계 검사도 통과했습니다.

로그: C:/Users/djdjd/.codex/visualizations/2026/10/28/01a180e6-40a1-7490-802b-6f31bb333c7f/S10-camera-validation.log

보호 대상 514개 해시 변화 없음. 사용자의 이전 화면 고정 원인은 단일 원인으로 재현/확정하지 못했습니다. Scene 뷰는 편집 카메라이므로 새 Play S10 in Game view 메뉴로 실제 Game 출력을 확인하세요. 카메라 자연스러움과 사용자 환경의 시각 품질은 직접 확인이 필요합니다.

## 7. 실제 Game 출력 충돌 원인 확정 및 수정 검증

현재 GUI Editor / DX12 / 2026-11-01에서 재현했습니다. 트럭 안 Camera(enabled=true, depth0)가 Main Camera와 동시 출력되어 트럭 전면으로 고정되어 보였습니다. S10 범위에서 트럭 내장 Camera를 비활성화한 뒤 일반 Play에서 운전대·도로·접근 트럭 화면을 직접 확인했고, 17.72초 종료와 최종 암전/Completed를 확인했습니다.

같은 GUI Editor에서 전체 자동 검사 S10_DRIVER_VALIDATION_PASS 확인. 신규 검사 FBX authoring cameras cannot overwrite Game view 및 Game display rendered only by intended output camera 통과. 취소·타임아웃·예외·비활성화·카메라/조작/오디오 복구·암전 인계 검사 통과. 보호514개 파일과 기존 GUID47개 변화 없음.

이전 Cancelled의 직접 계기는 미확정입니다. 이후 정상 재생에서는 재현되지 않았고, 중단 시 시간/길이/상태 기록을 추가했습니다. 앞선 Main Camera 단독 Render 캡처는 다른 Camera의 덮어쓰기를 검사하지 못했으므로 실제 Game 출력 정상 증거로 충분하지 않았습니다.

이번 로그: C:\Users\djdjd\.codex\visualizations\2026\10\28\01a180e6-40a1-7490-802b-6f31bb333c7f\S10-FBX-camera-fix-20261101-170717.log
