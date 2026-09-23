# 성당 전투 비주얼 팩

사용자가 제공한 `11793.png`의 중앙 무대와 원근 레인 구도를 현재 전투에 적용한다.
플레이어·몬스터·무기는 기존 네이티브 프리팹과 아트를 사용하며, 배경과 HUD/노트 그래픽 9종을 새로 제공한다.

## 적용

1. `codex/roguelike-foundation` 코드를 업데이트한다.
2. `BBSB_Cathedral_Visuals.zip`의 `Assets` 폴더를 프로젝트 루트에 합친다.
3. 임포트가 끝나면 `BBSB cathedral battle art connected` 로그를 확인한다.
4. `RunMap`에서 전투에 들어간다. 필요한 경우 `BBSB > Presentation > Install cathedral battle art` 메뉴로 설치한다.

이미지는 Git에 포함하지 않는다. ZIP을 복사하기 전에도 새 원근 레이아웃과 기본 노트 메시는 동작한다.
설치기는 아래 PNG를 일반 Texture/Sprite로 임포트하고, `Assets/BBSB/Resources/BBSB/Presentation/CathedralBattle.asset`을 한 번 만든다.
기존 테마 에셋과 수동 편집값은 재설치로 덮어쓰지 않는다. PNG를 같은 경로에서 교체하면 기존 참조가 유지된다.

| 파일 (`Assets/BBSB/Art/CathedralBattle/`) | 용도 |
| --- | --- |
| `cathedral-arena.png` | 하늘과 열린 아치가 투명한 성당 무대 |
| `sky-clouds.png` | 성당 뒤 하늘색 위에 겹치는 투명 구름 레이어 |
| `note-light.png` | 정박 노트의 금색 크리스털 머리 |
| `note-dark.png` | 엇박 노트의 보라색 크리스털 머리 |
| `health-frame.png` | 플레이어 및 개별 몬스터 체력 프레임 |
| `combo-crest.png` | 콤보 아래 금속 장식 |
| `beat-ring.png` | 현재 1~4박 표시의 원형 프레임 |
| `weapon-halo.png` | 장착 무기 뒤 부유 후광 |
| `judgment-flash.png` | 성공 입력 순간의 짧은 섬광 |

## 화면과 편집

- 위쪽에는 전열/후열 몬스터와 **각자의 체력**, 중앙에는 플레이어와 부유 무기를 둔다.
- 아래에는 S · D · F · Space · J · K · L의 원근 레인이 펼쳐진다. 빈 레인은 어둡게 남으며 입력을 받지 않는다.
- 콤보/최근 판정은 왼쪽, 현재 박자는 오른쪽, 층은 오른쪽 위, 곡 정보는 오른쪽 아래에 표시한다.
- `Beat / Block / Shake / Beat` 카운트인은 화면 중앙에 독립적으로 나온다. 단어 설정과 네 번째 단어의 음악 시작 시점은 그대로다.
- 금색/보라색은 **노트 자체의 정박/엇박 시점**을 구분한다. 혼돈 전환 뒤의 미리보기에도 그 시점의 색을 사용한다.
- 홀드 몸체는 동적 Canvas 메시다. 레인 원근을 따라 폭이 달라지고, 이어지는 홀드는 중간 머리를 추가하지 않고 연결 표시한다. 미확정 노트는 반투명, 미확정 홀드는 점선도 함께 표시한다.
- 3박 미리보기, 박자 스텝 이동, 전투 판정·피해·무기 패턴·카운트인 시간은 변경하지 않는다.
- `BattleBoardLayout`의 동일한 투영식을 트랙/노트/터치 판정이 공유한다. 사다리꼴 바깥쪽이나 옆 레인을 누르면 그 레인의 입력으로 잘못 인식하지 않는다.

`FiveLaneHudBindings`에서 Canvas 위치와 텍스트를 편집할 수 있다. 버전 3 전환은 예전 기본 위치와 정확히 같은 요소만 이동한다.
`arrangeEquippedLanes`를 끄면 사용자 지정 입력 영역과 판정점 배치를 사용하며 원근 터치 필터를 적용하지 않는다.
프리팹의 `visualTheme`에 다른 `BattleVisualTheme`를 연결할 수도 있다. 기본 테마의 `useArena`를 끄면 맵별 기존 배경을 사용한다.
기존 플레이어/몬스터 애니메이션, 무기 아틀라스, 에디터 편집 방식은 계속 사용한다.

## 검증

원근 레인의 전체 깊이와 확장 입력 조합에서 키 판정·순서·경계, 정박/엇박 색 판별 및 기존 노트/모션 타임라인을 CoreChecks로 검사한다.
PlayMode 검사는 중앙 구도·화면 비율·기존 프리팹 이행과 별도 카운트인 라벨에 맞춰 갱신한다.
이 작업 환경에는 Unity Editor가 없어 실제 Unity 렌더링/PlayMode 실행은 별도로 확인해야 한다.
아트 팩에는 PNG별 해상도·실제 알파 픽셀 검사 결과와 생성 프롬프트도 포함한다.
