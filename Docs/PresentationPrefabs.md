# Canvas 화면과 캐릭터 프리팹 편집

Unity 재컴파일 후 기본 프리팹이 없는 경우 한 번 자동 생성한다. 수동 실행은 **BBSB → Presentation → Create missing Canvas and actor prefabs**다. 이미 존재하는 프리팹·설정은 덮어쓰지 않는다. 생성된 `.prefab`, `.asset`, `.meta`는 프로젝트 데이터이므로 Unity 작업 후 Git에 함께 올린다.

## UI

**BBSB → Presentation → Open prefab catalog**를 열면 화면과 반복 UI 요소의 참조가 연결되어 있다. 생성 위치는 `Assets/BBSB/Resources/BBSB/Presentation`이다.

- TitleScreen: 제목·설명·맵 이름·이전/다음·시작·도감 버튼을 Canvas에서 직접 배치한다. TitleScreenBindings의 참조로 기능을 연결한다.
- PreparationScreen: 플레이어 이미지·이름·곡·HP·도감·메뉴·연주 시작·패턴 ScrollRect·무기 영역을 연결한다. 패턴과 무기 항목은 해당 컨테이너 안에 현재 전투 데이터로 채운다.
- BattleScreen: Arena 영역과 곡명·HP·콤보·피드백·피해·진행 바·메뉴를 BattleHudBindings로 연결한다. HUD RectTransform은 런타임에 다시 배치하지 않는다. Fill은 가로 anchorMax로 진행률을 갱신하므로 왼쪽 시작점과 부모 트랙 구조를 유지한다.
- Map / Report / Reward / Replacement / Rest / Upgrade / Shop / FieldCleared / GameOver / Codex: 화면별 Canvas와 Content를 제공한다. 배경·장식·Content 영역은 직접 수정하며, 지도 노드·상품·결과 등의 가변 항목은 기존 화면 로직이 채운다.
- PrimaryButton / SecondaryButton / TitleText / HeadingText / BodyText / CaptionText / Card: 반복 생성되는 항목의 외형을 수정한다. 버튼에는 Button과 자식 Text, 카드에는 VerticalLayoutGroup을 유지한다. 텍스트 내용·버튼 활성 상태·필요한 행 높이는 데이터가 정하고 폰트·색·버튼 배경·카드 내부 여백은 프리팹이 정한다.

CanvasScreen의 Content는 화면 로직이 사용할 영역이다. 배경과 순수 장식은 Content 밖에 둘 수 있다. 동적으로 생성되는 패턴 그래프·무기 그림·지도 연결선의 내부 배치는 전용 표시 코드가 담당한다. 일시정지·보조 메뉴는 반복 UI 요소 프리팹을 사용하는 기존 동적 화면이다.

버튼의 게임 동작은 실행 시 AddListener로 연결한다. 프리팹 OnClick에는 같은 게임 동작을 중복 등록하지 않는다. 화면은 재입장마다 새 인스턴스를 사용하여 이전 화면의 이벤트를 재사용하지 않는다. 씬에는 기존 RunBootstrap을 유지한다. 코드 생성 UI는 카탈로그가 없을 때의 호환 경로로 남는다.

## 플레이어

**BBSB → Player Editor**에서 `Player.asset`을 선택한다. 기본 PlayerVisual 프리팹은 Body SpriteRenderer와 Animator를 가진다. 처음에는 기존 프레임을 사용하여 동작을 유지한다.

- Visual Prefab: SpriteRenderer 외형 또는 RectTransform + Image 외형을 연결한다.
- Portrait: 준비 화면과 대체 이미지에 사용할 Sprite.
- Sprite Reference Height: SpriteRenderer 외형의 기준 키(Unity 단위). Display Scale과 Display Offset은 전투 표시 보정이다. 발 접지점을 원점에 놓는다.
- Layout: 기존 PlayerMotionDisplay의 지면 위치·크기·포즈 보정 설정.
- Motions: Idle/Prepare/Sustain/Impact/Recover, 행동 종류, 판정 등급, 펀치 종류 조건을 설정한다. Any Gesture/Any Grade를 끄면 해당 조건으로 제한한다. 더 구체적인 조건이 우선한다. Punch -1은 전체, 0/1/2는 왼손/오른손/어퍼다.
- Frames: 동작별 Sprite 배열을 직접 등록할 수 있다. Duration Beats와 Loop로 재생한다.
- Controller + State: 해당 상태가 있으면 Sprite 배열보다 Animator가 우선한다. 상태는 `Base Layer.TapImpact`처럼 전체 경로로 지정한다.
- Use Legacy Frames: 연결한 상태나 프레임이 없을 때 기존 동작 이미지를 사용한다. 직접 만든 외형만 사용하려면 끈다.

**Animator · 동작 Clip 틀 생성**은 새 폴더에 Controller와 동작별 Clip을 만들고 상태 경로까지 연결한다. Frames를 채웠다면 그 배열로, 비어 있다면 Portrait로 시작한다. 모션별 실제 그림과 Transform 키는 Animation 창에서 편집한다. 기존 Clip을 덮어쓰지 않는다.

## 몬스터

Monster Editor의 Animator 모션 탭에서 **SpriteRenderer / UI 외형 Prefab**을 연결한다. 기존 RectTransform visualPrefab 연결은 호환을 위해 유지하며 새 actorPrefab이 우선한다. Monster 에셋을 선택하고 **BBSB → Presentation → Create SpriteRenderer prefab for selected monster**를 실행하면 현재 Portrait로 Body 프리팹을 만들어 연결한다. 기존 Controller 참조는 유지되므로 필요하면 SpriteRenderer용 Clip으로 교체한다. 전용 새 프리팹을 연결하면 기존 몸체 이미지 재생보다 새 외형이 우선한다.

## SpriteRenderer와 Animator의 표시 규칙

SpriteRenderer의 Sprite, Color, Flip X/Y, 자식 Transform, Sorting Layer/Order를 Canvas 메시로 표시한다. 원본 SpriteRenderer 컴포넌트와 Animator는 외형 인스턴스에 유지되고 씬의 SpriteRenderer 직접 렌더링만 끈다. 이 방식으로 UI 마스크·메뉴와 기존 무기의 앞뒤 순서를 맞춘다. Draw Mode는 Simple을 사용한다. SpriteRenderer 전용 조명·커스텀 셰이더·MaterialPropertyBlock·SpriteMask·SortingGroup·3D 렌더러는 Canvas 메시 경로에 반영하지 않는다. 한 외형에서 SpriteRenderer와 Image를 혼합하지 않는다.

Animator는 하나를 사용한다. 루트 모션과 Animation Event는 끄고 노래 시각으로 상태를 샘플링한다. 판정·피해는 Animation Event나 StateMachineBehaviour에서 처리하지 않는다. 자동 Transition 없이 각 상태에 Clip을 연결하는 방식을 사용한다. 일시정지와 피격 정지는 기존 전투 시계를 따른다.

검증 범위: 자동 CoreChecks는 핵심 회귀와 C# 문법을 검사한다. SpriteRenderer 표시·Canvas 배치·프리팹 생성·Animator 상호작용은 Unity Editor/PlayMode에서 별도 확인이 필요하다.
