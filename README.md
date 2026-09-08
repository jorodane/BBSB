# Beat! Block, Shake~ Beat!

Unity 모바일 버전의 첫 탐험 흐름 구현. Unity **6000.3.5f2**, URP 2D, Input System을 사용한다.

## 실행

1. `codex/roguelike-foundation` 브랜치를 받아 Unity Hub에서 프로젝트 루트를 연다.
2. `Assets/BBSB/Scenes/RunMap.unity`를 열고 Play를 누른다.
3. Game 뷰를 **9:16**으로 설정하고 **탐험 시작**을 누른다.
4. 아래쪽의 밝은 노드부터 선택한다. 연결된 다음 단계만 진입할 수 있다.

`RunMap`은 Build Settings의 활성 시작 씬으로도 등록되어 있다. UI, EventSystem, 폰트는 씬의 `RunBootstrap`이 연결하므로 수동 오브젝트 연결은 필요하지 않다. 마우스 클릭과 모바일 터치를 지원하며 화면 안전 영역을 사용한다. 콘텐츠가 화면보다 길면 세로로 스크롤한다.

## 이번 버전에서 할 수 있는 일

- 3개 선택지 × 3단계와 4번째 단계의 보스로 구성된 무작위 연결 맵.
- 몬스터·엘리트·보스 클리어 → 골드 획득 → 무기/아이템/증강 중 하나 선택.
- 항상 무기 5슬롯 유지. 새 무기는 교체할 슬롯을 명시적으로 선택하며 기존 강화도 교체된다.
- 휴식: 최대 체력 비율 회복. 강화: 무기 하나 +1, 최대 +3. 각각 방문당 한 번 사용하거나 건너뛸 수 있다.
- 상점: 골드로 상품 구매, 재구매/초과 지출 방지. 구매하지 않고 나갈 수 있다.
- 회복 물약, 최대 체력 증강, 휴식량 증강, 상점 할인 증강.
- 보스 보상 처리 후 새 필드 생성. 체력·골드·장비·보상은 유지된다.
- 게임오버/탐험 종료 시 이번 런의 보상을 버린다. 새 탐험은 1필드의 기본 상태에서 시작한다.

**아직 실제 리듬 전투는 없다.** 전투 화면의 `테스트용 전투 결과` 버튼으로 승리/피해를 입은 승리/게임오버를 제출한다. 이 버튼은 리듬 판정이나 가상 전투 시뮬레이터가 아니다. 무기 리듬·강화의 전투 효과·Overkill·콘페티는 다음 전투 구현 범위다.

## 임시 콘텐츠와 조정 값

- 첫 필드부터 매 필드 4단계, 보스 한 노드. 최종 필드 수는 아직 정하지 않았으므로 보스 후 다음 필드로 반복한다.
- 시작 HP 100, 골드 60, 휴식 30%: 씬의 `RunBootstrap` Inspector에서 변경.
- `Use Fixed Seed`를 켜면 같은 시드로 재현 가능. 기본은 새 탐험마다 다른 시드다.
- 시작 무기 다섯 개, 상품 이름/설명, 가격, 회복량, 보상 골드는 샘플 값이다. 확정된 전투 밸런스가 아니다.
- `ContentCatalog.cs`에서 샘플 아이템을, `MapGenerator.cs`에서 노드 종류의 분포를 변경한다.
- 각 필드에 다섯 종류의 일반 노드가 모두 등장하도록 분포를 섞는다. 이는 초기 테스트용 분포다.
- 강화는 현재 레벨을 저장한다. 실제 계수는 전투 구현에서 사용한다.
- 회복 물약은 현재 전투 밖에서만 사용 가능. 저장/불러오기, 앱 재시작 후 런 복원, 영구 성장, 최종 엔딩은 아직 없다.

대화에서 확정한 리듬 전투 규칙은 [Docs/Design.md](Docs/Design.md)에 보존했다.

## 구조와 전투 연결

| 파일/폴더 | 역할 |
|---|---|
| `Assets/BBSB/Core` | Unity에 의존하지 않는 맵 생성, 런 상태, 보상/구매/초기화 규칙 |
| `Assets/BBSB/Runtime/RunBootstrap.cs` | 씬 진입점, 초기 값, 한글 폰트, 입력 모듈 |
| `Assets/BBSB/Runtime/RunPresenter.cs` | 화면 구성과 사용자 명령 전달 |
| `Assets/BBSB/Runtime/UI` | 레이아웃, 노드 연결선, 안전 영역 |
| `Assets/BBSB/Tests` | EditMode 핵심 규칙 테스트와 PlayMode UI 연결 테스트 |

`RunPresenter.BattleRequested(ticket, stageKind, fieldNumber)`에서 전투를 시작한다. 전투 구현은 `Session`의 현재 체력과 장비를 읽고, 종료 연출까지 끝낸 뒤 `SubmitBattleResult(ticket, victory, remainingHealth)`로 결과를 돌려준다. 고유 ticket이 이전 스테이지/런에서 늦게 도착하거나 중복된 결과를 거부한다. 적은 공용 HP를 사용하는 설계를 유지하며 개별 몬스터의 사망/타깃 처리를 이 흐름에 추가하지 않았다.

맵 난수와 보상 난수는 분리되어 있다. 상점을 들르거나 다른 종류의 보상을 받았다고 다음 필드 맵이 달라지지 않는다. 준비 단계에서 무기를 재편성하는 리듬 전투 내부 반복은 전투 계층이 맡고, `RunSession`은 스테이지 전체가 끝났을 때만 결과를 받는다.

## 검증

핵심 로직은 .NET 8에서 실행해 **14개 테스트 통과**. 1,000개 시드의 연결성/보스 위치, 진입 제한, 보상 중복 방지, 늦게 도착한 전투 결과, 무기 교체, 상점, 회복, 강화, 필드 진행, 게임오버 초기화를 포함한다.

```sh
dotnet run --project Tools/CoreChecks/BBSB.CoreChecks.csproj --configuration Release
```

Unity에서는 **Window → General → Test Runner**에서 EditMode의 `BBSB.Core.Tests`, PlayMode의 `BBSB.UI.Tests`를 실행한다. 이번 작업 환경에는 Unity Editor가 없어 **Unity 컴파일·PlayMode 테스트·모바일 빌드는 아직 실행하지 못했다.** 먼저 `RunMap`에서 시작→분기→보상→보스→다음 필드→게임오버를 확인하면 된다.

한글 글꼴은 OFL 라이선스의 Noto Sans CJK KR 일부를 `BBSB UI`로 이름을 바꿔 포함했다. 새 한글 텍스트를 넣을 때는 `Tools/subset_font.py`로 글자 집합을 갱신한다. 출처와 라이선스는 폰트 폴더에 있다.
