# S13 벙커 출구·백색 엔딩 프로토타입

## 1. 기획 범위

정확한 S13 스토리보드는 프로젝트에서 찾지 못했습니다. `Assets/Capstone/Direction/Documentation/Implementation-KO.md`의 S13 행은 확장 제안이며 확정 스토리가 아닙니다. 이번에는 출구 상호작용 → 문 개방 → 출구로 이동하는 카메라 → 백색 화면 → 엔딩 완료 이벤트만 구현합니다. 대사·결말의 의미·발사·사망·엔딩 이후 사건을 추가하지 않습니다.

## 2. Unity에서 실행

1. Unity 6000.3.21f1에서 스크립트 컴파일이 끝날 때까지 기다립니다.
2. 다른 씬에 저장하지 않은 작업이 있다면 직접 처리합니다. S13 도구는 임의로 저장하거나 버리지 않습니다.
3. 상단 **Capstone → Direction → S13 Workspace → Play test in Game view**를 클릭합니다.
4. Game 화면을 클릭합니다. 시작 위치는 문 상호작용 거리 안입니다.
5. **E**를 눌러 시작합니다. **WASD + 마우스**로 이동·시점을 조절할 수 있습니다. 문에서 멀어졌다면 다시 가까이 갑니다.
6. 문 열림, 카메라 전진, 약 3초의 백색 증가, 최종 백색 화면을 확인합니다. 시작 후 기본 약 6.4초입니다.
7. 재생 중 **X**로 취소합니다. 다시 **E**로 재시도할 수 있습니다.
8. Play를 종료하려면 상단 ▶ 또는 **Ctrl+P**를 누릅니다.

최종 백색 화면은 의도적으로 유지됩니다. 다시 보려면 Play를 종료하고 재시작하거나, 실행 중 `S13EndingSequence` 컴포넌트의 ⋮ 메뉴에서 **Reset standalone preview**를 실행합니다. 정상 종료 후 자동으로 다른 씬을 열지 않습니다.

## 3. 씬 열기와 생성

- **Open test scene**: 기존 `Scenes/S13_BunkerExit.unity`를 엽니다.
- **Generate independent prototype**: 처음 생성할 때 사용합니다. 기존 씬이 있으면 열기만 하며 덮어쓰지 않습니다.
- **Run Play validation**: 실제 Play 자동 검사를 실행합니다. 검사가 끝나면 Play를 종료합니다. 의도적으로 주입한 `S13_EXPECTED_FAULT`와 `S13_EXPECTED_START_FAULT`, 타임아웃 경고가 기록될 수 있습니다.

## 4. 시간 조절

1. Project에서 S13 폴더의 `S13Timing.asset`을 선택합니다.
2. Camera Blend, Door Opening, White Rise, White Hold 값을 변경합니다.
3. Play를 종료한 상태에서 **Apply timing to Timeline**을 실행합니다.
4. 다시 Play합니다. 각 시간은 0.1초 이상, 합계는 120초 미만이어야 합니다.

## 5. 확인할 화면 품질

벙커·문·플레이어는 블록아웃이고, 배경 험·바람은 합성 테스트 음원입니다. 문 소리는 기존 라이브러리의 실제 WAV를 재사용합니다. 카메라 자연스러움, Bloom, 밝기, 사운드의 질감은 직접 판단할 항목입니다. 자동 검사 결과와 구분하여 `VALIDATION.md`를 확인합니다.

## 6. 본편 통합

`S13_TestRig.prefab` 전체를 본편에 복사하지 않습니다. `S13_SequenceRig.prefab`과 `S13EndingAdapter`를 사용합니다. 실제 플레이어·카메라·출구·문·카메라 포즈와 화면을 연결하는 절차는 `TECHNICAL.md` 6.을 참고합니다.

## 7. 실제 검증 상태

실제 GUI Play 자동 검사와 InputSystem의 E→X→E 이벤트 주입을 통과했습니다. 물리 키보드의 조작감과 최종 시각/사운드 품질은 직접 확인해 주세요. 저장 Prefab의 스크립트 참조는 Audit saved prefab scripts 메뉴로 다시 검사할 수 있습니다. 결과와 캡처 여백 제한은 VALIDATION.md에 구분하여 기록했습니다.
