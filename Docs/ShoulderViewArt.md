# 숄더뷰 아트 적용

`BBSB_ShoulderView_Art.zip`과 이 브랜치의 코드를 함께 사용한다.

1. 코드를 업데이트한 뒤 ZIP의 `Assets` 폴더를 Unity 프로젝트 루트에 합친다.
2. Unity의 임포트와 스크립트 컴파일이 끝날 때까지 기다린다.
3. 콘솔에 `BBSB shoulder-view art connected`가 표시되면 `RunMap`에서 전투를 시작한다.

Inspector에서 이미지를 하나씩 연결할 필요가 없다. 에디터의 `ShoulderViewAssetInstaller`가 최초 한 번 네이티브 에셋을 생성하고 기존 플레이어·몬스터·전투 화면에 참조를 저장한다. 캐릭터·투사체·이펙트 60장이 들어오면 배경 유무와 무관하게 연결한다. 수동 재시도가 필요한 경우 메뉴 `BBSB > Presentation > Install shoulder-view art`에서 누락 파일을 확인할 수 있다. 이미 설치된 프로젝트에서는 기존 편집 내용을 덮어쓰지 않는다.

## 에셋 구조

| 위치 | 내용 |
| --- | --- |
| `Assets/BBSB/Art/ShoulderView/Characters` | 플레이어 5장, 몬스터 12종 × 3장 |
| `Assets/BBSB/Art/ShoulderView/Projectiles` | 몬스터별 투사체 12장 |
| `Assets/BBSB/Art/ShoulderView/Effects` | 패링·가드·베기·화살·충돌·노트 확정·노트 파괴 7장 |
| `Assets/BBSB/Art/ShoulderView/Stages` | 8개 맵 테마의 전투 배경 |
| `Assets/BBSB/Presentation/ShoulderView/<캐릭터>/` | 최초 임포트 시 생성되는 `.prefab`, `.controller`, `.anim` |
| `Assets/BBSB/Resources/BBSB/Presentation/ShoulderView.asset` | 맵 배경·하늘색·투사체·이펙트의 직렬화된 참조 |

런타임은 PNG 파일 경로를 읽지 않는다. `PlayerAuthoring`/`MonsterAuthoring`이 캐릭터 프리팹과 컨트롤러를 참조하고, `AnimationClip`의 `Body/SpriteRenderer.m_Sprite` 키프레임이 이미지 참조를 재생한다. 배경과 이펙트도 `ShoulderViewPresentation`의 참조를 사용한다. 기본 화면에는 해당 에셋이 연결되고, 사용자 화면에 참조가 비어 있으면 공통 프레젠테이션 에셋을 한 번 로드한다. 아트 팩이 없는 프로젝트에는 기존 표시 방식이 남아 있다.

## 프리팹과 애니메이션 편집

모든 캐릭터 프리팹은 `Animator` 루트 아래에 `Body/SpriteRenderer`를 둔다. 플레이어는 등 뒤 시점이며, 현재 5라인 전투에서는 화면 중앙 전경에 배치한다. 몬스터는 거의 정면 시점으로 위쪽 후경에 배치한다. 성당 무대와 원근 레인 업데이트는 [CathedralBattleVisuals.md](CathedralBattleVisuals.md)를 참고한다.

| 캐릭터 | Animator 상태 | 이미지/동작 |
| --- | --- | --- |
| 플레이어 | `Idle` | 뒤를 보는 대기 자세, 호흡 |
| 플레이어 | `TapImpact` | 실제 피해를 낸 공격 |
| 플레이어 | `Guard` | 홀드 중 가드 또는 패링 |
| 플레이어 | `Bow` | 활 당기기와 발사 대기 |
| 플레이어 | `Hit` | 실제 피격 |
| 몬스터 | `Idle` | 정면에 가까운 대기 자세 |
| 몬스터 | `Call` | 충돌 1박 전부터 공격 준비 |
| 몬스터 | `Attack` | 마지막 0.35박에 돌진/발사, 이후 짧은 마무리 |
| 몬스터 | `Recover`, `Hit`, `Perfect`, `HalfMiss`, `Miss`, `Defeated` | 기존 전투 시스템 호환 상태. 기본 대기 이미지에서 확장 가능 |

원본은 상태별 키 포즈 이미지이며, 기본 클립에 작은 호흡·반동 Transform 곡선을 포함한다. 다프레임 원화 애니메이션이나 리깅 애니메이션은 아니다. Unity Animation 창에서 Sprite 키프레임을 추가하거나 Transform 곡선을 수정하면 그대로 적용된다. 자동 설치는 기존 클립·컨트롤러·프리팹을 다시 생성하지 않는다.

`ActorPrefabView`는 네이티브 Animator를 전투 박자로 샘플링한다. `SpriteCanvasGraphic`은 그 SpriteRenderer를 Canvas에 표시하므로 캐릭터와 HUD의 정렬이 유지된다. 원본 SpriteRenderer의 화면 출력만 끄고 애니메이션과 Sprite 참조는 유지한다. 일시정지하면 자세, 투사체, 이펙트도 함께 멈춘다.

투사체는 몬스터별 공격에서 플레이어 방향으로 이동하며 정확한 충돌 박자에 도착한다. 패링 이펙트는 실제 차단이 일어난 입력 시점에 표시된다. 활 당기기를 완료하기 전에 발사 이펙트가 나오지 않으며, 조건 노트가 확정되거나 취소되면 각각 확정/파괴 이펙트를 표시한다.

## 배경과 투명도

이번 팩은 `POP`, `RNB`, `JAZZ`, `RAP`, `BARD`, `CELT`, `NEW`, `METAL`의 8개 배경이다. 각 맵의 10개 곡에 공통 적용되며 음악·공격 패턴은 그대로다. 개별 곡 전용 배경은 `ShoulderView.asset`의 `Stages`에 `BARD-06`처럼 곡 ID를 추가해서 지정한다. 곡 ID 참조가 맵 ID보다 우선한다.

배경은 선택적으로, 맵별로 따로 설치된다. `Stages` 폴더가 없으면 기존 스테이지 배경을 계속 사용한다. 나중에 `Assets/BBSB/Art/ShoulderView/Stages/<맵 ID>.png`를 추가하거나 이동하면 해당 배경만 자동 연결한다. 이미 설치된 캐릭터·프리팹·클립과 직접 수정한 배경 항목은 보존한다. 설치한 뒤 직접 제거한 배경 항목도 다음 이미지 임포트에서 자동으로 되살리지 않는다.

배경의 하늘과 열린 아치 구멍은 실제 알파 채널이다. 해당 맵의 `sky` 색이 뒤에 표시된다. 캐릭터·투사체·이펙트도 투명 PNG이며 파일 자체를 색상 제거 처리하지 않는다. PNG 바이너리는 Git에 올리지 않는다. Unity가 생성한 프리팹·클립·컨트롤러 및 `.meta`는 일반 텍스트 에셋으로 관리할 수 있다.

## 검증 범위

CoreChecks는 공격/가드/활 상태 전환, 공격 시점과 패링 시점의 구분, 몬스터별 예고, 엇박 투사체 도착, 일시정지, 임시 노트 확정/파괴를 검사한다. PlayMode의 `ShoulderViewPresentationTests`는 배경 없이 설치된 캐릭터·투사체·효과 참조와 실제 Animator의 Sprite 전환 및 Canvas 반영, 뒤늦은 배경 추가와 사용자 편집 보존을 검사한다. 캐릭터 아트 팩이 없으면 설치 통합 테스트는 건너뛴다.

이 작업 환경에는 Unity Editor가 없어 자동 설치와 실제 인게임 렌더링은 여기에서 실행 검증하지 못했다. CoreChecks 및 이미지 파일 검증 결과와 Unity PlayMode 검증 여부를 구분해서 기록한다.
