# S10 운전석 개정 파일 목록

## 1. 기존 파일 보존

기존 S10_FamilyAccident 씬을 재사용합니다. Scene 파일은 같은 프리팹 참조를 유지하고, 프리팹/Timeline/설정/스크립트 갱신으로 동작을 바꿨습니다. 기존 .meta GUID47개 유지, 기존 파일 삭제
0
개, 새 .unity 생성 없음.

A=이번 추가, M=이번 수정입니다. 비교 기준은 작업 전 외부 백업이며 Git 스테이징 상태가 아닙니다.

## 2. 실제 수정 파일

| 상태 | S10 기준 경로 |
|---|---|
| M | `Editor/S10Validation.cs` |
| M | `Editor/S10WorkbenchBuilder.cs` |
| M | `FILES.md` |
| M | `Materials/S10_ConfusionProfile.asset` |
| M | `Prefabs/S10_TestRig.prefab` |
| M | `S10Timing.asset` |
| M | `Scripts/S10CrashSequence.cs` |
| M | `Scripts/S10StoryAdapter.cs` |
| M | `Scripts/S10Timing.cs` |
| M | `START-HERE.md` |
| M | `TECHNICAL.md` |
| M | `Timelines/S10_FamilyAccident.playable` |
| M | `VALIDATION.md` |

## 3. 실제 추가 파일

| 상태 | S10 기준 경로 |
|---|---|
| A | `Editor/S10DriverUpgrade.cs` |
| A | `Editor/S10DriverUpgrade.cs.meta` |
| A | `Materials/DriverWindshield.mat` |
| A | `Materials/DriverWindshield.mat.meta` |
| A | `Materials/Imported_alu.mat` |
| A | `Materials/Imported_alu.mat.meta` |
| A | `Materials/Imported_exhaust.mat` |
| A | `Materials/Imported_exhaust.mat.meta` |
| A | `Materials/Imported_main_color.mat` |
| A | `Materials/Imported_main_color.mat.meta` |
| A | `Materials/Imported_metal_chromed.mat` |
| A | `Materials/Imported_metal_chromed.mat.meta` |
| A | `Materials/Imported_metal_dark.mat` |
| A | `Materials/Imported_metal_dark.mat.meta` |
| A | `Materials/Imported_plastic.mat` |
| A | `Materials/Imported_plastic.mat.meta` |
| A | `Materials/Imported_wheel_metal.mat` |
| A | `Materials/Imported_wheel_metal.mat.meta` |
| A | `Materials/Imported_wheel.mat` |
| A | `Materials/Imported_wheel.mat.meta` |
| A | `Materials/Imported_windows.mat` |
| A | `Materials/Imported_windows.mat.meta` |
| A | `Materials/MirrorFamilySilhouettes.mat` |
| A | `Materials/MirrorFamilySilhouettes.mat.meta` |
| A | `Materials/WindshieldCrack.mat` |
| A | `Materials/WindshieldCrack.mat.meta` |
| A | `Resources.meta` |
| A | `Resources/TruckFromLocalCache.meta` |
| A | `Resources/TruckFromLocalCache/SOURCE.json` |
| A | `Resources/TruckFromLocalCache/SOURCE.json.meta` |
| A | `Resources/TruckFromLocalCache/truck_color_01.bmp` |
| A | `Resources/TruckFromLocalCache/truck_color_01.bmp.meta` |
| A | `Resources/TruckFromLocalCache/truck.fbx` |
| A | `Resources/TruckFromLocalCache/truck.fbx.meta` |
| A | `Resources/TruckFromLocalCache/wheels.bmp` |
| A | `Resources/TruckFromLocalCache/wheels.bmp.meta` |
| A | `Scripts/S10BlackoutRelay.cs` |
| A | `Scripts/S10BlackoutRelay.cs.meta` |
| A | `Scripts/S10DriverEffects.cs` |
| A | `Scripts/S10DriverEffects.cs.meta` |

## 4. 변경하지 않은 기존 참조

기존 Scene/Signal7개/합성 WAV9개/기타 유지 재질 등은 동일 파일과 GUID로 참조합니다. 이 목록의 .meta를 삭제하거나 재생성하지 마세요.

원본 트럭/재질/텍스처, S03/S06/S07, 공통 코드, 팀원 스크립트·씬, Packages/ProjectSettings, _Recovery는 수정하지 않았습니다.

## 5. 외부 보관 자료

검증 폴더: `C:/Users/djdjd/.codex/visualizations/2026/10/28/01a180e6-40a1-7490-802b-6f31bb333c7f/`

- S10-before-driver-20261101-153717/S10/ — 작업 전 백업
- S10-driver-protected.json — 보호514개 SHA-256
- S10-driver-guids.json — 기존 GUID47개
- S10-driver-upgrade.log — 최초 실제 FBX 임포트/기존 에셋 갱신
- S10-driver-release-validation.log — 최종 반복 Builder/Play 검증
- S10-driver-normal.png / approach.png / impact.png / red.png / final-black.png — 실제 파일 이름의 모든 접두어는 S10-driver-

## 6. 출력 카메라 고정 대응

수정: Scripts/S10DriverEffects.cs, Editor/S10Validation.cs, Editor/S10DriverUpgrade.cs, START-HERE.md, FILES.md, VALIDATION.md. 신규 Scene이나 외부 에셋 변경은 없습니다. 출력 카메라 갱신은 S10 런타임 코드에 있어 기존 Prefab 재생 및 재생성 양쪽에 적용됩니다.

## 7. FBX 내장 카메라 수정

수정: Scripts/S10DriverEffects.cs (트럭 자식 Camera 차단), Scripts/S10CrashSequence.cs (중단 진단), Editor/S10DriverUpgrade.cs (생성 시 차단), Editor/S10Validation.cs (전체 출력 Camera 검사), Prefabs/S10_TestRig.prefab (내장 Camera 비활성), START-HERE.md, FILES.md, TECHNICAL.md, VALIDATION.md. 신규: Editor/S10PlaybackDiagnostics.cs 및 Unity 생성 .meta. 외부 원본은 수정하지 않았습니다.
