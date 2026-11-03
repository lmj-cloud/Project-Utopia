# S13 검증 기록

## 1. 현재 상태

2026-11-01 / Unity 6000.3.21f1 / URP17.3.0 / Cinemachine3.1.7 / Timeline1.8.12 / feature/jungwoo.

실제 AssetDatabase 생성에서 **S13_BUILD_PASS**, GUI Editor/D3D11의 실제 Play에서 최종 **S13_VALIDATION_PASS … InputSystem E-X-E path**를 확인했습니다. GUI 검사는 정상 종료되어 Play 모드를 벗어났습니다. 배포 빌드/DX12/다른 플랫폼은 미검증입니다.

로그: `C:/Users/djdjd/.codex/visualizations/2026/10/28/01a180e6-40a1-7490-802b-6f31bb333c7f/S13-GUI-validation.log`

| 항목 | 실제 결과 |
|---|---|
| 독립 3인칭 시작 | 테스트 씬·출구 거리·플레이어 참조 통과 |
| 렌더링 소유권 | 활성 Camera1개, AudioListener1개, 실제 출력 카메라 이동/시네마틱 소유권 통과 |
| 이식용 Prefab | 테스트 Player/Main Camera/AudioListener 참조 없는 구조 확인 |
| URP | 실제 URP 활성, 카메라 post-processing와 Volume layer 설정 통과 |
| 사운드 | 소스3개 실제 PCM 읽기 및 비무음 통과 |
| Timeline | 문 개방·백색 증가·차폐·완료4개 Cue와 단일 완료 이벤트 통과 |
| 백색 변화 | 증가 구간 monotonic alpha, 완전 차폐 유지, 완료 콜백에서 white=true 통과 |
| 실제 Game 출력 | GUI 백버퍼 읽기, 내부 모든 픽셀 백색. 가장자리4px 이내 RGB20 이하 readback 여백만 허용. TECHNICAL.md 9. 참고 |
| 중복 방지 | 실행 중/완료 후 재시작 거부 통과 |
| 정상 완료 | 입력·가상 카메라·문·광량·Volume·기존 환경음·시간척도 복원, 백색은 명시적 해제까지 유지 |
| 취소 | 백색 증가 도중 Cancel, 실제 카메라 복귀 블렌드 포함 통과 |
| 예외 | 백색 증가 중 S13_EXPECTED_FAULT 및 시작 콜백 S13_EXPECTED_START_FAULT에서 복구, 잘못된 완료 없음 |
| 타임아웃 | realtime watchdog0.15초 주입으로 초반 중단 및 복구 통과 |
| 비활성화 | S13 컴포넌트 비활성화 시 복구, 공통 Sequence 비활성 시 시작 거부 |
| 공유 소유권/잠금 | 다른 DirectionSequence 실행 중 시작 거부, 기존 ControlsLocked=true 보존 통과 |
| 외부 잘못된 참조 | 출력 Camera 누락 시 시작 거부 통과 |
| E/X 입력 경로 | 임시 InputSystem Keyboard 이벤트로 Update의 E 시작 → X 취소 → E 재시도 → 두 번째 정상 완료 통과. 테스트 장치는 종료 시 제거 |
| 보호 대상 | 기존472개 파일 SHA-256 변화 없음. 변경은 S13 내부 신규 파일에 한정 |

검사 도중 의도적 예외2개와 watchdog 경고를 출력합니다. 해당 로그가 있다는 이유로 정상 플레이에서 같은 오류가 발생했다고 해석하지 않습니다.

## 2. 구분 원칙

- 구현 완료: 실제 파일 및 생성된 에셋에서 확인한 기능.
- 자동 검증 완료: 실제 Unity Play에서 assertion을 통과한 기능만 기록.
- 수동 확인: 직접 본 화면/동작만 기록.
- 미확인/미완성: 실행하지 못했거나 최종 품질이 준비되지 않은 부분.

## 3. 미검증 연출 품질

카메라의 자연스러움, 백색광/Bloom의 시각적 질감, 실제 벙커에서의 구도, 손·캐릭터 애니메이션, 최종 환경음과 믹스, 실제 PlayerController·본편 카메라·맵 통합, 크레딧과 엔딩 호스트, 씬 언로드 중 화면 소유권은 별도 확인이 필요합니다. 실제 물리 키보드 E/X와 조작감은 InputSystem 이벤트 주입 검사와 구분하며 사용자 수동 확인이 필요합니다.

## 4. 실제 화면 관찰과 캡처 한계

GUI Game 화면에서 테스트 플레이어·벙커·문·출구 카메라와 백색 차폐를 관찰했습니다. 이는 미적 완성도 승인이나 사용자의 수동 확인을 대신하지 않습니다.

최초 검사는 Camera.Render용 Canvas 변환이 Overlay 최종 출력과 다른 회색 결과를 내어 실패했습니다. 배치 모드의 ScreenCapture에는 Game 백버퍼가 없어 실패했으며 이를 통과로 취급하지 않았습니다. 최종 검사는 GUI Game 백버퍼를 사용합니다. 현재 분수 DPI의 pixelRect889.67×484.33 / 백버퍼890×484에서 하단 RGB16의 여백3행을 확인했습니다. 검사 허용 범위는 가장자리4px 이내의 어두운 여백이며, 내부 비백색은 허용하지 않습니다. 전체 백버퍼가 RGB255라고 주장하지 않습니다.

## 5. 스토리와 미완성

확정 스토리보드 없음. 기존 확장 제안만 발견했습니다. 벙커 출구→백색 엔딩 이벤트 이상의 의미·대사·무기 발사·후속 사건을 구현하지 않았습니다. 아트·캐릭터 애니메이션·최종 폴리/믹싱·본편/엔딩 호스트 통합은 미완성입니다.

## 6. 최종 저장 에셋과 프로젝트 보호 감사

S13_PREFAB_SCRIPT_AUDIT_PASS missing=0 확인. 두 저장 Prefab의 모든 자식에서 Missing Script0개입니다. 컴파일/도메인 재로드 중 Unknown Behaviour Missing Script 경고1회를 관찰했으나 직접 원인은 미확인입니다. 저장된 S13 Prefab 감사는0개였으며, Console 전체가 경고 없이 깨끗하다고 주장하지 않습니다. 기존 Cinemachine HDRP sample asmref 경고도 기존 설정을 수정하지 않았습니다.

S13 내부61개 파일(내용27개 + 파일/하위 폴더 meta34개)을 생성했습니다. 기존 보호472개 SHA-256 변화0, 브랜치feature/jungwoo 유지, staged변경없음을 확인했습니다. Commit/Push/Merge/브랜치 전환/외부 원본 수정/LFS 복원을 수행하지 않았습니다.
