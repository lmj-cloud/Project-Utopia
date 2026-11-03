연출 독립 작업 공간

상태: 폴더 및 제작 설계만 준비. 씬, 프리팹, Timeline, Mixer는 아직 없음.
Unity Editor import/Play 검증 미실시. 브랜치 feature/jungwoo 유지.

소유 범위
- 새 제작물은 이 Workspace 아래에 보관한다.
- ../Scripts의 DirectionSequence, DirectionScreen, DirectionAudio,
  DirectionCue, DirectionVolume을 참조하며 원본 코드는 수정하지 않는다.
- 기존 Scenes, S03Prototype, PlayerController, 전투 코드, Packages,
  ProjectSettings, 외부 에셋과 _Recovery는 변경하지 않는다.
- 외부 모델/음원은 원래 경로에서 참조한다. 재질 변경은 새 복제본에만 한다.
- 외부 프리팹 변형은 새 Variant에 저장하고 원본에 Apply하지 않는다.

구조
Shared: 새 연출 공용 프리팹, 재질, VolumeProfile, AudioMixer.
Integration: 향후 게임 연결용 Adapter와 통합 프리팹. 현재 구현 없음.
Sequences/S03: Scenes, Timelines, Prefabs, Animations, Audio, Materials, Scripts.
Sequences/S06, S07, S10, S13: 후속 장면 자리만 준비.
빈 폴더는 Unity .meta와 함께 관리한다. 기존 GUID를 복사하지 않는다.

제작 순서
1. 복원 에셋 import/청취와 Console 확인.
2. S03/Scenes에 단독 테스트 씬을 Unity Editor에서 새로 생성.
3. S03/Prefabs에 S03_DirectionRoot 제작, 기존 Direction 컴포넌트 참조.
4. S03/Timelines에서 별도 Timeline을 Unity Editor로 작성.
5. 정상 완료/취소/비활성화/타임아웃/재실행 확인.
6. 별도 검토 후 Integration Adapter로 게임 이벤트 연결.

기존 DirectionSetup 메뉴는 Workspace 밖에 결과를 생성하므로 이 작업에는
실행하지 않는다. 기존 씬을 Additive로 열어 덮어쓰지 않는다.
Git의 LFS 상태 표시와 기존 재질 2개 변경은 별도 검토 대상으로 유지한다.
