# S06 검증 기록

검증일: 2026-10-29 / Unity 6000.3.21f1 / feature/jungwoo

## 완료된 자동 검사

실제 Unity Editor 배치 Play 모드에서 최종 `S06_VALIDATION_PASS`, 프로세스 종료 코드 0을 확인했습니다.

- 정상 시작 및 실행 중 중복 호출 차단
- 저장된 PCM WAV 3개 재임포트 후 길이 및 무음이 아닌 샘플 데이터
- Timeline의 Boxing → StarterShot → Approach → Hallucination → Disrupted → SecondPlace 단계 수신
- 환각 중 군중 볼륨 감소 및 LowPass 필터 적용
- 정상 완료 후 실제 onAwardReady 수신 1회, placing=2 및 다음 시상식의 intentionalFirstPerson=true
- 완료 이후 재실행 차단
- CinemachineBrain의 활성 카메라가 CM_ThirdPerson으로 복귀
- 주인공 수평 위치 유지: 전쟁터 순간 이동 없음
- 접근 환청 중 취소와 환각 중 취소
- 공통 Timeline 타임아웃을 테스트에서 0.2초로 축소한 강제 중단
- S06 실시간 watchdog을 테스트에서 0.2초로 축소한 강제 중단
- S06 컴포넌트 비활성화 복구
- onStarted 콜백에 의도적인 S06_EXPECTED_FAULT 예외 주입 후 취소/복구
- 각 중단에서 소유 ControlLock 해제, 화면/Volume 초기화, 주관적 오디오 정지, 군중 볼륨/필터와 카메라 우선순위 복원

타임아웃과 콜백 예외 검사에 따른 경고/예외 로그는 의도한 테스트 출력입니다. 최종 로그에서 C# 컴파일 오류나 그 밖의 S06 예외는 확인되지 않았습니다. 정상 플레이 중 예외를 발생시키는 코드는 없고 주입 코드는 Editor 검증기에만 있습니다.

최종 로그 (프로젝트 외부):
`C:/Users/djdjd/.codex/visualizations/2026/10/28/01a180e6-40a1-7490-802b-6f31bb333c7f/S06-play-validation.log`

초기 검사는 공통 타이머가 정상 Timeline 종료 전에 발동해 실패했습니다. 이후 신호·시간 추적으로 확인하고, S06의 공통 보조 타임아웃만 60초로 조정했습니다. 별도의 20초 실시간 watchdog을 실제 S06 제한으로 유지합니다. 공통 DirectionSequence 코드는 수정하지 않았습니다. 최종 설정으로 전체 검사를 재실행하여 통과했습니다.

## 직접 확인이 필요한 내용

- E / X / WASD / 마우스의 실제 키 입력 감각. 자동 검사는 같은 public 메서드를 호출합니다.
- 출발 총성의 원근감, 접근 환청의 심리적 설득력, 음량 밸런스. 자동 재생 상태·데이터 검사는 사람이 소리를 들은 검증을 대체하지 않습니다.
- 주관적 색보정, 짧은 화면 흔들림과 카메라 구도의 미적 완성도
- 일반 창 모드, 다양한 해상도와 다른 GPU에서의 시인성·성능
- 본 게임의 실제 전투·PlayerController와 결합한 상태. 현재 테스트 플레이어/임시 결과 흐름만 검증했습니다.

일반 Editor에서도 S06 씬 열기와 Play 진입, 실제 링·테스트 캐릭터·안내 HUD 표시를 확인했습니다. OS 자동 입력으로 E 시작은 확정하지 못했으므로 실제 키 입력은 사용자 확인 항목으로 남깁니다. 확인 후 Play를 종료하고 S06 씬을 열어둔 상태입니다. 편집 모드 Game 미리보기의 기본 카메라 위치는 Play 진입 후 3인칭 리그가 갱신합니다.

Editor를 처음 열 때 Package Manager 목록 조회 오류가 한 차례 표시됐습니다. S06 컴파일 오류와는 별개이며 Packages/ProjectSettings를 변경하지 않았습니다.

## 미구현 또는 임시 요소

- 실제 복싱 전투/데미지/승패 판정, 사람 캐릭터와 복싱 애니메이션
- 최종 출발 권총·총알 접근·트라우마 사운드 (현재 합성 PCM 테스트 음원)
- 관중 캐릭터·환호, 최종 경기장 아트 (실제 링 + 실내 Primitive 블록아웃)
- 다음 시상식 씬 로딩 및 1인칭 신체/메달/군번줄/권총/8초 전장/침실 각성
- Audio Mixer 라우팅

## 작업 범위

새 파일은 S06 폴더에만 생성했습니다. S03, S03Prototype, 공통 Direction 코드, 기존 게임플레이 코드는 참조만 했습니다. 원본 재질 2개의 SHA-256은 앞서 보존된 해시와 일치합니다. Packages/ProjectSettings와 _Recovery는 수정하지 않았습니다. 기존 LFS 상태의 정리·복원, 스테이징, 커밋, 푸시, 병합은 하지 않았습니다.

파일 전체 목록은 FILES.txt에 기록합니다. .meta도 함께 유지해야 Unity 참조가 보존됩니다.
