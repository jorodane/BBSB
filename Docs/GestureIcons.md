# 행동 아이콘과 무기 소켓

## 공통 의미

| 행동 | 색 | 작은 소켓 | 큰 안내 아이콘 |
|---|---|---|---|
| TAP | 빨강 `#DE424B` | 아래쪽 삼각형 하나 | 아래쪽 큰 삼각형 안에 같은 중심의 작은 삼각형 + TAP |
| HOLD | 파랑 `#287DD1` | 동그라미 | 원형 버튼을 누르고 있는 손가락 + HOLD |
| FLICK | 초록 `#27985A` | 오른쪽 위로 올라가는 `/` | 왼쪽 아래 작은 원에서 오른쪽 위로 향하는 화살표 + FLICK |
| DIVE | 주황 `#E28222` | U | 아래쪽을 훑고 오른쪽으로 올라오는 U자 화살표 + DIVE |
| SHAKE | 보라 `#9256C9` | = | 위는 오른쪽, 아래는 왼쪽으로 향하는 화살표 + SHAKE |

TAP의 큰 기호는 동심삼각형이다. 위아래로 쌓은 두 삼각형이 아니다. FLICK에는 캐럿이나 천장에 부딪혀 돌아오는 꺾임을 사용하지 않는다. 금속 테두리·색 바탕·새긴 문양으로 장식하되 작은 실루엣과 행동별 색을 유지한다. PNG 밖은 실제 알파 투명 배경이다.

## 설치

1. `codex/roguelike-foundation`의 최신 코드를 받는다.
2. `BBSB_gesture_icons_v1.zip`의 `Assets`를 프로젝트의 `Assets`에 합친다.
3. `Assets/BBSB/Resources/BBSB/GestureIcons/Small`과 `Large`에 각각 `tap.png`, `hold.png`, `flick.png`, `dive.png`, `shake.png`가 들어간다.

총 10개의 개별 정사각형 RGBA PNG다. 원본 투명 알파를 유지하며 배경색을 제거하지 않는다. `GestureIconImporter`가 Single Sprite, 중앙 피벗, FullRect, NPOT 유지, RGBA32 임포트를 적용한다. 수동 씬 연결은 필요 없다. PNG는 별도 전달하며 코드 커밋에는 포함하지 않는다.

## 표시와 시간

- `GestureIconCatalog`가 색·영문명·리소스 경로를 한 곳에서 정의한다.
- `WeaponSocketGraphic`의 자식 아이콘은 발동 여부와 관계없이 항상 표시된다. 부모 무기의 회전·크기·원거리 자세 변경을 그대로 상속하며 화면에 맞춰 역회전하지 않는다. 원래 소켓의 중심과 가로/세로 반지름을 유지한다.
- 발동 때는 바깥 빛만 강화한다. 기호 위를 흰빛으로 덮지 않는다.
- `ResponsePromptTimeline`은 Call이 실제 시작된 공격의 다음 미판정 행동을 읽는다. Call 전의 미래 패턴은 드러내지 않는다. 여러 동시 행동은 기호를 함께 표시하고, Hold·Dive는 유지·해제 판정까지 남는다. 판정된 행동은 다음 행동으로 넘어가고 휴식·중단·종료 시 사라진다.
- 전투·무기 연습·도감의 `BattleArenaView`가 같은 안내를 사용한다. 안내는 피격 흔들림 레이어 밖에 있으며 입력을 가로채지 않는다. 일시정지는 곡 시간과 함께 고정한다.
- 패턴 도표·음악 슬롯 도표에도 작은 기호를 그린다. 작은 타임라인에서는 동일한 도형 기호를 직접 그려 추가 텍스처나 자식 객체를 만들지 않는다. 판정 색은 유지선/끝 표시 등 별도 요소에 적용하고 행동 기호의 색은 유지한다.
- 조작 도움말과 장비 상세에는 영문명을 포함한 큰 아이콘을 사용한다. 이미지가 설치되지 않았어도 같은 의미의 코드 도형과 큰 아이콘의 영문명을 표시한다.

## 검증

`GestureIconTests`는 색·경로 대응, Call 이전 미노출, 미판정 행동 전환, Hold·Dive 종료, 동시 행동, 중단·연습 초기화와 조회의 무부작용을 검사한다. 기존 CoreChecks에서 함께 실행한다.

```sh
dotnet run --project Tools/CoreChecks/BBSB.CoreChecks.csproj --configuration Release
python Tools/validate_gesture_icons.py /path/to/unzipped-pack
```

Unity Test Runner의 `GestureIconIntegrationTests`는 정지 상태 소켓 표시, 여러 등급과 각도에서 회전·스케일 상속, PNG 10개의 로딩과 투명도, 입력 통과를 검사한다. 실행에는 Unity Editor와 이미지 팩이 필요하다. 실제 16:9/16:10 화면에서 안내 위치·금속 문양의 작은 크기 가독성·일시정지를 확인한다.
