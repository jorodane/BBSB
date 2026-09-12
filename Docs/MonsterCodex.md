# 몬스터 도감

시작 화면의 **몬스터 도감**, 탐험 메뉴, 전투 일시정지 메뉴에서 같은 도감을 연다. 아직 탐험을 시작하지 않아도 12종·24개 패턴을 모두 볼 수 있다.

목록에는 아이콘과 이름만 표시하며 화면 폭에 따라 열 수가 바뀐다. 상세 헤더는 종족 아이콘과 이름, 본문 왼쪽은 캐릭터·공격 재생, 오른쪽은 **평상시 → 각 패턴** 순서의 아이콘·이름 목록이다. 목록으로 돌아가면 이전 스크롤 위치를 유지한다. Escape는 상세에서 목록으로, 목록에서 원래 메뉴로 돌아간다.

## 재생과 반응 안내

- 패턴을 고르면 한 박 준비 후 Call부터 반복한다. 재생/멈춤, 다시 보기, 소리 켜기/끄기, 60–200 BPM 조절을 지원한다. 평상시를 고르면 패턴 재생과 소리를 정리한다.
- 첫 Call을 **1박**으로 센다. `clock-seven-beat-wait`는 8박에 Tap, `offbeat-single-tap`은 1.5박에 Tap한다. 입력 구간은 색이 바뀌는 반응 위치 표시, 실시간 문구, 타임라인과 박자 목록으로 안내한다.
- Hold는 끝까지 유지, Dive는 유지 후 끝 박에 떼기, Flick은 미리 누른 뒤 해당 박에 튕겨 떼기, Shake는 구간 안에 한 번 왕복으로 구분한다.
- `MonsterPreview`는 원래 `MonsterPatternDefinition`으로 독립된 계획과 미해결 노트를 만든다. 미리보기에 입력·판정·HP 처리를 하지 않는다. 본체 포즈는 `MonsterAttackSprites.Body`, 공격은 전투와 같은 `MonsterAttackCatalog`·`MonsterAttackTimeline`을 사용한다. 코드에 박자별 시간을 다시 적지 않는다.
- 네코마타는 선택한 한 패턴을 재생한다. 전투의 정박/엇박 연결 묶음 전체를 재생하는 기능은 아니다.
- 전투에서 열면 기존 DSP 시계·입력·판정은 일시정지 상태를 유지한다. 닫아도 메뉴에 남고 **이어하기**를 눌러 재개한다. 누르고 있었다면 기존 재접촉 절차를 유지한다. 미리보기 소리와 시계는 독립적이며 닫기·패턴 변경 때 정리한다.

## 이미지 넣기

별도 배포한 `BBSB_EncyclopediaIcons.zip`의 `Assets` 폴더를 프로젝트와 합친다. 파일별 대응은 [MonsterCodexIcons.json](MonsterCodexIcons.json)에 있다. PNG는 도감 코드 커밋에 포함하지 않는다.

| 용도 | 파일 경로 |
|---|---|
| 그리드·상세 헤더·평상시 아이콘 | `Assets/BBSB/Resources/BBSB/Codex/Monsters/{monster-id}.png` |
| 패턴 아이콘 | `Assets/BBSB/Resources/BBSB/Codex/Patterns/{pattern-id}.png` |
| 왼쪽 본체·공격 모습 | 기존 `Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/` 경로 |

새 얼굴 초상 12개와 기존 공격 장면을 활용한 패턴 아이콘 24개를 사용한다. 본체와 공격은 앞서 배포한 `BBSB_AllMonsterArt.zip`을 그대로 사용한다. 새 도감 아이콘이 없으면 본체/공격 이미지로, 본체도 없으면 기존 전투 이미지 또는 이름으로 표시한다.

`MonsterCodexImporter`는 새 PNG를 Single Sprite, 입력 알파 유지, Clamp, Bilinear, 밉맵 없음, 압축 없음, 최대 256으로 임포트한다. 이미 Sprite로 조정한 파일은 설정을 유지한다. 원본 PNG를 자르거나 배경을 제거하지 않는다. 본체·공격의 기존 임포터와 경로는 그대로다.

## 검증

`Tools/CoreChecks`의 `MonsterPreviewTests`는 모든 종족·패턴과 60/120/200 BPM에서 Call과 입력 간격, 공격의 정시 도착, 긴 쉼과 엇박 표시, Dive 끝 떼기, 회복 구간, 반복 시점, 다른 라운드와의 분리를 검증한다.

Unity PlayMode에는 시작 전 도감 탐색/평상시 복귀/메뉴 복귀, 전투를 누른 채 멈추고 도감을 열었다 닫는 경우의 시계·HP·재접촉 보존 검사를 추가했다. 이 환경에서는 Unity Editor를 실행할 수 없어 PlayMode 실행과 실제 기기의 화면·소리 확인은 별도 확인이 필요하다.
