# S13 파일 목록

## 1. 작업 범위

기준 경로: `Assets/Capstone/Direction/Workspace/Sequences/S13/`. S13 밖의 기존 파일은 수정하지 않습니다. Unity가 생성하는 각 파일/폴더의 `.meta`도 이 경로 안에 보관합니다.

## 2. 신규 소스와 문서

| 경로 | 용도 |
|---|---|
| Scripts/S13Timing.cs | 조절 가능한 시간/광량/음량 |
| Scripts/S13EndingSequence.cs | 시퀀스와 스냅샷/복구 |
| Scripts/S13EndingAdapter.cs | 본편 시작·완료·바인딩 계약 |
| Editor/S13WorkbenchBuilder.cs | 실제 Scene/Prefab/Timeline/리소스 생성과 메뉴 |
| Editor/S13Validation.cs | 실제 Play 검증 |
| START-HERE.md | 초보자 실행 방법 |
| FILES.md | 파일 목록 |
| VALIDATION.md | 실제 검사와 미검증 항목 |
| TECHNICAL.md | 구성·설정·이벤트·통합 방법 |

## 3. Unity 생성 리소스

| 경로 | 용도 |
|---|---|
| Scenes/S13_BunkerExit.unity | 독립 테스트 씬1개 |
| Prefabs/S13_TestRig.prefab | 전체 테스트 구성 |
| Prefabs/S13_SequenceRig.prefab | 테스트 Player/Camera 없는 이식용 구성 |
| Timelines/S13_BunkerEnding.playable | SignalTrack 포함 Timeline |
| Timelines/OpenDoor.signal | 문 개방 |
| Timelines/WhiteRise.signal | 백색 증가 |
| Timelines/Covered.signal | 백색 차폐 |
| Timelines/Finish.signal | 완료 요청 |
| S13Timing.asset | 기본 설정 |
| Materials/S13_WhiteProfile.asset | 전용 Bloom/ColorAdjustments |
| Materials/BunkerConcrete.mat | 벙커 블록아웃 |
| Materials/DoorSteel.mat | 문/파이프 |
| Materials/RibsAndPipes.mat | 구조 디테일 |
| Materials/WarmLamp.mat | 실내등 |
| Materials/ExitLight.mat | 출구 발광 |
| Materials/TestAvatar.mat | 테스트 플레이어 |
| Audio/TEMP_BunkerHum.wav | 합성 임시 험 |
| Audio/TEMP_ExitWind.wav | 합성 임시 바람 |

문 후보 WAV가 없는 경우 Builder는 TEMP_DoorCue.wav를 생성하도록 되어 있습니다. 최종 실제 생성 여부는 작업 종료 후 파일 인벤토리로 확인합니다.

## 4. 기존 참조와 보호

기존 DirectionSequence/DirectionScreen, S03ControlLockAdapter, S03FirstPersonPlayer, S03ThirdPersonRig 및 기존 문 WAV를 참조합니다. 해당 파일과 S03/S06/S07/S10·기존 씬·PlayerController·외부 에셋·Packages·ProjectSettings·_Recovery는 수정하지 않습니다. 브랜치 전환/Commit/Push/Merge/LFS 복원은 하지 않습니다. 보호 대상 SHA-256과 Git 상태는 프로젝트 외부에 기록합니다.

## 5. 최종 생성 인벤토리

실제 생성61개 파일: 내용27개, Unity .meta34개(하위 폴더 포함). Scene1개/Prefab2개/Timeline1개/Signal4개/Timing1개/VolumeProfile1개/Material6개/WAV2개/C#5개/문서4개입니다. 기존 문 WAV가 유효하여 TEMP_DoorCue.wav는 생성하지 않았습니다. 기존 S13.meta는 변경하지 않았습니다.

프로젝트 외부 감사 자료: C:/Users/djdjd/.codex/visualizations/2026/10/28/01a180e6-40a1-7490-802b-6f31bb333c7f/S13-final-files.json (개별 경로/바이트/SHA-256), S13-protected-before.json (기존472개 해시), S13-GUI-validation.log (최종 실제 Play 검사).
